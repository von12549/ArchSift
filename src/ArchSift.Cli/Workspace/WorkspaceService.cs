using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.TestGate;

namespace ArchSift.Workspaces;

internal sealed record WorkspaceMarker(int SchemaVersion, string Kind, string ProjectId, string TargetRoot,
    string StateRoot, string ConfigurationIdentity, string GeneratedConfigurationSha256);
internal sealed record WorkspaceResult(string Status, string ProductVersion, string TargetRoot, string StateRoot,
    string StartupConfig, string LauncherRoot, string? DailyTestConfig, WorkspaceHostSelection Host)
{
    public int SchemaVersion => 1;
    public string Kind => "archsift-workspace-initialization";
}

internal static class WorkspaceService
{
    internal static void WriteNew(string path, object value)
    {
        PathSafety.EnsureNoLinks(path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, value, JsonContract.Options); stream.Flush(true);
    }
    private static void Atomic(string path, object value)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        WriteNew(temporary, value); File.Move(temporary, path, true);
    }
    private static string? VerifySources(WorkspaceSettings settings)
    {
        if (settings.Configuration.Nuget.Mode != "offline") return null;
        if (settings.NugetConfig is null) throw new ConfigurationException("Offline startup requires an explicit standard NuGet.Config containing only approved local sources.");
        PathSafety.EnsureNoLinks(settings.NugetConfig);
        var xml = XDocument.Load(settings.NugetConfig);
        var sources = xml.Root?.Element("packageSources") ?? throw new ConfigurationException("Offline NuGet.Config requires packageSources.");
        if (sources.Element("clear") is null) throw new ConfigurationException("Offline NuGet.Config must clear inherited package sources.");
        var feeds = sources.Elements("add").Select(e => e.Attribute("value")?.Value ?? "").ToArray();
        if (feeds.Length == 0) throw new ConfigurationException("Offline NuGet.Config has no local source; choose the prepared approved offline file in local settings.");
        var resolved = new List<string>();
        foreach (var feed in feeds)
        {
            if (string.IsNullOrWhiteSpace(feed) || feed.Contains('%') || feed.Contains('$') ||
                Uri.TryCreate(feed, UriKind.Absolute, out var uri) && (!uri.IsFile || uri.IsUnc))
                throw new ConfigurationException("Offline mode rejects network/dynamic package source locations.");
            var directory = WorkspaceSettings.Canonical(feed, Path.GetDirectoryName(settings.NugetConfig));
            if (!Directory.Exists(directory)) throw new ConfigurationException("Offline package directory does not exist.");
            PathSafety.EnsureDisjoint(settings.ProjectRoot, directory); PathSafety.EnsureDisjoint(settings.StateRoot, directory);
            resolved.Add(directory);
        }
        if (xml.Root!.Element("fallbackPackageFolders")?.Elements("add").Any() == true ||
            xml.Root.Element("auditSources")?.Elements("add").Any() == true)
            throw new ConfigurationException("Offline configuration cannot add fallback folders or audit sources.");
        if (settings.SourceApproval is { } approvalFile)
        {
            if (resolved.Count != 1) throw new ConfigurationException("IFX requires exactly the approved complete offline feed.");
            using var approval = WorkspaceSettings.Read(approvalFile); var manifest = approval.RootElement;
            if (manifest.GetProperty("schemaVersion").GetInt32() != 1 || manifest.GetProperty("kind").GetString() != "archsift-approved-offline-source")
                throw new ConfigurationException("Unsupported source approval.");
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in manifest.GetProperty("packages").EnumerateArray())
            {
                var name = package.GetProperty("file").GetString()!;
                if (!expected.Add(name) || name != Path.GetFileName(name) || !name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) ||
                    AssemblyArtifacts.FileHash(PathSafety.Under(resolved[0], name)) != package.GetProperty("sha256").GetString())
                    throw new ConfigurationException("Offline feed package identity differs from IFX approval.");
            }
            if (Directory.GetFiles(resolved[0], "*.nupkg").Length != expected.Count)
                throw new ConfigurationException("Offline feed contains extra packages.");
        }
        return resolved.Count == 1 ? resolved[0] : null;
    }
    private static void VerifyIfx(WorkspaceSettings settings, string? feed)
    {
        if (settings.GateFile is null) return;
        if (feed is null) throw new ConfigurationException("IFX requires an approved local source.");
        using var manifest = WorkspaceSettings.Read(Path.Combine(settings.ProjectRoot, ".config", "dotnet-tools.json"));
        var tool = manifest.RootElement.GetProperty("tools").GetProperty("archsift.cli");
        if (!manifest.RootElement.GetProperty("isRoot").GetBoolean() || tool.GetProperty("version").GetString() != RuntimeInfo.ProductVersion ||
            tool.TryGetProperty("rollForward", out var rollForward) && rollForward.GetBoolean())
            throw new ConfigurationException("IFX requires the matching pinned project-root tool manifest.");
        using var gate = WorkspaceSettings.Read(settings.GateFile); var value = gate.RootElement;
        if (value.GetProperty("schemaVersion").GetInt32() != 1 || value.GetProperty("version").GetString() != RuntimeInfo.ProductVersion)
            throw new ConfigurationException("Workspace tool version differs from the approved IFX gate.");
        foreach (var pair in new[] { ("policyFile", "policySha256"), ("requiredCases", "requiredCasesSha256") })
            if (AssemblyArtifacts.FileHash(PathSafety.Under(settings.ProjectRoot, value.GetProperty(pair.Item1).GetString()!)) != value.GetProperty(pair.Item2).GetString())
                throw new ConfigurationException("IFX policy/case approval differs.");
        var archive = Path.Combine(feed, "ArchSift.Cli." + RuntimeInfo.ProductVersion + ".nupkg");
        if (AssemblyArtifacts.FileHash(archive) != value.GetProperty("cliPackageSha256").GetString())
            throw new ConfigurationException("CLI package archive differs from IFX approval.");
        var payload = WorkspaceSettings.Canonical(AppContext.BaseDirectory);
        using var zip = ZipFile.OpenRead(archive);
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("tools/net10.0/any/", StringComparison.Ordinal) && !e.FullName.EndsWith('/')))
        {
            var relative = entry.FullName["tools/net10.0/any/".Length..];
            var installed = PathSafety.Under(payload, relative);
            if (!expected.Add(relative)) throw new ConfigurationException("Duplicate package entry.");
            using var stream = entry.Open();
            if (AssemblyArtifacts.FileHash(installed) != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant())
                throw new ConfigurationException("Restored CLI/Web bytes differ from approved package.");
        }
        if (expected.Count == 0 || Directory.GetFiles(payload, "*", SearchOption.AllDirectories).Length != expected.Count)
            throw new ConfigurationException("Restored package has missing or unapproved extra payload files.");
        ArchSift.Hosting.WebPayload.Verify(Path.Combine(payload, "web"), RuntimeInfo.ProductVersion);
    }
    internal static async Task<int> RunAsync(string action, string file, string? localFile, string mode, bool noBrowser,
        string? migration, CancellationToken cancellation)
    {
        var guard = new WorkspaceHost();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cancellation = lifetime.Token;
        if (Environment.GetEnvironmentVariable("ARCHSIFT_WORKSPACE_PARENT_CONTROL") == "stdin-v1") _ = Task.Run(ObserveParent);
        async Task ObserveParent()
        {
            try { while (await Console.In.ReadLineAsync(lifetime.Token) is not null) { } await lifetime.CancelAsync(); }
            catch (OperationCanceledException) { }
            catch (IOException) { await lifetime.CancelAsync(); }
        }
        try
        {
            if (action is not ("init" or "doctor" or "start")) throw new ConfigurationException("Use workspace init, doctor or start.");
            if (action == "start" && mode is not ("launcher" or "workbench")) throw new ConfigurationException("Workspace mode must be launcher or workbench.");
            var settings = WorkspaceSettings.Load(file, localFile);
            var host = await guard.Resolve(settings, cancellation);
            var feed = VerifySources(settings); VerifyIfx(settings, feed); guard.Verify();
            var webDirectory = Path.Combine(AppContext.BaseDirectory, "web");
            var distributed = File.Exists(Path.Combine(AppContext.BaseDirectory, "DotnetToolSettings.xml"));
            for (var cursor = new DirectoryInfo(AppContext.BaseDirectory); cursor is not null && !distributed; cursor = cursor.Parent)
                distributed = cursor.Parent?.Name.Equals("archsift.cli", StringComparison.OrdinalIgnoreCase) == true && File.Exists(Path.Combine(cursor.FullName, ".nupkg.metadata"));
            if (distributed || Directory.Exists(webDirectory)) ArchSift.Hosting.WebPayload.Verify(webDirectory, RuntimeInfo.ProductVersion);
            var configPath = Path.Combine(settings.StateRoot, "config", "workbench.json");
            var markerPath = Path.Combine(settings.StateRoot, "workspace-state.json");
            if (action == "doctor")
            {
                if (migration is not null) throw new ConfigurationException("doctor never migrates state.");
                if (File.Exists(markerPath)) ValidateMarker(settings, markerPath);
                else if (Directory.Exists(settings.StateRoot) && !EmptyOrAbandonedLease(settings.StateRoot))
                    throw new ConfigurationException("Unrecognized nonempty workspace state; preserve it and review initialization recovery.");
                var busy = false; var lockPath = Path.Combine(settings.StateRoot, ".workspace.lock");
                if (File.Exists(lockPath))
                {
                    PathSafety.EnsureNoLinks(lockPath);
                    try { using var probe = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.None); }
                    catch (IOException) { busy = true; }
                }
                Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, kind = "archsift-workspace-diagnostic", status = busy ? "busy" : "ready", busy, productVersion = RuntimeInfo.ProductVersion,
                    settings.ProjectRoot, settings.StateRoot, startupConfig = configPath, initialized = File.Exists(markerPath), host,
                    nugetConfig = settings.NugetConfig, cache = settings.Cache ?? Environment.GetEnvironmentVariable("NUGET_PACKAGES") }, JsonContract.Options));
                return 0;
            }
            if (action is not ("init" or "start")) throw new ConfigurationException("Use workspace init, doctor or start.");
            using var workspaceLease = OpenLease(settings, markerPath);
            Initialize(settings, markerPath, configPath);
            if (migration is not null) Import(settings, migration);
            var launch = Path.Combine(settings.StateRoot, "launches", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(launch);
            var daily = Daily(settings, launch, feed);
            var result = new WorkspaceResult("initialized", RuntimeInfo.ProductVersion, settings.ProjectRoot, settings.StateRoot,
                configPath, Path.Combine(settings.StateRoot, "launcher"), daily, host);
            WriteNew(Path.Combine(launch, "initialization.json"), result);
            Console.WriteLine(JsonSerializer.Serialize(result, JsonContract.Options));
            if (daily is not null) Console.WriteLine("Daily-test configuration: " + daily);
            if (action == "init") return 0;
            var web = ArchSift.Hosting.WebPayload.Resolve(Path.Combine(AppContext.BaseDirectory, "web"), RuntimeInfo.ProductVersion);
            var arguments = mode == "launcher" ? new[] { "--launcher", result.LauncherRoot, "--config", result.StartupConfig }
                : new[] { "--config", result.StartupConfig };
            if (mode is not ("launcher" or "workbench")) throw new ConfigurationException("Workspace mode must be launcher or workbench.");
            var exit = -1;
            try
            {
                exit = await ArchSift.Hosting.UiProcess.RunArgumentsAsync(web.Executable, web.Assembly, arguments, Console.Out, Console.Error,
                    cancellation, onAddress: mode == "launcher" && !noBrowser ? address =>
                    {
                        try { using var browser = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true }); }
                        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
                        { throw new IOException("Default browser could not open the local Launcher."); }
                    } : null);
                return exit;
            }
            catch (OperationCanceledException) { exit = 130; throw; }
            finally { guard.Verify(); WriteNew(Path.Combine(launch, "closeout.json"), new { schemaVersion = 1, exitCode = exit,
                hostEqual = true, productVersion = RuntimeInfo.ProductVersion, targetRoot = settings.ProjectRoot, mode }); }
        }
        finally { await lifetime.CancelAsync(); guard.Verify(); }
    }
    private static FileStream OpenLease(WorkspaceSettings settings, string markerPath)
    {
        var root = settings.StateRoot;
        if (Directory.Exists(root) && !File.Exists(markerPath) && !File.Exists(Path.Combine(root, "initialization.json")) && !EmptyOrAbandonedLease(root))
            throw new ConfigurationException("Workspace initialization requires an empty/absent or verified owned root; unknown files are preserved.");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, ".workspace.lock"); PathSafety.EnsureNoLinks(path);
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new ConfigurationException("Workspace is busy; close the owned session before initialization or migration."); }
    }
    private static bool EmptyOrAbandonedLease(string root)
    {
        var entries = Directory.GetFileSystemEntries(root);
        if (entries.Length == 0) return true;
        if (entries.Length != 1 || entries[0] != Path.Combine(root, ".workspace.lock")) return false;
        PathSafety.EnsureNoLinks(entries[0]);
        return File.Exists(entries[0]) && new FileInfo(entries[0]).Length == 0;
    }
    private static WorkspaceMarker ValidateMarker(WorkspaceSettings settings, string path)
    {
        using var document = WorkspaceSettings.Read(path);
        var marker = document.RootElement.Deserialize<WorkspaceMarker>(JsonContract.Options);
        if (marker is null || marker.SchemaVersion != 1 || marker.Kind != "archsift-workspace-state" ||
            marker.ProjectId != settings.Configuration.Project.Id || marker.TargetRoot != settings.ProjectRoot || marker.StateRoot != settings.StateRoot)
            throw new ConfigurationException("Workspace state has another schema/project/root binding; preserve it and migrate explicitly.");
        return marker;
    }
    // Called only after the journal equals the expected transaction marker.
    // Reserved marker temporaries are opaque retained evidence: never load,
    // promote, rewrite or delete them. Their names do not prove ownership.
    private static bool IsPreservedInitializationTemporary(string root, string path)
    {
        const string prefix = "workspace-state.json.";
        var relative = Path.GetRelativePath(root, path);
        if (relative.Length != prefix.Length + 32 + 4 ||
            !relative.StartsWith(prefix, StringComparison.Ordinal) ||
            !relative.EndsWith(".tmp", StringComparison.Ordinal)) return false;
        var token = relative.Substring(prefix.Length, 32);
        if (!Guid.TryParseExact(token, "N", out var id) || token != id.ToString("N")) return false;
        PathSafety.EnsureNoLinks(path);
        return true;
    }
    private static void Initialize(WorkspaceSettings settings, string markerPath, string configPath)
    {
        var root = settings.StateRoot; var journalPath = Path.Combine(root, "initialization.json");
        var expected = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = settings.ProjectRoot, Entry = settings.Configuration.Project.Entry },
            Rulesets = settings.Configuration.Workbench.Rulesets.Select(p => WorkspaceSettings.Canonical(p, Path.GetDirectoryName(settings.File))).ToArray(),
            Build = settings.Configuration.Workbench.Build, RulesDirectory = Path.Combine(root, "library"),
            Output = new() { Directory = Path.Combine(root, "reports") } };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(expected, JsonContract.Options); var expectedHash = ContentHash.Bytes(bytes);
        WorkspaceMarker? marker = File.Exists(markerPath) ? ValidateMarker(settings, markerPath) : null;
        if (marker is null && File.Exists(journalPath))
        {
            using var journal = WorkspaceSettings.Read(journalPath);
            var partial = journal.RootElement.Deserialize<WorkspaceMarker>(JsonContract.Options);
            if (partial != new WorkspaceMarker(1, "archsift-workspace-state", settings.Configuration.Project.Id, settings.ProjectRoot, root, settings.ConfigIdentity, expectedHash))
                throw new ConfigurationException("Interrupted initialization inputs changed; preserve the transaction for review.");
            var allowed = new HashSet<string>([".workspace.lock", "initialization.json", "config/workbench.json"], StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (!allowed.Contains(Path.GetRelativePath(root, path).Replace('\\', '/')) && !IsPreservedInitializationTemporary(root, path))
                    throw new ConfigurationException("Unknown file in interrupted workspace transaction.");
            if (File.Exists(configPath) && AssemblyArtifacts.FileHash(configPath) != expectedHash) throw new ConfigurationException("Interrupted generated configuration changed.");
        }
        if (marker is null) WriteOrCheckJournal();
        foreach (var child in new[] { "config", "library", "reports", "runs", "launches", "backups" })
        { var path = Path.Combine(root, child); PathSafety.EnsureNoLinks(path); Directory.CreateDirectory(path); }
        if (File.Exists(configPath))
        {
            var current = ConfigLoader.Load(configPath);
            if (!current.Target.Root.Equals(settings.ProjectRoot, PathSafety.Comparison)) throw new ConfigurationException("Existing startup config targets another project.");
            if (marker is not null && marker.GeneratedConfigurationSha256 != expectedHash)
            {
                if (AssemblyArtifacts.FileHash(configPath) != marker.GeneratedConfigurationSha256)
                    throw new ConfigurationException("Workspace settings and externally edited startup config both changed; reconcile without overwriting edits.");
                Atomic(configPath, expected);
            }
        }
        else WriteNew(configPath, expected);
        Atomic(markerPath, new WorkspaceMarker(1, "archsift-workspace-state", settings.Configuration.Project.Id, settings.ProjectRoot, root,
            settings.ConfigIdentity, marker is not null && marker.ConfigurationIdentity == settings.ConfigIdentity ? marker.GeneratedConfigurationSha256 : expectedHash));
        if (File.Exists(journalPath)) File.Delete(journalPath);
        void WriteOrCheckJournal()
        {
            if (!File.Exists(journalPath)) WriteNew(journalPath, new WorkspaceMarker(1, "archsift-workspace-state", settings.Configuration.Project.Id,
                settings.ProjectRoot, root, settings.ConfigIdentity, expectedHash));
        }
    }
    private static string? Daily(WorkspaceSettings settings, string launch, string? feed)
    {
        if (settings.GateFile is null) return null;
        using var gate = WorkspaceSettings.Read(settings.GateFile); var value = gate.RootElement;
        var cache = settings.Cache ?? Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? throw new ConfigurationException("IFX needs the resolved external NuGet cache.");
        cache = WorkspaceSettings.Canonical(cache); PathSafety.EnsureDisjoint(settings.ProjectRoot, cache); PathSafety.EnsureDisjoint(settings.StateRoot, cache);
        var run = Path.Combine(settings.StateRoot, "runs", Path.GetFileName(launch));
        var config = new TestGateConfig { SchemaVersion = 2, TargetRoot = settings.ProjectRoot,
            TestProject = value.GetProperty("testProject").GetString()!, TestExecutableName = value.GetProperty("testExecutableName").GetString()!,
            TargetFramework = "net10.0", Configuration = "Release", NuGetConfig = "NuGet.Config", LocalFeed = feed!, PackageCache = cache,
            Rulesets = value.GetProperty("rulesets").Deserialize<string[]>()!, RequiredCases = value.GetProperty("requiredCases").GetString(),
            RequiredCasesSha256 = value.GetProperty("requiredCasesSha256").GetString(), PolicyFile = value.GetProperty("policyFile").GetString(),
            PolicySha256 = value.GetProperty("policySha256").GetString(), ToolIdentity = RuntimeInfo.ProductVersion,
            RunDirectory = run, DotnetCliHome = Path.Combine(run, "cli-home"), RunnerFormat = "mtp-ctrf", AuditMode = "offline-architecture" };
        var path = Path.Combine(launch, "test-gate.json"); WriteNew(path, config); return path;
    }
    private static void Import(WorkspaceSettings settings, string migrationFile)
    {
        using var document = WorkspaceSettings.Read(WorkspaceSettings.Canonical(migrationFile)); var migration = document.RootElement;
        if (migration.GetProperty("kind").GetString() != "archsift-workspace-migration" || migration.GetProperty("schemaVersion").GetInt32() != 1 ||
            WorkspaceSettings.Canonical(migration.GetProperty("targetRoot").GetString()!) != settings.ProjectRoot)
            throw new ConfigurationException("Migration target/schema must bind this workspace.");
        var entries = migration.GetProperty("files").EnumerateArray().ToArray();
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var relative = entry.GetProperty("destination").GetString()!;
            if (!(relative.StartsWith("library/", StringComparison.Ordinal) || relative.StartsWith("backups/imports/", StringComparison.Ordinal)) || !destinations.Add(relative))
                throw new ConfigurationException("Migration writes only unique library or inactive historical-backup files.");
            var sourceText = entry.GetProperty("source").GetString()!;
            if (!Path.IsPathFullyQualified(sourceText)) throw new ConfigurationException("Migration sources must be absolute.");
            var source = WorkspaceSettings.Canonical(sourceText); var target = PathSafety.Under(settings.StateRoot, relative);
            if (Path.GetRelativePath(settings.StateRoot, target).Replace('\\', '/') != relative)
                throw new ConfigurationException("Migration destinations must be canonical relative paths without aliases.");
            PathSafety.EnsureDisjoint(settings.StateRoot, source); PathSafety.EnsureDisjoint(settings.ProjectRoot, source);
            var hash = entry.GetProperty("sha256").GetString();
            if (AssemblyArtifacts.FileHash(source) != hash || File.Exists(target) && AssemblyArtifacts.FileHash(target) != hash)
                throw new ConfigurationException("Migration input/destination differs; existing bytes are preserved.");
        }
        foreach (var entry in entries)
        {
            var target = PathSafety.Under(settings.StateRoot, entry.GetProperty("destination").GetString()!);
            if (!File.Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var temporary = Path.Combine(settings.StateRoot, "backups", "import-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var input = new FileStream(entry.GetProperty("source").GetString()!, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { input.CopyTo(output); output.Flush(true); }
                if (AssemblyArtifacts.FileHash(temporary) != entry.GetProperty("sha256").GetString())
                    throw new ConfigurationException("Migration source changed during transfer; preserve the temporary evidence.");
                File.Move(temporary, target, false);
            }
        }
        var record = Path.Combine(settings.StateRoot, "backups", "migration-" + AssemblyArtifacts.FileHash(migrationFile) + ".json");
        if (!File.Exists(record)) WriteNew(record, migration);
    }
}
