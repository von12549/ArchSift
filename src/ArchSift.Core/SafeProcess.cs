using System.Diagnostics;
using System.Text;

namespace ArchSift.Core;

public sealed record ProcessResult(int ExitCode, string Output, string Error);

public static class SafeProcess
{
    public static async Task<ProcessResult> RunAsync(string executable, string directory, string[] arguments,
        string cliHome, string? input, CancellationToken token, Dictionary<string, string>? environment = null)
    {
        var before = EnvironmentIdentity();
        var start = new ProcessStartInfo
        {
            FileName = executable, WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = input is not null,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // A parent test/build host can pin SDK 10 task paths while global.json selects SDK 9.
        var inheritedSdkPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MSBUILD_EXE_PATH", "MSBuildSDKsPath", "MSBuildExtensionsPath", "MSBuildExtensionsPath32",
            "MSBuildToolsPath", "MSBuildBinPath", "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR",
            "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR", "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER"
        };
        foreach (var key in start.Environment.Keys.Where(inheritedSdkPaths.Contains).ToArray()) start.Environment.Remove(key);
        start.Environment["DOTNET_CLI_HOME"] = cliHome;
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["NUGET_CERT_REVOCATION_MODE"] = "offline";
        if (environment is not null)
            foreach (var item in environment) start.Environment[item.Key] = item.Value;
        start.Environment["DOTNET_CLI_HOME"] = cliHome;
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        using var process = Process.Start(start)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var output = ReadBounded(process.StandardOutput, timeout.Token);
        var error = ReadBounded(process.StandardError, timeout.Token);
        try
        {
            if (input is not null) { await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token); process.StandardInput.Close(); }
            await process.WaitForExitAsync(timeout.Token);
            var result = new ProcessResult(process.ExitCode, await output, await error);
            if (EnvironmentIdentity() != before) throw new InvalidOperationException("Persistent/process environment drift detected.");
            return result;
        }
        catch (Exception failure)
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync(CancellationToken.None);
            if (EnvironmentIdentity() != before) throw new InvalidOperationException("Environment drift on failed/cancelled process.");
            if (failure is OperationCanceledException && !token.IsCancellationRequested)
                throw new TimeoutException("Child process timed out.", failure);
            throw;
        }
    }

    private static async Task<string> ReadBounded(StreamReader reader, CancellationToken token)
    {
        var text = new StringBuilder(); var buffer = new char[8192]; var truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            if (text.Length + count <= 16 * 1024 * 1024) text.Append(buffer, 0, count);
            else truncated = true;
        }
        if (truncated) text.Append("\n[output truncated]");
        return text.ToString();
    }

    private static string EnvironmentIdentity()
    {
        var parts = new List<string>();
        foreach (var scope in new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            var values = Environment.GetEnvironmentVariables(scope);
            foreach (var key in values.Keys.Cast<string>().Order(StringComparer.Ordinal))
                parts.Add(scope + "\0" + key + "\0" + values[key]);
        }
        parts.Add("ProcessPath\0" + Environment.GetEnvironmentVariable("Path"));
        return ContentHash.Text(string.Join("\n", parts));
    }

    public static string Dotnet => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
}
