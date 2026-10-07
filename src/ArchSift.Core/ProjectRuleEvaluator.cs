using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public sealed record RuleEvaluation(RuleResult[] Results, Finding[] Findings);

public static class ProjectRuleEvaluator
{
    public static RuleEvaluation Evaluate(ProjectSnapshot snapshot, RuleBundle bundle)
    {
        var results = new List<RuleResult>();
        var findings = new List<Finding>();
        foreach (var rule in bundle.Rules.Where(r => r.Enabled))
        {
            if (rule.Type == "type-dependency" || (rule.Type == "naming" &&
                rule.Parameters.GetProperty("subjectKind").GetString() is "type" or "assembly")) continue;
            var scoped = snapshot.Projects.Where(p => SelectorMatcher.Matches(rule.Scope, p.Id)).ToArray();
            var matched = scoped.Length;
            var allowEmpty = rule.Scope.AllowEmpty;
            var limits = new List<string>();
            var current = new List<Finding>();
            if (rule.Type == "naming" && rule.Scope.Kind == "source-file")
            {
                var files = snapshot.SourceFiles.Where(f => SelectorMatcher.Matches(rule.Scope, f)).ToArray();
                matched = files.Length;
                var requirement = RuleSemantics.NameSelector(rule);
                foreach (var file in files)
                    if (!SelectorMatcher.Matches(requirement, file))
                        Add("source-file", file, "文件名/路径不符合命名规则。", location: file);
            }
            else
            {
                if (scoped.Any(p => !p.Supported)) limits.Add("Unsupported project inputs in scope.");
                switch (rule.Type)
                {
                    case "project-reference":
                        var source = rule.Parameters.GetProperty("source").Deserialize<Selector>(JsonContract.Options)!;
                        var target = rule.Parameters.GetProperty("target").Deserialize<Selector>(JsonContract.Options)!;
                        var sources = scoped.Where(p => SelectorMatcher.Matches(source, p.Id)).ToArray();
                        matched = sources.Length; allowEmpty = source.AllowEmpty || rule.Scope.AllowEmpty;
                        foreach (var project in sources)
                        {
                            if (!project.ReferencesComplete || project.References.Any(r => r.Status != "resolved"))
                                limits.Add(project.Id + ": project-reference coverage incomplete.");
                            foreach (var edge in project.References.Where(r => r.TargetId is not null))
                                if (SelectorMatcher.Matches(target, edge.TargetId!))
                                    Add("project", project.Id, "禁止的项目引用。", project.Id, edge.TargetId, project.Id);
                        }
                        break;
                    case "project-reference-allowlist":
                        var allowSource = rule.Parameters.GetProperty("source").Deserialize<Selector>(JsonContract.Options)!;
                        var allowedTargets = rule.Parameters.GetProperty("allowedTargets").EnumerateArray()
                            .Select(value => value.Deserialize<Selector>(JsonContract.Options)!).ToArray();
                        var allowSources = scoped.Where(p => SelectorMatcher.Matches(allowSource, p.Id)).ToArray();
                        matched = allowSources.Length; allowEmpty = allowSource.AllowEmpty || rule.Scope.AllowEmpty;
                        foreach (var project in allowSources)
                        {
                            if (!project.ReferencesComplete || project.References.Any(r => r.Status != "resolved"))
                                limits.Add(project.Id + ": project-reference coverage incomplete.");
                            foreach (var edge in project.References.Where(r => r.Status == "resolved" && r.TargetId is not null))
                                if (!allowedTargets.Any(selector => SelectorMatcher.Matches(selector, edge.TargetId!)))
                                    Add("project", project.Id, "项目引用不在允许列表。", project.Id, edge.TargetId, project.Id);
                        }
                        break;
                    case "graph-integrity":
                        var check = rule.Parameters.GetProperty("check").GetString();
                        if (check == "solution-membership")
                        {
                            if (snapshot.Entry is null || Path.GetExtension(snapshot.Entry).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                                limits.Add("Solution membership requires an explicit solution entry.");
                            else
                                foreach (var project in scoped.Where(p => snapshot.Scope.UnlistedProjects.Contains(p.Id, StringComparer.Ordinal)))
                                    Add("project", project.Id, "范围内项目未列入 solution。", location: project.Id);
                            if (snapshot.Scope.UnsupportedConstructs.Any(s => s.Contains("solution", StringComparison.Ordinal)))
                                limits.Add("Solution parsing coverage incomplete.");
                            if (snapshot.Scope.UnresolvedReferences.Any(s => s.StartsWith("entry ->", StringComparison.Ordinal)))
                                limits.Add("Solution lists unavailable projects.");
                        }
                        else
                        {
                            foreach (var project in scoped)
                            {
                                if (!project.ReferencesComplete) limits.Add(project.Id + ": reference declarations incomplete.");
                                foreach (var edge in project.References)
                                {
                                    if (edge.Status == "missing") Add("project", project.Id, "内部项目引用无法解析。", project.Id, edge.TargetId, project.Id);
                                    if (edge.Status is "external" or "unsupported") limits.Add(project.Id + ": external/unsupported reference is uncovered.");
                                }
                            }
                        }
                        break;
                    case "target-framework":
                        var allowed = rule.Parameters.GetProperty("allowedFrameworks").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
                        foreach (var project in scoped)
                        {
                            if (!project.FrameworkComplete) limits.Add(project.Id + ": framework cannot be determined.");
                            else foreach (var framework in project.Frameworks.Where(f => !allowed.Contains(f)))
                                Add("project", project.Id, "声明的目标框架不在允许列表。", project.Id, framework, project.FrameworkSource);
                        }
                        break;
                    case "nuget-denylist":
                        var forbidden = rule.Parameters.GetProperty("forbiddenPackageIds").EnumerateArray().Select(x => x.GetString()!)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        foreach (var project in scoped)
                        {
                            if (!project.PackagesComplete) limits.Add(project.Id + ": direct package declarations incomplete.");
                            foreach (var package in project.Packages.Where(p => p.Status == "declared" && forbidden.Contains(p.Id)))
                                Add("project", project.Id, "直接 PackageReference 命中禁止包 ID。", project.Id, package.Id.ToLowerInvariant(), project.Id);
                        }
                        break;
                    case "nuget-allowlist":
                        var allowedPackages = rule.Parameters.GetProperty("allowedPackageIds").EnumerateArray().Select(x => x.GetString()!)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        foreach (var project in scoped)
                        {
                            if (!project.PackagesComplete) limits.Add(project.Id + ": direct package declarations incomplete.");
                            foreach (var package in project.Packages.Where(p => p.Status == "declared" && !allowedPackages.Contains(p.Id)))
                                Add("project", project.Id, "直接 PackageReference 不在允许包 ID 列表。", project.Id, package.Id.ToLowerInvariant(), project.Id);
                        }
                        break;
                    case "naming":
                        var name = RuleSemantics.NameSelector(rule);
                        foreach (var project in scoped)
                            if (!SelectorMatcher.Matches(name, project.Name)) Add("project", project.Id, "项目名称不符合命名规则。", location: project.Id);
                        break;
                }
            }
            current = ApplyExceptions(current, bundle.Exceptions);
            findings.AddRange(current);
            var status = current.Any(f => f.ExceptionId is null) ? "violation"
                : matched == 0 ? (allowEmpty ? "not-applicable" : "inconclusive")
                : limits.Count > 0 ? "inconclusive" : "pass";
            if (matched == 0 && !allowEmpty) limits.Add("Source selector matched zero subjects.");
            results.Add(new(rule.Id, status, matched, current.Select(f => f.Id).Order(StringComparer.Ordinal).ToArray(),
                limits.Distinct().Order(StringComparer.Ordinal).ToArray()));

            void Add(string kind, string subject, string message, string? source = null, string? target = null, string? location = null)
            {
                var id = ContentHash.Text(string.Join("\0", rule.Id, kind, subject, source ?? "", target ?? ""));
                current.Add(new(id, rule.Id, kind, subject, message, rule.Severity, source, target, location));
            }
        }
        return new(results.OrderBy(r => r.RuleId, StringComparer.Ordinal).ToArray(),
            findings.DistinctBy(f => f.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToArray());
    }

    public static List<Finding> ApplyExceptions(IEnumerable<Finding> findings, RuleException[] exceptions) =>
        findings.Select(f =>
        {
            var exception = exceptions.Where(e => e.RuleId == f.RuleId && e.Scope.Kind == f.SubjectKind &&
                    SelectorMatcher.Matches(e.Scope, f.Subject))
                .OrderBy(e => e.Scope.Match == "exact" ? 0 : 1)
                .ThenByDescending(e => e.Scope.Value.Count(character => character is not ('*' or '?')))
                .ThenBy(e => e.Scope.Value.Count(character => character is '*' or '?'))
                .ThenBy(e => e.Scope.Value, StringComparer.Ordinal)
                .ThenBy(e => e.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            return exception is null ? f : f with { ExceptionId = exception.Id, ExceptionReason = exception.Reason };
        }).ToList();
}
