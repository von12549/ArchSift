using System.Text.Json;
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
            if (args.Length == 0 || args[0] != "init")
                throw new ConfigurationException("Only init and verify are available in the IP01 development host.");
            var options = Parse(args[1..]);
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
            if (args[i] is not ("--target" or "--package" or "--sha256" or "--solution") ||
                string.IsNullOrWhiteSpace(args[i + 1]) || !values.TryAdd(args[i], args[i + 1]))
                throw new ConfigurationException("Invalid or repeated init option: " + args[i]);
        }
        return values;
    }

    private static string Required(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : throw new ConfigurationException("Missing " + key + ".");
}
