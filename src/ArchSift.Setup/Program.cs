using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Setup;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        try
        {
            if (args is ["--version"]) { Console.WriteLine("archsift-setup " + RuntimeInfo.ProductVersion); return 0; }
            if (args is ["--notices"])
            {
                var assembly = typeof(Program).Assembly;
                foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("ArchSift.Setup.Notices.", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
                { using var stream = assembly.GetManifestResourceStream(name)!; using var reader = new StreamReader(stream); Console.WriteLine(name); Console.WriteLine(await reader.ReadToEndAsync(cancellation.Token)); }
                return 0;
            }
            if (args is [] or ["--help"])
            {
                Console.WriteLine("ArchSift portable setup. No PATH changes, SDK installation or target execution. --notices prints embedded licenses.");
                Console.WriteLine("inspect --root <root>; acquire --version <version> --sha256 <hash> --output <new-zip>");
                Console.WriteLine("plan install --root <root> --package <zip> --sha256 <hash> --target <root> --entry <relative-entry> --tfm <net8.0|net9.0|net10.0> [--configuration Debug|Release] [--output <reports>] [--library <rules>] [--smoke true|false]");
                Console.WriteLine("plan adopt --root <root> --version-directory <payload> --config <existing-config>");
                Console.WriteLine("plan upgrade --root <root> --package <zip> --sha256 <hash> [--smoke true|false]");
                Console.WriteLine("apply --plan <reviewed-json> --plan-id <sha256>");
                Console.WriteLine("rollback --root <root> --receipt <id> --selection-sha256 <hash>");
                Console.WriteLine("recover --root <root> --operation <id> --journal-sha256 <hash>");
                return 0;
            }
            var service = new SetupService(); object result;
            if (args is ["inspect", .. var inspect]) { var options = Flags(inspect, "--root"); result = service.Inspect(Required(options, "--root")); }
            else if (args is ["plan", "install", .. var install])
            {
                var options = Flags(install, "--root", "--package", "--sha256", "--target", "--entry", "--tfm", "--configuration", "--output", "--library", "--smoke");
                var root = SetupFiles.Safe(Required(options, "--root"));
                var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = SetupFiles.Safe(Required(options, "--target")), Entry = Required(options, "--entry").Replace('\\', '/') },
                    Build = new() { TargetFramework = Required(options, "--tfm"), Configuration = options.GetValueOrDefault("--configuration", "Debug") },
                    Output = new() { Directory = SetupFiles.Safe(options.GetValueOrDefault("--output", Path.Combine(root, "reports"))), Formats = ["json", "html", "sarif"] },
                    RulesDirectory = SetupFiles.Safe(options.GetValueOrDefault("--library", Path.Combine(root, "rules"))) };
                result = service.PlanInstall(root, Required(options, "--package"), Required(options, "--sha256"), config, Smoke(options));
            }
            else if (args is ["plan", "upgrade", .. var upgrade])
            {
                var options = Flags(upgrade, "--root", "--package", "--sha256", "--smoke");
                result = service.PlanUpgrade(Required(options, "--root"), Required(options, "--package"), Required(options, "--sha256"), Smoke(options));
            }
            else if (args is ["plan", "adopt", .. var adopt])
            {
                var options = Flags(adopt, "--root", "--version-directory", "--config");
                result = service.PlanAdopt(Required(options, "--root"), Required(options, "--version-directory"), Required(options, "--config"));
            }
            else if (args is ["apply", .. var apply])
            {
                var options = Flags(apply, "--plan", "--plan-id");
                result = await service.ApplyAsync(SetupFiles.Read<SetupPlan>(Required(options, "--plan")), Required(options, "--plan-id"), cancellation.Token);
            }
            else if (args is ["rollback", .. var rollback])
            {
                var options = Flags(rollback, "--root", "--receipt", "--selection-sha256");
                result = service.Rollback(Required(options, "--root"), Required(options, "--receipt"), Required(options, "--selection-sha256"));
            }
            else if (args is ["recover", .. var recover])
            {
                var options = Flags(recover, "--root", "--operation", "--journal-sha256");
                result = service.Recover(Required(options, "--root"), Required(options, "--operation"), Required(options, "--journal-sha256"));
            }
            else if (args is ["acquire", .. var acquire])
            {
                var options = Flags(acquire, "--version", "--sha256", "--output");
                result = await Acquire(Required(options, "--version"), Required(options, "--sha256"), Required(options, "--output"), cancellation.Token);
            }
            else throw new ConfigurationException("Unknown setup operation. Use --help.");
            Console.WriteLine(JsonSerializer.Serialize(result, JsonContract.Options)); return 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled; operation evidence retained."); return 130; }
        catch (Exception error) when (error is ConfigurationException or JsonException or ArgumentException or KeyNotFoundException)
        { Console.Error.WriteLine(error.Message); return 2; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or TimeoutException or HttpRequestException or System.ComponentModel.Win32Exception)
        { Console.Error.WriteLine(error.Message); return 3; }
    }

    private static Dictionary<string, string> Flags(string[] args, params string[] allowed)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
            if (i + 1 == args.Length || !allowed.Contains(args[i], StringComparer.Ordinal) || !options.TryAdd(args[i], args[i + 1]))
                throw new ConfigurationException("Unknown, duplicate or missing option: " + args[i]);
        return options;
    }
    private static string Required(Dictionary<string, string> options, string key) => options.TryGetValue(key, out var value) ? value : throw new ConfigurationException("Missing " + key);
    private static bool Smoke(Dictionary<string, string> options) => options.GetValueOrDefault("--smoke", "false") switch
    { "true" => true, "false" => false, _ => throw new ConfigurationException("--smoke requires true or false.") };
    private static async Task<object> Acquire(string version, string sha256, string output, CancellationToken cancellation)
    {
        PackageReader.Version(version);
        if (!System.Text.RegularExpressions.Regex.IsMatch(sha256, "^[a-f0-9]{64}$")) throw new ConfigurationException("Expected lowercase ZIP SHA-256 is required.");
        output = SetupFiles.Safe(output);
        if (File.Exists(output)) throw new ConfigurationException("Download destination already exists.");
        var baseline = SetupFiles.HostHash();
        var staging = output + ".download-" + Guid.NewGuid().ToString("N");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var url = "https://github.com/von12549/ArchSift/releases/download/v" + version + "/archsift-" + version + "-win-x64.zip";
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation); response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > PackageReader.MaximumZipBytes) throw new IOException("Download exceeds 1 GiB.");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!); SetupFiles.Safe(staging);
            await using (var input = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var destination = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920]; long bytes = 0; int count;
                while ((count = await input.ReadAsync(buffer, cancellation)) > 0)
                { if ((bytes += count) > PackageReader.MaximumZipBytes) throw new IOException("Download exceeds 1 GiB."); await destination.WriteAsync(buffer.AsMemory(0, count), cancellation); }
                await destination.FlushAsync(cancellation); destination.Flush(true);
            }
            var identity = PackageReader.Inspect(staging, sha256);
            if (identity.Version != version) throw new ConfigurationException("Downloaded version mismatch.");
            SetupFiles.HostCheckpoint(baseline); SetupFiles.Safe(output); File.Move(staging, output, false);
            return new { status = "downloaded-and-verified", path = output, version, sha256, publisherSignatureVerified = false };
        }
        finally { SetupFiles.HostCheckpoint(baseline); }
    }
}
