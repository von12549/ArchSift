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
    public required string LocalFeed { get; init; }
    public required string PackageCache { get; init; }
    public required string[] Rulesets { get; init; }
    public required string OriginalBaseline { get; init; }
    public required string OriginalBaselineSha256 { get; init; }
    public required string RunDirectory { get; init; }
    public string? DotnetCliHome { get; init; }
    public string? FilterCaseId { get; init; }
    public string RunnerFormat { get; init; } = "xunit-json";
}

public sealed record GateStage(string Status, int? ExitCode, string? LogPath);

public sealed record TestGateResult(int ExitCode, string Gate, string[] Reasons,
    GateStage Restore, GateStage Build, GateStage Test, MtpRun? NativeRun,
    GateCoverage? Coverage, string RunDirectory, bool SnapshotUpdatesDisabled,
    bool RelevantInputChanged)
{
    public string? ReportPath { get; init; }
    public string? HtmlPath { get; init; }
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
        var project = Under(root, config.TestProject);
        var nugetConfig = Under(root, config.NuGetConfig);
        var baseline = Path.GetFullPath(config.OriginalBaseline);
        var runDirectory = Path.GetFullPath(config.RunDirectory);
        var rulesetPaths = config.Rulesets.Select(path => Under(root, path)).ToArray();
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
                !PathSafety.IsUnder(config.DotnetCliHome, runDirectory))
                throw new ConfigurationException("DOTNET_CLI_HOME must be an owned child of the run directory.");
            PathSafety.EnsureNoLinks(config.DotnetCliHome);
        }
        if (Directory.Exists(runDirectory) || File.Exists(runDirectory))
            throw new ConfigurationException("Run directory already exists.");
        if (!Directory.Exists(config.LocalFeed)) throw new ConfigurationException("Offline package source is missing.");
        if (!Directory.Exists(config.PackageCache)) throw new ConfigurationException("Package cache is missing.");
        PathSafety.EnsureNoLinks(config.LocalFeed);
        PathSafety.EnsureNoLinks(config.PackageCache);
        var baselineBytes = File.ReadAllBytes(baseline);
        if (ContentHash.Bytes(baselineBytes) != config.OriginalBaselineSha256)
            throw new ConfigurationException("Original-case baseline hash changed.");
        var originalIds = JsonSerializer.Deserialize<string[]>(baselineBytes)
            ?? throw new ConfigurationException("Original-case baseline is empty.");
        if (originalIds.Length == 0 || originalIds.Distinct(StringComparer.Ordinal).Count() != originalIds.Length)
            throw new ConfigurationException("Original-case baseline is empty or has duplicates.");
        var (bundle, cases) = ArchitectureTestAdapter.LoadCases(rulesetPaths);
        var expectedJson = cases.ToDictionary(item => item.CaseId, item => item.Ruleset.Identity.Sha256,
            StringComparer.Ordinal);
        if (bundle.Rules.Length == 0) throw new ConfigurationException("No JSON rules are enabled.");

        var watched = new[] { project, nugetConfig, baseline, Path.GetFullPath(configPath) }.Concat(rulesetPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(path => path, FileHash, StringComparer.OrdinalIgnoreCase);
        using var sourceObserver = new RelevantInputObserver(root);
        Directory.CreateDirectory(runDirectory);
        TestRunHistory.Begin(runDirectory, runId, root, startedUtc);
        var notStarted = new GateStage("not-started", null, null);
        var restore = notStarted;
        var build = notStarted;
        var test = notStarted;
        var changed = false;
        try
        {
            restore = await Execute("restore", "dotnet",
                ["restore", project, "--configfile", nugetConfig, "--source", Path.GetFullPath(config.LocalFeed),
                    "--packages", Path.GetFullPath(config.PackageCache), "--disable-parallel", "--disable-build-servers",
                    "-p:NuGetAudit=false", "-m:1", "-v:minimal"], root, runDirectory, config, configPath, token);
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
                discovery = await Execute("discovery", executable,
                    ["--list-tests", "json", "--no-ansi", "--progress", "off"],
                    root, runDirectory, config, configPath, token);
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
                    ? MtpCtrf.Parse(File.ReadAllBytes(discovery!.LogPath!), File.ReadAllBytes(ctrfPath))
                    : MtpEvents.Parse(File.ReadAllLines(test.LogPath!, Encoding.UTF8));
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or KeyNotFoundException
                or InvalidOperationException or FileNotFoundException)
            { return Complete(Finish(3, "MTP structured output is invalid: " + error.Message)); }
            var coverage = MtpEvents.Assess(native, originalIds, expectedJson, test.ExitCode ?? 3,
                subset: config.FilterCaseId is not null);
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
            var published = false;
            try
            {
                var (json, html) = TestRunReportWriter.Save(config, result, bundle, originalIds,
                    runId, startedUtc, DateTimeOffset.UtcNow);
                published = true;
                var saved = result with { ReportPath = json, HtmlPath = html };
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
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workdir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["IFX_UPDATE_SNAPSHOTS"] = "0";
        start.Environment["ARCHSIFT_TEST_GATE_CONFIG"] = Path.GetFullPath(configPath);
        if (config.DotnetCliHome is not null)
        {
            start.Environment["DOTNET_CLI_HOME"] = Path.GetFullPath(config.DotnetCliHome);
            start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        }
        using var process = Process.Start(start) ?? throw new IOException("Could not start " + stage + " process.");
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(token);
            var stderr = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            var log = Path.Combine(runDirectory, stage + ".log");
            await File.WriteAllTextAsync(log, await stdout, new UTF8Encoding(false), token);
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
    }

    private static void Validate(TestGateConfig config)
    {
        var paths = new[] { config.TargetRoot, config.TestProject, config.TestExecutableName,
            config.TargetFramework, config.Configuration, config.NuGetConfig, config.LocalFeed,
            config.PackageCache, config.OriginalBaseline, config.OriginalBaselineSha256, config.RunDirectory };
        if (paths.Any(string.IsNullOrWhiteSpace) || config.Rulesets is null ||
            config.Rulesets.Any(string.IsNullOrWhiteSpace))
            throw new ConfigurationException("Test gate config has missing values.");
        if (config.SchemaVersion != 1 || config.TargetFramework != "net10.0" ||
            config.RunnerFormat is not ("xunit-json" or "mtp-ctrf") ||
            config.Configuration is not ("Debug" or "Release") ||
            config.Rulesets is not { Length: > 0 } ||
            Path.GetFileName(config.TestExecutableName) != config.TestExecutableName ||
            config.TestExecutableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathFullyQualified(config.TargetRoot) || !Path.IsPathFullyQualified(config.LocalFeed) ||
            !Path.IsPathFullyQualified(config.PackageCache) || !Path.IsPathFullyQualified(config.OriginalBaseline) ||
            !Path.IsPathFullyQualified(config.RunDirectory) ||
            config.OriginalBaselineSha256.Length != 64 ||
            config.OriginalBaselineSha256.Any(character => !Uri.IsHexDigit(character)))
            throw new ConfigurationException("Unsupported or incomplete test gate config.");
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
    private readonly FileSystemWatcher watcher;
    private int changed;

    internal RelevantInputObserver(string root)
    {
        watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024
        };
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += (_, args) => Mark(args.FullPath);
        watcher.Error += (_, _) => Interlocked.Exchange(ref changed, 1);
        watcher.EnableRaisingEvents = true;
    }

    internal bool Changed => Volatile.Read(ref changed) != 0;

    private void OnChanged(object sender, FileSystemEventArgs args) => Mark(args.FullPath);

    private void Mark(string path)
    {
        var segments = path.Replace('\\', '/').Split('/');
        if (segments.Any(part => part is "bin" or "obj" or "artifacts" or ".git" or "node_modules")) return;
        var extension = Path.GetExtension(path);
        if (extension is ".cs" or ".csproj" or ".props" or ".targets" or ".slnx" or ".json" or ".txt")
            Interlocked.Exchange(ref changed, 1);
    }

    public void Dispose() => watcher.Dispose();
}
