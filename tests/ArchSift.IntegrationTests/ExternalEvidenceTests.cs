using System.Reflection;
using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class ExternalEvidenceTests
{
    [Fact]
    public void UnapprovedGeneratedTemplateFailsCleanlyWithoutProducingAManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-external-template-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source"); var evidence = Path.Combine(root, "evidence"); Directory.CreateDirectory(source);
        try
        {
            File.WriteAllText(Path.Combine(source, "Example.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = source }, Build = new() { TargetFramework = "net10.0" }, Output = new() { Directory = evidence } };
            var capture = ExternalBuildEvidence.Capture(config, []); var captured = Path.Combine(evidence, "capture.json"); ExternalBuildEvidence.WriteNew(captured, capture);
            var review = Path.Combine(evidence, "review.json"); ExternalBuildEvidence.WriteNew(review, ExternalBuildEvidence.Template(capture, AssemblyArtifacts.FileHash(captured)));
            var output = Path.Combine(evidence, "manifest.json"); Assert.Throws<ConfigurationException>(() => ExternalBuildEvidence.Seal(config, captured, review, output));
            Assert.False(File.Exists(output));
        }
        finally { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualConditionalBuildCanBeReviewedAndTamperedEvidenceNeverBinds(bool violation)
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-external-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source"); var build = Path.Combine(root, "build"); var evidence = Path.Combine(root, "evidence");
        Directory.CreateDirectory(source); Directory.CreateDirectory(build); Directory.CreateDirectory(evidence);
        try
        {
            const string project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Example</AssemblyName></PropertyGroup></Project>";
            const string props = "<Project><PropertyGroup Condition=\"'$(CI)' != 'true'\"><NoWarn>$(NoWarn);CS0649</NoWarn></PropertyGroup><PropertyGroup Condition=\"$(MSBuildProjectName.EndsWith('Example'))\"><NoWarn>$(NoWarn);CS0649</NoWarn></PropertyGroup></Project>";
            File.WriteAllText(Path.Combine(source, "Example.csproj"), project);
            File.WriteAllText(Path.Combine(source, "Directory.Build.props"), props);
            File.WriteAllText(Path.Combine(source, "Types.cs"), "namespace Domain { public class Model { " +
                (violation ? "public Forbidden.Type Value;" : "public int Value;") + " } } namespace Forbidden { public class Type {} }");
            File.WriteAllText(Path.Combine(source, "BProbes.cs"), BProbes(violation));
            foreach (var path in Directory.GetFiles(source)) File.Copy(path, Path.Combine(build, Path.GetFileName(path)));
            var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = source }, Build = new() { Mode = "existing", TargetFramework = "net10.0" }, Output = new() { Directory = evidence } };
            var home = Path.Combine(root, "cli-home");
            async Task<ProcessResult> Run(params string[] args)
            {
                var result = await SafeProcess.RunAsync(SafeProcess.Dotnet, build, args, home, null, CancellationToken.None);
                Assert.True(result.ExitCode == 0, result.Output + result.Error); return result;
            }
            var sdk = (await Run("--version")).Output.Trim();
            var evaluation = await Run("msbuild", "Example.csproj", "-getProperty:MSBuildBinPath,MSBuildProjectName,CI,NoWarn", "-nologo");
            using var evaluated = JsonDocument.Parse(evaluation.Output);
            var values = evaluated.RootElement.GetProperty("Properties");
            var sdkRoot = values.GetProperty("MSBuildBinPath").GetString()!;
            var tool = Path.Combine(sdkRoot, "MSBuild.dll");
            var frameworks = Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
                .Where(p => { try { _ = AssemblyName.GetAssemblyName(p); return true; } catch (BadImageFormatException) { return false; } }).ToArray();
            var sdkInputs = Directory.GetFiles(sdkRoot, "*", SearchOption.AllDirectories);
            var dotnetRoot = Directory.GetParent(sdkRoot)!.Parent!.FullName;
            var packRoot = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
            var references = Directory.GetDirectories(packRoot).Where(p => Path.GetFileName(p).StartsWith("10.", StringComparison.Ordinal))
                .OrderBy(p => Version.Parse(Path.GetFileName(p))).Last();
            var referenceFiles = Directory.GetFiles(Path.Combine(references, "ref", "net10.0"), "*.dll");
            var priorRuntime = Directory.GetDirectories(Directory.GetParent(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar))!.FullName)
                .Where(p => Path.GetFileName(p).StartsWith("9.", StringComparison.Ordinal)).Select(p => Path.Combine(p, "System.Runtime.dll")).FirstOrDefault(File.Exists);
            var frameworkProof = referenceFiles.Concat(priorRuntime is null ? [] : new[] { priorRuntime }).ToArray();
            var capture = ExternalBuildEvidence.Capture(config, frameworks.Concat(sdkInputs).Concat(Directory.GetFiles(references, "*", SearchOption.AllDirectories))
                .Concat(frameworkProof).Distinct().ToArray(), ["ARCHSIFT_TEST_LOCAL_FEED"]);
            var capturePath = Path.Combine(evidence, "capture.json"); ExternalBuildEvidence.WriteNew(capturePath, capture);
            var nuget = Path.Combine(evidence, "NuGet.Config"); File.WriteAllText(nuget, "<configuration><packageSources><clear /></packageSources></configuration>");
            var restore = new List<string> { "restore", "Example.csproj", "--configfile", nuget, "--packages", Path.Combine(root, "packages"), "--disable-build-servers", "-p:NuGetAudit=false" };
            if (Environment.GetEnvironmentVariable("ARCHSIFT_TEST_LOCAL_FEED") is { } feed) { restore.Add("--source"); restore.Add(feed); }
            await Run(restore.ToArray());
            var command = new[] { "build", "Example.csproj", "--no-restore", "--disable-build-servers", "-p:UseSharedCompilation=false", "-c", "Debug", "-o", evidence, "-bl:" + Path.Combine(evidence, "build.binlog") };
            var built = await Run(command);
            var log = Path.Combine(evidence, "build.log"); File.WriteAllText(log, built.Output + built.Error);
            var notes = Path.Combine(evidence, "review.md"); File.WriteAllText(notes, "Synthetic reviewed build: copied captured source, literal SDK, conditional CI/NoWarn only; SDK imports/tools and runtime files captured before build. No custom tasks or generators. No application entry point executed.");
            EvidenceMaterial Material(string path, string role) { var fact = ExternalBuildEvidence.FileFact(path, "evidence"); return new(path, fact.Sha256, fact.Length, role); }
            var review = new ExternalBuildReview
            {
                CaptureSha256 = AssemblyArtifacts.FileHash(capturePath), Approved = true, InputsComplete = true, Reviewer = "Synthetic integration fixture",
                Sdk = sdk, SdkToolPath = tool, TargetFramework = "net10.0", Configuration = "Debug", Command = command, ExitCode = built.ExitCode,
                Projects = [new("Example.csproj", "Example", "net10.0", new() { ["CI"] = values.GetProperty("CI").GetString()!, ["NoWarn"] = values.GetProperty("NoWarn").GetString()!, ["MSBuildProjectName"] = values.GetProperty("MSBuildProjectName").GetString()! })],
                Assemblies = [new("Example.csproj", Path.Combine(evidence, "Example.dll"))], FrameworkPaths = frameworkProof,
                Materials = new[] { Material(log, "build-log"), Material(Path.Combine(evidence, "build.binlog"), "evaluation-trace"), Material(notes, "review-notes") }
                    .Concat(Directory.GetFiles(Path.Combine(build, "obj"), "*", SearchOption.AllDirectories).Select(p => Material(p, "generated-input"))).ToArray(), Unknowns = []
            };
            var reviewPath = Path.Combine(evidence, "review.json"); ExternalBuildEvidence.WriteNew(reviewPath, review);
            var manifest = Path.Combine(evidence, "assembly-manifest.json");
            ExternalBuildEvidence.Seal(config, capturePath, reviewPath, manifest);
            config = config with { Build = config.Build with { AssemblyManifest = manifest } };
            var snapshot = ProjectDiscovery.Discover(source, targetFramework: "net10.0");
            Assert.Contains(snapshot.Scope.UnsupportedConstructs, x => x.Contains("unevaluated", StringComparison.Ordinal));
            var inputs = AssemblyArtifacts.Existing(config, snapshot); Assert.True(inputs.SourceBound); Assert.Equal("external-reviewed", inputs.EvidenceOrigin);
            var manifestBytes = File.ReadAllBytes(manifest);
            var sealedManifest = JsonSerializer.Deserialize<AssemblyManifest>(manifestBytes, JsonContract.Options)!;
            foreach (var changed in new[] { sealedManifest with { Assemblies = [sealedManifest.Assemblies[0] with { AssemblyName = "Wrong" }] },
                sealedManifest with { Assemblies = [sealedManifest.Assemblies[0] with { SourceProject = "Missing.csproj" }] },
                sealedManifest with { Assemblies = [sealedManifest.Assemblies[0] with { Sha256 = new string('0', 64) }] },
                sealedManifest with { SchemaVersion = 1, ReviewedBuild = null } })
            {
                File.WriteAllBytes(manifest, JsonSerializer.SerializeToUtf8Bytes(changed, JsonContract.Options));
                Assert.False(AssemblyArtifacts.Existing(config, snapshot).SourceBound);
            }
            File.WriteAllBytes(manifest, manifestBytes);
            var set = RuleLoader.Parse(System.Text.Encoding.UTF8.GetBytes(RuleTemplates.Json("type-dependency"))) with
            {
                Rules = [new Rule { Id = "boundary", Type = "type-dependency", Enabled = true, Severity = "warning", Reason = "Synthetic dependency", Scope = new() { Kind = "namespace", Match = "exact", Value = "Domain" },
                    Parameters = JsonSerializer.SerializeToElement(new { source = new { kind = "namespace", match = "exact", value = "Domain" }, forbiddenTarget = new { kind = "namespace", match = "exact", value = "Forbidden" } }) }]
            };
            var result = AssemblyWorker.Evaluate(new(inputs, [set])); Assert.Empty(result.Errors);
            Assert.Equal(violation ? "violation" : "pass", Assert.Single(result.Results).Status);
            var probes = BProbeRules(set);
            var probeResults = AssemblyWorker.Evaluate(new(inputs, [probes])); Assert.Empty(probeResults.Errors); Assert.Equal(6, probeResults.Results.Length);
            Assert.All(probeResults.Results, r => { Assert.True(r.Matched > 0); Assert.Equal(violation ? "violation" : "pass", r.Status); });
            var zero = set with { Rules = [set.Rules[0] with { Scope = new() { Kind = "namespace", Match = "exact", Value = "Absent" } }] };
            Assert.Equal("inconclusive", Assert.Single(AssemblyWorker.Evaluate(new(inputs, [zero])).Results).Status);
            Assert.Throws<IOException>(() => ExternalBuildEvidence.Seal(config, capturePath, reviewPath, manifest));
            var savedReview = File.ReadAllBytes(reviewPath);
            foreach (var broken in new[] { review with { Approved = false }, review with { InputsComplete = false }, review with { Unknowns = ["Untracked generator"] },
                review with { ExitCode = 1 }, review with { Sdk = "different" }, review with { TargetFramework = "net9.0" }, review with { Configuration = "Release" },
                review with { Projects = [] }, review with { FrameworkPaths = [] }, review with { Materials = review.Materials.Where(m => m.Role != "evaluation-trace").ToArray() },
                review with { Materials = review.Materials.Where(m => m.Role != "generated-input").ToArray() },
                review with { Imports = [Path.Combine(evidence, "not-captured.targets")] } })
            {
                File.WriteAllBytes(reviewPath, JsonSerializer.SerializeToUtf8Bytes(broken, JsonContract.Options));
                Assert.False(AssemblyArtifacts.Existing(config, snapshot).SourceBound);
                var rejected = Path.Combine(evidence, "rejected.json");
                Assert.Throws<ConfigurationException>(() => ExternalBuildEvidence.Seal(config, capturePath, reviewPath, rejected));
                Assert.False(File.Exists(rejected));
            }
            File.WriteAllBytes(reviewPath, savedReview);
            var selected = new LoadedRuleset(set, new(set.Id, set.Version, ContentHash.Text("synthetic"), "synthetic.json"));
            var outcome = await new AnalysisService((data, bundle, output, token) => Task.FromResult(AssemblyWorker.Evaluate(new(data, bundle.Documents.Select(d => d.Ruleset).ToArray())))).RunSelectedAsync(config, selected);
            Assert.Equal("external-reviewed", outcome.Report.BuildContext.EvidenceOrigin);
            Assert.Equal(violation ? "violation" : "pass", Assert.Single(outcome.Report.RuleResults).Status);
            Assert.Contains("external-reviewed", ReportWriter.Json(outcome.Report)); Assert.Contains("externally reviewed", ReportWriter.Html(outcome.Report));
            File.AppendAllText(log, "changed"); Assert.False(AssemblyArtifacts.Existing(config, snapshot).SourceBound);
            File.WriteAllText(log, built.Output + built.Error);
            File.AppendAllText(Path.Combine(source, "Directory.Build.props"), "<!--changed-->");
            Assert.False(AssemblyArtifacts.Existing(config, ProjectDiscovery.Discover(source, targetFramework: "net10.0")).SourceBound);
            File.WriteAllText(Path.Combine(source, "Directory.Build.props"), props);
            var captureBytes = File.ReadAllBytes(capturePath);
            File.WriteAllBytes(capturePath, JsonSerializer.SerializeToUtf8Bytes(capture with { EnvironmentHashes = capture.EnvironmentHashes.ToDictionary(p => p.Key, _ => new string('0', 64)) }, JsonContract.Options));
            Assert.False(AssemblyArtifacts.Existing(config, snapshot).SourceBound); File.WriteAllBytes(capturePath, captureBytes);
            File.AppendAllText(Path.Combine(evidence, "Example.dll"), "old-dll-tamper"); Assert.False(AssemblyArtifacts.Existing(config, snapshot).SourceBound);
            Assert.False(Directory.Exists(Path.Combine(source, "bin"))); Assert.False(Directory.Exists(Path.Combine(source, "obj")));
        }
        finally { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); Directory.Delete(root, true); }
    }

    private static string BProbes(bool violation)
    {
        var domain = violation ? "public Microsoft.EntityFrameworkCore.DbContext Value;" : "public int Value;";
        var application = violation ? "public System.IServiceProvider Services; public IFX.Platform.Primitives.Token Platform;" : "public IPort Port;";
        var infrastructure = violation ? "public System.Transactions.TransactionScope Transaction; public Newtonsoft.Json.Linq.JObject Json; public IFX.IntegrationHub.ProviderRegistry.Infrastructure.Provider Hub;" : "public int Value;";
        return $"namespace IFX.Modules.CRM.Domain {{ public class Entity {{ {domain} }} }} " +
            $"namespace IFX.Modules.CRM.Application {{ public interface IPort {{}} public class UseCase {{ {application} }} }} " +
            $"namespace IFX.Modules.CRM.Infrastructure {{ public class Adapter {{ {infrastructure} }} }} " +
            "namespace Microsoft.EntityFrameworkCore { public class DbContext {} } namespace Newtonsoft.Json.Linq { public class JObject {} } " +
            "namespace IFX.Platform.Primitives { public class Token {} } namespace IFX.IntegrationHub.ProviderRegistry.Infrastructure { public class Provider {} }";
    }
    private static Ruleset BProbeRules(Ruleset template)
    {
        var cases = new[] {
            ("B01", "IFX.Modules.CRM.Domain", "namespace", "Microsoft.EntityFrameworkCore"),
            ("B02", "IFX.Modules.CRM.Application", "type", "System.IServiceProvider"),
            ("B03", "IFX.Modules.CRM.Infrastructure", "type", "System.Transactions.TransactionScope"),
            ("B04", "IFX.Modules.CRM.Infrastructure", "namespace", "IFX.IntegrationHub.ProviderRegistry.Infrastructure"),
            ("B05", "IFX.Modules.CRM.Infrastructure", "namespace", "Newtonsoft.Json.Linq"),
            ("B07", "IFX.Modules.CRM.Application", "namespace", "IFX.Platform.Primitives") };
        return template with { Id = "synthetic-B-probes", Rules = cases.Select(c => template.Rules[0] with
        {
            Id = "synthetic-" + c.Item1, Scope = new() { Kind = "namespace", Match = "exact", Value = c.Item2 },
            Parameters = JsonSerializer.SerializeToElement(new { source = new { kind = "namespace", match = "exact", value = c.Item2 }, forbiddenTarget = new { kind = c.Item3, match = "exact", value = c.Item4 } })
        }).ToArray() };
    }
}
