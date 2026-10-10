using System.Text;
using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        Console.OutputEncoding = Encoding.UTF8;
        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
        try
        {
            if (args is ["__worker"])
            {
                var input = await Console.In.ReadToEndAsync(cancel.Token);
                if (input.Length > 16 * 1024 * 1024) return 2;
                var request = JsonSerializer.Deserialize<WorkerRequest>(input, JsonContract.Options)!;
                Console.WriteLine(JsonSerializer.Serialize(AssemblyWorker.Evaluate(request), JsonContract.Options)); return 0;
            }
            if (args is ["--version"]) { Console.WriteLine($"archsift {RuntimeInfo.ProductVersion}"); return 0; }
            if (args is ["ui", "--config", _] or ["launch", "--root", _])
            {
                var launch = args[0] == "launch";
                var configPath = Path.GetFullPath(args[2]); if (!launch) ConfigLoader.Load(configPath);
                var web = Path.Combine(AppContext.BaseDirectory, "web", "ArchSift.Web.dll");
                if (!File.Exists(web))
                {
                    var directory = new DirectoryInfo(AppContext.BaseDirectory);
                    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
                    var configuration = typeof(Program).Assembly.GetCustomAttributes(false).OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
                    if (directory is not null) web = Path.Combine(directory.FullName, "src", "ArchSift.Web", "bin", configuration, "net10.0", "ArchSift.Web.dll");
                }
                if (!File.Exists(web)) throw new IOException("Web payload is missing; use the complete local package.");
                var native = Path.ChangeExtension(web, OperatingSystem.IsWindows() ? ".exe" : null);
                return await ArchSift.Hosting.UiProcess.RunArgumentsAsync(File.Exists(native) ? native : SafeProcess.Dotnet,
                    File.Exists(native) ? null : web, [launch ? "--launcher" : "--config", configPath], Console.Out, Console.Error, cancel.Token,
                    onAddress: launch ? address =>
                    {
                        try { using var browser = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true }); }
                        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
                        { throw new IOException("Default browser could not open the local launcher."); }
                    } : null);
            }
            if (args is [] or ["--help"] or ["-h"])
            {
                Console.WriteLine("ArchSift — .NET architecture and dependency analyzer.");
                Console.WriteLine("--version / --help");
                Console.WriteLine("chain verify --config <JSON> --chain <JSON> [--target-kind real|fixture]");
                Console.WriteLine("analyze/verify --config <JSON> [--target --entry --rules --output --tfm --configuration]");
                Console.WriteLine("rules validate --file <JSON>; rules render --file <JSON> --output <new Markdown>");
                Console.WriteLine("rules draft --config <JSON> --output <directory>; ui --config <JSON>");
                Console.WriteLine("launch --root <verified-install-root>; choose a profile before starting the workbench.");
                Console.WriteLine("changes --config <JSON> [--base <local commit> --head <local commit>]; default HEAD vs final worktree."); return 0;
            }
            if (args.Length >= 2 && args[0] == "rules" && args[1] is "validate" or "render")
            {
                var flags = Flags(args[2..], ["--file", "--output"]);
                var file = Required(flags, "--file"); var loaded = RuleLoader.Load(file); RuleLoader.Compose([loaded]);
                if (args[1] == "validate") { Console.WriteLine($"Valid ruleset: {loaded.Ruleset.Id}; SHA-256: {loaded.Identity.Sha256}"); return 0; }
                var output = Path.GetFullPath(Required(flags, "--output"));
                if (Path.GetFullPath(file).Equals(output, PathSafety.Comparison)) throw new ConfigurationException("Markdown output must not overwrite the source JSON ruleset.");
                PathSafety.EnsureNoLinks(output);
                using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(RuleMarkdown.Render(loaded)); Console.WriteLine(output); return 0;
            }
            if (args.Length >= 2 && args[0] == "chain" && args[1] == "verify")
            {
                var flags = Flags(args[2..], ["--config", "--chain", "--target-kind"]);
                var config = ConfigLoader.Load(Required(flags, "--config"));
                var chain = ChainStore.Load(Required(flags, "--chain"));
                var library = new RulesetLibrary(config.RulesDirectory ?? Path.Combine(config.Output.Directory, "rules"), config.Target.Root);
                var worker = new AssemblyWorkerClient(typeof(Program).Assembly.Location);
                var summary = await new ChainService(new AnalysisService(worker.EvaluateAsync)).RunAsync(config, chain, library,
                    flags.GetValueOrDefault("--target-kind")?.Single() ?? "real", cancel.Token,
                    progress => Console.Error.WriteLine($"Chain {progress.RunId}: {progress.Stage}; ended {progress.EndedCount}/{progress.TotalCount}; executed {progress.ExecutedCount}; skipped {progress.SkippedCount}; running {progress.RunningCount}"));
                Console.WriteLine(ChainWriter.Json(summary)); return summary.ExitCode;
            }
            var draft = args.Length >= 2 && args[0] == "rules" && args[1] == "draft";
            if (args.Length > 0 && args[0] == "changes")
            {
                var flags = Flags(args[1..], ["--config", "--target", "--entry", "--rules", "--output", "--tfm", "--configuration", "--base", "--head"]);
                var ordinary = flags.Where(p => p.Key is not ("--base" or "--head")).SelectMany(p => p.Value.SelectMany(v => new[] { p.Key, v })).ToArray();
                var config = Configuration(ordinary);
                var worker = new AssemblyWorkerClient(typeof(Program).Assembly.Location);
                var outcome = await new ComparisonService(new AnalysisService(worker.EvaluateAsync)).RunAsync(config,
                    new(flags.GetValueOrDefault("--base")?.Single(), flags.GetValueOrDefault("--head")?.Single()), cancel.Token);
                ComparisonWriter.Save(config, outcome.Report); Console.WriteLine(ComparisonWriter.Json(outcome.Report)); return outcome.ExitCode;
            }
            if (draft || args.Length > 0 && args[0] is "analyze" or "verify")
            {
                var config = Configuration(args[(draft ? 2 : 1)..]);
                Console.Error.WriteLine($"Effective target: {config.Target.Root}; entry: {config.Target.Entry ?? "(root scan)"}; TFM: {config.Build.TargetFramework ?? "(declared)"}; configuration: {config.Build.Configuration}; build: {config.Build.Mode}; output: {config.Output.Directory}; network restore: {config.Build.AllowNetwork}");
                if (draft)
                {
                    PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
                    var facts = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework, cancel.Token);
                    var proposed = RuleDraftService.Create(facts);
                    var directory = Path.Combine(config.Output.Directory, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "draft.json"), JsonSerializer.Serialize(proposed, JsonContract.Options), new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(directory, "candidate-rules.json"), JsonSerializer.Serialize(proposed.CandidateRules, JsonContract.Options), new UTF8Encoding(false));
                    Console.WriteLine(directory); return 0;
                }
                var worker = new AssemblyWorkerClient(typeof(Program).Assembly.Location);
                var result = await new AnalysisService(worker.EvaluateAsync).RunAsync(config, args[0], cancel.Token);
                ReportWriter.Save(config, result.Report); Console.WriteLine(ReportWriter.Json(result.Report)); return result.ExitCode;
            }
            throw new ConfigurationException("Unsupported command. Use --help for available commands.");
        }
        catch (ConfigurationException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (JsonException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled."); return 130; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or TimeoutException)
        { Console.Error.WriteLine(error.Message); return 3; }
    }

    private static Dictionary<string, List<string>> Flags(string[] args, string[] accepted)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!accepted.Contains(args[i], StringComparer.Ordinal) || i + 1 >= args.Length) throw new ConfigurationException("Unknown/missing option: " + args[i]);
            if (!result.TryGetValue(args[i], out var values)) { values = []; result.Add(args[i], values); }
            else if (args[i] != "--rules") throw new ConfigurationException("Duplicate option: " + args[i]);
            values.Add(args[i + 1]);
        }
        return result;
    }
    private static string Required(Dictionary<string, List<string>> flags, string name) =>
        flags.TryGetValue(name, out var values) ? values.Single() : throw new ConfigurationException("Missing " + name);
    private static RunConfiguration Configuration(string[] arguments)
    {
        var flags = Flags(arguments, ["--config", "--target", "--entry", "--rules", "--output", "--tfm", "--configuration"]);
        var config = flags.TryGetValue("--config", out var paths) ? ConfigLoader.Load(paths.Single()) : new RunConfiguration
        {
            SchemaVersion = 1, Target = new() { Root = Path.GetFullPath(Required(flags, "--target")) },
            Output = new() { Directory = Path.GetFullPath(Required(flags, "--output")) }
        };
        var root = flags.TryGetValue("--target", out var targets) ? Path.GetFullPath(targets.Single()) : config.Target.Root;
        var entry = config.Target.Entry;
        if (flags.TryGetValue("--entry", out var entries))
        {
            var full = Path.GetFullPath(entries.Single());
            if (!PathSafety.IsUnder(full, root)) throw new ConfigurationException("Entry is outside TargetRoot.");
            entry = Path.GetRelativePath(root, full);
        }
        if (entry is not null && Path.IsPathRooted(entry))
        {
            if (!PathSafety.IsUnder(entry, root)) throw new ConfigurationException("Configured entry is outside TargetRoot.");
            entry = Path.GetRelativePath(root, entry);
        }
        var tfm = flags.TryGetValue("--tfm", out var frameworks) ? frameworks.Single() : config.Build.TargetFramework;
        var configuration = flags.TryGetValue("--configuration", out var configurations) ? configurations.Single() : config.Build.Configuration;
        if (configuration is not ("Debug" or "Release")) throw new ConfigurationException("Unsupported configuration.");
        return config with
        {
            Target = config.Target with { Root = root, Entry = entry },
            Rulesets = flags.TryGetValue("--rules", out var rules) ? rules.Select(p => Path.GetFullPath(p)).ToArray() : config.Rulesets,
            Build = config.Build with { TargetFramework = tfm, Configuration = configuration },
            Output = config.Output with { Directory = flags.TryGetValue("--output", out var outputs) ? Path.GetFullPath(outputs.Single()) : config.Output.Directory }
        };
    }
}
