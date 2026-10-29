using System.Text.Json;
using System.Text.RegularExpressions;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Workspaces;

internal sealed record WorkspaceSettings(string File, string ProjectRoot, string StateRoot, string ConfigIdentity,
    WorkspaceConfiguration Configuration, string? NugetConfig, string? Cache, string? RuntimeApproval, string? SourceApproval, string? GateFile)
{
    internal static JsonDocument Read(string path)
    {
        PathSafety.EnsureNoLinks(path);
        if (!System.IO.File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024)
            throw new ConfigurationException("Workspace input is missing or exceeds 4 MiB: " + path);
        var bytes = System.IO.File.ReadAllBytes(path);
        var offset = bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? 3 : 0;
        var document = JsonDocument.Parse(bytes.AsMemory(offset));
        try { Unique(document.RootElement); return document; } catch { document.Dispose(); throw; }
    }
    private static void Unique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new ConfigurationException("Duplicate workspace JSON property."); Unique(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Unique(item);
    }
    private static void Schema(JsonElement value, string name)
    {
        var assembly = typeof(WorkspaceSettings).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + name + ".schema.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var schema = JsonDocument.Parse(stream);
        SchemaValidation.ValidateDocument(value, schema.RootElement);
    }
    internal static string Canonical(string path, string? relativeTo = null)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ConfigurationException("An explicit nonempty path is required.");
        var full = Path.TrimEndingDirectorySeparator(relativeTo is null ? Path.GetFullPath(path) : Path.GetFullPath(path, relativeTo));
        PathSafety.EnsureNoLinks(full); return full;
    }
    internal static WorkspaceSettings Load(string file, string? localFile = null)
    {
        file = Canonical(file);
        using var document = Read(file);
        Schema(document.RootElement, "workspace");
        var config = document.RootElement.Deserialize<WorkspaceConfiguration>(JsonContract.Options)
            ?? throw new ConfigurationException("Workspace configuration is required.");
        if (config.Kind != "archsift-workspace" || config.SchemaVersion != 1 || config.Project is null || config.State is null ||
            config.Nuget is null || config.Runtime is null || config.Workbench is null || config.Workbench.Build is null)
            throw new ConfigurationException("Expected archsift-workspace schema 1, not a startup/daily-test configuration.");
        if (!Regex.IsMatch(config.Project.Id ?? "", "^[a-zA-Z0-9][a-zA-Z0-9_-]{0,79}$"))
            throw new ConfigurationException("Project ID must be a stable 1-80 character identifier without traversal.");
        var directory = Path.GetDirectoryName(file)!;
        var root = Canonical(config.Project.Root, directory);
        if (!Directory.Exists(root)) throw new ConfigurationException("Workspace project root does not exist.");
        if (config.Project.Entry is { } entry) PathSafety.Under(root, entry);
        var localIdentity = "absent";
        var explicitLocal = localFile is not null;
        localFile ??= Path.GetFileName(file).Equals("workspace.json", StringComparison.Ordinal)
            ? Path.Combine(directory, "workspace.local.json") : null;
        if (localFile is not null)
        {
            localFile = Canonical(localFile, directory);
            if (System.IO.File.Exists(localFile))
            {
                using var local = Read(localFile);
                var value = local.RootElement;
                try { Schema(value, "workspace-local"); }
                catch (ConfigurationException error) { throw new ConfigurationException("Local settings cannot override workspace policy: " + error.Message); }
                if (value.GetProperty("kind").GetString() != "archsift-workspace-local" || value.GetProperty("schemaVersion").GetInt32() != 1)
                    throw new ConfigurationException("Expected archsift-workspace-local schema 1.");
                foreach (var property in value.EnumerateObject())
                {
                    if (property.Name is "kind" or "schemaVersion") continue;
                    var allowed = property.Name switch { "state" => new[] { "root" }, "runtime" => ["dotnetPath"],
                        "nuget" => ["cacheDirectory", "configFile"], _ => [] };
                    if (property.Value.ValueKind != JsonValueKind.Object || allowed.Length == 0 ||
                        property.Value.EnumerateObject().Any(p => !allowed.Contains(p.Name, StringComparer.Ordinal)))
                        throw new ConfigurationException("Local settings cannot override workspace policy: " + property.Name);
                    foreach (var setting in property.Value.EnumerateObject())
                    {
                        var text = setting.Value.GetString();
                        config = (property.Name, setting.Name) switch
                        {
                            ("state", "root") => config with { State = config.State with { Root = text ?? throw new ConfigurationException("State root cannot be null.") } },
                            ("runtime", "dotnetPath") => config with { Runtime = config.Runtime with { DotnetPath = text } },
                            ("nuget", "cacheDirectory") => config with { Nuget = config.Nuget with { CacheDirectory = text } },
                            ("nuget", "configFile") => config with { Nuget = config.Nuget with { ConfigFile = text } },
                            _ => throw new ConfigurationException("Unsupported local override.")
                        };
                    }
                }
                localIdentity = AssemblyArtifacts.FileHash(localFile);
            }
            else if (explicitLocal)
                throw new ConfigurationException("Explicit local configuration is missing.");
        }
        if (config.Nuget.Mode is not ("configured" or "offline") || config.Nuget.Mode == "offline" && config.Nuget.AllowNetwork ||
            config.Nuget.Mode == "configured" && !config.Nuget.AllowNetwork)
            throw new ConfigurationException("Use offline/allowNetwork=false or explicitly configured/allowNetwork=true.");
        if (config.Runtime.Mode is not ("host" or "approvedExact") || !Version.TryParse(config.Runtime.MinimumSdkVersion, out var minimum) || minimum < new Version(10, 0, 0))
            throw new ConfigurationException("Workspace requires a stable SDK >= 10.0.0; the minimum cannot weaken that floor.");
        if (config.Workbench.Build.Mode != "existing" || config.Workbench.Build.AllowNetwork || config.Workbench.Build.Sources.Length != 0)
            throw new ConfigurationException("Workspace initialization uses existing-build mode; target build/restore is a separate explicit operation.");
        string? Resolve(string? path) => path is null ? null : Canonical(path, directory);
        var user = ContentHash.Text(Environment.UserDomainName + "\\" + Environment.UserName)[..16];
        var stateBase = Canonical(config.State.Root, directory);
        PathSafety.EnsureDisjoint(root, stateBase);
        var state = Canonical(Path.Combine(stateBase, "users", user, "projects", config.Project.Id!));
        foreach (var protectedRoot in ArchSift.Hosting.WebPayload.ProtectedHostRoots()) PathSafety.EnsureDisjoint(state, protectedRoot);
        var settings = new WorkspaceSettings(file, root, state, ContentHash.Text(AssemblyArtifacts.FileHash(file) + "/" + localIdentity),
            config, Resolve(config.Nuget.ConfigFile), Resolve(config.Nuget.CacheDirectory), Resolve(config.Runtime.ApprovalFile),
            Resolve(config.Nuget.SourceApprovalFile), config.Gate is null ? null : Resolve(config.Gate.MetadataFile));
        foreach (var path in new[] { settings.Cache, settings.NugetConfig, settings.RuntimeApproval, settings.SourceApproval, settings.GateFile }.OfType<string>()) PathSafety.EnsureNoLinks(path);
        if (settings.Cache is { } cache) { PathSafety.EnsureDisjoint(root, cache); PathSafety.EnsureDisjoint(state, cache); }
        if (System.IO.File.Exists(Path.Combine(root, "architecture", "portable-gate.json")) || config.Gate is not null)
        {
            if (config.Gate?.Kind != "ifx" ||
                settings.GateFile != Canonical(Path.Combine(root, "architecture", "portable-gate.json")))
                throw new ConfigurationException("IFX workspace must retain its original gate identity and metadata path.");
            var exactOffline = config.Runtime.Mode == "approvedExact" && config.Nuget.Mode == "offline";
            var hostConfigured = config.Runtime.Mode == "host" && config.Nuget.Mode == "configured";
            if (!exactOffline && !hostConfigured)
                throw new ConfigurationException("IFX supports host/configured or approvedExact/offline; no automatic mode fallback.");
            if (exactOffline &&
                (settings.RuntimeApproval != Canonical(Path.Combine(root, "architecture", "runtime-approval.json")) ||
                 settings.SourceApproval != Canonical(Path.Combine(root, "architecture", "package-source.json"))))
                throw new ConfigurationException("Exact offline IFX workspace must retain its original runtime/source approval paths.");
            if (hostConfigured && (settings.RuntimeApproval is not null || settings.SourceApproval is not null))
                throw new ConfigurationException("Host/configured IFX workspace requires null runtime/source approvals; exact approvals cannot be ignored.");
            if (hostConfigured && (settings.NugetConfig is null || !System.IO.File.Exists(settings.NugetConfig)))
                throw new ConfigurationException("Host/configured IFX workspace requires an explicit existing NuGet.Config; restore pinned dependencies before startup.");
        }
        return settings;
    }
}
