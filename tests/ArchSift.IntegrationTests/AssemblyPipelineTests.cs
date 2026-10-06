using System.Text;
using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class AssemblyPipelineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealIsolatedAssembliesProduceArchUnitNetPassAndViolationWithoutExecutingTargets(bool violation)
    {
        using var fixture = new Fixture(); fixture.CreateSource(violation);
        var snapshot = ProjectDiscovery.Discover(fixture.Source);
        var inputs = await IsolatedBuild.BuildAsync(fixture.Config, snapshot, CancellationToken.None);
        Assert.True(inputs.SourceBound);
        var result = await fixture.Worker.EvaluateAsync(inputs, Rules(), fixture.Output, CancellationToken.None);
        Assert.Empty(result.Errors);
        Assert.Equal(violation ? "violation" : "pass", Assert.Single(result.Results).Status);
        Assert.Equal("0.13.4", result.EngineVersions["archunitnet"]);
        Assert.Equal(violation, result.Findings.Length > 0);
        Assert.False(File.Exists(Path.Combine(fixture.Source, "target-was-executed.txt")));
        InputCapture.VerifyUnchanged(fixture.Source, snapshot.InputIdentity);
        Assert.False(Directory.Exists(Path.Combine(fixture.Source, "obj")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Source, "bin")));

        // A bare DLL may yield assembly evidence, but cannot prove current-source conformance.
        var unbound = AssemblyArtifacts.Unbound(inputs.Paths, fixture.Config.Build, ["No manifest"]);
        var unboundResult = await fixture.Worker.EvaluateAsync(unbound, Rules(), fixture.Output, CancellationToken.None);
        Assert.Equal("inconclusive", Assert.Single(unboundResult.Results).Status);

        var wrongHash = inputs with { Sha256 = inputs.Sha256.ToDictionary(x => x.Key, _ => new string('0', 64)) };
        var changedResult = await fixture.Worker.EvaluateAsync(wrongHash, Rules(), fixture.Output, CancellationToken.None);
        Assert.Equal("inconclusive", Assert.Single(changedResult.Results).Status);
        Assert.NotEmpty(changedResult.Errors);

        var named = await fixture.Worker.EvaluateAsync(inputs, NamingRules(false), fixture.Output, CancellationToken.None);
        Assert.All(named.Results, r => Assert.Equal("pass", r.Status));
        var wrongNames = await fixture.Worker.EvaluateAsync(inputs, NamingRules(true), fixture.Output, CancellationToken.None);
        Assert.All(wrongNames.Results, r => Assert.Equal("violation", r.Status));
    }

    [Fact]
    public async Task MissingClosureZeroMatchConfigMismatchAndStaleSourceDoNotPass()
    {
        using var fixture = new Fixture(); fixture.CreateSource(false, twoProjects: true);
        var snapshot = ProjectDiscovery.Discover(fixture.Source);
        var inputs = await IsolatedBuild.BuildAsync(fixture.Config, snapshot, CancellationToken.None);
        Assert.True(inputs.SourceBound);
        var missing = inputs with
        {
            Paths = inputs.Paths.Where(p => Path.GetFileName(p) == "Sample.Domain.dll").ToArray(),
            Sha256 = inputs.Sha256.Where(p => Path.GetFileName(p.Key) == "Sample.Domain.dll").ToDictionary()
        };
        var partial = await fixture.Worker.EvaluateAsync(missing, Rules(), fixture.Output, CancellationToken.None);
        Assert.Equal("inconclusive", Assert.Single(partial.Results).Status);
        Assert.Contains(Assert.Single(partial.Results).Limitations, l => l.Contains("closure is missing"));
        var zero = await fixture.Worker.EvaluateAsync(inputs, Rules("Absent.Namespace"), fixture.Output, CancellationToken.None);
        Assert.Equal("inconclusive", Assert.Single(zero.Results).Status);

        var manifestPath = Directory.GetFiles(Path.Combine(fixture.Output, "build"), "assembly-manifest.json", SearchOption.AllDirectories).Single();
        var mismatch = AssemblyArtifacts.Existing(fixture.Config with
        {
            Build = fixture.Config.Build with { AssemblyManifest = manifestPath, Configuration = "Release" }
        }, snapshot);
        Assert.False(mismatch.SourceBound);
        var tfmMismatch = AssemblyArtifacts.Existing(fixture.Config with
        {
            Build = fixture.Config.Build with { AssemblyManifest = manifestPath, TargetFramework = "net8.0" }
        }, snapshot);
        Assert.False(tfmMismatch.SourceBound);
        fixture.Write("Changed.cs", "class Changed {}");
        var stale = AssemblyArtifacts.Existing(fixture.Config with
        { Build = fixture.Config.Build with { AssemblyManifest = manifestPath } }, ProjectDiscovery.Discover(fixture.Source));
        Assert.False(stale.SourceBound);
    }

    [Fact]
    public async Task BuildFailureUnsafeCustomOutputsAndCancellationAreRealFailures()
    {
        using var invalid = new Fixture(); invalid.CreateSource(false); invalid.Write("Domain/Broken.cs", "not valid C#");
        await Assert.ThrowsAsync<IOException>(() => IsolatedBuild.BuildAsync(invalid.Config, ProjectDiscovery.Discover(invalid.Source), CancellationToken.None));
        using var unsafeOutput = new Fixture(); unsafeOutput.CreateSource(false);
        unsafeOutput.Write("Directory.Build.props", "<Project><PropertyGroup><BaseIntermediateOutputPath>../outside</BaseIntermediateOutputPath></PropertyGroup></Project>");
        await Assert.ThrowsAsync<ConfigurationException>(() => IsolatedBuild.BuildAsync(unsafeOutput.Config, ProjectDiscovery.Discover(unsafeOutput.Source), CancellationToken.None));
        using var cancelled = new Fixture(); cancelled.CreateSource(false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => IsolatedBuild.BuildAsync(cancelled.Config,
            ProjectDiscovery.Discover(cancelled.Source), new CancellationToken(true)));
    }

    private static RuleBundle Rules(string sourceNamespace = "Sample.Domain")
    {
        var json = """
            {"schemaVersion":1,"id":"test-types","version":"1","description":"real ArchUnitNET fixture","rules":[
            {"id":"type-dependency-01","type":"type-dependency","enabled":true,"scope":{"kind":"namespace","match":"exact","value":"SOURCE"},
            "parameters":{"source":{"kind":"namespace","match":"exact","value":"SOURCE"},"forbiddenTarget":{"kind":"namespace","match":"exact","value":"Sample.Infrastructure"}},
            "severity":"warning","reason":"Domain cannot depend on Infrastructure"}],"exceptions":[]}
            """.Replace("SOURCE", sourceNamespace, StringComparison.Ordinal);
        var set = RuleLoader.Parse(Encoding.UTF8.GetBytes(json));
        return RuleLoader.Compose([new(set, new(set.Id, set.Version, ContentHash.Text(json), "fixture.json"))]);
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    public async Task SupportedEarlierTfmsBuildAndAnalyzeRealArtifacts(string tfm)
    {
        using var fixture = new Fixture(); fixture.CreateSource(false);
        fixture.Write("global.json", "{\"sdk\":{\"version\":\"9.0.314\",\"rollForward\":\"disable\",\"allowPrerelease\":false}}");
        foreach (var file in Directory.GetFiles(fixture.Source, "*.csproj", SearchOption.AllDirectories))
            File.WriteAllText(file, File.ReadAllText(file).Replace("net10.0", tfm, StringComparison.Ordinal));
        var config = fixture.Config with { Build = fixture.Config.Build with { TargetFramework = tfm } };
        var inputs = await IsolatedBuild.BuildAsync(config, ProjectDiscovery.Discover(fixture.Source), CancellationToken.None);
        Assert.True(inputs.SourceBound);
        Assert.Equal("9.0.314", inputs.Sdk);
        var result = await fixture.Worker.EvaluateAsync(inputs, Rules(), fixture.Output, CancellationToken.None);
        Assert.Equal("pass", Assert.Single(result.Results).Status);
        Assert.Empty(result.Errors);
    }

    private static RuleBundle NamingRules(bool violating)
    {
        var set = new Ruleset
        {
            SchemaVersion = 1, Id = "names", Version = "1", Description = "compiled naming", Exceptions = [],
            Rules = new[] { "type", "assembly" }.Select(kind => new Rule
            {
                Id = "naming-" + kind, Type = "naming", Enabled = true, Severity = "warning", Reason = "Naming policy",
                Scope = new() { Kind = kind, Match = "exact", Value = kind == "type" ? "Sample.Domain.Domain" : "Sample.Domain" },
                Parameters = JsonSerializer.SerializeToElement(new
                {
                    subjectKind = kind,
                    requiredName = new { match = "exact", value = violating ? "Different" : kind == "type" ? "Sample.Domain.Domain" : "Sample.Domain" }
                })
            }).ToArray()
        };
        return RuleLoader.Compose([new(set, new(set.Id, set.Version, new string('0', 64), "naming.json"))]);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "archsift-assemblies-" + Guid.NewGuid().ToString("N"));
        public string Source => Path.Combine(root, "source");
        public string Output => Path.Combine(root, "output");
        public Fixture() => Directory.CreateDirectory(Source);
        public RunConfiguration Config => new()
        {
            SchemaVersion = 1, Target = new() { Root = Source }, Output = new() { Directory = Output },
            Build = new() { Mode = "isolated", TargetFramework = "net10.0", Configuration = "Debug" }
        };
        public AssemblyWorkerClient Worker
        {
            get
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
                var configuration = typeof(AssemblyPipelineTests).Assembly.GetCustomAttributes(false)
                    .OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
                return new(Path.Combine(directory!.FullName, "src", "ArchSift.Cli", "bin", configuration, "net10.0", "archsift.dll"));
            }
        }
        public void Write(string path, string text)
        {
            var full = PathSafety.Under(Source, path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, text);
        }
        public void CreateSource(bool violation, bool twoProjects = false)
        {
            var reference = twoProjects ? "<ItemGroup><ProjectReference Include=\"../Contracts/Contracts.csproj\" /></ItemGroup>" : "";
            Write("Domain/Domain.csproj", $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Sample.Domain</AssemblyName><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>{reference}</Project>");
            if (twoProjects)
            {
                Write("Contracts/Contracts.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Sample.Contracts</AssemblyName></PropertyGroup></Project>");
                Write("Contracts/Contracts.cs", "namespace Sample.Contracts { public interface IPort {} }");
            }
            var body = violation ? "public System.Collections.Generic.List<Sample.Infrastructure.Forbidden> Value = new();" : "public int Value;";
            var implemented = twoProjects ? ": Sample.Contracts.IPort" : "";
            Write("Domain/Types.cs", $"namespace Sample.Domain {{ public class Domain {implemented} {{ {body} }} }} namespace Sample.Infrastructure {{ public class Forbidden {{ }} }}");
            var marker = Path.Combine(Source, "target-was-executed.txt").Replace("\\", "\\\\", StringComparison.Ordinal);
            Write("Domain/Initializer.cs", $"public static class Initializer {{ [System.Runtime.CompilerServices.ModuleInitializer] public static void Init() {{ System.IO.File.WriteAllText(\"{marker}\", \"executed\"); }} }}");
        }
        public void Dispose()
        {
            if (!Path.GetFileName(root).StartsWith("archsift-assemblies-", StringComparison.Ordinal)) throw new InvalidOperationException();
            Directory.Delete(root, true);
        }
    }
}
