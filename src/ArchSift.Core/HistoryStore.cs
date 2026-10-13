using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;

namespace ArchSift.Core;

/// <summary>Reads persisted reports. Neither history nor its rebuildable index verifies current source.</summary>
public sealed class HistoryStore(RunConfiguration config)
{
    public const int MaximumArtifactBytes = 16 * 1024 * 1024;
    private readonly object sync = new();
    private HistoryItem[]? index;
    private string? catalogError;
    private string Root => Path.GetFullPath(config.Output.Directory);
    private string CatalogPath => PathSafety.Under(Root, ".archsift-history.json");

    public HistoryDirectoryRegistration[] Directories()
    {
        lock (sync)
        {
            var result = new List<HistoryDirectoryRegistration> { new(Root, config.Target.Root) };
            catalogError = null;
            try
            {
            if (!File.Exists(CatalogPath)) return result.ToArray();
            using var document = JsonDocument.Parse(Read(CatalogPath));
            SchemaValidation.ValidateDocument(document.RootElement, JsonSerializer.SerializeToElement(new { type = "object" }));
            var catalog = document.RootElement.Deserialize<HistoryCatalog>(JsonContract.Options)
                ?? throw new ConfigurationException("History catalog is empty.");
            if (catalog.SchemaVersion != 1 || catalog.Directories.Length > 100)
                throw new ConfigurationException("Unsupported or oversized history catalog.");
            foreach (var registration in catalog.Directories)
            {
                ValidateRegistration(registration);
                if (!result.Any(r => string.Equals(r.Directory, registration.Directory, PathSafety.Comparison))) result.Add(registration);
            }
            }
            catch (Exception error) when (error is ConfigurationException or JsonException or IOException or UnauthorizedAccessException)
            { catalogError = "History directory catalog is unavailable: " + error.Message; }
            return result.ToArray();
        }
    }

    public void Register(HistoryDirectoryRegistration registration)
    {
        lock (sync)
        {
            registration = registration with { Directory = Path.GetFullPath(registration.Directory), TargetRoot = Path.GetFullPath(registration.TargetRoot) };
            ValidateRegistration(registration);
            if (!Directory.Exists(registration.Directory)) throw new ConfigurationException("History directory does not exist.");
            var entries = Directories().Where(r => !string.Equals(r.Directory, Root, PathSafety.Comparison)).ToList();
            if (catalogError is not null) throw new ConfigurationException(catalogError + " Preserve and repair the catalog before changing registrations.");
            if (!entries.Any(r => string.Equals(r.Directory, registration.Directory, PathSafety.Comparison))) entries.Add(registration);
            if (entries.Count > 100) throw new ConfigurationException("At most 100 additional history directories are allowed.");
            Atomic(CatalogPath, new HistoryCatalog(1, entries.ToArray())); index = null;
        }
    }

    private void ValidateRegistration(HistoryDirectoryRegistration entry)
    {
        if (!Path.IsPathRooted(entry.Directory) || !Path.IsPathRooted(entry.TargetRoot) ||
            !string.Equals(Path.GetFullPath(entry.TargetRoot), Path.GetFullPath(config.Target.Root), PathSafety.Comparison))
            throw new ConfigurationException("History directories must be registered for the current target project.");
        PathSafety.EnsureDisjoint(config.Target.Root, entry.Directory); PathSafety.EnsureNoLinks(entry.Directory);
    }

    public HistoryPage List(HistoryQuery query, IEnumerable<HistoryItem>? live = null, bool refresh = false)
    {
        WorkbenchRequests.History(query);
        lock (sync)
        {
            if (refresh || index is null) Rebuild();
            var running = (live ?? []).ToArray();
            var items = index!.Where(item => !running.Any(job =>
                job.ArtifactPath is not null && item.ArtifactPath is not null &&
                string.Equals(job.ArtifactPath, item.ArtifactPath, PathSafety.Comparison) ||
                job.RunId is not null && job.RunId == item.RunId && item.Operation == job.Operation &&
                item.ArtifactPath?.EndsWith("history-job.json", StringComparison.Ordinal) == true ||
                job.Id == item.RunId && item.ArtifactPath?.EndsWith("history-job.json", StringComparison.Ordinal) == true)).Concat(running);
            if (query.Operation is not null) items = items.Where(i => i.Operation == query.Operation);
            if (query.State is not null) items = items.Where(i => i.State == query.State);
            if (query.Selection is not null) items = items.Where(i => i.Selection == query.Selection);
            var ordered = items.OrderByDescending(i => Instant(i.StartedUtc)).ThenBy(i => i.Id, StringComparer.Ordinal).ToArray();
            return new(ordered.Skip(query.Offset).Take(query.Limit).ToArray(), query.Offset, query.Limit, ordered.Length);
        }
    }

    public void Invalidate() { lock (sync) index = null; }

    private void Rebuild()
    {
        var records = new List<HistoryItem>();
        foreach (var registration in Directories())
        {
            if (!Directory.Exists(registration.Directory)) { records.Add(Unavailable(registration.Directory, registration.Directory, "Registered history directory is unavailable.")); continue; }
            string[] candidates;
            try { candidates = Files(registration.Directory).ToArray(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ConfigurationException)
            { records.Add(Unavailable(registration.Directory, registration.Directory, error.Message)); continue; }
            var chainRoots = candidates.Where(p => Path.GetFileName(p) == "chain-summary.json").Select(p => Path.GetDirectoryName(p)!).ToArray();
            foreach (var file in candidates)
            {
                if (Path.GetFileName(file) == "report.json" && chainRoots.Any(root => PathSafety.IsUnder(file, root))) continue;
                try
                {
                    var view = Load(file, registration.Directory);
                    // The report is authoritative; the UI task receipt only supplies failures/no-report cases.
                    if (Path.GetFileName(file) == "history-job.json" && view.History.Diagnostic == "result-saved-separately") continue;
                    records.Add(view.History);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ConfigurationException or InvalidOperationException or FormatException)
                { records.Add(Unavailable(file, registration.Directory, error.Message)); }
            }
        }
        if (catalogError is not null) records.Add(Unavailable(Path.Combine(Root, ".archsift-history.json"), Root, catalogError));
        index = records.DistinctBy(r => r.Id).ToArray();
        Atomic(PathSafety.Under(Root, ".archsift-history-index.json"), new { schemaVersion = 1, items = index });
    }

    private static IEnumerable<string> Files(string root)
    {
        var stack = new Stack<string>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var directory = stack.Pop(); PathSafety.EnsureNoLinks(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) continue;
                if (Directory.Exists(entry)) stack.Push(entry);
                else if (Path.GetFileName(entry) is "report.json" or "chain-summary.json" or "comparison.json" or "history-job.json") yield return entry;
            }
        }
    }

    public HistoryView Open(string id)
    {
        lock (sync)
        {
            if (index is null) Rebuild();
            var item = index!.SingleOrDefault(i => i.Id == id) ?? throw new ConfigurationException("History item is not registered.");
            if (item.ArtifactPath is null || item.ArtifactSha256 is null) throw new ConfigurationException(item.Diagnostic ?? "History artifact is unavailable.");
            EnsureRegistered(item.ArtifactPath);
            var bytes = Read(item.ArtifactPath);
            if (ContentHash.Bytes(bytes) != item.ArtifactSha256) throw new ConfigurationException("History artifact changed. Refresh history to inspect its current bytes.");
            return Load(item.ArtifactPath, item.SourceDirectory, bytes);
        }
    }

    public (byte[] Bytes, string Name) Download(string id, string format, string? entryId = null)
    {
        if (format is not ("json" or "html" or "sarif")) throw new ConfigurationException("Unknown history download format.");
        var view = Open(id); var artifact = view.History.ArtifactPath!;
        if (entryId is not null)
        {
            var entry = view.Chain?.Entries.SingleOrDefault(e => e.EntryId == entryId);
            if (entry?.ReportDirectory is null) throw new ConfigurationException("Historical child report is unavailable.");
            artifact = PathSafety.Under(Path.GetDirectoryName(artifact)!, entry.ReportDirectory + "/" + (entry.Diagnostic is null ? "report.json" : "diagnostic.json"));
        }
        artifact = Path.ChangeExtension(artifact, format); EnsureRegistered(artifact);
        return (Read(artifact), Path.GetFileName(artifact));
    }

    private void EnsureRegistered(string artifact)
    {
        if (!Directories().Any(r => PathSafety.IsUnder(artifact, r.Directory))) throw new ConfigurationException("History artifact is outside registered directories.");
        PathSafety.EnsureNoLinks(artifact);
    }

    private HistoryView Load(string file, string directory, byte[]? bytes = null)
    {
        bytes ??= Read(file);
        using var original = JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0));
        SchemaValidation.ValidateDocument(original.RootElement, JsonSerializer.SerializeToElement(new { type = "object" }));
        var hash = ContentHash.Bytes(bytes); var id = ContentHash.Text(Path.GetFullPath(file));
        if (Path.GetFileName(file) == "history-job.json")
        {
            var job = original.RootElement.Deserialize<HistoryJobRecord>(JsonContract.Options)!;
            if (job.SchemaVersion != 1 || job.Kind != "history-job") throw new ConfigurationException("Unsupported history job record.");
            var hasResult = job.ArtifactPath is not null && PathSafety.IsUnder(job.ArtifactPath, directory) && File.Exists(job.ArtifactPath);
            var jobState = job.State == "running" ? "unavailable" : job.State;
            var item = new HistoryItem(id, job.Id, job.Operation, jobState, job.ExitCode, job.StartedUtc, job.ElapsedMilliseconds,
                job.Context.Target.Root, job.Selection, file, hash, directory, job.ToolVersion, "not-revalidated",
                hasResult ? "result-saved-separately" : job.State == "running" ? "Previous session has no recorded terminal state." : job.Error ?? (job.EvidenceValidation is null ? "Result JSON was not saved." : null));
            return new(id, job.Operation, jobState, job.ExitCode, item.Diagnostic, job.Context, job.TargetKind, job.Selection, null, null, null, null, item, job.EvidenceValidation);
        }
        var node = JsonNode.Parse(original.RootElement.GetRawText())!;
        var metadata = original.RootElement.TryGetProperty("runMetadata", out var meta) ? meta : default;
        var started = String(metadata, "startedUtc");
        var elapsed = metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("elapsedMilliseconds", out var duration) && duration.TryGetInt64(out var milliseconds) && milliseconds >= 0 ? (long?)milliseconds : null;
        var run = String(metadata, "runId");
        NormalizeMetadata(node);
        var version = node["schemaVersion"]?.GetValue<int>() ?? throw new ConfigurationException("History schemaVersion is missing.");
        AnalysisReport? report = null; ComparisonReport? comparison = null; ChainSummary? chain = null;
        string operation, state, target, tool; int exit; string? selection = null; RunConfiguration? context = null;
        using var normalized = JsonDocument.Parse(node.ToJsonString());
        switch (Path.GetFileName(file))
        {
            case "chain-summary.json":
                if (version is not (1 or 2)) throw new ConfigurationException("Unsupported history chain schema.");
                SchemaValidation.Validate(normalized.RootElement, version == 2 ? "chain-summary-v2" : "chain-summary");
                chain = normalized.RootElement.Deserialize<ChainSummary>(JsonContract.Options)!;
                foreach (var entry in chain.Entries.Where(e => e.ReportDirectory is not null)) PathSafety.Under(Path.GetDirectoryName(file)!, entry.ReportDirectory!);
                operation = "chain"; state = chain.Execution; target = chain.Snapshot.Target.Root; exit = chain.ExitCode; tool = chain.ToolVersion; selection = chain.Snapshot.ChainId;
                context = new() { SchemaVersion = 1, Target = chain.Snapshot.Target, Build = chain.Snapshot.Build, Output = new() { Directory = directory } };
                break;
            case "comparison.json":
                if (version != 1) throw new ConfigurationException("Unsupported history comparison schema.");
                SchemaValidation.Validate(normalized.RootElement, "comparison"); comparison = normalized.RootElement.Deserialize<ComparisonReport>(JsonContract.Options)!;
                operation = "changes"; state = comparison.Status; target = comparison.RunMetadata.TargetRoot; exit = state == "cancelled" ? 130 : state == "completed" ? 0 : 4; tool = comparison.ToolVersion;
                break;
            default:
                if (version != 1) throw new ConfigurationException("Unsupported history report schema.");
                SchemaValidation.Validate(normalized.RootElement, "report"); report = normalized.RootElement.Deserialize<AnalysisReport>(JsonContract.Options)!;
                operation = report.Operation; state = report.Execution; target = report.RunMetadata.TargetRoot; tool = report.ToolVersion;
                selection = report.RulesetIdentities.Length == 0 ? null : string.Join(", ", report.RulesetIdentities.Select(r => r.Id));
                exit = state == "cancelled" ? 130 : state == "failed" || report.ExecutionErrors.Length > 0 ? 3 : state == "partial" || report.Compliance == "inconclusive" ? 4 : 0;
                break;
        }
        var historical = new HistoryItem(id, run, operation, state, exit, started, elapsed, target, selection, file, hash, directory, tool, "not-revalidated",
            string.Equals(target, config.Target.Root, PathSafety.Comparison) ? null : "Historical target differs from the current target; identity is retained.");
        return new(id, operation, state, exit, null, context, chain?.Snapshot.TargetKind ?? "historical", selection, report, comparison, chain,
            chain is null ? null : ChainProjection.Aggregate(chain), historical);
    }

    private static string? String(JsonElement value, string field) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(field, out var entry) && entry.ValueKind == JsonValueKind.String ? entry.GetString() : null;
    private static void NormalizeMetadata(JsonNode node)
    {
        if (node["runMetadata"] is not JsonObject metadata) node["runMetadata"] = metadata = new();
        metadata["runId"] ??= "unknown-legacy-run"; metadata["startedUtc"] ??= "unknown";
        metadata["elapsedMilliseconds"] ??= 0; metadata["targetRoot"] ??= node["snapshot"]?["target"]?["root"]?.GetValue<string>() ?? "unknown";
        if (node["baseline"] is JsonObject baseline) NormalizeMetadata(baseline);
        if (node["target"] is JsonObject target && target.ContainsKey("schemaVersion")) NormalizeMetadata(target);
    }
    private HistoryItem Unavailable(string file, string directory, string error) => new(ContentHash.Text(Path.GetFullPath(file)), null, "unknown", "unavailable", null, null, null,
        config.Target.Root, null, file, null, directory, "unknown", "not-revalidated", error);
    private static DateTimeOffset Instant(string? time) => DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) &&
        time is not null && (time.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(time, @"[+-]\d{2}:\d{2}$")) ? value.ToUniversalTime() : DateTimeOffset.MinValue;
    private static byte[] Read(string file)
    {
        PathSafety.EnsureNoLinks(file);
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumArtifactBytes) throw new ConfigurationException("History artifact exceeds 16 MiB.");
        using var memory = new MemoryStream(); var buffer = new byte[65536]; int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            if (memory.Length + count > MaximumArtifactBytes) throw new ConfigurationException("History artifact exceeds 16 MiB.");
            memory.Write(buffer, 0, count);
        }
        return memory.ToArray();
    }
    public static void SaveJob(HistoryJobRecord job)
    {
        PathSafety.EnsureDisjoint(job.Context.Target.Root, job.Context.Output.Directory);
        var path = PathSafety.Under(job.Context.Output.Directory, ".archsift-jobs/" + job.Id + "/history-job.json");
        Atomic(path, job);
    }
    private static void Atomic<T>(string path, T value)
    {
        PathSafety.EnsureNoLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        ChainWriter.WriteNew(temporary, JsonSerializer.Serialize(value, JsonContract.Options));
        PathSafety.EnsureNoLinks(path); File.Move(temporary, path, true);
    }
}
