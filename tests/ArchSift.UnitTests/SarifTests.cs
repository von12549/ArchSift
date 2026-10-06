using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class SarifTests
{
    [Fact]
    public void SarifKeepsFingerprintsSafeRelativeUrisSuppressionAndNoInventedLines()
    {
        var report = Report() with { Findings = [Finding("src/A file.cs"), Finding("Type.WithoutSource", "type") with { ExceptionId = "reviewed", ExceptionReason = "Historical waiver" }] };
        var json = SarifWriter.Json(report); var run = JsonNode.Parse(json)!["runs"]![0]!;
        Assert.Equal("2.1.0", JsonNode.Parse(json)!["version"]!.GetValue<string>());
        Assert.Equal("src/A%20file.cs", run["results"]![0]!["locations"]![0]!["physicalLocation"]!["artifactLocation"]!["uri"]!.GetValue<string>());
        Assert.Equal(Finding("src/A file.cs").Id, run["results"]![0]!["partialFingerprints"]!["archsiftFindingId/v1"]!.GetValue<string>());
        Assert.Null(run["results"]![1]!["locations"]); Assert.NotNull(run["results"]![1]!["suppressions"]);
        Assert.DoesNotContain("startLine", json); Assert.DoesNotContain("baselineState", json);
        Assert.True(run["invocations"]![0]!["executionSuccessful"]!.GetValue<bool>());
    }

    [Fact]
    public void UnboundEvidenceAndErrorsCannotBeMistakenForViolationsOrACompleteSuccess()
    {
        var report = Report() with { Findings = [Finding("../external.cs")], Execution = "partial", ExecutionErrors = ["Build failed"],
            Limitations = ["assemblies-only"], RuleResults = [new("rule", "inconclusive", 1, [], ["Missing binding"])] };
        var run = JsonNode.Parse(SarifWriter.Json(report))!["runs"]![0]!;
        Assert.Equal("review", run["results"]![0]!["kind"]!.GetValue<string>());
        Assert.Null(run["results"]![0]!["locations"]);
        Assert.False(run["invocations"]![0]!["executionSuccessful"]!.GetValue<bool>());
        Assert.Equal(3, run["invocations"]![0]!["toolExecutionNotifications"]!.AsArray().Count);
    }

    [Fact]
    public void OnlyComprehensiveComparisonSetsBaselineStates()
    {
        var report = Report() with { Findings = [Finding("src/A file.cs")] };
        var identity = new SnapshotIdentity("commit", new string('a', 40), report.InputIdentity, []);
        var comparison = new ComparisonReport { Status = "completed", BaselineIdentity = identity, TargetIdentity = identity,
            Target = report, Baseline = report, Added = report.Findings, Resolved = [Finding("deleted.cs")], RunMetadata = report.RunMetadata };
        var run = JsonNode.Parse(SarifWriter.Json(comparison))!["runs"]![0]!;
        Assert.Equal("new", run["results"]![0]!["baselineState"]!.GetValue<string>());
        Assert.Equal("absent", run["results"]![1]!["baselineState"]!.GetValue<string>());
        Assert.DoesNotContain("baselineState", SarifWriter.Json(comparison with { Status = "inconclusive", Limitations = ["policy-changed"] }));
    }

    private static Finding Finding(string subject, string kind = "source-file") => new(ContentHash.Text(subject), "rule", kind, subject, "Evidence", "warning");
    private static AnalysisReport Report() => new()
    {
        Operation = "verify", InputIdentity = new(ContentHash.Text(""), [new("src/A file.cs", ContentHash.Text("content"), 7, "analysis")]),
        Scope = new([], [], [], [], []), BuildContext = new("existing", "net10.0", "Debug", null, "none"), Execution = "completed", Compliance = "noncompliant",
        RuleResults = [new("rule", "violation", 1, [], [])], Coverage = new(1, 0, false), RunMetadata = new("test", "now", 0, "unused")
    };
}
