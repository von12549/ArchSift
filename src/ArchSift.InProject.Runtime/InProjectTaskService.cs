using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public static class InProjectTaskService
{
    private static readonly HashSet<string> Terminal = new(StringComparer.Ordinal)
        { "completed", "failed", "cancelled" };

    public static async Task<InProjectTaskOutcome> GateAsync(string targetRoot,
        string evidenceGrade = "daily", CancellationToken token = default)
    {
        if (evidenceGrade is not ("daily" or "full"))
            throw new ConfigurationException("Unsupported InProject evidence grade.");
        var root = WorktreeLocator.Locate(targetRoot).Root;
        var paths = Paths(root);
        Directory.CreateDirectory(paths.Tasks);
        FileStream lease;
        try { lease = new FileStream(paths.Lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException)
        {
            var active = ReadFile(paths.Current) ?? throw new IOException("InProject task lease is busy without a current task.");
            if (Terminal.Contains(active.Phase))
                throw new IOException("InProject task lease is unavailable after the previous task finished.");
            string? requested = null;
            try { requested = RequestIdentity(InProjectConfiguration.Load(root), InputCapture.Capture(root).Sha256,
                evidenceGrade); }
            catch (Exception error) when (error is ConfigurationException or IOException or InvalidDataException) { }
            var joined = active.RequestSha256 is null || requested == active.RequestSha256;
            return new(active, null, joined, !joined);
        }
        using (lease)
        {
            RecoverLostTask(paths, root);
            var runId = Guid.NewGuid().ToString("N");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
            var cancelPath = Path.Combine(paths.Tasks, runId + ".cancel");
            var cancellationWatch = Task.Run(async () =>
            {
                try
                {
                    while (!linked.IsCancellationRequested)
                    {
                        if (File.Exists(cancelPath)) { linked.Cancel(); break; }
                        await Task.Delay(200, linked.Token);
                    }
                }
                catch (OperationCanceledException) { }
            });
            var state = new InProjectTaskState(1, "ArchSift.InProject", runId, ContentHash.Text(root),
                "queued", 1, DateTimeOffset.UtcNow.ToString("O"), null, null, null, null, null,
                null, null, null, evidenceGrade, "current");
            Save(paths, state);
            void Phase(string phase)
            {
                state = state with { Phase = phase, Revision = state.Revision + 1 };
                Save(paths, state);
            }
            InProjectLoadedConfiguration? config = null;
            try
            {
                Phase("preflight");
                config = InProjectConfiguration.Load(root);
                var input = InputCapture.Capture(root, linked.Token);
                state = state with { RequestSha256 = RequestIdentity(config, input.Sha256, evidenceGrade),
                    InputSha256 = input.Sha256, RulesSha256 = config.RulesSha256,
                    ToolSha256 = config.ToolSha256, Revision = state.Revision + 1 };
                Save(paths, state);
                var result = await InProjectRuleService.RunAsync(root, linked.Token, runId, Phase,
                    evidenceGrade);
                state = state with { Phase = "completed", Revision = state.Revision + 1,
                    FinishedUtc = DateTimeOffset.UtcNow.ToString("O"),
                    ResultPath = "reports/" + runId + "/gate-result.json",
                    ExitCode = result.Gate.ExitCode, Freshness = result.Freshness };
                Save(paths, state);
                state = TryRetention(config, state, paths);
                linked.Cancel();
                await cancellationWatch;
                return new(state, result, false, false);
            }
            catch (Exception error) when (error is ConfigurationException or InvalidDataException or
                JsonException or SourceChangedException or OperationCanceledException or IOException or
                TimeoutException or UnauthorizedAccessException or InvalidOperationException or
                System.ComponentModel.Win32Exception)
            {
                var code = ExitCode(error);
                state = state with { Phase = code == 130 ? "cancelled" : "failed",
                    Revision = state.Revision + 1, FinishedUtc = DateTimeOffset.UtcNow.ToString("O"),
                    ExitCode = code, Diagnostic = error.Message, Freshness = "stale" };
                Save(paths, state);
                if (!error.Message.Contains("drift", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        SaveFailureReport(root, runId, state, config, error);
                        state = state with { ResultPath = "reports/" + runId + "/gate-result.json",
                            Revision = state.Revision + 1 };
                        Save(paths, state);
                    }
                    catch (Exception reportError) when (reportError is IOException or UnauthorizedAccessException or
                        ConfigurationException or JsonException or InvalidDataException)
                    {
                        state = state with { Diagnostic = error.Message + "; failure report: " + reportError.Message,
                            Revision = state.Revision + 1 };
                        Save(paths, state);
                    }
                }
                if (config is not null) state = TryRetention(config, state, paths);
                linked.Cancel();
                await cancellationWatch;
                return new(state, null, false, false);
            }
        }
    }

    public static InProjectTaskState Cancel(string targetRoot, string runId)
    {
        if (!Guid.TryParseExact(runId, "N", out _))
            throw new ConfigurationException("Invalid InProject run ID.");
        var paths = Paths(WorktreeLocator.Locate(targetRoot).Root);
        var state = ReadFile(paths.Current) ?? throw new ConfigurationException("No current InProject task.");
        if (state.RunId != runId) throw new ConfigurationException("Cancel run ID is not the current task.");
        if (Terminal.Contains(state.Phase)) return state;
        var marker = Path.Combine(paths.Tasks, runId + ".cancel");
        PathSafety.EnsureNoLinks(marker);
        try { using var stream = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
        catch (IOException) when (File.Exists(marker)) { }
        return state;
    }

    public static InProjectTaskState Status(string targetRoot, string? runId = null)
    {
        var root = WorktreeLocator.Locate(targetRoot).Root;
        var paths = Paths(root);
        if (runId is not null && !Guid.TryParseExact(runId, "N", out _))
            throw new ConfigurationException("Invalid InProject run ID.");
        var path = runId is null ? paths.Current : Path.Combine(paths.Tasks, runId + ".json");
        var state = ReadFile(path) ?? throw new ConfigurationException("InProject task was not found.");
        if (!Terminal.Contains(state.Phase))
        {
            try
            {
                using var probe = new FileStream(paths.Lease, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
                state = state with { Phase = "failed", Revision = state.Revision + 1,
                    FinishedUtc = DateTimeOffset.UtcNow.ToString("O"), ExitCode = 3,
                    Diagnostic = "Task owner stopped before writing a terminal state.", Freshness = "stale" };
                try
                {
                    SaveFailureReport(root, state.RunId, state, null,
                        new IOException(state.Diagnostic));
                    state = state with { ResultPath = "reports/" + state.RunId + "/gate-result.json" };
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                    ConfigurationException or JsonException or InvalidDataException)
                { state = state with { Diagnostic = state.Diagnostic + "; failure report: " + error.Message }; }
                Save(paths, state, updateCurrent: runId is null || ReadFile(paths.Current)?.RunId == state.RunId);
            }
            catch (IOException) { }
        }
        if (Terminal.Contains(state.Phase))
        {
            try
            {
                var config = InProjectConfiguration.Load(targetRoot);
                var input = InputCapture.Capture(config.Root);
                if (state.InputSha256 != input.Sha256 || state.RulesSha256 != config.RulesSha256 ||
                    state.ToolSha256 != config.ToolSha256 || state.RequestSha256 !=
                    RequestIdentity(config, input.Sha256, state.EvidenceGrade))
                    state = state with { Freshness = "stale" };
            }
            catch (Exception error) when (error is ConfigurationException or IOException or InvalidDataException)
            { state = state with { Freshness = "stale" }; }
        }
        return state;
    }

    private static int ExitCode(Exception error) => error switch
    {
        OperationCanceledException => 130,
        SourceChangedException => 4,
        ConfigurationException or InvalidDataException or JsonException => 5,
        _ => 3
    };

    private static InProjectTaskState TryRetention(InProjectLoadedConfiguration config, InProjectTaskState state,
        (string Current, string Tasks, string Lease) paths)
    {
        try { InProjectRetentionService.ApplyLocked(config); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            ConfigurationException or InvalidDataException or JsonException)
        {
            state = state with { Diagnostic = string.Join("; ", new[] { state.Diagnostic,
                    "Retention unavailable: " + error.Message }.Where(value => !string.IsNullOrWhiteSpace(value))),
                Revision = state.Revision + 1 };
            Save(paths, state);
        }
        return state;
    }

    private static void SaveFailureReport(string root, string runId, InProjectTaskState state,
        InProjectLoadedConfiguration? config, Exception error)
    {
        var directory = Path.Combine(root, ".archsift", "reports", runId);
        PathSafety.EnsureNoLinks(directory);
        Directory.CreateDirectory(directory);
        var owner = Path.Combine(directory, "owner.json");
        if (!File.Exists(owner))
            File.WriteAllText(owner, JsonSerializer.Serialize(new { schemaVersion = 1,
                product = "ArchSift.InProject", worktreeRoot = root, runId,
                sourceInputSha256 = state.InputSha256 }, JsonContract.Options));
        var capture = Path.Combine(directory, "daily-binding.json");
        var binding = new InProjectBindingVerdict(1, "ArchSift.InProject", runId,
            File.Exists(capture) ? LocalPackage.HashFile(capture) : new string('0', 64),
            "1", false, [error.Message]);
        var required = config?.Rules.SelectMany(document => document.Ruleset.Rules
            .Where(rule => rule.Enabled).Select(rule => document.Ruleset.Id + "@" +
                document.Ruleset.Version + "/" + rule.Id))
            .Order(StringComparer.Ordinal).ToArray() ?? [];
        var execution = state.ExitCode switch
        {
            130 => "cancelled",
            5 => "invalid-input",
            4 => "partial",
            _ => "failed"
        };
        var coverage = new InProjectGateCoverage(false, required, []);
        var gate = InProjectGatePolicy.Decide(execution, binding, coverage, [],
            new HashSet<string>(StringComparer.Ordinal), false);
        var report = new InProjectGateResult(1, "ArchSift.InProject", runId,
            state.WorktreeId, execution, binding, "daily", coverage, [], [],
            [error.Message], gate, new("available", "daily-index.json"), "stale");
        InProjectReportWriter.Save(directory, report, replace: true);
    }

    private static string RequestIdentity(InProjectLoadedConfiguration config, string inputSha,
        string evidenceGrade) =>
        ContentHash.Text(config.ProjectSha256 + "\n" + config.RulesSha256 + "\n" +
            config.ToolSha256 + "\n" + inputSha + "\n" + config.Configuration + "\n" + evidenceGrade);

    private static (string Current, string Tasks, string Lease) Paths(string root)
    {
        var state = Path.Combine(root, ".archsift", "state");
        var tasks = Path.Combine(state, "tasks");
        var current = Path.Combine(state, "current-task.json");
        var lease = Path.Combine(state, "task-lease.lock");
        foreach (var path in new[] { state, tasks, current, lease }) PathSafety.EnsureNoLinks(path);
        return (current, tasks, lease);
    }

    private static void RecoverLostTask((string Current, string Tasks, string Lease) paths, string root)
    {
        var previous = ReadFile(paths.Current);
        if (previous is null || Terminal.Contains(previous.Phase)) return;
        var lost = previous with { Phase = "failed", Revision = previous.Revision + 1,
            FinishedUtc = DateTimeOffset.UtcNow.ToString("O"), ExitCode = 3,
            Diagnostic = "Task owner stopped before writing a terminal state.", Freshness = "stale" };
        try
        {
            SaveFailureReport(root, lost.RunId, lost, null, new IOException(lost.Diagnostic));
            lost = lost with { ResultPath = "reports/" + lost.RunId + "/gate-result.json" };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            ConfigurationException or JsonException or InvalidDataException)
        { lost = lost with { Diagnostic = lost.Diagnostic + "; failure report: " + error.Message }; }
        Save(paths, lost);
    }

    private static InProjectTaskState? ReadFile(string path)
    {
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path)) return null;
        var result = JsonSerializer.Deserialize<InProjectTaskState>(File.ReadAllBytes(path), JsonContract.Options);
        if (result is null || result.SchemaVersion != 1 || result.Product != "ArchSift.InProject")
            throw new ConfigurationException("Invalid InProject task state.");
        return result;
    }

    private static void Save((string Current, string Tasks, string Lease) paths,
        InProjectTaskState state, bool updateCurrent = true)
    {
        Directory.CreateDirectory(paths.Tasks);
        var history = Path.Combine(paths.Tasks, state.RunId + ".json");
        WriteAtomic(history, state);
        if (updateCurrent) WriteAtomic(paths.Current, state);
    }

    private static void WriteAtomic(string path, InProjectTaskState state)
    {
        PathSafety.EnsureNoLinks(path);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonContract.Options) + "\n",
                new UTF8Encoding(false));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
