using System.Diagnostics;
using ArchSift.Contracts;

namespace ArchSift.Core;

public sealed record RunOutcome(AnalysisReport Report, int ExitCode);

public sealed class AnalysisService(
    Func<AssemblyInputs, RuleBundle, string, CancellationToken, Task<AssemblyEvaluation>>? assemblyEvaluator = null)
{
    public async Task<RunOutcome> RunAsync(RunConfiguration config, string operation, CancellationToken token = default)
    {
        if (operation is not ("analyze" or "verify")) throw new ConfigurationException("Unsupported analysis operation.");
        PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
        if (config.RulesDirectory is { } rulesDirectory) PathSafety.EnsureDisjoint(config.Target.Root, rulesDirectory);
        var bundle = operation == "verify"
            ? RuleLoader.Compose(config.Rulesets.Select(RuleLoader.Load)) : new RuleBundle([]);
        if (operation == "verify" && bundle.Documents.Length == 0) throw new ConfigurationException("verify requires at least one ruleset.");
        var stopwatch = Stopwatch.StartNew();
        var started = DateTimeOffset.UtcNow.ToString("O"); var runId = Guid.NewGuid().ToString("N");
        ProjectSnapshot? snapshot = null;
        var results = new List<RuleResult>(); var findings = new List<Finding>(); var errors = new List<string>(); var limits = new List<string>();
        var binding = "none"; var assemblyCount = 0; string? sdk = null;
        var framework = config.Build.TargetFramework;
        var engines = new Dictionary<string, string> { ["core"] = ToolIdentity.Version };
        var execution = "completed"; AssemblyInputs? assemblyInputs = null;
        try
        {
            token.ThrowIfCancellationRequested();
            snapshot = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, framework, token);
            limits.AddRange(snapshot.Scope.UnsupportedConstructs);
            if (snapshot.Projects.Length == 0) limits.Add("No supported project files found.");
            var projectRules = ProjectRuleEvaluator.Evaluate(snapshot, bundle);
            results.AddRange(projectRules.Results); findings.AddRange(projectRules.Findings);
            var needsAssembly = bundle.Rules.Any(r => r.Enabled && (r.Type == "type-dependency" ||
                r.Type == "naming" && r.Parameters.GetProperty("subjectKind").GetString() is "type" or "assembly"));
            if (needsAssembly || config.Build.Mode == "isolated" || config.Build.AssemblyManifest is not null || config.Build.AssemblyPaths.Length > 0)
            {
                try
                {
                    assemblyInputs = config.Build.Mode == "isolated"
                        ? await IsolatedBuild.BuildAsync(config, snapshot, token)
                        : AssemblyArtifacts.Existing(config, snapshot);
                    binding = assemblyInputs.SourceBound ? "source-bound" : "assemblies-only";
                    sdk = assemblyInputs.Sdk; framework = assemblyInputs.TargetFramework ?? framework;
                    assemblyCount = assemblyInputs.Paths.Length;
                    limits.AddRange(assemblyInputs.Limitations);
                    if (needsAssembly)
                    {
                        if (assemblyEvaluator is null) throw new InvalidOperationException("Assembly worker is unavailable.");
                        var evaluated = await assemblyEvaluator(assemblyInputs, bundle, config.Output.Directory, token);
                        results.AddRange(evaluated.Results); findings.AddRange(evaluated.Findings);
                        errors.AddRange(evaluated.Errors); assemblyCount = evaluated.AssemblyCount;
                        foreach (var version in evaluated.EngineVersions) engines[version.Key] = version.Value;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) when (error is IOException or InvalidDataException or BadImageFormatException or
                    ConfigurationException or InvalidOperationException or TimeoutException)
                {
                    errors.Add(error.Message);
                    foreach (var rule in bundle.Rules.Where(r => r.Enabled && !results.Any(s => s.RuleId == r.Id)))
                        results.Add(new(rule.Id, "inconclusive", 0, [], [error.Message]));
                }
            }
            InputCapture.VerifyUnchanged(config.Target.Root, snapshot.InputIdentity, token);
            foreach (var document in bundle.Documents)
                if (AssemblyArtifacts.FileHash(document.Identity.Path) != document.Identity.Sha256) throw new SourceChangedException();
            if (assemblyInputs is not null)
                foreach (var path in assemblyInputs.Paths)
                    if (AssemblyArtifacts.FileHash(path) != assemblyInputs.Sha256[path]) throw new SourceChangedException();
        }
        catch (OperationCanceledException)
        {
            execution = "cancelled"; limits.Add("User cancelled this run.");
        }
        catch (SourceChangedException)
        {
            execution = "partial"; limits.Add("source-changed-during-analysis");
            for (var i = 0; i < results.Count; i++)
                results[i] = results[i] with { Status = "inconclusive", Limitations = [.. results[i].Limitations, "source-changed-during-analysis"] };
            binding = "none";
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            errors.Add(error.Message); execution = snapshot is null ? "failed" : "partial";
        }
        if (execution != "cancelled" && (errors.Count > 0 || limits.Count > 0 || results.Any(r => r.Status is "inconclusive" or "error")))
            execution = snapshot is null ? "failed" : "partial";
        foreach (var rule in bundle.Rules.Where(r => r.Enabled && !results.Any(s => s.RuleId == r.Id)))
            results.Add(new(rule.Id, "inconclusive", 0, [], ["This run did not complete the check."]));
        var compliance = operation == "analyze" ? null
            : execution == "cancelled" || limits.Contains("source-changed-during-analysis") ? "inconclusive"
            : results.Any(r => r.Status == "violation") ? "noncompliant"
            : errors.Count > 0 || results.Any(r => r.Status is "inconclusive" or "error") ? "inconclusive"
            : results.Count == 0 || results.All(r => r.Status == "not-applicable") ? "not-applicable" : "compliant";
        var empty = new InputIdentity(ContentHash.Text(""), []);
        var sourceIdentity = snapshot?.InputIdentity ?? empty;
        var evidenceFiles = assemblyInputs?.Paths.Select(p => new InputFile("@assembly/" + Path.GetFileName(p),
            assemblyInputs.Sha256[p], SafeLength(p), "evidence")).ToArray() ?? [];
        var policyFiles = bundle.Documents.Select(d => new InputFile("@rules/" + d.Ruleset.Id + "/" + Path.GetFileName(d.Identity.Path),
            d.Identity.Sha256, SafeLength(d.Identity.Path), "analysis")).ToArray();
        var runIdentity = new InputIdentity(ContentHash.Text(sourceIdentity.Sha256 + "\n" +
            string.Join("\n", policyFiles.Concat(evidenceFiles).Select(f => f.Path + ":" + f.Sha256)) +
            "\n" + framework + "\n" + config.Build.Configuration + "\n" + ToolIdentity.Version +
            "\n" + string.Join("\n", engines.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value))),
            [.. sourceIdentity.Files, .. policyFiles, .. evidenceFiles]);
        var report = new AnalysisReport
        {
            Operation = operation, InputIdentity = runIdentity,
            RulesetIdentities = bundle.Documents.Select(d => d.Identity).ToArray(),
            Scope = snapshot?.Scope ?? new([], [], [], [], ["No source snapshot was captured."]),
            BuildContext = new(config.Build.Mode, framework, config.Build.Configuration, sdk, binding),
            EngineVersions = engines, Execution = execution, Compliance = compliance,
            RuleResults = results.OrderBy(r => r.RuleId, StringComparer.Ordinal).ToArray(),
            Findings = findings.DistinctBy(f => f.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToArray(),
            Coverage = new(snapshot?.Projects.Length ?? 0, assemblyCount, binding == "source-bound"),
            Limitations = limits.Distinct().Order(StringComparer.Ordinal).ToArray(), ExecutionErrors = errors.ToArray(),
            RunMetadata = new(runId, started, stopwatch.ElapsedMilliseconds, config.Target.Root)
        };
        var code = execution == "cancelled" ? 130 : errors.Count > 0 ? 3
            : execution is "partial" or "failed" || compliance == "inconclusive" ? 4 : 0;
        return new(report, code);
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; } catch (IOException) { return 0; }
    }
}
