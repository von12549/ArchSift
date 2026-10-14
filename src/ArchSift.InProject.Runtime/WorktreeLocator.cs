using System.Diagnostics;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record WorktreeIdentity(string Root, string CommonGitDirectory);

public static class WorktreeLocator
{
    public static WorktreeIdentity Locate(string requestedRoot)
    {
        var root = Path.GetFullPath(requestedRoot);
        if (!Directory.Exists(root)) throw new ConfigurationException("Target checkout does not exist.");
        PathSafety.EnsureNoLinks(root);
        var top = Query(root, "--show-toplevel");
        var actual = Path.GetFullPath(top);
        if (!string.Equals(actual.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), PathSafety.Comparison))
            throw new ConfigurationException("Target must be the Git worktree root.");
        var common = Query(root, "--git-common-dir");
        var commonPath = Path.GetFullPath(common, root);
        PathSafety.EnsureNoLinks(commonPath);
        return new(root, commonPath);
    }

    private static string Query(string root, string argument)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("rev-parse");
        process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new IOException("Git worktree query did not start.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            throw new ConfigurationException("Git worktree query failed: " + stderr.Trim());
        return stdout.Trim();
    }
}
