using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class CliUiLifecycleTests
{
    [Theory]
    [InlineData("cancel")]
    [InlineData("timeout")]
    [InlineData("bad-config")]
    public async Task OwnedRunnerCancellationStartupTimeoutAndErrorLeaveNoChild(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-ui-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "target"));
        var configPath = Path.Combine(root, "config.json");
        File.WriteAllText(configPath, scenario == "bad-config" ? "{}" : JsonSerializer.Serialize(new RunConfiguration
        { SchemaVersion = 1, Target = new() { Root = Path.Combine(root, "target") }, Output = new() { Directory = Path.Combine(root, "reports") } }, JsonContract.Options));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
        var build = typeof(CliUiLifecycleTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var executable = Path.Combine(directory!.FullName, "src", "ArchSift.Web", "bin", build, "net10.0", OperatingSystem.IsWindows() ? "ArchSift.Web.exe" : "ArchSift.Web");
        using var cancellation = new CancellationTokenSource(); var errors = new StringWriter();
        var writer = new ReadinessWriter(cancellation, scenario == "cancel");
        try
        {
            var run = ArchSift.Hosting.UiProcess.RunAsync(executable, null, configPath, writer, errors, cancellation.Token,
                startupTimeout: scenario == "timeout" ? TimeSpan.FromTicks(1) : null);
            if (scenario == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            else if (scenario == "timeout") await Assert.ThrowsAsync<TimeoutException>(() => run);
            else { Assert.Equal(2, await run); Assert.NotEmpty(errors.ToString()); }
            if (writer.ChildPid is { } pid)
            {
                try { using var child = Process.GetProcessById(pid); Assert.True(child.HasExited); }
                catch (ArgumentException) { }
            }
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class ReadinessWriter(CancellationTokenSource cancellation, bool cancelOnReady) : StringWriter
    {
        public int? ChildPid { get; private set; }
        public override Task WriteLineAsync(string? value)
        {
            if (value?.StartsWith("ARCHSIFT_PID=", StringComparison.Ordinal) == true) ChildPid = int.Parse(value[13..], System.Globalization.CultureInfo.InvariantCulture);
            if (cancelOnReady && value?.StartsWith("ARCHSIFT_STATE=waiting", StringComparison.Ordinal) == true) cancellation.Cancel();
            // Never include session URL/token in test output or assertions.
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NativeCliForwardsStartupAndOwnsWebUntilShutdownOrParentExit(bool terminateParent, bool startJob)
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-cli-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "target"));
        if (startJob)
            for (var index = 0; index < 5000; index++)
                File.WriteAllText(Path.Combine(root, "target", "P" + index + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = Path.Combine(root, "target") },
            Output = new() { Directory = Path.Combine(root, "reports") }, RulesDirectory = Path.Combine(root, "rules") };
        var configPath = Path.Combine(root, "config.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, JsonContract.Options));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
        var build = typeof(CliUiLifecycleTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var executable = Path.Combine(directory!.FullName, "src", "ArchSift.Cli", "bin", build, "net10.0", OperatingSystem.IsWindows() ? "archsift.exe" : "archsift");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "ui", "--config", configPath }) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_HOME"] = Path.Combine(root, "cli-home");
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        using var parent = Process.Start(start)!;
        var error = parent.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Process? child = null;
        try
        {
            Uri? address = null; int? pid = null; bool waiting = false;
            while (address is null || pid is null || !waiting)
            {
                var line = await parent.StandardOutput.ReadLineAsync(timeout.Token);
                Assert.NotNull(line);
                if (line.StartsWith("ARCHSIFT_UI=", StringComparison.Ordinal)) address = new Uri(line[12..]);
                if (line.StartsWith("ARCHSIFT_PID=", StringComparison.Ordinal)) pid = int.Parse(line[13..], System.Globalization.CultureInfo.InvariantCulture);
                waiting |= line.StartsWith("ARCHSIFT_STATE=waiting", StringComparison.Ordinal);
            }
            child = Process.GetProcessById(pid.Value);
            Assert.Equal("127.0.0.1", address.Host);
            using var client = new HttpClient { BaseAddress = new Uri(address.GetLeftPart(UriPartial.Authority)) };
            client.DefaultRequestHeaders.Add("X-ArchSift-Token", address.Fragment[9..]);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/config", timeout.Token)).StatusCode);
            if (startJob)
            {
                using var started = await client.PostAsJsonAsync("/api/run/analyze", new { targetKind = "fixture" }, timeout.Token);
                started.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await started.Content.ReadAsStringAsync(timeout.Token));
                using var state = JsonDocument.Parse(await client.GetStringAsync("/api/jobs/" + document.RootElement.GetProperty("jobId").GetString(), timeout.Token));
                Assert.Equal("running", state.RootElement.GetProperty("state").GetString());
            }
            if (terminateParent) parent.Kill();
            else (await client.PostAsJsonAsync("/api/shutdown", new { }, timeout.Token)).EnsureSuccessStatusCode();
            await parent.WaitForExitAsync(timeout.Token);
            await child.WaitForExitAsync(timeout.Token);
            if (!terminateParent) Assert.Equal(0, parent.ExitCode);
            if (startJob)
            {
                var reportPath = Assert.Single(Directory.GetFiles(config.Output.Directory, "report.json", SearchOption.AllDirectories));
                using var report = JsonDocument.Parse(File.ReadAllBytes(reportPath));
                Assert.Equal("cancelled", report.RootElement.GetProperty("execution").GetString());
                Assert.Equal(5000, Directory.GetFiles(config.Target.Root, "*.csproj").Length);
            }
            else Assert.False(Directory.Exists(config.Output.Directory));
            await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.GetAsync("/api/config", timeout.Token));
            await error;
        }
        finally
        {
            if (!parent.HasExited) { parent.Kill(true); await parent.WaitForExitAsync(); }
            if (child is not null) { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } child.Dispose(); }
            Directory.Delete(root, true);
        }
    }
}
