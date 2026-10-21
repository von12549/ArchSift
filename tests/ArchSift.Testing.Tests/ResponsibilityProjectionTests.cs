using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.TestGate;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class ResponsibilityProjectionTests
{
    private static readonly string[] FoundationIds = ["ifx-global-net10", "ifx-global-resolved-references", "ifx-global-solution-membership"];
    private static string Root => Environment.GetEnvironmentVariable("ARCHSIFT_CL02_EVIDENCE") ?? Path.GetTempPath();

    [Fact]
    public void ApprovedResponsibilityIsSeparateFromJsonSelectionAndCurrentExecution()
    {
        var (config, tests) = Fixture();
        var ids = IncludedIds();
        var old = TestRunReportWriter.PreviewNotIncluded(ids);
        var view = RuleResponsibilityProjection.Create(config, ids, tests, true);
        Assert.Equal(old.Select(item => item["id"]), view.Items.Select(item => (object)item.Id));
        Assert.All(view.Items.Where(item => FoundationIds.Contains(item.Id)), item =>
        { Assert.Equal("adopted-active", item.PolicyState); Assert.Equal("ifx-csharp", item.Responsibility); Assert.Equal("passed", item.ExecutionResult); });
        var pending = view.Items.Where(item => item.PolicyState == "adopted-pending-activation").ToArray();
        Assert.Equal(4, pending.Length);
        Assert.All(pending, item => { Assert.Equal("not-run", item.ExecutionResult); Assert.Empty(item.CaseKeys); Assert.Single(item.DecisionReferences); });
        Assert.All(view.Items.Where(item => item.Category == "A" && !FoundationIds.Contains(item.Id)), item =>
        { Assert.Equal("unknown", item.PolicyState); Assert.Equal("unknown", item.Responsibility); Assert.Equal("not-run", item.ExecutionResult); });
        // The synthetic run receives the frozen approved selection, never learns a new selection.
        if (ids.Length > 0)
        {
            Assert.Equal(568, view.Items.Length);
            var remaining = view.Items.Where(item => item.Responsibility != "ifx-csharp").ToArray();
            Assert.Equal(565, remaining.Length);
            Assert.Equal(179, remaining.Count(item => item.Category == "A"));
            Assert.Equal(386, remaining.Count(item => item.Category == "B"));
            Assert.Equal(561, remaining.Count(item => item.PolicyState != "adopted-pending-activation"));
        }
        Assert.Contains(old, item => (string)item["id"]! == FoundationIds[0] && ((string)item["reason"]!).Contains("unported-declaration-check"));
    }

    [Fact]
    public void MissingSkippedDuplicateUnknownAndIncompleteNeverAcquirePass()
    {
        var (config, tests) = Fixture();
        string Result(MtpTest[] actual, bool complete = true, TestGateConfig? candidate = null) =>
            RuleResponsibilityProjection.Create(candidate ?? config, [], actual, complete).Items.Single(item => item.Id == FoundationIds[0]).ExecutionResult;
        Assert.Equal("not-run", Result([]));
        Assert.Equal("skipped", Result([tests[0] with { Result = "skipped" }]));
        Assert.Equal("failed", Result([tests[0] with { Result = "failed" }]));
        Assert.Equal("unavailable", Result([tests[0], tests[0]]));
        Assert.Equal("unavailable", Result(tests, false));
        Assert.Equal("unavailable", Result([tests[0] with { Result = "unknown" }]));
        Assert.Equal("unavailable", Result([tests[0] with { Identity = tests[0].Identity! with { ExecutionId = "wrong" } }]));
        var unknown = RuleResponsibilityProjection.Create(config with { PolicySha256 = new('0', 64) }, [], tests, true);
        Assert.All(unknown.Items.Where(item => FoundationIds.Contains(item.Id)), item =>
        { Assert.Equal("unknown", item.Responsibility); Assert.NotEqual("passed", item.ExecutionResult); });
        Assert.DoesNotContain(RuleResponsibilityProjection.Create(config with { PolicyFile = "missing.json" }, [], tests, true).Items,
            item => item.ExecutionResult == "passed");
        var file = Path.Combine(config.TargetRoot, "policy.json");
        var policy = JsonSerializer.Deserialize<NativePolicyDocument>(File.ReadAllBytes(file), JsonContract.Options)!;
        File.WriteAllText(file, JsonSerializer.Serialize(policy with { Sources = [policy.Sources[0] with { CaseKeys = [] }] }, JsonContract.Options));
        Assert.Equal("unknown", RuleResponsibilityProjection.Create(config with { PolicySha256 = ContentHash.Bytes(File.ReadAllBytes(file)) }, [], tests, true)
            .Items.Single(item => item.Id == FoundationIds[0]).Responsibility);
    }

    [Fact]
    public void ReportHistoryRetainsOldBytesAndRejectsUnknownProjectionAndCancelledPass()
    {
        var (config, tests) = Fixture();
        var stage = new GateStage("passed", 0, null);
        var native = new MtpRun(true, true, tests.Length, tests, tests.Select(item => item.Id).ToArray(), tests.Select(item => item.Id).ToArray(), [], []);
        var result = new TestGateResult(0, "allow", [], stage, stage, stage, native, new(0, true, [], [], [], []), config.RunDirectory, true, false);
        var bundle = RuleLoader.Compose([RuleLoader.Load(Path.Combine(config.TargetRoot, "rules.json"))]);
        var runId = Path.GetFileName(config.RunDirectory);
        TestRunHistory.Begin(config.RunDirectory, runId, config.TargetRoot, DateTimeOffset.UtcNow);
        var files = TestRunReportWriter.Save(config, result, bundle, [], runId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        result = result with { ReportPath = files.Json, HtmlPath = files.Html };
        TestRunHistory.Finish(config.RunDirectory, runId, result);
        var originalBytes = File.ReadAllBytes(files.Json);
        Assert.Equal("allow", TestRunHistory.List(Path.GetDirectoryName(config.RunDirectory)!).Items.Single(item => item.RunId == runId).Gate);
        Assert.Equal(originalBytes, File.ReadAllBytes(files.Json));
        var report = JsonNode.Parse(originalBytes)!;
        var projection = report["responsibilityProjection"]!.DeepClone();
        report.AsObject().Remove("responsibilityProjection");
        File.WriteAllText(files.Json, report.ToJsonString());
        var legacyBytes = File.ReadAllBytes(files.Json);
        Assert.Equal("allow", TestRunHistory.List(Path.GetDirectoryName(config.RunDirectory)!).Items.Single(item => item.RunId == runId).Gate);
        Assert.Equal(legacyBytes, File.ReadAllBytes(files.Json));
        File.WriteAllBytes(Path.Combine(Root, "synthetic-old-report.json"), legacyBytes);
        report["responsibilityProjection"] = projection;
        report["responsibilityProjection"]!["schemaVersion"] = 99;
        File.WriteAllText(files.Json, report.ToJsonString());
        var unknown = TestRunHistory.List(Path.GetDirectoryName(config.RunDirectory)!).Items.Single(item => item.RunId == runId);
        Assert.Equal("unavailable", unknown.State); Assert.Null(unknown.Gate);
        report["responsibilityProjection"]!["schemaVersion"] = 1;
        report["responsibilityProjection"]!["items"]![0]!["executionResult"] = "future-status";
        File.WriteAllText(files.Json, report.ToJsonString());
        Assert.Equal("unavailable", TestRunHistory.List(Path.GetDirectoryName(config.RunDirectory)!).Items.Single(item => item.RunId == runId).State);
        var cancelled = TestRunReportWriter.Build(config, result with { ExitCode = 130, Gate = "block", Test = new("cancelled", 130, null), Reasons = ["Cancelled."] },
            bundle, [], "synthetic-cancelled", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Assert.DoesNotContain(((RuleResponsibilityView)cancelled["responsibilityProjection"]!).Items, item => item.ExecutionResult == "passed");
        Assert.False(JsonSerializer.SerializeToElement(cancelled["policy"], JsonContract.Options).GetProperty("coverageComplete").GetBoolean());
        File.WriteAllBytes(Path.Combine(Root, "synthetic-new-report.json"), originalBytes);
        File.WriteAllText(Path.Combine(Root, "synthetic-cancelled-report.json"), JsonSerializer.Serialize(cancelled, JsonContract.Options));
        Assert.Contains("未作为JSON执行", File.ReadAllText(files.Html));
    }

    private static string[] IncludedIds()
    {
        var path = Environment.GetEnvironmentVariable("ARCHSIFT_CL00_SELECTION");
        if (path is null) return [];
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("enabledJsonIds").EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static (TestGateConfig Config, MtpTest[] Tests) Fixture()
    {
        var root = Path.Combine(Root, "synthetic-source-" + Guid.NewGuid().ToString("N"));
        var run = Path.Combine(Root, "synthetic-runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Directory.CreateDirectory(run);
        var rules = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "rules.json"));
        File.WriteAllBytes(Path.Combine(root, "rules.json"), rules);
        var tests = FoundationIds.Select((_, index) =>
        {
            var identity = new CaseIdentity("Synthetic.Tests", "Synthetic.Foundation", "Case" + index, [], []);
            var id = "synthetic-execution-" + index;
            return new MtpTest(id, id, identity.Type, identity.Method, "passed", null, null, []) { Identity = new(id, "xunit-" + index, identity) };
        }).ToArray();
        var policy = new NativePolicyDocument(1, "synthetic-cl02", [new("rules.json", ContentHash.Bytes(rules))],
            FoundationIds.Select((id, index) => new PolicySource(id, new('a', 64), "ifx-csharp", [CaseIdentities.Key(tests[index].Identity!.Identity)])).ToArray());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(policy, JsonContract.Options);
        File.WriteAllBytes(Path.Combine(root, "policy.json"), bytes);
        var manifestPath = Path.Combine(root, "synthetic-required-cases.json");
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, synthetic = true,
            caseKeys = tests.Select(test => CaseIdentities.Key(test.Identity!.Identity)).ToArray() }, JsonContract.Options);
        File.WriteAllBytes(manifestPath, manifest);
        return (FormalAdoptionTests.EmptyConfig() with { SchemaVersion = 2, TargetRoot = root, RunDirectory = run,
            RequiredCases = manifestPath, RequiredCasesSha256 = ContentHash.Bytes(manifest),
            TestExecutableName = "IFX.Architecture.Tests", PolicyFile = "policy.json", PolicySha256 = ContentHash.Bytes(bytes) }, tests);
    }
}
