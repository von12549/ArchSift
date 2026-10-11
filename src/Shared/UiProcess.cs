using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Hosting;

/// <summary>Owns one Web child; redirected stdin is also the parent-lifetime channel.</summary>
public static class UiProcess
{
    public static Task<int> RunAsync(string executable, string? assembly, string config,
        TextWriter output, TextWriter error, CancellationToken cancellation, bool smoke = false,
        TimeSpan? startupTimeout = null) => RunArgumentsAsync(executable, assembly, ["--config", config],
            output, error, cancellation, smoke, startupTimeout);

    public static async Task<int> RunArgumentsAsync(string executable, string? assembly, string[] arguments,
        TextWriter output, TextWriter error, CancellationToken cancellation, bool smoke = false,
        TimeSpan? startupTimeout = null, Action<Uri>? onAddress = null, SessionProfile? profile = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        if (assembly is not null) start.ArgumentList.Add(assembly);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // Inherit the complete Windows environment; only child-scoped overrides are added.
        start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Path.GetTempPath(), "archsift-ui-home-" + Guid.NewGuid().ToString("N"));
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["ARCHSIFT_UI_PARENT_CONTROL"] = "stdin-v1";
        start.Environment["ARCHSIFT_UI_PROFILE_CONTROL"] = "stdin-profile-v1";
        using var process = Process.Start(start) ?? throw new IOException("Web child did not start.");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Pump(StreamReader stream, TextWriter writer, bool stdout)
        {
            while (await stream.ReadLineAsync() is { } line)
            {
                // Setup smoke never persists or displays authentication tokens.
                if (stdout && line.StartsWith("ARCHSIFT_UI=", StringComparison.Ordinal) && onAddress is not null)
                {
                    var address = new Uri(line[12..]);
                    if (address.Scheme != "http" || address.Host != "127.0.0.1") throw new IOException("Unexpected workbench address.");
                    onAddress(address);
                }
                if (!(smoke || onAddress is not null) || !line.StartsWith("ARCHSIFT_UI=", StringComparison.Ordinal))
                { await writer.WriteLineAsync(line); await writer.FlushAsync(); }
                if (stdout && line.StartsWith("ARCHSIFT_STATE=waiting", StringComparison.Ordinal)) ready.TrySetResult();
            }
        }
        var stdout = Pump(process.StandardOutput, output, true);
        var stderr = Pump(process.StandardError, error, false);
        var exited = process.WaitForExitAsync();
        try
        {
            await process.StandardInput.WriteLineAsync("profile:" + JsonSerializer.Serialize(profile, new JsonSerializerOptions(JsonContract.Options) { WriteIndented = false }));
            await process.StandardInput.FlushAsync();
            var first = await Task.WhenAny(ready.Task, exited).WaitAsync(startupTimeout ?? TimeSpan.FromSeconds(30), cancellation);
            if (first == exited) { await Task.WhenAll(stdout, stderr); if (process.ExitCode == 0) throw new IOException("Web exited before readiness."); return process.ExitCode; }
            await ready.Task;
            if (smoke) { await Stop(); return process.ExitCode; }
            await exited.WaitAsync(cancellation);
            await Task.WhenAll(stdout, stderr);
            return process.ExitCode;
        }
        finally
        {
            await Stop();
            await Task.WhenAll(stdout, stderr);
        }

        async Task Stop()
        {
            if (process.HasExited) return;
            try { await process.StandardInput.WriteLineAsync("shutdown"); await process.StandardInput.FlushAsync(); }
            catch (IOException) { }
            try { await exited.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { if (!process.HasExited) process.Kill(entireProcessTree: true); await exited; }
        }
    }
}
