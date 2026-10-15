using System.Text.Json;
using System.Reflection;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record InProjectContextSelection
{
    public required string Project { get; init; }
    public required string TargetFramework { get; init; }
}

public sealed record InProjectBuildSelection
{
    public required string Configuration { get; init; }
    public required InProjectContextSelection[] Contexts { get; init; }
    public required bool AllowNetwork { get; init; }
    public required string[] NetworkSources { get; init; }
}

public sealed record InProjectRetention
{
    public required int DailyDays { get; init; }
    public required int DailyMaxRuns { get; init; }
}

public sealed record InProjectProjectFile
{
    public required int SchemaVersion { get; init; }
    public required string Product { get; init; }
    public required string Solution { get; init; }
    public required string[] Rulesets { get; init; }
    public required InProjectBuildSelection Build { get; init; }
    public required InProjectRetention Retention { get; init; }
}

public sealed record InProjectLocalSettings
{
    public required int SchemaVersion { get; init; }
    public string? EvidenceRoot { get; init; }
    public string? LocalFeed { get; init; }
}

public sealed record InProjectLoadedConfiguration(string Root, string InstallationRoot, string SolutionPath,
    string Configuration, InProjectContextSelection[] Contexts, string LocalFeed,
    string ProjectSha256, string RulesSha256, string ToolSha256, LoadedRuleset[] Rules,
    string EvidenceRoot, InProjectRetention Retention)
{
    public InProjectPackageReview? PackageReview { get; init; }
}

public static class InProjectConfiguration
{
    public static InProjectLoadedConfiguration Load(string targetRoot)
    {
        var root = WorktreeLocator.Locate(targetRoot).Root;
        var install = Path.Combine(root, ".archsift");
        LocalPackage.VerifyInstalled(root);
        VerifyExecutingPayload(install);
        var path = Path.Combine(install, "project.json");
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path)) throw new ConfigurationException("InProject project.json is missing.");
        InProjectProjectFile project;
        try { project = JsonSerializer.Deserialize<InProjectProjectFile>(File.ReadAllBytes(path), JsonContract.Options)!; }
        catch (JsonException error) { throw new ConfigurationException("Invalid InProject project.json: " + error.Message); }
        if (project is null || project.Build is null || project.Retention is null || project.Rulesets is null ||
            project.Build.Contexts is null || project.Build.NetworkSources is null ||
            project.SchemaVersion != 1 || project.Product != "ArchSift.InProject" ||
            project.Build.Configuration is not ("Debug" or "Release") ||
            project.Retention.DailyDays < 1 || project.Retention.DailyMaxRuns < 1)
            throw new ConfigurationException("Unsupported InProject project configuration.");
        if (project.Build.AllowNetwork)
            throw new ConfigurationException("Network restore is unavailable in this implementation; use an explicit local feed.");
        if (project.Build.NetworkSources.Length != 0)
            throw new ConfigurationException("Network sources require explicit network support.");
        var solution = ResolveOwned(root, install, project.Solution, root, ".sln", ".slnx");
        var ruleRoot = Path.Combine(install, "rules");
        var rulePaths = project.Rulesets.Select(name => ResolveOwned(root, install, name, ruleRoot, ".json")).ToArray();
        if (rulePaths.Length == 0 || rulePaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != rulePaths.Length)
            throw new ConfigurationException("One or more distinct rulesets are required.");
        var loadedRules = rulePaths.Select(RuleLoader.Load).ToArray();
        RuleLoader.Compose(loadedRules);
        var contexts = project.Build.Contexts.Select(context =>
        {
            if (context is null) throw new ConfigurationException("Null InProject build context.");
            var projectPath = ResolveOwned(root, install, context.Project, root, ".csproj");
            if (context.TargetFramework is not ("net8.0" or "net9.0" or "net10.0"))
                throw new ConfigurationException("Unsupported selected target framework.");
            return new InProjectContextSelection
            {
                Project = Path.GetRelativePath(root, projectPath).Replace('\\', '/'),
                TargetFramework = context.TargetFramework
            };
        }).ToArray();
        if (contexts.Select(c => c.Project + "\0" + c.TargetFramework).Distinct(StringComparer.OrdinalIgnoreCase).Count() != contexts.Length)
            throw new ConfigurationException("Duplicate InProject build context.");
        var localSettingsPath = Path.Combine(install, "state", "local-settings.json");
        string? configuredFeed = null;
        string? configuredEvidence = null;
        if (File.Exists(localSettingsPath))
        {
            PathSafety.EnsureNoLinks(localSettingsPath);
            InProjectLocalSettings settings;
            try { settings = JsonSerializer.Deserialize<InProjectLocalSettings>(File.ReadAllBytes(localSettingsPath), JsonContract.Options)!; }
            catch (JsonException error) { throw new ConfigurationException("Invalid local settings: " + error.Message); }
            if (settings is null || settings.SchemaVersion != 1)
                throw new ConfigurationException("Unsupported local settings version.");
            configuredFeed = settings.LocalFeed;
            configuredEvidence = settings.EvidenceRoot;
        }
        if (configuredFeed is not null && !Path.IsPathFullyQualified(configuredFeed))
            throw new ConfigurationException("Local package feed must be an absolute path.");
        var bundledFeed = Path.Combine(install, "offline");
        var useBundledFeed = configuredFeed is null && Directory.Exists(bundledFeed) &&
            Directory.EnumerateFiles(bundledFeed, "*.nupkg", SearchOption.TopDirectoryOnly).Any();
        var feed = Path.GetFullPath(configuredFeed ?? (useBundledFeed ? bundledFeed : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")));
        if (!Path.IsPathFullyQualified(feed) ||
            (PathSafety.IsUnder(feed, root) && !(useBundledFeed &&
                feed.Equals(bundledFeed, StringComparison.OrdinalIgnoreCase))) ||
            !Directory.Exists(feed)) throw new ConfigurationException("Explicit local package feed is missing or overlaps the worktree.");
        PathSafety.EnsureNoLinks(feed);
        if (configuredEvidence is not null && !Path.IsPathFullyQualified(configuredEvidence))
            throw new ConfigurationException("External evidence root must be an absolute path.");
        var evidence = Path.GetFullPath(configuredEvidence ?? Path.Combine(install, "evidence"));
        if (configuredEvidence is not null &&
            (PathSafety.IsUnder(evidence, root) || Path.GetPathRoot(evidence) == evidence))
            throw new ConfigurationException("External evidence root overlaps a checkout or volume root.");
        PathSafety.EnsureNoLinks(evidence);
        var rulesHash = ContentHash.Text(string.Join("\n", loadedRules.Select(rule =>
            rule.Identity.Path + "\0" + rule.Identity.Sha256).Order(StringComparer.Ordinal)));
        var lockHash = LocalPackage.HashFile(Path.Combine(install, "version.lock.json"));
        var manifestHash = LocalPackage.HashFile(Path.Combine(install, "runtime", "payload-manifest.json"));
        var review = InProjectPackageReview.Load(Path.Combine(install, "state", "build-input-review.json"));
        return new(root, install, solution, project.Build.Configuration, contexts, feed,
            LocalPackage.HashFile(path), rulesHash, ContentHash.Text(lockHash + "\n" + manifestHash + "\n" +
                (review?.Sha256 ?? "none")), loadedRules, evidence, project.Retention)
        { PackageReview = review };
    }

    private static void VerifyExecutingPayload(string install)
    {
        foreach (var assembly in new[] { typeof(InProjectConfiguration).Assembly,
                     typeof(JsonContract).Assembly, typeof(InputCapture).Assembly,
                     Assembly.GetEntryAssembly() }.Where(assembly => assembly is not null))
        {
            var name = assembly!.GetName().Name;
            if (name is not ("ArchSift.InProject.Runtime" or "ArchSift.Contracts" or
                "ArchSift.Core" or "archsift-inproject")) continue;
            var installed = Path.Combine(install, "runtime", name + ".dll");
            PathSafety.EnsureNoLinks(installed);
            if (!File.Exists(installed) || LocalPackage.HashFile(installed) != LocalPackage.HashFile(assembly.Location))
                throw new ConfigurationException("Executing InProject component differs from installed version lock: " + name);
        }
    }

    private static string ResolveOwned(string root, string configurationDirectory, string relative,
        string allowedRoot, params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new ConfigurationException("InProject configured path must be relative to project.json.");
        var path = Path.GetFullPath(relative.Replace('/', Path.DirectorySeparatorChar), configurationDirectory);
        if (!PathSafety.IsUnder(path, root) || !PathSafety.IsUnder(path, allowedRoot) ||
            (allowedRoot == root && PathSafety.IsUnder(path, Path.Combine(root, ".archsift"))) ||
            !extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) || !File.Exists(path))
            throw new ConfigurationException("Configured InProject path is missing or outside its owned scope: " + relative);
        PathSafety.EnsureNoLinks(path);
        return path;
    }
}
