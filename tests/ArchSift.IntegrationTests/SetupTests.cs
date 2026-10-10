using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Setup;

namespace ArchSift.IntegrationTests;

public sealed class SetupTests
{
    [Fact]
    public async Task Upgrade050To060KeepsLegacyConfigPathCatalogAndSelectionAndRollbackPreservesLaterSelection()
    {
        using var fixture = new Fixture(); var service = new SetupService();
        var first = fixture.Package("0.5.0"); var install = service.PlanInstall(fixture.Install, first, SetupFiles.FileHash(first), fixture.Config, false);
        var installed = await service.ApplyAsync(install, install.PlanId, default);
        var oldPath = Path.Combine(fixture.Install, "config", "ifx.json"); File.Move(installed.After.ConfigPath, oldPath);
        SetupFiles.Atomic(SetupService.Selection(fixture.Install), installed.After with { ConfigPath = oldPath });
        var catalog = Path.Combine(fixture.Install, "config", "profiles.json"); var selection = Path.Combine(fixture.Install, "config", "selection.json");
        SetupFiles.Atomic(catalog, new ProfileCatalog(1, null, [])); SetupFiles.Atomic(selection, new ProfileSelection(1, new string('a', 32)));
        var catalogHash = SetupFiles.FileHash(catalog); var stateHash = SetupFiles.Capture(oldPath, fixture.Config.RulesDirectory!).Sha256;
        var next = fixture.Package("0.6.0"); var plan = service.PlanUpgrade(fixture.Install, next, SetupFiles.FileHash(next), false);
        var upgraded = await service.ApplyAsync(plan, plan.PlanId, default);
        Assert.Equal(oldPath, upgraded.After.ConfigPath); Assert.Equal(stateHash, upgraded.AfterState.Sha256);
        SetupFiles.Atomic(selection, new ProfileSelection(1, new string('b', 32))); var laterSelection = SetupFiles.FileHash(selection);
        var reverted = service.Rollback(fixture.Install, upgraded.Id, SetupFiles.FileHash(SetupService.Selection(fixture.Install)));
        Assert.Equal("0.5.0", reverted.After.SelectedVersion); Assert.Equal(oldPath, reverted.After.ConfigPath);
        Assert.Equal(catalogHash, SetupFiles.FileHash(catalog)); Assert.Equal(laterSelection, SetupFiles.FileHash(selection));
    }

    [Fact]
    public async Task InstallAdoptUpgradeNoOpRollbackPreserveRegistryTombstonesAndDanglingChains()
    {
        using var fixture = new Fixture(); var service = new SetupService();
        var beforeTarget = InputCapture.Capture(fixture.Target).Sha256;
        var first = fixture.Package("0.4.0"); var plan = service.PlanInstall(fixture.Install, first, SetupFiles.FileHash(first), fixture.Config, false);
        Assert.False(Directory.Exists(fixture.Install));
        var installed = await service.ApplyAsync(plan, plan.PlanId, default);
        Assert.Equal("0.4.0", installed.After.SelectedVersion);
        Assert.Equal("default.json", Path.GetFileName(installed.After.ConfigPath));
        Assert.Empty(ConfigLoader.Load(installed.After.ConfigPath).Rulesets);
        var library = new RulesetLibrary(fixture.Config.RulesDirectory!, fixture.Target);
        // Use a real bundled template, then retain the tombstone and chain reference after deletion.
        var repo = FindRoot(); var rule = File.ReadAllBytes(Path.Combine(repo, "templates", "rules", "naming.json"));
        var card = library.Import("policy.json", rule); library.Delete(card.EntryId);
        Directory.CreateDirectory(Path.Combine(fixture.Config.RulesDirectory!, "chains"));
        SetupFiles.Atomic(Path.Combine(fixture.Config.RulesDirectory!, "chains", "chain.json"), new ChainDocument(1, "chain", "1", "unchanged", [new(card.EntryId)]));
        var state = SetupFiles.Capture(installed.After.ConfigPath, installed.After.LibraryPath);
        var next = fixture.Package("0.5.0");
        var upgrade = service.PlanUpgrade(fixture.Install, next, SetupFiles.FileHash(next), false);
        var upgraded = await service.ApplyAsync(upgrade, upgrade.PlanId, default);
        Assert.Equal(state.Sha256, upgraded.AfterState.Sha256);
        Assert.True(library.Find(card.EntryId)!.Deleted);
        Assert.Equal(state.Sha256, SetupFiles.Capture(upgraded.After.ConfigPath, upgraded.After.LibraryPath).Sha256);
        var again = service.PlanUpgrade(fixture.Install, next, SetupFiles.FileHash(next), false);
        Assert.Equal("no-op", (await service.ApplyAsync(again, again.PlanId, default)).Outcome);
        var rollback = service.Rollback(fixture.Install, upgraded.Id, SetupFiles.FileHash(SetupService.Selection(fixture.Install)));
        Assert.Equal("0.4.0", rollback.After.SelectedVersion);
        Assert.Equal(2, rollback.After.Versions.Length);
        Assert.Equal(state.Sha256, rollback.AfterState.Sha256);
        Assert.Equal(beforeTarget, InputCapture.Capture(fixture.Target).Sha256);
        Assert.False(Directory.Exists(fixture.Config.Output.Directory));
        Assert.NotNull(service.Inspect(fixture.Install));
        // All persisted contracts validate independently through the frozen JSON schemas.
        Assert.Equal(rollback.PlanId, SetupFiles.Read<Receipt>(Path.Combine(fixture.Install, "operations", rollback.Id, "receipt.json")).PlanId);
    }

    [Fact]
    public async Task ExistingManualPayloadNeedsExplicitAdoptionAndConfigBytesArePreserved()
    {
        using var fixture = new Fixture(); var package = fixture.Package("0.4.0");
        var payload = Path.Combine(fixture.Install, "legacy"); Directory.CreateDirectory(payload); ZipFile.ExtractToDirectory(package, payload);
        var path = Path.Combine(fixture.Root, "external-config.json"); SetupFiles.Atomic(path, fixture.Config);
        var hash = SetupFiles.FileHash(path); var service = new SetupService();
        Assert.Throws<ConfigurationException>(() => service.PlanInstall(fixture.Install, package, SetupFiles.FileHash(package), fixture.Config, false));
        var plan = service.PlanAdopt(fixture.Install, payload, path);
        Assert.False(File.Exists(SetupService.Selection(fixture.Install)));
        var receipt = await service.ApplyAsync(plan, plan.PlanId, default);
        Assert.Equal(hash, SetupFiles.FileHash(path)); Assert.Equal("adopt", receipt.Operation);
        Assert.Equal("adopted-local-payload", receipt.After.Versions[0].PackageSha256);
        var next = fixture.Package("0.6.0");
        Assert.Throws<ConfigurationException>(() => service.PlanUpgrade(fixture.Install, next, SetupFiles.FileHash(next), false));
        Assert.Equal(hash, SetupFiles.FileHash(path));
    }

    [Theory]
    [InlineData("prepared")]
    [InlineData("backed-up")]
    [InlineData("staged")]
    [InlineData("configured")]
    [InlineData("committed")]
    [InlineData("selected")]
    [InlineData("completed")]
    public async Task InterruptedUpgradeReconcilesFromBytesAndRetainsOldVersion(string phase)
    {
        using var fixture = new Fixture(); var service = new SetupService();
        var first = fixture.Package("0.4.0"); var install = service.PlanInstall(fixture.Install, first, SetupFiles.FileHash(first), fixture.Config, false);
        var installed = await service.ApplyAsync(install, install.PlanId, default);
        var before = SetupFiles.Capture(installed.After.ConfigPath, fixture.Config.RulesDirectory!);
        var next = fixture.Package("0.5.0"); var plan = service.PlanUpgrade(fixture.Install, next, SetupFiles.FileHash(next), false);
        var faulty = new SetupService(current => { if (current == phase) throw new IOException("Injected interrupted write/checkpoint."); });
        await Assert.ThrowsAsync<IOException>(() => faulty.ApplyAsync(plan, plan.PlanId, default));
        var journalPath = Directory.GetFiles(Path.Combine(fixture.Install, "operations"), "journal.json", SearchOption.AllDirectories)
            .Single(p => SetupFiles.Read<Journal>(p).Plan.Operation == "upgrade");
        var journal = SetupFiles.Read<Journal>(journalPath);
        var result = service.Recover(fixture.Install, journal.Id, SetupFiles.FileHash(journalPath));
        var selected = SetupFiles.Read<Installation>(SetupService.Selection(fixture.Install));
        Assert.Equal(phase is "selected" or "completed" ? "0.5.0" : "0.4.0", selected.SelectedVersion);
        Assert.Equal(before.Sha256, SetupFiles.Capture(selected.ConfigPath, selected.LibraryPath).Sha256);
        Assert.True(Directory.Exists(Path.Combine(fixture.Install, "versions", "0.4.0")));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task StalePlanConfigEditAndRollbackAfterLaterEditsRejectWithoutDataLoss()
    {
        using var fixture = new Fixture(); var service = new SetupService(); var first = fixture.Package("0.4.0");
        var install = service.PlanInstall(fixture.Install, first, SetupFiles.FileHash(first), fixture.Config, false);
        await Assert.ThrowsAsync<ConfigurationException>(() => service.ApplyAsync(install, new string('0', 64), default));
        await Assert.ThrowsAsync<ConfigurationException>(() => service.ApplyAsync(SetupService.Seal(install with { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(-1) }), install.PlanId, default));
        await service.ApplyAsync(install, install.PlanId, default);
        var next = fixture.Package("0.5.0"); var upgrade = service.PlanUpgrade(fixture.Install, next, SetupFiles.FileHash(next), false);
        var configPath = SetupService.LoadInstallation(fixture.Install).ConfigPath; File.AppendAllText(configPath, "\n");
        await Assert.ThrowsAsync<ConfigurationException>(() => service.ApplyAsync(upgrade, upgrade.PlanId, default));
        upgrade = service.PlanUpgrade(fixture.Install, next, SetupFiles.FileHash(next), false);
        var receipt = await service.ApplyAsync(upgrade, upgrade.PlanId, default);
        File.AppendAllText(configPath, "\n"); var edited = SetupFiles.FileHash(configPath);
        Assert.Throws<ConfigurationException>(() => service.Rollback(fixture.Install, receipt.Id, SetupFiles.FileHash(SetupService.Selection(fixture.Install))));
        Assert.Equal(edited, SetupFiles.FileHash(configPath));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("C:/escape")]
    [InlineData("a\\b")]
    [InlineData("CON.txt")]
    [InlineData("a/NUL")]
    [InlineData("LPT9")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("a//b")]
    public void UnsafeArchivePathsAreRejectedBeforeRootCreation(string path)
    {
        using var fixture = new Fixture(); var zip = fixture.Package("0.5.0", extra: path);
        Assert.Throws<ConfigurationException>(() => PackageReader.Inspect(zip, SetupFiles.FileHash(zip)));
        Assert.False(Directory.Exists(fixture.Install));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("case-collision")]
    [InlineData("corrupt")]
    [InlineData("link")]
    [InlineData("extra")]
    [InlineData("schema")]
    public void CorruptIncompleteLinkedOrIncompatiblePackageCannotReachLaunch(string fault)
    {
        using var fixture = new Fixture(); var zip = fixture.Package("0.5.0", fault);
        Assert.ThrowsAny<Exception>(() => PackageReader.Inspect(zip, SetupFiles.FileHash(zip)));
        Assert.False(Directory.Exists(fixture.Install));
    }

    [Fact]
    public async Task OverlapConcurrentLockUnknownStateAndSameVersionChangedBytesReject()
    {
        using var fixture = new Fixture(); var service = new SetupService(); var first = fixture.Package("0.4.0");
        Assert.Throws<ConfigurationException>(() => service.PlanInstall(fixture.Target, first, SetupFiles.FileHash(first), fixture.Config, false));
        var inside = fixture.Config with { Target = fixture.Config.Target with { Root = fixture.Install } };
        Assert.Throws<ConfigurationException>(() => service.PlanInstall(fixture.Root, first, SetupFiles.FileHash(first), inside, false));
        var plan = service.PlanInstall(fixture.Install, first, SetupFiles.FileHash(first), fixture.Config, false);
        await service.ApplyAsync(plan, plan.PlanId, default);
        var changed = fixture.Package("0.4.0", alternate: true);
        Assert.Throws<ConfigurationException>(() => service.PlanUpgrade(fixture.Install, changed, SetupFiles.FileHash(changed), false));
        var next = fixture.Package("0.5.0"); plan = service.PlanUpgrade(fixture.Install, next, SetupFiles.FileHash(next), false);
        using (var locked = new FileStream(Path.Combine(fixture.Install, ".archsift-setup.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => service.ApplyAsync(plan, plan.PlanId, default));
        Directory.CreateDirectory(fixture.Config.RulesDirectory!);
        File.WriteAllText(Path.Combine(fixture.Config.RulesDirectory!, ".archsift-library.json"), "{\"schemaVersion\":2,\"entries\":[]}");
        Assert.Throws<ConfigurationException>(() => service.PlanUpgrade(fixture.Install, next, SetupFiles.FileHash(next), false));
    }

    [Fact]
    public async Task CancellationRetainsJournalAndOwnedArtifactsForReviewedRecovery()
    {
        using var fixture = new Fixture(); using var cancellation = new CancellationTokenSource();
        var package = fixture.Package("0.5.0"); var service = new SetupService(phase => { if (phase == "backed-up") cancellation.Cancel(); });
        var plan = service.PlanInstall(fixture.Install, package, SetupFiles.FileHash(package), fixture.Config, false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ApplyAsync(plan, plan.PlanId, cancellation.Token));
        Assert.False(File.Exists(SetupService.Selection(fixture.Install)));
        var journal = Assert.Single(Directory.GetFiles(Path.Combine(fixture.Install, "operations"), "journal.json", SearchOption.AllDirectories));
        Assert.Equal("backed-up", SetupFiles.Read<Journal>(journal).Phase);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    public void MissingOrDuplicateSetupFieldsAreRejected(string json)
    { Assert.Throws<ConfigurationException>(() => SetupFiles.Parse<SetupPlan>(Encoding.UTF8.GetBytes(json))); }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "archsift-setup-test-" + Guid.NewGuid().ToString("N"));
        public string Target => Path.Combine(Root, "target");
        public string Install => Path.Combine(Root, "install");
        public RunConfiguration Config => new() { SchemaVersion = 1, Target = new() { Root = Target, Entry = "Project.csproj" },
            Build = new() { TargetFramework = "net10.0" }, Output = new() { Directory = Path.Combine(Install, "reports"), Formats = ["json", "html", "sarif"] }, RulesDirectory = Path.Combine(Install, "rules") };
        public Fixture() { Directory.CreateDirectory(Target); File.WriteAllText(Path.Combine(Target, "Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"); }
        public string Package(string version, string? fault = null, string? extra = null, bool alternate = false)
        {
            var files = new Dictionary<string, byte[]> { ["archsift.exe"] = Encoding.UTF8.GetBytes(alternate ? "different synthetic CLI" : "synthetic CLI; never execute"),
                ["web/ArchSift.Web.exe"] = Encoding.UTF8.GetBytes("synthetic Web; never execute"), ["LICENSE"] = Encoding.UTF8.GetBytes("synthetic license") };
            if (version.StartsWith("0.5.", StringComparison.Ordinal) || version.StartsWith("0.6.", StringComparison.Ordinal)) files["setup-compatibility.json"] = JsonSerializer.SerializeToUtf8Bytes(new SetupCompatibility(1, fault == "schema" ? 2 : 1, 1, 1), JsonContract.Options);
            var manifest = new PackageManifest(1, version, "win-x64", true, "candidate", new string('a', 40), false, false, new string('b', 64),
                files.Select(p => new PackageFile(p.Key, p.Value.Length, SetupFiles.Hash(p.Value))).ToArray(), true);
            files["package-manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonContract.Options);
            if (fault == "corrupt") files["archsift.exe"] = [1, 2, 3];
            var path = Path.Combine(Root, "package-" + Guid.NewGuid().ToString("N") + ".zip");
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            void Add(string name, byte[] bytes, int attributes = 0) { var entry = archive.CreateEntry(name); entry.ExternalAttributes = attributes; using var stream = entry.Open(); stream.Write(bytes); }
            foreach (var file in files) Add(file.Key, file.Value);
            if (fault == "duplicate") Add("archsift.exe", files["archsift.exe"]);
            if (fault == "case-collision") Add("ARCHSIFT.EXE", files["archsift.exe"]);
            if (fault == "link") Add("symlink", [1], 0xa000 << 16);
            if (fault == "extra") Add("unlisted", [1]);
            if (extra is not null) Add(extra, [1]);
            return path;
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
