using System.Text.Json;
using ArchSift.Testing;

namespace ArchSift.TestGate;

/// <summary>Reads MTP discovery and xUnit v3's native CTRF result without using display names as case identities.</summary>
public static class MtpCtrf
{
    public static MtpRun Parse(byte[] discoveryBytes, byte[] reportBytes, bool requireTypedAssociation = false)
    {
        using var discovery = JsonDocument.Parse(discoveryBytes);
        using var report = JsonDocument.Parse(reportBytes);
        var discovered = discovery.RootElement.GetProperty("tests").EnumerateArray().ToArray();
        var discoveryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in discovered)
            if (!discoveryIds.Add(Required(item, "uid")))
                throw new InvalidDataException("MTP discovery contains a duplicate UID.");

        var root = report.RootElement;
        if (Required(root, "reportFormat") != "CTRF")
            throw new InvalidDataException("Test result is not CTRF.");
        var results = root.GetProperty("results");
        if (requireTypedAssociation && (discovery.RootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
            results.GetProperty("tool").GetProperty("name").GetString() != "xUnit.net v3" ||
            results.GetProperty("tool").GetProperty("version").GetString() is not { } runnerVersion ||
            !(runnerVersion == "4.0.1" || runnerVersion.StartsWith("4.0.1+", StringComparison.Ordinal))))
            throw new InvalidDataException("Unsupported typed MTP/CTRF association contract.");
        var summary = results.GetProperty("summary");
        var rows = results.GetProperty("tests").EnumerateArray().ToArray();
        if (summary.GetProperty("tests").GetInt32() != rows.Length)
            throw new InvalidDataException("CTRF summary and case rows disagree.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        var tests = new List<MtpTest>(rows.Length);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var associatedUids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var extra = row.GetProperty("extra");
            var id = Required(extra, "id");
            if (!seen.Add(id)) duplicates.Add(id);
            var status = Required(row, "status");
            var result = status switch
            {
                "passed" => "passed",
                "failed" => "failed",
                "skipped" => "skipped",
                "pending" or "other" => "not-run",
                _ => throw new InvalidDataException("Unknown CTRF test status: " + status)
            };
            counts[result] = counts.GetValueOrDefault(result) + 1;
            var output = row.TryGetProperty("stdout", out var stdout) ? stdout.ValueKind switch
            {
                JsonValueKind.String => stdout.GetString() ?? "",
                JsonValueKind.Array => string.Join('\n', stdout.EnumerateArray().Select(item => item.GetString() ?? "")),
                JsonValueKind.Null => "",
                _ => throw new InvalidDataException("CTRF stdout has an unsupported shape.")
            } : "";
            var marker = JsonCaseOutput.Parse(output);
            var identity = CaseIdentities.Read(output);
            if (identity is not null && (identity.ExecutionId != id ||
                identity.Identity.Type != Required(extra, "type") || identity.Identity.Method != Required(extra, "method")))
                throw new InvalidDataException("Case projection does not match its CTRF execution row.");
            string? discoveryUid = null;
            if (requireTypedAssociation)
            {
                // Versioned xUnit 4.0.1 MTP adapter bridge: it publishes TestCase.UniqueID as its discovery UID.
                // This relates two domains for this run; neither is an execution ID or a portable identity.
                if (identity is null) throw new InvalidDataException("Typed association is missing.");
                discoveryUid = identity.XunitCaseId;
                var matches = discovered.Where(item => Required(item, "uid") == discoveryUid).ToArray();
                if (matches.Length != 1 || !associatedUids.Add(discoveryUid))
                    throw new InvalidDataException("Unknown or ambiguous current-run discovery association.");
                var type = matches[0].GetProperty("type");
                var ns = type.GetProperty("namespace").GetString();
                var fullType = string.IsNullOrEmpty(ns) ? Required(type, "typeName") : ns + "." + Required(type, "typeName");
                if (fullType != identity.Identity.Type || Required(type, "methodName") != identity.Identity.Method ||
                    Required(type, "assemblyFullName").Split(',')[0] != identity.Identity.Assembly)
                    throw new InvalidDataException("Discovery metadata differs from typed execution association.");
            }
            var message = row.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;
            tests.Add(new MtpTest(id, requireTypedAssociation ? identity!.XunitCaseId : id, Required(extra, "type"), Required(extra, "method"), result,
                marker.CaseId, marker.Sha256, message is null ? [] : [message])
            {
                Evaluation = marker.Evaluation,
                Identity = identity,
                DiscoveryUid = discoveryUid,
                ElapsedMilliseconds = row.TryGetProperty("duration", out var duration) &&
                    duration.ValueKind == JsonValueKind.Number ? duration.GetInt64() : null
            });
        }
        foreach (var (field, status) in new[] { ("passed", "passed"), ("failed", "failed"),
                     ("skipped", "skipped") })
            if (summary.GetProperty(field).GetInt32() != counts.GetValueOrDefault(status))
                throw new InvalidDataException("CTRF outcome totals disagree with case rows.");
        if (summary.GetProperty("pending").GetInt32() + summary.GetProperty("other").GetInt32()
            != counts.GetValueOrDefault("not-run"))
            throw new InvalidDataException("CTRF nonterminal totals disagree with case rows.");

        var sorted = tests.OrderBy(test => test.Id, StringComparer.Ordinal).ToArray();
        var ids = sorted.Select(test => test.Id).ToArray();
        return new MtpRun(true, true, discovered.Length, sorted, ids, ids,
            duplicates.Order(StringComparer.Ordinal).ToArray(), [])
        { DiscoveredIds = discoveryIds.Order(StringComparer.Ordinal).ToArray() };
    }

    private static string Required(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            value.GetString() is not { Length: > 0 } text)
            throw new InvalidDataException("MTP/CTRF is missing " + name + ".");
        return text;
    }
}
