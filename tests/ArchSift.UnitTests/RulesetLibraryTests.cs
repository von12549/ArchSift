using System.Text;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class RulesetLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "archsift-library-" + Guid.NewGuid().ToString("N"));
    private RulesetLibrary Library => new(Path.Combine(root, "rules"), Path.Combine(root, "target"));
    [Fact]
    public void ImportExportRetainsBomBytesAndConflictsDoNotMutateSavedLibrary()
    {
        var bytes = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(RuleTemplates.Json("naming"))).ToArray();
        var card = Library.Import("policy.json", bytes);
        Assert.Equal(ContentHash.Bytes(bytes), card.Identity!.Sha256);
        Assert.Equal(bytes, Library.Export(card.EntryId));
        var registry = File.ReadAllBytes(Path.Combine(root, "rules", ".archsift-library.json"));
        Assert.Throws<ConfigurationException>(() => Library.Import("POLICY.json", bytes));
        Assert.Throws<ConfigurationException>(() => Library.Import("other.json", bytes));
        Assert.Throws<ConfigurationException>(() => Library.Import("invalid.json", Encoding.UTF8.GetBytes("{}")));
        Assert.Equal(registry, File.ReadAllBytes(Path.Combine(root, "rules", ".archsift-library.json")));
        Assert.Equal(bytes, Library.Export(card.EntryId));
        Assert.Single(Library.List());
        Assert.False(File.Exists(Path.Combine(root, "rules", "other.json")));
        Assert.False(Directory.Exists(Path.Combine(root, "target")));
    }
    [Fact]
    public void SavedEditsKeepIdentityButDeletionAndReimportDoNotRebind()
    {
        var bytes = Encoding.UTF8.GetBytes(RuleTemplates.Json("naming"));
        var first = Library.Import("policy.json", bytes);
        var edited = Library.Save("policy.json", Encoding.UTF8.GetBytes(RuleTemplates.Json("naming").Replace("1.0.0", "2.0.0", StringComparison.Ordinal)));
        Assert.Equal(first.EntryId, edited.EntryId);
        Assert.NotEqual(first.Identity!.Sha256, edited.Identity!.Sha256);
        Library.Delete(first.EntryId);
        var reimported = Library.Import("policy.json", bytes);
        Assert.NotEqual(first.EntryId, reimported.EntryId);
        Assert.True(Library.Find(first.EntryId)!.Deleted);
        Assert.Throws<ConfigurationException>(() => Library.Capture(first.EntryId));
    }
    [Fact]
    public void TargetContainmentAndTraversalAreRejected()
    {
        Assert.Throws<ConfigurationException>(() => new RulesetLibrary(Path.Combine(root, "target", "rules"), Path.Combine(root, "target")));
        Assert.Throws<ConfigurationException>(() => Library.Import("../policy.json", Encoding.UTF8.GetBytes(RuleTemplates.Json("naming"))));
    }
    public void Dispose()
    {
        if (Directory.Exists(root)) { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); Directory.Delete(root, true); }
    }
}
