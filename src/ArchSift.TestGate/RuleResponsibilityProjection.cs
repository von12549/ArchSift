using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Testing;

namespace ArchSift.TestGate;

public sealed record RuleResponsibilityItem(string Id, string Category, string PolicyState,
    string Responsibility, string ExecutionResult, string[] CaseKeys, string[] DecisionReferences);
public sealed record RuleResponsibilityView(int SchemaVersion, string Kind, string Semantics,
    RuleResponsibilityItem[] Items);

/// <summary>Display projection only. Never participates in policy selection or gate evaluation.</summary>
public static class RuleResponsibilityProjection
{
    public static RuleResponsibilityView Create(TestGateConfig config, IEnumerable<string> includedRuleIds,
        MtpTest[]? tests = null, bool executionComplete = false)
    {
        var included = includedRuleIds.ToHashSet(StringComparer.Ordinal);
        using var inventory = Resource("PolicyInventory");
        using var dispositions = Resource("RuleDispositions");
        var sources = ApprovedSources(config);
        var actual = (tests ?? []).Where(test => test.Identity is not null)
            .GroupBy(test => CaseIdentities.Key(test.Identity!.Identity), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var items = inventory.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => !included.Contains(item.GetProperty("id").GetString()!))
            .Select(item =>
            {
                var id = item.GetProperty("id").GetString()!;
                var category = item.GetProperty("category").GetString()!;
                var source = sources.SingleOrDefault(source => source.SourceId == id);
                if (source is { Responsibility: "ifx-csharp", CaseKeys.Length: > 0 })
                    return new RuleResponsibilityItem(id, category, "adopted-active", "ifx-csharp",
                        Execution(source.CaseKeys, actual, executionComplete), source.CaseKeys,
                        ["native-policy:" + config.PolicySha256]);

                // A historical candidate flag is never a current adoption decision.
                var sourceHash = inventory.RootElement.GetProperty("sources").EnumerateArray()
                    .Single(entry => entry.GetProperty("name").GetString() == item.GetProperty("source").GetString())
                    .GetProperty("sha256").GetString();
                var pending = dispositions.RootElement.GetProperty("decisions").EnumerateArray()
                    .Where(decision => decision.GetProperty("inventorySourceSha256").GetString() == sourceHash &&
                        decision.GetProperty("ruleIds").EnumerateArray().Any(rule => rule.GetString() == id))
                    .ToArray();
                if (pending.Length == 1)
                    return new RuleResponsibilityItem(id, category, "adopted-pending-activation", "not-assigned",
                        "not-run", [], [pending[0].GetProperty("reference").GetString()! + "#" +
                            pending[0].GetProperty("decisionId").GetString()! + "@" + pending[0].GetProperty("sha256").GetString()!]);
                return new RuleResponsibilityItem(id, category, "unknown", "unknown", "not-run", [], []);
            }).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        return new(1, "rule-responsibility-projection", "not-executed-as-json", items);
    }

    private static PolicySource[] ApprovedSources(TestGateConfig config)
    {
        if (config.PolicyFile is null || config.PolicySha256 is null) return [];
        try
        {
            var policy = NativePolicy.Load(config.TargetRoot, config.PolicyFile);
            return policy.Sha256 == config.PolicySha256 ? policy.Document.Sources : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException
            or ConfigurationException or InvalidDataException) { return []; }
    }

    private static string Execution(string[] keys, Dictionary<string, MtpTest[]> actual, bool complete)
    {
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length) return "unavailable";
        var matches = keys.Select(key => actual.GetValueOrDefault(key) ?? []).ToArray();
        if (matches.Any(group => group.Length > 1)) return "unavailable";
        if (matches.Any(group => group.Length == 0)) return "not-run";
        var tests = matches.Select(group => group[0]).ToArray();
        if (tests.Any(test => test.Identity!.ExecutionId != test.Id)) return "unavailable";
        if (tests.Any(test => test.Result == "failed")) return "failed";
        if (tests.Any(test => test.Result == "skipped")) return "skipped";
        return complete && tests.All(test => test.Result == "passed") ? "passed" : "unavailable";
    }

    public static void ValidateReport(JsonElement report)
    {
        if (!report.TryGetProperty("responsibilityProjection", out var projection)) return; // Original v1 report.
        if (projection.ValueKind != JsonValueKind.Object ||
            !projection.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var number) || number != 1 ||
            !projection.TryGetProperty("kind", out var kind) || kind.GetString() != "rule-responsibility-projection" ||
            !projection.TryGetProperty("semantics", out var semantics) || semantics.GetString() != "not-executed-as-json" ||
            !projection.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Unsupported rule responsibility projection.");
        var view = JsonSerializer.Deserialize<RuleResponsibilityView>(projection.GetRawText(), JsonContract.Options)
            ?? throw new InvalidDataException("Missing rule responsibility projection.");
        if (view.Items.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id) ||
            item.Category is not ("A" or "B") || item.PolicyState is not ("adopted-active" or "adopted-pending-activation" or "unknown") ||
            item.Responsibility is not ("ifx-csharp" or "not-assigned" or "unknown") ||
            item.ExecutionResult is not ("passed" or "failed" or "skipped" or "not-run" or "unavailable") ||
            item.CaseKeys is null || item.DecisionReferences is null) ||
            view.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != view.Items.Length ||
            !view.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal).SetEquals(
                report.GetProperty("notIncluded").EnumerateArray().Select(item => item.GetProperty("id").GetString()!)))
            throw new InvalidDataException("Malformed rule responsibility projection.");
    }

    private static JsonDocument Resource(string name)
    {
        using var stream = typeof(RuleResponsibilityProjection).Assembly.GetManifestResourceStream(
            "ArchSift.TestGate." + name + ".json") ?? throw new InvalidDataException("Projection resource is missing.");
        return JsonDocument.Parse(stream);
    }
}
