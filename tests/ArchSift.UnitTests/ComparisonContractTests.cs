using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class ComparisonContractTests
{
    [Theory]
    [InlineData("context")]
    [InlineData("coverage")]
    [InlineData("failure")]
    [InlineData("changed-input")]
    public void MissingComparableEvidenceNeverClaimsAddedOrResolved(string reason)
    {
        var before = Report([Finding("old")]); var after = Report([Finding("new")]);
        if (reason == "context") after = after with { BuildContext = after.BuildContext with { Configuration = "Release" } };
        if (reason == "coverage") before = before with { RuleResults = [new("rule", "inconclusive", 0, [], ["Missing input"])] };
        if (reason == "failure") before = before with { ExecutionErrors = ["Build failed"], Execution = "partial" };
        var identity = new SnapshotIdentity("commit", new string('a', 40), new(ContentHash.Text(""), []), []);
        var result = ComparisonService.Compare(before, after, identity, identity, new() { ["rule"] = "same" }, new() { ["rule"] = "same" },
            new("test", "now", 0, "unused"), [], reason == "changed-input" ? ["source-changed-during-comparison"] : [], reason == "failure" ? [3] : []);
        Assert.Empty(result.Report.Added); Assert.Empty(result.Report.Resolved); Assert.Equal("inconclusive", result.Report.Status);
        Assert.Equal(reason == "failure" ? 3 : 4, result.ExitCode);
        Assert.Contains("&lt;script&gt;", ComparisonWriter.Html(result.Report));
        Assert.DoesNotContain("<script>", ComparisonWriter.Html(result.Report));
        ComparisonWriter.Json(result.Report);
    }

    [Fact]
    public void CancelledComparisonDoesNotBecomeACompletedComparison()
    {
        var identity = new SnapshotIdentity("commit", null, new(ContentHash.Text(""), []), []);
        var result = ComparisonService.Compare(Report([]), null, identity, identity, [], [], new("test", "now", 0, "unused"), [], [], [130]);
        Assert.Equal(130, result.ExitCode); Assert.Equal("cancelled", result.Report.Status); Assert.Empty(result.Report.Resolved);
    }

    private static Finding Finding(string id) => new(ContentHash.Text(id), "rule", "project", "<script>", "unsafe <script>", "warning");
    private static AnalysisReport Report(Finding[] findings) => new()
    {
        Operation = "verify", InputIdentity = new(ContentHash.Text(""), []), Scope = new([], [], [], [], []),
        BuildContext = new("existing", "net10.0", "Debug", null, "none"), Execution = "completed", Compliance = "noncompliant",
        Findings = findings, RuleResults = [new("rule", "violation", 1, findings.Select(f => f.Id).ToArray(), [])],
        Coverage = new(1, 0, false), RunMetadata = new("test", "now", 0, "unused")
    };
}
