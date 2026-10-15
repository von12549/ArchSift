using System.Text.Json;

namespace ArchSift.TestGate;

/// <summary>Reads MTP discovery and xUnit v3's native CTRF result without using display names as case identities.</summary>
public static class MtpCtrf
{
    public static MtpRun Parse(byte[] discoveryBytes, byte[] reportBytes)
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
        var summary = results.GetProperty("summary");
        var rows = results.GetProperty("tests").EnumerateArray().ToArray();
        if (summary.GetProperty("tests").GetInt32() != rows.Length)
            throw new InvalidDataException("CTRF summary and case rows disagree.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        var tests = new List<MtpTest>(rows.Length);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
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
            var message = row.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;
            tests.Add(new MtpTest(id, id, Required(extra, "type"), Required(extra, "method"), result,
                marker.CaseId, marker.Sha256, message is null ? [] : [message])
            {
                Evaluation = marker.Evaluation,
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
