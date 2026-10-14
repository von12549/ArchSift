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
                Console.WriteLine("archsift-inproject gate --project <worktree-root>/.archsift/project.json [--evidence daily]");
                Console.WriteLine("archsift-inproject status --project <worktree-root>/.archsift/project.json [--run <run-id>]");
                Console.WriteLine("archsift-inproject cancel --project <worktree-root>/.archsift/project.json --run <run-id>");
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
                    throw new ConfigurationException("gate requires --project and optional --evidence daily.");
                if (gateOptions.GetValueOrDefault("--evidence", "daily") != "daily")
                    throw new ConfigurationException("Full evidence is unavailable before IP05.");
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
                        cancellation.Token).GetAwaiter().GetResult();
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
            if (args.Length == 0 || args[0] != "init")
                throw new ConfigurationException("Expected init, verify, build, gate, status or cancel.");
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
                "--project" or "--evidence" or "--run") ||
                string.IsNullOrWhiteSpace(args[i + 1]) || !values.TryAdd(args[i], args[i + 1]))
                throw new ConfigurationException("Invalid or repeated init option: " + args[i]);
        }
        return values;
    }

    private static string Required(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : throw new ConfigurationException("Missing " + key + ".");
}
