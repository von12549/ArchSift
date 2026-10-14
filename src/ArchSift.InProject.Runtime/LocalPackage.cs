using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record PayloadFile(string Path, long Bytes, string Sha256);
public sealed record LocalPackageIdentity(string Path, string PackageSha256, string ManifestSha256,
    string Version, string SourceCommit, PayloadFile[] Files);

public static class LocalPackage
{
    private const long MaximumZipBytes = 1024L * 1024 * 1024;
    private const long MaximumExpandedBytes = 2 * MaximumZipBytes;
    private const long MaximumFileBytes = 256L * 1024 * 1024;
    private const int MaximumEntries = 10000;
    public static readonly IReadOnlyDictionary<string, string> Components = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ArchSift.InProject"] = "runtime/archsift-inproject.exe",
        ["ArchSift.InProject.Runtime"] = "runtime/ArchSift.InProject.Runtime.dll",
        ["ArchSift.InProject.TestBridge"] = "runtime/ArchSift.InProject.TestBridge.dll",
        ["ArchSift.Contracts"] = "runtime/ArchSift.Contracts.dll",
        ["ArchSift.Core"] = "runtime/ArchSift.Core.dll",
        ["ArchSift.ArchUnit"] = "runtime/ArchSift.ArchUnit.dll",
        ["ArchSift.Cli.Worker"] = "runtime/archsift.exe"
    };

    public static LocalPackageIdentity Inspect(string packagePath, string expectedSha256)
    {
        var path = Path.GetFullPath(packagePath);
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path) || new FileInfo(path).Length > MaximumZipBytes)
            throw new ConfigurationException("Local package is missing or too large.");
        if (!ValidHash(expectedSha256) || HashFile(path) != expectedSha256)
            throw new ConfigurationException("Local package SHA-256 mismatch.");
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count is 0 or > MaximumEntries)
            throw new ConfigurationException("Local package entry count is invalid.");
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/')) throw new ConfigurationException("Directory ZIP entries are not supported.");
            ValidateRelative(entry.FullName);
            var unixType = (entry.ExternalAttributes >> 16) & 0xf000;
            if (unixType is not (0 or 0x8000) || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new ConfigurationException("Package links and special files are rejected.");
            if (!entries.TryAdd(entry.FullName, entry)) throw new ConfigurationException("Duplicate or case-colliding package path.");
            if (entry.Length > MaximumFileBytes || (expanded += entry.Length) > MaximumExpandedBytes)
                throw new ConfigurationException("Expanded package size is too large.");
        }
        if (!entries.TryGetValue("payload-manifest.json", out var manifestEntry) || manifestEntry.Length > 4 * 1024 * 1024)
            throw new ConfigurationException("Payload manifest is missing or too large.");
        var manifestBytes = ReadBounded(manifestEntry);
        using var manifest = JsonDocument.Parse(manifestBytes);
        var root = manifest.RootElement;
        RequireKeys(root, "schemaVersion", "product", "version", "rid", "sourceCommit", "contractVersion", "files");
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("product").GetString() != "ArchSift.InProject" ||
            root.GetProperty("rid").GetString() != "win-x64" || root.GetProperty("contractVersion").GetInt32() != 1)
            throw new ConfigurationException("Unsupported InProject package identity.");
        var version = root.GetProperty("version").GetString() ?? "";
        var commit = root.GetProperty("sourceCommit").GetString() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(version, "^0\\.1\\.0(?:-[0-9A-Za-z.-]+)?$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(commit, "^[0-9a-f]{40}$"))
            throw new ConfigurationException("Unsupported package version or source commit.");
        var listed = new Dictionary<string, PayloadFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in root.GetProperty("files").EnumerateArray())
        {
            RequireKeys(file, "path", "bytes", "sha256");
            var name = file.GetProperty("path").GetString() ?? "";
            ValidateRelative(name);
            if (name == "payload-manifest.json" || !AllowedPayload(name))
                throw new ConfigurationException("Unexpected package payload path: " + name);
            var bytes = file.GetProperty("bytes").GetInt64();
            var sha = file.GetProperty("sha256").GetString() ?? "";
            if (bytes < 0 || bytes > MaximumFileBytes || !ValidHash(sha) || !listed.TryAdd(name, new(name, bytes, sha)))
                throw new ConfigurationException("Invalid package payload identity.");
        }
        if (listed.Count == 0 || listed.Count != entries.Count - 1 ||
            listed.Keys.Any(name => !entries.ContainsKey(name)))
            throw new ConfigurationException("Payload manifest does not cover exactly the archive entries.");
        foreach (var name in entries.Keys)
        {
            var parent = name;
            while (parent.Contains('/'))
            {
                parent = parent[..parent.LastIndexOf('/')];
                if (entries.ContainsKey(parent)) throw new ConfigurationException("Package file/directory path collision.");
            }
        }
        foreach (var (name, file) in listed)
        {
            var entry = entries[name];
            if (entry.Length != file.Bytes || HashBytes(ReadBounded(entry)) != file.Sha256)
                throw new ConfigurationException("Package payload mismatch: " + name);
        }
        if (Components.Values.Any(path => !listed.ContainsKey(path)) ||
            !listed.ContainsKey("tests/ArchSift.InProject.ArchitectureTests.csproj") ||
            !listed.ContainsKey("bootstrap/archsift-inproject.ps1") ||
            !listed.Keys.Any(path => path.StartsWith("rules/", StringComparison.Ordinal)))
            throw new ConfigurationException("Required runtime, test, bootstrap or rule payload is absent.");
        return new(path, expectedSha256, HashBytes(manifestBytes), version, commit,
            listed.Values.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray());
    }

    public static void Extract(LocalPackageIdentity identity, string destination)
    {
        var verified = Inspect(identity.Path, identity.PackageSha256);
        if (verified.ManifestSha256 != identity.ManifestSha256 || !verified.Files.SequenceEqual(identity.Files))
            throw new ConfigurationException("Package changed after inspection.");
        using var archive = ZipFile.OpenRead(identity.Path);
        var entries = archive.Entries.ToDictionary(e => e.FullName, StringComparer.OrdinalIgnoreCase);
        foreach (var file in identity.Files)
        {
            var target = Path.GetFullPath(file.Path.Replace('/', Path.DirectorySeparatorChar), destination);
            if (!PathSafety.IsUnder(target, destination)) throw new ConfigurationException("Payload escapes staging root.");
            PathSafety.EnsureNoLinks(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using (var source = entries[file.Path].Open())
            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(output);
                output.Flush(true);
                if (output.Length != file.Bytes) throw new ConfigurationException("Extracted payload size mismatch.");
            }
            if (HashFile(target) != file.Sha256)
                throw new ConfigurationException("Extracted payload differs from manifest.");
        }
        var manifestTarget = Path.Combine(destination, "runtime", "payload-manifest.json");
        using (var sourceManifest = entries["payload-manifest.json"].Open())
        using (var outputManifest = new FileStream(manifestTarget, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            sourceManifest.CopyTo(outputManifest);
            outputManifest.Flush(true);
        }
        if (HashFile(manifestTarget) != identity.ManifestSha256)
            throw new ConfigurationException("Extracted manifest mismatch.");
    }

    public static string VerifyInstalled(string targetRoot)
    {
        var worktree = WorktreeLocator.Locate(targetRoot);
        var installation = Path.Combine(worktree.Root, ".archsift");
        var lockPath = Path.Combine(installation, "version.lock.json");
        var manifestPath = Path.Combine(installation, "runtime", "payload-manifest.json");
        PathSafety.EnsureNoLinks(lockPath);
        PathSafety.EnsureNoLinks(manifestPath);
        if (!File.Exists(lockPath) || !File.Exists(manifestPath))
            throw new ConfigurationException("Installed version lock or payload manifest is missing.");
        using var locked = JsonDocument.Parse(File.ReadAllBytes(lockPath));
        var lockRoot = locked.RootElement;
        RequireKeys(lockRoot, "schemaVersion", "product", "version", "rid", "sourceCommit", "contractVersion",
            "packageSha256", "payloadManifestSha256", "components");
        if (lockRoot.GetProperty("schemaVersion").GetInt32() != 1 ||
            lockRoot.GetProperty("product").GetString() != "ArchSift.InProject" ||
            lockRoot.GetProperty("rid").GetString() != "win-x64" ||
            lockRoot.GetProperty("contractVersion").GetInt32() != 1 ||
            !System.Text.RegularExpressions.Regex.IsMatch(lockRoot.GetProperty("version").GetString() ?? "",
                "^0\\.1\\.0(?:-[0-9A-Za-z.-]+)?$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(lockRoot.GetProperty("sourceCommit").GetString() ?? "", "^[0-9a-f]{40}$") ||
            !ValidHash(lockRoot.GetProperty("packageSha256").GetString() ?? ""))
            throw new ConfigurationException("Installed version lock identity is invalid.");
        var manifestSha = lockRoot.GetProperty("payloadManifestSha256").GetString() ?? "";
        if (!ValidHash(manifestSha) || HashFile(manifestPath) != manifestSha)
            throw new ConfigurationException("Installed payload manifest SHA-256 mismatch.");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        var manifestRoot = manifest.RootElement;
        RequireKeys(manifestRoot, "schemaVersion", "product", "version", "rid", "sourceCommit", "contractVersion", "files");
        foreach (var key in new[] { "schemaVersion", "product", "version", "rid", "sourceCommit", "contractVersion" })
            if (manifestRoot.GetProperty(key).GetRawText() != lockRoot.GetProperty(key).GetRawText())
                throw new ConfigurationException("Installed manifest and version lock disagree: " + key);
        var runtimeFiles = new Dictionary<string, PayloadFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in manifestRoot.GetProperty("files").EnumerateArray())
        {
            RequireKeys(item, "path", "bytes", "sha256");
            var name = item.GetProperty("path").GetString() ?? "";
            ValidateRelative(name);
            if (!AllowedPayload(name)) throw new ConfigurationException("Unknown installed payload path.");
            if (!name.StartsWith("runtime/", StringComparison.Ordinal)) continue;
            var bytes = item.GetProperty("bytes").GetInt64();
            var sha = item.GetProperty("sha256").GetString() ?? "";
            if (bytes < 0 || !ValidHash(sha) || !runtimeFiles.TryAdd(name, new(name, bytes, sha)))
                throw new ConfigurationException("Invalid installed runtime entry.");
        }
        foreach (var file in runtimeFiles.Values)
        {
            var path = Path.GetFullPath(file.Path.Replace('/', Path.DirectorySeparatorChar), installation);
            if (!PathSafety.IsUnder(path, Path.Combine(installation, "runtime")))
                throw new ConfigurationException("Installed runtime path escapes ownership.");
            PathSafety.EnsureNoLinks(path);
            if (!File.Exists(path) || new FileInfo(path).Length != file.Bytes || HashFile(path) != file.Sha256)
                throw new ConfigurationException("Installed runtime payload changed: " + file.Path);
        }
        var actual = SafeFiles(Path.Combine(installation, "runtime"))
            .Select(path => Path.GetRelativePath(installation, path).Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expected = runtimeFiles.Keys.Append("runtime/payload-manifest.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actual.SetEquals(expected)) throw new ConfigurationException("Unlisted file in installed runtime.");
        var components = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in lockRoot.GetProperty("components").EnumerateArray())
        {
            RequireKeys(item, "id", "sha256");
            var id = item.GetProperty("id").GetString() ?? "";
            var sha = item.GetProperty("sha256").GetString() ?? "";
            if (!Components.ContainsKey(id) || !ValidHash(sha) || !components.TryAdd(id, sha))
                throw new ConfigurationException("Invalid installed component identity.");
        }
        if (components.Count != Components.Count || Components.Any(pair =>
                !runtimeFiles.TryGetValue(pair.Value, out var file) || components[pair.Key] != file.Sha256))
            throw new ConfigurationException("Installed component hashes differ from the version lock.");
        return lockRoot.GetProperty("version").GetString() ?? throw new ConfigurationException("Installed version is missing.");
    }

    private static bool AllowedPayload(string name) => name.StartsWith("runtime/", StringComparison.Ordinal) ||
        name.StartsWith("rules/", StringComparison.Ordinal) || name.StartsWith("tests/", StringComparison.Ordinal) ||
        name.StartsWith("bootstrap/", StringComparison.Ordinal);

    private static IEnumerable<string> SafeFiles(string directory)
    {
        PathSafety.EnsureNoLinks(directory);
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            PathSafety.EnsureNoLinks(entry);
            if (Directory.Exists(entry))
            {
                foreach (var nested in SafeFiles(entry)) yield return nested;
            }
            else yield return entry;
        }
    }

    private static void ValidateRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Contains(':') || path.StartsWith('/') ||
            path.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith(' ') || part.EndsWith('.')))
            throw new ConfigurationException("Invalid package path: " + path);
    }

    private static void RequireKeys(JsonElement element, params string[] keys)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new ConfigurationException("Expected package object.");
        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != keys.Length || actual.Distinct(StringComparer.Ordinal).Count() != keys.Length ||
            keys.Any(key => !actual.Contains(key, StringComparer.Ordinal)))
            throw new ConfigurationException("Unexpected package metadata fields.");
    }

    private static byte[] ReadBounded(ZipArchiveEntry entry)
    {
        using var source = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920]; int read;
        while ((read = source.Read(chunk)) > 0)
        {
            if (buffer.Length + read > entry.Length) throw new ConfigurationException("ZIP entry exceeds declared size.");
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length != entry.Length) throw new ConfigurationException("Truncated ZIP entry.");
        return buffer.ToArray();
    }

    private static bool ValidHash(string value) => System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-9a-f]{64}$");
    private static string HashBytes(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
