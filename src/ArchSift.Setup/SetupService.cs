using System.Diagnostics;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Hosting;

namespace ArchSift.Setup;

public sealed class SetupService(Action<string>? phaseObserver = null)
{
    public SetupPlan PlanInstall(string root, string package, string sha256, RunConfiguration configuration, bool smoke)
    {
        root = SetupFiles.Safe(root);
        if (File.Exists(Selection(root))) throw new ConfigurationException("Installation already owned; use upgrade.");
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(p => Path.GetFileName(p) != "downloads"))
            throw new ConfigurationException("Unknown existing installation; inspect and explicitly adopt it.");
        var configPath = SetupFiles.Under(root, "config/project.json");
        ValidateConfiguration(root, configPath, configuration);
        if (File.Exists(configPath)) throw new ConfigurationException("Existing configuration cannot be overwritten.");
        var identity = PackageReader.Inspect(package, sha256);
        return Seal(new(1, "", "install", DateTimeOffset.UtcNow.AddHours(24), root, identity, null, configPath, configuration,
            null, Capture(configPath, configuration), smoke));
    }

    public SetupPlan PlanUpgrade(string root, string package, string sha256, bool smoke)
    {
        var before = LoadInstallation(root);
        var config = ConfigLoader.Load(before.ConfigPath);
        ValidateConfiguration(before.Root, before.ConfigPath, config);
        if (config.Target.Root != before.TargetRoot || Library(config) != before.LibraryPath)
            throw new ConfigurationException("Configuration changed target/library; explicit adoption review is required.");
        ValidateState(config);
        var identity = PackageReader.Inspect(package, sha256);
        var existing = before.Versions.SingleOrDefault(v => v.Version == identity.Version);
        if (existing is not null && existing.ManifestSha256 != identity.ManifestSha256) throw new ConfigurationException("Same-version different bytes are rejected.");
        return Seal(new(1, "", "upgrade", DateTimeOffset.UtcNow.AddHours(24), before.Root, identity, null, before.ConfigPath,
            config, SetupFiles.FileHash(Selection(before.Root)), Capture(before.ConfigPath, config), smoke));
    }

    public SetupPlan PlanAdopt(string root, string versionDirectory, string configPath)
    {
        root = SetupFiles.Safe(root); versionDirectory = SetupFiles.Safe(versionDirectory); configPath = SetupFiles.Safe(configPath);
        if (File.Exists(Selection(root))) throw new ConfigurationException("Installation already owned.");
        if (!PathSafety.IsUnder(versionDirectory, root)) throw new ConfigurationException("Adopted payload must be inside install root.");
        var hash = SetupFiles.FileHash(SetupFiles.Under(versionDirectory, "package-manifest.json"));
        var manifest = PackageReader.VerifyDirectory(versionDirectory, hash);
        var config = ConfigLoader.Load(configPath);
        ValidateConfiguration(root, configPath, config); ValidateState(config);
        foreach (var entry in manifest.Entries)
        {
            var owned = SetupFiles.Under(versionDirectory, entry.Path);
            if (owned.Equals(configPath, PathSafety.Comparison) || PathSafety.IsUnder(owned, Library(config)) || PathSafety.IsUnder(owned, config.Output.Directory))
                throw new ConfigurationException("Payload ownership conflicts with user state.");
        }
        return Seal(new(1, "", "adopt", DateTimeOffset.UtcNow.AddHours(24), root, null,
            new(manifest.Version, versionDirectory, hash, "adopted-local-payload"), configPath, config, null, Capture(configPath, config), false));
    }

    public object Inspect(string root)
    {
        root = SetupFiles.Safe(root);
        if (!File.Exists(Selection(root))) return new { status = "unowned", root, message = "Existing ZIP installations require explicit adoption." };
        var installation = LoadInstallation(root);
        var config = ConfigLoader.Load(installation.ConfigPath);
        ValidateConfiguration(root, installation.ConfigPath, config); ValidateState(config);
        return new { status = "verified", installation, selectionSha256 = SetupFiles.FileHash(Selection(root)),
            state = Capture(installation.ConfigPath, config), launchExecutable = Path.Combine(Selected(installation).Directory, "archsift.exe"),
            pendingOperations = Pending(root), limits = new[] { "No target analysis/build performed.", "Schema migration is unsupported." } };
    }

    public async Task<Receipt> ApplyAsync(SetupPlan plan, string reviewedId, CancellationToken cancellation)
    {
        ValidatePlan(plan, reviewedId);
        if (plan.Operation == "install" && Directory.Exists(plan.Root) && Directory.EnumerateFileSystemEntries(plan.Root).Any(p => Path.GetFileName(p) != "downloads"))
            throw new ConfigurationException("Install root changed after plan review; explicit adoption/recovery required.");
        var baseline = SetupFiles.HostHash();
        Directory.CreateDirectory(SetupFiles.Safe(plan.Root));
        using var operationLock = Lock(plan.Root);
        try
        {
            if (Pending(plan.Root).Length != 0) throw new ConfigurationException("Interrupted operation requires reviewed recovery.");
            Installation? before = File.Exists(Selection(plan.Root)) ? LoadInstallation(plan.Root) : null;
            if ((before is null ? null : SetupFiles.FileHash(Selection(plan.Root))) != plan.SelectionSha256) throw new ConfigurationException("Stale installation selection.");
            EnsureStopped(before?.Versions ?? (plan.AdoptedVersion is null ? [] : [plan.AdoptedVersion]));
            ValidateConfiguration(plan.Root, plan.ConfigPath, plan.Config); ValidateState(plan.Config);
            if (Capture(plan.ConfigPath, plan.Config).Sha256 != plan.State.Sha256) throw new ConfigurationException("Stale config/library plan.");
            using var libraryLock = LockLibrary(Library(plan.Config));
            if (plan.Package is { } package && PackageReader.Inspect(package.Path, package.Sha256) != package) throw new ConfigurationException("Package changed after planning.");
            if (plan.AdoptedVersion is { } adopted) PackageReader.VerifyDirectory(adopted.Directory, adopted.ManifestSha256);
            if (before is not null && plan.Package?.Version == before.SelectedVersion)
                return new(1, before.LastOperation, "upgrade", plan.PlanId, before, before, plan.State, plan.State, "", baseline, true, "no-op");
            var id = Guid.NewGuid().ToString("N");
            var operation = SetupFiles.Under(plan.Root, "operations/" + id);
            var owned = plan.AdoptedVersion ?? new OwnedVersion(plan.Package!.Version,
                SetupFiles.Under(plan.Root, "versions/" + plan.Package.Version), plan.Package.ManifestSha256, plan.Package.Sha256);
            var versions = (before?.Versions ?? []).Where(v => v.Version != owned.Version).Append(owned).ToArray();
            var after = new Installation(1, plan.Root, plan.Config.Target.Root, plan.ConfigPath, Library(plan.Config), owned.Version, versions, id);
            var journal = new Journal(1, id, "prepared", plan, before, after, plan.State, null, baseline);
            SaveJournal(operation, journal);
            phaseObserver?.Invoke("prepared");
            cancellation.ThrowIfCancellationRequested();
            SetupFiles.Backup(plan.ConfigPath, Library(plan.Config), plan.State, Path.Combine(operation, "backup"));
            journal = Phase(operation, journal, "backed-up"); SetupFiles.HostCheckpoint(baseline);
            if (plan.Package is not null)
            {
                if (Directory.Exists(owned.Directory)) PackageReader.VerifyDirectory(owned.Directory, owned.ManifestSha256, true);
                else
                {
                    var stage = Path.Combine(operation, "staging");
                    PackageReader.Extract(plan.Package, stage, cancellation);
                    SetupFiles.Safe(owned.Directory); Directory.CreateDirectory(Path.GetDirectoryName(owned.Directory)!);
                    SetupFiles.Safe(owned.Directory); Directory.Move(stage, owned.Directory);
                }
            }
            journal = Phase(operation, journal, "staged"); SetupFiles.HostCheckpoint(baseline);
            cancellation.ThrowIfCancellationRequested();
            if (plan.Operation == "install")
            {
                if (File.Exists(plan.ConfigPath)) throw new ConfigurationException("Configuration destination appeared after planning.");
                SetupFiles.Atomic(plan.ConfigPath, plan.Config, false);
            }
            // Compare actual persisted config, not only plan JSON. No target analysis or application entry point.
            var loadedConfig = ConfigLoader.Load(plan.ConfigPath);
            if (SetupFiles.JsonHash(loadedConfig) != SetupFiles.JsonHash(plan.Config)) throw new ConfigurationException("Persisted config differs from reviewed plan.");
            ValidateState(loadedConfig);
            var stateAfter = Capture(plan.ConfigPath, loadedConfig);
            if (plan.Operation != "install" && stateAfter.Sha256 != plan.State.Sha256) throw new IOException("State drift before smoke.");
            journal = Phase(operation, journal with { AfterState = stateAfter }, "configured"); SetupFiles.HostCheckpoint(baseline);
            if (plan.Smoke)
            {
                var web = SetupFiles.Under(owned.Directory, "web/ArchSift.Web.exe");
                var code = await UiProcess.RunAsync(web, null, plan.ConfigPath, TextWriter.Null, TextWriter.Null, cancellation, smoke: true);
                if (code != 0) throw new IOException("Installation UI smoke failed; diagnostics journal retained. Exit " + code);
                if (Capture(plan.ConfigPath, loadedConfig).Sha256 != stateAfter.Sha256) throw new IOException("UI smoke changed external state.");
                SetupFiles.HostCheckpoint(baseline);
            }
            cancellation.ThrowIfCancellationRequested();
            EnsureStopped(versions);
            if ((File.Exists(Selection(plan.Root)) ? SetupFiles.FileHash(Selection(plan.Root)) : null) != plan.SelectionSha256 ||
                Capture(plan.ConfigPath, loadedConfig).Sha256 != stateAfter.Sha256) throw new IOException("Selection/state changed before commit.");
            SetupFiles.HostCheckpoint(baseline);
            // Persist commit intent first. Recovery compares byte identities, never merely a phase label.
            journal = Phase(operation, journal, "committed");
            SetupFiles.Atomic(Selection(plan.Root), after, before is not null);
            phaseObserver?.Invoke("selected");
            var receipt = Finish(operation, journal); SetupFiles.HostCheckpoint(baseline);
            return receipt;
        }
        finally { SetupFiles.HostCheckpoint(baseline); }
    }

    public Receipt Rollback(string root, string receiptId, string selectionHash)
    {
        root = SetupFiles.Safe(root); Id(receiptId); var baseline = SetupFiles.HostHash();
        using var operationLock = Lock(root);
        try
        {
            if (Pending(root).Length != 0) throw new ConfigurationException("Interrupted operation requires recovery before rollback.");
            var current = LoadInstallation(root);
            if (SetupFiles.FileHash(Selection(root)) != selectionHash) throw new ConfigurationException("Stale rollback selection.");
            var receipt = SetupFiles.Read<Receipt>(SetupFiles.Under(root, "operations/" + receiptId + "/receipt.json"));
            if (receipt.SchemaVersion != 1 || receipt.Id != receiptId || receipt.Operation != "upgrade" || receipt.Before is null || receipt.Outcome != "success" ||
                SetupFiles.JsonHash(current) != SetupFiles.JsonHash(receipt.After)) throw new ConfigurationException("Receipt does not match active upgrade.");
            ValidateInstallation(receipt.Before, root); EnsureStopped(current.Versions);
            using var libraryLock = LockLibrary(current.LibraryPath);
            var state = SetupFiles.Capture(current.ConfigPath, current.LibraryPath);
            if (state.Sha256 != receipt.AfterState.Sha256) throw new ConfigurationException("Later user edits detected; rollback preserves them and refuses replacement.");
            var id = Guid.NewGuid().ToString("N"); var operation = SetupFiles.Under(root, "operations/" + id);
            var after = receipt.Before with { Versions = current.Versions, LastOperation = id };
            var config = ConfigLoader.Load(current.ConfigPath); ValidateState(config);
            var plan = Seal(new(1, "", "rollback", DateTimeOffset.UtcNow.AddHours(24), root, null, null, current.ConfigPath, config, selectionHash, state, false));
            var journal = new Journal(1, id, "prepared", plan, current, after, state, state, baseline);
            SaveJournal(operation, journal); SetupFiles.HostCheckpoint(baseline);
            journal = Phase(operation, journal, "committed"); SetupFiles.Atomic(Selection(root), after);
            return Finish(operation, journal);
        }
        finally { SetupFiles.HostCheckpoint(baseline); }
    }

    public object Recover(string root, string operationId, string journalHash)
    {
        root = SetupFiles.Safe(root); Id(operationId); var baseline = SetupFiles.HostHash();
        using var operationLock = Lock(root);
        try
        {
            var operation = SetupFiles.Under(root, "operations/" + operationId); var path = Path.Combine(operation, "journal.json");
            if (SetupFiles.FileHash(path) != journalHash) throw new ConfigurationException("Journal changed since review.");
            var journal = SetupFiles.Read<Journal>(path);
            if (journal.SchemaVersion != 1 || journal.Id != operationId || journal.Plan.Root != root) throw new ConfigurationException("Journal identity mismatch.");
            ValidateInstallation(journal.After, root, verify: false); if (journal.Before is not null) ValidateInstallation(journal.Before, root);
            EnsureStopped(journal.After.Versions);
            if (File.Exists(Selection(root)))
            {
                var selected = LoadInstallation(root);
                if (SetupFiles.JsonHash(selected) == SetupFiles.JsonHash(journal.After))
                {
                    if (journal.AfterState is null || SetupFiles.Capture(selected.ConfigPath, selected.LibraryPath).Sha256 != journal.AfterState.Sha256)
                        throw new ConfigurationException("State changed since interrupted commit; manual review required.");
                    // A recovery has its own operator/process hash context; retain the original journal as evidence.
                    SetupFiles.Atomic(Path.Combine(operation, "recovery-baseline.json"), new { originalHostSha256 = journal.HostSha256, recoveryHostSha256 = baseline });
                    return Finish(operation, journal with { HostSha256 = baseline });
                }
                if (journal.Before is null || SetupFiles.JsonHash(selected) != SetupFiles.JsonHash(journal.Before)) throw new ConfigurationException("Selection drift; recovery refuses replacement.");
            }
            else if (journal.Before is not null) throw new ConfigurationException("Prior selection disappeared.");
            // Preserve precisely owned partial artifacts; do not recursively remove arbitrary directories.
            Phase(operation, journal, "aborted");
            return new { outcome = "aborted", operationId, retainedArtifacts = operation, message = "Active selection preserved; staging/config/version artifacts retained for review." };
        }
        finally { SetupFiles.HostCheckpoint(baseline); }
    }

    public static SetupPlan Seal(SetupPlan plan) => plan with { PlanId = SetupFiles.JsonHash(plan with { PlanId = "" }) };
    public static string Library(RunConfiguration config) => SetupFiles.Safe(config.RulesDirectory ?? Path.Combine(config.Output.Directory, "rules"));
    public static string Selection(string root) => SetupFiles.Under(root, "install.json");
    private static StateIdentity Capture(string path, RunConfiguration config) => SetupFiles.Capture(path, Library(config));
    private static OwnedVersion Selected(Installation installation) => installation.Versions.Single(v => v.Version == installation.SelectedVersion);
    public static void ValidateConfiguration(string root, string configPath, RunConfiguration config)
    {
        root = SetupFiles.Safe(root); configPath = SetupFiles.Safe(configPath);
        SchemaValidation.Validate(JsonSerializer.SerializeToElement(config, JsonContract.Options), "config");
        var target = SetupFiles.Safe(config.Target.Root);
        if (!Directory.Exists(target)) throw new ConfigurationException("Target directory does not exist.");
        PathSafety.EnsureDisjoint(target, root); PathSafety.EnsureDisjoint(target, configPath);
        PathSafety.EnsureDisjoint(target, SetupFiles.Safe(config.Output.Directory)); PathSafety.EnsureDisjoint(target, Library(config));
        if (config.Target.Entry is null || !File.Exists(SetupFiles.Under(target, config.Target.Entry.Replace('\\', '/'))) ||
            Path.GetExtension(config.Target.Entry).ToLowerInvariant() is not (".sln" or ".slnx" or ".csproj"))
            throw new ConfigurationException("An existing root-relative solution/project entry is required.");
        if (config.Build.TargetFramework is not ("net8.0" or "net9.0" or "net10.0")) throw new ConfigurationException("Explicit supported TFM is required.");
        // Setup validates config; it never enables build/network or changes an existing approved configuration.
        foreach (var statePath in new[] { configPath, Library(config), config.Output.Directory })
        {
            SetupFiles.Safe(statePath);
            foreach (var reserved in new[] { "versions", "operations", "install.json", ".archsift-setup.lock" })
                PathSafety.EnsureDisjoint(SetupFiles.Under(root, reserved), statePath);
        }
        PathSafety.EnsureDisjoint(configPath, Library(config));
        // Reports may contain a rules subdirectory in existing configurations, but cannot be inside the library.
        if (PathSafety.IsUnder(config.Output.Directory, Library(config))) throw new ConfigurationException("Reports must not be inside the library.");
    }
    public static void ValidateState(RunConfiguration config)
    {
        var library = Library(config);
        var registry = Path.Combine(library, ".archsift-library.json");
        if (File.Exists(registry)) ValidateJson(registry, "library");
        if (Directory.Exists(library))
            foreach (var rules in Directory.GetFiles(library, "*.json", SearchOption.TopDirectoryOnly))
                if (Path.GetFileName(rules) != ".archsift-library.json") RuleLoader.Load(SetupFiles.Safe(rules));
        var chains = Path.Combine(library, "chains");
        if (Directory.Exists(chains)) foreach (var chain in Directory.GetFiles(chains, "*.json", SearchOption.TopDirectoryOnly)) ValidateJson(chain, "chain");
    }
    private static void ValidateJson(string path, string schema)
    {
        SetupFiles.Safe(path); var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 4 * 1024 * 1024) throw new ConfigurationException("State JSON exceeds 4 MiB.");
        using var doc = JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0));
        SchemaValidation.Validate(doc.RootElement, schema);
    }
    private static void ValidatePlan(SetupPlan plan, string reviewedId)
    {
        if (plan.SchemaVersion != 1 || plan.Operation is not ("install" or "upgrade" or "adopt") || plan.PlanId != reviewedId || Seal(plan).PlanId != reviewedId ||
            plan.ExpiresUtc < DateTimeOffset.UtcNow || plan.ExpiresUtc > DateTimeOffset.UtcNow.AddHours(25)) throw new ConfigurationException("Invalid/expired/unreviewed plan.");
        if (plan.Root != SetupFiles.Safe(plan.Root) || plan.ConfigPath != SetupFiles.Safe(plan.ConfigPath) ||
            (plan.Operation == "adopt" ? plan.AdoptedVersion is null || plan.Package is not null : plan.Package is null || plan.AdoptedVersion is not null))
            throw new ConfigurationException("Plan operation/path mismatch.");
        if (plan.Operation == "install" && (plan.SelectionSha256 is not null || plan.State.ConfigExists || plan.ConfigPath != SetupFiles.Under(plan.Root, "config/project.json")))
            throw new ConfigurationException("Invalid install plan.");
        if (plan.Operation == "upgrade" && plan.SelectionSha256 is null) throw new ConfigurationException("Upgrade requires prior ownership.");
        if (plan.AdoptedVersion is { } version && !PathSafety.IsUnder(version.Directory, plan.Root)) throw new ConfigurationException("Adoption escapes installation root.");
    }
    private static Installation LoadInstallation(string root)
    {
        root = SetupFiles.Safe(root); var installation = SetupFiles.Read<Installation>(Selection(root));
        ValidateInstallation(installation, root); return installation;
    }
    private static void ValidateInstallation(Installation installation, string root, bool verify = true)
    {
        if (installation.SchemaVersion != 1 || installation.Root != root || installation.Versions.Length == 0 ||
            installation.Versions.Select(v => v.Version).Distinct(StringComparer.Ordinal).Count() != installation.Versions.Length ||
            installation.Versions.Count(v => v.Version == installation.SelectedVersion) != 1) throw new ConfigurationException("Invalid installation selection/ownership.");
        PathSafety.EnsureDisjoint(SetupFiles.Safe(installation.TargetRoot), root);
        foreach (var version in installation.Versions)
        {
            PackageReader.Version(version.Version);
            if (!PathSafety.IsUnder(SetupFiles.Safe(version.Directory), root)) throw new ConfigurationException("Owned version escapes root.");
            if (verify) { var manifest = PackageReader.VerifyDirectory(version.Directory, version.ManifestSha256); if (manifest.Version != version.Version) throw new ConfigurationException("Owned version mismatch."); }
        }
    }
    private static FileStream Lock(string root) => new(SetupFiles.Under(root, ".archsift-setup.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private static FileStream? LockLibrary(string library)
    {
        var path = SetupFiles.Under(library, ".archsift-library.lock");
        return File.Exists(path) ? new(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }
    private static void EnsureStopped(OwnedVersion[] versions)
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                try
                {
                    var name = process.ProcessName;
                    if (name is not ("archsift" or "ArchSift.Web" or "ArchSift.Setup")) continue;
                    var path = process.MainModule?.FileName ?? throw new ConfigurationException("Cannot inspect ArchSift process identity.");
                    if (versions.Any(v => PathSafety.IsUnder(path, v.Directory))) throw new ConfigurationException("Close the existing owned UI/CLI before setup; PID " + process.Id);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Process enumeration races normal exit. A live, inaccessible process still requires review.
                    if (!process.HasExited) throw new ConfigurationException("Cannot inspect live ArchSift process; operator review required.");
                }
            }
        }
    }
    private static string[] Pending(string root)
    {
        var directory = SetupFiles.Under(root, "operations");
        if (!Directory.Exists(directory)) return [];
        return Directory.GetDirectories(directory).Where(d =>
        {
            SetupFiles.Safe(d); var path = Path.Combine(d, "journal.json");
            if (!File.Exists(path)) return false;
            var journal = SetupFiles.Read<Journal>(path);
            return journal.Phase is not ("completed" or "aborted");
        }).Select(Path.GetFileName).OfType<string>().ToArray();
    }
    private static void Id(string id) { if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-f0-9]{32}$")) throw new ConfigurationException("Invalid operation ID."); }
    private static void SaveJournal(string directory, Journal journal) => SetupFiles.Atomic(Path.Combine(directory, "journal.json"), journal);
    private Journal Phase(string directory, Journal journal, string phase)
    { journal = journal with { Phase = phase }; SaveJournal(directory, journal); phaseObserver?.Invoke(phase); return journal; }
    private Receipt Finish(string directory, Journal journal)
    {
        if (journal.AfterState is null) throw new ConfigurationException("Committed operation lacks state identity.");
        SetupFiles.HostCheckpoint(journal.HostSha256);
        var receipt = new Receipt(1, journal.Id, journal.Plan.Operation, journal.Plan.PlanId, journal.Before, journal.After,
            journal.BeforeState, journal.AfterState, Path.Combine(directory, "backup"), journal.HostSha256, true, "success");
        SetupFiles.Atomic(Path.Combine(directory, "receipt.json"), receipt);
        Phase(directory, journal, "completed"); return receipt;
    }
}
