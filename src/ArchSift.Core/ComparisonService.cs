using System.Diagnostics;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public sealed record ComparisonOutcome(ComparisonReport Report, int ExitCode);

public sealed class ComparisonService(AnalysisService analysis)
{
    public async Task<ComparisonOutcome> RunAsync(RunConfiguration config, ChangeRequest request, CancellationToken token = default)
    {
        if ((request.Base is null) != (request.Head is null)) throw new ConfigurationException("Specify both --base and --head, or neither.");
        PathSafety.EnsureNoLinks(config.Target.Root); PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
        if (config.RulesDirectory is { } editor) PathSafety.EnsureDisjoint(config.Target.Root, editor);
        var clock = Stopwatch.StartNew(); var id = Guid.NewGuid().ToString("N"); var started = DateTimeOffset.UtcNow.ToString("O");
        var directory = PathSafety.Under(config.Output.Directory, id);
        var home = Path.Combine(directory, "git-home");
        var repository = await GitSnapshots.RepositoryRoot(config.Target.Root, home, token);
        PathSafety.EnsureNoLinks(repository); PathSafety.EnsureDisjoint(repository, config.Output.Directory);
        Directory.CreateDirectory(directory);
        var prefix = Path.GetRelativePath(repository, config.Target.Root);
        var baseCommit = await GitSnapshots.Resolve(repository, request.Base ?? "HEAD", home, token);
        var headCommit = request.Head is null ? null : await GitSnapshots.Resolve(repository, request.Head, home, token);
        var before = headCommit is null ? InputCapture.Capture(config.Target.Root, token) : null;
        var empty = new InputIdentity(ContentHash.Text(""), []);
        var baseIdentity = new SnapshotIdentity("commit", baseCommit, empty, []);
        var targetIdentity = new SnapshotIdentity(headCommit is null ? "worktree" : "commit", headCommit, empty, []);
        AnalysisReport? baseline = null, target = null;
        var basePolicy = new Dictionary<string, string>(); var targetPolicy = new Dictionary<string, string>();
        var errors = new List<string>(); var limits = new List<string>(); var codes = new List<int>();
        // Freeze external policies once; root-internal policies are read independently from each revision.
        var externalRules = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var rule in config.Rulesets.Where(p => !PathSafety.IsUnder(p, config.Target.Root)))
        {
            if (externalRules.ContainsKey(rule)) continue;
            PathSafety.EnsureNoLinks(rule);
            var destination = Path.Combine(directory, "policy", externalRules.Count + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(rule, destination, false); externalRules.Add(rule, destination);
        }
        async Task<(AnalysisReport?, SnapshotIdentity, Dictionary<string, string>)> Side(bool isBase)
        {
            var name = isBase ? "baseline" : "target";
            var source = Path.Combine(directory, name, "source");
            var identity = isBase ? baseIdentity : targetIdentity;
            try
            {
                string[] paths;
                if (identity.Commit is { } commit) paths = await GitSnapshots.Materialize(repository, prefix, commit, source, home, token);
                else
                {
                    paths = await GitSnapshots.WorktreeFiles(config.Target.Root, home, token);
                    GitSnapshots.CopyInputs(config.Target.Root, source, before!, token);
                }
                var captured = InputCapture.Capture(source, token);
                identity = identity with { Inputs = captured, ExcludedFiles = paths.Except(captured.Files.Select(f => f.Path), StringComparer.Ordinal).ToArray() };
                string Remap(string path) => PathSafety.IsUnder(path, config.Target.Root)
                    ? PathSafety.Under(source, Path.GetRelativePath(config.Target.Root, path)) : path;
                var effective = config with
                {
                    Target = config.Target with { Root = source }, RulesDirectory = null,
                    Rulesets = config.Rulesets.Select(p => externalRules.TryGetValue(p, out var frozen) ? frozen : Remap(p)).ToArray(),
                    Build = config.Build with
                    {
                        AssemblyManifest = config.Build.AssemblyManifest is { } manifest ? Remap(manifest) : null,
                        AssemblyPaths = config.Build.AssemblyPaths.Select(Remap).ToArray()
                    },
                    Output = config.Output with { Directory = Path.Combine(directory, name, "reports") }
                };
                var policy = Fingerprints(effective);
                var outcome = await analysis.RunAsync(effective, effective.Rulesets.Length > 0 ? "verify" : "analyze", token);
                ReportWriter.Save(effective, outcome.Report); codes.Add(outcome.ExitCode);
                return (outcome.Report, identity, policy);
            }
            catch (OperationCanceledException) { throw; }
            catch (SourceChangedException) { limits.Add("source-changed-during-comparison"); codes.Add(4); return (null, identity, new()); }
            catch (Exception error) when (error is ConfigurationException or IOException or UnauthorizedAccessException or InvalidDataException or SourceChangedException)
            { errors.Add(name + ": " + error.Message); codes.Add(3); return (null, identity, new()); }
        }
        try
        {
            (baseline, baseIdentity, basePolicy) = await Side(true);
            (target, targetIdentity, targetPolicy) = await Side(false);
            if (before is not null)
            {
                InputCapture.VerifyUnchanged(config.Target.Root, before, token);
                if (await GitSnapshots.Resolve(repository, "HEAD", home, token) != baseCommit) throw new SourceChangedException();
            }
            foreach (var rule in externalRules)
                if (AssemblyArtifacts.FileHash(rule.Key) != AssemblyArtifacts.FileHash(rule.Value)) throw new SourceChangedException();
        }
        catch (OperationCanceledException) { codes.Add(130); limits.Add("User cancelled comparison."); }
        catch (SourceChangedException) { codes.Add(4); limits.Add("source-changed-during-comparison"); }
        return Compare(baseline, target, baseIdentity, targetIdentity, basePolicy, targetPolicy,
            new(id, started, clock.ElapsedMilliseconds, config.Target.Root), errors.ToArray(), limits.ToArray(), codes.ToArray());
    }

    public static Dictionary<string, string> Fingerprints(RunConfiguration config)
    {
        var bundle = RuleLoader.Compose(config.Rulesets.Select(RuleLoader.Load));
        return bundle.Rules.ToDictionary(r => r.Id, r => ContentHash.Text(JsonSerializer.Serialize(r, JsonContract.Options) + "\n" +
            JsonSerializer.Serialize(bundle.Documents.SelectMany(d => d.Ruleset.Exceptions).Where(e => e.RuleId == r.Id).OrderBy(e => e.Id), JsonContract.Options)), StringComparer.Ordinal);
    }

    public static ComparisonOutcome Compare(AnalysisReport? baseline, AnalysisReport? target, SnapshotIdentity baselineIdentity,
        SnapshotIdentity targetIdentity, Dictionary<string, string> basePolicy, Dictionary<string, string> targetPolicy,
        RunMetadata metadata, string[] errors, string[] limitations, int[] codes)
    {
        var changes = basePolicy.Keys.Union(targetPolicy.Keys).Order(StringComparer.Ordinal).Where(k =>
            !basePolicy.TryGetValue(k, out var a) || !targetPolicy.TryGetValue(k, out var b) || a != b)
            .Select(k => new PolicyChange(k, !basePolicy.ContainsKey(k) ? "added" : !targetPolicy.ContainsKey(k) ? "removed" : "modified")).ToArray();
        var limits = limitations.ToList();
        var added = new List<Finding>(); var existing = new List<Finding>(); var resolved = new List<Finding>(); var unclassified = new List<Finding>();
        var safe = baseline is not null && target is not null && errors.Length == 0 && !codes.Contains(130) && limitations.Length == 0;
        var contextEqual = baseline is not null && target is not null && baseline.BuildContext == target.BuildContext &&
            JsonSerializer.Serialize(baseline.EngineVersions.OrderBy(p => p.Key)) == JsonSerializer.Serialize(target.EngineVersions.OrderBy(p => p.Key));
        if (!contextEqual) limits.Add("Build/engine context differs or a side is missing; disappearance is not evidence of resolution.");
        var baseFindings = (baseline?.Findings ?? []).Where(f => f.ExceptionId is null).ToDictionary(f => f.Id);
        var targetFindings = (target?.Findings ?? []).Where(f => f.ExceptionId is null).ToDictionary(f => f.Id);
        bool Comparable(string rule) => safe && contextEqual && basePolicy.TryGetValue(rule, out var a) && targetPolicy.TryGetValue(rule, out var b) && a == b &&
            Complete(baseline!, rule) && Complete(target!, rule);
        foreach (var finding in targetFindings.Values.OrderBy(f => f.Id))
            if (!Comparable(finding.RuleId)) unclassified.Add(finding);
            else if (baseFindings.ContainsKey(finding.Id)) existing.Add(finding);
            else added.Add(finding);
        foreach (var finding in baseFindings.Values.Where(f => !targetFindings.ContainsKey(f.Id)).OrderBy(f => f.Id))
            if (Comparable(finding.RuleId)) resolved.Add(finding); else unclassified.Add(finding);
        if (changes.Length > 0) limits.Add("policy-changed: changed/deleted rules or exceptions are not resolutions.");
        if (unclassified.Count > 0 || !safe || baseline?.Execution != "completed" || target?.Execution != "completed")
            limits.Add("Some evidence is not comparable; consult both full reports.");
        var status = codes.Contains(130) ? "cancelled" : limits.Count > 0 || errors.Length > 0 || !contextEqual ? "inconclusive" : "completed";
        var report = new ComparisonReport
        {
            Status = status, BaselineIdentity = baselineIdentity, TargetIdentity = targetIdentity, Baseline = baseline, Target = target,
            Added = added.ToArray(), Existing = existing.ToArray(), Resolved = resolved.ToArray(), Unclassified = unclassified.DistinctBy(f => f.Id).ToArray(),
            PolicyChanges = changes, FileChanges = Files(baselineIdentity.Inputs, targetIdentity.Inputs),
            ExecutionErrors = [.. errors, .. baseline?.ExecutionErrors ?? [], .. target?.ExecutionErrors ?? []], Limitations = limits.Distinct().ToArray(), RunMetadata = metadata
        };
        return new(report, codes.Contains(130) ? 130 : errors.Length > 0 || codes.Contains(3) ? 3 : status == "inconclusive" || codes.Contains(4) ? 4 : 0);
    }

    private static bool Complete(AnalysisReport report, string rule) => report.ExecutionErrors.Length == 0 &&
        report.Execution is not ("failed" or "cancelled") && !report.Limitations.Contains("source-changed-during-analysis") &&
        report.RuleResults.Any(r => r.RuleId == rule && r.Status is "pass" or "violation" && r.Limitations.Length == 0);

    private static FileChange[] Files(InputIdentity before, InputIdentity after)
    {
        var a = before.Files.ToDictionary(f => f.Path); var b = after.Files.ToDictionary(f => f.Path);
        var removed = a.Values.Where(f => !b.ContainsKey(f.Path)).ToList(); var changes = new List<FileChange>();
        foreach (var file in b.Values.OrderBy(f => f.Path))
        {
            if (a.TryGetValue(file.Path, out var old)) { if (old.Sha256 != file.Sha256) changes.Add(new(file.Path, "modified")); }
            else
            {
                var matches = removed.Where(f => f.Sha256 == file.Sha256).ToArray();
                if (matches.Length == 1 && b.Values.Count(f => !a.ContainsKey(f.Path) && f.Sha256 == file.Sha256) == 1)
                { changes.Add(new(file.Path, "renamed", matches[0].Path)); removed.Remove(matches[0]); }
                else changes.Add(new(file.Path, "added"));
            }
        }
        changes.AddRange(removed.Select(f => new FileChange(f.Path, "deleted")));
        return changes.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();
    }
}
