using System.Net;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class ReportWriter
{
    public static string Json(AnalysisReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonContract.Options);
        using var document = JsonDocument.Parse(json);
        SchemaValidation.Validate(document.RootElement, "report");
        return json;
    }

    public static string Html(AnalysisReport report)
    {
        string E(string? value) => WebUtility.HtmlEncode(value ?? "未提供");
        var text = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>ArchSift 报告</title><style>body{font:16px system-ui;margin:3rem;max-width:1000px;color:#172c31}table{width:100%;border-collapse:collapse}td,th{padding:.7rem;border-bottom:1px solid #cddcdb;text-align:left}pre{white-space:pre-wrap}code{overflow-wrap:anywhere}</style><h1>ArchSift 分析报告</h1>");
        text.Append($"<p>运行 {E(report.RunMetadata.RunId)} · {E(report.Execution)} · 合规 {E(report.Compliance)}</p>");
        text.Append($"<p>目标：{E(report.RunMetadata.TargetRoot)} · 模型：{E(report.Scope.Model)} · 产物绑定：{E(report.BuildContext.Binding)}</p>");
        text.Append($"<p>项目 {report.Coverage.ProjectCount} / 程序集 {report.Coverage.AssemblyCount}；有效违规 {report.Findings.Count(f => f.ExceptionId is null && report.RuleResults.Any(r => r.RuleId == f.RuleId && r.Status == "violation"))}；例外 {report.Findings.Count(f => f.ExceptionId is not null)}</p>");
        text.Append("<h2>规则结果</h2><table><tr><th>规则</th><th>状态</th><th>匹配</th><th>限制</th></tr>");
        foreach (var rule in report.RuleResults)
            text.Append($"<tr><td>{E(rule.RuleId)}</td><td>{E(rule.Status)}</td><td>{rule.Matched}</td><td>{E(string.Join("; ", rule.Limitations))}</td></tr>");
        text.Append("</table><h2>证据</h2><table><tr><th>规则 / 主体</th><th>证据</th><th>例外 ID / 理由</th></tr>");
        foreach (var finding in report.Findings)
            text.Append($"<tr><td>{E(finding.RuleId)}<br>{E(finding.Subject)}</td><td>{E(finding.Message)}<br>{E(finding.Source)} → {E(finding.Target)}<br>{E(finding.Location)}</td><td>{E(finding.ExceptionId)}<br>{E(finding.ExceptionReason)}</td></tr>");
        text.Append("</table><h2>范围与限制</h2><pre>");
        text.Append(E(string.Join("\n", report.Scope.ExternalReferences.Concat(report.Scope.UnresolvedReferences)
            .Concat(report.Scope.UnsupportedConstructs).Concat(report.Limitations).Concat(report.ExecutionErrors))));
        text.Append("</pre><h2>输入身份</h2><code>" + E(report.InputIdentity.Sha256) + "</code></html>");
        return text.ToString();
    }

    public static void Save(RunConfiguration config, AnalysisReport report)
    {
        PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
        Directory.CreateDirectory(config.Output.Directory);
        var directory = Path.Combine(config.Output.Directory, report.RunMetadata.RunId);
        PathSafety.EnsureNoLinks(directory); Directory.CreateDirectory(directory);
        if (config.Output.Formats.Contains("json", StringComparer.Ordinal))
            WriteNew(Path.Combine(directory, "report.json"), Json(report));
        if (config.Output.Formats.Contains("html", StringComparer.Ordinal))
            WriteNew(Path.Combine(directory, "report.html"), Html(report));
        if (config.Output.Formats.Contains("sarif", StringComparer.Ordinal))
            WriteNew(Path.Combine(directory, "report.sarif"), SarifWriter.Json(report));
    }

    private static void WriteNew(string path, string text)
    {
        PathSafety.EnsureNoLinks(path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)); writer.Write(text);
    }
}
