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
    }
    private static void Row(string value) { }
}
