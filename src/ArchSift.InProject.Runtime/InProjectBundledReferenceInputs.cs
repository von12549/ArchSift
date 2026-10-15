using System.IO.Compression;
using System.Security.Cryptography;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public static class InProjectBundledReferenceInputs
{
    private static readonly HashSet<string> ReferencePackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "microsoft.netcore.app.ref", "microsoft.aspnetcore.app.ref", "microsoft.windowsdesktop.app.ref"
    };

    public static bool AllowsAnalyzer(string installationRoot, string packageCache, string path, string sha256)
    {
        if (!PathSafety.IsUnder(path, packageCache)) return false;
        var parts = Path.GetRelativePath(packageCache, path).Replace('\\', '/').Split('/');
        if (parts.Length < 4 || !ReferencePackages.Contains(parts[0]) ||
            parts[2] != "analyzers" || !parts[^1].EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].All(character => char.IsDigit(character) || character == '.')) return false;
        var archive = Path.Combine(installationRoot, "offline", parts[0] + "." + parts[1] + ".nupkg");
        PathSafety.EnsureNoLinks(archive);
        if (!File.Exists(archive)) return false;
        using var zip = ZipFile.OpenRead(archive);
        var name = string.Join('/', parts.Skip(2));
        var entries = zip.Entries.Where(entry => entry.FullName.Equals(name, StringComparison.Ordinal)).ToArray();
        if (entries.Length != 1) return false;
        using var stream = entries[0].Open();
        var archivedSha = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return archivedSha.Equals(sha256, StringComparison.OrdinalIgnoreCase);
    }
}
