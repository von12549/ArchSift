using ArchSift.TestGate;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class MtpCoverageTests
{
    private const string Sha = "b9669d62d9195f9322a1fda34b46003472564070c8ec492d71234900cc88b341";
    private static readonly IReadOnlyDictionary<string, string> JsonCases =
        new Dictionary<string, string> { ["archsift-json:allowed-source"] = Sha };

    [Fact]
    public void NativeIdsAndTerminalEventsPermitFullGate()
    {
        var run = MtpEvents.Parse(Events());
        var coverage = MtpEvents.Assess(run, ["original-row-1"], JsonCases, 0);
        Assert.Equal(2, run.DiscoveredCaseCount);
        Assert.Equal(2, run.Tests.Length);
        Assert.Equal("OriginalTests", Assert.Single(run.Tests, test => test.Id == "original-row-1").ClassName);
        Assert.Equal("archsift-json:allowed-source", Assert.Single(run.Tests, test => test.Id == "json-row-1").JsonCaseId);
        Assert.True(coverage.Complete);
        Assert.Equal(0, coverage.ExitCode);
    }

    [Fact]
    public void MissingOriginalRowCannotBeOffsetByAnExtraJsonCase()
    {
        var run = MtpEvents.Parse(Events());
        var coverage = MtpEvents.Assess(run, ["original-row-1", "original-row-2"], JsonCases, 0);
        Assert.False(coverage.Complete);
        Assert.Equal(4, coverage.ExitCode);
        Assert.Equal("original-row-2", Assert.Single(coverage.MissingOriginalIds));
    }

    [Fact]
    public void SkipDuplicateSubsetAndWrongRulesetBytesBlock()
    {
        var original = Events();
        var skipped = original.Select(line => line.Replace("\"$type\":\"test-passed\",\"TestUniqueID\":\"original-row-1\"",
            "\"$type\":\"test-skipped\",\"TestUniqueID\":\"original-row-1\"")).ToArray();
        Assert.Equal(4, MtpEvents.Assess(MtpEvents.Parse(skipped), ["original-row-1"], JsonCases, 0).ExitCode);
        var duplicated = original.Append("{\"$type\":\"test-starting\",\"TestUniqueID\":\"json-row-1\",\"TestCaseUniqueID\":\"json-case\"}");
        Assert.Equal(4, MtpEvents.Assess(MtpEvents.Parse(duplicated), ["original-row-1"], JsonCases, 0).ExitCode);
        Assert.Equal(4, MtpEvents.Assess(MtpEvents.Parse(original), ["original-row-1"], JsonCases, 0, subset: true).ExitCode);
        Assert.Equal(4, MtpEvents.Assess(MtpEvents.Parse(original), ["original-row-1"],
            new Dictionary<string, string> { ["archsift-json:allowed-source"] = new string('0', 64) }, 0).ExitCode);
    }

    [Fact]
    public void AssertionFailureIsDistinctFromMissingTerminalAndProcessError()
    {
        var original = Events();
        var failed = original.Select(line => line.Replace("\"$type\":\"test-passed\",\"TestUniqueID\":\"original-row-1\"",
            "\"$type\":\"test-failed\",\"TestUniqueID\":\"original-row-1\"")).ToArray();
        Assert.Equal(2, MtpEvents.Assess(MtpEvents.Parse(failed), ["original-row-1"], JsonCases, 1).ExitCode);
        var missingTerminal = original.Where(line => !line.Contains("\"$type\":\"test-finished\",\"TestUniqueID\":\"original-row-1\""));
        Assert.Equal(4, MtpEvents.Assess(MtpEvents.Parse(missingTerminal), ["original-row-1"], JsonCases, 0).ExitCode);
        Assert.Equal(3, MtpEvents.Assess(MtpEvents.Parse(original), ["original-row-1"], JsonCases, 1).ExitCode);
    }

    private static string[] Events() =>
    [
        "{\"$type\":\"discovery-complete\",\"TestCasesToRun\":2}",
        "{\"$type\":\"test-case-starting\",\"TestCaseUniqueID\":\"original-case\",\"TestClassName\":\"OriginalTests\",\"TestMethodName\":\"Theory\"}",
        "{\"$type\":\"test-case-starting\",\"TestCaseUniqueID\":\"json-case\",\"TestClassName\":\"JsonTests\",\"TestMethodName\":\"JsonRule\"}",
        "{\"$type\":\"test-starting\",\"TestUniqueID\":\"original-row-1\",\"TestCaseUniqueID\":\"original-case\"}",
        "{\"$type\":\"test-passed\",\"TestUniqueID\":\"original-row-1\"}",
        "{\"$type\":\"test-finished\",\"TestUniqueID\":\"original-row-1\"}",
        "{\"$type\":\"test-starting\",\"TestUniqueID\":\"json-row-1\",\"TestCaseUniqueID\":\"json-case\"}",
        "{\"$type\":\"test-output\",\"TestUniqueID\":\"json-row-1\",\"Output\":\"ARCHSIFT_JSON_CASE:archsift-json:allowed-source:" + Sha + "\\n\"}",
        "{\"$type\":\"test-passed\",\"TestUniqueID\":\"json-row-1\"}",
        "{\"$type\":\"test-finished\",\"TestUniqueID\":\"json-row-1\"}",
        "{\"$type\":\"test-assembly-finished\"}"
    ];
}
