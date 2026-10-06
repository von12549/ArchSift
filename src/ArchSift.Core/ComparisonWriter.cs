using System.Net;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class ComparisonWriter
{
    public static string Json(ComparisonReport report)
    {
        if (report.Baseline is not null) ReportWriter.Json(report.Baseline);
        if (report.Target is not null) ReportWriter.Json(report.Target);
        var json = JsonSerializer.Serialize(report, JsonContract.Options);
        using var document = JsonDocument.Parse(json); SchemaValidation.Validate(document.RootElement, "comparison");
        return json;
    }
    public static string Html(ComparisonReport report)
    {
        string E(string? value) => WebUtility.HtmlEncode(value ?? "worktree");
        var text = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>ArchSift 变更比较</title><style>body{font:16px system-ui;margin:3rem;max-width:1000px}pre{white-space:pre-wrap}li{overflow-wrap:anywhere}</style><h1>ArchSift 变更比较</h1>");
        text.Append($"<p>{E(report.Status)}；基线 {E(report.BaselineIdentity.Commit)}；目标 {E(report.TargetIdentity.Commit)}</p>");
        foreach (var group in new[] { ("新增", report.Added), ("既有", report.Existing), ("已解决", report.Resolved), ("不可比较", report.Unclassified) })
        {
            text.Append($"<h2>{group.Item1} ({group.Item2.Length})</h2><ul>");
            foreach (var f in group.Item2) text.Append($"<li>{E(f.RuleId)} / {E(f.Subject)}：{E(f.Message)}</li>");
            text.Append("</ul>");
        }
        text.Append("<h2>规则变化（不作为问题解决）</h2><ul>");
        foreach (var p in report.PolicyChanges) text.Append($"<li>{E(p.RuleId)}：{E(p.Kind)}</li>");
        text.Append("</ul><h2>限制</h2><pre>" + E(string.Join("\n", report.Limitations.Concat(report.ExecutionErrors))) + "</pre>");
        foreach (var side in new[] { ("基线完整报告", report.Baseline), ("目标完整报告", report.Target) })
            if (side.Item2 is not null) text.Append($"<details><summary>{side.Item1}</summary><pre>{E(ReportWriter.Json(side.Item2))}</pre></details>");
        return text.Append("</html>").ToString();
    }
    public static void Save(RunConfiguration config, ComparisonReport report)
    {
        PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
        var root = PathSafety.Under(config.Output.Directory, report.RunMetadata.RunId); Directory.CreateDirectory(root);
        foreach (var format in config.Output.Formats.Where(f => f is "json" or "html" or "sarif"))
        {
            var path = PathSafety.Under(root, "comparison." + format);
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)); writer.Write(format == "json" ? Json(report) : format == "html" ? Html(report) : SarifWriter.Json(report));
        }
    }
}
