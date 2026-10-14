using ArchSift.Contracts;

namespace ArchSift.InProject.Runtime;

public static class InProjectGatePolicy
{
    public static InProjectGateDecision Decide(string execution, InProjectBindingVerdict binding,
        InProjectGateCoverage coverage, IReadOnlyList<InProjectRuleCoverage> rules,
        IReadOnlySet<string> allowEmptyRuleKeys, bool fresh, bool limitationsOutsideRequired = false)
    {
        if (execution == "cancelled") return Block("cancelled", 130);
        if (execution == "invalid-input") return Block("invalid-input", 5);
        if (execution == "failed") return Block("execution-error", 3);
        if (execution is not ("completed" or "partial")) return Block("execution-error", 3);
        if (!binding.SourceBound || !fresh || !CompleteCoverage(coverage, rules))
            return Block("inconclusive", 4);
        if (rules.Any(rule => rule.Status == "error")) return Block("execution-error", 3);
        if (rules.Any(rule => rule.Status is "inconclusive" or "unknown"))
            return Block("inconclusive", 4);
        if (rules.Any(rule => rule.Status == "not-applicable" &&
                !allowEmptyRuleKeys.Contains(rule.RuleKey)))
            return Block("inconclusive", 4);
        if (rules.All(rule => rule.Status == "not-applicable")) return Block("inconclusive", 4);
        if (execution == "partial" && !limitationsOutsideRequired)
            return Block("inconclusive", 4);
        if (rules.Any(rule => rule.Status == "violation")) return Block("violation", 2);
        if (rules.All(rule => rule.Status is "pass" or "not-applicable") &&
            rules.Any(rule => rule.Status == "pass"))
            return new("allow", "passed", 0);
        return Block("inconclusive", 4);
    }

    private static bool CompleteCoverage(InProjectGateCoverage coverage,
        IReadOnlyList<InProjectRuleCoverage> rules)
    {
        if (!coverage.Complete || coverage.RequiredRules.Length == 0 ||
            coverage.RequiredRules.Length != coverage.RequiredRules.Distinct(StringComparer.Ordinal).Count() ||
            coverage.ExecutedRules.Length != coverage.ExecutedRules.Distinct(StringComparer.Ordinal).Count() ||
            rules.Count != coverage.RequiredRules.Length ||
            rules.Select(rule => rule.RuleKey).Distinct(StringComparer.Ordinal).Count() != rules.Count)
            return false;
        var required = coverage.RequiredRules.ToHashSet(StringComparer.Ordinal);
        if (!required.SetEquals(coverage.ExecutedRules) ||
            !required.SetEquals(rules.Select(rule => rule.RuleKey))) return false;
        return rules.All(rule => rule.Started == 1 && rule.Finished == 1 &&
            (rule.Classification == "A" && rule.Discovered == 0 && rule.TestIds.Length == 0 ||
             rule.Classification == "B" && rule.Discovered == 1 && rule.TestIds.Length == 1));
    }

    private static InProjectGateDecision Block(string reason, int exit) => new("block", reason, exit);
}
