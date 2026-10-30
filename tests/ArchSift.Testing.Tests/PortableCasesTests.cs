using ArchSift.TestGate;
using ArchSift.Testing;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class PortableCasesTests
{
    [Fact]
    public void LogicalCasesIgnoreCurrentRunIdsButRejectReplacedRowsAndAmbiguity()
    {
        var identity = CaseIdentities.Create(typeof(PortableCasesTests).GetMethod(nameof(Row), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!, ["first"]);
        var expected = new RequiredCasesDocument(1,"archsift-required-cases","10.0.303","mtp-ctrf/xunit-v3-4.0.1","net10.0","candidate",[new("original",identity)]);
        var original = new MtpTest("execution-path-one","xunit-path-one",identity.Type,identity.Method,"passed",null,null,[])
            { Identity=new("execution-path-one","xunit-path-one",identity) };
        // JSON detail is exercised by the real synthetic target; this unit checks the identity boundary.
        var run = new MtpRun(true,true,1,[original],[original.Id],[original.Id],[],[]);
        var json = new Dictionary<string,string>{{"archsift-json:required","hash"}};
        var first = RequiredCases.Assess(expected,run,json,0);
        Assert.DoesNotContain(first.Reasons, reason => reason.Contains("logical",StringComparison.Ordinal));
        var moved = original with { Id="execution-other-path", CaseId="xunit-other-path", Identity=new("execution-other-path","xunit-other-path",identity) };
        Assert.Equal(first.Reasons,RequiredCases.Assess(expected,run with {Tests=[moved]},json,0).Reasons);
        var replaced = moved with { Identity=moved.Identity! with { Identity=CaseIdentities.Create(typeof(PortableCasesTests).GetMethod(nameof(Row), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!,["replaced"]) } };
        Assert.Contains(RequiredCases.Assess(expected,run with {Tests=[replaced]},json,0).Reasons,reason=>reason.Contains("logical",StringComparison.Ordinal));
        Assert.Contains(RequiredCases.Assess(expected,run with {Tests=[original,moved]},json,0).Reasons,reason=>reason.Contains("ambiguous",StringComparison.Ordinal));
        Assert.Contains(RequiredCases.Assess(expected,run with {Tests=[original with {Identity=null}]},json,0).Reasons,reason=>reason.Contains("projection",StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(()=>RequiredCases.Validate(expected with {Cases=[..expected.Cases,..expected.Cases]}));
        Assert.Throws<InvalidDataException>(()=>RequiredCases.Validate(expected with {SchemaVersion=9}));
        Assert.Throws<InvalidDataException>(()=>RequiredCases.Validate(expected with {Sdk="10.0.401-preview.1"}));
    }
    private static void Row(string value) { }

    [Fact]
    public void DailySdkPolicyRetainsExactBaselineAndBoundsCompatibleSelection()
    {
        var exact = new TestGateConfig { TargetRoot = "C:/source", TestProject = "tests/Tests.csproj",
            TestExecutableName = "Tests", TargetFramework = "net10.0", Configuration = "Release",
            NuGetConfig = "NuGet.Config", LocalFeed = "C:/feed", PackageCache = "C:/cache",
            Rulesets = ["rules.json"], RunDirectory = "C:/state/run" };
        Assert.True(TestGateService.AcceptsSelectedSdk(exact, "10.0.303", "10.0.303"));
        Assert.False(TestGateService.AcceptsSelectedSdk(exact, "10.0.303", "10.0.401"));
        var daily = exact with { MinimumSdkVersion = "10.0.300", MaximumSdkVersionExclusive = "10.1.0" };
        foreach (var sdk in new[] { "10.0.300", "10.0.303", "10.0.401" })
            Assert.True(TestGateService.AcceptsSelectedSdk(daily, "10.0.303", sdk));
        foreach (var sdk in new[] { "10.0.299", "10.1.0", "11.0.100", "10.0.401-preview.1" })
            Assert.False(TestGateService.AcceptsSelectedSdk(daily, "10.0.303", sdk));
        var configured = daily with { RestoreMode = "configured", LocalFeed = "", AuditMode = "offline-architecture" };
        var arguments = NuGetAudit.RestoreArguments(configured with { SchemaVersion = 2 }, "Tests.csproj", "explicit.config");
        Assert.DoesNotContain("--source", arguments);
        Assert.Contains("--locked-mode", arguments);
        Assert.Contains("explicit.config", arguments);
        Assert.Contains("-p:NuGetAudit=false", arguments);
    }
}
