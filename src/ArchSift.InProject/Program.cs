using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.InProject.Runtime;

namespace ArchSift.InProject;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--version")
            {
                Console.WriteLine("ArchSift.InProject 0.1.0-dev");
                return 0;
            }
            if (args.Length == 1 && args[0] == "--help")
            {
                Console.WriteLine("archsift-inproject init --target <worktree-root> --package <local-zip> --sha256 <lowercase-sha256> [--solution <root-relative.sln|.slnx>]");
                Console.WriteLine("archsift-inproject verify --target <worktree-root>");
                Console.WriteLine("archsift-inproject build --target <worktree-root>");
                Console.WriteLine("archsift-inproject gate --project <worktree-root>/.archsift/project.json [--evidence daily|full]");
                Console.WriteLine("archsift-inproject status --project <worktree-root>/.archsift/project.json [--run <run-id>]");
                Console.WriteLine("archsift-inproject cancel --project <worktree-root>/.archsift/project.json --run <run-id>");
                Console.WriteLine("archsift-inproject evidence-verify --bundle <full-evidence-directory>");
                Console.WriteLine("archsift-inproject evidence-archive --bundle <full-evidence-directory> --archive <new-zip>");
                Console.WriteLine("archsift-inproject evidence-verify-archive --archive <zip>");
                Console.WriteLine("archsift-inproject evidence-restore --archive <zip> --destination <new-directory>");
                Console.WriteLine("archsift-inproject retention --project <.archsift/project.json> [--apply true]");
                Console.WriteLine("archsift-inproject pin --project <.archsift/project.json> --run <run-id> --value true|false");
                Console.WriteLine("archsift-inproject evidence-prune --project <.archsift/project.json> --run <run-id> --bundle <full-directory> --archive <verified-zip> [--apply true]");
                Console.WriteLine("archsift-inproject worktree-export --project <.archsift/project.json> [--destination <new-external-directory>]");
                Console.WriteLine("archsift-inproject worktree-export-verify --directory <export-directory>");
                return 0;
            }
            if (args.Length > 0 && args[0] == "verify")
            {
                var verifyOptions = Parse(args[1..]);
                if (verifyOptions.Count != 1 || !verifyOptions.ContainsKey("--target"))
                    throw new ConfigurationException("verify requires only --target.");
                Console.WriteLine(JsonSerializer.Serialize(new { product = "ArchSift.InProject",
                    version = LocalPackage.VerifyInstalled(Required(verifyOptions, "--target")), verified = true }));
                return 0;
            }
            if (args.Length > 0 && args[0] == "build")
            {
                var buildOptions = Parse(args[1..]);
                if (buildOptions.Count != 1 || !buildOptions.ContainsKey("--target"))
                    throw new ConfigurationException("build requires only --target.");
                using var cancellation = new CancellationTokenSource();
                ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };
                Console.CancelKeyPress += onCancel;
                try
                {
                    var buildResult = InProjectDailyBuild.RunAsync(Required(buildOptions, "--target"),
                        cancellation.Token).GetAwaiter().GetResult();
                    Console.WriteLine(JsonSerializer.Serialize(buildResult, JsonContract.Options));
                    return buildResult.SourceBound ? 0 : 4;
                }
                finally { Console.CancelKeyPress -= onCancel; }
            }
            if (args.Length > 0 && args[0] == "gate")
            {
                var gateOptions = Parse(args[1..]);
                if (!gateOptions.ContainsKey("--project") || gateOptions.Count > 2 ||
                    gateOptions.Keys.Any(key => key is not ("--project" or "--evidence")))
                    throw new ConfigurationException("gate requires --project and optional --evidence daily|full.");
                var evidenceGrade = gateOptions.GetValueOrDefault("--evidence", "daily");
                var project = Path.GetFullPath(Required(gateOptions, "--project"));
                var install = Path.GetDirectoryName(project)!;
                if (Path.GetFileName(project) != "project.json" ||
                    Path.GetFileName(install) != ".archsift" || !File.Exists(project))
                    throw new ConfigurationException("gate --project must identify this worktree's .archsift/project.json.");
                PathSafety.EnsureNoLinks(project);
                using var cancellation = new CancellationTokenSource();
                ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };
                Console.CancelKeyPress += onCancel;
                try
                {
                    var outcome = InProjectTaskService.GateAsync(Path.GetDirectoryName(install)!,
                        evidenceGrade, cancellation.Token).GetAwaiter().GetResult();
                    Console.WriteLine(JsonSerializer.Serialize((object?)outcome.Result ?? outcome.Task,
                        JsonContract.Options));
                    return outcome.Conflict ? 4 : outcome.Task.ExitCode ?? 4;
                }
                finally { Console.CancelKeyPress -= onCancel; }
            }
            if (args.Length > 0 && args[0] == "status")
            {
                var statusOptions = Parse(args[1..]);
                if (!statusOptions.ContainsKey("--project") || statusOptions.Count > 2 ||
                    statusOptions.Keys.Any(key => key is not ("--project" or "--run")))
                    throw new ConfigurationException("status requires --project and optional --run.");
                var project = Path.GetFullPath(Required(statusOptions, "--project"));
                var install = Path.GetDirectoryName(project)!;
                if (Path.GetFileName(project) != "project.json" ||
                    Path.GetFileName(install) != ".archsift" || !File.Exists(project))
                    throw new ConfigurationException("status --project must identify this worktree's .archsift/project.json.");
                PathSafety.EnsureNoLinks(project);
                var status = InProjectTaskService.Status(Path.GetDirectoryName(install)!,
                    statusOptions.GetValueOrDefault("--run"));
                Console.WriteLine(JsonSerializer.Serialize(status, JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "cancel")
            {
                var cancelOptions = Parse(args[1..]);
                if (cancelOptions.Count != 2 || !cancelOptions.ContainsKey("--project") ||
                    !cancelOptions.ContainsKey("--run"))
                    throw new ConfigurationException("cancel requires --project and --run.");
                var project = Path.GetFullPath(Required(cancelOptions, "--project"));
                var install = Path.GetDirectoryName(project)!;
                if (Path.GetFileName(project) != "project.json" ||
                    Path.GetFileName(install) != ".archsift" || !File.Exists(project))
                    throw new ConfigurationException("cancel --project must identify this worktree's .archsift/project.json.");
                PathSafety.EnsureNoLinks(project);
                var state = InProjectTaskService.Cancel(Path.GetDirectoryName(install)!,
                    Required(cancelOptions, "--run"));
                Console.WriteLine(JsonSerializer.Serialize(state, JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "evidence-verify")
            {
                var evidenceOptions = Parse(args[1..]);
                if (evidenceOptions.Count != 1 || !evidenceOptions.ContainsKey("--bundle"))
                    throw new ConfigurationException("evidence-verify requires --bundle.");
                var manifest = InProjectFullEvidence.Verify(Required(evidenceOptions, "--bundle"));
                Console.WriteLine(JsonSerializer.Serialize(new { manifest.RunId, manifest.WorktreeId,
                    verified = true, files = manifest.Files.Length }, JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "evidence-archive")
            {
                var archiveOptions = Parse(args[1..]);
                if (archiveOptions.Count != 2 || !archiveOptions.ContainsKey("--bundle") ||
                    !archiveOptions.ContainsKey("--archive"))
                    throw new ConfigurationException("evidence-archive requires --bundle and --archive.");
                var sha = InProjectFullEvidence.Archive(Required(archiveOptions, "--bundle"),
                    Required(archiveOptions, "--archive"));
                Console.WriteLine(JsonSerializer.Serialize(new { archiveSha256 = sha, verified = true },
                    JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "evidence-verify-archive")
            {
                var archiveOptions = Parse(args[1..]);
                if (archiveOptions.Count != 1 || !archiveOptions.ContainsKey("--archive"))
                    throw new ConfigurationException("evidence-verify-archive requires --archive.");
                var manifest = InProjectFullEvidence.VerifyArchive(Required(archiveOptions, "--archive"));
                Console.WriteLine(JsonSerializer.Serialize(new { manifest.RunId, verified = true,
                    files = manifest.Files.Length }, JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "evidence-restore")
            {
                var restoreOptions = Parse(args[1..]);
                if (restoreOptions.Count != 2 || !restoreOptions.ContainsKey("--archive") ||
                    !restoreOptions.ContainsKey("--destination"))
                    throw new ConfigurationException("evidence-restore requires --archive and --destination.");
                var manifest = InProjectFullEvidence.Restore(Required(restoreOptions, "--archive"),
                    Required(restoreOptions, "--destination"));
                Console.WriteLine(JsonSerializer.Serialize(new { manifest.RunId, verified = true,
                    files = manifest.Files.Length }, JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "retention")
            {
                var retentionOptions = Parse(args[1..]);
                if (!retentionOptions.ContainsKey("--project") || retentionOptions.Count > 2 ||
                    retentionOptions.Keys.Any(key => key is not ("--project" or "--apply")))
                    throw new ConfigurationException("retention requires --project and optional --apply true.");
                var root = RootFromProject(Required(retentionOptions, "--project"));
                var apply = retentionOptions.GetValueOrDefault("--apply", "false");
                if (apply is not ("true" or "false")) throw new ConfigurationException("--apply must be true or false.");
                var preview = apply == "true" ? InProjectRetentionService.Apply(root) :
                    InProjectRetentionService.Preview(root);
                Console.WriteLine(JsonSerializer.Serialize(preview, JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "pin")
            {
                var pinOptions = Parse(args[1..]);
                if (pinOptions.Count != 3 || !pinOptions.ContainsKey("--project") ||
                    !pinOptions.ContainsKey("--run") || !pinOptions.ContainsKey("--value") ||
                    pinOptions["--value"] is not ("true" or "false"))
                    throw new ConfigurationException("pin requires --project, --run and --value true|false.");
                InProjectRetentionService.SetPin(RootFromProject(pinOptions["--project"]),
                    pinOptions["--run"], pinOptions["--value"] == "true");
                Console.WriteLine(JsonSerializer.Serialize(new { runId = pinOptions["--run"],
                    pinned = pinOptions["--value"] == "true" }, JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "evidence-prune")
            {
                var pruneOptions = Parse(args[1..]);
                if (!pruneOptions.ContainsKey("--project") || !pruneOptions.ContainsKey("--run") ||
                    !pruneOptions.ContainsKey("--bundle") || !pruneOptions.ContainsKey("--archive") ||
                    pruneOptions.Count > 5 ||
                    pruneOptions.Keys.Any(key => key is not ("--project" or "--run" or "--bundle" or "--archive" or "--apply")))
                    throw new ConfigurationException("evidence-prune requires --project, --run, --bundle, --archive and optional --apply true.");
                var apply = pruneOptions.GetValueOrDefault("--apply", "false");
                if (apply is not ("true" or "false")) throw new ConfigurationException("--apply must be true or false.");
                var preview = InProjectFullEvidence.Prune(RootFromProject(pruneOptions["--project"]),
                    pruneOptions["--run"], pruneOptions["--bundle"], pruneOptions["--archive"],
                    apply == "true");
                Console.WriteLine(JsonSerializer.Serialize(preview, JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "worktree-export")
            {
                var exportOptions = Parse(args[1..]);
                if (!exportOptions.ContainsKey("--project") || exportOptions.Count > 2 ||
                    exportOptions.Keys.Any(key => key is not ("--project" or "--destination")))
                    throw new ConfigurationException("worktree-export requires --project and optional --destination.");
                var root = RootFromProject(exportOptions["--project"]);
                if (exportOptions.TryGetValue("--destination", out var destination))
                {
                    var exported = InProjectWorktreeExport.Export(root, destination);
                    Console.WriteLine(JsonSerializer.Serialize(new { exported.WorktreeId,
                        files = exported.Files.Length, verified = true }, JsonContract.Options));
                }
                else Console.WriteLine(JsonSerializer.Serialize(InProjectWorktreeExport.Preview(root),
                    JsonContract.Options));
                return 0;
            }
            if (args.Length > 0 && args[0] == "worktree-export-verify")
            {
                var exportOptions = Parse(args[1..]);
                if (exportOptions.Count != 1 || !exportOptions.ContainsKey("--directory"))
                    throw new ConfigurationException("worktree-export-verify requires --directory.");
                var exported = InProjectWorktreeExport.Verify(exportOptions["--directory"]);
                Console.WriteLine(JsonSerializer.Serialize(new { exported.WorktreeId,
                    files = exported.Files.Length, verified = true }, JsonContract.Options));
                return 0;
            }
            if (args.Length == 0 || args[0] != "init")
                throw new ConfigurationException("Expected an InProject command; use --help for the supported commands.");
            var options = Parse(args[1..]);
            if (options.Keys.Any(key => key is not ("--target" or "--package" or "--sha256" or "--solution")))
                throw new ConfigurationException("init received an unsupported option.");
            var result = InProjectInstaller.Initialize(Required(options, "--target"), Required(options, "--package"),
                Required(options, "--sha256"), options.GetValueOrDefault("--solution"));
            Console.WriteLine(JsonSerializer.Serialize(result));
            return 0;
        }
        catch (ConfigurationException error)
        {
            Console.Error.WriteLine(error.Message);
            return 5;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("InProject build cancelled; inspect current run state.");
            return 130;
        }
        catch (SourceChangedException)
        {
            Console.Error.WriteLine("InProject source changed during this build; current binding is unavailable.");
            return 4;
        }
        catch (IOException error)
        {
            Console.Error.WriteLine(error.Message);
            return 3;
        }
        catch (UnauthorizedAccessException error)
        {
            Console.Error.WriteLine(error.Message);
            return 3;
        }
        catch (JsonException error)
        {
            Console.Error.WriteLine("Invalid package or installation JSON: " + error.Message);
            return 5;
        }
        catch (InvalidDataException error)
        {
            Console.Error.WriteLine(error.Message);
            return 5;
        }
        catch (InvalidOperationException error)
        {
            Console.Error.WriteLine(error.Message);
            return 5;
        }
        catch (KeyNotFoundException error)
        {
            Console.Error.WriteLine(error.Message);
            return 5;
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine(error.Message);
            return 5;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        if (args.Length % 2 != 0) throw new ConfigurationException("Expected option/value pairs.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (args[i] is not ("--target" or "--package" or "--sha256" or "--solution" or
                "--project" or "--evidence" or "--run" or "--bundle" or "--archive" or
                "--destination" or "--directory" or "--apply" or "--value") ||
                string.IsNullOrWhiteSpace(args[i + 1]) || !values.TryAdd(args[i], args[i + 1]))
                throw new ConfigurationException("Invalid or repeated init option: " + args[i]);
        }
        return values;
    }

    private static string Required(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : throw new ConfigurationException("Missing " + key + ".");

    private static string RootFromProject(string projectValue)
    {
        var project = Path.GetFullPath(projectValue);
        var install = Path.GetDirectoryName(project)!;
        if (Path.GetFileName(project) != "project.json" ||
            Path.GetFileName(install) != ".archsift" || !File.Exists(project))
            throw new ConfigurationException("--project must identify this worktree's .archsift/project.json.");
        PathSafety.EnsureNoLinks(project);
        return Path.GetDirectoryName(install)!;
    }
}
