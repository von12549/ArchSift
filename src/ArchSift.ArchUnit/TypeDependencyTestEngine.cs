using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace ArchSift.ArchUnit;

public sealed class TypeDependencyTestContext
{
    internal TypeDependencyTestContext(IType[] types) => Types = types;
    internal IType[] Types { get; }
}

// This test-facing path deliberately does not accept or invent a SourceBound value.
public static class TypeDependencyTestEngine
{
    public static TypeDependencyTestContext LoadContext(IEnumerable<string> assemblyPaths)
    {
        var paths = assemblyPaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0 || paths.Any(path => !File.Exists(path)))
            throw new ConfigurationException("At least one existing assembly path is required.");
        var loader = new ArchLoader().WithoutArchitectureCache().WithoutRuleEvaluationCache();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var name = System.Reflection.AssemblyName.GetAssemblyName(path).Name!;
            if (!names.Add(name)) throw new ConfigurationException("Duplicate assembly name: " + name);
            loader.LoadFilteredDirectory(Path.GetDirectoryName(path)!, Path.GetFileName(path));
        }

        var types = loader.Build().Types.Where(type => !type.IsStub && !type.IsGenericParameter &&
            names.Contains(type.Assembly.Name)).ToArray();
        return new TypeDependencyTestContext(types);
    }

    public static RuleEvaluation Evaluate(RuleBundle bundle, IEnumerable<string> assemblyPaths)
        => Evaluate(bundle, LoadContext(assemblyPaths));

    public static RuleEvaluation Evaluate(RuleBundle bundle, TypeDependencyTestContext context)
    {
        var rules = bundle.Rules.Where(rule => rule.Enabled).ToArray();
        if (rules.Length == 0) throw new ConfigurationException("No enabled JSON rules.");
        if (rules.Any(rule => rule.Type != "type-dependency"))
            throw new ConfigurationException("Only type-dependency rules are supported by the architecture test adapter.");

        var types = context.Types;
        var results = new List<RuleResult>();
        var findings = new List<Finding>();
        foreach (var rule in rules.OrderBy(rule => rule.Id, StringComparer.Ordinal))
        {
            var source = rule.Parameters.GetProperty("source").Deserialize<Selector>(JsonContract.Options)!;
            var target = rule.Parameters.GetProperty("forbiddenTarget").Deserialize<Selector>(JsonContract.Options)!;
            var sources = types.Where(type => Matches(rule.Scope, type) && Matches(source, type)).ToArray();
            var current = new List<Finding>();
            foreach (var type in sources)
            foreach (var dependency in type.Dependencies)
            foreach (var dependencyTarget in TypeDependencyTraversal.Targets(dependency.Target, dependency.TargetGenericArguments))
            {
                if (!Matches(target, dependencyTarget)) continue;
                var subject = rule.Scope.Kind == "namespace" ? type.Namespace.FullName :
                    rule.Scope.Kind == "assembly" ? type.Assembly.Name : type.FullName;
                var identity = string.Join("\0", rule.Id, rule.Scope.Kind, subject, type.FullName, dependencyTarget.FullName);
                current.Add(new(ContentHash.Text(identity), rule.Id, rule.Scope.Kind, subject,
                    "ArchUnitNET detected a forbidden type dependency.", rule.Severity,
                    type.FullName, dependencyTarget.FullName, type.Assembly.Name + ":" + type.FullName));
            }
            var selected = ProjectRuleEvaluator.ApplyExceptions(current.DistinctBy(finding => finding.Id), bundle.Exceptions);
            findings.AddRange(selected);
            var status = selected.Any(finding => finding.ExceptionId is null) ? "violation"
                : sources.Length == 0 ? (rule.Scope.AllowEmpty || source.AllowEmpty ? "not-applicable" : "inconclusive")
                : "pass";
            results.Add(new(rule.Id, status, sources.Length,
                selected.Select(finding => finding.Id).Order(StringComparer.Ordinal).ToArray(),
                status == "inconclusive" ? ["Source selector matched zero types."] : []));
        }
        return new(results.ToArray(), findings.DistinctBy(finding => finding.Id)
            .OrderBy(finding => finding.Id, StringComparer.Ordinal).ToArray());
    }

    private static bool Matches(Selector selector, IType type) => SelectorMatcher.Matches(selector,
        selector.Kind == "namespace" ? type.Namespace.FullName :
        selector.Kind == "assembly" ? type.Assembly.Name : type.FullName);

}
