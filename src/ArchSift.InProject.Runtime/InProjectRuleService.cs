using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public static class InProjectRuleService
{
    public static async Task<InProjectGateResult> RunAsync(string targetRoot, CancellationToken token = default)
    {
        VerifyTestPayload(InProjectConfiguration.Load(targetRoot));
        var built = await InProjectDailyBuild.RunAsync(targetRoot, token);
        var config = InProjectConfiguration.Load(targetRoot);
        var plan = InProjectBuildPlanner.Create(config, token);
        var report = Path.GetDirectoryName(built.CapturePath)!;
        var runDirectory = Path.Combine(config.InstallationRoot, "state", "build", built.RunId);
        var snapshot = new InProjectSnapshot(built.RunId, runDirectory,
            Path.Combine(runDirectory, "source"), report, plan.Snapshot.InputIdentity);
        var binding = JsonSerializer.Deserialize<InProjectDailyBinding>(File.ReadAllBytes(built.CapturePath),
            JsonContract.Options) ?? throw new ConfigurationException("Daily binding is missing.");
        var verdict = JsonSerializer.Deserialize<InProjectBindingVerdict>(File.ReadAllBytes(built.VerdictPath),
            JsonContract.Options) ?? throw new ConfigurationException("Binding verdict is missing.");
        var bundle = RuleLoader.Compose(config.Rules);
        VerifyTestPayload(config);
        var enabled = config.Rules.SelectMany(document => document.Ruleset.Rules.Where(rule => rule.Enabled)
            .Select(rule => (Key: document.Ruleset.Id + "@" + document.Ruleset.Version + "/" + rule.Id,
                Rule: rule))).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        if (enabled.Length == 0) throw new ConfigurationException("InProject gate has no enabled rules.");
        var byId = enabled.ToDictionary(item => item.Rule.Id, item => item.Key, StringComparer.Ordinal);
        var bRules = enabled.Where(item => IsAssemblyRule(item.Rule)).ToArray();
        var limitations = new List<string>();
        var coverage = new List<InProjectRuleCoverage>();
        var findings = new List<Finding>();
        var baseline = InProjectHostIdentity.Capture();
        var projectResults = ProjectRuleEvaluator.Evaluate(plan.Snapshot, bundle, token);
        findings.AddRange(projectResults.Findings);
        foreach (var result in projectResults.Results)
        {
            var key = byId[result.RuleId];
            coverage.Add(new(key, "A", [], 0, 1, 1, result.Status, result.Matched,
                result.FindingIds, result.Limitations));
        }
        if (bRules.Length > 0)
        {
            var workerPath = Path.Combine(config.InstallationRoot, "runtime", "archsift.dll");
            PathSafety.EnsureNoLinks(workerPath);
            if (!File.Exists(workerPath)) throw new ConfigurationException("Pinned assembly Worker is missing.");
            var inputs = WorkerInputs(binding, verdict, report);
            var worker = await new AssemblyWorkerClient(workerPath).EvaluateAsync(inputs, bundle, report, token);
            if (InProjectHostIdentity.Capture().Sha256 != baseline.Sha256)
                throw new InvalidOperationException("Host environment/Profile drift after Worker; evidence preserved.");
            WriteJson(Path.Combine(report, "worker-evaluation.json"), worker);
            findings.AddRange(worker.Findings);
            limitations.AddRange(worker.Errors);
            var observations = bRules.Select(item =>
            {
                var results = worker.Results.Where(result => result.RuleId == item.Rule.Id).ToArray();
                if (results.Length != 1)
                    return new InProjectRuleObservation(item.Key, "inconclusive", 0, [],
                        ["Worker omitted or duplicated a required assembly rule."]);
                var result = results[0];
                return new InProjectRuleObservation(item.Key, result.Status, result.Matched,
                    result.FindingIds, result.Limitations);
            }).ToArray();
            var materialPath = Path.Combine(report, "worker-results.json");
            WriteJson(materialPath, observations);
            var envelope = new InProjectTestEnvelope(1, built.RunId,
                LocalPackage.VerifyInstalled(config.Root), config.RulesSha256, binding.InputSha256,
                ContentHash.Text(string.Join("\n", binding.Contexts.Select(context =>
                    context.Project + "\0" + context.TargetFramework + "\0" + context.Configuration +
                    "\0" + context.OutputSha256))), bRules.Select(item => item.Key).ToArray(),
                "worker-results.json", LocalPackage.HashFile(materialPath), LocalPackage.HashFile(workerPath),
                "test-receipts");
            var envelopePath = Path.Combine(report, "test-envelope.json");
            WriteJson(envelopePath, envelope);
            var tested = await ExecuteTests(config, snapshot, envelopePath, envelope, observations,
                baseline, token);
            coverage.AddRange(tested.Coverage);
            limitations.AddRange(tested.Limitations);
        }
        var current = InProjectConfiguration.Load(config.Root);
        VerifyTestPayload(config);
        var fresh = binding.EndInputSha256 == InputCapture.Capture(config.Root, token).Sha256 &&
            current.RulesSha256 == config.RulesSha256 && current.ToolSha256 == config.ToolSha256 &&
            verdict.SourceBound && verdict.RunId == built.RunId &&
            verdict.CaptureSha256 == LocalPackage.HashFile(built.CapturePath);
        if (InProjectHostIdentity.Capture().Sha256 != baseline.Sha256)
            throw new InvalidOperationException("Host environment/Profile drift after rules; evidence preserved.");
        var ordered = coverage.OrderBy(item => item.RuleKey, StringComparer.Ordinal).ToArray();
        var required = enabled.Select(item => item.Key).ToArray();
        var executed = ordered.Where(item => item.Finished == 1).Select(item => item.RuleKey).ToArray();
        var complete = limitations.Count == 0 && ordered.Length == required.Length &&
            required.ToHashSet(StringComparer.Ordinal).SetEquals(executed) &&
            ordered.All(item => item.Classification == "A" || item.Discovered == 1);
        var gateCoverage = new InProjectGateCoverage(complete, required, executed);
        var allowEmpty = enabled.Where(item => item.Rule.Scope.AllowEmpty ||
                item.Rule.Type == "type-dependency" &&
                item.Rule.Parameters.GetProperty("source").Deserialize<Selector>(JsonContract.Options)!.AllowEmpty)
            .Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var decision = InProjectGatePolicy.Decide("completed", verdict, gateCoverage, ordered, allowEmpty, fresh);
        var resultReport = new InProjectGateResult(1, "ArchSift.InProject", built.RunId,
            binding.WorktreeId, "completed", verdict, "daily", gateCoverage, ordered,
            findings.DistinctBy(item => item.Id).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            limitations.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            decision, new("available", "daily-binding.json"), fresh ? "current" : "stale");
        WriteJson(Path.Combine(report, "gate-result.json"), resultReport);
        return resultReport;
    }

    private static bool IsAssemblyRule(Rule rule) => rule.Type == "type-dependency" ||
        rule.Type == "naming" && rule.Parameters.GetProperty("subjectKind").GetString() is "type" or "assembly";

    private static void VerifyTestPayload(InProjectLoadedConfiguration config)
    {
        var manifestPath = Path.Combine(config.InstallationRoot, "runtime", "payload-manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        var listed = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Where(item => item.GetProperty("path").GetString()!.StartsWith("tests/", StringComparison.Ordinal))
            .ToArray();
        if (!listed.Any(item => item.GetProperty("path").GetString() == "tests/ArchitectureRules.cs"))
            throw new ConfigurationException("Pinned package has no dedicated architecture test source.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in listed)
        {
            var relative = item.GetProperty("path").GetString()!;
            if (!names.Add(relative) || !relative.StartsWith("tests/", StringComparison.Ordinal) ||
                relative.Split('/').Any(part => part is "" or "." or ".."))
                throw new ConfigurationException("Invalid dedicated test payload path.");
            var path = Path.Combine(config.InstallationRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            PathSafety.EnsureNoLinks(path);
            if (!File.Exists(path) || new FileInfo(path).Length != item.GetProperty("bytes").GetInt64() ||
                LocalPackage.HashFile(path) != item.GetProperty("sha256").GetString())
                throw new ConfigurationException("Pinned dedicated test payload changed: " + relative);
        }
        var testDirectory = Path.Combine(config.InstallationRoot, "tests");
        if (!Directory.Exists(testDirectory))
            throw new ConfigurationException("Pinned dedicated architecture test project is missing.");
        foreach (var path in Directory.EnumerateFiles(testDirectory, "*.cs", SearchOption.TopDirectoryOnly))
            if (!names.Contains("tests/" + Path.GetFileName(path)))
                throw new ConfigurationException("Unlisted dedicated architecture test source.");
    }

    private static AssemblyInputs WorkerInputs(InProjectDailyBinding binding, InProjectBindingVerdict verdict,
        string report)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var contexts = new List<AssemblyAnalysisContext>();
        foreach (var captured in binding.Contexts)
        {
            var references = captured.References.Where(reference => reference.Role == "compile").ToArray();
            var framework = references.Where(reference => reference.Path.Replace('\\', '/').Contains(
                "/packs/Microsoft.NETCore.App.Ref/", StringComparison.OrdinalIgnoreCase)).Select(reference => reference.Path).ToArray();
            var paths = new[] { captured.OutputPath }.Concat(references.Select(reference => reference.Path))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!hashes.TryAdd(captured.OutputPath, captured.OutputSha256) &&
                hashes[captured.OutputPath] != captured.OutputSha256)
                throw new ConfigurationException("One output path has conflicting context bytes.");
            foreach (var reference in references)
                if (!hashes.TryAdd(reference.Path, reference.Sha256) && hashes[reference.Path] != reference.Sha256)
                    throw new ConfigurationException("One reference path has conflicting context bytes.");
            var id = ContentHash.Text(captured.Project + "\0" + captured.TargetFramework + "\0" +
                captured.Configuration)[..16];
            contexts.Add(new(id, captured.Project, captured.OutputPath, paths) { FrameworkPaths = framework });
        }
        return new(hashes.Keys.ToArray(), hashes, verdict.SourceBound, true, [],
            binding.SdkVersion, null, binding.Configuration)
        { Contexts = contexts.ToArray(), EvidenceOrigin = report };
    }

    private static async Task<(InProjectRuleCoverage[] Coverage, string[] Limitations)> ExecuteTests(
        InProjectLoadedConfiguration config, InProjectSnapshot snapshot, string envelopePath,
        InProjectTestEnvelope envelope, InProjectRuleObservation[] observations,
        InProjectHostState baseline, CancellationToken token)
    {
        var testProject = Path.Combine(config.InstallationRoot, "tests",
            "ArchSift.InProject.ArchitectureTests.csproj");
        PathSafety.EnsureNoLinks(testProject);
        var testDirectory = Path.GetDirectoryName(testProject)!;
        var cache = Path.Combine(config.InstallationRoot, "state", "cache", "packages");
        var nuget = Path.Combine(snapshot.RunDirectory, "NuGet.Config");
        var envelopeSha = LocalPackage.HashFile(envelopePath);
        var process = new InProjectBuildProcess(snapshot, baseline, cache, token);
        var child = new Dictionary<string, string> { ["ARCHSIFT_INPROJECT_TEST_ENVELOPE"] = envelopePath };
        await process.DotnetResultAsync("test-restore", testDirectory,
            ["restore", testProject, "--configfile", nuget, "--source", config.LocalFeed,
             "--packages", cache, "--locked-mode", "--disable-parallel", "--disable-build-servers",
             "-p:NuGetAudit=false", "--verbosity", "minimal"], child);
        await process.DotnetResultAsync("test-build", testDirectory,
            ["build", testProject, "--no-restore", "--disable-build-servers",
             "-p:UseSharedCompilation=false", "-c", "Release", "--verbosity", "minimal"], child);
        var discovery = await process.DotnetResultAsync("test-discovery", testDirectory,
            ["test", testProject, "--no-build", "--no-restore", "-c", "Release",
             "--list-tests", "--verbosity", "minimal"], child);
        var execution = await process.DotnetResultAsync("test-execution", testDirectory,
            ["test", testProject, "--no-build", "--no-restore", "-c", "Release",
             "--logger", "trx;LogFileName=architecture.trx", "--results-directory", snapshot.ReportDirectory,
             "--verbosity", "minimal"], child, allowNonzero: true);
        var trxPath = Path.Combine(snapshot.ReportDirectory, "architecture.trx");
        if (!File.Exists(trxPath)) throw new IOException("Dedicated xUnit execution produced no TRX result.");
        var trx = XDocument.Load(trxPath);
        var results = trx.Descendants().Where(element => element.Name.LocalName == "UnitTestResult").ToArray();
        var limits = new List<string>();
        if (LocalPackage.HashFile(envelopePath) != envelopeSha)
            limits.Add("Architecture test envelope changed during dedicated xUnit execution.");
        if (LocalPackage.HashFile(Path.Combine(snapshot.ReportDirectory, envelope.ResultMaterialPath)) !=
            envelope.ResultMaterialSha256)
            limits.Add("Worker rule result material changed during dedicated xUnit execution.");
        if (execution.ExitCode is not (0 or 1)) limits.Add("Dedicated xUnit runner exited unexpectedly.");
        var receiptDirectory = Path.Combine(snapshot.ReportDirectory, envelope.ReceiptDirectory);
        var coverage = new List<InProjectRuleCoverage>();
        foreach (var observation in observations)
        {
            var discovered = discovery.Output.Split('\n').Count(line =>
                line.Contains(observation.RuleKey, StringComparison.Ordinal));
            var matches = results.Where(element => ((string?)element.Attribute("testName"))?
                .Contains(observation.RuleKey, StringComparison.Ordinal) == true).ToArray();
            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(observation.RuleKey)))
                .ToLowerInvariant() + ".json";
            var receiptPath = Path.Combine(receiptDirectory, name);
            InProjectTestReceipt? receipt = null;
            if (File.Exists(receiptPath))
                receipt = JsonSerializer.Deserialize<InProjectTestReceipt>(File.ReadAllBytes(receiptPath),
                    JsonContract.Options);
            var expectedOutcome = observation.Status is "pass" or "not-applicable" ? "Passed" : "Failed";
            var okay = discovered == 1 && matches.Length == 1 &&
                (string?)matches[0].Attribute("outcome") == expectedOutcome &&
                receipt is { Started: 1, Finished: 1 } && receipt.RunId == envelope.RunId &&
                receipt.RuleKey == observation.RuleKey && receipt.Status == observation.Status &&
                receipt.ResultMaterialSha256 == envelope.ResultMaterialSha256;
            if (!okay) limits.Add("Dedicated xUnit coverage or outcome differs for " + observation.RuleKey);
            coverage.Add(new(observation.RuleKey, "B", receipt is null ? [] : [receipt.TestId],
                okay ? 1 : 0, receipt?.Started ?? 0, receipt?.Finished ?? 0,
                observation.Status, observation.Matched, observation.FindingIds,
                observation.Limitations.Concat(okay ? [] : ["xUnit discovery/execution mismatch."]).ToArray()));
        }
        return (coverage.ToArray(), limits.ToArray());
    }

    private static void WriteJson<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonContract.Options) + "\n", new UTF8Encoding(false));
}
