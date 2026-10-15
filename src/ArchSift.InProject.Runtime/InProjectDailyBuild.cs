using System.Reflection;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record InProjectBuildResult(string RunId, string CapturePath, string VerdictPath,
    bool SourceBound, string[] Limitations);

public static class InProjectDailyBuild
{
    public static async Task<InProjectBuildResult> RunAsync(string targetRoot, CancellationToken token = default,
        string? reservedRunId = null, Action<string>? phase = null)
    {
        var config = InProjectConfiguration.Load(targetRoot);
        var plan = InProjectBuildPlanner.Create(config, token);
        var baseline = InProjectHostIdentity.Capture();
        config.PackageReview?.VerifyFeed(config.LocalFeed);
        InProjectSnapshot snapshot;
        try { snapshot = InProjectSnapshots.Create(config, plan, token, reservedRunId); phase?.Invoke("snapshot"); }
        catch
        {
            if (InProjectHostIdentity.Capture().Sha256 != baseline.Sha256)
                throw new InvalidOperationException("Host environment/Profile drift detected during snapshot; evidence preserved.");
            throw;
        }
        var packageCache = Path.Combine(config.InstallationRoot, "state", "cache", "packages");
        PathSafety.EnsureNoLinks(packageCache);
        Directory.CreateDirectory(packageCache);
        var process = new InProjectBuildProcess(snapshot, baseline, packageCache, token);
        var capturePath = Path.Combine(snapshot.ReportDirectory, "daily-binding.json");
        var verdictPath = Path.Combine(snapshot.ReportDirectory, "binding-verdict.json");
        try
        {
            phase?.Invoke("restore");
            var sdk = (await process.DotnetAsync("sdk", snapshot.SourceDirectory, ["--version"])).Trim();
            if (string.IsNullOrWhiteSpace(sdk)) throw new ConfigurationException("Selected SDK version is unavailable.");
            var nuget = Path.Combine(snapshot.RunDirectory, "NuGet.Config");
            File.WriteAllText(nuget,
                "<configuration><packageSources><clear /></packageSources></configuration>", new UTF8Encoding(false));
            var solution = Path.GetFullPath(Path.GetRelativePath(config.Root, config.SolutionPath), snapshot.SourceDirectory);
            await process.DotnetAsync("restore", snapshot.SourceDirectory,
                ["restore", solution, "--configfile", nuget, "--source", config.LocalFeed,
                 "--packages", packageCache, "--disable-parallel", "--disable-build-servers",
                 "-p:NuGetAudit=false", "-p:RestoreIgnoreFailedSources=false", "--verbosity", "minimal"]);
            config.PackageReview?.VerifyCache(packageCache);
            phase?.Invoke("build");
            VerifyPackageBuildAssets(config.PackageReview, plan, snapshot, packageCache);
            var originalHashes = snapshot.InputIdentity.Files.ToDictionary(file => file.Path, file => file.Sha256,
                StringComparer.OrdinalIgnoreCase);
            var contexts = new List<InProjectBindingContext>();
            var observed = new List<(InProjectEvaluation Evaluation, InProjectCompilerCapture Compiler)>();
            foreach (var selected in plan.Contexts)
            {
                token.ThrowIfCancellationRequested();
                var projectPath = Path.GetFullPath(selected.Project.Id.Replace('/', Path.DirectorySeparatorChar),
                    snapshot.SourceDirectory);
                var projectDirectory = Path.GetDirectoryName(projectPath)!;
                var key = ContentHash.Text(selected.Project.Id + "\0" + selected.TargetFramework)[..16];
                var preprocessed = Path.Combine(snapshot.ReportDirectory, "preprocess-" + key + ".xml");
                var afterPreprocessed = Path.Combine(snapshot.ReportDirectory, "preprocess-after-" + key + ".xml");
                await process.DotnetAsync("preprocess-" + key, projectDirectory,
                    ["msbuild", projectPath, "-preprocess:" + preprocessed,
                     "-property:TargetFramework=" + selected.TargetFramework,
                     "-property:Configuration=" + selected.Configuration,
                     "-property:BuildProjectReferences=false"]);
                var beforePreprocessHash = LocalPackage.HashFile(preprocessed);
                var before = InProjectEvaluations.Parse(await process.DotnetAsync("evaluate-" + key, projectDirectory,
                    EvaluationArguments(projectPath, selected)), projectPath, selected.TargetFramework, selected.Configuration);
                ValidateEvaluatedPaths(snapshot, packageCache, before, config);
                var buildLog = await process.DotnetAsync("build-" + key, projectDirectory,
                    ["build", projectPath, "--no-restore", "--disable-build-servers",
                     "-p:UseSharedCompilation=false", "-p:BuildProjectReferences=false",
                     "-f", selected.TargetFramework, "-c", selected.Configuration, "--verbosity", "normal"]);
                var compiler = InProjectCompilerCaptures.Parse(buildLog, projectDirectory);
                if (!PathSafety.IsUnder(compiler.CompilerPath, DotnetRoot()))
                    throw new ConfigurationException("Compiler executable is outside the selected .NET SDK.");
                InProjectCompilerCaptures.VerifyAgainstEvaluation(compiler, before, snapshot.SourceDirectory,
                    originalHashes, config.PackageReview, packageCache);
                var after = InProjectEvaluations.Parse(await process.DotnetAsync("evaluate-after-" + key, projectDirectory,
                    EvaluationArguments(projectPath, selected)), projectPath, selected.TargetFramework, selected.Configuration);
                InProjectEvaluations.VerifyStable(before, after);
                await process.DotnetAsync("preprocess-after-" + key, projectDirectory,
                    ["msbuild", projectPath, "-preprocess:" + afterPreprocessed,
                     "-property:TargetFramework=" + selected.TargetFramework,
                     "-property:Configuration=" + selected.Configuration,
                     "-property:BuildProjectReferences=false"]);
                if (LocalPackage.HashFile(afterPreprocessed) != beforePreprocessHash)
                    throw new ConfigurationException("Effective MSBuild imports changed during compilation.");
                if (!File.Exists(before.TargetPath) ||
                    LocalPackage.HashFile(before.TargetPath) != compiler.IntermediateOutputSha256)
                    throw new ConfigurationException("Actual compiler output differs from selected project output.");
                var outputFacts = AssemblyArtifacts.Inspect(before.TargetPath);
                if (outputFacts.Framework != ".NETCoreApp,Version=v" + selected.TargetFramework[3..] ||
                    outputFacts.Configuration != selected.Configuration || outputFacts.Name != selected.Project.AssemblyName)
                    throw new ConfigurationException("Produced assembly metadata differs from selected context.");
                var closure = new List<string>();
                var framework = before.References.Select(reference => reference.CompilerPath)
                    .Where(path => path.Replace('\\', '/').Contains("/packs/Microsoft.NETCore.App.Ref/", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                ProjectContextEvidence.ValidateClosure(
                    [before.TargetPath, .. before.References.Select(reference => reference.CompilerPath)],
                    closure, framework, before.TargetPath);
                if (closure.Count > 0) throw new ConfigurationException("Resolved dependency closure is incomplete: " + string.Join("; ", closure));
                contexts.Add(Context(config, snapshot, selected, before, compiler, preprocessed, packageCache));
                observed.Add((before, compiler));
            }
            InputCapture.VerifyUnchanged(config.Root, snapshot.InputIdentity, token);
            InProjectSnapshots.VerifySnapshot(snapshot.SourceDirectory, snapshot.InputIdentity);
            var current = InProjectConfiguration.Load(config.Root);
            if (current.ProjectSha256 != config.ProjectSha256 || current.RulesSha256 != config.RulesSha256 ||
                current.ToolSha256 != config.ToolSha256)
                throw new ConfigurationException("Rules, configuration or tool changed during the build.");
            var assets = plan.Contexts.Select(context => Path.Combine(snapshot.SourceDirectory,
                    Path.GetDirectoryName(context.Project.Id.Replace('/', Path.DirectorySeparatorChar)) ?? "",
                    "obj", "project.assets.json"))
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)
                .Select(LocalPackage.HashFile).ToArray();
            var binding = new InProjectDailyBinding(1, "ArchSift.InProject", snapshot.RunId,
                ContentHash.Text(config.Root), Path.GetRelativePath(config.Root, config.SolutionPath).Replace('\\', '/'),
                config.Configuration, snapshot.InputIdentity.Sha256, config.RulesSha256, config.ToolSha256,
                sdk, new("succeeded", [config.LocalFeed], ContentHash.Text(string.Join("\n", assets))),
                contexts.ToArray(), snapshot.InputIdentity.Sha256, InputCapture.Capture(config.Root, token).Sha256);
            File.WriteAllText(capturePath, JsonSerializer.Serialize(binding, JsonContract.Options) + "\n", new UTF8Encoding(false));
            phase?.Invoke("bind");
            var verdict = InProjectBindingVerifier.Verify(config, plan, snapshot, binding, observed);
            if (InProjectHostIdentity.Capture().Sha256 != baseline.Sha256)
                throw new InvalidOperationException("Host environment/Profile drift detected after build; evidence preserved.");
            File.WriteAllText(verdictPath, JsonSerializer.Serialize(verdict, JsonContract.Options) + "\n", new UTF8Encoding(false));
            return new(snapshot.RunId, capturePath, verdictPath, verdict.SourceBound, verdict.Limitations);
        }
        catch (Exception error) when (error is ConfigurationException or SourceChangedException or IOException or
            JsonException or InvalidOperationException or OperationCanceledException or TimeoutException)
        {
            var state = token.IsCancellationRequested ? "cancelled" : "failed";
            File.WriteAllText(Path.Combine(snapshot.ReportDirectory, "build-state.json"),
                JsonSerializer.Serialize(new { schemaVersion = 1, product = "ArchSift.InProject",
                    runId = snapshot.RunId, state, sourceBound = false, diagnostic = error.Message }, JsonContract.Options));
            File.WriteAllText(verdictPath, JsonSerializer.Serialize(new InProjectBindingVerdict(1,
                "ArchSift.InProject", snapshot.RunId, File.Exists(capturePath) ? LocalPackage.HashFile(capturePath) :
                new string('0', 64), "1", false, [error.Message]), JsonContract.Options));
            if (InProjectHostIdentity.Capture().Sha256 != baseline.Sha256)
                throw new InvalidOperationException("Host environment/Profile drift detected; evidence preserved.", error);
            throw;
        }
    }

    private static string[] EvaluationArguments(string projectPath, InProjectBuildContext selected) =>
        ["msbuild", projectPath, "-target:ResolveReferences",
         "-property:TargetFramework=" + selected.TargetFramework,
         "-property:Configuration=" + selected.Configuration,
         "-property:BuildProjectReferences=false",
         "-getProperty:MSBuildAllProjects,TargetFramework,Configuration,TargetPath",
         "-getItem:ReferencePath,ReferenceCopyLocalPaths,Compile,Analyzer,AdditionalFiles,EmbeddedResource"];

    private static void VerifyPackageBuildAssets(InProjectPackageReview? review, InProjectBuildPlan plan,
        InProjectSnapshot snapshot, string packageCache)
    {
        foreach (var project in plan.Contexts.Select(context => context.Project.Id).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var assetsPath = Path.Combine(snapshot.SourceDirectory,
                Path.GetDirectoryName(project.Replace('/', Path.DirectorySeparatorChar)) ?? "", "obj", "project.assets.json");
            if (!File.Exists(assetsPath)) throw new ConfigurationException("Fresh restore assets are missing: " + project);
            using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
            foreach (var package in assets.RootElement.GetProperty("targets").EnumerateObject()
                         .SelectMany(target => target.Value.EnumerateObject()))
                foreach (var item in package.Value.EnumerateObject().Where(item =>
                             item.Name.StartsWith("build", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Equals("analyzers", StringComparison.OrdinalIgnoreCase)))
                    foreach (var asset in item.Value.EnumerateObject())
                    {
                        if (review is null)
                            throw new ConfigurationException("Package build tasks or analyzers require reviewed input evidence.");
                        review.VerifyAsset(packageCache, package.Name, asset.Name);
                    }
            foreach (var package in assets.RootElement.GetProperty("libraries").EnumerateObject())
                if (package.Value.TryGetProperty("files", out var files))
                    foreach (var asset in files.EnumerateArray().Select(file => file.GetString()!)
                                 .Where(file => file.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase) &&
                                     file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (review is null)
                            throw new ConfigurationException("Package generators require reviewed input evidence.");
                        review.VerifyAsset(packageCache, package.Name, asset);
                    }
        }
    }

    private static void ValidateEvaluatedPaths(InProjectSnapshot snapshot, string packageCache,
        InProjectEvaluation evaluation, InProjectLoadedConfiguration config)
    {
        var sdkRoot = DotnetRoot();
        var files = evaluation.References.SelectMany(reference => new[] { reference.Path, reference.CompilerPath })
            .Concat(evaluation.CopyLocal.Select(file => file.Path)).Concat(evaluation.Compile.Select(file => file.Path))
            .Concat(evaluation.Analyzers.Select(file => file.Path)).Concat(evaluation.AdditionalFiles.Select(file => file.Path))
            .Concat(evaluation.EmbeddedResources.Select(file => file.Path)).Concat(evaluation.Imports.Select(file => file.Path));
        foreach (var file in files)
        {
            if (!PathSafety.IsUnder(file, snapshot.SourceDirectory) &&
                !PathSafety.IsUnder(file, packageCache) && !PathSafety.IsUnder(file, sdkRoot))
                throw new ConfigurationException("MSBuild input escaped fresh snapshot, SDK or owned package cache: " + file);
            PathSafety.EnsureNoLinks(file);
            if (PathSafety.IsUnder(file, packageCache) &&
                (evaluation.Imports.Any(item => item.Path == file) ||
                 evaluation.Analyzers.Any(item => item.Path == file) ||
                 evaluation.Compile.Any(item => item.Path == file)))
            {
                var hash = LocalPackage.HashFile(file);
                if (config.PackageReview?.AllowsPackageFile(packageCache, file, hash) != true &&
                    !(evaluation.Analyzers.Any(item => item.Path == file) &&
                      InProjectBundledReferenceInputs.AllowsAnalyzer(config.InstallationRoot, packageCache, file, hash)))
                    throw new ConfigurationException("Unreviewed evaluated package input: " + file);
            }
        }
    }

    private static string DotnetRoot()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        if (!File.Exists(Path.Combine(root, "dotnet.exe")))
            throw new ConfigurationException("The supported Windows x64 .NET SDK root is unavailable.");
        PathSafety.EnsureNoLinks(root);
        return root;
    }

    private static InProjectBindingContext Context(InProjectLoadedConfiguration config, InProjectSnapshot snapshot,
        InProjectBuildContext selected, InProjectEvaluation evaluation, InProjectCompilerCapture compiler,
        string preprocessedPath, string packageCache)
    {
        var sourceHashes = snapshot.InputIdentity.Files.ToDictionary(file => file.Path, file => file.Sha256,
            StringComparer.OrdinalIgnoreCase);
        var materials = new List<InProjectBindingMaterial>();
        foreach (var file in compiler.Sources)
        {
            var relative = Path.GetRelativePath(snapshot.SourceDirectory, file.Path).Replace('\\', '/');
            materials.Add(new(file.Path, sourceHashes.ContainsKey(relative) ? "source" :
                PathSafety.IsUnder(file.Path, packageCache) ? "resource" : "generated-source", file.Sha256));
        }
        materials.AddRange(compiler.Analyzers.Select(file => new InProjectBindingMaterial(file.Path, "analyzer", file.Sha256)));
        materials.AddRange(compiler.AdditionalFiles.Select(file => new InProjectBindingMaterial(file.Path, "additional-file", file.Sha256)));
        materials.AddRange(compiler.OtherInputs.Select(file => new InProjectBindingMaterial(file.Path, "resource", file.Sha256)));
        materials.AddRange(compiler.ResponseFiles.Select(file => new InProjectBindingMaterial(file.Path, "response-file", file.Sha256)));
        materials.AddRange(evaluation.Imports.Select(file => new InProjectBindingMaterial(file.Path, "import", file.Sha256)));
        materials.Add(new(preprocessedPath, "import", LocalPackage.HashFile(preprocessedPath)));
        materials.Add(new(compiler.CompilerPath, "sdk", compiler.CompilerSha256));
        if (config.PackageReview is not null)
            materials.Add(new(config.PackageReview.Path, "task", config.PackageReview.Sha256));
        var assets = Path.Combine(Path.GetDirectoryName(evaluation.ProjectPath)!, "obj", "project.assets.json");
        materials.Add(new(assets, "assets", LocalPackage.HashFile(assets)));
        var references = evaluation.References.Select(reference =>
                new InProjectBindingReference(reference.CompilerPath, reference.AssemblyIdentity,
                    reference.CompilerSha256, "compile"))
            .Concat(evaluation.CopyLocal.Select(file => new InProjectBindingReference(file.Path,
                ManagedIdentity(file.Path), file.Sha256, "copy-local"))).ToArray();
        return new(Path.GetRelativePath(config.Root, Path.GetFullPath(selected.Project.Id, config.Root)).Replace('\\', '/'),
            selected.TargetFramework, selected.Configuration,
            ContentHash.Text(selected.Configuration + "\0" + selected.TargetFramework + "\0BuildProjectReferences=false"),
            compiler.CommandSha256, compiler.CompilerPath, materials.ToArray(), references,
            evaluation.TargetPath, LocalPackage.HashFile(evaluation.TargetPath));
    }

    private static string ManagedIdentity(string path)
    {
        try { return AssemblyName.GetAssemblyName(path).FullName!; }
        catch (BadImageFormatException) { return "native/" + Path.GetFileName(path); }
    }
}
