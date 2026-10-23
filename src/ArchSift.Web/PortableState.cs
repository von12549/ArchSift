using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Setup;

namespace ArchSift.Web;

public sealed record PortableMarker(int SchemaVersion, string Kind, string Root, string InitialConfigPath);
public static class PortableState
{
    public const string MarkerName = "workbench-state.json";
    private static void EnsureExternalRoot(string root)
    {
        foreach (var protectedRoot in ArchSift.Hosting.WebPayload.ProtectedHostRoots()) PathSafety.EnsureDisjoint(root, protectedRoot);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (File.Exists(Path.Combine(cursor.FullName, "install.json")))
                throw new ConfigurationException("Portable state conflicts with an install.json ancestor; use a disjoint external root.");
    }
    public static PortableMarker Read(string root)
    {
        root = SetupFiles.Safe(root);
        EnsureExternalRoot(root);
        if (File.Exists(SetupFiles.Under(root, "install.json")))
            throw new ConfigurationException("Ambiguous managed/portable root; preserve both markers for review.");
        var path = SetupFiles.Under(root, MarkerName);
        if (!File.Exists(path)) throw new ConfigurationException("Unknown Launcher root. Initialize explicitly: archsift launch --root <external-state-root> --config <existing-json>.");
        if (new FileInfo(path).Length > 16384) throw new ConfigurationException("Portable marker exceeds its limit.");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        SetupFiles.CheckKeys(doc.RootElement);
        if (doc.RootElement.ValueKind != JsonValueKind.Object ||
            doc.RootElement.EnumerateObject().Any(p => !new[] { "schemaVersion", "kind", "root", "initialConfigPath" }.Contains(p.Name, StringComparer.Ordinal)))
            throw new ConfigurationException("Unknown portable marker fields.");
        var marker = doc.RootElement.Deserialize<PortableMarker>(JsonContract.Options);
        if (marker is null || marker.SchemaVersion != 1 || marker.Kind != "ArchSift.Workbench.Portable" ||
            marker.Root != root || !Path.IsPathFullyQualified(marker.InitialConfigPath) ||
            marker.InitialConfigPath != SetupFiles.Safe(marker.InitialConfigPath))
            throw new ConfigurationException("Invalid portable state identity/schema.");
        return marker;
    }

    public static void Initialize(string root, string configPath)
    {
        root = SetupFiles.Safe(root); configPath = SetupFiles.Safe(configPath);
        EnsureExternalRoot(root);
        if (File.Exists(SetupFiles.Under(root, "install.json"))) throw new ConfigurationException("Explicit portable initialization cannot modify a managed root.");
        if (File.Exists(SetupFiles.Under(root, MarkerName)))
        {
            var existing = Read(root);
            if (!existing.InitialConfigPath.Equals(configPath, PathSafety.Comparison)) throw new ConfigurationException("Portable root already references another initial configuration; register profiles explicitly.");
            var store = new ProfileStore(root);
            using var active = new FileStream(SetupFiles.Under(root, ".archsift-workbench.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            store.ValidateConfiguration(configPath, ConfigLoader.Load(configPath));
            return;
        }
        if (File.Exists(root) || Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new ConfigurationException("Portable initialization requires an absent or empty root; unknown files are preserved.");
        if (new FileInfo(configPath).Length > 4 * 1024 * 1024) throw new ConfigurationException("Configuration exceeds 4 MiB.");
        var configHash = SetupFiles.FileHash(configPath);
        var config = ConfigLoader.Load(configPath);
        ProfileStore.ForInitialization(root, configPath).ValidateConfiguration(configPath, config);
        SetupService.ValidateState(config);
        var parent = Path.GetDirectoryName(root) ?? throw new ConfigurationException("A volume root cannot be portable state.");
        if (!Directory.Exists(parent)) throw new ConfigurationException("Portable state parent must already exist.");
        var stage = SetupFiles.Safe(root + ".archsift-bootstrap");
        PathSafety.EnsureDisjoint(stage, config.Target.Root);
        PathSafety.EnsureDisjoint(stage, configPath);
        PathSafety.EnsureDisjoint(stage, SetupService.Library(config));
        PathSafety.EnsureDisjoint(stage, config.Output.Directory);
        var leasePath = SetupFiles.Safe(root + ".archsift-bootstrap.lock");
        foreach (var protectedPath in new[] { configPath, config.Target.Root, SetupService.Library(config), config.Output.Directory })
            PathSafety.EnsureDisjoint(leasePath, protectedPath);
        using var lease = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var marker = new PortableMarker(1, "ArchSift.Workbench.Portable", root, configPath);
        var id = ContentHash.Text(configPath)[..32];
        var expected = new Dictionary<string, byte[]>(StringComparer.Ordinal) {
            ["workbench-state.json"] = JsonSerializer.SerializeToUtf8Bytes(marker, JsonContract.Options),
            ["config/profiles.json"] = JsonSerializer.SerializeToUtf8Bytes(new ProfileCatalog(1, id, [new(id, Path.GetFileName(configPath), configPath)]), JsonContract.Options),
            ["config/selection.json"] = JsonSerializer.SerializeToUtf8Bytes(new ProfileSelection(1, id), JsonContract.Options),
            ["bootstrap.json"] = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, root, configPath, configSha256 = configHash }, JsonContract.Options)
        };
        if (Directory.Exists(stage))
        {
            // Recovery accepts only a bounded, byte-identical partial transaction for this exact config.
            void Inspect(string folder)
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
                {
                    SetupFiles.Safe(entry);
                    var relative = Path.GetRelativePath(stage, entry).Replace('\\', '/');
                    if (Directory.Exists(entry))
                    {
                        if (relative != "config") throw new ConfigurationException("Unknown bootstrap directory; preserve for review.");
                        Inspect(entry);
                    }
                    else if (!expected.TryGetValue(relative, out var bytes) || !File.ReadAllBytes(entry).AsSpan().SequenceEqual(bytes))
                        throw new ConfigurationException("Interrupted bootstrap differs; preserve it for review.");
                }
            }
            Inspect(stage);
        }
        Directory.CreateDirectory(stage);
        foreach (var entry in expected)
            if (!File.Exists(SetupFiles.Under(stage, entry.Key))) SetupFiles.AtomicBytes(SetupFiles.Under(stage, entry.Key), entry.Value, false);
        if (SetupFiles.FileHash(configPath) != configHash) throw new ConfigurationException("Configuration changed during bootstrap.");
        if (Directory.Exists(root)) Directory.Delete(root, recursive: false); // Only the explicitly checked empty root.
        SetupFiles.Safe(root); Directory.Move(stage, root); // Atomic create; never merges or replaces files.
    }
}
