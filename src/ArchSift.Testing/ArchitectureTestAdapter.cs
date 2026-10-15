using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Testing;

public sealed record ArchitectureRuleCase(string CaseId, Rule Rule, LoadedRuleset Ruleset);

public static class ArchitectureTestAdapter
{
    public static (RuleBundle Bundle, ArchitectureRuleCase[] Cases) LoadCases(IEnumerable<string> rulesetPaths)
    {
        var bundle = RuleLoader.Compose(rulesetPaths.Select(RuleLoader.Load));
        var enabled = bundle.Documents.SelectMany(document => document.Ruleset.Rules
            .Where(rule => rule.Enabled).Select(rule => new ArchitectureRuleCase(
                "archsift-json:" + rule.Id, rule, document))).OrderBy(item => item.CaseId, StringComparer.Ordinal).ToArray();
        if (enabled.Length == 0) throw new ConfigurationException("No enabled JSON rules.");
        if (enabled.Any(item => item.Rule.Type != "type-dependency"))
            throw new ConfigurationException("Only type-dependency rules are supported by the architecture test adapter.");
        return (bundle, enabled);
    }

    public static RuleEvaluation EvaluateCase(RuleBundle bundle, ArchitectureRuleCase testCase,
        IEnumerable<string> assemblyPaths)
        => TypeDependencyTestEngine.Evaluate(OneRule(bundle, testCase), assemblyPaths);

    public static RuleEvaluation EvaluateCase(RuleBundle bundle, ArchitectureRuleCase testCase,
        TypeDependencyTestContext context)
        => TypeDependencyTestEngine.Evaluate(OneRule(bundle, testCase), context);

    private static RuleBundle OneRule(RuleBundle bundle, ArchitectureRuleCase testCase)
    {
        var document = bundle.Documents.SingleOrDefault(item => item.Identity == testCase.Ruleset.Identity);
        var rule = document?.Ruleset.Rules.SingleOrDefault(item => item.Id == testCase.Rule.Id && item.Enabled);
        if (document is null || rule is null || testCase.CaseId != "archsift-json:" + rule.Id)
            throw new ConfigurationException("Rule case is not in the loaded bundle.");
        var one = new RuleBundle([new LoadedRuleset(document.Ruleset with
        {
            Rules = [rule],
            Exceptions = bundle.Exceptions.Where(exception => exception.RuleId == rule.Id).ToArray()
        }, document.Identity)]);
        return one;
    }

    public static void AssertPassed(RuleEvaluation evaluation)
    {
        var result = evaluation.Results.Single();
        if (result.Status == "pass") return;
        var details = string.Join("; ", evaluation.Findings.Where(finding => finding.ExceptionId is null)
            .Select(finding => finding.Source + " -> " + finding.Target));
        throw new ArchitectureRuleFailureException(result.RuleId, result.Status,
            details.Length == 0 ? string.Join("; ", result.Limitations) : details);
    }
}

public sealed class ArchitectureRuleFailureException(string ruleId, string status, string details)
    : Exception($"{ruleId}: {status}: {details}");
