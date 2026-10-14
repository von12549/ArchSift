using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.InProject.Runtime;

namespace ArchSift.InProject.Maintenance;

public sealed record MaintenanceView(string Worktree, string State, string? Version,
    string? PackageSha256, bool RecoveryRequired, string[] ReceiptIds);
public sealed record MaintenanceReceipt(int SchemaVersion, string Product, string OperationId,
    string Operation, string? OldVersion, string? NewVersion, string? OldPackageSha256,
    string? NewPackageSha256, string BackupSha256, string FinishedUtc, bool HostStateEqual);
internal sealed record MaintenanceJournal(int SchemaVersion, string Product, string OperationId,
    string Operation, string[] OldEntries, string[] NewEntries, string? OldVersion,
    string? NewVersion, string? OldPackageSha256, string? NewPackageSha256);

public static class InProjectMaintenance
{
    private static readonly string[] Managed =
        ["runtime", "tests", "bootstrap", "offline", "licenses", "maintenance", "version.lock.json"];
    private static readonly HashSet<string> ValidEntries = Managed.ToHashSet(StringComparer.Ordinal);

    public static MaintenanceView Inspect(string targetRoot)
    {
        var root = WorktreeLocator.Locate(targetRoot).Root;
        var install = Install(root);
        var journal = JournalPath(install);
        var lockPath = Path.Combine(install, "version.lock.json");
        string? version = null, sha = null;
        if (File.Exists(journal))
            return new(root, "recovery-required", null, null, true, []);
        if (File.Exists(lockPath))
        {
            version = LocalPackage.VerifyInstalled(root);
            sha = PackageSha(lockPath);
        }
        var receiptRoot = ReceiptRoot(install);
        PathSafety.EnsureNoLinks(receiptRoot);
        var receipts = Directory.Exists(receiptRoot)
            ? Directory.EnumerateFiles(receiptRoot, "*.json", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetFileNameWithoutExtension(path)!).Order(StringComparer.Ordinal).ToArray()
            : [];
        return new(root, version is null ? "removed" : "installed", version, sha,
            File.Exists(journal), receipts);
    }

    public static MaintenanceReceipt Upgrade(string targetRoot, string packagePath, string sha256)
    {
        var baseline = InProjectHostIdentity.Capture().Sha256;
        var root = WorktreeLocator.Locate(targetRoot).Root;
        var install = Install(root);
        using var lease = Lease(install);
        EnsureNoJournal(install);
        var oldVersion = LocalPackage.VerifyInstalled(root);
        var oldSha = PackageSha(Path.Combine(install, "version.lock.json"));
        var package = LocalPackage.Inspect(packagePath, sha256);
        if (oldSha == package.PackageSha256)
            throw new ConfigurationException("This exact InProject package is already installed.");
        var operationId = Guid.NewGuid().ToString("N");
        var operationRoot = OperationRoot(install, operationId);
        var stage = Path.Combine(operationRoot, "new");
        Directory.CreateDirectory(stage);
        LocalPackage.Extract(package, stage);
        InProjectInstaller.WriteLock(stage, package);
        EnsureHost(baseline);
        var oldEntries = Managed.Where(name => Exists(Path.Combine(install, name))).ToArray();
        var newEntries = Managed.Where(name => Exists(Path.Combine(stage, name))).ToArray();
        var journal = new MaintenanceJournal(1, "ArchSift.InProject", operationId, "upgrade",
            oldEntries, newEntries, oldVersion, package.Version, oldSha, package.PackageSha256);
        return Switch(root, install, journal, baseline);
    }

    public static MaintenanceReceipt Remove(string targetRoot)
    {
        var baseline = InProjectHostIdentity.Capture().Sha256;
        var root = WorktreeLocator.Locate(targetRoot).Root;
        var install = Install(root);
        using var lease = Lease(install);
        EnsureNoJournal(install);
        var oldVersion = LocalPackage.VerifyInstalled(root);
        var oldSha = PackageSha(Path.Combine(install, "version.lock.json"));
        var operationId = Guid.NewGuid().ToString("N");
        var oldEntries = Managed.Where(name => Exists(Path.Combine(install, name))).ToArray();
        var journal = new MaintenanceJournal(1, "ArchSift.InProject", operationId, "remove",
            oldEntries, [], oldVersion, null, oldSha, null);
        return Switch(root, install, journal, baseline);
    }

    public static MaintenanceReceipt Rollback(string targetRoot, string receiptId)
    {
        var baseline = InProjectHostIdentity.Capture().Sha256;
        var root = WorktreeLocator.Locate(targetRoot).Root;
        var install = Install(root);
        using var lease = Lease(install);
        EnsureNoJournal(install);
        var receipt = ReadReceipt(install, receiptId);
        var currentLock = Path.Combine(install, "version.lock.json");
        if (receipt.NewPackageSha256 is null)
        {
            if (File.Exists(currentLock))
                throw new ConfigurationException("Removed installation changed after the receipt.");
        }
        else if (LocalPackage.VerifyInstalled(root) != receipt.NewVersion ||
            PackageSha(currentLock) != receipt.NewPackageSha256)
            throw new ConfigurationException("Current package differs from the rollback receipt.");
        var saved = Path.Combine(OperationRoot(install, receiptId), "old");
        if (!Directory.Exists(saved) || HashTree(saved) != receipt.BackupSha256)
            throw new ConfigurationException("Retained rollback payload differs from its receipt.");
        var operationId = Guid.NewGuid().ToString("N");
        var stage = Path.Combine(OperationRoot(install, operationId), "new");
        Directory.CreateDirectory(stage);
        foreach (var name in Managed.Where(name => Exists(Path.Combine(saved, name))))
            CopyEntry(Path.Combine(saved, name), Path.Combine(stage, name));
        if (HashTree(stage) != receipt.BackupSha256)
            throw new IOException("Rollback staging copy differs from retained payload.");
        EnsureHost(baseline);
        var journal = new MaintenanceJournal(1, "ArchSift.InProject", operationId, "rollback",
            Managed.Where(name => Exists(Path.Combine(install, name))).ToArray(),
            Managed.Where(name => Exists(Path.Combine(stage, name))).ToArray(),
            receipt.NewVersion, receipt.OldVersion, receipt.NewPackageSha256,
            receipt.OldPackageSha256);
        return Switch(root, install, journal, baseline);
    }

    public static object Recover(string targetRoot)
    {
        var baseline = InProjectHostIdentity.Capture().Sha256;
        var root = WorktreeLocator.Locate(targetRoot).Root;
        var install = Install(root);
        using var lease = Lease(install);
        var journal = ReadJournal(install);
        var receipt = ReceiptPath(install, journal.OperationId);
        if (File.Exists(receipt))
        {
            _ = ReadReceipt(install, journal.OperationId);
            File.Delete(JournalPath(install));
            EnsureHost(baseline);
            return new { journal.OperationId, outcome = "committed", hostStateEqual = true };
        }
        RestoreOld(install, journal);
        EnsureHost(baseline);
        if (journal.OldPackageSha256 is not null) _ = LocalPackage.VerifyInstalled(root);
        File.Delete(JournalPath(install));
        return new { journal.OperationId, outcome = "recovered-old", hostStateEqual = true };
    }

    private static MaintenanceReceipt Switch(string root, string install,
        MaintenanceJournal journal, string baseline)
    {
        var journalPath = JournalPath(install);
        var operationRoot = OperationRoot(install, journal.OperationId);
        var stage = Path.Combine(operationRoot, "new");
        var backup = Path.Combine(operationRoot, "old");
        Directory.CreateDirectory(backup);
        WriteJson(journalPath, journal);
        var committed = false;
        try
        {
            foreach (var name in journal.OldEntries)
                MoveEntry(Path.Combine(install, name), Path.Combine(backup, name));
            EnsureHost(baseline);
            foreach (var name in journal.NewEntries)
                MoveEntry(Path.Combine(stage, name), Path.Combine(install, name));
            EnsureHost(baseline);
            if (journal.NewPackageSha256 is not null)
            {
                var version = LocalPackage.VerifyInstalled(root);
                if (version != journal.NewVersion ||
                    PackageSha(Path.Combine(install, "version.lock.json")) != journal.NewPackageSha256)
                    throw new ConfigurationException("Activated package identity differs from the candidate.");
            }
            else if (Managed.Any(name => Exists(Path.Combine(install, name))))
                throw new ConfigurationException("Removed payload remains active.");
            EnsureHost(baseline);
            var receipt = new MaintenanceReceipt(1, "ArchSift.InProject", journal.OperationId,
                journal.Operation, journal.OldVersion, journal.NewVersion,
                journal.OldPackageSha256, journal.NewPackageSha256, HashTree(backup),
                DateTimeOffset.UtcNow.ToString("O"), true);
            Directory.CreateDirectory(ReceiptRoot(install));
            WriteJson(ReceiptPath(install, journal.OperationId), receipt);
            committed = true;
            File.Delete(journalPath);
            return receipt;
        }
        catch (Exception error) when (error is not HostDriftException && !committed)
        {
            RestoreOld(install, journal);
            File.Delete(journalPath);
            throw;
        }
    }

    private static void RestoreOld(string install, MaintenanceJournal journal)
    {
        var operationRoot = OperationRoot(install, journal.OperationId);
        var backup = Path.Combine(operationRoot, "old");
        var quarantine = Path.Combine(operationRoot, "quarantine");
        foreach (var name in journal.OldEntries.Concat(journal.NewEntries)
            .Distinct(StringComparer.Ordinal).Reverse())
        {
            var active = Path.Combine(install, name);
            var old = Path.Combine(backup, name);
            if (Exists(old))
            {
                if (Exists(active))
                {
                    Directory.CreateDirectory(quarantine);
                    MoveEntry(active, Path.Combine(quarantine, name));
                }
                MoveEntry(old, active);
            }
            else if (!journal.OldEntries.Contains(name, StringComparer.Ordinal) && Exists(active))
            {
                Directory.CreateDirectory(quarantine);
                MoveEntry(active, Path.Combine(quarantine, name));
            }
        }
    }

    private static FileStream Lease(string install)
    {
        var state = Path.Combine(install, "state");
        var path = Path.Combine(state, "task-lease.lock");
        PathSafety.EnsureNoLinks(path);
        if (!Directory.Exists(install))
            throw new ConfigurationException("InProject installation does not exist.");
        Directory.CreateDirectory(state);
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new ConfigurationException("Active InProject task or maintenance owns this worktree."); }
    }

    private static void EnsureHost(string baseline)
    {
        if (InProjectHostIdentity.Capture().Sha256 != baseline)
            throw new HostDriftException("Host environment/Profile drift; maintenance journal retained.");
    }

    private static void EnsureNoJournal(string install)
    {
        if (File.Exists(JournalPath(install)))
            throw new ConfigurationException("Incomplete maintenance exists; run recover before another operation.");
    }

    private static MaintenanceJournal ReadJournal(string install)
    {
        var path = JournalPath(install);
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path)) throw new ConfigurationException("No maintenance journal to recover.");
        var value = JsonSerializer.Deserialize<MaintenanceJournal>(File.ReadAllBytes(path), JsonContract.Options);
        if (value is null || value.SchemaVersion != 1 || value.Product != "ArchSift.InProject" ||
            !Guid.TryParseExact(value.OperationId, "N", out _) ||
            value.Operation is not ("upgrade" or "rollback" or "remove") ||
            value.OldEntries is null || value.NewEntries is null ||
            value.OldEntries.Distinct(StringComparer.Ordinal).Count() != value.OldEntries.Length ||
            value.NewEntries.Distinct(StringComparer.Ordinal).Count() != value.NewEntries.Length ||
            value.OldEntries.Concat(value.NewEntries).Any(name => !ValidEntries.Contains(name)))
            throw new ConfigurationException("Invalid maintenance journal.");
        return value;
    }

    private static MaintenanceReceipt ReadReceipt(string install, string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ConfigurationException("Invalid receipt ID.");
        var path = ReceiptPath(install, id);
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path)) throw new ConfigurationException("Rollback receipt not found.");
        var value = JsonSerializer.Deserialize<MaintenanceReceipt>(File.ReadAllBytes(path), JsonContract.Options);
        if (value is null || value.SchemaVersion != 1 || value.Product != "ArchSift.InProject" ||
            value.OperationId != id || value.BackupSha256 is null || value.BackupSha256.Length != 64)
            throw new ConfigurationException("Invalid rollback receipt.");
        return value;
    }

    private static string PackageSha(string lockPath)
    {
        PathSafety.EnsureNoLinks(lockPath);
        using var document = JsonDocument.Parse(File.ReadAllBytes(lockPath));
        return document.RootElement.GetProperty("packageSha256").GetString() ??
            throw new ConfigurationException("Package SHA-256 missing from lock.");
    }

    private static string HashTree(string directory)
    {
        PathSafety.EnsureNoLinks(directory);
        var lines = SafeFiles(directory)
            .Select(path =>
            {
                PathSafety.EnsureNoLinks(path);
                return Path.GetRelativePath(directory, path).Replace('\\', '/') + "\0" +
                    LocalPackage.HashFile(path);
            }).Order(StringComparer.Ordinal).ToArray();
        return ContentHash.Text(string.Join("\n", lines));
    }

    private static void CopyEntry(string source, string target)
    {
        PathSafety.EnsureNoLinks(source);
        PathSafety.EnsureNoLinks(target);
        if (Exists(target)) throw new ConfigurationException("Rollback staging entry exists.");
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
            if (LocalPackage.HashFile(source) != LocalPackage.HashFile(target))
                throw new IOException("Rollback staging file changed during copy.");
            return;
        }
        Directory.CreateDirectory(target);
        var pending = new Stack<string>();
        pending.Push(source);
        while (pending.Count > 0)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                PathSafety.EnsureNoLinks(path);
                var relative = Path.GetRelativePath(source, path);
                var output = Path.GetFullPath(relative, target);
                if (!PathSafety.IsUnder(output, target))
                    throw new ConfigurationException("Rollback copy escaped staging.");
                if (Directory.Exists(path)) { Directory.CreateDirectory(output); pending.Push(path); }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    File.Copy(path, output);
                    if (LocalPackage.HashFile(path) != LocalPackage.HashFile(output))
                        throw new IOException("Rollback staging file changed during copy.");
                }
            }
        }
    }

    private static IEnumerable<string> SafeFiles(string directory)
    {
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.Count > 0)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                PathSafety.EnsureNoLinks(entry);
                if (Directory.Exists(entry)) pending.Push(entry);
                else yield return entry;
            }
        }
    }

    private static void MoveEntry(string source, string target)
    {
        PathSafety.EnsureNoLinks(source);
        PathSafety.EnsureNoLinks(target);
        if (!Exists(source) || Exists(target) || !PathSafety.IsUnder(target, InstallFromEntry(target)))
            throw new ConfigurationException("Unsafe maintenance move.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(source)) File.Move(source, target);
        else Directory.Move(source, target);
    }

    private static string InstallFromEntry(string target)
    {
        var cursor = Path.GetFullPath(target);
        while (cursor is not null && Path.GetFileName(cursor) != ".archsift")
            cursor = Path.GetDirectoryName(cursor);
        return cursor ?? throw new ConfigurationException("Maintenance path is outside .archsift.");
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    private static string Install(string root)
    {
        var path = Path.Combine(root, ".archsift");
        PathSafety.EnsureNoLinks(path);
        return path;
    }
    private static string OperationRoot(string install, string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ConfigurationException("Invalid operation ID.");
        var path = Path.Combine(install, "state", "maintenance", id);
        PathSafety.EnsureNoLinks(path);
        return path;
    }
    private static string JournalPath(string install) =>
        Path.Combine(install, "state", "maintenance-journal.json");
    private static string ReceiptRoot(string install) =>
        Path.Combine(install, "state", "maintenance-receipts");
    private static string ReceiptPath(string install, string id) =>
        Path.Combine(ReceiptRoot(install), id + ".json");

    private static void WriteJson<T>(string path, T value)
    {
        PathSafety.EnsureNoLinks(path);
        if (File.Exists(path)) throw new ConfigurationException("Maintenance file already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonContract.Options) + "\n",
                new UTF8Encoding(false));
            File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private sealed class HostDriftException(string message) : InvalidOperationException(message);
}
