using System.Text.Json;

namespace ArchSift.TestGate;

public sealed record MtpTest(string Id, string CaseId, string ClassName, string MethodName,
    string Result, string? JsonCaseId, string? RulesetSha256, string[] Messages);

public sealed record MtpRun(bool DiscoveryFinished, bool AssemblyFinished, int DiscoveredCaseCount,
    MtpTest[] Tests, string[] StartedIds, string[] CompletedIds, string[] DuplicateIds,
    string[] MissingTerminalIds);

public sealed record GateCoverage(int ExitCode, bool Complete, string[] MissingOriginalIds,
    string[] MissingJsonCaseIds, string[] DuplicateIds, string[] Reasons);

public static class MtpEvents
{
    public static MtpRun Parse(IEnumerable<string> lines)
    {
        var cases = new Dictionary<string, (string Class, string Method)>(StringComparer.Ordinal);
        var starts = new Dictionary<string, string>(StringComparer.Ordinal);
        var results = new Dictionary<string, (string Result, string[] Messages)>(StringComparer.Ordinal);
        var markers = new Dictionary<string, (string CaseId, string Sha)>(StringComparer.Ordinal);
        var completed = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        var discoveryFinished = false;
        var assemblyFinished = false;
        var discovered = -1;
        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            using var document = JsonDocument.Parse(line);
            var item = document.RootElement;
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("$type", out var kind))
                throw new InvalidDataException("MTP reporter did not emit JSON event objects.");
            var type = kind.GetString();
            switch (type)
            {
                case "discovery-complete":
                    if (discoveryFinished) throw new InvalidDataException("Duplicate MTP discovery completion.");
                    discoveryFinished = true;
                    discovered = item.GetProperty("TestCasesToRun").GetInt32();
                    break;
                case "test-case-starting":
                    var caseId = Required(item, "TestCaseUniqueID");
                    if (!cases.TryAdd(caseId, (Required(item, "TestClassName"), Required(item, "TestMethodName"))))
                        duplicates.Add(caseId);
                    break;
                case "test-starting":
                    var id = Required(item, "TestUniqueID");
                    if (!starts.TryAdd(id, Required(item, "TestCaseUniqueID"))) duplicates.Add(id);
                    break;
                case "test-passed" or "test-failed" or "test-skipped" or "test-not-run":
                    var resultId = Required(item, "TestUniqueID");
                    var result = type[5..];
                    var messages = item.TryGetProperty("Messages", out var values) && values.ValueKind == JsonValueKind.Array
                        ? values.EnumerateArray().Select(value => value.GetString() ?? "").ToArray() : [];
                    if (!results.TryAdd(resultId, (result, messages))) duplicates.Add(resultId);
                    AddMarker(item, resultId);
                    break;
                case "test-output":
                    AddMarker(item, Required(item, "TestUniqueID"));
                    break;
                case "test-finished":
                    var finishedId = Required(item, "TestUniqueID");
                    if (!completed.Add(finishedId)) duplicates.Add(finishedId);
                    break;
                case "test-assembly-finished":
                    if (assemblyFinished) throw new InvalidDataException("Duplicate MTP assembly completion.");
                    assemblyFinished = true;
                    break;
            }
        }
        var tests = starts.Select(pair =>
        {
            var (className, methodName) = cases.TryGetValue(pair.Value, out var meta) ? meta : ("", "");
            var terminal = results.TryGetValue(pair.Key, out var value) ? value : ("not-run", Array.Empty<string>());
            var marker = markers.TryGetValue(pair.Key, out var named) ? named : (null, null);
            return new MtpTest(pair.Key, pair.Value, className, methodName, terminal.Item1,
                marker.Item1, marker.Item2, terminal.Item2);
        }).OrderBy(test => test.Id, StringComparer.Ordinal).ToArray();
        return new(discoveryFinished, assemblyFinished, discovered, tests,
            starts.Keys.Order(StringComparer.Ordinal).ToArray(), completed.Order(StringComparer.Ordinal).ToArray(),
            duplicates.Order(StringComparer.Ordinal).ToArray(),
            starts.Keys.Where(id => !results.ContainsKey(id) || !completed.Contains(id))
                .Order(StringComparer.Ordinal).ToArray());

        void AddMarker(JsonElement item, string id)
        {
            if (!item.TryGetProperty("Output", out var output) || output.ValueKind != JsonValueKind.String) return;
            foreach (var line in (output.GetString() ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                const string prefix = "ARCHSIFT_JSON_CASE:";
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var body = line[prefix.Length..];
                var split = body.LastIndexOf(':');
                if (split <= 0 || split == body.Length - 1) throw new InvalidDataException("Malformed JSON test marker.");
                var marker = (body[..split], body[(split + 1)..]);
                if (!markers.TryAdd(id, marker) && markers[id] != marker) duplicates.Add(id);
            }
        }
    }

    public static GateCoverage Assess(MtpRun run, IEnumerable<string> originalIds,
        IReadOnlyDictionary<string, string> jsonCases, int processExitCode, bool subset = false)
    {
        var original = originalIds.ToHashSet(StringComparer.Ordinal);
        var actualOriginal = run.Tests.Where(test => test.JsonCaseId is null).Select(test => test.Id).ToHashSet(StringComparer.Ordinal);
        var missingOriginal = original.Except(actualOriginal).Order(StringComparer.Ordinal).ToArray();
        var json = run.Tests.Where(test => test.JsonCaseId is not null).ToArray();
        var actualJson = json.Select(test => test.JsonCaseId!).ToHashSet(StringComparer.Ordinal);
        var missingJson = jsonCases.Keys.Except(actualJson).Order(StringComparer.Ordinal).ToArray();
        var duplicateJson = json.GroupBy(test => test.JsonCaseId!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key);
        var duplicates = run.DuplicateIds.Concat(duplicateJson).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var reasons = new List<string>();
        if (subset) reasons.Add("Filtered execution is a subset.");
        if (!run.DiscoveryFinished || !run.AssemblyFinished || run.DiscoveredCaseCount < 1)
            reasons.Add("MTP discovery or assembly completion is missing.");
        if (!subset && run.DiscoveredCaseCount != run.Tests.Select(test => test.CaseId).Distinct(StringComparer.Ordinal).Count())
            reasons.Add("MTP discovered case count does not match started case identities.");
        if (original.Count == 0 || jsonCases.Count == 0) reasons.Add("Required original or JSON case baseline is empty.");
        if (missingOriginal.Length > 0) reasons.Add("Original cases were not executed.");
        if (missingJson.Length > 0) reasons.Add("Enabled JSON cases were not executed.");
        if (duplicates.Length > 0) reasons.Add("Duplicate case identities or outcomes were observed.");
        if (run.MissingTerminalIds.Length > 0 || run.Tests.Any(test => test.Result is "skipped" or "not-run"))
            reasons.Add("Required cases were skipped or did not finish.");
        if (run.Tests.Any(test => test.ClassName.Length == 0 || test.MethodName.Length == 0))
            reasons.Add("MTP case metadata is incomplete.");
        if (json.Any(test => !jsonCases.TryGetValue(test.JsonCaseId!, out var sha) || sha != test.RulesetSha256))
            reasons.Add("JSON case identity or ruleset bytes do not match the enabled config.");
        var coverageComplete = reasons.Count == 0;
        if (run.Tests.Any(test => test.Result == "failed")) reasons.Add("An executed test assertion failed.");
        if (processExitCode != 0 && !run.Tests.Any(test => test.Result == "failed")) reasons.Add("Test process failed without a matching assertion outcome.");
        var processError = processExitCode != 0 && !run.Tests.Any(test => test.Result == "failed");
        var exit = processError ? 3 : !coverageComplete ? 4
            : run.Tests.Any(test => test.Result == "failed") ? 2 : 0;
        return new(exit, coverageComplete, missingOriginal, missingJson, duplicates, reasons.ToArray());
    }

    private static string Required(JsonElement item, string name) => item.GetProperty(name).GetString()
        is { Length: > 0 } value ? value : throw new InvalidDataException("MTP event is missing " + name + ".");
}
