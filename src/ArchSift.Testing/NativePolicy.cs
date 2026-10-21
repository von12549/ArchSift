using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Testing;

public sealed record PolicyRuleset(string Path, string Sha256);
public sealed record PolicySource(string SourceId, string SourceSha256, string Responsibility,
    string[] CaseKeys);
public sealed record NativePolicyDocument(int SchemaVersion, string Version, PolicyRuleset[] Rulesets,
    PolicySource[] Sources);

public static class NativePolicy
{
    public static string FindRoot(string startDirectory, string solutionName)
    {
        if (Path.GetFileName(solutionName) != solutionName) throw new ConfigurationException("Invalid solution marker.");
        for (var cursor = Path.GetFullPath(startDirectory); cursor is not null; cursor = Path.GetDirectoryName(cursor))
        {
            PathSafety.EnsureNoLinks(cursor);
            if (File.Exists(Path.Combine(cursor, solutionName))) return cursor;
        }
        throw new ConfigurationException("Native policy target root was not found.");
    }

    public static (NativePolicyDocument Document, string[] Paths, string Sha256) Load(string targetRoot,
        string policyRelativePath, string? gateConfigPath = null)
    {
        var root = Path.GetFullPath(targetRoot);
        var path = PathSafety.Under(root, policyRelativePath);
        var bytes = File.ReadAllBytes(path);
        var policy = JsonSerializer.Deserialize<NativePolicyDocument>(bytes, JsonContract.Options)
            ?? throw new ConfigurationException("Native policy is missing.");
        if (policy.SchemaVersion != 1 || string.IsNullOrWhiteSpace(policy.Version) ||
            policy.Rulesets is not { Length: > 0 } || policy.Sources is null ||
            policy.Sources.Any(item => string.IsNullOrWhiteSpace(item.SourceId) ||
                item.SourceSha256 is not { Length: 64 } ||
                item.Responsibility is not ("ifx-csharp" or "json" or "deferred") || item.CaseKeys is null) ||
            policy.Sources.Select(item => item.SourceId).Distinct(StringComparer.Ordinal).Count() != policy.Sources.Length)
            throw new ConfigurationException("Unsupported or empty native policy.");
        var paths = policy.Rulesets.Select(item => PathSafety.Under(root, item.Path)).ToArray();
        if (paths.Distinct(PathSafetyComparer).Count() != paths.Length)
            throw new ConfigurationException("Duplicate native ruleset paths.");
        foreach (var (item, index) in policy.Rulesets.Select((item, index) => (item, index)))
            if (ContentHash.Bytes(File.ReadAllBytes(paths[index])) != item.Sha256)
                throw new ConfigurationException("Native ruleset hash changed.");
        _ = ArchitectureTestAdapter.LoadCases(paths);
        var sha = ContentHash.Bytes(bytes);
        if (gateConfigPath is not null)
        {
            PathSafety.EnsureNoLinks(gateConfigPath);
            using var gate = JsonDocument.Parse(File.ReadAllBytes(gateConfigPath));
            var config = gate.RootElement;
            var expectedRoot = Path.GetFullPath(config.GetProperty("targetRoot").GetString()!);
            var expectedPolicy = config.GetProperty("policySha256").GetString();
            var overrides = config.GetProperty("rulesets").EnumerateArray().Select(item =>
                Path.GetFullPath(item.GetString()!, root)).ToArray();
            if (!expectedRoot.Equals(root, PathSafety.Comparison) || expectedPolicy != sha ||
                !paths.ToHashSet(PathSafetyComparer).SetEquals(overrides))
                throw new ConfigurationException("Gate override target or approved policy does not match native policy.");
        }
        return (policy, paths, sha);
    }

    private static StringComparer PathSafetyComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
