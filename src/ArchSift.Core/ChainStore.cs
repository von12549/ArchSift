using System.Text.Json;
using System.Text.RegularExpressions;
using ArchSift.Contracts;

namespace ArchSift.Core;

public sealed class ChainStore(RulesetLibrary library)
{
    private string DirectoryPath => Path.Combine(library.DirectoryPath, "chains");
    public ChainDocument[] List() => Directory.Exists(DirectoryPath)
        ? Directory.GetFiles(DirectoryPath, "*.json").Order(StringComparer.Ordinal).Select(Load).ToArray() : [];
    public ChainDocument Read(string id) => Load(FilePath(id));
    public void Save(string id, byte[] bytes)
    {
        var parsed = Parse(bytes);
        if (parsed.Id != id) throw new ConfigurationException("Chain ID does not match the filename.");
        var path = FilePath(id);
        var previous = File.Exists(path) ? Load(path).Entries.Select(e => e.EntryId).ToHashSet(StringComparer.Ordinal) : [];
        foreach (var entry in parsed.Entries)
        {
            var found = library.Find(entry.EntryId);
            if ((found is null || found.Deleted || !File.Exists(library.FilePath(found.FileName))) && !previous.Contains(entry.EntryId))
                throw new ConfigurationException("New chain entries must select an existing saved library ruleset: " + entry.EntryId);
        }
        RulesetLibrary.AtomicFile(path, bytes);
    }
    public void Delete(string id) { var path = FilePath(id); if (!File.Exists(path)) throw new ConfigurationException("Missing chain."); File.Delete(path); }
    public static ChainDocument Load(string path) { PathSafety.EnsureNoLinks(path); return Parse(File.ReadAllBytes(path)); }
    public static ChainDocument Parse(byte[] bytes)
    {
        if (bytes.Length > RuleLoader.MaximumJsonBytes) throw new ConfigurationException("Chain exceeds 4 MiB.");
        using var doc = JsonDocument.Parse(bytes); SchemaValidation.Validate(doc.RootElement, "chain");
        var chain = doc.RootElement.Deserialize<ChainDocument>(JsonContract.Options)!;
        ValidateId(chain.Id);
        if (string.IsNullOrWhiteSpace(chain.Version) || chain.Entries.Length == 0 ||
            chain.Entries.Select(e => e.EntryId).Distinct(StringComparer.Ordinal).Count() != chain.Entries.Length)
            throw new ConfigurationException("Chain needs a nonblank version and unique, nonempty entries.");
        return chain;
    }
    private string FilePath(string id) { ValidateId(id); return PathSafety.Under(DirectoryPath, id + ".json"); }
    private static void ValidateId(string id)
    {
        if (!Regex.IsMatch(id, "^[a-zA-Z0-9_-]{1,100}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            throw new ConfigurationException("Chain ID must use 1–100 letters, digits, underscores or hyphens.");
    }
}
