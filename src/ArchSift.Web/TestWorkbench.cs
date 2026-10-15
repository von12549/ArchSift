using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.TestGate;
using ArchSift.Testing;

namespace ArchSift.Web;

/// <summary>One explicitly selected daily-test configuration in a loopback Web session.</summary>
public sealed class TestWorkbench(Func<RunConfiguration> context, string rulesDirectory, SemaphoreSlim gate)
{
    private readonly object sync = new();
    private TestGateConfig? selected;
    private string? selectedPath;
    private TestJob? active;

    public void Map(WebApplication app)
    {
        app.Lifetime.ApplicationStopping.Register(Cancel);
        app.MapPost("/api/test-workbench/config", async (HttpContext request) =>
        {
            using var document = await JsonDocument.ParseAsync(request.Request.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("path", out var pathValue) ||
                pathValue.ValueKind != JsonValueKind.String)
                throw new ConfigurationException("Daily-test configuration path is required.");
            var path = pathValue.GetString();
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new ConfigurationException("Daily-test configuration path must be absolute.");
            var full = Path.GetFullPath(path);
            var config = TestGateService.Load(full);
            CheckTarget(config);
            lock (sync)
            {
                if (active?.State == "running") return Results.Conflict(new { error = "A daily test is running." });
                selected = config;
                selectedPath = full;
            }
            return Results.Json(View(), JsonContract.Options);
        });
        app.MapGet("/api/test-workbench", () => Results.Json(View(), JsonContract.Options));
        app.MapPost("/api/test-workbench/copy-rule", async (HttpContext request) =>
        {
            using var document = await JsonDocument.ParseAsync(request.Request.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("index", out var indexValue) ||
                indexValue.ValueKind != JsonValueKind.Number || !indexValue.TryGetInt32(out var index))
                throw new ConfigurationException("Daily-test ruleset index is required.");
            lock (sync)
            {
                if (active?.State == "running") return Results.Conflict(new { error = "A daily test is running." });
                var config = Selected();
                if (index < 0 || index >= config.Rulesets.Length)
                    throw new ConfigurationException("Unknown daily-test ruleset.");
                var source = RulePath(config, config.Rulesets[index]);
                PathSafety.EnsureNoLinks(source);
                var bytes = File.ReadAllBytes(source);
                var loaded = RuleLoader.Load(source);
                var library = new RulesetLibrary(rulesDirectory, config.TargetRoot);
                var existing = library.List().SingleOrDefault(item => item.Available &&
                    string.Equals(item.RulesetId, loaded.Ruleset.Id, StringComparison.OrdinalIgnoreCase));
                if (existing is not null && !string.Equals(existing.Identity?.Sha256,
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                    throw new ConfigurationException("A library ruleset has this ID with different bytes; review it before selection.");
                var card = existing ?? library.Save("test-" + Guid.NewGuid().ToString("N")[..8] + ".json", bytes, false);
                var target = Path.Combine(rulesDirectory, card.FileName);
                var next = config with { Rulesets = config.Rulesets.Select((value, position) =>
                    position == index ? target : value).ToArray() };
                var savedConfig = Path.Combine(rulesDirectory, ".test-workbench-" + Guid.NewGuid().ToString("N") + ".json");
                var manualRun = Path.Combine(HistoryRoot(), "manual-" + Guid.NewGuid().ToString("N"));
                using (var file = new FileStream(savedConfig, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                    writer.Write(JsonSerializer.Serialize(next with { RunDirectory = manualRun,
                        FilterCaseId = null }, JsonContract.Options));
                selected = next;
                selectedPath = savedConfig;
                return Results.Json(new { fileName = card.FileName, entryId = card.EntryId, path = target,
                    configPath = savedConfig }, JsonContract.Options);
            }
        });
        app.MapPost("/api/test-workbench/run", async (HttpContext request) =>
        {
            using var document = await JsonDocument.ParseAsync(request.Request.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("mode", out var modeValue) ||
                modeValue.ValueKind != JsonValueKind.String)
                throw new ConfigurationException("Daily-test mode is required.");
            var mode = modeValue.GetString();
            var caseId = document.RootElement.TryGetProperty("caseId", out var id) ?
                id.ValueKind switch
                {
                    JsonValueKind.String => id.GetString(),
                    JsonValueKind.Null => null,
                    _ => throw new ConfigurationException("Subset case ID must be text.")
                } : null;
            if (mode is not ("full" or "subset") || mode == "full" && !string.IsNullOrEmpty(caseId) ||
                mode == "subset" && string.IsNullOrWhiteSpace(caseId))
                throw new ConfigurationException("Select full mode or one explicit subset case ID.");
            TestJob job;
            TestGateConfig config;
            string configPath;
            if (!await gate.WaitAsync(0)) return Results.Conflict(new { error = "A workbench job is running." });
            try
            {
                lock (sync)
                {
                if (active?.State == "running") { gate.Release(); return Results.Conflict(new { error = "A daily test is running." }); }
                var selectedConfig = Selected();
                CheckTarget(selectedConfig);
                var root = Path.GetFullPath(Path.Combine(context().Output.Directory, "test-runs"));
                PathSafety.EnsureDisjoint(selectedConfig.TargetRoot, root);
                PathSafety.EnsureNoLinks(root);
                Directory.CreateDirectory(root);
                var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..8];
                var runDirectory = Path.Combine(root, runId);
                config = selectedConfig with { RunDirectory = runDirectory,
                    FilterCaseId = mode == "subset" ? caseId : null };
                configPath = Path.Combine(root, runId + ".config.json");
                using (var file = new FileStream(configPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                    writer.Write(JsonSerializer.Serialize(config, JsonContract.Options));
                job = new TestJob(runId, runDirectory, configPath);
                active = job;
                }
            }
            catch { gate.Release(); throw; }
            _ = Task.Run(async () =>
            {
                try
                {
                    job.Result = await TestGateService.RunAsync(config, configPath, job.Cancel.Token);
                    job.State = job.Result.ExitCode == 130 ? "cancelled" : "finished";
                }
                catch (Exception error)
                { job.Error = error.Message; job.State = "failed"; }
                finally { job.Publish(); gate.Release(); job.Cancel.Dispose(); }
            });
            return Results.Json(new { jobId = job.Id, job.RunDirectory }, JsonContract.Options);
        });
        app.MapGet("/api/test-workbench/job", () => Results.Json(JobView(), JsonContract.Options));
        app.MapPost("/api/test-workbench/cancel", () => { Cancel(); return Results.Ok(); });
        app.MapGet("/api/test-workbench/history", (int? offset, int? limit) =>
        {
            var config = Selected();
            if (!Directory.Exists(HistoryRoot()))
                return Results.Json(new TestRunHistoryPage([], 0, offset ?? 0, limit ?? 25), JsonContract.Options);
            string[] activeIds = [];
            lock (sync)
            {
                if (active is { State: "running" })
                {
                    var marker = Path.Combine(active.RunDirectory, "architecture-test-state.json");
                    if (File.Exists(marker))
                    {
                        try
                        {
                            using var document = JsonDocument.Parse(File.ReadAllBytes(marker));
                            activeIds = [document.RootElement.GetProperty("runId").GetString()!];
                        }
                        catch (Exception error) when (error is IOException or JsonException or KeyNotFoundException)
                        { /* A marker may be in the middle of its atomic publication. */ }
                    }
                }
            }
            return Results.Json(TestRunHistory.List(HistoryRoot(), config.TargetRoot, offset ?? 0, limit ?? 25,
                activeIds), JsonContract.Options);
        });
        app.MapGet("/api/test-workbench/report/{runId}/{format}", (string runId, string format) =>
        {
            if (format is not ("json" or "html")) return Results.BadRequest();
            if (!Directory.Exists(HistoryRoot())) return Results.NotFound();
            TestRunHistoryItem? item = null;
            for (var offset = 0; ; offset += 200)
            {
                var page = TestRunHistory.List(HistoryRoot(), Selected().TargetRoot, offset, 200);
                item = page.Items.SingleOrDefault(value => value.RunId == runId);
                if (item is not null || offset + page.Items.Length >= page.Total) break;
            }
            var path = format == "json" ? item?.ReportPath : item?.HtmlPath;
            if (item is null || item.State is "running" or "interrupted" or "unavailable" ||
                path is null || !PathSafety.IsUnder(path, HistoryRoot())) return Results.NotFound();
            PathSafety.EnsureNoLinks(path);
            return Results.File(File.ReadAllBytes(path), format == "html" ? "text/html" : "application/json",
                "architecture-test-run." + format);
        });
    }

    public void Cancel()
    {
        lock (sync) if (active?.State == "running") active.Cancel.Cancel();
    }

    public async Task DrainAsync()
    {
        Cancel();
        Task? task;
        lock (sync) task = active?.Completion;
        if (task is not null) await task.WaitAsync(TimeSpan.FromSeconds(8));
    }

    private object View()
    {
        lock (sync)
        {
            if (selected is null) return new { configured = false };
            var config = Selected();
            CheckTarget(config);
            var baseline = File.ReadAllBytes(config.OriginalBaseline);
            var sha = Convert.ToHexString(SHA256.HashData(baseline)).ToLowerInvariant();
            if (!sha.Equals(config.OriginalBaselineSha256, StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationException("Original-case baseline hash changed.");
            var originals = JsonSerializer.Deserialize<string[]>(baseline) ?? [];
            var rulePaths = config.Rulesets.Select(path => RulePath(config, path)).ToArray();
            var allCases = ArchitectureTestAdapter.LoadCases(rulePaths).Cases;
            var rules = config.Rulesets.Select((path, index) => new
            {
                index, path = rulePaths[index], editable = !PathSafety.IsUnder(rulePaths[index], config.TargetRoot),
                cases = allCases.Where(item => item.Ruleset.Identity.Path.Equals(rulePaths[index], PathSafety.Comparison))
                    .Select(item => item.CaseId).ToArray()
            }).ToArray();
            var notIncluded = config.TestExecutableName == "IFX.Architecture.Tests"
                ? TestRunReportWriter.PreviewNotIncluded(allCases.Select(item => item.Rule.Id)) : [];
            return new { configured = true, configPath = selectedPath, targetRoot = config.TargetRoot,
                testProject = config.TestProject, originalCaseIds = originals, rules,
                notIncluded, historyRoot = HistoryRoot(), job = JobView() };
        }
    }

    private object? JobView()
    {
        lock (sync) return active is null ? null : new { active.Id,
            State = active.Finalized ? active.State : "running", Error = active.Finalized ? active.Error : null,
            exitCode = active.Finalized ? active.Result?.ExitCode : null,
            gate = active.Finalized ? active.Result?.Gate : null,
            reasons = active.Finalized ? active.Result?.Reasons : null,
            active.RunDirectory, active.ConfigPath };
    }

    private TestGateConfig Selected() => selected ?? throw new ConfigurationException("Select a daily-test configuration first.");
    private string HistoryRoot() => Path.GetFullPath(Path.Combine(context().Output.Directory, "test-runs"));
    private void CheckTarget(TestGateConfig config)
    {
        if (!Path.GetFullPath(config.TargetRoot).Equals(Path.GetFullPath(context().Target.Root), PathSafety.Comparison))
            throw new ConfigurationException("Daily-test target must match this Web session target.");
        PathSafety.EnsureDisjoint(config.TargetRoot, HistoryRoot());
    }
    private static string RulePath(TestGateConfig config, string path) => Path.IsPathFullyQualified(path)
        ? Path.GetFullPath(path) : PathSafety.Under(config.TargetRoot, path);

    private sealed class TestJob(string id, string runDirectory, string configPath)
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int finalized;
        public string Id { get; } = id;
        public string RunDirectory { get; } = runDirectory;
        public string ConfigPath { get; } = configPath;
        public CancellationTokenSource Cancel { get; } = new();
        public string State { get; set; } = "running";
        public string? Error { get; set; }
        public TestGateResult? Result { get; set; }
        public bool Finalized => Volatile.Read(ref finalized) != 0;
        public Task Completion => completion.Task;
        public void Publish() { Volatile.Write(ref finalized, 1); completion.TrySetResult(); }
    }
}
