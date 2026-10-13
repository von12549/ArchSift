using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;

namespace ArchSift.Core;

public sealed class EvidenceJobService(string[] hostProtectedRoots)
{
    public EvidenceJobPreview Preview(RunConfiguration config, EvidenceJobRequest request)
    {
        if (!OperatingSystem.IsWindows()) throw new ConfigurationException("External evidence build jobs currently require Windows owned process groups.");
        if (config.Build.TargetFramework is not ("net8.0" or "net9.0" or "net10.0") || config.Build.AllowNetwork)
            throw new ConfigurationException("Evidence build requires an explicit supported TFM and offline restore.");
        if (!Path.IsPathFullyQualified(request.DotnetPath) || !File.Exists(request.DotnetPath) || !Directory.Exists(request.LocalFeed) ||
            !Path.IsPathFullyQualified(request.LocalFeed) || request.TimeoutSeconds is < 30 or > 3600 || request.ExternalInputRoots is null || request.EnvironmentInputNames is null ||
            request.ExternalInputRoots.Any(string.IsNullOrWhiteSpace) || request.EnvironmentInputNames.Any(string.IsNullOrWhiteSpace))
            throw new ConfigurationException("Explicit dotnet path, local feed and bounded execution settings are required.");
        PathSafety.EnsureNoLinks(request.DotnetPath); PathSafety.EnsureNoLinks(request.LocalFeed);
        var storage = EvidenceWorkbench.Plan(config, request.Storage, hostProtectedRoots);
        var source = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework);
        if (source.Projects.Length == 0 || source.Projects.Any(p => !p.Supported)) throw new ConfigurationException("Every evidence project must be supported SDK-style C#.");
        var projects = Order(source.Projects); var snapshot = Path.Combine(storage.RunDirectory, "snapshot");
        var propertyNames = ExternalBuildEvidence.PropertyNames(source, config.Target.Root);
        var commands = new List<EvidenceCommand> { new("sdk", request.DotnetPath, ["--version"], snapshot) };
        foreach (var project in projects)
        {
            var path = PathSafety.Under(snapshot, project.Id);
            commands.Add(new("restore", request.DotnetPath, RestoreArguments(path, storage.RunDirectory, request, config), Path.GetDirectoryName(path)!));
        }
        foreach (var project in projects)
        {
            var path = PathSafety.Under(snapshot, project.Id);
            commands.Add(new("build", request.DotnetPath, BuildArguments(path, config), Path.GetDirectoryName(path)!));
        }
        foreach (var project in projects)
        {
            var path = PathSafety.Under(snapshot, project.Id);
            commands.Add(new("evaluate", request.DotnetPath, EvaluationArguments(path, config, propertyNames), Path.GetDirectoryName(path)!));
        }
        var risks = source.Projects.SelectMany(p => p.Limitations).Distinct().Concat(new[] {
            "MSBuild snapshot is not an OS sandbox. Review imports, generators and custom tasks; application/Test/VSTest entry points are not authorized.",
            "BuildProjectReferences=false uses reviewed per-project dependency order; this is not deployment or complete solution configuration certification.",
            "Offline NuGetAudit=false is a build coverage setting, not vulnerability verification. Binary traces are local confidential review material." }).ToArray();
        var preview = new EvidenceJobPreview(storage, config.Target.Root, config.Target.Entry, source.InputIdentity.Sha256,
            projects.Length, projects.Select(p => p.Id).ToArray(), AssemblyArtifacts.FileHash(request.DotnetPath), config.Build.TargetFramework,
            config.Build.Configuration, risks, commands.ToArray(), "");
        var executionRequest = request with { ReviewedBuildLogic = false, ExecutionReviewer = "", ExecutionReason = "" };
        return preview with { PreviewSha256 = ContentHash.Text(JsonSerializer.Serialize(new { preview, request = executionRequest }, JsonContract.Options)) };
    }

    public async Task<EvidenceJobRecord> BuildAsync(RunConfiguration config, EvidenceJobRequest request, string previewSha256,
        Action<EvidenceJobView>? progress = null, CancellationToken token = default)
    {
        var preview = Preview(config, request);
        if (preview.PreviewSha256 != previewSha256 || !request.ReviewedBuildLogic || string.IsNullOrWhiteSpace(request.ExecutionReviewer) || string.IsNullOrWhiteSpace(request.ExecutionReason))
            throw new ConfigurationException("Start requires the unchanged execution preview and an actual execution reviewer/reason.");
        var source = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework, token);
        if (source.InputIdentity.Sha256 != preview.SourceIdentity) throw new SourceChangedException();
        var propertyNames = ExternalBuildEvidence.PropertyNames(source, config.Target.Root);
        var root = preview.Storage.RunDirectory; var snapshotRoot = Path.Combine(root, "snapshot");
        Directory.CreateDirectory(root);
        EvidenceArtifactStore.WriteNew(Path.Combine(root, "owner.json"), new { kind = "archsift-evidence-run", projectId = request.Storage.ProjectId, runId = request.Storage.RunId, targetRoot = config.Target.Root, entry = config.Target.Entry });
        RegisterProject(preview.Storage, config);
        var host = EvidenceHostState.Capture(); EvidenceArtifactStore.WriteNew(Path.Combine(root, "host-baseline.json"), host);
        var watch = Stopwatch.StartNew(); var executions = new List<EvidenceExecution>(); var unknowns = new List<string>();
        using var hashTelemetry = new EvidenceHashTelemetry();
        var job = new EvidenceJobRecord(1, "evidence-job", request.Storage.RunId, "planned", DateTimeOffset.UtcNow.ToString("O"), null, 0, 0,
            request, config, preview, [], null, null, [], null, host.Identity, ToolIdentity.Version);
        void Update(string stage)
        {
            job = job with { Stage = stage, ElapsedMilliseconds = watch.ElapsedMilliseconds, Executions = executions.ToArray(), Unknowns = unknowns.Distinct().ToArray() };
            EvidenceArtifactStore.SaveJob(job); progress?.Invoke(View(job));
        }
        void Check()
        {
            host.Verify(); InputCapture.VerifyUnchanged(config.Target.Root, source.InputIdentity, token);
            if (AssemblyArtifacts.FileHash(request.DotnetPath, token) != preview.ToolSha256) throw new SourceChangedException();
        }
        async Task<ProcessResult> Execute(string stage, string directory, string[] arguments)
        {
            Check(); Update(stage); var attempt = executions.Count + 1; var start = DateTimeOffset.UtcNow.ToString("O"); int? pid = null;
            var stdout = Path.Combine(root, "logs", attempt.ToString("D4") + "-" + stage + ".stdout.log"); var stderr = Path.ChangeExtension(stdout, ".stderr.log");
            Directory.CreateDirectory(Path.GetDirectoryName(stdout)!);
            var environment = new Dictionary<string, string> { ["NUGET_PACKAGES"] = Path.Combine(root, "packages") };
            ProcessResult? result = null; Exception? failure = null;
            try
            {
                result = await SafeProcess.RunAsync(request.DotnetPath, directory, arguments, Path.Combine(root, "cli-home"), null, token,
                    environment, TimeSpan.FromSeconds(request.TimeoutSeconds), true, id =>
                    {
                        pid = id;
                        executions.Add(new(attempt, stage, start, "", id, null, arguments, stdout, stderr, false, false, false, true)); Update(stage);
                    }, partial =>
                    {
                        result = partial;
                        EvidenceArtifactStore.Log(stdout, partial.Output); EvidenceArtifactStore.Log(stderr, partial.Error);
                    });
                if (stage != "evaluating") EvidenceArtifactStore.Log(stdout, result.Output);
                else EvidenceArtifactStore.WriteNew(stdout, new { rawOutputSha256 = ContentHash.Text(result.Output), structuredProjectionFollows = true });
                EvidenceArtifactStore.Log(stderr, result.Error);
                if (result.Output.Contains("[output truncated]", StringComparison.Ordinal) || result.Error.Contains("[output truncated]", StringComparison.Ordinal))
                    throw new IOException("Actual tool output exceeded its bounded limit; no complete evidence is claimed.");
                if (result.ExitCode != 0) throw new IOException(stage + " command exited " + result.ExitCode + "; inspect the preserved attempt logs.");
                return result;
            }
            catch (Exception error)
            {
                failure = error;
                if (!File.Exists(stdout)) EvidenceArtifactStore.Log(stdout, "No complete stdout was produced. " + error.GetType().Name);
                if (!File.Exists(stderr)) EvidenceArtifactStore.Log(stderr, "Attempt did not complete. " + error.GetType().Name);
                throw;
            }
            finally
            {
                var equal = EvidenceHostState.Capture().Identity == host.Identity;
                var terminal = new EvidenceExecution(attempt, stage, start, DateTimeOffset.UtcNow.ToString("O"), pid, result?.ExitCode, arguments, stdout, stderr,
                    failure is OperationCanceledException, failure is TimeoutException, result?.OwnedTreeStopped ?? failure is OperationCanceledException or TimeoutException, equal);
                if (executions.Count >= attempt) executions[attempt - 1] = terminal; else executions.Add(terminal);
                Update(stage);
                if (!equal) throw new HostDriftException();
            }
        }
        try
        {
            Update("snapshotting"); Directory.CreateDirectory(snapshotRoot);
            foreach (var file in source.InputIdentity.Files)
            {
                token.ThrowIfCancellationRequested(); var from = PathSafety.Under(config.Target.Root, file.Path); var to = PathSafety.Under(snapshotRoot, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(from, to, false);
                if (AssemblyArtifacts.FileHash(to, token) != file.Sha256) throw new SourceChangedException();
            }
            if (InputCapture.Capture(snapshotRoot, token).Sha256 != source.InputIdentity.Sha256) throw new SourceChangedException();
            EvidenceArtifactStore.WriteNew(Path.Combine(root, "source-files.json"), source.InputIdentity);
            Check(); Update("snapshot-ready");
            // Explicit config prevents ambient network feeds; never edits the copied target's NuGet config.
            var nuget = "<configuration><packageSources><clear /><add key=\"local\" value=\"" + System.Security.SecurityElement.Escape(request.LocalFeed) + "\" /></packageSources></configuration>";
            ChainWriter.WriteNew(Path.Combine(root, "NuGet.Config"), nuget);
            Directory.CreateDirectory(Path.Combine(root, "packages"));
            var sdk = (await Execute("inspecting-sdk", snapshotRoot, ["--version"])).Output.Trim();
            var dotnetRoot = Path.GetDirectoryName(request.DotnetPath)!;
            var sdkDirectory = request.SdkDirectory ?? Path.Combine(dotnetRoot, "sdk", sdk);
            if (!string.Equals(Path.GetFullPath(sdkDirectory), Path.GetFullPath(Path.Combine(dotnetRoot, "sdk", sdk)), PathSafety.Comparison) ||
                !File.Exists(Path.Combine(sdkDirectory, "MSBuild.dll"))) throw new ConfigurationException("Actual SDK differs from the explicit SDK directory.");
            var projects = Order(source.Projects);
            foreach (var project in projects) await Execute("restoring", Path.GetDirectoryName(PathSafety.Under(snapshotRoot, project.Id))!, RestoreArguments(PathSafety.Under(snapshotRoot, project.Id), root, request, config));
            Check(); Update("capturing");
            var roots = new List<string> { request.DotnetPath, sdkDirectory, Path.Combine(root, "packages") };
            roots.AddRange(request.ExternalInputRoots);
            foreach (var folder in new[] { "sdk-manifests", "metadata", "host" }) if (Directory.Exists(Path.Combine(dotnetRoot, folder))) roots.Add(Path.Combine(dotnetRoot, folder));
            foreach (var pack in new[] { "Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref", "Microsoft.NETCore.App.Host.win-x64", "NETStandard.Library.Ref" })
            {
                var packRoot = Path.Combine(dotnetRoot, "packs", pack);
                if (Directory.Exists(packRoot)) roots.AddRange(Directory.GetDirectories(packRoot).Where(p => pack == "NETStandard.Library.Ref" || Path.GetFileName(p).StartsWith(config.Build.TargetFramework![3..^2] + ".", StringComparison.Ordinal)));
            }
            var runtimeRoot = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App");
            if (Directory.Exists(runtimeRoot)) roots.AddRange(Directory.GetDirectories(runtimeRoot).Where(p => Path.GetFileName(p).StartsWith(sdk.Split('.')[0] + ".", StringComparison.Ordinal)));
            foreach (var project in projects) if (Directory.Exists(Path.Combine(Path.GetDirectoryName(PathSafety.Under(snapshotRoot, project.Id))!, "obj"))) roots.Add(Path.Combine(Path.GetDirectoryName(PathSafety.Under(snapshotRoot, project.Id))!, "obj"));
            var external = Files(roots).Distinct(PathComparer).ToArray();
            var capture = ExternalBuildEvidence.Capture(config, external, request.EnvironmentInputNames, token);
            if (capture.PropertyNames.Length != propertyNames.Length || !capture.PropertyNames.SequenceEqual(propertyNames, StringComparer.Ordinal))
                throw new SourceChangedException();
            var capturePath = Path.Combine(root, "capture.json"); EvidenceArtifactStore.WriteNew(capturePath, capture, true);
            if (new FileInfo(capturePath).Length > RuleLoader.MaximumJsonBytes) throw new ConfigurationException("Complete capture exceeds 4 MiB; retain it and review the explicit input scope without deleting required inputs.");
            job = job with { CapturePath = capturePath }; Update("captured");
            var buildCommands = new List<string[]>(); var compiler = new List<object>(); var materials = new List<EvidenceMaterial>();
            foreach (var project in projects)
            {
                var path = PathSafety.Under(snapshotRoot, project.Id); var args = BuildArguments(path, config); buildCommands.Add(args);
                var built = await Execute("building", Path.GetDirectoryName(path)!, args);
                compiler.AddRange(CompilerInputs.Audit(built.Output, Path.GetDirectoryName(path)!, project.Id, config.Target.Root, snapshotRoot, capture, unknowns, token));
                job = job with { CompletedProjects = job.CompletedProjects + 1 }; Update("building");
            }
            var reviewedProjects = new List<ReviewedProject>(); var assemblies = new List<ReviewedAssembly>(); var contexts = new List<ReviewedAssemblyContext>();
            var imports = new HashSet<string>(PathComparer); var generators = new HashSet<string>(PathComparer);
            foreach (var project in projects)
            {
                var path = PathSafety.Under(snapshotRoot, project.Id);
                var properties = EvaluationProperties(capture.PropertyNames);
                var arguments = EvaluationArguments(path, config, capture.PropertyNames);
                var evaluated = await Execute("evaluating", Path.GetDirectoryName(path)!, arguments);
                var node = Evaluation(evaluated.Output, properties); var prop = node["Properties"]!.AsObject();
                var target = prop["TargetPath"]!.GetValue<string>();
                if (!PathSafety.IsUnder(target, snapshotRoot) || !File.Exists(target) || prop["TargetFramework"]!.GetValue<string>() != config.Build.TargetFramework || prop["Configuration"]!.GetValue<string>() != config.Build.Configuration)
                    throw new ConfigurationException("Evaluated target/TFM/configuration is outside the reviewed snapshot.");
                foreach (var value in (prop["MSBuildAllProjects"]?.GetValue<string>() ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
                    if (Path.IsPathFullyQualified(value)) imports.Add(PathSafety.IsUnder(value, snapshotRoot) && capture.InputIdentity.Files.Any(f => f.Path == Path.GetRelativePath(snapshotRoot, value).Replace('\\', '/')) ? PathSafety.Under(config.Target.Root, Path.GetRelativePath(snapshotRoot, value)) : value);
                foreach (var item in node["Items"]!["Analyzer"]!.AsArray()) if (item?["FullPath"] is { } name) generators.Add(name.GetValue<string>());
                foreach (var property in properties.Where(p => EvidenceArtifactStore.Sensitive(p) || Environment.GetEnvironmentVariables().Contains(p)))
                    if (prop[property] is { } value) prop[property] = "value-sha256:" + ContentHash.Text(value.GetValue<string>());
                var trace = Path.Combine(root, "evaluations", "project-" + ContentHash.Text(project.Id)[..16] + ".json"); EvidenceArtifactStore.WriteNew(trace, node);
                materials.Add(EvidenceArtifactStore.Material(trace, "evaluation-trace"));
                reviewedProjects.Add(new(project.Id, prop["AssemblyName"]!.GetValue<string>(), config.Build.TargetFramework!, capture.PropertyNames.ToDictionary(p => p, p => prop[p]!.GetValue<string>(), StringComparer.Ordinal)));
                assemblies.Add(new(project.Id, target));
                string[] Paths(string item) => node["Items"]![item]!.AsArray().Select(n => n!["FullPath"]!.GetValue<string>()).Distinct(PathComparer).ToArray();
                contexts.Add(new("project-" + ContentHash.Text(project.Id)[..16], project.Id, target, trace, Paths("ReferencePath")) { RuntimePaths = Paths("ReferenceCopyLocalPaths") });
                job = job with { EvaluatedProjects = job.EvaluatedProjects + 1 }; Update("evaluating");
            }
            foreach (var file in capture.ExternalInputs)
                if (!File.Exists(file.Path) || AssemblyArtifacts.FileHash(file.Path, token) != file.Sha256) unknowns.Add("Captured input changed after build: " + file.Path);
            foreach (var file in imports.Concat(generators))
                if (!capture.ExternalInputs.Any(f => string.Equals(f.Path, file, PathSafety.Comparison)) && !capture.InputIdentity.Files.Any(f => string.Equals(PathSafety.Under(config.Target.Root, f.Path), file, PathSafety.Comparison))) unknowns.Add("Input needs recapture before a new build: " + file);
            var audit = Path.Combine(root, "compiler-input-audit.json"); EvidenceArtifactStore.WriteNew(audit, new { kind = "actual-compiler-input-audit", projects = compiler }); materials.Add(EvidenceArtifactStore.Material(audit, "evaluation-trace"));
            foreach (var execution in executions) foreach (var path in new[] { execution.StdoutPath, execution.StderrPath }.Where(File.Exists)) materials.Add(EvidenceArtifactStore.Material(path, "build-log"));
            var generated = Files(projects.Select(p => Path.GetDirectoryName(PathSafety.Under(snapshotRoot, p.Id))!).SelectMany(p => new[] { Path.Combine(p, "obj"), Path.Combine(p, "bin") }).Where(Directory.Exists));
            materials.AddRange(generated.Distinct(PathComparer).Select(p => EvidenceArtifactStore.Material(p, "generated-input")));
            var notes = Path.Combine(root, "review-required.md"); ChainWriter.WriteNew(notes, "Unapproved material. Actual reviewer must inspect build causality, compiler inputs, generators/imports, privacy and unknowns.\n"); materials.Add(EvidenceArtifactStore.Material(notes, "review-notes"));
            var template = new ExternalBuildReview { SchemaVersion = 2, CaptureSha256 = AssemblyArtifacts.FileHash(capturePath), Approved = false, InputsComplete = false,
                Reviewer = "", Sdk = sdk, SdkToolPath = Path.Combine(sdkDirectory, "MSBuild.dll"), TargetFramework = config.Build.TargetFramework!, Configuration = config.Build.Configuration,
                Command = [request.DotnetPath, .. buildCommands[0]], ExitCode = 0, GlobalProperties = new() { ["Configuration"] = config.Build.Configuration, ["TargetFramework"] = config.Build.TargetFramework!, ["BuildProjectReferences"] = "false" },
                Materials = materials.DistinctBy(m => m.Path).ToArray(), Projects = reviewedProjects.ToArray(), Assemblies = assemblies.ToArray(), ProjectContexts = contexts.ToArray(), BuildSnapshotRoot = snapshotRoot,
                FrameworkPaths = contexts.SelectMany(c => c.ReferencePaths).Where(p => PathSafety.IsUnder(p, Path.Combine(dotnetRoot, "packs"))).Distinct(PathComparer).ToArray(), Imports = imports.ToArray(), GeneratorsAndTasks = generators.ToArray(),
                Unknowns = [.. unknowns.Distinct(), "Actual material review and build causality approval are required."] };
            var reviewPath = Path.Combine(root, "review-template-v2.json"); EvidenceArtifactStore.WriteNew(reviewPath, template, true);
            job = job with { ReviewTemplatePath = reviewPath };
            Check(); Update(unknowns.Count == 0 ? "review-required" : "inputs-need-recapture");
        }
        catch (HostDriftException error) { job = job with { Error = error.Message }; Update("host-drift"); }
        catch (SourceChangedException error) { job = job with { Error = error.Message }; Update("stale"); }
        catch (OperationCanceledException) { Update("cancelled"); }
        catch (Exception error)
        { job = job with { Error = error.Message }; Update("failed"); }
        finally
        {
            watch.Stop(); var final = EvidenceHostState.Capture(); EvidenceArtifactStore.WriteNew(Path.Combine(root, "host-final.json"), new { equal = final.Identity == host.Identity, hashes = final });
            if (final.Identity != host.Identity) job = job with { Stage = "host-drift", Error = "Host drift; no automatic repair." };
            job = job with { FinishedUtc = DateTimeOffset.UtcNow.ToString("O"), ElapsedMilliseconds = watch.ElapsedMilliseconds }; EvidenceArtifactStore.SaveJob(job); progress?.Invoke(View(job));
            EvidenceArtifactStore.WriteNew(Path.Combine(root, "hash-statistics.json"), new { statistics = hashTelemetry.Statistics,
                scope = "AssemblyArtifacts.FileHash reads only; InputCapture/SDK subprocess IO is not included. Timings may overlap and must not be summed." });
        }
        return job;
    }

    public static EvidenceJobView View(EvidenceJobRecord job) => new(job.Id, job.Stage, job.StartedUtc, job.FinishedUtc, job.ElapsedMilliseconds,
        job.Preview.ProjectCount, job.CompletedProjects, job.Preview.Storage.RunDirectory, job.CapturePath, job.ReviewTemplatePath, job.Unknowns, job.Error, job.Executions) { EvaluatedProjects = job.EvaluatedProjects };

    private static string[] RestoreArguments(string project, string root, EvidenceJobRequest request, RunConfiguration config) =>
        ["restore", project, "--configfile", Path.Combine(root, "NuGet.Config"), "--source", request.LocalFeed, "--packages", Path.Combine(root, "packages"), "--disable-parallel", "--disable-build-servers", "-p:NuGetAudit=false", "-p:RestoreIgnoreFailedSources=false", "-p:RestoreAdditionalProjectSources=", "-p:TargetFramework=" + config.Build.TargetFramework];
    private static string[] BuildArguments(string project, RunConfiguration config) =>
        ["build", project, "--no-restore", "--no-incremental", "--disable-build-servers", "-m:1", "-p:UseSharedCompilation=false", "-p:BuildProjectReferences=false", "-p:NuGetAudit=false", "-p:TargetFramework=" + config.Build.TargetFramework, "-c", config.Build.Configuration, "--verbosity", "normal"];
    private static string[] EvaluationProperties(string[] propertyNames) =>
        [.. new[] { "MSBuildProjectFullPath", "TargetPath", "AssemblyName", "TargetFramework", "Configuration", "MSBuildAllProjects" }.Concat(propertyNames).Distinct(StringComparer.Ordinal)];
    private static string[] EvaluationArguments(string project, RunConfiguration config, string[] propertyNames) =>
        ["msbuild", project, "-t:ResolveReferences", "-getProperty:" + string.Join(",", EvaluationProperties(propertyNames)),
            "-getItem:Compile,Analyzer,AdditionalFiles,EditorConfigFiles,ReferencePath,ReferenceCopyLocalPaths", "-m:1",
            "-p:Configuration=" + config.Build.Configuration, "-p:TargetFramework=" + config.Build.TargetFramework,
            "-p:UseSharedCompilation=false", "-p:BuildProjectReferences=false"];

    private static ProjectModel[] Order(ProjectModel[] projects)
    {
        var ordered = new List<ProjectModel>(); var pending = projects.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        while (pending.Count > 0)
        {
            var next = pending.FirstOrDefault(p => p.References.All(r => r.TargetId is null || !pending.Any(other => other.Id == r.TargetId)));
            if (next is null) throw new ConfigurationException("Declared project dependency cycle prevents a conservative per-project build order.");
            ordered.Add(next); pending.Remove(next);
        }
        return ordered.ToArray();
    }
    internal static IEnumerable<string> Files(IEnumerable<string> roots)
    {
        var count = 0;
        foreach (var supplied in roots)
        {
            var root = Path.GetFullPath(supplied); PathSafety.EnsureNoLinks(root);
            if (File.Exists(root)) { if (++count > 100000) throw new ConfigurationException("Evidence input file count exceeds bounded scope."); yield return root; continue; }
            if (!Directory.Exists(root)) throw new ConfigurationException("Explicit evidence input root is unavailable: " + root);
            var stack = new Stack<string>(); stack.Push(root);
            while (stack.Count > 0) foreach (var entry in Directory.EnumerateFileSystemEntries(stack.Pop()).Order(StringComparer.Ordinal))
            {
                PathSafety.EnsureNoLinks(entry);
                if (Directory.Exists(entry)) stack.Push(entry);
                else { if (++count > 100000) throw new ConfigurationException("Evidence input file count exceeds bounded scope."); yield return Path.GetFullPath(entry); }
            }
        }
    }
    private static JsonNode Evaluation(string output, string[] properties)
    {
        var start = output.IndexOf('{'); var end = output.LastIndexOf('}');
        if (start < 0 || end < start) throw new ConfigurationException("Actual project evaluation JSON is missing.");
        using var doc = JsonDocument.Parse(output[start..(end + 1)]); SchemaValidation.ValidateDocument(doc.RootElement, JsonSerializer.SerializeToElement(new { type = "object" }));
        var original = JsonNode.Parse(doc.RootElement.GetRawText())!; var selectedProperties = new JsonObject();
        foreach (var property in properties) selectedProperties[property] = original["Properties"]![property]?.DeepClone() ?? throw new ConfigurationException("Actual evaluation property is missing: " + property);
        var selectedItems = new JsonObject();
        foreach (var item in new[] { "Compile", "Analyzer", "AdditionalFiles", "EditorConfigFiles", "ReferencePath", "ReferenceCopyLocalPaths" })
            selectedItems[item] = new JsonArray(original["Items"]![item]!.AsArray().Select(n => (JsonNode)new JsonObject { ["Identity"] = n!["Identity"]?.DeepClone(), ["FullPath"] = n["FullPath"]?.DeepClone(), ["RuntimeIdentifier"] = n["RuntimeIdentifier"]?.DeepClone() }).ToArray());
        return new JsonObject { ["Properties"] = selectedProperties, ["Items"] = selectedItems };
    }
    private static void RegisterProject(EvidenceStoragePlan plan, RunConfiguration config)
    {
        var path = Path.Combine(Path.GetDirectoryName(plan.RunDirectory)!, "project.json"); PathSafety.EnsureNoLinks(path);
        if (File.Exists(path))
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            if (!string.Equals(doc.RootElement.GetProperty("targetRoot").GetString(), config.Target.Root, PathSafety.Comparison) || doc.RootElement.GetProperty("entry").GetString() != config.Target.Entry)
                throw new ConfigurationException("Project identity is already registered to a different source/entry.");
        }
        else EvidenceArtifactStore.WriteNew(path, new { kind = "archsift-evidence-project", projectId = plan.ProjectId, targetRoot = config.Target.Root, entry = config.Target.Entry });
    }
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
