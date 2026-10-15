using ArchSift.Contracts;
using ArchSift.InProject.Runtime;

namespace ArchSift.IntegrationTests;

public sealed class InProjectGatePolicyTests
{
    private static readonly InProjectBindingVerdict Bound = new(1, "ArchSift.InProject", "run",
        new string('a', 64), "1", true, []);

    [Theory]
    [InlineData("pass", "completed", true, 0, "passed")]
    [InlineData("violation", "completed", true, 2, "violation")]
    [InlineData("inconclusive", "completed", true, 4, "inconclusive")]
    [InlineData("error", "completed", true, 3, "execution-error")]
    [InlineData("pass", "cancelled", true, 130, "cancelled")]
    [InlineData("pass", "failed", true, 3, "execution-error")]
    [InlineData("pass", "completed", false, 4, "inconclusive")]
    public void Gate_UsesBindingCoverageAndRuleStatus(string status, string execution, bool sourceBound,
        int exit, string reason)
    {
        var coverage = Complete("rules@1/check", status);
        var verdict = Bound with { SourceBound = sourceBound };
        var decision = InProjectGatePolicy.Decide(execution, verdict, coverage.Coverage, coverage.Rules,
            new HashSet<string>(StringComparer.Ordinal), true);
        Assert.Equal(exit, decision.ExitCode);
        Assert.Equal(reason, decision.Reason);
    }

    [Fact]
    public void Gate_BlocksZeroTestsMissingRuleAndAllNotApplicable()
    {
        var complete = Complete("rules@1/check", "pass");
        Assert.Equal(4, InProjectGatePolicy.Decide("completed", Bound,
            complete.Coverage with { Complete = false }, complete.Rules, new HashSet<string>(), true).ExitCode);
        Assert.Equal(4, InProjectGatePolicy.Decide("completed", Bound,
            complete.Coverage with { ExecutedRules = [] }, complete.Rules, new HashSet<string>(), true).ExitCode);
        Assert.Equal(4, InProjectGatePolicy.Decide("completed", Bound,
            complete.Coverage, [complete.Rules[0] with { Discovered = 0 }], new HashSet<string>(), true).ExitCode);
        var empty = Complete("rules@1/check", "not-applicable");
        Assert.Equal(4, InProjectGatePolicy.Decide("completed", Bound,
            empty.Coverage, empty.Rules, new HashSet<string> { "rules@1/check" }, true).ExitCode);
    }

    [Fact]
    public void Gate_AllowsOnlyAttributedNonRequiredPartial()
    {
        var complete = Complete("rules@1/check", "pass");
        Assert.Equal(4, InProjectGatePolicy.Decide("partial", Bound, complete.Coverage,
            complete.Rules, new HashSet<string>(), true).ExitCode);
        Assert.Equal(0, InProjectGatePolicy.Decide("partial", Bound, complete.Coverage,
            complete.Rules, new HashSet<string>(), true, limitationsOutsideRequired: true).ExitCode);
    }

    [Fact]
    public void HtmlProjection_EscapesRuleAndFindingText()
    {
        var key = "rules@1/<script>alert(1)</script>";
        var complete = Complete(key, "violation");
        var report = new InProjectGateResult(1, "ArchSift.InProject", "run", "worktree",
            "completed", Bound, "daily", complete.Coverage, complete.Rules,
            [new Finding("finding", "check", "type", "<img src=x>", "<script>bad()</script>", "error")],
            [], new("block", "violation", 2), new("available", "daily-index.json"), "current");
        var html = InProjectReportWriter.Html(report);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img src=x>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    private static (InProjectGateCoverage Coverage, InProjectRuleCoverage[] Rules) Complete(string key,
        string status) => (new(true, [key], [key]),
        [new(key, "B", ["ArchitectureRules.Evaluate(" + key + ")"], 1, 1, 1,
            status, 1, [], [])]);
}
