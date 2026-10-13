using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Setup;

// Retirement is recoverable: only exact, verified payload directories are moved to a
// fresh archive. Evidence, reports, configuration and operation history remain in place.
public sealed class RetirementService
{
    public RetirementPlan Plan(string operation, string root, string? version, string archiveRoot, string[] customEvidenceRoots)
    {
        if (operation is not ("remove-version" or "uninstall")) throw new ConfigurationException("Unknown retirement operation.");
        var before = SetupService.LoadInstallation(root);
        if (Pending(before.Root).Length != 0) throw new ConfigurationException("Recover the interrupted setup operation first.");
        if (File.Exists(SetupFiles.Under(before.Root, "uninstalled.json")))
            throw new ConfigurationException("Prior uninstall marker requires explicit recovery review.");
        if (operation == "remove-version")
        {
            if (version is null || version == before.SelectedVersion || !before.Versions.Any(v => v.Version == version))
                throw new ConfigurationException("Only an owned, unselected version can be removed.");
        }
        else if (version is not null) throw new ConfigurationException("Uninstall does not accept a version.");
        archiveRoot = Archive(before, archiveRoot);
        if (Directory.Exists(archiveRoot) || File.Exists(archiveRoot)) throw new ConfigurationException("Archive destination must be new.");
        var selected = operation == "uninstall" ? before.Versions : before.Versions.Where(v => v.Version == version).ToArray();
        foreach (var item in selected) PackageReader.VerifyDirectory(item.Directory, item.ManifestSha256, true);
        var roots = Roots(before, customEvidenceRoots, archiveRoot);
        var evidenceHash = EvidenceHash(roots, selected);
        var state = SetupFiles.Capture(before.ConfigPath, before.LibraryPath);
        return Seal(new RetirementPlan(1, "", operation, DateTimeOffset.UtcNow.AddHours(24), before.Root,
            version, archiveRoot, SetupFiles.FileHash(SetupService.Selection(before.Root)), state.Sha256,
            roots, evidenceHash, selected));
    }

    public RetirementReceipt Apply(RetirementPlan plan, string reviewedId)
    {
        ValidatePlan(plan, reviewedId);
        var baseline = SetupFiles.HostHash();
        using var operationLock = new FileStream(SetupFiles.Under(plan.Root, ".archsift-setup.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (Pending(plan.Root).Length != 0) throw new ConfigurationException("Recover the interrupted setup operation first.");
            var before = SetupService.LoadInstallation(plan.Root);
            VerifyCurrent(plan, before);
            SetupService.EnsureStopped(before.Versions);
            var id = Guid.NewGuid().ToString("N");
            var after = plan.Operation == "remove-version" ? before with
            { Versions = before.Versions.Where(v => v.Version != plan.Version).ToArray(), LastOperation = id } : null;
            var journalPath = JournalPath(plan.Root, id);
            var journal = new RetirementJournal(1, id, "prepared", plan, before, after, baseline);
            SetupFiles.Atomic(journalPath, journal, false);
            SetupFiles.HostCheckpoint(baseline);
            Directory.CreateDirectory(SetupFiles.Safe(plan.ArchiveRoot));
            foreach (var item in plan.Versions)
            {
                var destination = ArchivedVersion(plan, item);
                SetupFiles.Safe(item.Directory); SetupFiles.Safe(destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (Directory.Exists(destination) || File.Exists(destination)) throw new ConfigurationException("Archive collision.");
                PackageReader.VerifyDirectory(item.Directory, item.ManifestSha256, true);
                Directory.Move(item.Directory, destination);
                PackageReader.VerifyDirectory(destination, item.ManifestSha256, true);
                SetupFiles.HostCheckpoint(baseline);
            }
            journal = Phase(journalPath, journal, "archived");
            if (SetupFiles.FileHash(SetupService.Selection(plan.Root)) != plan.SelectionSha256 ||
                SetupFiles.Capture(before.ConfigPath, before.LibraryPath).Sha256 != plan.StateSha256 ||
                EvidenceHash(plan.EvidenceRoots, plan.Versions) != plan.EvidenceSha256)
                throw new IOException("Selection, state or evidence changed during retirement; recover before continuing.");
            SetupFiles.HostCheckpoint(baseline);
            if (after is null)
            {
                File.Move(SetupService.Selection(plan.Root), SetupFiles.Under(plan.ArchiveRoot, "install.json"));
                SetupFiles.Atomic(SetupFiles.Under(plan.Root, "uninstalled.json"),
                    new { schemaVersion = 1, operationId = id, archiveRoot = plan.ArchiveRoot, selectionSha256 = plan.SelectionSha256 }, false);
            }
            else SetupFiles.Atomic(SetupService.Selection(plan.Root), after);
            journal = Phase(journalPath, journal, "selected");
            return Finish(journalPath, journal);
        }
        finally { SetupFiles.HostCheckpoint(baseline); }
    }

    public object Recover(string root, string id, string journalSha256)
    {
        root = SetupFiles.Safe(root); Id(id);
        var baseline = SetupFiles.HostHash();
        using var operationLock = new FileStream(SetupFiles.Under(root, ".archsift-setup.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var path = JournalPath(root, id);
            if (SetupFiles.FileHash(path) != journalSha256) throw new ConfigurationException("Retirement journal changed since review.");
            var journal = SetupFiles.Read<RetirementJournal>(path);
            if (journal.SchemaVersion != 1 || journal.Id != id || journal.Plan.Root != root || journal.Phase is "completed" or "aborted")
                throw new ConfigurationException("Retirement journal is not pending for this root.");
            ValidatePlan(journal.Plan, journal.Plan.PlanId, allowExpired: true);
            var plan = journal.Plan;
            var selection = SetupService.Selection(root);
            var committed = journal.After is not null && File.Exists(selection) &&
                SetupFiles.JsonHash(SetupFiles.Read<Installation>(selection)) == SetupFiles.JsonHash(journal.After) ||
                journal.After is null && !File.Exists(selection) && File.Exists(SetupFiles.Under(plan.ArchiveRoot, "install.json")) &&
                SetupFiles.FileHash(SetupFiles.Under(plan.ArchiveRoot, "install.json")) == plan.SelectionSha256;
            if (committed)
            {
                foreach (var item in plan.Versions) PackageReader.VerifyDirectory(ArchivedVersion(plan, item), item.ManifestSha256, true);
                if (SetupFiles.Capture(journal.Before.ConfigPath, journal.Before.LibraryPath).Sha256 != plan.StateSha256 ||
                    EvidenceHash(plan.EvidenceRoots, plan.Versions) != plan.EvidenceSha256)
                    throw new ConfigurationException("Retained state/evidence changed; manual review required.");
                if (journal.After is null && !File.Exists(SetupFiles.Under(root, "uninstalled.json")))
                    SetupFiles.Atomic(SetupFiles.Under(root, "uninstalled.json"),
                        new { schemaVersion = 1, operationId = id, archiveRoot = plan.ArchiveRoot, selectionSha256 = plan.SelectionSha256 }, false);
                return Finish(path, journal with { HostSha256 = baseline });
            }
            if (!File.Exists(selection) || SetupFiles.FileHash(selection) != plan.SelectionSha256)
                throw new ConfigurationException("Selection drift; retirement recovery refuses replacement.");
            if (SetupFiles.Capture(journal.Before.ConfigPath, journal.Before.LibraryPath).Sha256 != plan.StateSha256 ||
                EvidenceHash(plan.EvidenceRoots, plan.Versions) != plan.EvidenceSha256)
                throw new ConfigurationException("Retained state/evidence changed; manual review required.");
            foreach (var item in plan.Versions)
            {
                var archived = ArchivedVersion(plan, item);
                if (!Directory.Exists(archived)) { PackageReader.VerifyDirectory(item.Directory, item.ManifestSha256, true); continue; }
                if (Directory.Exists(item.Directory)) throw new ConfigurationException("Version exists at both original and archive paths.");
                PackageReader.VerifyDirectory(archived, item.ManifestSha256, true);
                SetupFiles.Safe(item.Directory); SetupFiles.Safe(archived);
                Directory.CreateDirectory(Path.GetDirectoryName(item.Directory)!);
                Directory.Move(archived, item.Directory);
            }
            SetupService.LoadInstallation(root);
            Phase(path, journal, "aborted");
            return new { outcome = "aborted-restored", operationId = id, archiveRoot = plan.ArchiveRoot };
        }
        finally { SetupFiles.HostCheckpoint(baseline); }
    }

    private static RetirementPlan Seal(RetirementPlan plan) => plan with { PlanId = SetupFiles.JsonHash(plan with { PlanId = "" }) };
    private static void ValidatePlan(RetirementPlan plan, string reviewedId, bool allowExpired = false)
    {
        if (plan.SchemaVersion != 1 || plan.PlanId != reviewedId || Seal(plan).PlanId != reviewedId ||
            plan.Operation is not ("remove-version" or "uninstall") || !allowExpired &&
            (plan.ExpiresUtc < DateTimeOffset.UtcNow || plan.ExpiresUtc > DateTimeOffset.UtcNow.AddHours(25)))
            throw new ConfigurationException("Invalid, expired or unreviewed retirement plan.");
        if (plan.Root != SetupFiles.Safe(plan.Root) || plan.ArchiveRoot != SetupFiles.Safe(plan.ArchiveRoot) ||
            !string.Equals(Path.GetPathRoot(plan.Root), Path.GetPathRoot(plan.ArchiveRoot), PathSafety.Comparison) ||
            !plan.EvidenceRoots.All(root => root == SetupFiles.Safe(root)) ||
            plan.Operation == "remove-version" && (plan.Version is null || plan.Versions.Length != 1 || plan.Versions[0].Version != plan.Version) ||
            plan.Operation == "uninstall" && (plan.Version is not null || plan.Versions.Length == 0))
            throw new ConfigurationException("Retirement plan paths/versions changed.");
    }
    private static void VerifyCurrent(RetirementPlan plan, Installation before)
    {
        Archive(before, plan.ArchiveRoot);
        foreach (var root in plan.EvidenceRoots)
        {
            PathSafety.EnsureDisjoint(root, plan.ArchiveRoot);
            PathSafety.EnsureDisjoint(root, before.TargetRoot);
            foreach (var item in before.Versions) PathSafety.EnsureDisjoint(root, item.Directory);
        }
        if (SetupFiles.FileHash(SetupService.Selection(plan.Root)) != plan.SelectionSha256 ||
            SetupFiles.Capture(before.ConfigPath, before.LibraryPath).Sha256 != plan.StateSha256)
            throw new ConfigurationException("Stale retirement selection/state.");
        var expected = plan.Operation == "uninstall" ? before.Versions : before.Versions.Where(v => v.Version == plan.Version).ToArray();
        if (plan.Operation == "remove-version" && plan.Version == before.SelectedVersion ||
            SetupFiles.JsonHash(expected) != SetupFiles.JsonHash(plan.Versions))
            throw new ConfigurationException("Owned version set changed.");
        if (Directory.Exists(plan.ArchiveRoot) || File.Exists(plan.ArchiveRoot)) throw new ConfigurationException("Archive destination appeared after review.");
        if (EvidenceHash(plan.EvidenceRoots, plan.Versions) != plan.EvidenceSha256) throw new ConfigurationException("Evidence changed after review.");
        foreach (var item in plan.Versions) PackageReader.VerifyDirectory(item.Directory, item.ManifestSha256, true);
    }
    private static string Archive(Installation before, string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ConfigurationException("Archive requires an absolute path.");
        path = SetupFiles.Safe(path);
        PathSafety.EnsureDisjoint(before.Root, path);
        if (!string.IsNullOrEmpty(before.TargetRoot)) PathSafety.EnsureDisjoint(before.TargetRoot, path);
        if (!string.Equals(Path.GetPathRoot(before.Root), Path.GetPathRoot(path), PathSafety.Comparison))
            throw new ConfigurationException("Recoverable archive must be on the installation volume.");
        return path;
    }
    private static string[] Roots(Installation before, string[] custom, string archive)
    {
        if (custom.Any(root => !Path.IsPathFullyQualified(root))) throw new ConfigurationException("Custom evidence roots require absolute paths.");
        var config = ConfigLoader.Load(before.ConfigPath);
        var roots = new[] { SetupFiles.Under(before.Root, "evidence"), SetupFiles.Safe(config.Output.Directory), before.ConfigPath }
            .Concat(custom.Select(SetupFiles.Safe)).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).ToArray();
        foreach (var root in roots)
        {
            PathSafety.EnsureDisjoint(root, archive);
            PathSafety.EnsureDisjoint(root, before.TargetRoot);
            foreach (var version in before.Versions) PathSafety.EnsureDisjoint(root, version.Directory);
        }
        return roots;
    }
    private static string EvidenceHash(string[] roots, OwnedVersion[] versions)
    {
        var records = new List<object>(); var count = 0;
        void VisitFile(string path)
        {
            SetupFiles.Safe(path);
            if (++count > 100000) throw new ConfigurationException("Retained evidence file-count limit exceeded.");
            var info = new FileInfo(path);
            if (info.Length > 2L * 1024 * 1024 * 1024) throw new ConfigurationException("Retained evidence file exceeds review limit.");
            if (new[] { ".json", ".jsonl", ".sarif", ".log", ".txt", ".html" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                if (info.Length > 32L * 1024 * 1024) throw new ConfigurationException("Text evidence exceeds reference review limit.");
                var content = File.ReadAllText(path, Encoding.UTF8);
                var normalized = content.Replace("\\\\", "\\", StringComparison.Ordinal);
                var referenced = versions.Any(v => normalized.Contains(v.Directory, PathSafety.Comparison) ||
                    normalized.Contains(v.Directory.Replace('\\', '/'), PathSafety.Comparison));
                if (!referenced && Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var document = JsonDocument.Parse(content);
                        referenced = Strings(document.RootElement).Any(value => versions.Any(v =>
                            value.Contains(v.Directory, PathSafety.Comparison) ||
                            value.Contains(v.Directory.Replace('\\', '/'), PathSafety.Comparison)));
                    }
                    catch (JsonException) { /* A malformed retained file remains hashed; raw text was checked. */ }
                }
                if (referenced) throw new ConfigurationException("Retained evidence references a version payload: " + path);
            }
            records.Add(new { path, exists = true, sha256 = SetupFiles.FileHash(path) });
        }
        foreach (var root in roots)
        {
            SetupFiles.Safe(root);
            records.Add(new { path = root, exists = Directory.Exists(root) || File.Exists(root), sha256 = "" });
            if (File.Exists(root)) { VisitFile(root); continue; }
            if (!Directory.Exists(root)) continue;
            var stack = new Stack<string>(); stack.Push(root);
            while (stack.Count > 0)
                foreach (var path in Directory.EnumerateFileSystemEntries(stack.Pop()).Order(StringComparer.Ordinal))
                {
                    SetupFiles.Safe(path);
                    if (Directory.Exists(path)) { stack.Push(path); continue; }
                    VisitFile(path);
                }
        }
        return SetupFiles.JsonHash(records);
    }
    private static IEnumerable<string> Strings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) yield return element.GetString() ?? "";
        else if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject()) foreach (var value in Strings(property.Value)) yield return value;
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) foreach (var value in Strings(item)) yield return value;
    }
    private static string ArchivedVersion(RetirementPlan plan, OwnedVersion version) => SetupFiles.Under(plan.ArchiveRoot, "versions/" + version.Version);
    private static string JournalPath(string root, string id) => SetupFiles.Under(root, "operations/" + id + "/retirement-journal.json");
    private static void Id(string id)
    { if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-f0-9]{32}$")) throw new ConfigurationException("Invalid operation ID."); }
    private static RetirementJournal Phase(string path, RetirementJournal journal, string phase)
    { journal = journal with { Phase = phase }; SetupFiles.Atomic(path, journal); return journal; }
    private static RetirementReceipt Finish(string path, RetirementJournal journal)
    {
        SetupFiles.HostCheckpoint(journal.HostSha256);
        var plan = journal.Plan;
        var receipt = new RetirementReceipt(1, journal.Id, plan.Operation, plan.PlanId, journal.Before,
            journal.After, plan.ArchiveRoot, plan.EvidenceRoots, plan.EvidenceSha256, plan.StateSha256,
            journal.HostSha256, true, "success");
        SetupFiles.Atomic(Path.Combine(Path.GetDirectoryName(path)!, "retirement-receipt.json"), receipt);
        Phase(path, journal, "completed");
        return receipt;
    }
    private static string[] Pending(string root)
    {
        var operations = SetupFiles.Under(root, "operations");
        if (!Directory.Exists(operations)) return [];
        return Directory.GetDirectories(operations).Where(directory =>
        {
            SetupFiles.Safe(directory);
            var standard = Path.Combine(directory, "journal.json");
            var retirement = Path.Combine(directory, "retirement-journal.json");
            return File.Exists(standard) && SetupFiles.Read<Journal>(standard).Phase is not ("completed" or "aborted") ||
                File.Exists(retirement) && SetupFiles.Read<RetirementJournal>(retirement).Phase is not ("completed" or "aborted");
        }).Select(Path.GetFileName).OfType<string>().ToArray();
    }
}
