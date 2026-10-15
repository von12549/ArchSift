using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record InProjectReviewedFile(string Path, string Sha256);

public sealed record InProjectPackageReviewFile(int SchemaVersion, string Product,
    InProjectReviewedFile[] Files, string[] GeneratedSourceNames);

/// <summary>Operator-reviewed package build inputs, pinned to individual bytes before restore and build.</summary>
public sealed class InProjectPackageReview
{
    private readonly Dictionary<string, string> files;
    private readonly HashSet<string> generatedSourceNames;

    private InProjectPackageReview(string path, string sha256, Dictionary<string, string> files,
        HashSet<string> generatedSourceNames)
    {
        Path = path;
        Sha256 = sha256;
        this.files = files;
        this.generatedSourceNames = generatedSourceNames;
    }

    public string Path { get; }
    public string Sha256 { get; }

    public static InProjectPackageReview? Load(string path)
    {
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path)) return null;
        InProjectPackageReviewFile review;
        try { review = JsonSerializer.Deserialize<InProjectPackageReviewFile>(File.ReadAllBytes(path), JsonContract.Options)!; }
        catch (JsonException error) { throw new ConfigurationException("Invalid build input review: " + error.Message); }
        if (review is null || review.SchemaVersion != 1 || review.Product != "ArchSift.InProject" ||
            review.Files is null || review.GeneratedSourceNames is null)
            throw new ConfigurationException("Unsupported build input review.");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in review.Files)
        {
            if (file is null || !ValidRelative(file.Path) || !ValidHash(file.Sha256) ||
                !files.TryAdd(file.Path.Replace('\\', '/'), file.Sha256.ToLowerInvariant()))
                throw new ConfigurationException("Invalid or duplicate reviewed package file.");
        }
        var generated = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in review.GeneratedSourceNames)
        {
            if (string.IsNullOrWhiteSpace(name) || name != System.IO.Path.GetFileName(name) ||
                !name.EndsWith(".cs", StringComparison.Ordinal) || !generated.Add(name))
                throw new ConfigurationException("Invalid reviewed generated source name.");
        }
        return new(path, LocalPackage.HashFile(path), files, generated);
    }

    public void VerifyFeed(string feed)
    {
        foreach (var item in files)
            VerifyFile(feed, item.Key, item.Value);
    }

    public void VerifyAsset(string packageCache, string packageName, string asset)
    {
        if (asset.EndsWith("/_._", StringComparison.Ordinal)) return;
        var slash = packageName.IndexOf('/');
        if (slash <= 0 || slash == packageName.Length - 1)
            throw new ConfigurationException("Invalid restored package identity.");
        var key = packageName[..slash].ToLowerInvariant() + "/" +
            packageName[(slash + 1)..].ToLowerInvariant() + "/" + asset.Replace('\\', '/');
        if (!files.TryGetValue(key, out var expected))
            throw new ConfigurationException("Unreviewed package build input: " + key);
        VerifyFile(packageCache, key, expected);
    }

    public bool AllowsPackageFile(string packageCache, string path, string sha256)
    {
        if (!PathSafety.IsUnder(path, packageCache)) return false;
        var key = System.IO.Path.GetRelativePath(packageCache, path).Replace('\\', '/');
        return files.TryGetValue(key, out var expected) &&
            expected.Equals(sha256, StringComparison.OrdinalIgnoreCase);
    }

    public bool AllowsGeneratedSource(string relativePath) =>
        relativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase) &&
        generatedSourceNames.Contains(System.IO.Path.GetFileName(relativePath));

    private static void VerifyFile(string root, string relative, string expected)
    {
        var path = System.IO.Path.GetFullPath(relative.Replace('/', System.IO.Path.DirectorySeparatorChar), root);
        if (!PathSafety.IsUnder(path, root)) throw new ConfigurationException("Reviewed package file escapes its root.");
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path) || !LocalPackage.HashFile(path).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new ConfigurationException("Reviewed package file is missing or changed: " + relative);
    }

    private static bool ValidRelative(string path) =>
        !string.IsNullOrWhiteSpace(path) && !System.IO.Path.IsPathRooted(path) &&
        !path.Contains('\\') && !path.Split('/').Any(part => part is "" or "." or "..") &&
        path.Split('/').Length >= 3;

    private static bool ValidHash(string hash) => hash is { Length: 64 } &&
        hash.All(character => Uri.IsHexDigit(character));
}
