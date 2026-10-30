using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Testing;

namespace ArchSift.TestGate;

/// <summary>Writes the independent daily-test report. JSON is published last and is authoritative.</summary>
public static class TestRunReportWriter
{
    private static readonly UTF8Encoding Utf8 = new(false);

    public static Dictionary<string, object?>[] PreviewNotIncluded(IEnumerable<string> includedRuleIds) =>
        LoadNotIncluded(includedRuleIds.ToHashSet(StringComparer.Ordinal));

    public static (string Json, string Html) Save(TestGateConfig config, TestGateResult result,
        RuleBundle bundle, string[] originalIds, string runId,
        DateTimeOffset startedUtc, DateTimeOffset finishedUtc)
    {
        var directory = Path.GetFullPath(result.RunDirectory);
        PathSafety.EnsureDisjoint(Path.GetFullPath(config.TargetRoot), directory);
        PathSafety.EnsureNoLinks(directory);
        if (!Directory.Exists(directory)) throw new IOException("Test run directory is missing.");
        var report = Build(config, result, bundle, originalIds, runId, startedUtc, finishedUtc);
        var json = JsonSerializer.Serialize(report, JsonContract.Options) + "\n";
        var html = Html(report);
        var jsonPath = Path.Combine(directory, "architecture-test-run.json");
        var htmlPath = Path.Combine(directory, "architecture-test-run.html");
        var nonce = Guid.NewGuid().ToString("N");
        var jsonTemp = Path.Combine(directory, ".architecture-test-run-" + nonce + ".json.tmp");
        var htmlTemp = Path.Combine(directory, ".architecture-test-run-" + nonce + ".html.tmp");
        foreach (var path in new[] { jsonPath, htmlPath, jsonTemp, htmlTemp }) PathSafety.EnsureNoLinks(path);
        try
        {
            WriteNew(htmlTemp, html);
            WriteNew(jsonTemp, json);
            File.Move(htmlTemp, htmlPath);
            File.Move(jsonTemp, jsonPath);
            return (jsonPath, htmlPath);
        }
        finally
        {
            if (File.Exists(jsonTemp)) File.Delete(jsonTemp);
            if (File.Exists(htmlTemp)) File.Delete(htmlTemp);
        }
    }

    public static Dictionary<string, object?> Build(TestGateConfig config, TestGateResult result,
        RuleBundle bundle, string[] originalIds, string runId,
        DateTimeOffset startedUtc, DateTimeOffset finishedUtc)
    {
        var native = result.NativeRun;
        var tests = native?.Tests ?? [];
        var originals = originalIds.ToHashSet(StringComparer.Ordinal);
        string OriginalKey(MtpTest test) => config.SchemaVersion == 2 && test.Identity is not null
            ? CaseIdentities.Key(test.Identity.Identity) : test.Id;
        var cases = bundle.Documents.SelectMany(document => document.Ruleset.Rules
            .Where(rule => rule.Enabled).Select(rule => (Rule: rule, Document: document))).ToArray();
        var expectedJson = cases.Select(item => "archsift-json:" + item.Rule.Id).Order(StringComparer.Ordinal).ToArray();
        var checks = new List<Dictionary<string, object?>>();
        foreach (var test in tests.Where(test => test.JsonCaseId is null))
        {
            checks.Add(Obj(("id", "ifx-specialized:" + (config.SchemaVersion == 2 ? ContentHash.Text(OriginalKey(test)) : test.Id)), ("source", "ifx-specialized"),
                ("responsibleChecker", "IFX.Architecture.Tests"), ("inDailyScope", true),
                ("runnerCaseId", test.Id), ("xunitCaseId", test.Identity?.XunitCaseId),
                ("mtpDiscoveryUid", test.DiscoveryUid), ("scope", test.ClassName + "." + test.MethodName),
                ("discovered", true), ("executed", true), ("result", TestStatus(test.Result)),
                ("elapsedMilliseconds", test.ElapsedMilliseconds), ("findingIds", Array.Empty<string>()),
                ("messages", test.Messages), ("limitations", Array.Empty<string>())));
        }
        var executedOriginal = tests.Where(test => test.JsonCaseId is null)
            .Select(OriginalKey).ToHashSet(StringComparer.Ordinal);
        foreach (var id in originals.Except(executedOriginal).Order(StringComparer.Ordinal))
            checks.Add(Obj(("id", "ifx-specialized:" + id), ("source", "ifx-specialized"),
                ("responsibleChecker", "IFX.Architecture.Tests"), ("inDailyScope", true),
                ("runnerCaseId", id), ("discovered", false), ("executed", false),
                ("result", "not-run"), ("findingIds", Array.Empty<string>()),
                ("messages", Array.Empty<string>()),
                ("limitations", new[] { "Required original result ID was not observed." })));

        foreach (var (rule, _) in cases.OrderBy(item => item.Rule.Id, StringComparer.Ordinal))
        {
            var caseId = "archsift-json:" + rule.Id;
            var test = tests.SingleOrDefault(item => item.JsonCaseId == caseId);
            var evaluated = test?.Evaluation?.Results.SingleOrDefault();
            checks.Add(Obj(("id", caseId), ("source", "json-type-dependency"),
                ("responsibleChecker", "ArchSift.Testing/ArchUnitNET"), ("inDailyScope", true),
                ("runnerCaseId", test?.Id), ("xunitCaseId", test?.Identity?.XunitCaseId),
                ("mtpDiscoveryUid", test?.DiscoveryUid), ("ruleId", rule.Id),
                ("scope", rule.Scope.Kind + ":" + rule.Scope.Match + ":" + rule.Scope.Value),
                ("reason", rule.Reason), ("discovered", test is not null),
                ("executed", test is not null), ("result", test is null ? "not-run" : TestStatus(test.Result)),
                ("rawRuleStatus", evaluated?.Status), ("matched", evaluated?.Matched),
                ("elapsedMilliseconds", test?.ElapsedMilliseconds),
                ("findingIds", evaluated?.FindingIds ?? []), ("messages", test?.Messages ?? []),
                ("limitations", evaluated?.Limitations ?? (test is null ? ["JSON case was not executed."] :
                    ["Structured JSON evaluation was not emitted."]))));
        }
        var findings = tests.Where(test => test.JsonCaseId is not null && test.Evaluation is not null)
            .SelectMany(test => test.Evaluation!.Findings.Select(finding => Obj(
                ("id", finding.Id), ("checkId", "archsift-json:" + finding.RuleId),
                ("message", finding.Message),
                ("severity", finding.Severity is "info" or "warning" or "error" ? finding.Severity : null),
                ("sourceType", NonEmpty(finding.Source)), ("targetType", NonEmpty(finding.Target)),
                ("location", NonEmpty(finding.Location)), ("exceptionId", NonEmpty(finding.ExceptionId)),
                ("exceptionReason", NonEmpty(finding.ExceptionReason)))))
            .DistinctBy(item => item["id"]).OrderBy(item => (string)item["id"]!, StringComparer.Ordinal).ToArray();
        var includedIds = cases.Select(item => item.Rule.Id).ToHashSet(StringComparer.Ordinal);
        var notIncluded = config.TestExecutableName == "IFX.Architecture.Tests"
            ? LoadNotIncluded(includedIds) : [];
        var executionComplete = result.ExitCode == 0 && result.Gate == "allow" && result.Coverage?.Complete == true &&
            native is { DiscoveryFinished: true, AssemblyFinished: true } &&
            result.Test.Status == "passed" && !result.RelevantInputChanged;
        var (commit, dirty) = GitContext(config.TargetRoot, config);
        var report = Obj(
            ("schemaVersion", 1), ("kind", "architecture-test-run"),
            ("run", Obj(("id", runId),
                ("startedUtc", startedUtc.ToString("O")), ("finishedUtc", finishedUtc.ToString("O")),
                ("mode", config.FilterCaseId is null ? "full" : "subset"),
                ("reportDirectory", Path.GetFullPath(result.RunDirectory)))),
            ("target", Obj(("worktreeRoot", Path.GetFullPath(config.TargetRoot)),
                ("gitCommit", commit), ("dirty", dirty), ("entry", "archsift test-gate"),
                ("testProjects", new[] { config.TestProject }), ("sdk", ReadSdk(config)),
                ("targetFramework", config.TargetFramework), ("configuration", config.Configuration))),
            ("inputs", Obj(("ifxBaselineSha256", config.SchemaVersion == 1 ? config.OriginalBaselineSha256 : null),
                ("restoreMode", config.RestoreMode),
                ("sdkPolicy", config.MinimumSdkVersion is null ? Obj(("mode", "exact-baseline")) :
                    Obj(("mode", "compatible-range"), ("minimumInclusive", config.MinimumSdkVersion),
                        ("maximumExclusive", config.MaximumSdkVersionExclusive))),
                ("caseManifest", config.SchemaVersion == 2 ? Obj(("schemaVersion", 1),
                    ("path", config.RequiredCases), ("sha256", config.RequiredCasesSha256),
                    ("identityMode", "logical-v1")) : null),
                ("toolRoot", config.ToolRoot), ("toolIdentity", config.ToolIdentity),
                ("policySha256", config.PolicySha256), ("caseMappingSha256", config.CaseMappingSha256),
                ("rulesets", bundle.Documents.Select(document => Obj(("id", document.Identity.Id),
                    ("path", document.Identity.Path), ("sha256", document.Identity.Sha256))).ToArray()))),
            ("engines", Obj(("runner", config.RunnerFormat), ("dotnetPath", config.DotnetPath),
                ("testAdapter", "ArchSift.Testing " + typeof(ArchitectureTestAdapter).Assembly.GetName().Version),
                ("archUnitNet", ArchUnitVersion()))),
            ("testScope", Obj(("requiredOriginalProject", config.TestProject),
                ("expectedOriginalCaseIds", config.SchemaVersion == 1 ? originalIds.Order(StringComparer.Ordinal).ToArray() : []),
                ("expectedLogicalCaseKeys", config.SchemaVersion == 2 ? originalIds.Order(StringComparer.Ordinal).ToArray() : []),
                ("enabledJsonRuleIds", expectedJson), ("discoveredCaseIds", native?.DiscoveredIds ?? []),
                ("startedCaseIds", native?.StartedIds ?? []), ("completedCaseIds", native?.CompletedIds ?? []),
                ("skippedCaseIds", tests.Where(test => test.Result == "skipped").Select(test => test.Id).ToArray()),
                ("missingCaseIds", (result.Coverage?.MissingOriginalIds ?? [])
                    .Concat(result.Coverage?.MissingJsonCaseIds ?? []).Distinct(StringComparer.Ordinal).ToArray()),
                ("duplicateCaseIds", result.Coverage?.DuplicateIds ?? []),
                ("coverageComplete", result.Coverage?.Complete ?? false))),
            ("process", Obj(("snapshotUpdatesDisabled", result.SnapshotUpdatesDisabled),
                ("audit", result.Audit),
                ("restore", Stage(result.Restore)), ("build", Stage(result.Build)), ("test", Stage(result.Test)),
                ("sourceChangedDuringRun", result.RelevantInputChanged),
                ("authoritativeOutputSaved", true))),
            ("checks", checks.OrderBy(item => (string)item["id"]!, StringComparer.Ordinal).ToArray()),
            ("findings", findings), ("notIncluded", notIncluded),
            ("responsibilityProjection", config.TestExecutableName == "IFX.Architecture.Tests"
                ? RuleResponsibilityProjection.Create(config, includedIds, tests, executionComplete) : null),
            ("policy", PolicyView(config, tests, executionComplete)),
            ("outcome", Obj(("runGate", result.Gate), ("exitCode", result.ExitCode),
                ("reasons", result.Reasons.Distinct(StringComparer.Ordinal).ToArray()))));
        return report;
    }

    public static object PolicyView(TestGateConfig config, MtpTest[]? tests = null, bool executionComplete = false)
    {
        if (config.PolicyFile is null) return new { status = "unknown", coverageComplete = false };
        (NativePolicyDocument Document, string[] Paths, string Sha256) policy;
        try { policy = NativePolicy.Load(config.TargetRoot, config.PolicyFile); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException
            or ConfigurationException or InvalidDataException)
        { return new { status = "unknown", coverageComplete = false }; }
        if (policy.Sha256 != config.PolicySha256)
            return new { status = "unknown", coverageComplete = false };
        var actualKeys = (tests ?? []).Where(item => item.Identity is not null)
            .GroupBy(item => CaseIdentities.Key(item.Identity!.Identity), StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        return new
        {
            status = policy.Document.Sources.Length == 0 ? "unmapped" : "mapped", version = policy.Document.Version, sha256 = policy.Sha256,
            // Responsibility metadata never manufactures an execution result or a pass.
            coverageComplete = executionComplete && policy.Document.Sources.Length > 0 && policy.Document.Sources.All(item =>
                item.Responsibility != "deferred" && item.CaseKeys.Length > 0 &&
                item.CaseKeys.All(key => actualKeys.TryGetValue(key, out var test) && test.Result == "passed")),
            sources = policy.Document.Sources.Select(item => new
            {
                item.SourceId, item.SourceSha256, item.Responsibility, editable = item.Responsibility == "json",
                caseKeys = item.CaseKeys, executionIds = item.CaseKeys.Where(actualKeys.ContainsKey)
                    .Select(key => actualKeys[key].Id).ToArray()
            }).ToArray()
        };
    }

    private static Dictionary<string, object?>[] LoadNotIncluded(HashSet<string> includedIds)
    {
        using var stream = typeof(TestRunReportWriter).Assembly.GetManifestResourceStream(
            "ArchSift.TestGate.PolicyInventory.json") ?? throw new InvalidDataException("Policy inventory resource is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => !includedIds.Contains(item.GetProperty("id").GetString()!))
            .Select(item => Obj(("id", item.GetProperty("id").GetString()!),
                ("source", item.GetProperty("category").GetString() == "A" ? "legacy-archsift" : "candidate-json-test"),
                ("responsibleChecker", item.GetProperty("responsibleChecker").GetString()!),
                ("reason", "LT00 inventory: " + item.GetProperty("dailySelection").GetString() +
                    "; relationship=" + item.GetProperty("relation").GetString())))
            .OrderBy(item => (string)item["id"]!, StringComparer.Ordinal).ToArray();
    }

    private static Dictionary<string, object?> Stage(GateStage stage) => Obj(("status", stage.Status),
        ("exitCode", stage.ExitCode), ("logPath", NonEmpty(stage.LogPath)));

    private static string TestStatus(string native) => native switch
    {
        "passed" => "passed", "failed" => "failed", "skipped" => "skipped", _ => "not-run"
    };

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static Dictionary<string, object?> Obj(params (string Key, object? Value)[] fields) =>
        fields.Where(field => field.Value is not null).ToDictionary(field => field.Key,
            field => field.Value, StringComparer.Ordinal);

    private static (string Commit, bool Dirty) GitContext(string root, TestGateConfig config)
    {
        var commit = Command("git", ["-C", root, "rev-parse", "HEAD"], root, config);
        var status = Command("git", ["-C", root, "status", "--porcelain", "--untracked-files=normal"], root, config);
        return (string.IsNullOrWhiteSpace(commit) ? "unknown" : commit.Trim(),
            status is null || !string.IsNullOrWhiteSpace(status));
    }

    private static string ReadSdk(TestGateConfig config)
    {
        if (config.SchemaVersion == 2)
        {
            var path = Path.Combine(config.RunDirectory, "sdk.log");
            return File.Exists(path) && File.ReadAllText(path).Trim() is { Length: > 0 } recorded ? recorded : "unavailable";
        }
        return Command("dotnet", ["--version"], config.TargetRoot, config)?.Trim() is { Length: > 0 } version
            ? version : "unavailable";
    }

    private static string ArchUnitVersion()
    {
        var reference = typeof(TypeDependencyTestEngine).Assembly.GetReferencedAssemblies()
            .FirstOrDefault(item => item.Name == "ArchUnitNET");
        return "ArchUnitNET " + (reference?.Version?.ToString() ?? "version unavailable");
    }

    private static string? Command(string executable, string[] arguments, string root, TestGateConfig config)
    {
        try
        {
            var start = new ProcessStartInfo(executable)
            {
                WorkingDirectory = root, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            start.Environment["IFX_UPDATE_SNAPSHOTS"] = "0";
            start.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true";
            if (config.DotnetCliHome is not null)
            {
                start.Environment["DOTNET_CLI_HOME"] = config.DotnetCliHome;
                start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
            }
            using var process = Process.Start(start);
            if (process is null) return null;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                return null;
            }
            _ = stderr.GetAwaiter().GetResult();
            return process.ExitCode == 0 ? stdout.GetAwaiter().GetResult() : null;
        }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { return null; }
    }

    private static string Html(Dictionary<string, object?> report)
    {
        var run = (Dictionary<string, object?>)report["run"]!;
        var outcome = (Dictionary<string, object?>)report["outcome"]!;
        var checks = (Dictionary<string, object?>[])report["checks"]!;
        var findings = (Dictionary<string, object?>[])report["findings"]!;
        var notIncluded = (Dictionary<string, object?>[])report["notIncluded"]!;
        var text = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>ArchSift 架构测试运行</title>");
        text.Append("<style>body{font:15px/1.5 system-ui,sans-serif;max-width:1200px;margin:2rem auto;padding:0 1rem;color:#17202a}table{border-collapse:collapse;width:100%;margin:1rem 0}th,td{border:1px solid #cad2d9;padding:.4rem;text-align:left;vertical-align:top}th{background:#f1f4f6}code{overflow-wrap:anywhere}details{margin:1rem 0}</style>");
        text.Append("<h1>架构测试运行</h1><p>运行 ID：<code>").Append(E(run["id"]))
            .Append("</code><br>时间：").Append(E(run["finishedUtc"]))
            .Append("<br>模式：").Append(E(run["mode"]))
            .Append("<br>本次门禁：<strong>").Append(E(outcome["runGate"]))
            .Append(" / ").Append(E(outcome["exitCode"])).Append("</strong></p>");
        text.Append("<p>本次通过只覆盖实际执行的 IFX 专用测试与已启用 JSON 测试；未纳入项及旧报告不计入通过数。</p>");
        text.Append("<details><summary>工具、政策来源与 Audit 范围</summary><pre>")
            .Append(E(JsonSerializer.Serialize(new { inputs = report["inputs"], policy = report["policy"], process = report["process"] }, JsonContract.Options)))
            .Append("</pre></details>");
        text.Append("<h2>本次检查</h2><table><tr><th>来源</th><th>检查</th><th>结果</th><th>原始规则状态</th><th>匹配数</th><th>消息</th></tr>");
        foreach (var check in checks)
            text.Append("<tr><td>").Append(E(check.GetValueOrDefault("source")))
                .Append("</td><td><code>").Append(E(check.GetValueOrDefault("id")))
                .Append("</code></td><td>").Append(E(check.GetValueOrDefault("result")))
                .Append("</td><td>").Append(E(check.GetValueOrDefault("rawRuleStatus")))
                .Append("</td><td>").Append(E(check.GetValueOrDefault("matched")))
                .Append("</td><td>").Append(E(string.Join("; ", (string[])check["messages"]!)))
                .Append("</td></tr>");
        text.Append("</table><h2>实际 finding</h2><table><tr><th>ID</th><th>源 → 目标</th><th>说明</th></tr>");
        foreach (var finding in findings)
            text.Append("<tr><td><code>").Append(E(finding["id"]))
                .Append("</code></td><td>").Append(E(finding.GetValueOrDefault("sourceType")))
                .Append(" → ").Append(E(finding.GetValueOrDefault("targetType")))
                .Append("</td><td>").Append(E(finding["message"]))
                .Append("</td></tr>");
        text.Append("</table>");
        if (report.TryGetValue("responsibilityProjection", out var projection) && projection is RuleResponsibilityView view)
        {
            text.Append("<details><summary>当前政策／执行责任／本次执行（未作为JSON执行）</summary><table><tr><th>ID</th><th>政策状态</th><th>执行责任</th><th>本次执行</th></tr>");
            foreach (var item in view.Items)
                text.Append("<tr><td>").Append(E(item.Id)).Append("</td><td>").Append(E(item.PolicyState))
                    .Append("</td><td>").Append(E(item.Responsibility)).Append("</td><td>").Append(E(item.ExecutionResult)).Append("</td></tr>");
            text.Append("</table></details>");
        }
        text.Append("<details><summary>未作为JSON执行；原LT00历史说明（").Append(notIncluded.Length)
            .Append(" 项）</summary><table><tr><th>来源</th><th>ID</th><th>原因</th></tr>");
        foreach (var item in notIncluded)
            text.Append("<tr><td>").Append(E(item["source"]))
                .Append("</td><td><code>").Append(E(item["id"]))
                .Append("</code></td><td>").Append(E(item["reason"]))
                .Append("</td></tr>");
        return text.Append("</table></details></html>").ToString();
    }

    private static string E(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? "");

    private static void WriteNew(string path, string value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, Utf8);
        writer.Write(value);
    }
}
