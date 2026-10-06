using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class IsolatedBuild
{
    public static async Task<AssemblyInputs> BuildAsync(RunConfiguration config, ProjectSnapshot snapshot, CancellationToken token)
    {
        var root = config.Target.Root;
        PathSafety.EnsureDisjoint(root, config.Output.Directory);
        var tfm = config.Build.TargetFramework ?? snapshot.Projects.SelectMany(p => p.Frameworks).Distinct().SingleOrDefault();
        if (tfm is not ("net8.0" or "net9.0" or "net10.0")) throw new ConfigurationException("Isolated build needs a supported explicit/unambiguous TFM.");
        if (snapshot.Projects.Length == 0 || snapshot.Projects.Any(p => !p.Supported || !p.Frameworks.Contains(tfm, StringComparer.Ordinal)))
            throw new ConfigurationException("All isolated projects must support the selected TFM.");
        ValidateSnapshotPaths(root, snapshot.InputIdentity);
        InputCapture.VerifyUnchanged(root, snapshot.InputIdentity, token);
        var run = Path.Combine(config.Output.Directory, "build", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(run, "source"); var output = Path.Combine(run, "output");
        Directory.CreateDirectory(source); Directory.CreateDirectory(output);
        foreach (var file in snapshot.InputIdentity.Files)
        {
            token.ThrowIfCancellationRequested();
            var from = PathSafety.Under(root, file.Path); var to = PathSafety.Under(source, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(from, to);
            if (AssemblyArtifacts.FileHash(to) != file.Sha256) throw new SourceChangedException();
        }
        foreach (var sentinel in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
            if (!File.Exists(Path.Combine(source, sentinel))) File.WriteAllText(Path.Combine(source, sentinel), "<Project />", new UTF8Encoding(false));
        // Do not inherit NuGet sources from the snapshot's parents.
        var nuget = Path.Combine(run, "NuGet.Config");
        File.WriteAllText(nuget, "<configuration><packageSources><clear /></packageSources></configuration>", new UTF8Encoding(false));
        var cliHome = Path.Combine(run, "cli-home");
        var packageCache = config.Build.CacheDirectory ?? Path.Combine(config.Output.Directory, "cache", "packages");
        PathSafety.EnsureDisjoint(root, packageCache);
        var environment = new Dictionary<string, string> { ["NUGET_PACKAGES"] = packageCache };
        var sdk = await SafeProcess.RunAsync(SafeProcess.Dotnet, source, ["--version"], cliHome, null, token, environment);
        if (sdk.ExitCode != 0) throw new IOException("SDK resolution failed: " + sdk.Error);
        var entries = new Dictionary<string, AssemblyEntry>(StringComparer.Ordinal);
        foreach (var project in snapshot.Projects)
        {
            token.ThrowIfCancellationRequested();
            var projectPath = PathSafety.Under(source, project.Id);
            var restore = new List<string> { "restore", projectPath, "--configfile", nuget, "--packages", packageCache,
                "--disable-build-servers", "-p:NuGetAudit=false", "-p:RestoreIgnoreFailedSources=false", "-p:TargetFramework=" + tfm };
            if (config.Build.AllowNetwork)
                foreach (var feed in config.Build.Sources) { restore.Add("--source"); restore.Add(feed); }
            else if (config.Build.LocalFeed is { } feed) { restore.Add("--source"); restore.Add(feed); }
            var restored = await SafeProcess.RunAsync(SafeProcess.Dotnet, source, restore.ToArray(), cliHome, null, token, environment);
            if (restored.ExitCode != 0) throw new IOException("Offline/explicit restore failed: " + restored.Error + restored.Output);
            var assetsPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj", "project.assets.json");
            if (File.Exists(assetsPath))
            {
                using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
                if (assets.RootElement.GetProperty("targets").EnumerateObject().SelectMany(t => t.Value.EnumerateObject()).Any(p =>
                        p.Value.EnumerateObject().Any(a => a.Name is "build" or "buildTransitive" or "analyzers")))
                    throw new ConfigurationException("Package build tasks/analyzers require external reviewed build evidence.");
                if (assets.RootElement.GetProperty("libraries").EnumerateObject().Any(p => p.Value.TryGetProperty("files", out var fileList) &&
                    fileList.EnumerateArray().Any(f => f.GetString()!.StartsWith("analyzers/", StringComparison.Ordinal))))
                    throw new ConfigurationException("Package source generators/analyzers require external reviewed build evidence.");
            }
            var projectOutput = Path.Combine(output, ContentHash.Text(project.Id)[..12]);
            var built = await SafeProcess.RunAsync(SafeProcess.Dotnet, source,
                ["build", projectPath, "--no-restore", "--disable-build-servers", "-p:UseSharedCompilation=false", "-c", config.Build.Configuration,
                 "-f", tfm, "-o", projectOutput], cliHome, null, token, environment);
            if (built.ExitCode != 0) throw new IOException("Isolated build failed: " + built.Error + built.Output);
            var primary = Path.Combine(projectOutput, project.AssemblyName + ".dll");
            if (!File.Exists(primary)) throw new IOException("Expected assembly was not produced: " + project.AssemblyName);
            foreach (var file in Directory.GetFiles(projectOutput, "*.dll"))
            {
                var info = AssemblyArtifacts.Inspect(file);
                var sourceProject = snapshot.Projects.FirstOrDefault(p => p.AssemblyName == info.Name)?.Id ?? "@dependency";
                var copied = Path.Combine(output, info.Name + ".dll");
                if (entries.TryGetValue(info.Name, out var prior) && AssemblyArtifacts.FileHash(file) != prior.Sha256)
                    throw new IOException("Different assembly versions share an identity: " + info.Name);
                File.Copy(file, copied, true);
                entries[info.Name] = new(info.Name, sourceProject, Path.GetFileName(copied), AssemblyArtifacts.FileHash(copied));
            }
        }
        InputCapture.VerifyUnchanged(root, snapshot.InputIdentity, token);
        var buildFiles = snapshot.InputIdentity.Files.Select(f => f with { Kind = "build" }).ToArray();
        var manifest = new AssemblyManifest
        {
            InputIdentity = snapshot.InputIdentity, BuildInputIdentity = new(AssemblyArtifacts.FileIdentity(buildFiles), buildFiles),
            Sdk = sdk.Output.Trim(), TargetFramework = tfm, Configuration = config.Build.Configuration, Generation = "isolated",
            Assemblies = entries.Values.OrderBy(a => a.AssemblyName, StringComparer.Ordinal).ToArray()
        };
        var manifestPath = Path.Combine(output, "assembly-manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonContract.Options), new UTF8Encoding(false));
        return AssemblyArtifacts.Existing(config with { Build = config.Build with { AssemblyManifest = manifestPath } }, snapshot);
    }

    private static void ValidateSnapshotPaths(string root, InputIdentity identity)
    {
        foreach (var file in identity.Files.Where(f => Path.GetExtension(f.Path) is ".csproj" or ".props" or ".targets"))
        {
            var xml = InputCapture.ReadXml(PathSafety.Under(root, file.Path));
            if (xml.Descendants().Any(n => n.Attribute("Condition") is not null || n.Attributes().Any(a => a.Value.Contains("$(", StringComparison.Ordinal)) ||
                    (!n.HasElements && n.Value.Contains("$(", StringComparison.Ordinal))))
                throw new ConfigurationException("Conditional/expression build context requires external reviewed build evidence.");
            if (Path.GetExtension(file.Path) == ".csproj" &&
                (string?)xml.Root?.Attribute("Sdk") is not ("Microsoft.NET.Sdk" or "Microsoft.NET.Sdk.Web" or "Microsoft.NET.Sdk.Razor"))
                throw new ConfigurationException("Custom SDK requires external reviewed build evidence.");
            if (xml.Descendants().Any(n => n.Name.LocalName is "Target" or "UsingTask" or "Import"))
                throw new ConfigurationException("Custom targets/tasks/imports require external reviewed build evidence: " + file.Path);
            if (xml.Descendants().Any(n => n.Name.LocalName is "OutputPath" or "BaseOutputPath" or "BaseIntermediateOutputPath" or
                    "IntermediateOutputPath" or "MSBuildProjectExtensionsPath" or "OutDir" or "ArtifactsPath" or "RestorePackagesPath" or
                    "NuGetPackageRoot" or "CompilerGeneratedFilesOutputPath"))
                throw new ConfigurationException("Custom output/cache paths cannot be safely isolated: " + file.Path);
            foreach (var attribute in xml.Descendants().Attributes().Where(a => a.Name.LocalName is "Include" or "Update" or "Project"))
                if (Path.IsPathRooted(attribute.Value) || attribute.Value.Contains("$(", StringComparison.Ordinal) ||
                    attribute.Value.Contains("@(", StringComparison.Ordinal) || attribute.Value.Contains("%(", StringComparison.Ordinal))
                    throw new ConfigurationException("Absolute/expression MSBuild input paths cannot be isolated: " + file.Path);
        }
    }
}
