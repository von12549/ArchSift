using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class ChainProjectionTests
{
    private static ChainSummary Summary(ChainChild[] entries, params string[] ids) => new(
        new("chain", "1", ids.Select(id => new ChainEntrySnapshot(id, null, null, null, null)).ToArray(),
            new() { Root = "source" }, new(), new("hash", []), new("build", []), "reports", "fixture"),
        "partial", "compliant", 4, 105, entries, [], new("run", "2026-10-13T14:37:31+11:00", 250, "source"));
    private static ChainChild Child(string id, string[] statuses) => new(id, "partial", "compliant", 4, id, null,
        new(105, 105, true), "source-bound", statuses.Select((status, i) => new RuleResult("r" + i, status, 1, [], [])).ToArray(), [], [])
        { EvidenceSha256 = "same-evidence", AssemblyContextCount = 105 };

    [Fact]
    public void Shared105ContextsAndCrossEntryRuleNamesAreCountedCorrectly()
    {
        var counts = new[] { 24, 33, 35, 90 };
        var entries = counts.Select((count, i) => Child("e" + i, Enumerable.Repeat("pass", count).ToArray())).ToArray();
        var summary = Summary(entries, entries.Select(e => e.EntryId).ToArray());
        var a = ChainProjection.Aggregate(summary);
        Assert.Equal(182, a.RecordedRules); Assert.Equal(182, a.StatusCounts.Pass);
        Assert.Equal(105, a.ProjectCount); Assert.Equal(105, a.AssemblyContextCount); Assert.True(a.SourceBound);
        Assert.Equal(4, a.PartialEntries); Assert.Equal(0, a.CompletedEntries); Assert.Equal(4, a.EndedEntries);
        Assert.Equal("partial", a.Execution); Assert.Equal(4, a.ExitCode);
        Assert.Contains(ChainProjection.Text(a), ChainWriter.Html(summary));
    }

    [Fact]
    public void MissingFailuresAndMultipleFindingsNeverManufactureSuccess()
    {
        var mixed = Child("mixed", ["pass", "violation", "inconclusive", "error", "not-applicable"]) with
        { Findings = [new("a", "r1", "type", "T", "one", "error"), new("b", "r1", "type", "T", "two", "error")] };
        var failed = Child("failed", []) with { Execution = "failed", Diagnostic = new("failed", "missing-ruleset", "Missing"), ReportDirectory = null, Coverage = new(0, 0, false) };
        var skipped = Child("skip", []) with { Execution = "skipped", ReportDirectory = null, Coverage = new(0, 0, false) };
        var a = ChainProjection.Aggregate(Summary([mixed, failed, skipped], "mixed", "failed", "skip", "absent"));
        Assert.Equal(new RuleStatusCounts(1, 1, 1, 1, 1), a.StatusCounts); Assert.Equal(5, a.RecordedRules);
        Assert.Equal(2, a.FindingCount); Assert.Equal(1, a.DiagnosticCount); Assert.Equal(1, a.MissingEntries);
        Assert.Equal(1, a.ReportedEntries); Assert.False(a.SourceBound); Assert.Null(a.AssemblyContextCount);
    }

    [Fact]
    public void DuplicateRecordsAreRejectedAndMixedContextIdentityRemainsUnknown()
    {
        var first = Child("a", ["pass"]); var second = Child("b", ["pass"]) with { EvidenceSha256 = "other-proof" };
        Assert.Null(ChainProjection.Aggregate(Summary([first, second], "a", "b")).AssemblyContextCount);
        Assert.Throws<ConfigurationException>(() => ChainProjection.Aggregate(Summary([first, first], "a")));
        Assert.Throws<ConfigurationException>(() => ChainProjection.Aggregate(Summary([first with { RuleResults = [first.RuleResults[0], first.RuleResults[0]] }], "a")));
    }
}
