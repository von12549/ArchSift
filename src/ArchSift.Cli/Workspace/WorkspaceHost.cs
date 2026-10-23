using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Workspaces;

internal sealed class WorkspaceHost
{
    private readonly string baseline = Capture();
    private static string Capture()
    {
        var values = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var scope in new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
            foreach (System.Collections.DictionaryEntry item in Environment.GetEnvironmentVariables(scope)) values[scope + "/" + item.Key] = item.Value?.ToString();
        values["Process/PATH"] = Environment.GetEnvironmentVariable("PATH");
        foreach (var scope in new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.Process })
            values[scope + "/PATH-UTF16"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                Encoding.Unicode.GetBytes(Environment.GetEnvironmentVariable("PATH", scope) ?? "")));
        var profiles = Environment.GetEnvironmentVariable("ARCHSIFT_PROFILE_PATHS") is { } json ? JsonSerializer.Deserialize<string[]>(json)!
            : new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "profile.ps1"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "Microsoft.PowerShell_profile.ps1"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PowerShell", "profile.ps1"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PowerShell", "Microsoft.PowerShell_profile.ps1") };
        if (profiles.Length != 4 || profiles.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4 || profiles.Any(p => !Path.IsPathFullyQualified(p))) throw new ConfigurationException("Four explicit profile paths are required.");
        foreach (var path in profiles) values["profile/" + path] = File.Exists(path) ? AssemblyArtifacts.FileHash(path) : "absent";
        return ContentHash.Text(JsonSerializer.Serialize(values));
    }
    internal void Verify() { if (Capture() != baseline) throw new HostDriftException(); }
    internal async Task<string> Probe(string executable, string root, string[] args, CancellationToken cancellation)
    {
        Verify();
        var start = new ProcessStartInfo(executable) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_HOME"] = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME") ?? Path.Combine(Path.GetTempPath(), "archsift-workspace-home-" + Guid.NewGuid().ToString("N"));
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1";
        start.Environment["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_ROLL_FORWARD"] = "LatestPatch";
        using var process = Process.Start(start) ?? throw new IOException("dotnet discovery did not start.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellation); var error = process.StandardError.ReadToEndAsync(cancellation);
            await process.WaitForExitAsync(cancellation).WaitAsync(TimeSpan.FromSeconds(30), cancellation);
            var text = await output; await error; Verify();
            if (process.ExitCode != 0) throw new ConfigurationException("dotnet " + string.Join(' ', args) + " failed; check the selected host and project global.json.");
            return text;
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } Verify(); }
    }
    internal async Task<WorkspaceHostSelection> Resolve(WorkspaceSettings settings, CancellationToken cancellation)
    {
        var configured = settings.Configuration.Runtime.DotnetPath;
        var candidates = new List<string>();
        if (configured is not null) candidates.Add(WorkspaceSettings.Canonical(configured, Path.GetDirectoryName(settings.File)));
        else
        {
            if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host) candidates.Add(host);
            foreach (var name in new[] { "DOTNET_ROOT_X64", "DOTNET_ROOT" })
                if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } root) candidates.Add(Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
            foreach (var path in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                candidates.Add(Path.Combine(path.Trim('"'), OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        }
        foreach (var candidate in candidates.Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
        {
            try
            {
                if (!Path.IsPathFullyQualified(candidate) || !File.Exists(candidate)) throw new ConfigurationException("Selected dotnet executable is missing.");
                PathSafety.EnsureNoLinks(candidate);
                var sdkText = (await Probe(candidate, settings.ProjectRoot, ["--version"], cancellation)).Trim();
                if ((!settings.Configuration.Runtime.AllowPrerelease && sdkText.Contains('-')) ||
                    !Version.TryParse(sdkText.Split('-')[0], out var sdk) || sdk < Version.Parse(settings.Configuration.Runtime.MinimumSdkVersion))
                    throw new ConfigurationException("The selected SDK must be stable and >= " + settings.Configuration.Runtime.MinimumSdkVersion + ".");
                var info = await Probe(candidate, settings.ProjectRoot, ["--info"], cancellation);
                if (OperatingSystem.IsWindows() && !Regex.IsMatch(info, @"(?m)^\s*Architecture:\s*x64\s*$"))
                    throw new ConfigurationException("The interactive Windows host must be x64.");
                var runtimes = await Probe(candidate, settings.ProjectRoot, ["--list-runtimes"], cancellation);
                var selected = new Dictionary<string, string>();
                foreach (var framework in new[] { "Microsoft.NETCore.App", "Microsoft.AspNetCore.App" })
                {
                    var matches = Regex.Matches(runtimes, "(?m)^" + Regex.Escape(framework) + @" (10\.0\.\d+) \[")
                        .Select(m => Version.Parse(m.Groups[1].Value)).OrderDescending().ToArray();
                    if (matches.Length == 0) throw new ConfigurationException(framework + " 10.0 is required; a newer SDK alone does not provide framework compatibility.");
                    selected[framework] = matches[0].ToString();
                }
                var runtimeRoot = WorkspaceSettings.Canonical(Path.GetDirectoryName(candidate)!);
                PathSafety.EnsureDisjoint(settings.ProjectRoot, runtimeRoot); PathSafety.EnsureDisjoint(settings.StateRoot, runtimeRoot);
                if (settings.RuntimeApproval is { } approvalPath)
                {
                    using var approval = WorkspaceSettings.Read(approvalPath); var value = approval.RootElement;
                    if (settings.Configuration.Runtime.Mode != "approvedExact" || value.GetProperty("schemaVersion").GetInt32() != 1 ||
                        sdkText != value.GetProperty("sdk").GetString() || selected.Values.Any(v => v != value.GetProperty("runtime").GetString()) ||
                        !Regex.IsMatch(info, "(?ms)^Host:\\s*\\r?\\n\\s*Version:\\s+" + Regex.Escape(value.GetProperty("runtime").GetString()!) + @"\s*$"))
                        throw new ConfigurationException("The selected host differs from the exact IFX SDK/runtime approval.");
                    foreach (var file in value.GetProperty("files").EnumerateArray())
                        if (AssemblyArtifacts.FileHash(PathSafety.Under(runtimeRoot, file.GetProperty("path").GetString()!)) != file.GetProperty("sha256").GetString())
                            throw new ConfigurationException("Approved runtime bytes differ.");
                    foreach (var framework in selected.Keys)
                        if (Directory.GetDirectories(Path.Combine(runtimeRoot, "shared", framework), "10.*").Length != 1)
                            throw new ConfigurationException("Exact IFX host contains an unapproved additional .NET 10 patch.");
                }
                else if (settings.Configuration.Runtime.Mode == "approvedExact") throw new ConfigurationException("An exact runtime approval file is required.");
                return new(candidate, sdkText, selected["Microsoft.NETCore.App"], selected["Microsoft.AspNetCore.App"]);
            }
            catch (Exception error) when (configured is null && error is ConfigurationException or IOException or System.ComponentModel.Win32Exception) { Verify(); }
        }
        throw new ConfigurationException("No compatible dotnet host satisfies the workspace SDK/framework/global.json requirements. Supply runtime.dotnetPath or install the required SDK/frameworks explicitly.");
    }
}
internal sealed record WorkspaceHostSelection(string Dotnet, string Sdk, string CoreRuntime, string AspNetRuntime);
