using ArchSift.TestGate;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class InputObserverTests
{
    [Fact]
    public void ColdOutputsOnlyChangeExistingDirectoryMetadata()
    {
        WithFixture(root =>
        {
            var project = Path.Combine(root, "Project");
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, "Known.cs"), "class Known {}");
            using var observer = new RelevantInputObserver(root);
            foreach (var name in new[] { "obj", "bin" })
            {
                var output = Path.Combine(project, name);
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "project.assets.json"), "{}");
            }
            Directory.SetLastWriteTimeUtc(project, DateTime.UtcNow.AddSeconds(-5));
            Thread.Sleep(500);
            Assert.False(observer.Changed);
            File.AppendAllText(Path.Combine(project, "Known.cs"), "// edit");
            Assert.True(SpinWait.SpinUntil(() => observer.Changed, TimeSpan.FromSeconds(3)));
        });
    }

    [Theory]
    [InlineData("create-delete")]
    [InlineData("rename")]
    [InlineData("delete")]
    [InlineData("source-restored")]
    [InlineData("source-to-ignored")]
    [InlineData("ignored-to-source")]
    public void TopologyAndTransientSourceChangesRemainBlocking(string scenario)
    {
        WithFixture(root =>
        {
            var project = Path.Combine(root, "Project");
            Directory.CreateDirectory(project);
            var source = Path.Combine(project, "Known.cs");
            File.WriteAllText(source, "class Known {}");
            var ignored = Path.Combine(root, "obj");
            Directory.CreateDirectory(ignored);
            var hidden = Path.Combine(ignored, "Hidden.cs");
            File.WriteAllText(hidden, "class Hidden {}");
            using var observer = new RelevantInputObserver(root);
            switch (scenario)
            {
                case "create-delete":
                    var transient = Path.Combine(root, "Transient");
                    Directory.CreateDirectory(transient);
                    Directory.Delete(transient);
                    break;
                case "rename": Directory.Move(project, Path.Combine(root, "Renamed")); break;
                case "delete": Directory.Delete(project, true); break;
                case "source-restored":
                    File.WriteAllText(source, "class Changed {}");
                    Assert.True(SpinWait.SpinUntil(() => observer.Changed, TimeSpan.FromSeconds(3)));
                    File.WriteAllText(source, "class Known {}");
                    break;
                case "source-to-ignored": File.Move(source, Path.Combine(ignored, "Known.cs")); break;
                case "ignored-to-source": File.Move(hidden, Path.Combine(project, "Hidden.cs")); break;
            }
            Assert.True(SpinWait.SpinUntil(() => observer.Changed, TimeSpan.FromSeconds(3)));
        });
    }

    [Fact]
    public void WatcherErrorStillFailsClosed()
    {
        WithFixture(root =>
        {
            using var observer = new RelevantInputObserver(root);
            var watcher = (FileSystemWatcher)typeof(RelevantInputObserver)
                .GetField("watcher", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(observer)!;
            typeof(FileSystemWatcher).GetMethod("OnError", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(watcher, [new ErrorEventArgs(new InternalBufferOverflowException("Injected watcher overflow"))]);
            Assert.True(observer.Changed);
        });
    }

    private static void WithFixture(Action<string> check)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var directory = Path.GetFullPath(Path.Combine(tempRoot, "archsift-fa05-watch-" + Guid.NewGuid().ToString("N")));
        if (!directory.StartsWith(tempRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Watcher fixture escaped temp root.");
        Directory.CreateDirectory(directory);
        try { check(directory); }
        finally { Directory.Delete(directory, true); }
    }

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
