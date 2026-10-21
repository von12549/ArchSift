using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.TestGate;
using Xunit;

namespace ArchSift.Testing.Tests;

public enum CaseFlavor : ulong { Small = 1, Large = ulong.MaxValue }

public sealed class FormalAdoptionTests
{
    [Theory]
    [InlineData(CaseFlavor.Small, typeof(string), " a|b:\"\\\n雪 ", Label = "")]
    [InlineData(CaseFlavor.Large, typeof(int), "a|b", Label = "")]
    public void TypedTheory(CaseFlavor flavor, Type type, string text)
    {
        Assert.NotNull(type);
        Assert.NotEmpty(text);
        Assert.True(flavor is CaseFlavor.Small or CaseFlavor.Large);
    }

    [Fact]
    public void TypedArgumentsAreLosslessAndOverloadAndCultureIndependent()
    {
        var method = typeof(FormalAdoptionTests).GetMethod(nameof(TypedTheory))!;
        var args = new object?[] { CaseFlavor.Large, typeof(string), " a|b:\"\\\n雪 " };
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var identity = CaseIdentities.Create(method, args);
            Assert.Equal("18446744073709551615", identity.Arguments[0].Value);
            Assert.Contains("System.String", identity.Arguments[1].Value!);
            Assert.Equal(args[2], identity.Arguments[2].Value);
            var line = CaseIdentities.Marker + CaseIdentities.Serialize(new("execution", "xunit-case", identity));
            Assert.Equal(CaseIdentities.Key(identity), CaseIdentities.Key(CaseIdentities.Read(line)!.Identity));
            Assert.Throws<InvalidDataException>(() => CaseIdentities.Read(line + "\n" + line));
            Assert.Throws<InvalidDataException>(() => CaseIdentities.Read(line.Replace("\"kind\":\"enum\"", "\"kind\":\"unknown\"", StringComparison.Ordinal)));
            Assert.NotEqual(CaseIdentities.Key(CaseIdentities.Create(Overload(typeof(int)), [1])),
                CaseIdentities.Key(CaseIdentities.Create(Overload(typeof(string)), ["1"])));
            Assert.Throws<InvalidDataException>(() => CaseIdentities.Create(method, [new object(), typeof(int), "x"]));
        }
        finally { CultureInfo.CurrentCulture = before; }
        static MethodInfo Overload(Type type) => typeof(FormalAdoptionTests).GetMethod(nameof(Signature),
            BindingFlags.Static | BindingFlags.NonPublic, [type])!;
    }

    private static void Signature(int value) { }
    private static void Signature(string value) { }

    [Fact]
    public void MappingRejectsCollisionsMissingReplacedAndAddedCases()
    {
        var identity = CaseIdentities.Create(typeof(FormalAdoptionTests).GetMethod(nameof(TypedTheory))!,
            [CaseFlavor.Small, typeof(string), "row"]);
        var test = new MtpTest("exec-1", "exec-1", identity.Type, identity.Method, "passed", null, null, [])
            { Identity = new("exec-1", "case-1", identity) };
        var run = new MtpRun(true, true, 1, [test], [test.Id], [test.Id], [], []);
        var map = CaseMapping.Propose(run, Path.GetTempPath(), "10.0.303", "test-candidate", "source-context", [new("case.cs", "hash")]);
        CaseMapping.Assess(map, run);
        Assert.Throws<InvalidDataException>(() => CaseMapping.Assess(map, run with { Tests = [] }));
        Assert.Throws<InvalidDataException>(() => CaseMapping.Assess(map, run with
            { Tests = [test with { Identity = test.Identity with { Identity = identity with { Arguments = [.. identity.Arguments[..2], identity.Arguments[2] with { Value = "replaced" }] } } }] }));
        Assert.Throws<InvalidDataException>(() => CaseMapping.Propose(run with { Tests = [test, test with
            { Id = "other", Identity = test.Identity with { ExecutionId = "other", XunitCaseId = "other-case" } }] },
            map.TargetRoot, map.Sdk, map.ToolIdentity, map.SourceCommit, map.IdentityInputs));
        Assert.Throws<InvalidDataException>(() => CaseMapping.Assess(map, run with { Tests = [test with { Identity = null }] }));
    }

    [Fact]
    public void AuditDefaultsToInheritanceAndCannotInferSecurityFromRestoreSuccess()
    {
        var config = EmptyConfig();
        Assert.DoesNotContain(NuGetAudit.RestoreArguments(config, "test.csproj", "NuGet.config"), arg => arg.Contains("NuGetAudit=false"));
        Assert.Contains("-p:NuGetAudit=false", NuGetAudit.RestoreArguments(config with { AuditMode = "offline-architecture" }, "test.csproj", "NuGet.config"));
        Assert.False(NuGetAudit.CoverageComplete(NuGetAudit.ReadCompletion("restore succeeded"), 3));
        var counts = NuGetAudit.ReadCompletion("restored\n{\"Properties\":{\"RestoreProjectCount\":\"3\",\"RestoreProjectsAuditedCount\":\"3\",\"RestoreSkippedCount\":\"0\"}}");
        Assert.True(NuGetAudit.CoverageComplete(counts, 3));
        Assert.False(NuGetAudit.CoverageComplete(counts with { SkippedCount = 1 }, 3));
        Assert.False(NuGetAudit.CoverageComplete(counts with { AuditedCount = 2 }, 3));
    }

    [Fact]
    public void NativePolicyLoadsWithoutEnvironmentAndRejectsWrongOrEmptyOverrides()
    {
        var directory = Path.Combine(Path.GetTempPath(), "archsift-native-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var rules = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "rules.json"));
        File.WriteAllBytes(Path.Combine(directory, "rules.json"), rules);
        File.WriteAllText(Path.Combine(directory, "IFX.sln"), "synthetic marker");
        var policy = new NativePolicyDocument(1, "synthetic-policy-1", [new("rules.json", ContentHash.Bytes(rules))], []);
        Write("policy.json", policy);
        var result = NativePolicy.Load(directory, "policy.json");
        Assert.Single(result.Paths);
        Assert.Equal(directory, NativePolicy.FindRoot(directory, "IFX.sln"));
        var gate = EmptyConfig() with { TargetRoot = directory, PolicySha256 = result.Sha256, Rulesets = ["rules.json"] };
        Write("gate.json", gate);
        _ = NativePolicy.Load(directory, "policy.json", Path.Combine(directory, "gate.json"));
        Assert.Throws<FileNotFoundException>(() => NativePolicy.Load(directory, "missing.json"));
        Write("gate.json", gate with { PolicySha256 = new('0', 64) });
        Assert.Throws<ConfigurationException>(() => NativePolicy.Load(directory, "policy.json", Path.Combine(directory, "gate.json")));
        Write("gate.json", gate with { TargetRoot = Path.GetTempPath() });
        Assert.Throws<ConfigurationException>(() => NativePolicy.Load(directory, "policy.json", Path.Combine(directory, "gate.json")));
        Write("policy.json", policy with { Rulesets = [] });
        Assert.Throws<ConfigurationException>(() => NativePolicy.Load(directory, "policy.json"));
        Assert.Throws<ConfigurationException>(() => NativePolicy.Load(directory, "../policy.json"));
        Write("policy.json", policy with { Sources = [new("legacy-A", new('a', 64), "ifx-csharp", ["no-execution"])] });
        var mapped = NativePolicy.Load(directory, "policy.json");
        var view = JsonSerializer.SerializeToElement(TestRunReportWriter.PolicyView(gate with
            { PolicyFile = "policy.json", PolicySha256 = mapped.Sha256 }), JsonContract.Options);
        Assert.Equal("mapped", view.GetProperty("status").GetString());
        Assert.False(view.GetProperty("coverageComplete").GetBoolean());
        Assert.False(view.GetProperty("sources")[0].GetProperty("editable").GetBoolean());
        Assert.Empty(view.GetProperty("sources")[0].GetProperty("executionIds").EnumerateArray());
        void Write(string name, object value) => File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, JsonContract.Options));
    }

    internal static TestGateConfig EmptyConfig() => new()
    {
        TargetRoot = Path.GetTempPath(), TestProject = "test.csproj", TestExecutableName = "test",
        TargetFramework = "net10.0", Configuration = "Release", NuGetConfig = "NuGet.config",
        LocalFeed = Path.GetTempPath(), PackageCache = Path.GetTempPath(), Rulesets = ["rules.json"],
        OriginalBaseline = Path.Combine(Path.GetTempPath(), "baseline.json"), OriginalBaselineSha256 = new('0', 64),
        RunDirectory = Path.Combine(Path.GetTempPath(), "run")
    };
}
