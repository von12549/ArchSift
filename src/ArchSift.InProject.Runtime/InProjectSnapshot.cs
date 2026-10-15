using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record InProjectSnapshot(string RunId, string RunDirectory, string SourceDirectory,
    string ReportDirectory, InputIdentity InputIdentity);

public static class InProjectSnapshots
{
    public static InProjectSnapshot Create(InProjectLoadedConfiguration config, InProjectBuildPlan plan,
        CancellationToken token = default, string? reservedRunId = null)
    {
        InputCapture.VerifyUnchanged(config.Root, plan.Snapshot.InputIdentity, token);
        var runId = reservedRunId ?? Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(runId, "N", out _))
            throw new ConfigurationException("Invalid reserved InProject run ID.");
        var buildRoot = Path.Combine(config.InstallationRoot, "state", "build");
        var reportRoot = Path.Combine(config.InstallationRoot, "reports");
        var runDirectory = Path.Combine(buildRoot, runId);
        var source = Path.Combine(runDirectory, "source");
        var report = Path.Combine(reportRoot, runId);
        foreach (var path in new[] { buildRoot, reportRoot, runDirectory, source, report })
        {
            if (!PathSafety.IsUnder(path, config.InstallationRoot) || !PathSafety.IsUnder(path, config.Root))
                throw new ConfigurationException("InProject run path escapes owned installation.");
            PathSafety.EnsureNoLinks(path);
        }
        if (Directory.Exists(runDirectory) || Directory.Exists(report))
            throw new ConfigurationException("Run ID already has owned output.");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(report);
        try
        {
            var owner = new { schemaVersion = 1, product = "ArchSift.InProject", worktreeRoot = config.Root,
                runId, sourceInputSha256 = plan.Snapshot.InputIdentity.Sha256 };
            File.WriteAllText(Path.Combine(runDirectory, "owner.json"), JsonSerializer.Serialize(owner, JsonContract.Options));
            File.WriteAllText(Path.Combine(report, "owner.json"), JsonSerializer.Serialize(owner, JsonContract.Options));
            foreach (var file in plan.Snapshot.InputIdentity.Files)
            {
                token.ThrowIfCancellationRequested();
                if (file.Path.StartsWith(".archsift/", StringComparison.OrdinalIgnoreCase))
                    throw new ConfigurationException("Tool files cannot be copied into a business snapshot.");
                var original = PathSafety.Under(config.Root, file.Path);
                var copy = PathSafety.Under(source, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(original, copy, overwrite: false);
                if (LocalPackage.HashFile(copy) != file.Sha256 || new FileInfo(copy).Length != file.Length)
                    throw new SourceChangedException();
            }
            InputCapture.VerifyUnchanged(config.Root, plan.Snapshot.InputIdentity, token);
            VerifySnapshot(source, plan.Snapshot.InputIdentity);
            return new(runId, runDirectory, source, report, plan.Snapshot.InputIdentity);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(report, "build-state.json"),
                JsonSerializer.Serialize(new { schemaVersion = 1, product = "ArchSift.InProject", runId,
                    state = error is OperationCanceledException ? "cancelled" : "failed", sourceBound = false,
                    diagnostic = error.Message }, JsonContract.Options));
            throw;
        }
    }

    public static void VerifySnapshot(string source, InputIdentity expected)
    {
        var captured = InputCapture.Capture(source);
        if (captured.Sha256 != expected.Sha256 || captured.Files.Length != expected.Files.Length)
            throw new ConfigurationException("Fresh snapshot differs from captured business input.");
    }
}
