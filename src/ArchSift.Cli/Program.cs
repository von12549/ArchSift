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
            if (args is ["ui", "--config", var uiConfig])
            {
                var configPath = Path.GetFullPath(uiConfig); ConfigLoader.Load(configPath);
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
                var start = new System.Diagnostics.ProcessStartInfo
                { FileName = File.Exists(native) ? native : SafeProcess.Dotnet, UseShellExecute = false, CreateNoWindow = true };
                if (!File.Exists(native)) start.ArgumentList.Add(web);
                start.ArgumentList.Add("--config"); start.ArgumentList.Add(configPath);
                start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Path.GetTempPath(), "archsift-ui-home-" + Guid.NewGuid().ToString("N"));
                start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
                using var process = System.Diagnostics.Process.Start(start)!;
                try { await process.WaitForExitAsync(cancel.Token); }
                catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); throw; }
                return process.ExitCode;
            }
            if (args is [] or ["--help"] or ["-h"])
            {
                Console.WriteLine("ArchSift — .NET 架构与依赖分析工具。");
                Console.WriteLine("--version / --help");
                Console.WriteLine("analyze/verify --config <JSON> [--target --entry --rules --output --tfm --configuration]");
                Console.WriteLine("rules validate --file <JSON>；rules render --file <JSON> --output <新 Markdown>");
                Console.WriteLine("rules draft --config <JSON> --output <目录>；ui --config <JSON>");
                Console.WriteLine("changes --config <JSON> [--base <本地 commit> --head <本地 commit>]；默认 HEAD 对磁盘工作区最终状态。"); return 0;
            }
            if (args.Length >= 2 && args[0] == "rules" && args[1] is "validate" or "render")
            {
                var flags = Flags(args[2..], ["--file", "--output"]);
                var file = Required(flags, "--file"); var loaded = RuleLoader.Load(file); RuleLoader.Compose([loaded]);
                if (args[1] == "validate") { Console.WriteLine($"有效规则集：{loaded.Ruleset.Id}；SHA-256：{loaded.Identity.Sha256}"); return 0; }
                var output = Path.GetFullPath(Required(flags, "--output"));
                if (Path.GetFullPath(file).Equals(output, PathSafety.Comparison)) throw new ConfigurationException("Markdown 输出不得覆盖 JSON 规则来源。");
                PathSafety.EnsureNoLinks(output);
                using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(RuleMarkdown.Render(loaded)); Console.WriteLine(output); return 0;
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
                Console.Error.WriteLine($"生效目标：{config.Target.Root}；入口：{config.Target.Entry ?? "(root scan)"}；TFM：{config.Build.TargetFramework ?? "(declared)"}；配置：{config.Build.Configuration}；产物：{config.Build.Mode}；输出：{config.Output.Directory}；网络 restore：{config.Build.AllowNetwork}");
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
            throw new ConfigurationException("配置错误：当前阶段不支持此命令。使用 --help 查看已实现入口。");
        }
        catch (ConfigurationException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (JsonException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (OperationCanceledException) { Console.Error.WriteLine("已取消。"); return 130; }
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
