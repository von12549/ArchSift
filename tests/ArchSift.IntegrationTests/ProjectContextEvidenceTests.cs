using System.Reflection;
using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class ProjectContextEvidenceTests
{
    [Fact]
    public async Task ActualProjectSelectionsIsolateVersionsAndSameNamedOwnersWithoutHidingViolations()
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-project-context-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source"); var build = Path.Combine(root, "snapshot"); var evidence = Path.Combine(root, "evidence");
        Directory.CreateDirectory(source); Directory.CreateDirectory(build); Directory.CreateDirectory(evidence);
        var nuget = Path.Combine(root, "NuGet.Config"); File.WriteAllText(nuget, "<configuration><packageSources><clear /></packageSources></configuration>");
        try
        {
            async Task<ProcessResult> Run(string cwd, params string[] args)
            {
                var result = await SafeProcess.RunAsync(SafeProcess.Dotnet, cwd, args, Path.Combine(root, "home"), null, CancellationToken.None);
                Assert.True(result.ExitCode == 0, result.Output + result.Error); return result;
            }
            async Task Restore(string cwd, string entry)
            {
                var args = new List<string> { "restore", entry, "--configfile", nuget, "--packages", Path.Combine(root, "packages"), "--disable-build-servers", "-p:NuGetAudit=false" };
                if (Environment.GetEnvironmentVariable("ARCHSIFT_TEST_LOCAL_FEED") is { } feed) { args.Add("--source"); args.Add(feed); }
                await Run(cwd, args.ToArray());
            }
            async Task<string> Dependency(string name, string version, string code, string? hint = null)
            {
                var directory = Path.Combine(root, "dependencies", name + version); Directory.CreateDirectory(directory);
                var project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>" + name + "</AssemblyName><AssemblyVersion>" + version + "</AssemblyVersion></PropertyGroup>" +
                    (hint is null ? "" : "<ItemGroup><Reference Include=\"SharedLib\"><HintPath>" + System.Security.SecurityElement.Escape(hint) + "</HintPath></Reference></ItemGroup>") + "</Project>";
                File.WriteAllText(Path.Combine(directory, name + ".csproj"), project); File.WriteAllText(Path.Combine(directory, "Types.cs"), code);
                await Restore(directory, name + ".csproj");
                await Run(directory, "build", name + ".csproj", "--no-restore", "--disable-build-servers", "-p:UseSharedCompilation=false");
                return Path.Combine(directory, "bin", "Debug", "net10.0", name + ".dll");
            }
            var first = await Dependency("SharedLib", "1.0.0.0", "namespace Lib.Shared { public class Legacy {} } namespace Domain { public class Foreign {} }");
            var second = await Dependency("SharedLib", "2.0.0.0", "namespace Lib.Shared { public class Legacy {} public class Modern {} } namespace Domain { public class Foreign {} }");
            var transit = await Dependency("Transit", "1.0.0.0", "namespace Transit { public class Bridge { public Lib.Shared.Legacy Value = new(); } }", first);
            File.WriteAllText(Path.Combine(source, "Directory.Build.props"), "<Project><PropertyGroup Condition=\"'$(CI)' != 'false'\"><NoWarn>$(NoWarn);CS1591</NoWarn></PropertyGroup><PropertyGroup Condition=\"$(MSBuildProjectName.EndsWith('Owner'))\"><NoWarn>$(NoWarn);CS0169</NoWarn></PropertyGroup></Project>");
            foreach (var (folder, dependency, member) in new[] { ("FirstOwner", first, "Legacy"), ("SecondOwner", second, "Modern"), ("ThirdOwner", first, "Legacy") })
            {
                var directory = Path.Combine(source, folder); Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, folder + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>" + (folder == "ThirdOwner" ? "OtherOwner" : "Owner") + "</AssemblyName></PropertyGroup><ItemGroup><Reference Include=\"SharedLib\"><HintPath>" + System.Security.SecurityElement.Escape(dependency) + "</HintPath></Reference>" +
                    (folder == "SecondOwner" ? "<Reference Include=\"Transit\"><HintPath>" + System.Security.SecurityElement.Escape(transit) + "</HintPath></Reference>" : "") + "</ItemGroup></Project>");
                File.WriteAllText(Path.Combine(directory, "Types.cs"), "namespace " + (folder == "ThirdOwner" ? "Other" : "Domain") + "; public class Shared { public Lib.Shared." + member + " Value = new(); }");
            }
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(build, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination);
            }
            var sdk = (await Run(build, "--version")).Output.Trim();
            var sdkEvaluation = await Run(build, "msbuild", "FirstOwner/FirstOwner.csproj", "-nologo", "-getProperty:MSBuildBinPath,MSBuildProjectName");
            using var sdkDocument = JsonDocument.Parse(sdkEvaluation.Output);
            var sdkRoot = sdkDocument.RootElement.GetProperty("Properties").GetProperty("MSBuildBinPath").GetString()!;
            var packs = Path.Combine(Directory.GetParent(sdkRoot)!.Parent!.FullName, "packs", "Microsoft.NETCore.App.Ref");
            var refs = Directory.GetDirectories(packs).Where(p => Path.GetFileName(p).StartsWith("10.", StringComparison.Ordinal)).OrderBy(p => Version.Parse(Path.GetFileName(p))).Last();
            var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = source }, Build = new() { Mode = "existing", TargetFramework = "net10.0" }, Output = new() { Directory = evidence } };
            var capture = ExternalBuildEvidence.Capture(config, Directory.GetFiles(sdkRoot, "*", SearchOption.AllDirectories).Concat(Directory.GetFiles(refs, "*", SearchOption.AllDirectories)).Concat([first, second, transit]).ToArray());
            var capturePath = Path.Combine(evidence, "capture.json"); ExternalBuildEvidence.WriteNew(capturePath, capture);
            var projects = new List<ReviewedProject>(); var assemblies = new List<ReviewedAssembly>(); var contexts = new List<ReviewedAssemblyContext>(); var materials = new List<EvidenceMaterial>();
            EvidenceMaterial Material(string p, string role) { var file = ExternalBuildEvidence.FileFact(p, "evidence"); return new(p, file.Sha256, file.Length, role); }
            foreach (var folder in new[] { "FirstOwner", "SecondOwner", "ThirdOwner" })
            {
                var entry = folder + "/" + folder + ".csproj"; await Restore(build, entry);
                var built = await Run(build, "build", entry, "--no-restore", "--disable-build-servers", "-p:UseSharedCompilation=false", "-bl:" + Path.Combine(evidence, folder + ".binlog"));
                var log = Path.Combine(evidence, folder + ".log"); File.WriteAllText(log, built.Output + built.Error); materials.Add(Material(log, "build-log")); materials.Add(Material(Path.Combine(evidence, folder + ".binlog"), "evaluation-trace"));
                var names = capture.PropertyNames.Concat(["MSBuildProjectFullPath", "AssemblyName", "TargetPath", "TargetFramework", "Configuration"]).Distinct().ToArray();
                var evaluated = await Run(build, "msbuild", entry, "-nologo", "-t:ResolveReferences", "-p:BuildProjectReferences=false", "-p:Configuration=Debug", "-getProperty:" + string.Join(',', names), "-getItem:ReferencePath,ReferenceCopyLocalPaths");
                var tracePath = Path.Combine(evidence, folder + "-evaluation.json"); File.WriteAllText(tracePath, evaluated.Output); materials.Add(Material(tracePath, "evaluation-trace"));
                using var trace = JsonDocument.Parse(evaluated.Output); var values = trace.RootElement.GetProperty("Properties");
                var path = values.GetProperty("TargetPath").GetString()!;
                projects.Add(new(entry, values.GetProperty("AssemblyName").GetString()!, "net10.0", capture.PropertyNames.ToDictionary(p => p, p => values.GetProperty(p).GetString()!)));
                assemblies.Add(new(entry, path)); contexts.Add(new(folder, entry, path, tracePath, trace.RootElement.GetProperty("Items").GetProperty("ReferencePath").EnumerateArray().Select(i => i.GetProperty("FullPath").GetString()!).ToArray())
                { RuntimePaths = trace.RootElement.GetProperty("Items").GetProperty("ReferenceCopyLocalPaths").EnumerateArray().Select(i => i.GetProperty("FullPath").GetString()!).ToArray() });
            }
            foreach (var file in Directory.GetFiles(build, "*", SearchOption.AllDirectories).Where(f => f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))) materials.Add(Material(file, "generated-input"));
            var notes = Path.Combine(evidence, "review.md"); File.WriteAllText(notes, "Actual offline synthetic builds: same-named owners, SharedLib 1/2 in independent compiler contexts, Transit requested 1 but MSBuild selected 2; exact actual trace and token/culture still required. No target application invoked."); materials.Add(Material(notes, "review-notes"));
            var review = new ExternalBuildReview
            {
                SchemaVersion = 2, CaptureSha256 = AssemblyArtifacts.FileHash(capturePath), Approved = true, InputsComplete = true, Reviewer = "Synthetic context fixture",
                Sdk = sdk, SdkToolPath = Path.Combine(sdkRoot, "MSBuild.dll"), TargetFramework = "net10.0", Configuration = "Debug", Command = ["dotnet", "build", "FirstOwner/FirstOwner.csproj", "and", "SecondOwner/SecondOwner.csproj", "--no-restore"], ExitCode = 0,
                Materials = materials.ToArray(), Projects = projects.ToArray(), Assemblies = assemblies.ToArray(), BuildSnapshotRoot = build, ProjectContexts = contexts.ToArray(), Unknowns = []
            };
            var reviewPath = Path.Combine(evidence, "review.json"); ExternalBuildEvidence.WriteNew(reviewPath, review);
            var manifest = Path.Combine(root, "manifest.json"); ExternalBuildEvidence.Seal(config, capturePath, reviewPath, manifest);
            config = config with { Build = config.Build with { AssemblyManifest = manifest } };
            var snapshot = ProjectDiscovery.Discover(source, targetFramework: "net10.0"); var inputs = AssemblyArtifacts.Existing(config, snapshot);
            var detail = EvidenceWorkbench.Details(config); Assert.Equal("not-revalidated", detail.Freshness); Assert.Equal(3, detail.Contexts.Length);
            var independently = EvidenceWorkbench.Validate(config); Assert.True(independently.SourceBound); Assert.Equal(3, independently.OwnerCount); Assert.Equal(3, independently.ContextCount);
            Assert.True(inputs.SourceBound, string.Join('\n', inputs.Limitations)); Assert.Equal(3, inputs.Contexts.Length);
            var template = RuleLoader.Parse(System.Text.Encoding.UTF8.GetBytes(RuleTemplates.Json("type-dependency")));
            Rule Rule(string id, string kind, string target, string sourceNamespace = "Domain") => template.Rules[0] with { Id = id, Scope = new() { Kind = "namespace", Match = "exact", Value = sourceNamespace }, Parameters = JsonSerializer.SerializeToElement(new { source = new { kind = "namespace", match = "exact", value = sourceNamespace }, forbiddenTarget = new { kind, match = "exact", value = target } }) };
            var policy = template with { Rules = [Rule("version-one-only", "type", "Lib.Shared.Legacy"), Rule("both-contexts", "namespace", "Lib.Shared"), Rule("no-violation", "type", "Missing.Target"), Rule("zero-source", "type", "Lib.Shared.Legacy", "Absent"), Rule("compatible-batch-owner", "type", "Lib.Shared.Legacy", "Other")] };
            var result = AssemblyWorker.Evaluate(new(inputs, [policy]) { ContextWorkspace = evidence }); Assert.Empty(result.Errors);
            Assert.Equal(3, result.AssemblyCount); Assert.All(result.Results.Where(r => r.RuleId is not ("zero-source" or "compatible-batch-owner")), r => Assert.Equal(2, r.Matched));
            var batched = result.Results.Single(r => r.RuleId == "compatible-batch-owner"); Assert.Equal(1, batched.Matched); Assert.Equal("violation", batched.Status);
            Assert.Contains(result.Findings, f => f.RuleId == "compatible-batch-owner" && f.Location!.Contains("ThirdOwner", StringComparison.Ordinal));
            Assert.Equal("violation", result.Results.Single(r => r.RuleId == "version-one-only").Status);
            Assert.Single(result.Findings, f => f.RuleId == "version-one-only");
            var both = result.Findings.Where(f => f.RuleId == "both-contexts").ToArray(); Assert.Equal(2, both.Length); Assert.Equal(2, both.Select(f => f.Id).Distinct().Count()); Assert.All(both, f => Assert.StartsWith("@context/", f.Location));
            Assert.Equal("pass", result.Results.Single(r => r.RuleId == "no-violation").Status); Assert.Equal("inconclusive", result.Results.Single(r => r.RuleId == "zero-source").Status);
            Assert.Empty(Directory.GetDirectories(evidence, "context-worker-*"));
            var cliDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            while (cliDirectory is not null && !File.Exists(Path.Combine(cliDirectory.FullName, "ArchSift.slnx"))) cliDirectory = cliDirectory.Parent;
            var configuration = typeof(ProjectContextEvidenceTests).Assembly.GetCustomAttributes(false).OfType<AssemblyConfigurationAttribute>().Single().Configuration;
            var client = new AssemblyWorkerClient(Path.Combine(cliDirectory!.FullName, "src", "ArchSift.Cli", "bin", configuration, "net10.0", "archsift.dll"));
            var sibling = Path.Combine(evidence, "preserved-sibling.txt"); File.WriteAllText(sibling, "not a worker artifact");
            var bundle = new RuleBundle([new(policy, new(policy.Id, policy.Version, ContentHash.Text("fixture"), "fixture.json"))]);
            var childResult = await client.EvaluateAsync(inputs, bundle, evidence, CancellationToken.None);
            Assert.Empty(childResult.Errors); Assert.Equal(result.AssemblyCount, childResult.AssemblyCount);
            Assert.Equal(result.Results.Select(r => (r.RuleId, r.Status, r.Matched)), childResult.Results.Select(r => (r.RuleId, r.Status, r.Matched)));
            Assert.Equal("not a worker artifact", File.ReadAllText(sibling)); Assert.Empty(Directory.GetDirectories(evidence, "worker-contexts-*"));
            var brokenChild = await client.EvaluateAsync(inputs with { Contexts = [inputs.Contexts[0] with { PrimaryPath = Path.Combine(root, "absent.dll") }] }, bundle, evidence, CancellationToken.None);
            Assert.NotEmpty(brokenChild.Errors); Assert.Empty(Directory.GetDirectories(evidence, "worker-contexts-*")); Assert.True(File.Exists(sibling));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.EvaluateAsync(inputs, bundle, evidence, cancelled.Token));
            Assert.Empty(Directory.GetDirectories(evidence, "worker-contexts-*")); Assert.True(File.Exists(sibling));
            var naming = RuleLoader.Parse(System.Text.Encoding.UTF8.GetBytes(RuleTemplates.Json("naming")));
            naming = naming with { Rules = [naming.Rules[0] with { Scope = new() { Kind = "assembly", Match = "glob", Value = "**" },
                Parameters = JsonSerializer.SerializeToElement(new { subjectKind = "assembly", requiredName = new { match = "glob", value = "**" } }) }] };
            var failed = AssemblyWorker.Evaluate(new(inputs with { Contexts = [inputs.Contexts[0] with { PrimaryPath = Path.Combine(root, "missing.dll") }] }, [naming]) { ContextWorkspace = evidence });
            Assert.NotEmpty(failed.Errors); Assert.Equal("inconclusive", Assert.Single(failed.Results).Status);
            var originalReview = File.ReadAllBytes(reviewPath);
            foreach (var broken in new[] {
                review with { ProjectContexts = [contexts[0]] }, review with { ProjectContexts = [contexts[0], contexts[0]] },
                review with { ProjectContexts = [contexts[0] with { ReferencePaths = contexts[1].ReferencePaths }, contexts[1], contexts[2]] },
                review with { ProjectContexts = [contexts[0] with { EvaluationPath = contexts[1].EvaluationPath }, contexts[1], contexts[2]] },
                review with { ProjectContexts = [contexts[0] with { RuntimePaths = contexts[1].RuntimePaths }, contexts[1], contexts[2]] },
                review with { ProjectContexts = [contexts[0] with { ReferencePaths = contexts[0].ReferencePaths.Concat([second]).ToArray() }, contexts[1], contexts[2]] },
                review with { BuildSnapshotRoot = source }, review with { Materials = review.Materials.Where(m => m.Path != contexts[0].EvaluationPath).ToArray() } })
            {
                File.WriteAllBytes(reviewPath, JsonSerializer.SerializeToUtf8Bytes(broken, JsonContract.Options));
                Assert.Throws<ConfigurationException>(() => ExternalBuildEvidence.Seal(config, capturePath, reviewPath, Path.Combine(root, "rejected.json")));
                Assert.False(File.Exists(Path.Combine(root, "rejected.json"))); Assert.False(AssemblyArtifacts.Existing(config, snapshot).SourceBound);
            }
            var evaluationFile = contexts[0].EvaluationPath;
            var originalEvaluation = File.ReadAllBytes(evaluationFile);
            foreach (var (bytes, reason) in new[] { (System.Text.Encoding.UTF8.GetBytes("{}"), "Malformed project evaluation"),
                (new byte[ProjectContextEvidence.MaximumJsonBytes + 1], "Evaluation material is too large") })
            {
                File.WriteAllBytes(evaluationFile, bytes);
                var malformed = review with { Materials = review.Materials.Select(m => m.Path == evaluationFile ? Material(evaluationFile, "evaluation-trace") : m).ToArray() };
                File.WriteAllBytes(reviewPath, JsonSerializer.SerializeToUtf8Bytes(malformed, JsonContract.Options));
                Assert.Contains(reason, Assert.Throws<ConfigurationException>(() => ExternalBuildEvidence.Seal(config, capturePath, reviewPath, Path.Combine(root, "rejected.json"))).Message);
                Assert.False(File.Exists(Path.Combine(root, "rejected.json")));
            }
            File.WriteAllBytes(evaluationFile, originalEvaluation);
            File.WriteAllBytes(reviewPath, originalReview);
            var report = await new AnalysisService((data, bundle, output, token) => Task.FromResult(AssemblyWorker.Evaluate(new(data, bundle.Documents.Select(d => d.Ruleset).ToArray()) { ContextWorkspace = output })))
                .RunSelectedAsync(config, new(policy, new(policy.Id, policy.Version, ContentHash.Text("fixture"), "fixture.json")));
            Assert.True(report.Report.Coverage.SourceBound); Assert.Equal("project-contexts", report.Report.BuildContext.AssemblyBindingMode); Assert.Equal(3, report.Report.BuildContext.AssemblyContextCount);
            File.AppendAllText(Path.Combine(build, "Directory.Build.props"), "<!--changed snapshot-->"); Assert.False(AssemblyArtifacts.Existing(config, snapshot).SourceBound);
            Assert.Equal("stale", EvidenceWorkbench.Validate(config).Status);
        }
        finally { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "Shared, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null", true)]
    [InlineData("Shared, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null", "Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", false)]
    [InlineData("Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "Shared, Version=2.0.0.0, Culture=en-US, PublicKeyToken=null", false)]
    [InlineData("Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "Shared, Version=2.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", true)]
    [InlineData("Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "Shared, Version=2.0.0.0, Culture=neutral, PublicKeyToken=c8d65537854e1f03", false)]
    [InlineData("Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "Shared, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null", false)]
    public void ProjectResolutionRequiresCompatibleVersionAndExactCultureToken(string requested, string selected, bool valid)
        => Assert.Equal(valid, ProjectContextEvidence.CanResolve(new AssemblyName(requested), new AssemblyName(selected)));

    [Fact]
    public void OversizedContextReviewIsRejectedBeforeJsonParsing()
    {
        var file = Path.Combine(Path.GetTempPath(), "archsift-context-size-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            using (var stream = File.Create(file)) stream.SetLength(ProjectContextEvidence.MaximumJsonBytes + 1L);
            Assert.Contains("bounded size", Assert.Throws<ConfigurationException>(() => ExternalBuildEvidence.Read<ExternalBuildReview>(file)).Message);
        }
        finally { File.Delete(file); }
    }
}
