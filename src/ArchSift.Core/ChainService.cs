using System.Diagnostics;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public sealed record PreparedChain(ChainDocument Chain, ProjectSnapshot Source,
    ChainEntrySnapshot[] Entries, LoadedRuleset?[] Rulesets, string TargetKind);

public sealed class ChainService(AnalysisService analysis)
{
    public static PreparedChain Prepare(RunConfiguration config, ChainDocument chain, RulesetLibrary library, string targetKind = "real")
    {
        PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
        PathSafety.EnsureDisjoint(config.Target.Root, library.DirectoryPath);
        ChainStore.Parse(JsonSerializer.SerializeToUtf8Bytes(chain, JsonContract.Options));
        if (targetKind is not ("real" or "fixture")) throw new ConfigurationException("Target kind must be real or fixture.");
        var source = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework);
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
        return new(chain, source, entries.ToArray(), frozen.ToArray(), targetKind);
    }

    public Task<ChainSummary> RunAsync(RunConfiguration config, ChainDocument chain, RulesetLibrary library,
        string targetKind = "real", CancellationToken token = default) => RunAsync(config, Prepare(config, chain, library, targetKind), token);

    public async Task<ChainSummary> RunAsync(RunConfiguration config, PreparedChain prepared, CancellationToken token = default)
    {
        var timer = Stopwatch.StartNew(); var id = Guid.NewGuid().ToString("N"); var started = DateTimeOffset.UtcNow.ToString("O");
        var directory = Path.Combine(config.Output.Directory, id); PathSafety.EnsureNoLinks(directory);
        Directory.CreateDirectory(directory);
        var children = new List<ChainChild>(); var limits = new List<string>();
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
        var cancelled = token.IsCancellationRequested; var drift = false;
        try
        {
            CheckInputs(); token.ThrowIfCancellationRequested();
            var needsBuild = config.Build.Mode == "isolated" || config.Build.AssemblyManifest is not null || config.Build.AssemblyPaths.Length > 0 ||
                prepared.Rulesets.Any(r => r is not null && r.Ruleset.Rules.Any(x => x.Enabled &&
                    (x.Type == "type-dependency" || x.Type == "naming" && x.Parameters.GetProperty("subjectKind").GetString() is "type" or "assembly")));
            if (needsBuild)
            {
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
        var snapshot = new ChainSnapshot(prepared.Chain.Id, prepared.Chain.Version, prepared.Entries, config.Target, config.Build,
            prepared.Source.InputIdentity, new(AssemblyArtifacts.FileIdentity(buildFiles.ToArray()), buildFiles.ToArray()), directory, prepared.TargetKind);
        ChainWriter.WriteNew(Path.Combine(directory, "snapshot.json"), JsonSerializer.Serialize(snapshot, JsonContract.Options));
        for (var index = 0; index < prepared.Entries.Length; index++)
        {
            var entry = prepared.Entries[index];
            cancelled |= token.IsCancellationRequested;
            if (!cancelled && !drift)
            {
                try { CheckInputs(); }
                catch (SourceChangedException) { drift = true; limits.Add("global-input-drift"); }
            }
            if (cancelled || drift)
            {
                children.Add(new(entry.EntryId, "skipped", "inconclusive", null, null, null, new(0, 0, false), "none", [], [],
                    [cancelled ? "User cancelled chain." : "global-input-drift"])); continue;
            }
            var entryDirectory = Path.Combine(directory, entry.EntryId);
            Directory.CreateDirectory(entryDirectory);
            if (prepared.Rulesets[index] is not { } rules)
            {
                var diagnostic = new ChainDiagnostic(entry.EntryId, entry.ErrorCode!, entry.ErrorMessage!);
                ChainWriter.SaveDiagnostic(entryDirectory, diagnostic);
                children.Add(new(entry.EntryId, "failed", "inconclusive", 2, entry.EntryId, diagnostic, new(0, 0, false), "none", [], [], [])); continue;
            }
            try
            {
                var childConfig = config with { Rulesets = [rules.Identity.Path], Output = new() { Directory = entryDirectory, Formats = ["json", "html", "sarif"] } };
                var outcome = await analysis.RunFrozenAsync(childConfig, rules, prepared.Source, assemblies, buildError, token);
                var report = outcome.Report;
                try { CheckInputs(); }
                catch (SourceChangedException)
                {
                    drift = true; limits.Add("global-input-drift");
                    report = report with { Execution = "partial", Compliance = "inconclusive", Limitations = [.. report.Limitations, "global-input-drift"],
                        RuleResults = report.RuleResults.Select(r => r with { Status = "inconclusive", Limitations = [.. r.Limitations, "global-input-drift"] }).ToArray(),
                        Coverage = report.Coverage with { SourceBound = false }, BuildContext = report.BuildContext with { Binding = "none" } };
                }
                if (report.Limitations.Contains("source-changed-during-analysis", StringComparer.Ordinal)) { drift = true; limits.Add("global-input-drift"); }
                ReportWriter.Save(childConfig, report);
                reports[entry.EntryId] = report;
                var childLimits = report.Limitations.Concat(report.ExecutionErrors).Concat(report.RuleResults.SelectMany(r => r.Limitations)).Distinct().ToArray();
                children.Add(new(entry.EntryId, report.Execution, report.Compliance ?? "inconclusive", drift ? 4 : outcome.ExitCode,
                    entry.EntryId + "/" + report.RunMetadata.RunId, null, report.Coverage, report.BuildContext.Binding, report.RuleResults, report.Findings, childLimits));
                if (report.Execution == "cancelled") cancelled = true;
            }
            catch (OperationCanceledException) { cancelled = true; children.Add(new(entry.EntryId, "skipped", "inconclusive", null, null, null, new(0, 0, false), "none", [], [], ["User cancelled chain."])); }
            catch (Exception error) when (error is IOException or ConfigurationException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
            {
                var diagnostic = new ChainDiagnostic(entry.EntryId, "child-error", error.Message); ChainWriter.SaveDiagnostic(entryDirectory, diagnostic);
                children.Add(new(entry.EntryId, "failed", "inconclusive", 3, entry.EntryId, diagnostic, new(0, 0, false), "none", [], [], []));
            }
        }
        var execution = cancelled ? "cancelled" : drift || children.Any(c => c.Execution != "completed") ? "partial" : "completed";
        var compliance = Aggregate(children);
        var code = cancelled ? 130 : execution == "partial" || compliance == "inconclusive" ? 4 : 0;
        var summary = new ChainSummary(snapshot, execution, compliance, code, prepared.Source.Projects.Length, children.ToArray(), limits.Distinct().ToArray(),
            new(id, started, timer.ElapsedMilliseconds, config.Target.Root)) { AnalysisReports = reports };
        ChainWriter.Save(summary); return summary;

        void CheckInputs()
        {
            try { InputCapture.VerifyUnchanged(config.Target.Root, prepared.Source.InputIdentity); }
            catch (Exception error) when (error is IOException or ConfigurationException or UnauthorizedAccessException) { throw new SourceChangedException(); }
            foreach (var (path, expected) in evidenceChecks)
            {
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
