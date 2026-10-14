using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public static class InProjectFullEvidence
{
    public static InProjectEvidencePrunePreview Prune(string targetRoot, string runId,
        string bundleDirectory, string archivePath, bool apply)
    {
        var config = InProjectConfiguration.Load(targetRoot);
        ValidateRunId(runId);
        var lease = Path.Combine(config.InstallationRoot, "state", "task-lease.lock");
        if (!apply) return PrunePreview(config, runId, bundleDirectory, archivePath);
        PathSafety.EnsureNoLinks(lease);
        using var guard = new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var preview = PrunePreview(config, runId, bundleDirectory, archivePath);
        if (!preview.CanDelete)
            throw new ConfigurationException("Full evidence is protected: " + string.Join(", ", preview.ProtectedBy));
        var taskPath = Path.Combine(config.InstallationRoot, "state", "tasks", runId + ".json");
        var task = InProjectRetentionService.ReadTask(taskPath);
        Directory.Delete(preview.BundlePath, recursive: true);
        InProjectRetentionService.SaveTask(taskPath, task with { MaterialsState = "archived",
            ArchivePath = preview.ArchivePath, Revision = task.Revision + 1 });
        return preview;
    }

    private static InProjectEvidencePrunePreview PrunePreview(InProjectLoadedConfiguration config,
        string runId, string bundleDirectory, string archivePath)
    {
        var bundle = Path.GetFullPath(bundleDirectory);
        if (Path.GetFileName(bundle) != runId)
            throw new ConfigurationException("Selected full evidence path does not match run ID.");
        var owner = Path.Combine(Path.GetDirectoryName(bundle)!, "owner.json");
        PathSafety.EnsureNoLinks(owner);
        if (!File.Exists(owner)) throw new ConfigurationException("Full evidence root has no owner marker.");
        using (var document = JsonDocument.Parse(File.ReadAllBytes(owner)))
            if (document.RootElement.GetProperty("product").GetString() != "ArchSift.InProject" ||
                document.RootElement.GetProperty("worktreeRoot").GetString() != config.Root ||
                document.RootElement.GetProperty("worktreeId").GetString() != ContentHash.Text(config.Root))
                throw new ConfigurationException("Full evidence root belongs to another worktree.");
        var expected = Verify(bundle, runId);
        if (expected.WorktreeId != ContentHash.Text(config.Root))
            throw new ConfigurationException("Full evidence belongs to another worktree.");
        var archived = VerifyArchive(archivePath);
        if (JsonSerializer.Serialize(archived, JsonContract.Options) !=
            JsonSerializer.Serialize(expected, JsonContract.Options))
            throw new ConfigurationException("Archive does not contain the selected full evidence.");
        var task = InProjectRetentionService.ReadTask(Path.Combine(config.InstallationRoot,
            "state", "tasks", runId + ".json"));
        var protections = new List<string>();
        if (task.EvidenceGrade != "full" || task.MaterialsState != "available")
            protections.Add("not-available-full");
        if (task.Phase is not ("completed" or "failed" or "cancelled")) protections.Add("active");
        if (InProjectRetentionService.ReadCurrentId(config) == runId) protections.Add("current");
        if (InProjectRetentionService.Pinned(config).Contains(runId)) protections.Add("pinned");
        var archive = Path.GetFullPath(archivePath);
        return new(runId, bundle, archive, LocalPackage.HashFile(archive),
            expected.Files.Sum(file => file.Bytes), expected.Files.Length,
            protections.ToArray(), protections.Count == 0);
    }

    public static string Archive(string bundleDirectory, string archivePath)
    {
        var manifest = Verify(bundleDirectory);
        var bundle = Path.GetFullPath(bundleDirectory);
        var archive = Path.GetFullPath(archivePath);
        if (Path.GetExtension(archive) != ".zip" || PathSafety.IsUnder(archive, bundle) ||
            File.Exists(archive) || Directory.Exists(archive))
            throw new ConfigurationException("Full evidence archive destination is invalid or occupied.");
        RejectBusinessSourceDestination(archive);
        PathSafety.EnsureNoLinks(archive);
        var parent = Path.GetDirectoryName(archive)!;
        if (!Directory.Exists(parent)) throw new ConfigurationException("Archive parent directory is missing.");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            foreach (var relative in manifest.Files.Select(file => file.Path).Append("manifest.json"))
            {
                var entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
                using var input = File.OpenRead(Path.Combine(bundle, relative.Replace('/', Path.DirectorySeparatorChar)));
                using var output = entry.Open();
                input.CopyTo(output);
            }
        }
        VerifyArchive(archive);
        return LocalPackage.HashFile(archive);
    }

    public static InProjectEvidenceManifest VerifyArchive(string archivePath)
    {
        var archive = Path.GetFullPath(archivePath);
        PathSafety.EnsureNoLinks(archive);
        if (!File.Exists(archive)) throw new ConfigurationException("Full evidence archive is missing.");
        using var zip = ZipFile.OpenRead(archive);
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName != "manifest.json") ValidateRelative(entry.FullName);
            if (!entries.TryAdd(entry.FullName, entry) || entry.FullName.EndsWith('/') ||
                (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 ||
                ((entry.ExternalAttributes >> 16) & 0xf000) is not (0 or 0x8000))
                throw new ConfigurationException("Unsafe or duplicated full evidence archive item.");
        }
        if (!entries.TryGetValue("manifest.json", out var manifestEntry) ||
            manifestEntry.Length > 16 * 1024 * 1024)
            throw new ConfigurationException("Full evidence archive manifest is missing or too large.");
        InProjectEvidenceManifest manifest;
        using (var stream = manifestEntry.Open())
            manifest = JsonSerializer.Deserialize<InProjectEvidenceManifest>(stream, JsonContract.Options)
                ?? throw new ConfigurationException("Full evidence archive manifest is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Product != "ArchSift.InProject" ||
            string.IsNullOrWhiteSpace(manifest.ProductVersion) ||
            manifest.RulesetVersions is not { Length: > 0 } ||
            !Guid.TryParseExact(manifest.RunId, "N", out _) || manifest.Files is null ||
            !ValidHash(manifest.WorktreeId) || !ValidHash(manifest.InputSha256) ||
            !ValidHash(manifest.RulesSha256) || !ValidHash(manifest.ToolSha256) ||
            entries.Count != manifest.Files.Length + 1)
            throw new ConfigurationException("Full evidence archive identity is invalid.");
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            ValidateRelative(file.Path);
            if (!listed.Add(file.Path) || !entries.TryGetValue(file.Path, out var entry) ||
                file.Bytes < 0 || entry.Length != file.Bytes || !ValidHash(file.Sha256))
                throw new ConfigurationException("Full evidence archive manifest does not cover its entries.");
            using var content = entry.Open();
            if (Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant() != file.Sha256)
                throw new ConfigurationException("Full evidence archive bytes changed: " + file.Path);
        }
        return manifest;
    }

    public static InProjectEvidenceManifest Restore(string archivePath, string destinationDirectory)
    {
        var manifest = VerifyArchive(archivePath);
        var destination = Path.GetFullPath(destinationDirectory);
        PathSafety.EnsureNoLinks(destination);
        if (File.Exists(destination) || Directory.Exists(destination) ||
            Path.GetPathRoot(destination) == destination)
            throw new ConfigurationException("Full evidence restore destination must be a new directory.");
        RejectBusinessSourceDestination(destination);
        var parent = Path.GetDirectoryName(destination)!;
        if (!Directory.Exists(parent)) throw new ConfigurationException("Restore parent directory is missing.");
        Directory.CreateDirectory(destination);
        using (var zip = ZipFile.OpenRead(archivePath))
        {
            foreach (var entry in zip.Entries)
            {
                var path = Path.GetFullPath(entry.FullName.Replace('/', Path.DirectorySeparatorChar), destination);
                if (!PathSafety.IsUnder(path, destination))
                    throw new ConfigurationException("Archive item escaped restore destination.");
                PathSafety.EnsureNoLinks(path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var input = entry.Open();
                using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
            }
        }
        var restored = Verify(destination, manifest.RunId);
        if (JsonSerializer.Serialize(restored, JsonContract.Options) !=
            JsonSerializer.Serialize(manifest, JsonContract.Options))
            throw new ConfigurationException("Restored full evidence manifest changed.");
        return restored;
    }

    public static string Seal(InProjectLoadedConfiguration config, string runId, string inputSha)
    {
        ValidateRunId(runId);
        var root = config.EvidenceRoot;
        EnsureOwnedRoot(root, config.Root);
        var target = Path.Combine(root, runId);
        var stage = Path.Combine(root, ".partial-" + runId + "-" + Guid.NewGuid().ToString("N"));
        foreach (var path in new[] { target, stage }) PathSafety.EnsureNoLinks(path);
        if (Directory.Exists(target) || Directory.Exists(stage))
            throw new ConfigurationException("Full evidence run already exists.");
        Directory.CreateDirectory(stage);
        var pairs = new[]
        {
            (Source: Path.Combine(config.InstallationRoot, "state", "build", runId), Name: "build"),
            (Source: Path.Combine(config.InstallationRoot, "reports", runId), Name: "report"),
            (Source: Path.Combine(config.InstallationRoot, "runtime"), Name: "tool"),
            (Source: Path.Combine(config.InstallationRoot, "state", "cache", "packages"), Name: "packages")
        };
        foreach (var (source, name) in pairs)
        {
            if (!Directory.Exists(source)) throw new ConfigurationException("Full evidence source is missing: " + name);
            PathSafety.EnsureNoLinks(source);
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                PathSafety.EnsureNoLinks(directory);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                PathSafety.EnsureNoLinks(file);
                var relative = Path.GetRelativePath(source, file);
                var destination = Path.GetFullPath(relative, Path.Combine(stage, name));
                if (!PathSafety.IsUnder(destination, stage))
                    throw new ConfigurationException("Full evidence file escaped its owned stage.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: false);
                if (LocalPackage.HashFile(file) != LocalPackage.HashFile(destination))
                    throw new IOException("Full evidence copy differs from source: " + relative);
            }
        }
        foreach (var name in new[] { "project.json", "version.lock.json" })
            CopyFile(Path.Combine(config.InstallationRoot, name),
                Path.Combine(stage, "configuration", name));
        if (LocalPackage.HashFile(Path.Combine(stage, "configuration", "project.json")) !=
            config.ProjectSha256)
            throw new ConfigurationException("Full evidence project configuration changed.");
        foreach (var name in new[] { "rules", "tests", "bootstrap" })
        {
            var source = Path.Combine(config.InstallationRoot, name);
            PathSafety.EnsureNoLinks(source);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                CopyFile(file, Path.Combine(stage, "configuration", name, relative));
            }
        }
        foreach (var ruleset in config.Rules)
        {
            var relative = Path.GetRelativePath(config.InstallationRoot, ruleset.Identity.Path);
            var copy = Path.Combine(stage, "configuration", relative);
            if (LocalPackage.HashFile(copy) != ruleset.Identity.Sha256)
                throw new ConfigurationException("Full evidence ruleset changed.");
        }
        LocalPackage.VerifyInstalled(config.Root);
        var captured = JsonSerializer.Deserialize<InProjectDailyBinding>(File.ReadAllBytes(
            Path.Combine(config.InstallationRoot, "reports", runId, "daily-binding.json")), JsonContract.Options)
            ?? throw new ConfigurationException("Full evidence has no daily binding capture.");
        var closure = new List<InProjectEvidenceClosure>();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in CapturedInputs(captured))
        {
            var path = Path.GetFullPath(input.Path);
            PathSafety.EnsureNoLinks(path);
            if (!File.Exists(path) || LocalPackage.HashFile(path) != input.Sha256)
                throw new ConfigurationException("Full evidence dependency changed: " + path);
            if (seen.TryGetValue(path, out var prior))
            {
                if (prior != input.Sha256) throw new ConfigurationException("Conflicting full evidence dependency bytes.");
                continue;
            }
            seen.Add(path, input.Sha256);
            var logical = LogicalPath(path, config, runId);
            var copy = Path.GetFullPath(logical.Replace('/', Path.DirectorySeparatorChar), stage);
            if (!PathSafety.IsUnder(copy, stage))
                throw new ConfigurationException("Full evidence closure escaped its owned stage.");
            if (!File.Exists(copy)) CopyFile(path, copy);
            if (LocalPackage.HashFile(copy) != input.Sha256)
                throw new ConfigurationException("Full evidence closure differs from captured input.");
            closure.Add(new(path, logical, input.Sha256));
        }
        File.WriteAllText(Path.Combine(stage, "closure.json"),
            JsonSerializer.Serialize(closure.OrderBy(item => item.OriginalPath, StringComparer.OrdinalIgnoreCase),
                JsonContract.Options) + "\n", new UTF8Encoding(false));
        var files = Files(stage);
        using var versionLock = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(stage,
            "configuration", "version.lock.json")));
        var productVersion = versionLock.RootElement.GetProperty("version").GetString() ?? "";
        var rulesetVersions = config.Rules.Select(ruleset => ruleset.Ruleset.Id + "@" +
            ruleset.Ruleset.Version + ":" + ruleset.Identity.Sha256)
            .Order(StringComparer.Ordinal).ToArray();
        var manifest = new InProjectEvidenceManifest(1, "ArchSift.InProject",
            productVersion, rulesetVersions, runId,
            ContentHash.Text(config.Root), inputSha, config.RulesSha256, config.ToolSha256, files);
        File.WriteAllText(Path.Combine(stage, "manifest.json"),
            JsonSerializer.Serialize(manifest, JsonContract.Options) + "\n", new UTF8Encoding(false));
        Verify(stage, runId);
        Directory.Move(stage, target);
        Verify(target, runId);
        return target;
    }

    public static InProjectEvidenceManifest Verify(string bundleDirectory, string? expectedRunId = null)
    {
        var root = Path.GetFullPath(bundleDirectory);
        PathSafety.EnsureNoLinks(root);
        if (!Directory.Exists(root)) throw new ConfigurationException("Full evidence directory is missing.");
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            PathSafety.EnsureNoLinks(directory);
        var manifestPath = Path.Combine(root, "manifest.json");
        PathSafety.EnsureNoLinks(manifestPath);
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 16 * 1024 * 1024)
            throw new ConfigurationException("Full evidence manifest is missing or too large.");
        var manifest = JsonSerializer.Deserialize<InProjectEvidenceManifest>(File.ReadAllBytes(manifestPath),
            JsonContract.Options) ?? throw new ConfigurationException("Full evidence manifest is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Product != "ArchSift.InProject" ||
            string.IsNullOrWhiteSpace(manifest.ProductVersion) ||
            manifest.RulesetVersions is not { Length: > 0 } ||
            (expectedRunId is not null && manifest.RunId != expectedRunId) ||
            !Guid.TryParseExact(manifest.RunId, "N", out _) || manifest.Files is null ||
            !ValidHash(manifest.WorktreeId) || !ValidHash(manifest.InputSha256) ||
            !ValidHash(manifest.RulesSha256) || !ValidHash(manifest.ToolSha256))
            throw new ConfigurationException("Full evidence identity is invalid.");
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in manifest.Files)
        {
            ValidateRelative(item.Path);
            if (!listed.Add(item.Path) || !ValidHash(item.Sha256) || item.Bytes < 0)
                throw new ConfigurationException("Duplicate or invalid full evidence item.");
            var path = Path.GetFullPath(item.Path.Replace('/', Path.DirectorySeparatorChar), root);
            if (!PathSafety.IsUnder(path, root) || !File.Exists(path))
                throw new ConfigurationException("Full evidence item is missing or escaped.");
            PathSafety.EnsureNoLinks(path);
            if (new FileInfo(path).Length != item.Bytes || LocalPackage.HashFile(path) != item.Sha256)
                throw new ConfigurationException("Full evidence item changed: " + item.Path);
        }
        var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => path != "manifest.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actual.SetEquals(listed) || !listed.Any(path => path.StartsWith("build/source/", StringComparison.Ordinal)) ||
            !listed.Contains("report/gate-result.json") || !listed.Contains("report/daily-index.json") ||
            !listed.Contains("tool/payload-manifest.json") || !listed.Contains("configuration/project.json") ||
            !listed.Contains("closure.json"))
            throw new ConfigurationException("Full evidence has missing or unlisted dependencies.");
        var report = Path.Combine(root, "report", "gate-result.json");
        var gate = JsonSerializer.Deserialize<InProjectGateResult>(File.ReadAllBytes(report), JsonContract.Options);
        if (gate is null || gate.RunId != manifest.RunId || gate.EvidenceGrade != "full" ||
            gate.WorktreeId != manifest.WorktreeId || gate.Binding.RunId != manifest.RunId)
            throw new ConfigurationException("Full evidence report identity differs from manifest.");
        using (var versionLock = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,
                   "configuration", "version.lock.json"))))
            if (versionLock.RootElement.GetProperty("version").GetString() != manifest.ProductVersion)
                throw new ConfigurationException("Full evidence product version changed.");
        var ruleFiles = Directory.EnumerateFiles(Path.Combine(root, "configuration", "rules"),
            "*.json", SearchOption.AllDirectories).ToArray();
        var actualRules = ruleFiles.Select(RuleLoader.Load).Select(ruleset =>
            ruleset.Ruleset.Id + "@" + ruleset.Ruleset.Version + ":" + ruleset.Identity.Sha256)
            .Order(StringComparer.Ordinal).ToArray();
        if (!actualRules.SequenceEqual(manifest.RulesetVersions, StringComparer.Ordinal))
            throw new ConfigurationException("Full evidence ruleset versions changed.");
        var capture = JsonSerializer.Deserialize<InProjectDailyBinding>(File.ReadAllBytes(
            Path.Combine(root, "report", "daily-binding.json")), JsonContract.Options)
            ?? throw new ConfigurationException("Full evidence binding capture is empty.");
        var verdict = JsonSerializer.Deserialize<InProjectBindingVerdict>(File.ReadAllBytes(
            Path.Combine(root, "report", "binding-verdict.json")), JsonContract.Options)
            ?? throw new ConfigurationException("Full evidence binding verdict is empty.");
        if (capture.RunId != manifest.RunId || capture.WorktreeId != manifest.WorktreeId ||
            capture.InputSha256 != manifest.InputSha256 || capture.RulesSha256 != manifest.RulesSha256 ||
            capture.ToolSha256 != manifest.ToolSha256 ||
            verdict.RunId != manifest.RunId || gate.Binding.CaptureSha256 != verdict.CaptureSha256 ||
            gate.Binding.SourceBound != verdict.SourceBound ||
            verdict.CaptureSha256 != LocalPackage.HashFile(Path.Combine(root, "report", "daily-binding.json")))
            throw new ConfigurationException("Full evidence binding identity differs from source capture.");
        var closure = JsonSerializer.Deserialize<InProjectEvidenceClosure[]>(File.ReadAllBytes(
            Path.Combine(root, "closure.json")), JsonContract.Options)
            ?? throw new ConfigurationException("Full evidence closure is empty.");
        var mapped = new Dictionary<string, InProjectEvidenceClosure>(StringComparer.OrdinalIgnoreCase);
        var fileHashes = manifest.Files.ToDictionary(file => file.Path, file => file.Sha256,
            StringComparer.OrdinalIgnoreCase);
        foreach (var entry in closure)
        {
            if (string.IsNullOrWhiteSpace(entry.OriginalPath) || !Path.IsPathFullyQualified(entry.OriginalPath) ||
                !ValidHash(entry.Sha256))
                throw new ConfigurationException("Full evidence closure identity is invalid.");
            ValidateRelative(entry.LogicalPath);
            if (!mapped.TryAdd(entry.OriginalPath, entry) ||
                !fileHashes.TryGetValue(entry.LogicalPath, out var hash) || hash != entry.Sha256)
                throw new ConfigurationException("Full evidence closure mapping is invalid.");
        }
        foreach (var input in CapturedInputs(capture))
            if (!mapped.TryGetValue(Path.GetFullPath(input.Path), out var entry) ||
                entry.Sha256 != input.Sha256)
                throw new ConfigurationException("Full evidence omitted a captured dependency.");
        return manifest;
    }

    private static void EnsureOwnedRoot(string evidenceRoot, string worktreeRoot)
    {
        PathSafety.EnsureNoLinks(evidenceRoot);
        if (!Directory.Exists(evidenceRoot)) Directory.CreateDirectory(evidenceRoot);
        var owner = Path.Combine(evidenceRoot, "owner.json");
        PathSafety.EnsureNoLinks(owner);
        if (!File.Exists(owner))
        {
            if (Directory.EnumerateFileSystemEntries(evidenceRoot).Any())
                throw new ConfigurationException("Unowned evidence root is not empty.");
            File.WriteAllText(owner, JsonSerializer.Serialize(new { schemaVersion = 1,
                product = "ArchSift.InProject", worktreeRoot, worktreeId = ContentHash.Text(worktreeRoot) },
                JsonContract.Options) + "\n", new UTF8Encoding(false));
        }
        using var document = JsonDocument.Parse(File.ReadAllBytes(owner));
        if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
            document.RootElement.GetProperty("product").GetString() != "ArchSift.InProject" ||
            document.RootElement.GetProperty("worktreeRoot").GetString() != worktreeRoot ||
            document.RootElement.GetProperty("worktreeId").GetString() != ContentHash.Text(worktreeRoot))
            throw new ConfigurationException("Evidence root belongs to another worktree.");
    }

    private static InProjectEvidenceFile[] Files(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Select(path =>
            {
                PathSafety.EnsureNoLinks(path);
                return new InProjectEvidenceFile(Path.GetRelativePath(root, path).Replace('\\', '/'),
                    new FileInfo(path).Length, LocalPackage.HashFile(path));
            }).ToArray();

    private static void ValidateRunId(string runId)
    {
        if (!Guid.TryParseExact(runId, "N", out _)) throw new ConfigurationException("Invalid full evidence run ID.");
    }

    private static void ValidateRelative(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') || name.StartsWith('/') ||
            name.Split('/').Any(part => part is "" or "." or "..") ||
            name is "manifest.json" || name != "closure.json" &&
            !new[] { "build/", "report/", "tool/", "packages/", "configuration/", "closure/" }
                .Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            throw new ConfigurationException("Unsafe full evidence relative path.");
    }

    private static bool ValidHash(string? value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static IEnumerable<(string Path, string Sha256)> CapturedInputs(InProjectDailyBinding capture) =>
        capture.Contexts.SelectMany(context => context.Materials.Select(item => (item.Path, item.Sha256))
            .Concat(context.References.Select(item => (item.Path, item.Sha256)))
            .Append((context.OutputPath, context.OutputSha256)));

    private static string LogicalPath(string path, InProjectLoadedConfiguration config, string runId)
    {
        var build = Path.Combine(config.InstallationRoot, "state", "build", runId);
        var cache = Path.Combine(config.InstallationRoot, "state", "cache", "packages");
        var runtime = Path.Combine(config.InstallationRoot, "runtime");
        if (PathSafety.IsUnder(path, build))
            return "build/" + Path.GetRelativePath(build, path).Replace('\\', '/');
        if (PathSafety.IsUnder(path, cache))
            return "packages/" + Path.GetRelativePath(cache, path).Replace('\\', '/');
        if (PathSafety.IsUnder(path, runtime))
            return "tool/" + Path.GetRelativePath(runtime, path).Replace('\\', '/');
        return "closure/" + ContentHash.Text(path) + "/" + Path.GetFileName(path);
    }

    private static void CopyFile(string source, string destination)
    {
        PathSafety.EnsureNoLinks(source);
        PathSafety.EnsureNoLinks(destination);
        if (!File.Exists(source)) throw new ConfigurationException("Full evidence source file is missing.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: false);
        if (LocalPackage.HashFile(source) != LocalPackage.HashFile(destination))
            throw new IOException("Full evidence file changed during copy.");
    }

    private static void RejectBusinessSourceDestination(string destination)
    {
        var parent = Path.GetDirectoryName(destination);
        while (parent is not null)
        {
            if (File.Exists(Path.Combine(parent, ".git")) ||
                Directory.Exists(Path.Combine(parent, ".git")))
            {
                if (!PathSafety.IsUnder(destination, Path.Combine(parent, ".archsift", "evidence")))
                    throw new ConfigurationException("Evidence output cannot enter business source paths.");
                return;
            }
            parent = Path.GetDirectoryName(parent);
        }
    }
}
