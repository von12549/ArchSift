using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public static class InProjectRetentionService
{
    public static InProjectRetentionPreview Preview(string targetRoot)
    {
        var config = InProjectConfiguration.Load(targetRoot);
        return Preview(config, DateTimeOffset.UtcNow);
    }

    public static InProjectRetentionPreview Apply(string targetRoot)
    {
        var config = InProjectConfiguration.Load(targetRoot);
        var lease = Path.Combine(config.InstallationRoot, "state", "task-lease.lock");
        PathSafety.EnsureNoLinks(lease);
        using var guard = new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        return ApplyLocked(config);
    }

    public static InProjectRetentionPreview ApplyLocked(InProjectLoadedConfiguration config)
    {
        var preview = Preview(config, DateTimeOffset.UtcNow);
        var problems = new List<string>(preview.Limitations);
        foreach (var selected in preview.Items.Where(item => item.SelectedForCompaction))
        {
            var current = ReadTask(Path.Combine(config.InstallationRoot, "state", "tasks", selected.RunId + ".json"));
            if (current.RunId != selected.RunId || current.EvidenceGrade != "daily" ||
                current.Phase is not ("completed" or "failed" or "cancelled") ||
                current.MaterialsState != "available" ||
                ReadCurrentId(config) == selected.RunId || Pinned(config).Contains(selected.RunId))
            {
                problems.Add("Run changed during retention preview: " + selected.RunId);
                continue;
            }
            var report = Path.Combine(config.InstallationRoot, "reports", selected.RunId);
            var build = Path.Combine(config.InstallationRoot, "state", "build", selected.RunId);
            try
            {
                VerifyOwnedRun(config, report, selected.RunId);
                if (Directory.Exists(build)) VerifyOwnedRun(config, build, selected.RunId);
                Directory.Delete(report, recursive: true);
                if (Directory.Exists(build)) Directory.Delete(build, recursive: true);
                SaveTask(Path.Combine(config.InstallationRoot, "state", "tasks", selected.RunId + ".json"),
                    current with { MaterialsState = "compacted", Revision = current.Revision + 1 });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ConfigurationException)
            {
                problems.Add("Compaction stopped for " + selected.RunId + ": " + error.Message);
                var state = current with { MaterialsState = Directory.Exists(report) ? "available" : "missing",
                    Revision = current.Revision + 1,
                    Diagnostic = string.Join("; ", new[] { current.Diagnostic, "Retention incomplete: " + error.Message }
                        .Where(value => !string.IsNullOrWhiteSpace(value))) };
                SaveTask(Path.Combine(config.InstallationRoot, "state", "tasks", selected.RunId + ".json"), state);
            }
        }
        var after = Preview(config, DateTimeOffset.UtcNow);
        return after with { Limitations = after.Limitations.Concat(problems).Distinct(StringComparer.Ordinal).ToArray() };
    }

    public static void SetPin(string targetRoot, string runId, bool pinned)
    {
        if (!Guid.TryParseExact(runId, "N", out _)) throw new ConfigurationException("Invalid pin run ID.");
        var config = InProjectConfiguration.Load(targetRoot);
        var lease = Path.Combine(config.InstallationRoot, "state", "task-lease.lock");
        PathSafety.EnsureNoLinks(lease);
        using var guard = new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        _ = ReadTask(Path.Combine(config.InstallationRoot, "state", "tasks", runId + ".json"));
        var pins = Pinned(config);
        if (pinned) pins.Add(runId); else pins.Remove(runId);
        var path = Path.Combine(config.InstallationRoot, "state", "pins.json");
        WriteAtomic(path, JsonSerializer.Serialize(new { schemaVersion = 1, product = "ArchSift.InProject",
            runs = pins.Order(StringComparer.Ordinal).ToArray() }, JsonContract.Options) + "\n");
    }

    private static InProjectRetentionPreview Preview(InProjectLoadedConfiguration config, DateTimeOffset now)
    {
        var tasksRoot = Path.Combine(config.InstallationRoot, "state", "tasks");
        var reportsRoot = Path.Combine(config.InstallationRoot, "reports");
        PathSafety.EnsureNoLinks(tasksRoot);
        PathSafety.EnsureNoLinks(reportsRoot);
        var tasks = Directory.Exists(tasksRoot)
            ? Directory.EnumerateFiles(tasksRoot, "*.json", SearchOption.TopDirectoryOnly)
                .Select(ReadTask).ToDictionary(task => task.RunId, StringComparer.Ordinal)
            : new Dictionary<string, InProjectTaskState>(StringComparer.Ordinal);
        var available = Directory.Exists(reportsRoot)
            ? Directory.EnumerateDirectories(reportsRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetFileName(path)!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var pins = Pinned(config);
        var current = ReadCurrentId(config);
        var limitations = new List<string>();
        foreach (var runId in available.Where(id => !tasks.ContainsKey(id)))
            limitations.Add("Unregistered report is protected: " + runId);
        var rows = new List<(InProjectTaskState Task, DateTimeOffset? Finished, List<string> Protected)>();
        foreach (var task in tasks.Values.OrderBy(task => task.RunId, StringComparer.Ordinal))
        {
            if (!available.Contains(task.RunId) || task.MaterialsState != "available") continue;
            var protection = new List<string>();
            if (task.RunId == current) protection.Add("current");
            if (pins.Contains(task.RunId)) protection.Add("pinned");
            if (task.EvidenceGrade == "full") protection.Add("full");
            if (task.Phase is not ("completed" or "failed" or "cancelled")) protection.Add("active");
            DateTimeOffset? finished = null;
            if (task.FinishedUtc is not null && DateTimeOffset.TryParse(task.FinishedUtc, out var parsed))
            {
                finished = parsed;
                if (parsed > now) protection.Add("future-time");
            }
            else protection.Add("unknown-time");
            rows.Add((task, finished, protection));
        }
        var daily = rows.Where(row => row.Task.EvidenceGrade == "daily").ToArray();
        var selected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in daily.Where(row => row.Protected.Count == 0 &&
            now - row.Finished!.Value > TimeSpan.FromDays(config.Retention.DailyDays)))
            selected[row.Task.RunId] = "age";
        var remaining = daily.Where(row => !selected.ContainsKey(row.Task.RunId)).ToArray();
        foreach (var row in remaining.Where(row => row.Protected.Count == 0)
            .OrderBy(row => row.Finished).ThenBy(row => row.Task.RunId, StringComparer.Ordinal))
        {
            if (remaining.Length - selected.Values.Count(reason => reason == "count") <=
                config.Retention.DailyMaxRuns) break;
            selected[row.Task.RunId] = "count";
        }
        var protectedDaily = daily.Count(row => row.Protected.Count > 0);
        var overBudget = daily.Length - selected.Count > config.Retention.DailyMaxRuns;
        if (overBudget) limitations.Add("Protected daily runs exceed the configured count budget.");
        var items = rows.Select(row => new InProjectRetentionItem(row.Task.RunId,
            row.Task.EvidenceGrade, row.Task.Phase, row.Task.FinishedUtc, row.Task.MaterialsState,
            row.Protected.ToArray(), selected.ContainsKey(row.Task.RunId),
            selected.GetValueOrDefault(row.Task.RunId))).ToArray();
        return new(1, "ArchSift.InProject", config.Retention.DailyDays,
            config.Retention.DailyMaxRuns, now.ToString("O"), daily.Length, protectedDaily,
            selected.Count, overBudget, items, limitations.ToArray());
    }

    internal static HashSet<string> Pinned(InProjectLoadedConfiguration config)
    {
        var path = Path.Combine(config.InstallationRoot, "state", "pins.json");
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path)) return new(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
            document.RootElement.GetProperty("product").GetString() != "ArchSift.InProject")
            throw new ConfigurationException("Invalid InProject pin index.");
        var runs = document.RootElement.GetProperty("runs").EnumerateArray()
            .Select(item => item.GetString() ?? "").ToArray();
        if (runs.Any(id => !Guid.TryParseExact(id, "N", out _)) ||
            runs.Distinct(StringComparer.Ordinal).Count() != runs.Length)
            throw new ConfigurationException("Invalid InProject pinned run IDs.");
        return runs.ToHashSet(StringComparer.Ordinal);
    }

    internal static string? ReadCurrentId(InProjectLoadedConfiguration config)
    {
        var path = Path.Combine(config.InstallationRoot, "state", "current-task.json");
        PathSafety.EnsureNoLinks(path);
        return File.Exists(path) ? ReadTask(path).RunId : null;
    }

    internal static InProjectTaskState ReadTask(string path)
    {
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path)) throw new ConfigurationException("InProject task index is missing.");
        var state = JsonSerializer.Deserialize<InProjectTaskState>(File.ReadAllBytes(path), JsonContract.Options);
        if (state is null || state.SchemaVersion != 1 || state.Product != "ArchSift.InProject" ||
            !Guid.TryParseExact(state.RunId, "N", out _))
            throw new ConfigurationException("Invalid InProject task index.");
        return state;
    }

    private static void VerifyOwnedRun(InProjectLoadedConfiguration config, string directory, string runId)
    {
        var reportRoot = Path.Combine(config.InstallationRoot, "reports");
        var buildRoot = Path.Combine(config.InstallationRoot, "state", "build");
        if (!PathSafety.IsUnder(directory, reportRoot) && !PathSafety.IsUnder(directory, buildRoot))
            throw new ConfigurationException("Retention target escaped its owned run root.");
        PathSafety.EnsureNoLinks(directory);
        var owner = Path.Combine(directory, "owner.json");
        PathSafety.EnsureNoLinks(owner);
        if (!File.Exists(owner)) throw new ConfigurationException("Owned run marker is missing.");
        using var document = JsonDocument.Parse(File.ReadAllBytes(owner));
        if (document.RootElement.GetProperty("product").GetString() != "ArchSift.InProject" ||
            document.RootElement.GetProperty("worktreeRoot").GetString() != config.Root ||
            document.RootElement.GetProperty("runId").GetString() != runId)
            throw new ConfigurationException("Run ownership differs from retention index.");
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
            PathSafety.EnsureNoLinks(entry);
    }

    internal static void SaveTask(string path, InProjectTaskState state) =>
        WriteAtomic(path, JsonSerializer.Serialize(state, JsonContract.Options) + "\n");

    private static void WriteAtomic(string path, string text)
    {
        PathSafety.EnsureNoLinks(path);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
