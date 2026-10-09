using System.Diagnostics;
using System.Text;

namespace ArchSift.Hosting;

/// <summary>Owns one Web child; redirected stdin is also the parent-lifetime channel.</summary>
public static class UiProcess
{
    public static async Task<int> RunAsync(string executable, string? assembly, string config,
        TextWriter output, TextWriter error, CancellationToken cancellation, bool smoke = false,
        TimeSpan? startupTimeout = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        if (assembly is not null) start.ArgumentList.Add(assembly);
        start.ArgumentList.Add("--config"); start.ArgumentList.Add(config);
        // Inherit the complete Windows environment; only child-scoped overrides are added.
        start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Path.GetTempPath(), "archsift-ui-home-" + Guid.NewGuid().ToString("N"));
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["ARCHSIFT_UI_PARENT_CONTROL"] = "stdin-v1";
        using var process = Process.Start(start) ?? throw new IOException("Web child did not start.");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Pump(StreamReader stream, TextWriter writer, bool stdout)
        {
            while (await stream.ReadLineAsync() is { } line)
            {
                // Setup smoke never persists or displays authentication tokens.
                if (!smoke || !line.StartsWith("ARCHSIFT_UI=", StringComparison.Ordinal))
                { await writer.WriteLineAsync(line); await writer.FlushAsync(); }
                if (stdout && line.StartsWith("ARCHSIFT_STATE=waiting", StringComparison.Ordinal)) ready.TrySetResult();
            }
        }
        var stdout = Pump(process.StandardOutput, output, true);
        var stderr = Pump(process.StandardError, error, false);
        var exited = process.WaitForExitAsync();
        try
        {
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
