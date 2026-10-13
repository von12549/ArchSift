using System.Text.RegularExpressions;
using ArchSift.Contracts;

namespace ArchSift.Core;

internal static class CompilerInputs
{
    public static object[] Audit(string stdout, string cwd, string project, string sourceRoot, string snapshotRoot,
        EvidenceCapture capture, List<string> unknowns, CancellationToken token)
    {
        var records = new List<object>();
        foreach (var line in stdout.Split('\n').Where(l => l.Contains("csc.dll", StringComparison.OrdinalIgnoreCase) || l.Contains("csc.exe", StringComparison.OrdinalIgnoreCase)))
        {
            var arguments = Tokens(line); if (!arguments.Any(a => a.StartsWith("/out:", StringComparison.OrdinalIgnoreCase))) continue;
            var files = new List<string>();
            void Add(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var path = Path.GetFullPath(value, cwd); PathSafety.EnsureNoLinks(path); files.Add(path);
            }
            foreach (var argument in arguments)
            {
                token.ThrowIfCancellationRequested();
                if (argument.StartsWith('@')) { Add(argument[1..]); unknowns.Add("Compiler response file requires explicit expansion for " + project); }
                else if (!argument.StartsWith('/') && argument.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) Add(argument);
                else foreach (var flag in new[] { "/reference:", "/analyzer:", "/analyzerconfig:", "/additionalfile:", "/resource:", "/link:", "/keyfile:", "/ruleset:", "/sourcelink:", "/win32res:" })
                    if (argument.StartsWith(flag, StringComparison.OrdinalIgnoreCase))
                    {
                        var value = argument[flag.Length..]; if (flag == "/resource:") value = value.Split(',')[0];
                        foreach (var part in File.Exists(Path.GetFullPath(value, cwd)) ? new[] { value } : value.Split(','))
                            Add(Regex.Replace(part, @"^[A-Za-z_][A-Za-z0-9_]*=", ""));
                    }
            }
            var facts = new List<object>();
            foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(file)) { unknowns.Add("Compiler file is missing for " + project); continue; }
                var sha = AssemblyArtifacts.FileHash(file, token); var kind = "unknown";
                var relative = PathSafety.IsUnder(file, snapshotRoot) ? Path.GetRelativePath(snapshotRoot, file).Replace('\\', '/') : PathSafety.IsUnder(file, sourceRoot) ? Path.GetRelativePath(sourceRoot, file).Replace('\\', '/') : null;
                if (relative is not null && capture.InputIdentity.Files.Any(f => f.Path == relative && f.Sha256 == sha)) kind = "captured-source";
                else if (capture.ExternalInputs.Any(f => string.Equals(f.Path, file, PathSafety.Comparison) && f.Sha256 == sha)) kind = "captured-external";
                else if (PathSafety.IsUnder(file, snapshotRoot)) kind = "generated-input";
                else unknowns.Add("Compiler input needs recapture: " + file);
                facts.Add(new { path = file, sha256 = sha, kind });
            }
            records.Add(new { sourceProject = project, commandSha256 = ContentHash.Text(line), inputs = facts });
        }
        if (records.Count == 0) unknowns.Add("Actual compiler invocation was not found for " + project);
        return records.ToArray();
    }
    internal static string[] Tokens(string line) => Regex.Matches(line, "(?:[^\\s\"]+|\"[^\"]*\")+", RegexOptions.CultureInvariant)
        .Select(m => m.Value.Replace("\"", "")).ToArray();
}
