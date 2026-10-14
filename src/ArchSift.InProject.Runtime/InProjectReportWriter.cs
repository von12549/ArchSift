using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public static class InProjectReportWriter
{
    public static void Save(string reportDirectory, InProjectGateResult report, bool replace = false)
    {
        Write(Path.Combine(reportDirectory, "gate-result.html"), Html(report), replace);
        Write(Path.Combine(reportDirectory, "gate-result.sarif"), Sarif(report), replace);
        Write(Path.Combine(reportDirectory, "gate-result.json"),
            JsonSerializer.Serialize(report, JsonContract.Options) + "\n", replace);
        var files = Directory.EnumerateFiles(reportDirectory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != "daily-index.json")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                PathSafety.EnsureNoLinks(path);
                return new { path = Path.GetRelativePath(reportDirectory, path).Replace('\\', '/'),
                    bytes = new FileInfo(path).Length, sha256 = LocalPackage.HashFile(path) };
            }).ToArray();
        Write(Path.Combine(reportDirectory, "daily-index.json"),
            JsonSerializer.Serialize(new { schemaVersion = 1, product = "ArchSift.InProject",
                report.RunId, report.WorktreeId, report.EvidenceGrade,
                report.Freshness, gate = report.Gate, files }, JsonContract.Options) + "\n", replace);
    }

    public static string Html(InProjectGateResult report)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
        var page = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>ArchSift.InProject 门禁</title>");
        page.Append("<style>body{font:16px system-ui;margin:2rem auto;padding:0 1rem;max-width:1100px;color:#172c31}table{border-collapse:collapse;width:100%}td,th{padding:.6rem;border-bottom:1px solid #cddcdb;text-align:left}code,pre{overflow-wrap:anywhere;white-space:pre-wrap}</style>");
        page.Append("<h1>ArchSift.InProject 门禁</h1><p>运行 ").Append(E(report.RunId))
            .Append(" · 门禁 ").Append(E(report.Gate.Decision)).Append(" / ").Append(E(report.Gate.Reason))
            .Append(" · 退出码 ").Append(report.Gate.ExitCode).Append("</p><p>执行 ")
            .Append(E(report.Execution)).Append(" · 关联 ").Append(report.Binding.SourceBound ? "已核验" : "未核验")
            .Append(" · 材料 ").Append(E(report.EvidenceGrade)).Append(" · 新鲜度 ")
            .Append(E(report.Freshness)).Append("</p>");
        if (report.Gate.Decision != "allow")
            page.Append("<p role=\"alert\">本次门禁未放行；请检查规则、覆盖和限制。</p>");
        page.Append("<h2>必检覆盖</h2><p>").Append(report.Coverage.ExecutedRules.Length)
            .Append(" / ").Append(report.Coverage.RequiredRules.Length).Append("；完整 ")
            .Append(report.Coverage.Complete ? "是" : "否").Append("</p><table><tr><th>规则</th><th>类别</th><th>状态</th><th>发现/开始/终结</th><th>匹配</th><th>限制</th></tr>");
        foreach (var rule in report.Rules)
            page.Append("<tr><td>").Append(E(rule.RuleKey)).Append("</td><td>").Append(E(rule.Classification))
                .Append("</td><td>").Append(E(rule.Status)).Append("</td><td>")
                .Append(rule.Discovered).Append(" / ").Append(rule.Started).Append(" / ").Append(rule.Finished)
                .Append("</td><td>").Append(rule.Matched).Append("</td><td>")
                .Append(E(string.Join("; ", rule.Limitations))).Append("</td></tr>");
        page.Append("</table><h2>发现</h2><table><tr><th>规则</th><th>对象</th><th>信息</th><th>证据定位</th></tr>");
        foreach (var finding in report.Findings)
            page.Append("<tr><td>").Append(E(finding.RuleId)).Append("</td><td>")
                .Append(E(finding.Subject)).Append("</td><td>").Append(E(finding.Message))
                .Append("</td><td>").Append(E(finding.Location)).Append("</td></tr>");
        page.Append("</table><h2>限制</h2><pre>").Append(E(string.Join("\n", report.Limitations)))
            .Append("</pre><p>权威结论见同一运行的 gate-result.json。</p></html>");
        return page.ToString();
    }

    public static string Sarif(InProjectGateResult report)
    {
        var rules = new JsonArray(report.Rules.Select(rule => (JsonNode)new JsonObject
        {
            ["id"] = rule.RuleKey,
            ["shortDescription"] = new JsonObject { ["text"] = rule.RuleKey },
            ["properties"] = new JsonObject { ["archsiftStatus"] = rule.Status,
                ["classification"] = rule.Classification }
        }).ToArray());
        var findings = new JsonArray(report.Findings.Select(finding =>
        {
            var key = report.Rules.FirstOrDefault(rule => rule.RuleKey.EndsWith("/" + finding.RuleId,
                StringComparison.Ordinal))?.RuleKey ?? finding.RuleId;
            return (JsonNode)new JsonObject
            {
                ["ruleId"] = key,
                ["level"] = finding.Severity == "info" ? "note" : finding.Severity,
                ["kind"] = "fail",
                ["message"] = new JsonObject { ["text"] = finding.Message + " " + finding.Subject },
                ["partialFingerprints"] = new JsonObject { ["archsiftFindingId/v1"] = finding.Id },
                ["properties"] = new JsonObject { ["subjectKind"] = finding.SubjectKind,
                    ["subject"] = finding.Subject, ["source"] = finding.Source,
                    ["target"] = finding.Target, ["evidenceLocation"] = finding.Location }
            };
        }).ToArray());
        var notifications = new JsonArray(report.Limitations.Select(limit => (JsonNode)new JsonObject
            { ["level"] = "warning", ["message"] = new JsonObject { ["text"] = limit } }).ToArray());
        var run = new JsonObject
        {
            ["tool"] = new JsonObject { ["driver"] = new JsonObject {
                ["name"] = "ArchSift.InProject", ["version"] = "0.1.0-dev", ["rules"] = rules } },
            ["invocations"] = new JsonArray(new JsonObject { ["executionSuccessful"] =
                report.Execution == "completed", ["toolExecutionNotifications"] = notifications }),
            ["results"] = findings,
            ["properties"] = new JsonObject { ["runId"] = report.RunId,
                ["execution"] = report.Execution, ["sourceBound"] = report.Binding.SourceBound,
                ["evidenceGrade"] = report.EvidenceGrade, ["freshness"] = report.Freshness,
                ["gate"] = JsonSerializer.SerializeToNode(report.Gate, JsonContract.Options),
                ["coverage"] = JsonSerializer.SerializeToNode(report.Coverage, JsonContract.Options) }
        };
        return new JsonObject { ["$schema"] =
            "https://docs.oasis-open.org/sarif/sarif/v2.1.0/cos02/schemas/sarif-schema-2.1.0.json",
            ["version"] = "2.1.0", ["runs"] = new JsonArray(run) }.ToJsonString(JsonContract.Options) + "\n";
    }

    private static void Write(string path, string value, bool replace)
    {
        PathSafety.EnsureNoLinks(path);
        using var stream = new FileStream(path, replace ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(value);
    }
}
