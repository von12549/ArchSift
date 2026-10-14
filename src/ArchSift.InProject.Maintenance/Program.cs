using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.InProject.Runtime;

namespace ArchSift.InProject.Maintenance;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args is ["--help"] or [])
            {
                Console.WriteLine("archsift-inproject-maintenance inspect --target <worktree-root>");
                Console.WriteLine("archsift-inproject-maintenance upgrade --target <worktree-root> --package <local-zip> --sha256 <lowercase-sha256>");
                Console.WriteLine("archsift-inproject-maintenance rollback --target <worktree-root> --receipt <operation-id>");
                Console.WriteLine("archsift-inproject-maintenance remove --target <worktree-root>");
                Console.WriteLine("archsift-inproject-maintenance recover --target <worktree-root>");
                return 0;
            }
            var command = args[0];
            var options = Parse(args[1..]);
            if (!options.TryGetValue("--target", out var target))
                throw new ConfigurationException("Maintenance requires --target.");
            var installedDirectory = Path.GetFullPath(Path.Combine(target, ".archsift", "maintenance"));
            if (command != "inspect" &&
                Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory).Equals(
                    Path.TrimEndingDirectorySeparator(installedDirectory),
                    StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationException(
                    "Run the installed archsift-inproject-maintenance.ps1 launcher for payload changes.");
            object result = command switch
            {
                "inspect" when options.Count == 1 => InProjectMaintenance.Inspect(target),
                "upgrade" when options.Count == 3 &&
                    options.TryGetValue("--package", out var package) &&
                    options.TryGetValue("--sha256", out var sha) =>
                    InProjectMaintenance.Upgrade(target, package, sha),
                "rollback" when options.Count == 2 &&
                    options.TryGetValue("--receipt", out var receipt) =>
                    InProjectMaintenance.Rollback(target, receipt),
                "remove" when options.Count == 1 => InProjectMaintenance.Remove(target),
                "recover" when options.Count == 1 => InProjectMaintenance.Recover(target),
                _ => throw new ConfigurationException("Unsupported maintenance command or options.")
            };
            Console.WriteLine(JsonSerializer.Serialize(result, JsonContract.Options));
            return 0;
        }
        catch (Exception error) when (error is ConfigurationException or InvalidDataException or
            JsonException or ArgumentException)
        {
            Console.Error.WriteLine(error.Message);
            return 5;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException)
        {
            Console.Error.WriteLine(error.Message);
            return 3;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        if (args.Length % 2 != 0) throw new ConfigurationException("Expected option/value pairs.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
            if (args[i] is not ("--target" or "--package" or "--sha256" or "--receipt") ||
                string.IsNullOrWhiteSpace(args[i + 1]) || !values.TryAdd(args[i], args[i + 1]))
                throw new ConfigurationException("Invalid maintenance option: " + args[i]);
        return values;
    }
}
