using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using ArchSift.Contracts;

namespace ArchSift.Core;

public sealed class SourceChangedException() : Exception("source-changed-during-analysis");

public static class InputCapture
{
    public static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    { ".git", ".vs", ".idea", "bin", "obj", "artifacts", ".archsift" };
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ".cs", ".csproj", ".fsproj", ".vbproj", ".sln", ".slnx", ".props", ".targets", ".resx" };

    public static InputIdentity Capture(string root, CancellationToken cancellationToken = default)
    {
        root = Path.GetFullPath(root);
        PathSafety.EnsureNoLinks(root);
        if (!Directory.Exists(root)) throw new ConfigurationException("TargetRoot does not exist.");
        var selected = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var file in Walk(root, false, cancellationToken))
            if (Extensions.Contains(Path.GetExtension(file)) ||
                Path.GetFileName(file) is "global.json" or "NuGet.Config" or "nuget.config")
                selected.Add(file);
        foreach (var project in selected.Where(p => Path.GetExtension(p).Equals(".csproj", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            XDocument xml;
            try { xml = ReadXml(project); } catch (XmlException) { continue; }
            foreach (var item in xml.Descendants().Where(n => n.Name.LocalName is
                         "Compile" or "Content" or "None" or "EmbeddedResource" or "AdditionalFiles" or "Analyzer"))
            {
                var include = (string?)item.Attribute("Include");
                if (string.IsNullOrWhiteSpace(include) || include.Contains("$(", StringComparison.Ordinal) ||
                    include.Contains(';') || Path.IsPathRooted(include)) continue;
                var full = Path.GetFullPath(include.Replace('\\', Path.DirectorySeparatorChar), Path.GetDirectoryName(project)!);
                if (!PathSafety.IsUnder(full, root)) continue;
                if (include.Contains('*') || include.Contains('?'))
                {
                    // Explicit includes may point into normally excluded bin/obj.
                    var pattern = Path.GetRelativePath(root, full).Replace('\\', '/');
                    var selector = new Selector { Kind = "source-file", Match = "glob", Value = pattern };
                    foreach (var file in Walk(root, true, cancellationToken))
                        if (SelectorMatcher.Matches(selector, Path.GetRelativePath(root, file).Replace('\\', '/'))) selected.Add(file);
                }
                else if (File.Exists(full)) { PathSafety.EnsureNoLinks(full); selected.Add(full); }
            }
        }
        var records = new List<InputFile>();
        foreach (var file in selected.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            PathSafety.EnsureNoLinks(file);
            using var stream = File.OpenRead(file);
            records.Add(new(Path.GetRelativePath(root, file).Replace('\\', '/'),
                Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(), stream.Length, "analysis"));
        }
        var identity = ContentHash.Text(string.Join("\n", records.Select(f => $"{f.Path}\0{f.Length}\0{f.Sha256}")));
        return new(identity, records.ToArray());
    }

    public static void VerifyUnchanged(string root, InputIdentity before, CancellationToken token = default)
    {
        if (Capture(root, token).Sha256 != before.Sha256) throw new SourceChangedException();
    }

    public static XDocument ReadXml(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    private static IEnumerable<string> Walk(string root, bool includeGenerated, CancellationToken token)
    {
        var directories = new Stack<string>();
        directories.Push(root);
        var count = 0;
        while (directories.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                var name = Path.GetFileName(entry);
                if (name is ".git" or ".vs" or ".idea" or ".archsift" ||
                    (!includeGenerated && ExcludedDirectories.Contains(name))) continue;
                PathSafety.EnsureNoLinks(entry);
                if (Directory.Exists(entry)) directories.Push(entry);
                else
                {
                    if (++count > 100000) throw new ConfigurationException("Target exceeds the 100000-file scan limit.");
                    yield return entry;
                }
            }
        }
    }
}
