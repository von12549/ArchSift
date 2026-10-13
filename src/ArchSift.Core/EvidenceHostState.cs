using System.Text.Json;

namespace ArchSift.Core;

public sealed class HostDriftException() : Exception("Host environment/Profile drift detected; stop and preserve evidence.");
public sealed record EvidenceHostState(string UserEnvironmentSha256, string MachineEnvironmentSha256,
    string ProcessPathSha256, Dictionary<string, string> ProfileSha256)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Identity => ContentHash.Text(JsonSerializer.Serialize(this, ArchSift.Contracts.JsonContract.Options));
    public static EvidenceHostState Capture()
    {
        string EnvironmentHash(EnvironmentVariableTarget scope)
        {
            var values = Environment.GetEnvironmentVariables(scope);
            return ContentHash.Text(string.Join("\n", values.Keys.Cast<string>().Order(StringComparer.Ordinal).Select(k => k + "\0" + values[k])));
        }
        var home = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7");
        var profiles = new[] { Path.Combine(shell, "profile.ps1"), Path.Combine(shell, "Microsoft.PowerShell_profile.ps1"),
            Path.Combine(home, "PowerShell", "profile.ps1"), Path.Combine(home, "PowerShell", "Microsoft.PowerShell_profile.ps1") };
        return new(EnvironmentHash(EnvironmentVariableTarget.User), EnvironmentHash(EnvironmentVariableTarget.Machine),
            ContentHash.Text(Environment.GetEnvironmentVariable("Path") ?? ""), profiles.ToDictionary(p => p, p => File.Exists(p) ? AssemblyArtifacts.FileHash(p) : "absent", StringComparer.Ordinal));
    }
    public void Verify() { if (Capture().Identity != Identity) throw new HostDriftException(); }
}
