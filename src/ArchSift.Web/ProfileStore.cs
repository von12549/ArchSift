using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Setup;

namespace ArchSift.Web;

public sealed record ProfileHealth(ProfileEntry Entry, string Status, string? Error, RunConfiguration? Config,
    SessionProfile? Session, string? Sha256);

public sealed class ProfileStore
{
    public Installation Installation { get; }
    public string Root { get; }
    public string ConfigRoot { get; }
    public string CatalogPath { get; }
    public string SelectionPath { get; }
    public ProfileStore(string root)
    {
        Root = SetupFiles.Safe(root); Installation = SetupService.LoadInstallation(Root);
        ConfigRoot = SetupFiles.Under(Root, "config");
        CatalogPath = SetupFiles.Under(Root, "config/profiles.json"); SelectionPath = SetupFiles.Under(Root, "config/selection.json");
        if (Installation.ConfigPath.Equals(CatalogPath, PathSafety.Comparison) || Installation.ConfigPath.Equals(SelectionPath, PathSafety.Comparison))
            throw new ConfigurationException("Launcher metadata conflicts with existing managed configuration; preserve it for review.");
    }
    public string? CatalogHash => File.Exists(CatalogPath) ? SetupFiles.FileHash(CatalogPath) : null;
    public ProfileCatalog ReadCatalog()
    {
        var catalog = File.Exists(CatalogPath) ? Read<ProfileCatalog>(CatalogPath, "profile-catalog") : new(1, null, []);
        if (catalog.Profiles.Select(p => p.ProfileId).Distinct(StringComparer.Ordinal).Count() != catalog.Profiles.Length ||
            catalog.Profiles.Select(p => Path.GetFullPath(p.ConfigPath)).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() != catalog.Profiles.Length ||
            catalog.DefaultProfileId is { } id && !catalog.Profiles.Any(p => p.ProfileId == id))
            throw new ConfigurationException("Duplicate profile identity/path or dangling default; preserve catalog for review.");
        foreach (var profile in catalog.Profiles)
            if (string.IsNullOrWhiteSpace(profile.Name) || !Path.IsPathFullyQualified(profile.ConfigPath) ||
                !Path.GetFullPath(profile.ConfigPath).Equals(profile.ConfigPath, PathSafety.Comparison))
                throw new ConfigurationException("Profile names and canonical absolute paths are required.");
        return catalog;
    }
    public ProfileSelection? ReadSelection() => File.Exists(SelectionPath) ? Read<ProfileSelection>(SelectionPath, "profile-selection") : null;

    public ProfileHealth Inspect(ProfileEntry profile)
    {
        try
        {
            var path = ConfigPath(profile.ConfigPath);
            if (!File.Exists(path)) return new(profile, "missing", "Configuration file is missing.", null, null, null);
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new ConfigurationException("Config exceeds 4 MiB.");
            var hash = SetupFiles.FileHash(path);
            var bytes = File.ReadAllBytes(path);
            using (var doc = JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0)))
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("schemaVersion", out var version) &&
                    version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number) && number > 1)
                    return new(profile, "incompatible", "Unsupported profile schema version.", null, null, hash);
            var config = ConfigLoader.Load(path);
            ValidateConfiguration(path, config);
            if (SetupFiles.FileHash(path) != hash) throw new ConfigurationException("Configuration changed during validation.");
            var protectedConfig = path.Equals(Installation.ConfigPath, PathSafety.Comparison) && PathSafety.IsUnder(path, ConfigRoot);
            var library = SetupService.Library(config);
            var protectedLibrary = library.Equals(Installation.LibraryPath, PathSafety.Comparison);
            SetupService.ValidateState(config);
            if (protectedConfig && (!config.Target.Root.Equals(Installation.TargetRoot, PathSafety.Comparison) || !protectedLibrary))
                throw new ConfigurationException("Managed configuration changed target/library; explicit setup review required.");
            return new(profile, "valid", null, config, new(profile.Name, path, protectedConfig, protectedLibrary), hash);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ConfigurationException or JsonException)
        { return new(profile, "invalid", error.Message, null, null, null); }
    }
    public void ValidateConfiguration(string configPath, RunConfiguration config)
    {
        SchemaValidation.Validate(JsonSerializer.SerializeToElement(config, JsonContract.Options), "config");
        var target = SetupFiles.Safe(config.Target.Root);
        if (!Directory.Exists(target)) throw new ConfigurationException("Target directory does not exist.");
        PathSafety.EnsureDisjoint(target, Root); PathSafety.EnsureDisjoint(target, configPath);
        if (config.Target.Entry is { } entry)
        {
            var path = PathSafety.Under(target, entry);
            if (!File.Exists(path) || Path.GetExtension(path).ToLowerInvariant() is not (".sln" or ".slnx" or ".csproj"))
                throw new ConfigurationException("An existing root-relative solution/project entry is required.");
        }
        var library = SetupService.Library(config); var output = SetupFiles.Safe(config.Output.Directory);
        if (File.Exists(library) || File.Exists(output)) throw new ConfigurationException("Library/output paths must be directories.");
        SetupFiles.Safe(Path.Combine(library, "chains"));
        foreach (var path in config.Build.AssemblyPaths.Concat(new[] { config.Build.AssemblyManifest, config.Build.LocalFeed, config.Build.CacheDirectory }.OfType<string>()))
            SetupFiles.Safe(path);
        if (config.Build.CacheDirectory is { } cache) PathSafety.EnsureDisjoint(target, cache);
        PathSafety.EnsureDisjoint(target, library); PathSafety.EnsureDisjoint(target, output); PathSafety.EnsureDisjoint(configPath, library);
        PathSafety.EnsureDisjoint(configPath, output);
        if (PathSafety.IsUnder(output, library)) throw new ConfigurationException("Reports must not be inside the library.");
        foreach (var destination in new[] { library, output })
            foreach (var reserved in new[] { "versions", "operations", "install.json", ".archsift-setup.lock", "config/profiles.json", "config/selection.json", "config/.archsift-profiles.lock" })
                PathSafety.EnsureDisjoint(SetupFiles.Under(Root, reserved), destination);
    }
    public RunConfiguration Preview(string path, RunConfiguration config)
    {
        path = ConfigPath(path);
        if (!PathSafety.IsUnder(path, ConfigRoot)) throw new ConfigurationException("New profiles must be inside config.");
        SetupFiles.ValidateRelative(Path.GetRelativePath(ConfigRoot, path).Replace('\\', '/'));
        config = ConfigLoader.ResolvePaths(config, Path.GetDirectoryName(path)!);
        ValidateConfiguration(path, config); return config;
    }
    public ProfileEntry Register(string name, string path, string? expectedHash, RunConfiguration? create = null)
    {
        path = ConfigPath(path);
        if (string.IsNullOrWhiteSpace(name)) throw new ConfigurationException("Profile name is required.");
        using var lease = Lock(); CheckHash(expectedHash);
        var catalog = ReadCatalog();
        if (catalog.Profiles.Length >= 100 || catalog.Profiles.Any(p => p.ConfigPath.Equals(path, PathSafety.Comparison)))
            throw new ConfigurationException("Profile limit or duplicate canonical path.");
        var profile = new ProfileEntry(Guid.NewGuid().ToString("N"), name, path);
        if (create is not null)
        {
            if (!PathSafety.IsUnder(path, ConfigRoot) || File.Exists(path) || path.Equals(Installation.ConfigPath, PathSafety.Comparison))
                throw new ConfigurationException("New external profile requires an unused config-directory path; managed state is not repaired here.");
            create = Preview(path, create);
            SetupFiles.Atomic(path, create, false);
        }
        else
        {
            var health = Inspect(profile);
            if (health.Status != "valid") throw new ConfigurationException(health.Error ?? "Invalid profile.");
        }
        var next = catalog with { Profiles = [.. catalog.Profiles, profile],
            DefaultProfileId = catalog.DefaultProfileId ?? (path.Equals(Installation.ConfigPath, PathSafety.Comparison) ? profile.ProfileId : null) };
        SchemaValidation.Validate(JsonSerializer.SerializeToElement(next, JsonContract.Options), "profile-catalog");
        CheckHash(expectedHash); SetupFiles.Atomic(CatalogPath, next, File.Exists(CatalogPath)); return profile;
    }
    public void Select(string profileId, string? expectedSelectionHash)
    {
        using var lease = Lock();
        _ = ReadSelection(); // Unknown/corrupt metadata is preserved, not overwritten as a side effect of starting.
        if (!ReadCatalog().Profiles.Any(p => p.ProfileId == profileId)) throw new ConfigurationException("Missing registered profile.");
        var actual = File.Exists(SelectionPath) ? SetupFiles.FileHash(SelectionPath) : null;
        if (actual != expectedSelectionHash) throw new ConfigurationException("Selection changed; reload before overwriting.");
        SetupFiles.Atomic(SelectionPath, new ProfileSelection(1, profileId), File.Exists(SelectionPath));
    }
    public static T Read<T>(string path, string schema)
    {
        SetupFiles.Safe(path); var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 4 * 1024 * 1024) throw new ConfigurationException("Launcher JSON exceeds 4 MiB.");
        using var doc = JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0));
        SchemaValidation.Validate(doc.RootElement, schema); return doc.RootElement.Deserialize<T>(JsonContract.Options)!;
    }
    private string ConfigPath(string path)
    {
        path = SetupFiles.Safe(path);
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.Equals(CatalogPath, PathSafety.Comparison) || path.Equals(SelectionPath, PathSafety.Comparison))
            throw new ConfigurationException("Select a configuration JSON, not launcher metadata.");
        foreach (var reserved in new[] { "versions", "operations", "install.json", ".archsift-setup.lock" })
            PathSafety.EnsureDisjoint(SetupFiles.Under(Root, reserved), path);
        PathSafety.EnsureDisjoint(Installation.TargetRoot, path); return path;
    }
    private FileStream Lock()
    {
        SetupFiles.Safe(ConfigRoot); Directory.CreateDirectory(ConfigRoot); SetupFiles.Safe(ConfigRoot);
        return new(SetupFiles.Under(Root, "config/.archsift-profiles.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private void CheckHash(string? expected) { if (CatalogHash != expected) throw new ConfigurationException("Catalog changed; reload before overwriting."); }
}
