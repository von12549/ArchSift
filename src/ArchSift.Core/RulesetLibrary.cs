using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

/// <summary>External saved-byte library. Tombstones make deletion/reimport an explicit identity change.</summary>
public sealed class RulesetLibrary
{
    public string DirectoryPath { get; }
    private readonly string targetRoot;
    public RulesetLibrary(string directory, string target)
    {
        DirectoryPath = Path.GetFullPath(directory); targetRoot = Path.GetFullPath(target);
        PathSafety.EnsureDisjoint(targetRoot, DirectoryPath);
    }

    public LibraryCard[] List() => Locked(registry => registry.Entries.Where(e => !e.Deleted).Select(e =>
    {
        try { var rules = Capture(e); return new LibraryCard(e.EntryId, e.FileName, rules.Ruleset.Id, true, rules.Identity, rules.Ruleset, null); }
        catch (Exception error) when (error is IOException or ConfigurationException or UnauthorizedAccessException)
        { return new LibraryCard(e.EntryId, e.FileName, e.RulesetId, false, null, null, error.Message); }
    }).ToArray());

    public LibraryEntry? Find(string entryId) => Locked(registry => registry.Entries.FirstOrDefault(e => e.EntryId == entryId));
    public LibraryEntry[] References() => Locked(registry => registry.Entries.ToArray());
    public LoadedRuleset Capture(string entryId) => Locked(registry => Capture(registry.Entries.FirstOrDefault(e => e.EntryId == entryId)
        ?? throw new ConfigurationException("Missing library entry: " + entryId)));
    public byte[] Export(string entryId) => Locked(registry =>
    {
        var entry = registry.Entries.FirstOrDefault(e => e.EntryId == entryId && !e.Deleted)
            ?? throw new ConfigurationException("Missing library entry: " + entryId);
        return File.ReadAllBytes(FilePath(entry.FileName));
    });

    public LibraryCard Import(string name, byte[] bytes) => Save(name, bytes, false);
    public LibraryCard Save(string name, byte[] bytes, bool allowUpdate = true)
    {
        var path = FilePath(name);
        var parsed = RuleLoader.Parse(bytes);
        RuleLoader.Compose([new(parsed, new(parsed.Id, parsed.Version, ContentHash.Bytes(bytes), path))]);
        return Locked(registry =>
        {
            var previous = registry.Entries.FirstOrDefault(e => !e.Deleted && e.FileName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!allowUpdate && (previous is not null || File.Exists(path)))
                throw new ConfigurationException("Import filename conflict: " + name);
            if (registry.Entries.Any(e => !e.Deleted && e.RulesetId == parsed.Id && e.EntryId != previous?.EntryId))
                throw new ConfigurationException("Ruleset ID conflict: " + parsed.Id);
            var entry = new LibraryEntry(previous?.EntryId ?? Guid.NewGuid().ToString("N"), name, parsed.Id, false);
            var entries = registry.Entries.Where(e => e.EntryId != entry.EntryId).Append(entry).ToArray();
            var oldBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
            AtomicFile(path, bytes);
            try { WriteRegistry(new(1, entries)); }
            catch { if (oldBytes is null) File.Delete(path); else AtomicFile(path, oldBytes); throw; }
            return new LibraryCard(entry.EntryId, name, parsed.Id, true, new(parsed.Id, parsed.Version, ContentHash.Bytes(bytes), path), parsed, null);
        });
    }

    public void Delete(string entryId) => Locked(registry =>
    {
        var entry = registry.Entries.FirstOrDefault(e => e.EntryId == entryId && !e.Deleted)
            ?? throw new ConfigurationException("Missing library entry: " + entryId);
        var path = FilePath(entry.FileName);
        var bytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
        if (bytes is not null) File.Delete(path);
        try { WriteRegistry(new(1, registry.Entries.Select(e => e.EntryId == entryId ? e with { Deleted = true } : e).ToArray())); }
        catch { if (bytes is not null) AtomicFile(path, bytes); throw; }
        return true;
    });

    private LoadedRuleset Capture(LibraryEntry entry)
    {
        if (entry.Deleted) throw new ConfigurationException("Deleted library entry: " + entry.EntryId);
        var loaded = RuleLoader.Load(FilePath(entry.FileName));
        RuleLoader.Compose([loaded]);
        return loaded;
    }
    private T Locked<T>(Func<LibraryRegistry, T> action)
    {
        PathSafety.EnsureDisjoint(targetRoot, DirectoryPath); Directory.CreateDirectory(DirectoryPath);
        var lockPath = Path.Combine(DirectoryPath, ".archsift-library.lock"); PathSafety.EnsureNoLinks(lockPath);
        using var fileLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        return action(ReadRegistry());
    }
    private LibraryRegistry ReadRegistry()
    {
        var path = Path.Combine(DirectoryPath, ".archsift-library.json"); PathSafety.EnsureNoLinks(path);
        if (File.Exists(path))
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > RuleLoader.MaximumJsonBytes) throw new ConfigurationException("Library registry exceeds 4 MiB.");
            using var doc = JsonDocument.Parse(bytes); SchemaValidation.Validate(doc.RootElement, "library");
            var registry = doc.RootElement.Deserialize<LibraryRegistry>(JsonContract.Options)!;
            if (registry.Entries.Select(e => e.EntryId).Distinct(StringComparer.Ordinal).Count() != registry.Entries.Length ||
                registry.Entries.Where(e => !e.Deleted).Select(e => e.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != registry.Entries.Count(e => !e.Deleted))
                throw new ConfigurationException("Duplicate library identity or filename.");
            foreach (var entry in registry.Entries) FilePath(entry.FileName);
            return registry;
        }
        var entries = new List<LibraryEntry>();
        foreach (var file in Directory.GetFiles(DirectoryPath, "*.json").Where(p => !Path.GetFileName(p).StartsWith('.')).Order(StringComparer.Ordinal))
        {
            var rules = RuleLoader.Load(FilePath(Path.GetFileName(file))); RuleLoader.Compose([rules]);
            if (entries.Any(e => e.RulesetId == rules.Ruleset.Id)) throw new ConfigurationException("Existing library ruleset ID conflict: " + rules.Ruleset.Id);
            entries.Add(new(Guid.NewGuid().ToString("N"), Path.GetFileName(file), rules.Ruleset.Id, false));
        }
        var created = new LibraryRegistry(1, entries.ToArray()); WriteRegistry(created); return created;
    }
    private void WriteRegistry(LibraryRegistry registry) => AtomicFile(Path.Combine(DirectoryPath, ".archsift-library.json"), JsonSerializer.SerializeToUtf8Bytes(registry, JsonContract.Options));
    public string FilePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name.StartsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new ConfigurationException("Use a JSON filename in the managed rules directory.");
        return PathSafety.Under(DirectoryPath, name);
    }
    internal static void AtomicFile(string path, byte[] bytes)
    {
        PathSafety.EnsureNoLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".save-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
