using System.Collections;
using System.Text.Json;
using ArchSift.Core;

namespace ArchSift.TestGate;

internal static class GateHostSafety
{
    internal static string Capture()
    {
        var values = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var scope in new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
            foreach (DictionaryEntry item in Environment.GetEnvironmentVariables(scope))
                values[scope + "/" + item.Key] = item.Value?.ToString();
        values["process-path"] = Environment.GetEnvironmentVariable("PATH");
        var profileJson = Environment.GetEnvironmentVariable("ARCHSIFT_PROFILE_PATHS");
        var directories = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PowerShell") };
        var profiles = profileJson is null ? directories.SelectMany(directory => new[] {
            Path.Combine(directory, "profile.ps1"), Path.Combine(directory, "Microsoft.PowerShell_profile.ps1") }).ToArray()
            : JsonSerializer.Deserialize<string[]>(profileJson)!;
        if (profiles.Length != 4 || profiles.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4 ||
            profiles.Any(path => !Path.IsPathFullyQualified(path)))
            throw new ConfigurationException("Four explicit PowerShell profile paths are required.");
        foreach (var path in profiles) values["profile/" + path] = File.Exists(path)
            ? ContentHash.Bytes(File.ReadAllBytes(path)) : "absent";
        return ContentHash.Text(JsonSerializer.Serialize(values));
    }

    internal static void Verify(string before)
    {
        if (Capture() != before) throw new ConfigurationException("Host environment/Profile drift detected; execution stopped, evidence retained.");
    }
}
