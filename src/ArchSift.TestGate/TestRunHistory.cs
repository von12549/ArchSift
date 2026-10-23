using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.TestGate;

public sealed record TestRunMarker(int SchemaVersion, string Kind, string RunId, string TargetRoot,
    string StartedUtc, string State, string? Gate, int? ExitCode, string? ReportPath);

public sealed record TestRunHistoryItem(string RunId, string TargetRoot, string StartedUtc,
    string State, string? Gate, int? ExitCode, string? ReportPath, string? HtmlPath,
    string HistoryFreshness, string[] Reasons);

public sealed record TestRunHistoryPage(TestRunHistoryItem[] Items, int Total, int Offset, int Limit);

/// <summary>Small, read-only history over directories explicitly marked as daily test runs.</summary>
public static class TestRunHistory
{
    private const string MarkerName = "architecture-test-state.json";
    private const string ReportName = "architecture-test-run.json";
    private const string HtmlName = "architecture-test-run.html";
    private static readonly UTF8Encoding Utf8 = new(false);

    public static void Begin(string directory, string runId, string targetRoot, DateTimeOffset startedUtc)
    {
        var path = Marker(directory);
        var marker = new TestRunMarker(1, "architecture-test-state", runId, Path.GetFullPath(targetRoot),
            startedUtc.ToString("O"), "running", null, null, null);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, Utf8);
        writer.Write(JsonSerializer.Serialize(marker, JsonContract.Options));
    }

    public static void Finish(string directory, string runId, TestGateResult result)
    {
        var path = Marker(directory);
        var old = ReadMarker(path);
        if (old.RunId != runId) throw new InvalidDataException("Run marker identity changed.");
        var state = result.ExitCode == 130 ? "cancelled" : result.ExitCode == 0 ? "finished" : "failed";
        var next = old with { State = state, Gate = result.Gate, ExitCode = result.ExitCode,
            ReportPath = result.ReportPath };
        var temp = Path.Combine(Path.GetFullPath(directory), ".architecture-test-state-" + runId + ".tmp");
        PathSafety.EnsureNoLinks(temp);
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8))
                writer.Write(JsonSerializer.Serialize(next, JsonContract.Options));
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static TestRunHistoryPage List(string historyRoot, string? targetRoot = null,
        int offset = 0, int limit = 50, IEnumerable<string>? activeRunIds = null)
    {
        if (offset < 0 || limit is < 1 or > 200) throw new ConfigurationException("Invalid test history page.");
        var root = Path.GetFullPath(historyRoot);
        PathSafety.EnsureNoLinks(root);
        if (!Directory.Exists(root)) throw new ConfigurationException("Test history root is missing.");
        var active = (activeRunIds ?? []).ToHashSet(StringComparer.Ordinal);
        var target = targetRoot is null ? null : Path.GetFullPath(targetRoot);
        var items = new List<TestRunHistoryItem>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            TestRunMarker? marker = null;
            try
            {
                PathSafety.EnsureNoLinks(directory);
                var markerPath = Marker(directory);
                if (!File.Exists(markerPath)) continue;
                marker = ReadMarker(markerPath);
                if (target is not null && !string.Equals(target, marker.TargetRoot, PathSafety.Comparison)) continue;
                items.Add(ReadItem(directory, marker, active.Contains(marker.RunId)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException
                or InvalidDataException or ConfigurationException or KeyNotFoundException or InvalidOperationException)
            {
                if (marker is not null) items.Add(new(marker.RunId, marker.TargetRoot, marker.StartedUtc,
                    "unavailable", null, null, null, null, "unknown",
                    ["Run report or directory is unavailable."]));
                else if (target is null) items.Add(new(Path.GetFileName(directory), "unknown", "", "unavailable",
                    null, null, null, null, "unknown", ["Run marker or directory is unavailable."]));
            }
        }
        var ordered = items.OrderByDescending(item => item.StartedUtc, StringComparer.Ordinal)
            .ThenBy(item => item.RunId, StringComparer.Ordinal).ToArray();
        return new(ordered.Skip(offset).Take(limit).ToArray(), ordered.Length, offset, limit);
    }

    private static TestRunHistoryItem ReadItem(string directory, TestRunMarker marker, bool active)
    {
        var state = marker.State == "running" && !active ? "interrupted" : marker.State;
        var reportPath = Path.Combine(directory, ReportName);
        var htmlPath = Path.Combine(directory, HtmlName);
        if (!File.Exists(reportPath) || !File.Exists(htmlPath))
            return new(marker.RunId, marker.TargetRoot, marker.StartedUtc,
                state == "running" || state == "interrupted" ? state : "unavailable",
                null, marker.ExitCode, null, null, "unknown",
                ["Authoritative JSON or HTML report is missing."]);
        PathSafety.EnsureNoLinks(reportPath);
        PathSafety.EnsureNoLinks(htmlPath);
        using var document = JsonDocument.Parse(File.ReadAllBytes(reportPath));
        var report = document.RootElement;
        RuleResponsibilityProjection.ValidateReport(report);
        if (report.TryGetProperty("inputs", out var inputs) && inputs.TryGetProperty("caseManifest", out var manifest) &&
            (manifest.ValueKind != JsonValueKind.Object || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
             manifest.GetProperty("identityMode").GetString() != "logical-v1"))
            throw new InvalidDataException("Unsupported portable report identity contract.");
        if (report.GetProperty("schemaVersion").GetInt32() != 1 ||
            report.GetProperty("kind").GetString() != "architecture-test-run" ||
            report.GetProperty("run").GetProperty("id").GetString() != marker.RunId ||
            !string.Equals(report.GetProperty("target").GetProperty("worktreeRoot").GetString(),
                marker.TargetRoot, PathSafety.Comparison))
            throw new InvalidDataException("Test report does not match its run marker.");
        var outcome = report.GetProperty("outcome");
        var reasons = outcome.GetProperty("reasons").EnumerateArray()
            .Select(value => value.GetString() ?? "").ToArray();
        var reportGate = outcome.GetProperty("runGate").GetString();
        var reportExit = outcome.GetProperty("exitCode").GetInt32();
        if (state is "running" or "interrupted")
            return new(marker.RunId, marker.TargetRoot, marker.StartedUtc, state,
                null, null, reportPath, htmlPath, "unknown", ["Run terminal state is not confirmed."]);
        if (marker.Gate != reportGate || marker.ExitCode != reportExit)
            return new(marker.RunId, marker.TargetRoot, marker.StartedUtc, "unavailable",
                null, null, null, null, "unknown", ["Run marker and authoritative report disagree."]);
        return new(marker.RunId, marker.TargetRoot, marker.StartedUtc, state,
            reportGate, reportExit,
            reportPath, htmlPath, Freshness(report), reasons);
    }

    private static string Freshness(JsonElement report)
    {
        try
        {
            foreach (var ruleset in report.GetProperty("inputs").GetProperty("rulesets").EnumerateArray())
            {
                var path = ruleset.GetProperty("path").GetString()!;
                if (!File.Exists(path) || ContentHash.Bytes(File.ReadAllBytes(path)) !=
                    ruleset.GetProperty("sha256").GetString()) return "stale";
            }
            var target = report.GetProperty("target");
            var root = target.GetProperty("worktreeRoot").GetString()!;
            var recorded = target.GetProperty("gitCommit").GetString();
            var current = GitHead(root);
            if (recorded is not null && recorded != "unknown" && current is not null && current != recorded)
                return "stale";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { return "unknown"; }
        return "unknown"; // matching observations do not prove the whole source/build context remains current
    }

    private static string? GitHead(string root)
    {
        try
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.Environment["GIT_REDIRECT_STDIN"] = "off";
            foreach (var arg in new[] { "-C", root, "rev-parse", "HEAD" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                return null;
            }
            _ = error.GetAwaiter().GetResult();
            return process.ExitCode == 0 ? output.GetAwaiter().GetResult().Trim() : null;
        }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { return null; }
    }

    private static TestRunMarker ReadMarker(string path)
    {
        PathSafety.EnsureNoLinks(path);
        var marker = JsonSerializer.Deserialize<TestRunMarker>(File.ReadAllBytes(path), JsonContract.Options)
            ?? throw new InvalidDataException("Empty test run marker.");
        if (marker.SchemaVersion != 1 || marker.Kind != "architecture-test-state" ||
            string.IsNullOrWhiteSpace(marker.RunId) || string.IsNullOrWhiteSpace(marker.TargetRoot))
            throw new InvalidDataException("Invalid test run marker.");
        return marker;
    }

    private static string Marker(string directory)
    {
        var path = Path.Combine(Path.GetFullPath(directory), MarkerName);
        PathSafety.EnsureNoLinks(path);
        return path;
    }
}
