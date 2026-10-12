using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class CreateFromTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "archsift-copy-" + Guid.NewGuid().ToString("N"));
    private RulesetLibrary Library => new(Path.Combine(root, "rules"), Path.Combine(root, "source"));
    private static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, JsonContract.Options);
    private static Ruleset Policy(string id)
    {
        var template = RuleLoader.Parse(System.Text.Encoding.UTF8.GetBytes(RuleTemplates.Json("naming")));
        return template with { Id = id, Rules = Enumerable.Range(0, 406).Select(i => template.Rules[0] with { Id = "rule-" + i.ToString("D3") }).ToArray() };
    }
    [Fact]
    public void LargeSavedRulesetCopiesIndependentlyAndCaseConflictsPreserveRegistry()
    {
        var original = Bytes(Policy("Original")); var source = Library.Import("original.json", original);
        var copy = Library.Save("copy.json", Bytes(Library.Capture(source.EntryId).Ruleset with { Id = "Copy", Version = "2.0.0" }), false);
        Assert.NotEqual(source.EntryId, copy.EntryId); Assert.Equal(original, Library.Export(source.EntryId));
        Assert.Equal(406, copy.Ruleset!.Rules.Length); Assert.Equal(source.Ruleset!.Rules.Select(r => r.Id), copy.Ruleset.Rules.Select(r => r.Id));
        var registryPath = Path.Combine(root, "rules", ".archsift-library.json"); var registry = File.ReadAllBytes(registryPath);
        Assert.Throws<ConfigurationException>(() => Library.Save("COPY.json", Bytes(Policy("Other")), false));
        Assert.Throws<ConfigurationException>(() => Library.Save("other.json", Bytes(Policy("copy")), false));
        Assert.Equal(registry, File.ReadAllBytes(registryPath)); Assert.Equal(original, Library.Export(source.EntryId));
        Assert.False(File.Exists(Path.Combine(root, "rules", "other.json")));
    }
    [Fact]
    public async Task ConcurrentCreatesHaveOneWinnerAndCannotOverwriteSavedBytes()
    {
        Library.Import("source.json", Bytes(Policy("source")));
        var first = Bytes(Policy("first")); var second = Bytes(Policy("second"));
        var outcomes = await Task.WhenAll(new[] { first, second }.Select(bytes => Task.Run(() =>
        {
            try { Library.Save("copy.json", bytes, false); return true; }
            catch (ConfigurationException) { return false; }
        })));
        Assert.Equal(1, outcomes.Count(x => x)); var saved = File.ReadAllBytes(Path.Combine(root, "rules", "copy.json"));
        Assert.True(saved.SequenceEqual(first) || saved.SequenceEqual(second)); Assert.Equal(2, Library.List().Length);
    }
    [Fact]
    public async Task ConcurrentLibraryReadWaitsForAnOwnedRegistryTransaction()
    {
        Library.Import("source.json", Bytes(Policy("source")));
        var fileLock = new FileStream(Path.Combine(root, "rules", ".archsift-library.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Task<LibraryCard[]>? pending = null;
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Task.Run(() => { started.SetResult(); return Library.List(); });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(100);
            Assert.False(pending.IsCompleted);
        }
        finally { fileLock.Dispose(); }
        Assert.Equal("source", Assert.Single(await pending!.WaitAsync(TimeSpan.FromSeconds(5))).RulesetId);
    }
    [Fact]
    public async Task ChainCopiesRetainOrderedStableReferencesAndRejectDuplicateOrMissingCreates()
    {
        var a = Library.Import("a.json", Bytes(Policy("a"))); var b = Library.Import("b.json", Bytes(Policy("b")));
        var store = new ChainStore(Library); var source = new ChainDocument(1, "source", "1", "Original", [new(a.EntryId), new(b.EntryId)]);
        store.Save(source.Id, Bytes(source), false); var sourcePath = Path.Combine(root, "rules", "chains", "source.json"); var original = File.ReadAllBytes(sourcePath);
        var copy = store.Read(source.Id) with { Id = "copy", Version = "2", Entries = [new(b.EntryId), new(a.EntryId)] };
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            try { store.Save(copy.Id, Bytes(copy), false); return true; } catch (ConfigurationException) { return false; }
        })));
        Assert.Equal(1, outcomes.Count(x => x)); Assert.Equal(original, File.ReadAllBytes(sourcePath)); Assert.Equal(copy.Entries, store.Read(copy.Id).Entries);
        Assert.Throws<ConfigurationException>(() => store.Save("COPY", Bytes(copy with { Id = "COPY" }), false));
        Library.Delete(a.EntryId);
        Assert.Throws<ConfigurationException>(() => store.Save("missing-copy", Bytes(source with { Id = "missing-copy" }), false));
        Assert.False(File.Exists(Path.Combine(root, "rules", "chains", "missing-copy.json")));
        Assert.Equal(original, File.ReadAllBytes(sourcePath)); Assert.Equal(source.Entries, store.Read(source.Id).Entries);
    }
    public void Dispose() { if (Directory.Exists(root)) { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); Directory.Delete(root, true); } }
    [Fact]
    public void DependencyOnlyManifestWithNoProjectCannotClaimCurrentSourceBinding()
    {
        var source = Path.Combine(root, "source"); var evidence = Path.Combine(root, "evidence"); Directory.CreateDirectory(source); Directory.CreateDirectory(evidence);
        File.WriteAllText(Path.Combine(source, "Orphan.cs"), "class Orphan {}");
        var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = source }, Build = new() { TargetFramework = "net10.0", AssemblyManifest = Path.Combine(evidence, "manifest.json") }, Output = new() { Directory = Path.Combine(root, "reports") } };
        var snapshot = ProjectDiscovery.Discover(source, targetFramework: "net10.0"); var dll = Path.Combine(evidence, "Core.dll"); File.Copy(typeof(ContentHash).Assembly.Location, dll);
        var manifest = new AssemblyManifest { InputIdentity = snapshot.InputIdentity, BuildInputIdentity = new(AssemblyArtifacts.FileIdentity(snapshot.InputIdentity.Files), snapshot.InputIdentity.Files),
            Sdk = "10.0.303", TargetFramework = "net10.0", Configuration = "Debug", Generation = "external", Assemblies = [new("ArchSift.Core", "@dependency", "Core.dll", AssemblyArtifacts.FileHash(dll))] };
        File.WriteAllBytes(config.Build.AssemblyManifest, Bytes(manifest)); Assert.False(AssemblyArtifacts.Existing(config, snapshot).SourceBound);
    }
}
