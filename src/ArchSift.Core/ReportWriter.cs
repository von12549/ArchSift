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
        string E(string? value) => WebUtility.HtmlEncode(value ?? "Not provided");
        var text = new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>ArchSift report</title><style>body{font:16px system-ui;margin:3rem;max-width:1000px;color:#172c31}table{width:100%;border-collapse:collapse}td,th{padding:.7rem;border-bottom:1px solid #cddcdb;text-align:left}pre{white-space:pre-wrap}code{overflow-wrap:anywhere}</style><h1>ArchSift analysis report</h1>");
        text.Append($"<p>Run {E(report.RunMetadata.RunId)} · {E(report.Execution)} · compliance {E(report.Compliance)}</p>");
        if (report.Execution != "completed") text.Append("<p style=\"border-left:4px solid #8a4b00;background:#fff1d2;padding:1rem\">This run is incomplete. Passing rules do not imply full coverage. Review limitations and source binding.</p>");
        text.Append($"<p>Target: {E(report.RunMetadata.TargetRoot)} · model: {E(report.Scope.Model)} · source binding: {E(report.BuildContext.Binding)}</p>");
        text.Append($"<p>Projects {report.Coverage.ProjectCount} / assemblies {report.Coverage.AssemblyCount}; effective violations {report.Findings.Count(f => f.ExceptionId is null && report.RuleResults.Any(r => r.RuleId == f.RuleId && r.Status == "violation"))}; exceptions {report.Findings.Count(f => f.ExceptionId is not null)}</p>");
        text.Append("<h2>Rule results</h2><table><tr><th>Rule</th><th>Status</th><th>Matched</th><th>Limitations</th></tr>");
        foreach (var rule in report.RuleResults)
            text.Append($"<tr><td>{E(rule.RuleId)}</td><td>{E(rule.Status)}</td><td>{rule.Matched}</td><td>{E(string.Join("; ", rule.Limitations))}</td></tr>");
        text.Append("</table><h2>Evidence</h2><table><tr><th>Rule / subject</th><th>Evidence</th><th>Exception ID / reason</th></tr>");
        foreach (var finding in report.Findings)
            text.Append($"<tr><td>{E(finding.RuleId)}<br>{E(finding.Subject)}</td><td>{E(finding.Message)}<br>{E(finding.Source)} → {E(finding.Target)}<br>{E(finding.Location)}</td><td>{E(finding.ExceptionId)}<br>{E(finding.ExceptionReason)}</td></tr>");
        text.Append("</table><h2>Scope and limitations</h2><pre>");
        text.Append(E(string.Join("\n", report.Scope.ExternalReferences.Concat(report.Scope.UnresolvedReferences)
            .Concat(report.Scope.UnsupportedConstructs).Concat(report.Limitations).Concat(report.ExecutionErrors))));
        text.Append("</pre><h2>Input identity</h2><code>" + E(report.InputIdentity.Sha256) + "</code></html>");
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
