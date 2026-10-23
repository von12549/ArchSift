using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Hosting;

public sealed record WebHostCommand(string Executable, string? Assembly);
public sealed record WebPayloadEntry(string Path, long Bytes, string Sha256);
public sealed record WebPayloadInventory(int SchemaVersion, string Version, string SourceCommit, WebPayloadEntry[] Entries);

/// <summary>Resolves the matching adjacent publish closure for distributed tools.</summary>
public static class WebPayload
{
    public const string InventoryName = "web-payload.json";
    public static WebHostCommand Resolve(string directory, string expectedVersion, bool requireInventory = true)
    {
        directory = Path.GetFullPath(directory);
        PathSafety.EnsureNoLinks(directory);
        if (requireInventory) Verify(directory, expectedVersion);
        var assembly = Path.Combine(directory, "ArchSift.Web.dll");
        if (!File.Exists(assembly)) throw new IOException("Matching Web payload is missing; restore the complete approved Workbench package.");
        var native = Path.Combine(directory, OperatingSystem.IsWindows() ? "ArchSift.Web.exe" : "ArchSift.Web");
        if (File.Exists(native)) return new(native, null);
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrEmpty(host) || !Path.IsPathFullyQualified(host) || !File.Exists(host))
        {
            var runtime = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            host = Path.GetFullPath(Path.Combine(runtime, "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        }
        if (!File.Exists(host)) throw new IOException("An explicit compatible dotnet host with Core and ASP.NET 10.0.13 is required for Web.");
        PathSafety.EnsureNoLinks(host);
        return new(host, assembly);
    }

    public static void Verify(string directory, string expectedVersion)
    {
        directory = Path.GetFullPath(directory); PathSafety.EnsureNoLinks(directory);
        var path = Path.Combine(directory, InventoryName); PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024)
            throw new IOException("Web payload inventory is missing or too large; restore the approved package.");
        var inventory = JsonSerializer.Deserialize<WebPayloadInventory>(File.ReadAllBytes(path), JsonContract.Options);
        if (inventory is null || inventory.SchemaVersion != 1 || inventory.Version != expectedVersion ||
            inventory.SourceCommit is null || !System.Text.RegularExpressions.Regex.IsMatch(inventory.SourceCommit, "^[a-f0-9]{40}$") ||
            inventory.Entries is null || inventory.Entries.Length is < 1 or > 10000)
            throw new IOException("Unsupported or mismatched Web payload identity.");
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in inventory.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Path) || entry.Path.Contains('\\') || entry.Path.Contains(':') ||
                entry.Path.Split('/').Any(p => p is "" or "." or ".." || p.EndsWith(' ') || p.EndsWith('.')) ||
                Path.IsPathRooted(entry.Path) || entry.Path == InventoryName || !entries.Add(entry.Path) || entry.Bytes < 0)
                throw new IOException("Unsafe or duplicate Web inventory path.");
            var file = PathSafety.Under(directory, entry.Path); PathSafety.EnsureNoLinks(file);
            if (!File.Exists(file) || new FileInfo(file).Length != entry.Bytes) throw new IOException("Web payload file is missing or changed: " + entry.Path);
            using var stream = File.OpenRead(file);
            if (Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() != entry.Sha256)
                throw new IOException("Web payload hash differs: " + entry.Path);
        }
        void Walk(string folder)
        {
            foreach (var item in Directory.EnumerateFileSystemEntries(folder))
            {
                PathSafety.EnsureNoLinks(item);
                if (Directory.Exists(item)) Walk(item);
                else
                {
                    var relative = Path.GetRelativePath(directory, item).Replace('\\', '/');
                    if (relative != InventoryName && !entries.Contains(relative)) throw new IOException("Unlisted Web payload file: " + relative);
                }
            }
        }
        Walk(directory);
        foreach (var required in new[] { "ArchSift.Web.dll", "ArchSift.Web.deps.json", "ArchSift.Web.runtimeconfig.json",
            "wwwroot/index.html", "wwwroot/launcher.html", "wwwroot/test-workbench.js", "notices/third-party-notices.md" })
            if (!entries.Contains(required)) throw new IOException("Incomplete Web publish closure: " + required);
        foreach (var entry in inventory.Entries.Where(e => !e.Path.Contains('/') &&
            e.Path.StartsWith("ArchSift.", StringComparison.Ordinal) && e.Path.EndsWith(".dll", StringComparison.Ordinal)))
        {
            var product = FileVersionInfo.GetVersionInfo(Path.Combine(directory, entry.Path)).ProductVersion;
            if (product != expectedVersion + "+" + inventory.SourceCommit) throw new IOException("Mixed Web product assembly version: " + entry.Path);
        }
    }
}
