using System.Diagnostics;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public sealed record PreparedChain(ChainDocument Chain, ProjectSnapshot Source,
    ChainEntrySnapshot[] Entries, LoadedRuleset?[] Rulesets, string TargetKind)
{
    public string? PreparationError { get; init; }
}
public sealed record CapturedChain(ChainDocument Chain, ChainEntrySnapshot[] Entries, LoadedRuleset?[] Rulesets, string TargetKind);

public sealed class ChainService(AnalysisService analysis)
{
    public static PreparedChain Prepare(RunConfiguration config, ChainDocument chain, RulesetLibrary library, string targetKind = "real")
    {
        var captured = Capture(config, chain, library, targetKind);
        return new(chain, ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework),
            captured.Entries, captured.Rulesets, targetKind);
    }

    public static CapturedChain Capture(RunConfiguration config, ChainDocument chain, RulesetLibrary library, string targetKind = "real")
    {
        PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
        PathSafety.EnsureDisjoint(config.Target.Root, library.DirectoryPath);
        ChainStore.Parse(JsonSerializer.SerializeToUtf8Bytes(chain, JsonContract.Options));
        if (targetKind is not ("real" or "fixture")) throw new ConfigurationException("Target kind must be real or fixture.");
        var entries = new List<ChainEntrySnapshot>(); var frozen = new List<LoadedRuleset?>();
        foreach (var entry in chain.Entries)
        {
            var reference = library.Find(entry.EntryId);
            var path = reference is null ? null : library.FilePath(reference.FileName);
            try
            {
                var rules = library.Capture(entry.EntryId);
                entries.Add(new(entry.EntryId, path, rules.Identity, null, null)); frozen.Add(rules);
            }
            catch (Exception error) when (error is IOException or ConfigurationException or UnauthorizedAccessException)
            {
                var missing = reference is null || reference.Deleted || !File.Exists(path);
                entries.Add(new(entry.EntryId, path, null, missing ? "missing-ruleset" : "invalid-ruleset", error.Message)); frozen.Add(null);
            }
        }
        return new(chain, entries.ToArray(), frozen.ToArray(), targetKind);
    }

    public Task<ChainSummary> RunAsync(RunConfiguration config, ChainDocument chain, RulesetLibrary library,
        string targetKind = "real", CancellationToken token = default, Action<ChainProgress>? progress = null,
        ChainRunOptions? options = null)
    {
        options = ValidateOptions(options);
        return RunCapturedAsync(config, Capture(config, chain, library, targetKind), token, progress, options: options);
    }

    public async Task<ChainSummary> RunCapturedAsync(RunConfiguration config, CapturedChain captured,
        CancellationToken token = default, Action<ChainProgress>? progress = null, string? runId = null, ChainRunOptions? options = null)
    {
        options = ValidateOptions(options);
        var tracker = new ChainProgressTracker(captured.Chain, progress, runId);
        ProjectSnapshot source; string? error = null;
        try { source = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework, token); }
        catch (Exception e) when (e is OperationCanceledException or SourceChangedException)
        {
            error = e is OperationCanceledException ? "preparation-cancelled" : "global-input-drift";
            source = new([], new([], [], [], [], [error]), new(ContentHash.Text(""), []), [], config.Target.Entry, config.Build.TargetFramework);
        }
        return await RunPreparedAsync(config, new(captured.Chain, source, captured.Entries, captured.Rulesets, captured.TargetKind)
            { PreparationError = error }, token, tracker, options);
    }

    public Task<ChainSummary> RunAsync(RunConfiguration config, PreparedChain prepared, CancellationToken token = default,
        Action<ChainProgress>? progress = null, ChainRunOptions? options = null) =>
        RunPreparedAsync(config, prepared, token, new(prepared.Chain, progress, null), ValidateOptions(options));

    public static ChainRunOptions ValidateOptions(ChainRunOptions? options)
    {
        options ??= new();
        if (options.MaxConcurrency is < 1 or > 4) throw new ConfigurationException("Max concurrency must be an integer from 1 to 4.");
        return options;
    }

    private async Task<ChainSummary> RunPreparedAsync(RunConfiguration config, PreparedChain prepared, CancellationToken token,
        ChainProgressTracker tracker, ChainRunOptions options)
    {
        var timer = tracker.Timer; var id = tracker.Id; var started = tracker.Started;
        tracker.Preparation = timer.ElapsedMilliseconds;
        var directory = Path.Combine(config.Output.Directory, id); PathSafety.EnsureNoLinks(directory);
        Directory.CreateDirectory(directory);
        var children = new ChainChild[prepared.Entries.Length]; var limits = new List<string>();
        var coordination = new object();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var reports = new Dictionary<string, AnalysisReport>(StringComparer.Ordinal);
        AssemblyInputs? assemblies = null; string? buildError = null;
        var buildFiles = prepared.Source.InputIdentity.Files.Select(f => f with { Kind = "build" }).ToList();
        var evidenceChecks = new Dictionary<string, string?>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var buildSettings = JsonSerializer.SerializeToUtf8Bytes(config.Build, JsonContract.Options);
        buildFiles.Add(new("@context/build.json", ContentHash.Bytes(buildSettings), buildSettings.Length, "build"));
        if (config.Build.AssemblyManifest is { } manifest)
        {
            PathSafety.EnsureNoLinks(manifest);
            var hash = File.Exists(manifest) ? AssemblyArtifacts.FileHash(manifest) : null;
            evidenceChecks[manifest] = hash;
            if (hash is not null) buildFiles.Add(new("@manifest/assembly.json", hash, new FileInfo(manifest).Length, "build"));
        }
        foreach (var path in config.Build.AssemblyPaths)
        { PathSafety.EnsureNoLinks(path); evidenceChecks[path] = File.Exists(path) ? AssemblyArtifacts.FileHash(path) : null; }
        var cancelled = token.IsCancellationRequested || prepared.PreparationError == "preparation-cancelled";
        var drift = prepared.PreparationError == "global-input-drift";
        if (prepared.PreparationError is { } preparationError) limits.Add(preparationError);
        var evidenceStarted = timer.ElapsedMilliseconds;
        try
        {
            if (drift) throw new SourceChangedException();
            token.ThrowIfCancellationRequested();
            CheckInputs(); token.ThrowIfCancellationRequested();
            var needsBuild = config.Build.Mode == "isolated" || config.Build.AssemblyManifest is not null || config.Build.AssemblyPaths.Length > 0 ||
                prepared.Rulesets.Any(r => r is not null && r.Ruleset.Rules.Any(x => x.Enabled &&
                    (x.Type == "type-dependency" || x.Type == "naming" && x.Parameters.GetProperty("subjectKind").GetString() is "type" or "assembly")));
            if (needsBuild)
            {
                tracker.Stage("acquiring-evidence");
                try { assemblies = config.Build.Mode == "isolated"
                    ? await IsolatedBuild.BuildAsync(config with { Output = config.Output with { Directory = directory } }, prepared.Source, token)
                    : AssemblyArtifacts.Existing(config, prepared.Source); }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) when (error is IOException or ConfigurationException or InvalidDataException or InvalidOperationException or TimeoutException)
                { buildError = error.Message; }
            }
            if (assemblies is not null)
                foreach (var path in assemblies.Paths)
                {
                    evidenceChecks[path] = assemblies.Sha256[path];
                    buildFiles.Add(new("@assembly/" + Path.GetFileName(path), assemblies.Sha256[path], new FileInfo(path).Length, "evidence"));
                }
            CheckInputs();
        }
        catch (OperationCanceledException) { cancelled = true; }
        catch (SourceChangedException) { drift = true; limits.Add("global-input-drift"); }
        tracker.Evidence = timer.ElapsedMilliseconds - evidenceStarted;
        var snapshot = new ChainSnapshot(prepared.Chain.Id, prepared.Chain.Version, prepared.Entries, config.Target, config.Build,
            prepared.Source.InputIdentity, new(AssemblyArtifacts.FileIdentity(buildFiles.ToArray()), buildFiles.ToArray()), directory, prepared.TargetKind)
            { ExecutionOptions = options };
        ChainWriter.WriteNew(Path.Combine(directory, "snapshot.json"), JsonSerializer.Serialize(snapshot, JsonContract.Options));
        tracker.Stage("evaluating");
        var evaluationStarted = timer.ElapsedMilliseconds;
        if (options.MaxConcurrency == 1)
        {
            for (var index = 0; index < prepared.Entries.Length; index++) await RunEntry(index);
        }
        else
        {
            var active = new List<Task>(); Exception? fatal = null;
            async Task DrainOne()
            {
                var done = await Task.WhenAny(active); active.Remove(done);
                try { await done; } catch (Exception error) { fatal ??= error; stop.Cancel(); }
            }
            for (var index = 0; index < prepared.Entries.Length; index++)
            {
                while (active.Count >= options.MaxConcurrency) await DrainOne();
                if (fatal is not null || IsStopped()) { Complete(index, Skipped(index)); continue; }
                var capturedIndex = index;
                active.Add(Task.Run(() => RunEntry(capturedIndex)));
            }
            while (active.Count > 0) await DrainOne();
            if (fatal is not null) throw new IOException("Chain output failed after all owned entries stopped.", fatal);
        }
        tracker.Evaluation = timer.ElapsedMilliseconds - evaluationStarted;
        tracker.Stage("writing-reports");
        if (!IsStopped())
            try { CheckInputs(); }
            catch (SourceChangedException) { Drift(); }
            catch (OperationCanceledException) { Cancel(); }
        lock (coordination) cancelled |= token.IsCancellationRequested;
        var execution = cancelled ? "cancelled" : drift || children.Any(c => c.Execution != "completed") ? "partial" : "completed";
        var compliance = Aggregate(children);
        if (drift && compliance != "noncompliant") compliance = "inconclusive";
        var code = cancelled ? 130 : execution == "partial" || compliance == "inconclusive" ? 4 : 0;
        var summary = new ChainSummary(snapshot, execution, compliance, code, prepared.Source.Projects.Length, children, limits.Distinct().ToArray(),
            new(id, started, timer.ElapsedMilliseconds, config.Target.Root)) { AnalysisReports = reports, SchemaVersion = 2, Timings = tracker.Timings() };
        ChainWriter.Save(summary);
        if (token.IsCancellationRequested && summary.Execution != "cancelled")
        {
            summary = summary with { Execution = "cancelled", ExitCode = 130 };
            ChainWriter.Replace(summary);
        }
        tracker.Stage("finished"); return summary;

        bool IsStopped() { lock (coordination) return cancelled || drift || token.IsCancellationRequested; }
        bool HasDrift() { lock (coordination) return drift; }
        void Drift()
        {
            lock (coordination) { drift = true; if (!limits.Contains("global-input-drift", StringComparer.Ordinal)) limits.Add("global-input-drift"); }
            stop.Cancel();
        }
        void Cancel()
        {
            lock (coordination) { if (!drift || token.IsCancellationRequested) cancelled = true; }
            stop.Cancel();
        }
        ChainChild Skipped(int index) => new(prepared.Entries[index].EntryId, "skipped", "inconclusive", null, null, null,
            new(0, 0, false), "none", [], [], [HasDrift() ? "global-input-drift" : "User cancelled chain."]);
        void Complete(int index, ChainChild child)
        {
            children[index] = child; tracker.Entry(index, child.Execution, child);
        }
        AnalysisReport Invalidate(AnalysisReport report) => report with
        {
            Execution = "partial", Compliance = "inconclusive",
            Limitations = [.. report.Limitations.Where(l => l != "User cancelled this run."), "global-input-drift"],
            RuleResults = report.RuleResults.Select(r => r with { Status = "inconclusive", Limitations = [.. r.Limitations, "global-input-drift"] }).ToArray(),
            Coverage = report.Coverage with { SourceBound = false }, BuildContext = report.BuildContext with { Binding = "none" }
        };
        async Task RunEntry(int index)
        {
            var entry = prepared.Entries[index];
            if (!IsStopped())
                try { CheckInputs(); }
                catch (SourceChangedException) { Drift(); }
                catch (OperationCanceledException) { Cancel(); }
            if (IsStopped()) { Complete(index, Skipped(index)); return; }
            var entryDirectory = Path.Combine(directory, entry.EntryId);
            tracker.Entry(index, "running");
            try
            {
                Directory.CreateDirectory(entryDirectory);
                if (prepared.Rulesets[index] is not { } rules)
                {
                    var diagnostic = new ChainDiagnostic(entry.EntryId, entry.ErrorCode!, entry.ErrorMessage!);
                    tracker.Entry(index, "writing"); tracker.Write(() => ChainWriter.SaveDiagnostic(entryDirectory, diagnostic));
                    Complete(index, new(entry.EntryId, "failed", "inconclusive", 2, entry.EntryId, diagnostic, new(0, 0, false), "none", [], [], [])); return;
                }
                var childConfig = config with { Rulesets = [rules.Identity.Path], Output = new() { Directory = entryDirectory, Formats = ["json", "html", "sarif"] } };
                var outcome = await analysis.RunFrozenAsync(childConfig, rules, prepared.Source, assemblies, buildError, stop.Token);
                var report = outcome.Report;
                if (report.Limitations.Contains("source-changed-during-analysis", StringComparer.Ordinal)) Drift();
                if (!IsStopped())
                    try { CheckInputs(); }
                    catch (SourceChangedException) { Drift(); }
                    catch (OperationCanceledException) { Cancel(); }
                if (report.Execution == "cancelled" && !HasDrift()) Cancel();
                tracker.Entry(index, "writing");
                // Result publication is serialized with the global stop decision. Each entry still owns its files.
                lock (coordination)
                {
                    if (drift) report = Invalidate(report);
                    tracker.Write(() => ReportWriter.Save(childConfig, report));
                    reports[entry.EntryId] = report;
                    var childLimits = report.Limitations.Concat(report.ExecutionErrors).Concat(report.RuleResults.SelectMany(r => r.Limitations)).Distinct().ToArray();
                    Complete(index, new(entry.EntryId, report.Execution, report.Compliance ?? "inconclusive", drift ? 4 : outcome.ExitCode,
                        entry.EntryId + "/" + report.RunMetadata.RunId, null, report.Coverage, report.BuildContext.Binding, report.RuleResults, report.Findings, childLimits));
                }
            }
            catch (OperationCanceledException)
            {
                Cancel();
                Complete(index, new(entry.EntryId, HasDrift() ? "partial" : "cancelled", "inconclusive", HasDrift() ? 4 : 130,
                    null, null, new(0, 0, false), "none", [], [], [HasDrift() ? "global-input-drift" : "User cancelled chain."]));
            }
            catch (Exception error) when (error is IOException or ConfigurationException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
            {
                var diagnostic = new ChainDiagnostic(entry.EntryId, "child-error", error.Message);
                tracker.Entry(index, "writing"); tracker.Write(() => ChainWriter.SaveDiagnostic(entryDirectory, diagnostic));
                Complete(index, new(entry.EntryId, "failed", "inconclusive", 3, entry.EntryId, diagnostic, new(0, 0, false), "none", [], [], []));
            }
        }
        void CheckInputs()
        {
            tracker.Verify(VerifyInputs);
        }
        void VerifyInputs()
        {
            try { InputCapture.VerifyUnchanged(config.Target.Root, prepared.Source.InputIdentity, stop.Token); }
            catch (Exception error) when (error is IOException or ConfigurationException or UnauthorizedAccessException) { throw new SourceChangedException(); }
            foreach (var (path, expected) in evidenceChecks)
            {
                stop.Token.ThrowIfCancellationRequested();
                try { PathSafety.EnsureNoLinks(path); if ((File.Exists(path) ? AssemblyArtifacts.FileHash(path) : null) != expected) throw new SourceChangedException(); }
                catch (Exception error) when (error is IOException or ConfigurationException or UnauthorizedAccessException) { throw new SourceChangedException(); }
            }
        }
    }
    public static string Aggregate(IEnumerable<ChainChild> entries)
    {
        var children = entries.ToArray();
        if (children.Any(c => c.Compliance == "noncompliant")) return "noncompliant";
        if (children.Any(c => c.Compliance == "inconclusive" || c.Execution is "failed" or "skipped" or "cancelled")) return "inconclusive";
        return children.Any(c => c.Compliance == "compliant") ? "compliant" : "not-applicable";
    }
}
