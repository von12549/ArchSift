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
            Description = "未接受的规则建议；须人类审查并显式启用。",
            Exceptions = [],
            Rules =
            [
                new()
                {
                    Id = "draft-reference-resolution", Type = "graph-integrity", Enabled = false, Scope = scope,
                    Parameters = JsonSerializer.SerializeToElement(new { check = "resolved-references" }), Severity = "warning",
                    Reason = "建议检查内部项目引用能否解析；当前依赖不自动成为允许策略。"
                },
                new()
                {
                    Id = "draft-supported-frameworks", Type = "target-framework", Enabled = false, Scope = scope,
                    Parameters = JsonSerializer.SerializeToElement(new { allowedFrameworks = new[] { "net8.0", "net9.0", "net10.0" } }),
                    Severity = "warning", Reason = "建议先审查工具支持框架范围；允许策略仍需人类选择。"
                }
            ]
        };
        return new(facts, candidates,
        [
            "哪些项目引用方向应被禁止？",
            "哪些直接 NuGet 包 ID 应被禁止？",
            "哪些命名空间/类型依赖需要约束，并使用哪些已绑定程序集？",
            "项目、程序集、类型或文件的命名模式是什么？",
            "是否有带明确规则 ID、范围和理由的例外？"
        ]);
    }
}
