using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class RuleDraftService
{
    public static RuleDraft Create(ProjectSnapshot facts)
    {
        var scope = new Selector { Kind = "project", Match = "glob", Value = "**" };
        var candidates = new Ruleset
        {
            SchemaVersion = 1, Id = "draft-" + facts.InputIdentity.Sha256[..12], Version = "1.0.0",
            Description = "Unapproved candidate rules. Review and enable explicitly.",
            Exceptions = [],
            Rules =
            [
                new()
                {
                    Id = "draft-reference-resolution", Type = "graph-integrity", Enabled = false, Scope = scope,
                    Parameters = JsonSerializer.SerializeToElement(new { check = "resolved-references" }), Severity = "warning",
                    Reason = "Consider checking internal project-reference resolution. Existing dependencies are not automatically approved policy."
                },
                new()
                {
                    Id = "draft-supported-frameworks", Type = "target-framework", Enabled = false, Scope = scope,
                    Parameters = JsonSerializer.SerializeToElement(new { allowedFrameworks = new[] { "net8.0", "net9.0", "net10.0" } }),
                    Severity = "warning", Reason = "Review supported frameworks before choosing an allowed policy."
                }
            ]
        };
        return new(facts, candidates,
        [
            "Which project-reference directions should be forbidden?",
            "Which direct NuGet package IDs should be forbidden?",
            "Which namespace/type dependencies need constraints, and which source-bound assemblies will provide evidence?",
            "What naming patterns apply to projects, assemblies, types or source files?",
            "Are any exceptions needed, with explicit rule ID, scope and reason?"
        ]);
    }
}
