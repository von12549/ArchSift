using System.Diagnostics;
using System.Text.RegularExpressions;
using ArchSift.Contracts;

namespace ArchSift.Core;

/// <summary>Read-only Git plumbing. Never checkout, fetch, execute hooks, or run target code.</summary>
public static class GitSnapshots
{
    private static readonly string[] Options = ["--no-replace-objects", "-c", "core.fsmonitor=false"];
    private static async Task<string> Git(string root, string[] args, string home, CancellationToken token)
    {
        var result = await SafeProcess.RunAsync("git", root, [.. Options, .. args], home, null, token);
        if (result.ExitCode != 0 || result.Output.Contains("[output truncated]", StringComparison.Ordinal))
            throw new ConfigurationException("Git operation failed: " + result.Error.Trim());
        return result.Output;
    }

    public static async Task<string> Resolve(string root, string reference, string home, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 1024 || reference.Contains('\0'))
            throw new ConfigurationException("Invalid Git commit reference.");
        var sha = (await Git(root, ["rev-parse", "--verify", "--end-of-options", reference + "^{commit}"], home, token)).Trim();
        if (!Regex.IsMatch(sha, "^[0-9a-f]{40}([0-9a-f]{24})?$", RegexOptions.CultureInvariant))
            throw new ConfigurationException("Reference did not resolve to a local commit.");
        return sha;
    }

    public static async Task<string> RepositoryRoot(string root, string home, CancellationToken token) =>
        (await Git(root, ["rev-parse", "--show-toplevel"], home, token)).Trim();

    public static async Task<string[]> WorktreeFiles(string root, string home, CancellationToken token) =>
        (await Git(root, ["ls-files", "-z", "--cached", "--others", "--exclude-standard"], home, token))
            .Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public static async Task<string[]> Materialize(string repository, string relativeRoot, string commit,
        string destination, string home, CancellationToken token)
    {
        var tree = commit + (relativeRoot == "." ? "" : ":" + relativeRoot.Replace('\\', '/'));
        var listing = await Git(repository, ["ls-tree", "-r", "-z", tree], home, token);
        var entries = listing.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (entries.Length > 100000) throw new ConfigurationException("Git tree exceeds 100000 files.");
        var paths = new List<string>(); var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        long total = 0;
        Directory.CreateDirectory(destination);
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            var tab = entry.IndexOf('\t');
            if (tab < 0) throw new InvalidDataException("Malformed Git tree.");
            var metadata = entry[..tab].Split(' '); var relative = entry[(tab + 1)..];
            if (metadata.Length != 3 || metadata[0] is not ("100644" or "100755") || metadata[1] != "blob")
                throw new ConfigurationException("Unsupported Git link/submodule: " + relative);
            if (!seen.Add(relative)) throw new ConfigurationException("Git paths collide on this platform: " + relative);
            var path = PathSafety.Under(destination, relative);
            paths.Add(relative);
            // Copy blobs directly: export-ignore/export-subst, filters, and checkout hooks cannot alter inputs.
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var start = new ProcessStartInfo("git") { WorkingDirectory = repository, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (var argument in Options.Concat(["cat-file", "blob", metadata[2]])) start.ArgumentList.Add(argument);
            start.Environment["DOTNET_CLI_HOME"] = home; start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
            using var child = Process.Start(start)!;
            var error = child.StandardError.ReadToEndAsync(token);
            try
            {
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                var buffer = new byte[65536]; int count; long length = 0;
                while ((count = await child.StandardOutput.BaseStream.ReadAsync(buffer, token)) != 0)
                {
                    total += count; length += count;
                    if (length > 128 * 1024 * 1024 || total > 512 * 1024 * 1024)
                        throw new ConfigurationException("Git snapshot exceeds byte limits (128 MiB/file, 512 MiB/tree).");
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                }
                await child.WaitForExitAsync(token);
                if (child.ExitCode != 0) throw new IOException("Git blob read failed: " + await error);
            }
            finally { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(CancellationToken.None); } }
        }
        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    public static void CopyInputs(string root, string destination, InputIdentity identity, CancellationToken token)
    {
        Directory.CreateDirectory(destination);
        foreach (var input in identity.Files)
        {
            token.ThrowIfCancellationRequested();
            var source = PathSafety.Under(root, input.Path); var target = PathSafety.Under(destination, input.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target, false);
            if (AssemblyArtifacts.FileHash(target) != input.Sha256) throw new SourceChangedException();
        }
        InputCapture.VerifyUnchanged(root, identity, token);
    }
}
