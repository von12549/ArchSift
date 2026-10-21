using System.Text.Json;
using System.Xml.Linq;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Testing;

namespace ArchSift.TestGate;

public sealed record ToolFile(string Path, string Sha256);
public sealed record FixedToolManifest(int SchemaVersion, string Identity, string SourceCommit, ToolFile[] Files);
public sealed record LocalTestLaunch(int SchemaVersion, string TargetRoot, string ToolRoot, string StateRoot,
    string LocalFeed, string PackageCache, string GateTemplate, string LocalProps, string ToolManifest,
    string ToolManifestSha256, string ToolIdentity);
public sealed record ResolvedTestLaunch(TestGateConfig Config, string ConfigPath, string CliPath, string WebPath);

public static class TestLaunch
{
    public static ResolvedTestLaunch Prepare(string localConfigPath, string runName)
    {
        if (string.IsNullOrWhiteSpace(runName) || runName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new ConfigurationException("Run name must contain only ASCII letters, digits or hyphens.");
        var localPath = Path.GetFullPath(localConfigPath);
        PathSafety.EnsureNoLinks(localPath);
        var local = JsonSerializer.Deserialize<LocalTestLaunch>(File.ReadAllBytes(localPath), JsonContract.Options)
            ?? throw new ConfigurationException("Local launch config is empty.");
        if (local.SchemaVersion != 1) throw new ConfigurationException("Unsupported local launch schema.");
        var directory = Path.GetDirectoryName(localPath)!;
        var root = Resolve(local.TargetRoot);
        var tool = Resolve(local.ToolRoot);
        var state = Resolve(local.StateRoot);
        PathSafety.EnsureDisjoint(root, tool);
        PathSafety.EnsureDisjoint(root, state);
        PathSafety.EnsureDisjoint(tool, state);
        // MSBuild property lists interpret these characters even with ProcessStartInfo.ArgumentList.
        if (tool.IndexOfAny([';', ',', '%', '"']) >= 0) throw new ConfigurationException("Tool root contains MSBuild property syntax.");
        _ = VerifyTool(tool, local.ToolManifest, local.ToolManifestSha256, local.ToolIdentity);
        var propsPath = PathSafety.Under(root, local.LocalProps);
        var props = XDocument.Load(propsPath);
        var configured = props.Descendants("ArchSiftLocalToolRoot").SingleOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured) ||
            !Path.GetFullPath(configured).Equals(tool, PathSafety.Comparison))
            throw new ConfigurationException("Local props and launch tool roots conflict or are missing.");
        var templatePath = PathSafety.Under(root, local.GateTemplate);
        var template = TestGateService.Load(templatePath);
        var run = PathSafety.Under(state, "runs/" + runName);
        if (Directory.Exists(run) || File.Exists(run)) throw new ConfigurationException("Run directory already exists.");
        var config = template with
        {
            TargetRoot = root, ToolRoot = tool, ToolIdentity = local.ToolIdentity,
            ToolManifest = local.ToolManifest, ToolManifestSha256 = local.ToolManifestSha256,
            LocalFeed = Resolve(local.LocalFeed), PackageCache = Resolve(local.PackageCache),
            OriginalBaseline = PathSafety.Under(state, template.OriginalBaseline),
            CaseMapping = template.CaseMapping is null ? null : PathSafety.Under(state, template.CaseMapping),
            RunDirectory = run, DotnetCliHome = template.DotnetCliHome is null ? null : PathSafety.Under(run, template.DotnetCliHome)
        };
        if (config.PolicyFile is null || config.CaseMapping is null || config.ToolIdentity is null)
            throw new ConfigurationException("Formal launch requires policy and reviewed worktree case mapping.");
        var nativePolicy = NativePolicy.Load(root, config.PolicyFile);
        if (nativePolicy.Sha256 != config.PolicySha256 ||
            !nativePolicy.Paths.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(config.Rulesets.Select(path => PathSafety.Under(root, path))))
            throw new ConfigurationException("Launch template does not match the approved native policy.");
        _ = CaseMapping.Load(config);
        TestGateService.Validate(config);
        foreach (var path in new[] { config.TestProject, config.NuGetConfig }.Concat(config.Rulesets))
            if (!File.Exists(PathSafety.Under(root, path))) throw new ConfigurationException("Required target input is missing.");
        if (!Directory.Exists(config.LocalFeed) || !Directory.Exists(config.PackageCache) ||
            !File.Exists(config.OriginalBaseline) || ContentHash.Bytes(File.ReadAllBytes(config.OriginalBaseline)) != config.OriginalBaselineSha256)
            throw new ConfigurationException("Required local package or baseline input is missing or changed.");
        var configPath = PathSafety.Under(state, "config/" + runName + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        using (var stream = new FileStream(configPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(stream, config, JsonContract.Options);
        // Neither the run nor the CLI home is created here; gate owns create-new validation.
        return new(config, configPath, PathSafety.Under(tool, "cli/archsift.dll"), PathSafety.Under(tool, "web/ArchSift.Web.dll"));

        string Resolve(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ConfigurationException("Missing local path.");
            var path = Path.GetFullPath(value, directory);
            PathSafety.EnsureNoLinks(path);
            return path;
        }
    }

    internal static string[] VerifyTool(string tool, string manifestRelativePath, string manifestSha256, string identity)
    {
        var manifestPath = PathSafety.Under(tool, manifestRelativePath);
        var manifestBytes = File.ReadAllBytes(manifestPath);
        if (ContentHash.Bytes(manifestBytes) != manifestSha256)
            throw new ConfigurationException("Fixed tool manifest bytes changed.");
        var manifest = JsonSerializer.Deserialize<FixedToolManifest>(manifestBytes, JsonContract.Options)
            ?? throw new ConfigurationException("Fixed tool manifest is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Identity != identity ||
            manifest.SourceCommit.Length != 40 || manifest.Files is not { Length: > 0 } ||
            manifest.Files.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Length)
            throw new ConfigurationException("Fixed tool identity is incomplete.");
        foreach (var file in manifest.Files)
            if (ContentHash.Bytes(File.ReadAllBytes(PathSafety.Under(tool, file.Path))) != file.Sha256)
                throw new ConfigurationException("Fixed tool source or payload changed: " + file.Path);
        var required = new[] { "src/ArchSift.Testing/ArchSift.Testing.csproj", "cli/archsift.dll", "web/ArchSift.Web.dll" };
        if (required.Except(manifest.Files.Select(item => item.Path), StringComparer.OrdinalIgnoreCase).Any())
            throw new ConfigurationException("Fixed tool source/CLI/Web entries are missing.");
        return manifest.Files.Select(file => PathSafety.Under(tool, file.Path)).Append(manifestPath).ToArray();
    }
}
