using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;

namespace ArchSift.Core;

/// <summary>SARIF 2.1.0 is a projection, not a replacement for ArchSift's authoritative JSON.</summary>
public static class SarifWriter
{
    public static string Json(AnalysisReport report) => Document(Run(report)).ToJsonString(JsonContract.Options);
    public static string Json(ComparisonReport comparison)
    {
        var report = comparison.Target ?? comparison.Baseline;
        var run = report is null ? EmptyRun(comparison.ToolVersion) : Run(report);
        var results = run["results"]!.AsArray();
        // baselineState promises comprehensive comparison; partial/policy-changed runs must not make that claim.
        if (comparison.Status == "completed")
        {
            var states = comparison.Added.ToDictionary(f => f.Id, _ => "new");
            foreach (var finding in comparison.Existing) states[finding.Id] = "unchanged";
            foreach (var result in results)
                if (states.TryGetValue(result!["partialFingerprints"]!["archsiftFindingId/v1"]!.GetValue<string>(), out var state)) result["baselineState"] = state;
            foreach (var finding in comparison.Resolved)
            {
                var result = Result(finding, comparison.Baseline!); result["baselineState"] = "absent"; results.Add(result);
            }
        }
        run["properties"]!["comparisonStatus"] = comparison.Status;
        run["properties"]!["comparisonLimitations"] = JsonSerializer.SerializeToNode(comparison.Limitations);
        run["properties"]!["policyChanges"] = JsonSerializer.SerializeToNode(comparison.PolicyChanges, JsonContract.Options);
        if (comparison.ExecutionErrors.Length > 0 || comparison.Status == "cancelled")
            run["invocations"]![0]!["executionSuccessful"] = false;
        var notifications = run["invocations"]![0]!["toolExecutionNotifications"]!.AsArray();
        foreach (var error in comparison.ExecutionErrors) notifications.Add(Notification(error, "error"));
        foreach (var limit in comparison.Limitations) notifications.Add(Notification(limit, "warning"));
        return Document(run).ToJsonString(JsonContract.Options);
    }

    private static JsonObject Document(JsonObject run) => new()
    {
        ["$schema"] = "https://docs.oasis-open.org/sarif/sarif/v2.1.0/cos02/schemas/sarif-schema-2.1.0.json",
        ["version"] = "2.1.0", ["runs"] = new JsonArray(run)
    };
    private static JsonObject EmptyRun(string version) => new()
    {
        ["tool"] = new JsonObject { ["driver"] = new JsonObject { ["name"] = "ArchSift", ["version"] = version, ["rules"] = new JsonArray() } },
        ["invocations"] = new JsonArray(new JsonObject { ["executionSuccessful"] = false, ["toolExecutionNotifications"] = new JsonArray() }),
        ["results"] = new JsonArray(), ["properties"] = new JsonObject()
    };
    private static JsonObject Run(AnalysisReport report)
    {
        var run = EmptyRun(report.ToolVersion);
        run["tool"]!["driver"]!["rules"] = new JsonArray(report.RuleResults.Select(r => (JsonNode)new JsonObject
        { ["id"] = r.RuleId, ["shortDescription"] = new JsonObject { ["text"] = r.RuleId }, ["properties"] = new JsonObject { ["archsiftStatus"] = r.Status } }).ToArray());
        run["results"] = new JsonArray(report.Findings.Select(f => (JsonNode)Result(f, report)).ToArray());
        run["invocations"]![0]!["executionSuccessful"] = report.ExecutionErrors.Length == 0 && report.Execution is not ("failed" or "cancelled");
        var notifications = run["invocations"]![0]!["toolExecutionNotifications"]!.AsArray();
        foreach (var error in report.ExecutionErrors) notifications.Add(Notification(error, "error"));
        foreach (var limit in report.Limitations.Concat(report.RuleResults.SelectMany(r => r.Limitations)).Distinct()) notifications.Add(Notification(limit, "warning"));
        run["properties"] = new JsonObject
        {
            ["archsiftExecution"] = report.Execution, ["archsiftCompliance"] = report.Compliance,
            ["inputIdentity"] = report.InputIdentity.Sha256,
            ["coverage"] = JsonSerializer.SerializeToNode(report.Coverage, JsonContract.Options),
            ["ruleResults"] = JsonSerializer.SerializeToNode(report.RuleResults, JsonContract.Options)
        };
        return run;
    }
    private static JsonObject Result(Finding finding, AnalysisReport report)
    {
        var status = report.RuleResults.FirstOrDefault(r => r.RuleId == finding.RuleId)?.Status ?? "inconclusive";
        var result = new JsonObject
        {
            ["ruleId"] = finding.RuleId, ["level"] = finding.Severity == "info" ? "note" : finding.Severity,
            ["kind"] = status == "violation" ? "fail" : "review", ["message"] = new JsonObject { ["text"] = finding.Message + " " + finding.Subject },
            ["partialFingerprints"] = new JsonObject { ["archsiftFindingId/v1"] = finding.Id },
            ["properties"] = new JsonObject { ["ruleStatus"] = status, ["subjectKind"] = finding.SubjectKind,
                ["subject"] = finding.Subject, ["source"] = finding.Source, ["target"] = finding.Target, ["exceptionId"] = finding.ExceptionId }
        };
        if (finding.ExceptionId is not null)
            result["suppressions"] = new JsonArray(new JsonObject { ["kind"] = "external", ["status"] = "accepted", ["justification"] = finding.ExceptionReason ?? finding.ExceptionId });
        // Only genuine root-relative file paths. No guessed line or fabricated DLL/source mapping.
        var path = finding.Location ?? (finding.SubjectKind is "project" or "source-file" ? finding.Subject : null);
        if (path is not null && !Path.IsPathRooted(path) && !path.Contains(':') && !path.Replace('\\', '/').Split('/').Any(s => s is ".." or "." or ""))
        {
            var relative = path.Replace('\\', '/');
            if (report.InputIdentity.Files.Any(f => f.Path == relative))
                result["locations"] = new JsonArray(new JsonObject { ["physicalLocation"] = new JsonObject {
                    ["artifactLocation"] = new JsonObject { ["uri"] = string.Join('/', relative.Split('/').Select(Uri.EscapeDataString)) } } });
        }
        return result;
    }
    private static JsonObject Notification(string text, string level) => new() { ["level"] = level, ["message"] = new JsonObject { ["text"] = text } };
}
