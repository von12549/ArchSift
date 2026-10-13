using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class EvidenceArtifactStore
{
    public static void WriteNew<T>(string path, T value, bool compact = false)
    {
        PathSafety.EnsureNoLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, value, compact ? new(JsonContract.Options) { WriteIndented = false } : JsonContract.Options);
    }
    public static void SaveJob(EvidenceJobRecord job)
    {
        var path = PathSafety.Under(job.Preview.Storage.RunDirectory, "job.json");
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N"); WriteNew(temporary, job);
        PathSafety.EnsureNoLinks(path); File.Move(temporary, path, true);
    }
    public static EvidenceMaterial Material(string path, string role) => new(Path.GetFullPath(path), AssemblyArtifacts.FileHash(path), new FileInfo(path).Length, role);
    public static void Log(string path, string text)
    {
        // Do not persist environment assignment/property dumps. Compiler command lines remain reviewable.
        var environment = Environment.GetEnvironmentVariables().Keys.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line, @"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*[:=]", RegexOptions.CultureInvariant);
            if (match.Success && (environment.Contains(match.Groups[1].Value) || Sensitive(match.Groups[1].Value)))
                result.AppendLine(match.Groups[1].Value + " = [value redacted]");
            else result.AppendLine(line);
        }
        foreach (var key in environment.Where(Sensitive))
            if (Environment.GetEnvironmentVariable(key) is { Length: >= 4 } value) result.Replace(value, "[value redacted]");
        ChainWriter.WriteNew(path, result.ToString());
    }
    public static bool Sensitive(string name) => Regex.IsMatch(name, "token|password|secret|credential|api.?key", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
