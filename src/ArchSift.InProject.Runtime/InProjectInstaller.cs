using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record InstallResult(string WorktreeRoot, string Solution, string PackageSha256,
    string ConfigurationPath, string VersionLockPath);

public static class InProjectInstaller
{
    private static readonly string[] IgnoredDirectories = ["runtime", "state", "reports", "evidence"];

    public static InstallResult Initialize(string targetRoot, string packagePath, string packageSha256,
        string? selectedSolution = null)
    {
        var worktree = WorktreeLocator.Locate(targetRoot);
        var root = worktree.Root;
        var destination = Path.Combine(root, ".archsift");
        var ignorePath = Path.Combine(root, ".gitignore");
        PathSafety.EnsureNoLinks(destination);
        PathSafety.EnsureNoLinks(ignorePath);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new ConfigurationException("This worktree already has an InProject installation.");
        var solution = SelectSolution(root, selectedSolution);
        var package = LocalPackage.Inspect(packagePath, packageSha256);
        var stage = Path.Combine(root, ".archsift.stage." + Guid.NewGuid().ToString("N"));
        if (!PathSafety.IsUnder(stage, root) || Directory.Exists(stage) || File.Exists(stage))
            throw new ConfigurationException("Invalid installation staging path.");
        var created = false;
        try
        {
            Directory.CreateDirectory(stage);
            created = true;
            LocalPackage.Extract(package, stage);
            var rulePaths = package.Files.Where(file => file.Path.StartsWith("rules/", StringComparison.Ordinal) &&
                file.Path.EndsWith(".json", StringComparison.Ordinal))
                .Select(file => Path.Combine(stage, file.Path.Replace('/', Path.DirectorySeparatorChar))).ToArray();
            RuleLoader.Compose(rulePaths.Select(RuleLoader.Load));
            foreach (var directory in IgnoredDirectories)
                Directory.CreateDirectory(Path.Combine(stage, directory));
            WriteProject(stage, solution, package);
            WriteLock(stage, package);
            UpdateIgnoreAndMove(ignorePath, stage, destination);
            created = false;
            return new(root, solution, package.PackageSha256,
                Path.Combine(destination, "project.json"), Path.Combine(destination, "version.lock.json"));
        }
        finally
        {
            if (created && Directory.Exists(stage) && PathSafety.IsUnder(stage, root))
            {
                PathSafety.EnsureNoLinks(stage);
                foreach (var entry in Directory.EnumerateFileSystemEntries(stage, "*", SearchOption.AllDirectories))
                    PathSafety.EnsureNoLinks(entry);
                Directory.Delete(stage, recursive: true);
            }
        }
    }

    private static string SelectSolution(string root, string? selected)
    {
        if (selected is not null)
        {
            if (Path.IsPathRooted(selected)) throw new ConfigurationException("Solution selection must be root-relative.");
            var path = Path.GetFullPath(selected, root);
            if (!PathSafety.IsUnder(path, root) || !SupportedSolution(path) || !File.Exists(path) ||
                PathSafety.IsUnder(path, Path.Combine(root, ".archsift")))
                throw new ConfigurationException("Selected solution is outside this worktree or unsupported.");
            PathSafety.EnsureNoLinks(path);
            return path;
        }
        var candidates = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var name = Path.GetFileName(entry);
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) continue;
                if (Directory.Exists(entry))
                {
                    if (name is ".git" or ".archsift" or "bin" or "obj" or ".vs" || name.StartsWith(".archsift.stage.", StringComparison.Ordinal)) continue;
                    pending.Push(entry);
                }
                else if (SupportedSolution(entry)) candidates.Add(Path.GetFullPath(entry));
            }
        }
        if (candidates.Count != 1)
            throw new ConfigurationException(candidates.Count == 0 ? "No .sln or .slnx was found." :
                "Multiple solutions were found; select one explicitly.");
        return candidates[0];
    }

    private static bool SupportedSolution(string path) => Path.GetExtension(path) is ".sln" or ".slnx";

    private static void UpdateIgnoreAndMove(string ignorePath, string stage, string destination)
    {
        var existed = File.Exists(ignorePath);
        using var stream = new FileStream(ignorePath, existed ? FileMode.Open : FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None);
        var originalLength = stream.Length;
        var committed = false;
        try
        {
            if (originalLength > 4 * 1024 * 1024) throw new ConfigurationException(".gitignore is too large to edit safely.");
            var originalBytes = new byte[checked((int)originalLength)];
            stream.ReadExactly(originalBytes);
            var suffix = MissingIgnoreLines(Encoding.UTF8.GetString(originalBytes));
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new ConfigurationException("Another installation appeared during initialization.");
            if (suffix is not null)
            {
                var bytes = new UTF8Encoding(false).GetBytes(suffix);
                stream.Write(bytes);
                stream.Flush(true);
            }
            Directory.Move(stage, destination);
            committed = true;
        }
        finally
        {
            if (!committed)
            {
                stream.SetLength(originalLength);
                stream.Flush(true);
                if (!existed)
                {
                    stream.Dispose();
                    File.Delete(ignorePath);
                }
            }
        }
    }

    private static string? MissingIgnoreLines(string original)
    {
        var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var existing = new HashSet<string>(original.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'), StringComparer.Ordinal);
        var missing = IgnoredDirectories.Select(directory => "/.archsift/" + directory + "/")
            .Where(pattern => !existing.Contains(pattern)).ToArray();
        if (missing.Length == 0) return null;
        var prefix = original.Length == 0 || original.EndsWith('\n') ? "" : newline;
        return prefix + string.Join(newline, missing) + newline;
    }

    private static void WriteProject(string stage, string solution, LocalPackageIdentity package)
    {
        var relativeSolution = Path.GetRelativePath(stage, solution).Replace('\\', '/');
        var rules = package.Files.Where(file => file.Path.StartsWith("rules/", StringComparison.Ordinal) &&
            file.Path.EndsWith(".json", StringComparison.Ordinal)).Select(file => file.Path).Order(StringComparer.Ordinal).ToArray();
        if (rules.Length == 0) throw new ConfigurationException("Package contains no JSON ruleset.");
        var document = new
        {
            schemaVersion = 1,
            product = "ArchSift.InProject",
            solution = relativeSolution,
            rulesets = rules,
            build = new { configuration = "Debug", contexts = Array.Empty<object>(), allowNetwork = false, networkSources = Array.Empty<string>() },
            retention = new { dailyDays = 30, dailyMaxRuns = 100 }
        };
        File.WriteAllText(Path.Combine(stage, "project.json"), JsonSerializer.Serialize(document, JsonContract.Options) + "\n", new UTF8Encoding(false));
    }

    private static void WriteLock(string stage, LocalPackageIdentity package)
    {
        var files = package.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var components = LocalPackage.Components.Select(pair => new { id = pair.Key, sha256 = files[pair.Value].Sha256 })
            .OrderBy(component => component.id, StringComparer.Ordinal).ToArray();
        var document = new
        {
            schemaVersion = 1,
            product = "ArchSift.InProject",
            version = package.Version,
            rid = "win-x64",
            sourceCommit = package.SourceCommit,
            contractVersion = 1,
            packageSha256 = package.PackageSha256,
            payloadManifestSha256 = package.ManifestSha256,
            components
        };
        File.WriteAllText(Path.Combine(stage, "version.lock.json"), JsonSerializer.Serialize(document, JsonContract.Options) + "\n", new UTF8Encoding(false));
    }
}
