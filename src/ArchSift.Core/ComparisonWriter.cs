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
        var text = new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>ArchSift changes comparison</title><style>body{font:16px system-ui;margin:3rem;max-width:1000px}pre{white-space:pre-wrap}li{overflow-wrap:anywhere}</style><h1>ArchSift changes comparison</h1>");
        text.Append($"<p>{E(report.Status)}; baseline {E(report.BaselineIdentity.Commit)}; target {E(report.TargetIdentity.Commit)}</p>");
        foreach (var group in new[] { ("Added", report.Added), ("Existing", report.Existing), ("Resolved", report.Resolved), ("Unclassified", report.Unclassified) })
        {
            text.Append($"<h2>{group.Item1} ({group.Item2.Length})</h2><ul>");
            foreach (var f in group.Item2) text.Append($"<li>{E(f.RuleId)} / {E(f.Subject)}: {E(f.Message)}</li>");
            text.Append("</ul>");
        }
        text.Append("<h2>Policy changes (not code fixes)</h2><ul>");
        foreach (var p in report.PolicyChanges) text.Append($"<li>{E(p.RuleId)}: {E(p.Kind)}</li>");
        text.Append("</ul><h2>Limitations</h2><pre>" + E(string.Join("\n", report.Limitations.Concat(report.ExecutionErrors))) + "</pre>");
        foreach (var side in new[] { ("Full baseline report", report.Baseline), ("Full target report", report.Target) })
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
