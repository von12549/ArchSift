using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public static class InProjectWorktreeExport
{
    public static InProjectExportPreview Preview(string targetRoot) =>
        Plan(InProjectConfiguration.Load(targetRoot));

    public static InProjectExportManifest Export(string targetRoot, string destinationDirectory)
    {
        var config = InProjectConfiguration.Load(targetRoot);
        var destination = Path.GetFullPath(destinationDirectory);
        PathSafety.EnsureNoLinks(destination);
        if (Directory.Exists(destination) || File.Exists(destination) ||
            PathSafety.IsUnder(destination, config.Root) || Path.GetPathRoot(destination) == destination)
            throw new ConfigurationException("Worktree export must use a new directory outside the checkout.");
        RejectAnyCheckout(destination);
        if (!Directory.Exists(Path.GetDirectoryName(destination)))
            throw new ConfigurationException("Worktree export parent directory is missing.");
        var lease = Path.Combine(config.InstallationRoot, "state", "task-lease.lock");
        PathSafety.EnsureNoLinks(lease);
        using var guard = new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var plan = Plan(config);
        if (plan.Limitations.Length > 0)
            throw new ConfigurationException("Worktree export preview has limitations: " +
                string.Join("; ", plan.Limitations));
        Directory.CreateDirectory(destination);
        var files = new List<InProjectEvidenceFile>();
        foreach (var source in plan.Sources)
        {
            var paths = File.Exists(source.SourcePath) ? [source.SourcePath] :
                Directory.EnumerateFiles(source.SourcePath, "*", SearchOption.AllDirectories).ToArray();
            foreach (var original in paths)
            {
                PathSafety.EnsureNoLinks(original);
                var relative = File.Exists(source.SourcePath) ? Path.GetFileName(original) :
                    Path.GetRelativePath(source.SourcePath, original);
                var logical = source.LogicalPrefix + "/" + relative.Replace('\\', '/');
                var copy = Path.GetFullPath(logical.Replace('/', Path.DirectorySeparatorChar), destination);
                if (!PathSafety.IsUnder(copy, destination))
                    throw new ConfigurationException("Worktree export path escaped its destination.");
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(original, copy, overwrite: false);
                var sha = LocalPackage.HashFile(original);
                if (LocalPackage.HashFile(copy) != sha)
                    throw new IOException("Worktree export changed during copy.");
                files.Add(new(logical, new FileInfo(copy).Length, sha));
            }
        }
        var manifest = new InProjectExportManifest(1, "ArchSift.InProject",
            ContentHash.Text(config.Root), DateTimeOffset.UtcNow.ToString("O"),
            files.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray());
        File.WriteAllText(Path.Combine(destination, "export-manifest.json"),
            JsonSerializer.Serialize(manifest, JsonContract.Options) + "\n", new UTF8Encoding(false));
        Verify(destination);
        return manifest;
    }

    public static InProjectExportManifest Verify(string exportDirectory)
    {
        var directory = Path.GetFullPath(exportDirectory);
        PathSafety.EnsureNoLinks(directory);
        if (!Directory.Exists(directory)) throw new ConfigurationException("Worktree export is missing.");
        foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
            PathSafety.EnsureNoLinks(path);
        var manifestPath = Path.Combine(directory, "export-manifest.json");
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 32 * 1024 * 1024)
            throw new ConfigurationException("Worktree export manifest is missing or too large.");
        var manifest = JsonSerializer.Deserialize<InProjectExportManifest>(File.ReadAllBytes(manifestPath),
            JsonContract.Options) ?? throw new ConfigurationException("Worktree export manifest is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Product != "ArchSift.InProject" ||
            manifest.WorktreeId is not { Length: 64 } || manifest.Files is null)
            throw new ConfigurationException("Worktree export identity is invalid.");
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Path) || file.Path.StartsWith('/') || file.Path.Contains('\\') ||
                file.Path.Split('/').Any(part => part is "" or "." or "..") ||
                !listed.Add(file.Path) || file.Bytes < 0 || file.Sha256 is not { Length: 64 })
                throw new ConfigurationException("Unsafe worktree export entry.");
            var path = Path.GetFullPath(file.Path.Replace('/', Path.DirectorySeparatorChar), directory);
            if (!PathSafety.IsUnder(path, directory) || !File.Exists(path) ||
                new FileInfo(path).Length != file.Bytes || LocalPackage.HashFile(path) != file.Sha256)
                throw new ConfigurationException("Worktree export item changed: " + file.Path);
        }
        var actual = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/'))
            .Where(path => path != "export-manifest.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actual.SetEquals(listed)) throw new ConfigurationException("Worktree export has unlisted files.");
        return manifest;
    }

    private static InProjectExportPreview Plan(InProjectLoadedConfiguration config)
    {
        var sources = new List<InProjectExportSource>();
        var limits = new List<string>();
        void Add(string kind, string path, string prefix)
        {
            PathSafety.EnsureNoLinks(path);
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            var files = File.Exists(path) ? [path] :
                Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToArray();
            foreach (var file in files) PathSafety.EnsureNoLinks(file);
            sources.Add(new(kind, path, prefix, files.Length,
                files.Sum(file => new FileInfo(file).Length)));
        }
        foreach (var name in new[] { "project.json", "version.lock.json" })
            Add("configuration", Path.Combine(config.InstallationRoot, name), "configuration");
        foreach (var name in new[] { "rules", "tests", "bootstrap" })
            Add("configuration", Path.Combine(config.InstallationRoot, name), "configuration/" + name);
        var state = Path.Combine(config.InstallationRoot, "state");
        Add("history", Path.Combine(state, "tasks"), "history/tasks");
        foreach (var name in new[] { "current-task.json", "pins.json", "local-settings.json" })
            Add("history", Path.Combine(state, name), "history");
        Add("reports", Path.Combine(config.InstallationRoot, "reports"), "reports");
        var evidenceRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(config.InstallationRoot, "evidence"), config.EvidenceRoot
        };
        var tasks = Path.Combine(state, "tasks");
        if (Directory.Exists(tasks))
            foreach (var path in Directory.EnumerateFiles(tasks, "*.json", SearchOption.TopDirectoryOnly))
            {
                var task = InProjectRetentionService.ReadTask(path);
                if (task.ArchivePath is not null)
                    Add("archive", task.ArchivePath, "archives/" + task.RunId);
                if (task.ResultPath is null) continue;
                var report = Path.Combine(config.InstallationRoot,
                    task.ResultPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(report)) continue;
                var gate = JsonSerializer.Deserialize<InProjectGateResult>(File.ReadAllBytes(report),
                    JsonContract.Options);
                if (gate?.EvidenceGrade != "full") continue;
                var manifest = gate.Materials.IndexPath;
                if (Path.IsPathFullyQualified(manifest))
                    evidenceRoots.Add(Path.GetDirectoryName(Path.GetDirectoryName(manifest)!)!);
            }
        foreach (var root in evidenceRoots.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root)) continue;
            var owner = Path.Combine(root, "owner.json");
            PathSafety.EnsureNoLinks(owner);
            if (!File.Exists(owner))
            {
                if (Directory.EnumerateFileSystemEntries(root).Any())
                    limits.Add("Unowned evidence root: " + root);
                continue;
            }
            using var document = JsonDocument.Parse(File.ReadAllBytes(owner));
            if (document.RootElement.GetProperty("worktreeRoot").GetString() != config.Root ||
                document.RootElement.GetProperty("worktreeId").GetString() != ContentHash.Text(config.Root))
            { limits.Add("Other-worktree evidence root: " + root); continue; }
            Add("full-evidence", root, "full/" + ContentHash.Text(root)[..16]);
        }
        return new(1, "ArchSift.InProject", ContentHash.Text(config.Root), sources.ToArray(),
            limits.ToArray());
    }

    private static void RejectAnyCheckout(string destination)
    {
        var parent = Path.GetDirectoryName(destination);
        while (parent is not null)
        {
            if (File.Exists(Path.Combine(parent, ".git")) ||
                Directory.Exists(Path.Combine(parent, ".git")))
                throw new ConfigurationException("Worktree export cannot enter a source checkout.");
            parent = Path.GetDirectoryName(parent);
        }
    }
}
