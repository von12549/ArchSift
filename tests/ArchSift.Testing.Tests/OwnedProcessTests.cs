using System.Diagnostics;
using ArchSift.TestGate;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class OwnedProcessTests
{
    [Fact]
    public async Task CancellationStopsOwnedChildPromptly()
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var directory = Path.GetFullPath(Path.Combine(tempRoot, "archsift-lt02-cancel-" + Guid.NewGuid().ToString("N")));
        if (!directory.StartsWith(tempRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Cancellation fixture escaped the temp root.");
        Directory.CreateDirectory(directory);
        try
        {
            var config = new TestGateConfig
            {
                TargetRoot = directory,
                TestProject = "unused.csproj",
                TestExecutableName = "unused",
                TargetFramework = "net10.0",
                Configuration = "Release",
                NuGetConfig = "unused.config",
                LocalFeed = directory,
                PackageCache = directory,
                Rulesets = ["unused.json"],
                OriginalBaseline = "unused.json",
                OriginalBaselineSha256 = new string('0', 64),
                RunDirectory = directory
            };
            var executable = OperatingSystem.IsWindows() ? "pwsh" : "/bin/sleep";
            var arguments = OperatingSystem.IsWindows()
                ? new[] { "-NoProfile", "-Command", "Start-Sleep -Seconds 30" } : ["30"];
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            var timer = Stopwatch.StartNew();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TestGateService.Execute(
                "cancel", executable, arguments, tempRoot, directory, config,
                Path.Combine(directory, "unused.json"), cancel.Token));
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10), "Owned child did not stop promptly.");
        }
        finally
        {
            // This test owns exactly one fresh temp directory and no other process output.
            if (Directory.Exists(directory) && directory.StartsWith(tempRoot,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                Directory.Delete(directory, recursive: true);
        }
    }
}
