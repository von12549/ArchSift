using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class ChainWriter
{
    public static string Json(ChainSummary summary)
    {
        var text = JsonSerializer.Serialize(summary, JsonContract.Options);
        using var doc = JsonDocument.Parse(text); SchemaValidation.Validate(doc.RootElement, summary.SchemaVersion == 2 ? "chain-summary-v2" : "chain-summary"); return text;
    }
    public static string Html(ChainSummary summary)
    {
        string E(string? text) => WebUtility.HtmlEncode(text ?? "Unavailable");
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>ArchSift chain summary</title><style>body{font:16px Segoe UI,sans-serif;margin:2rem;color:#17324d;max-width:1100px}table{border-collapse:collapse;width:100%}td,th{padding:.7rem;border-bottom:1px solid #c2d1df;text-align:left}code,pre{overflow-wrap:anywhere;white-space:pre-wrap}.limit{border-left:4px solid #8a4b00;padding:1rem;background:#fff2d9}details{margin:1rem 0}</style><h1>Chain summary</h1>");
        html.Append($"<p>{E(summary.Snapshot.ChainId)} / {E(summary.Snapshot.ChainVersion)}</p><p>Report tool version: {E(summary.ToolVersion)}; max concurrency: {summary.Snapshot.ExecutionOptions?.MaxConcurrency.ToString() ?? "Unavailable (legacy report)"}</p><p>Execution: <strong>{E(summary.Execution)}</strong>; policy: <strong>{E(summary.Compliance)}</strong>; exit: {summary.ExitCode}</p>");
        html.Append($"<p>Target: {E(summary.Snapshot.Target.Root)}; kind: {E(summary.Snapshot.TargetKind)}; unique captured projects: {summary.ProjectCount}</p><p>Source input: <code>{E(summary.Snapshot.InputIdentity.Sha256)}</code></p><p>Build input: <code>{E(summary.Snapshot.BuildInputIdentity.Sha256)}</code></p>");
        if (summary.Execution != "completed") html.Append("<p class=\"limit\">This run is incomplete. Passing policy results do not prove full coverage. Review each entry's limitations and source binding.</p>");
        foreach (var child in summary.Entries)
        {
            var entry = summary.Snapshot.Entries.Single(e => e.EntryId == child.EntryId);
            html.Append($"<section><h2>{E(entry.RulesetIdentity?.Id ?? child.EntryId)}</h2><p>Entry: {E(child.EntryId)}; execution: {E(child.Execution)}; compliance: {E(child.Compliance)}; exit: {child.ExitCode}</p><p>Source: {E(entry.SourcePath)}; SHA-256: {E(entry.RulesetIdentity?.Sha256)}</p><p>Projects: {child.Coverage.ProjectCount}; assemblies: {child.Coverage.AssemblyCount}; source-bound: {child.Coverage.SourceBound}; binding: {E(child.Binding)}</p>");
            if (child.ReportDirectory is { } relative)
                foreach (var format in new[] { "json", "html", "sarif" })
                    html.Append($"<a href=\"{E(relative + "/" + (child.Diagnostic is null ? "report." : "diagnostic.") + format)}\">{format.ToUpperInvariant()}</a> ");
            if (child.Diagnostic is { } diagnostic) html.Append($"<p class=\"limit\">{E(diagnostic.Code)}: {E(diagnostic.Message)}</p>");
            if (child.RuleResults.Length > 0)
            {
                html.Append("<details><summary>Rule results</summary><table><tr><th>Qualified rule</th><th>Status</th><th>Matched</th><th>Limitations</th></tr>");
                foreach (var rule in child.RuleResults) html.Append($"<tr><td>{E(child.EntryId + "/" + rule.RuleId)}</td><td>{E(rule.Status)}</td><td>{rule.Matched}</td><td>{E(string.Join("; ", rule.Limitations))}</td></tr>");
                html.Append("</table></details>");
            }
            if (child.Findings.Length > 0)
            {
                html.Append("<details><summary>Findings and exceptions</summary><table><tr><th>Rule</th><th>Subject</th><th>Evidence</th><th>Exception</th></tr>");
                foreach (var finding in child.Findings) html.Append($"<tr><td>{E(child.EntryId + "/" + finding.RuleId)}</td><td>{E(finding.Subject)}</td><td>{E(finding.Message)}</td><td>{E(finding.ExceptionId)} {E(finding.ExceptionReason)}</td></tr>");
                html.Append("</table></details>");
            }
            if (child.Limitations.Length > 0) html.Append($"<p class=\"limit\">Coverage limitations: {child.Limitations.Length}</p><details><summary>Coverage details</summary><pre>{E(string.Join("\n", child.Limitations))}</pre></details>");
            html.Append("</section>");
        }
        html.Append($"<pre>{E(string.Join("\n", summary.Limitations))}</pre></html>"); return html.ToString();
    }
    public static string Sarif(ChainSummary summary)
    {
        var orchestration = DiagnosticRun(summary.ToolVersion);
        orchestration["properties"] = new JsonObject { ["chainId"] = summary.Snapshot.ChainId, ["execution"] = summary.Execution, ["compliance"] = summary.Compliance,
            ["inputIdentity"] = summary.Snapshot.InputIdentity.Sha256, ["buildInputIdentity"] = summary.Snapshot.BuildInputIdentity.Sha256 };
        orchestration["invocations"]![0]!["executionSuccessful"] = summary.Execution == "completed";
        var notifications = orchestration["invocations"]![0]!["toolExecutionNotifications"]!.AsArray();
        var runs = new JsonArray();
        foreach (var child in summary.Entries)
        {
            if (child.Diagnostic is { } diagnostic) notifications.Add(Notification(child.EntryId + ": " + diagnostic.Code + ": " + diagnostic.Message, "error"));
            foreach (var limit in child.Limitations) notifications.Add(Notification(child.EntryId + ": " + limit, "warning"));
            if (child.Diagnostic is not null || child.ReportDirectory is null) continue;
            if (!summary.AnalysisReports.TryGetValue(child.EntryId, out var report))
                throw new InvalidDataException("The frozen child report is unavailable for SARIF projection.");
            var document = JsonNode.Parse(SarifWriter.Json(report))!;
            var run = document["runs"]![0]!.DeepClone();
            foreach (var rule in run["tool"]!["driver"]!["rules"]!.AsArray()) rule!["id"] = child.EntryId + "/" + rule["id"]!.GetValue<string>();
            foreach (var result in run["results"]!.AsArray())
            {
                result!["ruleId"] = child.EntryId + "/" + result["ruleId"]!.GetValue<string>();
                result["partialFingerprints"]!["archsiftChainEntry/v1"] = child.EntryId;
            }
            run["properties"]!["chainEntryId"] = child.EntryId; runs.Add(run);
        }
        foreach (var limit in summary.Limitations) notifications.Add(Notification(limit, "warning"));
        runs.Add(orchestration);
        return Document(runs).ToJsonString(JsonContract.Options);
    }
    public static void Save(ChainSummary summary)
    {
        var root = summary.Snapshot.OutputDirectory;
        WriteNew(Path.Combine(root, "chain-summary.json"), Json(summary));
        WriteNew(Path.Combine(root, "chain-summary.html"), Html(summary));
        WriteNew(Path.Combine(root, "chain-summary.sarif"), Sarif(summary));
    }
    internal static void Replace(ChainSummary summary)
    {
        var root = summary.Snapshot.OutputDirectory;
        foreach (var (name, text) in new[] { ("chain-summary.json", Json(summary)), ("chain-summary.html", Html(summary)), ("chain-summary.sarif", Sarif(summary)) })
        {
            var path = Path.Combine(root, name); PathSafety.EnsureNoLinks(path);
            if (!File.Exists(path)) throw new IOException("Missing current summary during cancellation publication.");
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            WriteNew(temporary, text); PathSafety.EnsureNoLinks(path); File.Move(temporary, path, true);
        }
    }
    public static void SaveDiagnostic(string directory, ChainDiagnostic diagnostic)
    {
        var json = JsonSerializer.Serialize(diagnostic, JsonContract.Options);
        using var doc = JsonDocument.Parse(json); SchemaValidation.Validate(doc.RootElement, "chain-diagnostic");
        WriteNew(Path.Combine(directory, "diagnostic.json"), json);
        WriteNew(Path.Combine(directory, "diagnostic.html"), "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>Chain entry diagnostic</title><h1>Chain entry diagnostic</h1><p>" + WebUtility.HtmlEncode(diagnostic.EntryId) + "</p><p>" + WebUtility.HtmlEncode(diagnostic.Code + ": " + diagnostic.Message) + "</p><p>No analysis findings were produced for this entry.</p></html>");
        var run = DiagnosticRun(ToolIdentity.Version);
        run["invocations"]![0]!["toolExecutionNotifications"]!.AsArray().Add(Notification(diagnostic.Code + ": " + diagnostic.Message, "error"));
        WriteNew(Path.Combine(directory, "diagnostic.sarif"), Document(new JsonArray(run)).ToJsonString(JsonContract.Options));
    }
    internal static void WriteNew(string path, string text)
    {
        PathSafety.EnsureNoLinks(path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)); writer.Write(text);
    }
    private static JsonObject Notification(string message, string level) => new() { ["level"] = level, ["message"] = new JsonObject { ["text"] = message } };
    private static JsonObject DiagnosticRun(string version) => new()
    {
        ["tool"] = new JsonObject { ["driver"] = new JsonObject { ["name"] = "ArchSift", ["version"] = version, ["rules"] = new JsonArray() } },
        ["invocations"] = new JsonArray(new JsonObject { ["executionSuccessful"] = false, ["toolExecutionNotifications"] = new JsonArray() }), ["results"] = new JsonArray()
    };
    private static JsonObject Document(JsonArray runs) => new() { ["$schema"] = "https://docs.oasis-open.org/sarif/sarif/v2.1.0/cos02/schemas/sarif-schema-2.1.0.json", ["version"] = "2.1.0", ["runs"] = runs };
}
