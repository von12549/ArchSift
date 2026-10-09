using System.IO.Compression;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Setup;

public static class PackageReader
{
    public const long MaximumZipBytes = 1024L * 1024 * 1024;
    public const long MaximumExpandedBytes = 2 * MaximumZipBytes;
    public const long MaximumFileBytes = 256L * 1024 * 1024;
    public const int MaximumEntries = 10000;
    public static void Version(string version)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(version, "^\\d+\\.\\d+\\.\\d+$")) throw new ConfigurationException("A stable semantic version is required.");
    }
    public static void Compatibility(PackageManifest manifest, Func<string, byte[]> read)
    {
        if (manifest.SchemaVersion != 1 || manifest.Rid != "win-x64" || !manifest.SelfContained || manifest.ProductSourceDirty)
            throw new ConfigurationException("Unsupported/uncommitted package metadata.");
        Version(manifest.Version);
        if (!System.Text.RegularExpressions.Regex.IsMatch(manifest.SourceCommit, "^[a-f0-9]{40}$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(manifest.ProductInputsSha256, "^[a-f0-9]{64}$"))
            throw new ConfigurationException("Missing source identity.");
        if (manifest.Version != "0.4.0")
        {
            if (!manifest.Version.StartsWith("0.5.", StringComparison.Ordinal)) throw new ConfigurationException("Unknown package compatibility; migration is unsupported.");
            var compatibility = SetupFiles.Parse<SetupCompatibility>(read("setup-compatibility.json"));
            if (compatibility != new SetupCompatibility(1, 1, 1, 1)) throw new ConfigurationException("Unsupported config/library/chain schema migration.");
        }
        foreach (var required in new[] { "archsift.exe", "web/ArchSift.Web.exe", "LICENSE" })
            if (!manifest.Entries.Any(e => e.Path == required)) throw new ConfigurationException("Incomplete native package.");
    }
    public static PackageIdentity Inspect(string path, string sha256)
    {
        path = SetupFiles.Safe(path);
        if (!System.Text.RegularExpressions.Regex.IsMatch(sha256, "^[a-f0-9]{64}$")) throw new ConfigurationException("Expected lowercase SHA-256 is required.");
        using var packageStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (packageStream.Length > MaximumZipBytes || Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packageStream)).ToLowerInvariant() != sha256)
            throw new IOException("Package size/checksum mismatch.");
        packageStream.Position = 0;
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        var entries = CheckArchive(archive);
        var manifestBytes = Bytes(entries["package-manifest.json"]);
        using (var doc = JsonDocument.Parse(manifestBytes)) SetupFiles.CheckKeys(doc.RootElement);
        var manifest = SetupFiles.Parse<PackageManifest>(manifestBytes);
        VerifyEntries(manifest, entries.Keys.Where(p => p != "package-manifest.json"), name => entries[name].Length, name =>
        { using var stream = entries[name].Open(); return BoundedHash(stream, entries[name].Length); });
        Compatibility(manifest, name => Bytes(entries[name]));
        return new(path, sha256, manifest.Version, SetupFiles.Hash(manifestBytes), manifest.SourceCommit);
    }
    private static Dictionary<string, ZipArchiveEntry> CheckArchive(ZipArchive archive)
    {
        if (archive.Entries.Count > MaximumEntries) throw new ConfigurationException("ZIP entry count exceeded.");
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            var directory = entry.FullName.EndsWith('/'); var name = directory ? entry.FullName[..^1] : entry.FullName;
            SetupFiles.ValidateRelative(name);
            var unixType = (entry.ExternalAttributes >> 16) & 0xf000;
            if (unixType is not (0 or 0x8000 or 0x4000) || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new ConfigurationException("Archive links/special files are rejected.");
            if (!names.Add(name)) throw new ConfigurationException("Duplicate/case-colliding archive path.");
            if (entry.Length > MaximumFileBytes || (expanded += entry.Length) > MaximumExpandedBytes) throw new ConfigurationException("ZIP expanded-size limit exceeded.");
            if (!directory) entries.Add(entry.FullName, entry);
        }
        if (!entries.ContainsKey("package-manifest.json")) throw new ConfigurationException("Package manifest is missing.");
        foreach (var name in names)
        {
            var parent = name;
            while (parent.Contains('/'))
            { parent = parent[..parent.LastIndexOf('/')]; if (entries.ContainsKey(parent)) throw new ConfigurationException("File/directory archive collision."); }
        }
        return entries;
    }
    private static byte[] Bytes(ZipArchiveEntry entry)
    {
        if (entry.Length > 4 * 1024 * 1024) throw new ConfigurationException("Package metadata exceeds 4 MiB.");
        using var stream = entry.Open(); using var output = new MemoryStream(); var buffer = new byte[81920]; int count;
        while ((count = stream.Read(buffer)) > 0) { if (output.Length + count > entry.Length) throw new IOException("Metadata exceeded declared length."); output.Write(buffer, 0, count); }
        if (output.Length != entry.Length) throw new IOException("Truncated archive entry.");
        return output.ToArray();
    }
    private static string BoundedHash(Stream stream, long expectedLength)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var buffer = new byte[81920]; long count = 0; int read;
        while ((read = stream.Read(buffer)) > 0)
        { if ((count += read) > expectedLength) throw new IOException("Payload exceeded declared length."); hash.AppendData(buffer, 0, read); }
        if (count != expectedLength) throw new IOException("Truncated payload.");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    private static void VerifyEntries(PackageManifest manifest, IEnumerable<string> actual, Func<string, long> size, Func<string, string> hash)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Entries)
        {
            SetupFiles.ValidateRelative(entry.Path);
            if (entry.Path == "package-manifest.json" || !paths.Add(entry.Path) || entry.Bytes < 0 || entry.Bytes > MaximumFileBytes)
                throw new ConfigurationException("Invalid manifest payload identity.");
            if (size(entry.Path) != entry.Bytes || hash(entry.Path) != entry.Sha256) throw new IOException("Payload mismatch: " + entry.Path);
        }
        if (!actual.Order(StringComparer.Ordinal).SequenceEqual(manifest.Entries.Select(e => e.Path).Order(StringComparer.Ordinal)))
            throw new ConfigurationException("Archive payload differs from manifest.");
    }
    public static PackageManifest VerifyDirectory(string directory, string expectedManifest, bool rejectExtra = false)
    {
        directory = SetupFiles.Safe(directory);
        var manifestPath = SetupFiles.Under(directory, "package-manifest.json");
        if (SetupFiles.FileHash(manifestPath) != expectedManifest) throw new IOException("Installed manifest identity changed.");
        var manifest = SetupFiles.Read<PackageManifest>(manifestPath);
        VerifyEntries(manifest, manifest.Entries.Select(e => e.Path), name => new FileInfo(SetupFiles.Under(directory, name)).Length, name => SetupFiles.FileHash(SetupFiles.Under(directory, name)));
        Compatibility(manifest, name => File.ReadAllBytes(SetupFiles.Under(directory, name)));
        if (rejectExtra)
        {
            var actual = SafeFiles(directory).Select(p => Path.GetRelativePath(directory, p).Replace('\\', '/')).Where(p => p != "package-manifest.json");
            if (!actual.Order(StringComparer.Ordinal).SequenceEqual(manifest.Entries.Select(e => e.Path).Order(StringComparer.Ordinal))) throw new IOException("Unowned files in version directory.");
        }
        return manifest;
    }
    private static IEnumerable<string> SafeFiles(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            SetupFiles.Safe(path);
            if (Directory.Exists(path)) { foreach (var file in SafeFiles(path)) yield return file; }
            else yield return path;
        }
    }
    public static void Extract(PackageIdentity identity, string destination, CancellationToken cancellation)
    {
        if (Inspect(identity.Path, identity.Sha256) != identity) throw new IOException("Package changed after plan review.");
        if (Directory.Exists(destination)) throw new ConfigurationException("Staging directory exists.");
        Directory.CreateDirectory(SetupFiles.Safe(destination));
        using var archive = ZipFile.OpenRead(identity.Path);
        foreach (var entry in CheckArchive(archive).Values)
        {
            cancellation.ThrowIfCancellationRequested();
            var path = SetupFiles.Under(destination, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); SetupFiles.Safe(path);
            using var input = entry.Open(); using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920]; long written = 0; int count;
            while ((count = input.Read(buffer)) > 0)
            { cancellation.ThrowIfCancellationRequested(); if ((written += count) > entry.Length) throw new IOException("Entry exceeded declared length."); output.Write(buffer, 0, count); }
            if (written != entry.Length) throw new IOException("Truncated extraction.");
            output.Flush(true);
        }
        VerifyDirectory(destination, identity.ManifestSha256, true);
    }
}
