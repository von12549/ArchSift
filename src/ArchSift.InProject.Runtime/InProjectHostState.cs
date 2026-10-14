using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record InProjectHostState(string Sha256);

public static class InProjectHostIdentity
{
    public static InProjectHostState Capture()
    {
        var lines = new List<string>();
        foreach (var scope in new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            var variables = Environment.GetEnvironmentVariables(scope);
            foreach (DictionaryEntry item in variables.Cast<DictionaryEntry>().OrderBy(item => item.Key?.ToString(), StringComparer.Ordinal))
                lines.Add(scope + "\0" + item.Key + "\0" + item.Value);
        }
        lines.Add("ProcessPath\0" + Environment.GetEnvironmentVariable("Path"));
        lines.Add("PowerShellProfiles\0" + ProfileIdentity());
        return new(ContentHash.Text(string.Join("\n", lines)));
    }

    private static string ProfileIdentity()
    {
        const string script = "$ErrorActionPreference='Stop'; $names=@('AllUsersAllHosts','AllUsersCurrentHost','CurrentUserAllHosts','CurrentUserCurrentHost'); foreach($name in $names){$path=[string]$PROFILE.$name; $exists=[IO.File]::Exists($path); $hash=if($exists){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}else{'missing'}; Write-Output ($name+'|'+$exists+'|'+$hash)}";
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo("pwsh")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", script })
            process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new ConfigurationException("Profile hash probe did not start.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        var profileLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (process.ExitCode != 0 || profileLines.Length != 4 || profileLines.Any(line => !line.Contains('|')))
            throw new ConfigurationException("Profile hash probe failed: " + error.Trim());
        return string.Join("\n", profileLines);
    }
}
