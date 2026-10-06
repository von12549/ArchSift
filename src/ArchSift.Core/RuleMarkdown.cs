using System.Text;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class RuleMarkdown
{
    public static string Render(LoadedRuleset document)
    {
        var rules = document.Ruleset;
        var text = new StringBuilder();
        text.AppendLine("# " + Escape(rules.Id));
        text.AppendLine();
        text.AppendLine("此文件是 JSON 的只读阅读投影；编辑它不会改变执行规则。");
        text.AppendLine();
        text.AppendLine($"来源：{Escape(Path.GetFileName(document.Identity.Path))}；版本：{Escape(rules.Version)}；SHA-256：{document.Identity.Sha256}");
        text.AppendLine();
        text.AppendLine(Escape(rules.Description));
        foreach (var rule in rules.Rules)
        {
            text.AppendLine();
            text.AppendLine("## " + Escape(rule.Id));
            text.AppendLine();
            text.AppendLine($"类型：{rule.Type}；启用：{rule.Enabled.ToString().ToLowerInvariant()}；severity：{rule.Severity}");
            text.AppendLine();
            text.AppendLine($"范围：{rule.Scope.Kind} / {rule.Scope.Match} / {Escape(rule.Scope.Value)}；allowEmpty={rule.Scope.AllowEmpty.ToString().ToLowerInvariant()}");
            text.AppendLine();
            text.AppendLine("理由：" + Escape(rule.Reason));
            text.AppendLine();
            foreach (var parameter in rule.Parameters.EnumerateObject())
                text.AppendLine($"- {Escape(parameter.Name)}：{Escape(parameter.Value.ToString())}");
        }
        text.AppendLine();
        text.AppendLine("## 例外");
        text.AppendLine();
        foreach (var exception in rules.Exceptions)
            text.AppendLine($"- {Escape(exception.Id)} → {Escape(exception.RuleId)}：{exception.Scope.Kind}/{exception.Scope.Match}/{Escape(exception.Scope.Value)}；理由：{Escape(exception.Reason)}");
        if (rules.Exceptions.Length == 0) text.AppendLine("无。");
        return text.ToString().Replace("\r\n", "\n");
    }

    private static string Escape(string value) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\\", "\\\\").Replace("*", "\\*").Replace("_", "\\_").Replace("[", "\\[").Replace("]", "\\]")
        .Replace("\x60", @"\`").Replace("#", "\\#").Replace("\r", "").Replace("\n", " ");
}
