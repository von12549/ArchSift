using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Hosting;
using ArchSift.Setup;
using ArchSift.Web;

namespace ArchSift.IntegrationTests;

internal static class WebPayloadFixtures
{
    public static void CopyDirectory(string source, string destination)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var output = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.Copy(file, output, false);
        }
    }
    // Synthetic inventory qualifies resolver behavior only; cold package/release gates remain separate.
    public static void Inventory(string directory)
    {
        var notice = Path.Combine(directory, "notices", "third-party-notices.md");
        Directory.CreateDirectory(Path.GetDirectoryName(notice)!); File.WriteAllText(notice, "Synthetic test notice; no distribution claim.");
        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != WebPayload.InventoryName)
            .Select(f => new WebPayloadEntry(Path.GetRelativePath(directory, f).Replace('\\', '/'), new FileInfo(f).Length, SetupFiles.FileHash(f))).ToArray();
        File.WriteAllBytes(Path.Combine(directory, WebPayload.InventoryName),
            JsonSerializer.SerializeToUtf8Bytes(new WebPayloadInventory(1, ToolIdentity.Version, System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(Launcher).Assembly.Location).ProductVersion!.Split('+')[1], files), JsonContract.Options));
    }
}

public sealed class PortableWorkbenchTests
{
    [Fact]
    public void BootstrapRegistersExternalProfileWithoutSetupOwnershipAndPreservesInputs()
    {
        using var f = new Fixture();
        var config = SetupFiles.FileHash(f.ConfigPath); var target = InputCapture.Capture(f.Target).Sha256;
        PortableState.Initialize(f.State, f.ConfigPath);
        Assert.False(File.Exists(Path.Combine(f.State, "install.json")));
        var store = new ProfileStore(f.State); Assert.True(store.Portable); Assert.Null(store.Installation);
        var profile = Assert.Single(store.ReadCatalog().Profiles);
        var health = store.Inspect(profile); Assert.Equal("valid", health.Status);
        Assert.False(health.Session!.ConfigProtected); Assert.False(health.Session.LibraryProtected);
        Assert.Equal(profile.ProfileId, store.ReadSelection()!.SelectedProfileId);
        PortableState.Initialize(f.State, f.ConfigPath);
        Assert.Equal(config, SetupFiles.FileHash(f.ConfigPath)); Assert.Equal(target, InputCapture.Capture(f.Target).Sha256);
    }

    [Fact]
    public void InterruptedBoundedBootstrapRecoversButChangedOrUnknownStagingIsPreserved()
    {
        using var f = new Fixture(); PortableState.Initialize(f.State, f.ConfigPath);
        var before = Directory.GetFiles(f.State, "*", SearchOption.AllDirectories).ToDictionary(p => Path.GetRelativePath(f.State, p), SetupFiles.FileHash);
        var stage = f.State + ".archsift-bootstrap"; Directory.Move(f.State, stage);
        File.Delete(Path.Combine(stage, "config", "selection.json"));
        PortableState.Initialize(f.State, f.ConfigPath);
        foreach (var entry in before) Assert.Equal(entry.Value, SetupFiles.FileHash(Path.Combine(f.State, entry.Key)));
        Directory.Move(f.State, stage); File.WriteAllText(Path.Combine(stage, "unknown.txt"), "keep");
        Assert.Throws<ConfigurationException>(() => PortableState.Initialize(f.State, f.ConfigPath));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(stage, "unknown.txt"))); Assert.False(Directory.Exists(f.State));
    }

    [Fact]
    public void BusyUnknownAmbiguousAndFutureRootsNeverBecomeManagedOrOverwriteFiles()
    {
        using var f = new Fixture();
        Directory.CreateDirectory(f.State); File.WriteAllText(Path.Combine(f.State, "keep.txt"), "original");
        Assert.Throws<ConfigurationException>(() => PortableState.Initialize(f.State, f.ConfigPath));
        Assert.Equal("original", File.ReadAllText(Path.Combine(f.State, "keep.txt")));
        File.Delete(Path.Combine(f.State, "keep.txt")); PortableState.Initialize(f.State, f.ConfigPath);
        using (var lease = new FileStream(Path.Combine(f.State, ".archsift-workbench.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => PortableState.Initialize(f.State, f.ConfigPath));
        File.WriteAllText(Path.Combine(f.State, "install.json"), "{}");
        Assert.Throws<ConfigurationException>(() => new ProfileStore(f.State));
        File.Delete(Path.Combine(f.State, "install.json"));
        var marker = Path.Combine(f.State, PortableState.MarkerName);
        File.WriteAllText(marker, File.ReadAllText(marker).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"));
        Assert.Throws<ConfigurationException>(() => new ProfileStore(f.State));
    }

    [Fact]
    public void OverlappingStateTargetLibraryAndConfigAreRejectedBeforeWrites()
    {
        using var f = new Fixture();
        Assert.Throws<ConfigurationException>(() => PortableState.Initialize(Path.Combine(f.Target, "state"), f.ConfigPath));
        Assert.Throws<ConfigurationException>(() => PortableState.Initialize(Path.GetDirectoryName(f.ConfigPath)!, f.ConfigPath));
        var config = ConfigLoader.Load(f.ConfigPath);
        File.WriteAllText(f.ConfigPath, JsonSerializer.Serialize(config with { Output = config.Output with { Directory = Path.Combine(f.State, "reports") } }, JsonContract.Options));
        Assert.Throws<ConfigurationException>(() => PortableState.Initialize(f.State, f.ConfigPath));
        Assert.False(Directory.Exists(f.State));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("version")]
    [InlineData("duplicate")]
    [InlineData("escape")]
    public void PayloadRejectsChangedMissingExtraMixedAndUnsafeFiles(string scenario)
    {
        using var f = new Fixture();
        var web = Path.Combine(f.Root, "web"); Directory.CreateDirectory(web);
        foreach (var path in new[] { "ArchSift.Web.deps.json", "ArchSift.Web.runtimeconfig.json", "wwwroot/index.html", "wwwroot/launcher.html", "wwwroot/test-workbench.js" })
        {
            var file = Path.Combine(web, path); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, "{}");
        }
        File.Copy(typeof(Launcher).Assembly.Location, Path.Combine(web, "ArchSift.Web.dll"));
        WebPayloadFixtures.Inventory(web); WebPayload.Verify(web, ToolIdentity.Version);
        var inventoryPath = Path.Combine(web, WebPayload.InventoryName);
        var inventory = JsonSerializer.Deserialize<WebPayloadInventory>(File.ReadAllBytes(inventoryPath), JsonContract.Options)!;
        if (scenario == "changed") File.AppendAllText(Path.Combine(web, "wwwroot/index.html"), "altered");
        if (scenario == "missing") File.Delete(Path.Combine(web, "ArchSift.Web.deps.json"));
        if (scenario == "extra") File.WriteAllText(Path.Combine(web, "foreign.dll"), "unlisted");
        if (scenario == "version") inventory = inventory with { Version = "0.0.0" };
        if (scenario == "duplicate") inventory = inventory with { Entries = [.. inventory.Entries, inventory.Entries[0]] };
        if (scenario == "escape") inventory = inventory with { Entries = [.. inventory.Entries, new("../outside", 0, new string('a', 64))] };
        if (scenario is "version" or "duplicate" or "escape") File.WriteAllBytes(inventoryPath, JsonSerializer.SerializeToUtf8Bytes(inventory, JsonContract.Options));
        Assert.ThrowsAny<IOException>(() => WebPayload.Verify(web, ToolIdentity.Version));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "archsift-portable-test-" + Guid.NewGuid().ToString("N"));
        public string State => Path.Combine(Root, "state");
        public string Target => Path.Combine(Root, "target");
        public string ConfigPath => Path.Combine(Root, "config.json");
        public Fixture()
        {
            Directory.CreateDirectory(Target);
            File.WriteAllText(Path.Combine(Target, "Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = Target, Entry = "Project.csproj" },
                Output = new() { Directory = Path.Combine(Root, "reports") }, RulesDirectory = Path.Combine(Root, "library") };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonContract.Options));
        }
        public void Dispose() { Assert.True(PathSafety.IsUnder(Root, Path.GetTempPath())); Directory.Delete(Root, true); }
    }
}
