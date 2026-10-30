using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Testing;

namespace ArchSift.TestGate;

public sealed record TestGateConfig
{
    public int SchemaVersion { get; init; } = 1;
    public required string TargetRoot { get; init; }
    public required string TestProject { get; init; }
    public required string TestExecutableName { get; init; }
    public required string TargetFramework { get; init; }
    public required string Configuration { get; init; }
    public required string NuGetConfig { get; init; }
    public string LocalFeed { get; init; } = "";
    public string RestoreMode { get; init; } = "offline";
    public string? DotnetPath { get; init; }
    public required string PackageCache { get; init; }
    public required string[] Rulesets { get; init; }
    public string OriginalBaseline { get; init; } = "";
    public string OriginalBaselineSha256 { get; init; } = "";
    public required string RunDirectory { get; init; }
    public string? DotnetCliHome { get; init; }
    public string? FilterCaseId { get; init; }
    public string RunnerFormat { get; init; } = "xunit-json";
    public string AuditMode { get; init; } = "inherit";
    public string? ToolRoot { get; init; }
    public string? ToolIdentity { get; init; }
    public string? ToolManifest { get; init; }
    public string? ToolManifestSha256 { get; init; }
    public string? PolicyFile { get; init; }
    public string? PolicySha256 { get; init; }
    public string? CaseMapping { get; init; }
    public string? CaseMappingSha256 { get; init; }
    public string? RequiredCases { get; init; }
    public string? RequiredCasesSha256 { get; init; }
    public string? MinimumSdkVersion { get; init; }
    public string? MaximumSdkVersionExclusive { get; init; }
}

public sealed record GateStage(string Status, int? ExitCode, string? LogPath);

public sealed record TestGateResult(int ExitCode, string Gate, string[] Reasons,
    GateStage Restore, GateStage Build, GateStage Test, MtpRun? NativeRun,
    GateCoverage? Coverage, string RunDirectory, bool SnapshotUpdatesDisabled,
    bool RelevantInputChanged)
{
    public string? ReportRunId { get; init; }
    public string? ReportPath { get; init; }
    public string? HtmlPath { get; init; }
    public GateAudit? Audit { get; init; }
}

public static class TestGateService
{
    public static TestGateConfig Load(string path)
    {
        var full = Path.GetFullPath(path);
        PathSafety.EnsureNoLinks(full);
        if (!File.Exists(full)) throw new ConfigurationException("Test gate config is missing.");
        if (new FileInfo(full).Length > RuleLoader.MaximumJsonBytes)
            throw new ConfigurationException("Test gate config exceeds 4 MiB.");
        var config = JsonSerializer.Deserialize<TestGateConfig>(File.ReadAllBytes(full), JsonContract.Options)
            ?? throw new ConfigurationException("Test gate config is empty.");
        return config;
    }

    public static async Task<TestGateResult> RunAsync(TestGateConfig config, string configPath,
        CancellationToken token = default)
    {
        var startedUtc = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid().ToString("N");
        Validate(config);
        var root = Path.GetFullPath(config.TargetRoot);
        if (config.DotnetPath is { } dotnetPath)
        {
            PathSafety.EnsureNoLinks(dotnetPath);
            if (!File.Exists(dotnetPath)) throw new ConfigurationException("Explicit gate dotnet host is missing; no fallback.");
            PathSafety.EnsureDisjoint(root, Path.GetDirectoryName(dotnetPath)!);
        }
        if (config.ToolRoot is not null) PathSafety.EnsureDisjoint(root, config.ToolRoot);
        var project = Under(root, config.TestProject);
        var nugetConfig = config.RestoreMode == "configured" ? Path.GetFullPath(config.NuGetConfig) : Under(root, config.NuGetConfig);
        if (config.RestoreMode == "configured")
        {
            PathSafety.EnsureNoLinks(nugetConfig);
            if (!File.Exists(nugetConfig) || new FileInfo(nugetConfig).Length > RuleLoader.MaximumJsonBytes)
                throw new ConfigurationException("Configured NuGet.Config is missing or too large.");
            var sources = System.Xml.Linq.XDocument.Load(nugetConfig).Root?.Element("packageSources");
            if (sources?.Element("clear") is null || !sources.Elements("add").Any())
                throw new ConfigurationException("Configured gate restore requires an explicit NuGet.Config with cleared, declared sources.");
        }
        var portable = config.SchemaVersion == 2 ? RequiredCases.Load(config) : null;
        var baseline = portable is null ? Path.GetFullPath(config.OriginalBaseline) : Under(root, config.RequiredCases!);
        var runDirectory = Path.GetFullPath(config.RunDirectory);
        var fixedInputs = config.ToolRoot is null ? [] : TestLaunch.VerifyTool(config.ToolRoot,
            config.ToolManifest ?? throw new ConfigurationException("Fixed tool manifest is required."),
            config.ToolManifestSha256 ?? "", config.ToolIdentity ?? "");
        var rulesetPaths = config.Rulesets.Select(path => Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path) : Under(root, path)).ToArray();
        foreach (var file in new[] { project, nugetConfig, baseline, Path.GetFullPath(configPath) }.Concat(rulesetPaths))
        {
            PathSafety.EnsureNoLinks(file);
            if (!File.Exists(file)) throw new ConfigurationException("Required test gate input is missing: " + file);
        }
        PathSafety.EnsureNoLinks(runDirectory);
        if (PathSafety.IsUnder(runDirectory, root) || PathSafety.IsUnder(root, runDirectory))
            throw new ConfigurationException("Run directory must be outside the target source root.");
        if (config.DotnetCliHome is not null)
        {
            if (!Path.IsPathFullyQualified(config.DotnetCliHome) ||
                !PathSafety.IsUnder(config.DotnetCliHome, runDirectory) ||
                Path.GetFullPath(config.DotnetCliHome).Equals(runDirectory, PathSafety.Comparison))
                throw new ConfigurationException("DOTNET_CLI_HOME must be an owned child of the run directory.");
            PathSafety.EnsureNoLinks(config.DotnetCliHome);
        }
        if (Directory.Exists(runDirectory) || File.Exists(runDirectory))
            throw new ConfigurationException("Run directory already exists.");
        if (config.RestoreMode == "offline" && !Directory.Exists(config.LocalFeed)) throw new ConfigurationException("Offline package source is missing.");
        if (!Directory.Exists(config.PackageCache)) throw new ConfigurationException("Package cache is missing.");
        if (config.RestoreMode == "offline") PathSafety.EnsureNoLinks(config.LocalFeed);
        PathSafety.EnsureNoLinks(config.PackageCache);
        var baselineBytes = File.ReadAllBytes(baseline);
        if (ContentHash.Bytes(baselineBytes) != (portable is null ? config.OriginalBaselineSha256 : config.RequiredCasesSha256))
            throw new ConfigurationException("Original-case baseline hash changed.");
        var originalIds = portable?.Cases.Select(item => CaseIdentities.Key(item.Identity)).ToArray() ?? JsonSerializer.Deserialize<string[]>(baselineBytes)
            ?? throw new ConfigurationException("Original-case baseline is empty.");
        if (originalIds.Length == 0 || originalIds.Distinct(StringComparer.Ordinal).Count() != originalIds.Length)
            throw new ConfigurationException("Original-case baseline is empty or has duplicates.");
        var mapping = config.CaseMapping is null ? null : CaseMapping.Load(config);
        if (mapping is not null && !originalIds.ToHashSet(StringComparer.Ordinal).SetEquals(mapping.Cases.Select(item => item.ExecutionId)))
            throw new ConfigurationException("Required C# baseline and approved case mapping disagree.");
        var (bundle, cases) = ArchitectureTestAdapter.LoadCases(rulesetPaths);
        if (config.PolicyFile is not null)
            _ = NativePolicy.Load(root, config.PolicyFile, configPath);
        var expectedJson = cases.ToDictionary(item => item.CaseId, item => item.Ruleset.Identity.Sha256,
            StringComparer.Ordinal);
        if (bundle.Rules.Length == 0) throw new ConfigurationException("No JSON rules are enabled.");

        var watched = new[] { project, nugetConfig, baseline, Path.GetFullPath(configPath) }.Concat(rulesetPaths)
            .Concat(fixedInputs)
            .Concat(config.PolicyFile is null ? [] : new[] { PathSafety.Under(root, config.PolicyFile) })
            .Concat(config.CaseMapping is null ? [] : new[] { config.CaseMapping })
            .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(path => path, FileHash, StringComparer.OrdinalIgnoreCase);
        using var sourceObserver = new RelevantInputObserver(root);
        Directory.CreateDirectory(runDirectory);
        TestRunHistory.Begin(runDirectory, runId, root, startedUtc);
        var notStarted = new GateStage("not-started", null, null);
        var restore = notStarted;
        var build = notStarted;
        var test = notStarted;
        var restoreArguments = NuGetAudit.RestoreArguments(config, project, nugetConfig);
        var changed = false;
        try
        {
            if (mapping is not null || portable is not null)
            {
                var sdk = await Execute("sdk", "dotnet", ["--version"], root, runDirectory, config, configPath, token);
                if (sdk.ExitCode != 0 || !AcceptsSelectedSdk(config, portable?.Sdk ?? mapping!.Sdk,
                    File.ReadAllText(sdk.LogPath!).Trim()))
                    return Complete(Finish(4, "Selected SDK does not satisfy the test gate SDK policy."));
            }
            restore = await Execute("restore", "dotnet",
                restoreArguments, root, runDirectory, config, configPath, token);
            changed = sourceObserver.Changed || InputsChanged(watched);
            if (restore.ExitCode != 0 || changed) return Complete(Finish(restore.ExitCode != 0 ? 3 : 4,
                restore.ExitCode != 0 ? "Offline restore failed." : "Relevant input changed during restore."));
            build = await Execute("build", "dotnet",
                ["build", project, "--no-restore", "-c", config.Configuration, "-f", config.TargetFramework,
                    "--disable-build-servers", "-p:UseSharedCompilation=false", "-m:1", "-v:minimal"],
                root, runDirectory, config, configPath, token);
            changed = sourceObserver.Changed || InputsChanged(watched);
            if (build.ExitCode != 0 || changed) return Complete(Finish(build.ExitCode != 0 ? 3 : 4,
                build.ExitCode != 0 ? "Incremental build failed." : "Relevant input changed during build."));
            var executable = Path.Combine(Path.GetDirectoryName(project)!, "bin", config.Configuration,
                config.TargetFramework, config.TestExecutableName + (OperatingSystem.IsWindows() ? ".exe" : ""));
            PathSafety.EnsureNoLinks(executable);
            if (!File.Exists(executable)) return Complete(Finish(3, "Built MTP test executable is missing."));
            GateStage? discovery = null;
            var ctrfPath = Path.Combine(runDirectory, "test-results.ctrf.json");
            List<string> arguments;
            if (config.RunnerFormat == "mtp-ctrf")
            {
                try
                {
                    discovery = await Execute("discovery", executable,
                        ["--list-tests", "json", "--no-ansi", "--progress", "off"],
                        root, runDirectory, config, configPath, token);
                }
                catch (Exception error) when (error is JsonException or DecoderFallbackException)
                { return Complete(Finish(3, "MTP discovery output could not be decoded: " + error.Message)); }
                changed = sourceObserver.Changed || InputsChanged(watched);
                if (discovery.ExitCode != 0 || changed) return Complete(Finish(discovery.ExitCode != 0 ? 3 : 4,
                    discovery.ExitCode != 0 ? "MTP discovery failed." : "Relevant input changed during discovery."));
                arguments = ["--no-ansi", "--progress", "off", "--report-xunit-ctrf",
                    "--report-xunit-ctrf-filename", Path.GetFileName(ctrfPath),
                    "--results-directory", runDirectory];
                if (config.FilterCaseId is not null) arguments.AddRange(["--filter-uid", config.FilterCaseId]);
            }
            else
            {
                arguments = ["-reporter", "json", "-noLogo", "-noAutoReporters"];
                if (config.FilterCaseId is not null) arguments.AddRange(["-id", config.FilterCaseId]);
            }
            test = await Execute("test", executable, arguments, root, runDirectory, config, configPath, token);
            changed = sourceObserver.Changed || InputsChanged(watched);
            MtpRun native;
            try
            {
                native = config.RunnerFormat == "mtp-ctrf"
                    ? MtpCtrf.Parse(File.ReadAllBytes(discovery!.LogPath!), File.ReadAllBytes(ctrfPath), portable is not null)
                    : MtpEvents.Parse(File.ReadAllLines(test.LogPath!, Encoding.UTF8));
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or KeyNotFoundException
                or InvalidOperationException or FileNotFoundException)
            { return Complete(Finish(3, "MTP structured output is invalid: " + error.Message)); }
            var coverage = portable is null
                ? MtpEvents.Assess(native, originalIds, expectedJson, test.ExitCode ?? 3, subset: config.FilterCaseId is not null)
                : RequiredCases.Assess(portable, native, expectedJson, test.ExitCode ?? 3, subset: config.FilterCaseId is not null);
            if (mapping is not null)
            {
                try { CaseMapping.Assess(mapping, native); }
                catch (InvalidDataException error)
                { coverage = coverage with { ExitCode = 4, Complete = false, Reasons = coverage.Reasons.Append(error.Message).ToArray() }; }
            }
            if (portable is not null)
            {
                var policy = NativePolicy.Load(root, config.PolicyFile!);
                var associations = native.Tests.Where(item => item.Identity is not null)
                    .GroupBy(item => CaseIdentities.Key(item.Identity!.Identity), StringComparer.Ordinal)
                    .ToDictionary(item => item.Key, item => item.ToArray(), StringComparer.Ordinal);
                var mismatch = policy.Document.Sources.Where(item => item.Responsibility != "deferred").Any(source =>
                    source.CaseKeys.Length == 0 || source.CaseKeys.Any(key => !associations.TryGetValue(key, out var rows) ||
                        rows.Length != 1 || (source.Responsibility == "json"
                            ? rows[0].JsonCaseId != "archsift-json:" + source.SourceId : rows[0].JsonCaseId is not null)));
                if (mismatch) coverage = coverage with { ExitCode = 4, Complete = false,
                    Reasons = coverage.Reasons.Append("Approved policy responsibility does not match current-run cases.").ToArray() };
            }
            if (changed) return Complete(new(4, "block", ["Relevant input changed during test execution."],
                restore, build, test, native, coverage, runDirectory, true, true));
            return Complete(new(coverage.ExitCode, coverage.ExitCode == 0 ? "allow" : "block", coverage.Reasons,
                restore, build, test, native, coverage, runDirectory, true, false));
        }
        catch (OperationCanceledException)
        {
            return Complete(Finish(130, "Run was cancelled."));
        }

        TestGateResult Finish(int code, string reason) => new(code, "block", [reason], restore, build, test,
            null, null, runDirectory, true, changed);

        TestGateResult Complete(TestGateResult result)
        {
            result = result with { Audit = new(config.AuditMode,
                config.AuditMode == "offline-architecture" ? "not-run" : "not-verified",
                restore.LogPath is not null && File.Exists(restore.LogPath)
                    ? NuGetAudit.ReadCompletion(File.ReadAllText(restore.LogPath)) : new(null, null, null), restoreArguments) };
            var published = false;
            try
            {
                var (json, html) = TestRunReportWriter.Save(config, result, bundle, originalIds,
                    runId, startedUtc, DateTimeOffset.UtcNow);
                published = true;
                var saved = result with { ReportRunId = runId, ReportPath = json, HtmlPath = html };
                TestRunHistory.Finish(runDirectory, runId, saved);
                return saved;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException
                or InvalidDataException or System.ComponentModel.Win32Exception)
            {
                var failed = result with
                {
                    ExitCode = 3, Gate = "block",
                    Reasons = result.Reasons.Append("Authoritative test report could not be saved: " + error.Message).ToArray()
                };
                if (!published)
                {
                    try { TestRunHistory.Finish(runDirectory, runId, failed); }
                    catch (Exception updateError) when (updateError is IOException or UnauthorizedAccessException
                        or InvalidDataException) { /* The running marker is interpreted as interrupted. */ }
                }
                return failed;
            }
        }
    }

    internal static async Task<GateStage> Execute(string stage, string executable, IEnumerable<string> args,
        string workdir, string runDirectory, TestGateConfig config, string configPath, CancellationToken token)
    {
        var hostBefore = config.SchemaVersion == 2 ? GateHostSafety.Capture() : null;
        var start = new ProcessStartInfo(executable == "dotnet" && config.DotnetPath is not null ? config.DotnetPath : executable)
        {
            WorkingDirectory = workdir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        if (config.DotnetPath is { } selectedHost)
        {
            var runtimeRoot = Path.GetDirectoryName(selectedHost)!;
            start.Environment["DOTNET_HOST_PATH"] = selectedHost;
            start.Environment["DOTNET_ROOT"] = runtimeRoot;
            start.Environment["DOTNET_ROOT_X64"] = runtimeRoot;
            start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
            start.Environment["PATH"] = runtimeRoot + Path.PathSeparator + start.Environment["PATH"];
        }
        start.Environment["NUGET_PACKAGES"] = config.PackageCache;
        if (stage == "build" && config.ToolRoot is not null)
            start.ArgumentList.Add("-p:ArchSiftToolRoot=" + Path.GetFullPath(config.ToolRoot));
        start.Environment["IFX_UPDATE_SNAPSHOTS"] = "0";
        start.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true";
        if (config.SchemaVersion == 2) start.Environment["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false";
        start.Environment["ARCHSIFT_TEST_GATE_CONFIG"] = Path.GetFullPath(configPath);
        if (config.DotnetCliHome is not null)
        {
            start.Environment["DOTNET_CLI_HOME"] = Path.GetFullPath(config.DotnetCliHome);
            start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        }
        using var process = Process.Start(start) ?? throw new IOException("Could not start " + stage + " process.");
        try
        {
            using var discoveryBytes = stage == "discovery" && config.RunnerFormat == "mtp-ctrf" ? new MemoryStream() : null;
            var stdout = discoveryBytes is null ? process.StandardOutput.ReadToEndAsync(token) : null;
            var discoveryCopy = discoveryBytes is null ? null : process.StandardOutput.BaseStream.CopyToAsync(discoveryBytes, token);
            var stderr = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            var log = Path.Combine(runDirectory, stage + ".log");
            if (discoveryBytes is not null)
            {
                await discoveryCopy!;
                var bytes = discoveryBytes.ToArray();
                await File.WriteAllBytesAsync(Path.Combine(runDirectory, "discovery.stdout.bin"), bytes, token);
                // Decode only successful JSON discovery. Failed process diagnostics remain raw evidence.
                if (process.ExitCode == 0)
                {
                    var decoded = DiscoveryOutput.Decode(bytes, DiscoveryOutput.WindowsOemCodePage);
                    await File.WriteAllTextAsync(log, decoded.Text, new UTF8Encoding(false), token);
                    await File.WriteAllTextAsync(Path.Combine(runDirectory, "discovery.encoding.json"),
                        JsonSerializer.Serialize(new { sourceEncoding = decoded.Encoding, storedEncoding = "utf-8" }), token);
                }
                else await File.WriteAllTextAsync(log, Encoding.UTF8.GetString(bytes), new UTF8Encoding(false), token);
            }
            else await File.WriteAllTextAsync(log, await stdout!, new UTF8Encoding(false), token);
            var err = await stderr;
            if (err.Length > 0) await File.WriteAllTextAsync(Path.Combine(runDirectory, stage + ".stderr.log"), err, new UTF8Encoding(false), token);
            return new(process.ExitCode == 0 ? "passed" : "failed", process.ExitCode, log);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (hostBefore is not null) GateHostSafety.Verify(hostBefore);
        }
    }

    internal static void Validate(TestGateConfig config)
    {
        var paths = new[] { config.TargetRoot, config.TestProject, config.TestExecutableName,
            config.TargetFramework, config.Configuration, config.NuGetConfig,
            config.PackageCache, config.RunDirectory };
        if (paths.Any(string.IsNullOrWhiteSpace) || config.Rulesets is null ||
            config.Rulesets.Any(string.IsNullOrWhiteSpace))
            throw new ConfigurationException("Test gate config has missing values.");
        if (config.ToolRoot is not null && (!Path.IsPathFullyQualified(config.ToolRoot) ||
            config.ToolRoot.IndexOfAny([';', ',', '%', '"']) >= 0))
            throw new ConfigurationException("Tool root must be absolute and contain no MSBuild property syntax.");
        if (config.DotnetPath is not null && !Path.IsPathFullyQualified(config.DotnetPath))
            throw new ConfigurationException("Gate dotnetPath must be absolute.");
        if (config.SchemaVersion is not (1 or 2) || config.TargetFramework != "net10.0" ||
            config.AuditMode is not ("inherit" or "offline-architecture") ||
            config.RunnerFormat is not ("xunit-json" or "mtp-ctrf") ||
            config.Configuration is not ("Debug" or "Release") ||
            config.Rulesets is not { Length: > 0 } ||
            Path.GetFileName(config.TestExecutableName) != config.TestExecutableName ||
            config.TestExecutableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathFullyQualified(config.TargetRoot) || config.RestoreMode is not ("offline" or "configured") ||
            (config.RestoreMode == "offline" && !Path.IsPathFullyQualified(config.LocalFeed)) ||
            (config.RestoreMode == "configured" && (config.SchemaVersion != 2 || config.LocalFeed.Length != 0 ||
                !Path.IsPathFullyQualified(config.NuGetConfig) || config.AuditMode != "offline-architecture")) ||
            !Path.IsPathFullyQualified(config.PackageCache) ||
            !Path.IsPathFullyQualified(config.RunDirectory) ||
            (config.SchemaVersion == 1 && (!Path.IsPathFullyQualified(config.OriginalBaseline) ||
                config.OriginalBaselineSha256.Length != 64 || config.OriginalBaselineSha256.Any(character => !Uri.IsHexDigit(character)) ||
                config.RequiredCases is not null || config.RequiredCasesSha256 is not null)) ||
            (config.SchemaVersion == 2 && (string.IsNullOrWhiteSpace(config.RequiredCases) ||
                config.RequiredCasesSha256 is not { Length: 64 } || config.RequiredCasesSha256.Any(character => !Uri.IsHexDigit(character)) ||
                config.CaseMapping is not null || config.CaseMappingSha256 is not null || config.ToolRoot is not null ||
                config.OriginalBaseline.Length != 0 || config.OriginalBaselineSha256.Length != 0 ||
                config.RunnerFormat != "mtp-ctrf" || config.PolicyFile is null || config.PolicySha256 is null ||
                config.ToolIdentity != RuntimeInfo.ProductVersion)) ||
            ((config.MinimumSdkVersion is null) != (config.MaximumSdkVersionExclusive is null)) ||
            (config.MinimumSdkVersion is not null && (config.SchemaVersion != 2 ||
                !StableSdk(config.MinimumSdkVersion, out var lower) ||
                !StableSdk(config.MaximumSdkVersionExclusive!, out var upper) || lower >= upper)))
            throw new ConfigurationException("Unsupported or incomplete test gate config.");
    }

    internal static bool AcceptsSelectedSdk(TestGateConfig config, string approvedBaseline, string selected)
    {
        if (config.MinimumSdkVersion is null) return selected == approvedBaseline;
        return StableSdk(selected, out var actual) &&
            actual >= Version.Parse(config.MinimumSdkVersion) &&
            actual < Version.Parse(config.MaximumSdkVersionExclusive!);
    }

    private static bool StableSdk(string value, out Version version)
    {
        version = new Version(0, 0);
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d+\.\d+\.\d+$") ||
            !Version.TryParse(value, out var parsed)) return false;
        version = parsed;
        return true;
    }

    private static string Under(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new ConfigurationException("Test gate target paths must be relative.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!PathSafety.IsUnder(full, root)) throw new ConfigurationException("Test gate path escapes target root.");
        return full;
    }

    private static string FileHash(string path) => ContentHash.Bytes(File.ReadAllBytes(path));

    private static bool InputsChanged(Dictionary<string, string> watched) => watched.Any(item =>
        !File.Exists(item.Key) || FileHash(item.Key) != item.Value);
}

internal sealed class RelevantInputObserver : IDisposable
{
    private readonly string root;
    private readonly HashSet<string> initialInputs;
    private readonly FileSystemWatcher watcher;
    private int changed;

    internal RelevantInputObserver(string root)
    {
        this.root = root;
        initialInputs = EnumerateRelevantInputs(root);
        watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024
        };
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += (_, args) =>
        {
            Mark(args.OldFullPath, args.ChangeType);
            Mark(args.FullPath, args.ChangeType);
        };
        watcher.Error += (_, _) => Interlocked.Exchange(ref changed, 1);
        watcher.EnableRaisingEvents = true;
    }

    internal bool Changed
    {
        get
        {
            if (Volatile.Read(ref changed) != 0) return true;
            try
            {
                // Recursive watchers can miss the first file in a newly created directory.
                // Compare paths at every gate checkpoint so a missed event cannot certify that input.
                if (!initialInputs.SetEquals(EnumerateRelevantInputs(root)))
                    Interlocked.Exchange(ref changed, 1);
            }
            catch (IOException) { Interlocked.Exchange(ref changed, 1); }
            catch (UnauthorizedAccessException) { Interlocked.Exchange(ref changed, 1); }
            return Volatile.Read(ref changed) != 0;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs args) => Mark(args.FullPath, args.ChangeType);

    private void Mark(string path, WatcherChangeTypes changeType)
    {
        var segments = path.Replace('\\', '/').Split('/');
        if (segments.Any(IsIgnored)) return;
        var initialDirectory = initialInputs.Contains("d:" + Path.GetRelativePath(root, path));
        if (Directory.Exists(path) || initialDirectory)
        {
            // Creating ignored obj/bin changes the existing parent's LastWrite metadata.
            // Directory topology events and new directories remain relevant, even if transient.
            if (changeType != WatcherChangeTypes.Changed || !initialDirectory)
                Interlocked.Exchange(ref changed, 1);
            return;
        }
        // A topology event can arrive after a transient directory has disappeared.
        // Its kind is then unknown; retain a conservative block rather than lose it.
        if (changeType != WatcherChangeTypes.Changed && !File.Exists(path))
        {
            Interlocked.Exchange(ref changed, 1);
            return;
        }
        var extension = Path.GetExtension(path);
        if (IsRelevantExtension(extension))
            Interlocked.Exchange(ref changed, 1);
    }

    private static HashSet<string> EnumerateRelevantInputs(string root)
    {
        var paths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                if (IsIgnored(Path.GetFileName(path))) continue;
                var attributes = File.GetAttributes(path);
                var relativePath = Path.GetRelativePath(root, path);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    paths.Add("d:" + relativePath);
                    if ((attributes & FileAttributes.ReparsePoint) == 0) pending.Push(path);
                }
                else if (IsRelevantExtension(Path.GetExtension(path)))
                {
                    paths.Add("f:" + relativePath);
                }
            }
        }
        return paths;
    }

    private static bool IsIgnored(string part) => part is "bin" or "obj" or "artifacts" or ".git" or "node_modules";

    private static bool IsRelevantExtension(string extension) => extension is ".cs" or ".csproj" or ".props" or ".targets" or ".slnx" or ".json" or ".txt";

    public void Dispose() => watcher.Dispose();
}
