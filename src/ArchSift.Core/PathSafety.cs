namespace ArchSift.Core;

public static class PathSafety
{
    public static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    public static bool IsUnder(string path, string root)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relative == "." || (!Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    public static string Under(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Replace('\\', '/').Split('/').Contains("..", StringComparer.Ordinal))
            throw new ConfigurationException("Entry/input must be a root-relative path without traversal: " + relative);
        var path = Path.GetFullPath(relative, root);
        if (!IsUnder(path, root)) throw new ConfigurationException("Path escapes TargetRoot.");
        EnsureNoLinks(path);
        return path;
    }

    public static void EnsureNoLinks(string path)
    {
        var cursor = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(cursor))
        {
            try
            {
                var attributes = File.GetAttributes(cursor);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new ConfigurationException("Path crosses a link/reparse point: " + cursor);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            cursor = Path.GetDirectoryName(cursor);
        }
    }

    public static void EnsureDisjoint(string target, string output)
    {
        if (IsUnder(output, target) || IsUnder(target, output))
            throw new ConfigurationException("Output and TargetRoot must be disjoint.");
        EnsureNoLinks(output);
    }
}
