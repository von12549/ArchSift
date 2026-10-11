using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class ConfigLoader
{
    public static RunConfiguration Load(string path)
    {
        var full = Path.GetFullPath(path);
        if (new FileInfo(full).Length > RuleLoader.MaximumJsonBytes) throw new ConfigurationException("Config exceeds 4 MiB.");
        var bytes = File.ReadAllBytes(full);
        if (bytes.Length > RuleLoader.MaximumJsonBytes) throw new ConfigurationException("Config exceeds 4 MiB.");
        try
        {
            var offset = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
            using var doc = JsonDocument.Parse(bytes.AsMemory(offset));
            SchemaValidation.Validate(doc.RootElement, "config");
            var config = doc.RootElement.Deserialize<RunConfiguration>(JsonContract.Options)!;
            return ResolvePaths(config, Path.GetDirectoryName(full)!);
        }
        catch (JsonException e) { throw new ConfigurationException("Invalid config JSON: " + e.Message); }
    }

    public static RunConfiguration ResolvePaths(RunConfiguration config, string directory)
    {
            string Resolve(string value) => Path.GetFullPath(value, directory);
            if (config.Build.AllowNetwork && config.Build.Sources.Length == 0)
                throw new ConfigurationException("Network restore requires explicit sources.");
            if (!config.Build.AllowNetwork && config.Build.Sources.Length != 0)
                throw new ConfigurationException("Network sources require allowNetwork=true.");
            return config with
            {
                Target = config.Target with { Root = Resolve(config.Target.Root), Entry = config.Target.Entry },
                Rulesets = config.Rulesets.Select(Resolve).ToArray(),
                Build = config.Build with
                {
                    AssemblyManifest = config.Build.AssemblyManifest is { } m ? Resolve(m) : null,
                    AssemblyPaths = config.Build.AssemblyPaths.Select(Resolve).ToArray(),
                    LocalFeed = config.Build.LocalFeed is { } f ? Resolve(f) : null,
                    CacheDirectory = config.Build.CacheDirectory is { } c ? Resolve(c) : null
                },
                Output = config.Output with { Directory = Resolve(config.Output.Directory) },
                RulesDirectory = config.RulesDirectory is { } r ? Resolve(r) : null
            };
    }
}
