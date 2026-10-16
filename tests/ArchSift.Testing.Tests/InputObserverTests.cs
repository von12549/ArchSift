using ArchSift.TestGate;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class InputObserverTests
{
    [Fact]
    public void SourceEditIsKnownWhileBuildOutputIsIgnored()
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var directory = Path.GetFullPath(Path.Combine(tempRoot, "archsift-lt02-watch-" + Guid.NewGuid().ToString("N")));
        if (!directory.StartsWith(tempRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Watcher fixture escaped the temp root.");
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "src");
            Directory.CreateDirectory(source);
            using var observer = new RelevantInputObserver(directory);
            var output = Path.Combine(directory, "bin");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "Generated.cs"), "class Generated {}");
            Thread.Sleep(100);
            Assert.False(observer.Changed);
            File.WriteAllText(Path.Combine(source, "Changed.cs"), "class Changed {}");
            Assert.True(SpinWait.SpinUntil(() => observer.Changed, TimeSpan.FromSeconds(3)));
        }
        finally
        {
            if (Directory.Exists(directory) && directory.StartsWith(tempRoot,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void NewSourceDirectoryIsObservedBeforeItsFirstFile()
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var directory = Path.GetFullPath(Path.Combine(tempRoot, "archsift-lt02-watch-" + Guid.NewGuid().ToString("N")));
        if (!directory.StartsWith(tempRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Watcher fixture escaped the temp root.");
        Directory.CreateDirectory(directory);
        try
        {
            using var observer = new RelevantInputObserver(directory);
            Directory.CreateDirectory(Path.Combine(directory, "src"));
            Assert.True(SpinWait.SpinUntil(() => observer.Changed, TimeSpan.FromSeconds(3)));
        }
        finally
        {
            if (Directory.Exists(directory) && directory.StartsWith(tempRoot,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                Directory.Delete(directory, recursive: true);
        }
    }
}
