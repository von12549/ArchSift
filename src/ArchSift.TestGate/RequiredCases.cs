using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Testing;

namespace ArchSift.TestGate;

public sealed record RequiredCase(string Group, CaseIdentity Identity);
public sealed record RequiredCasesDocument(int SchemaVersion, string Kind, string Sdk, string Runner,
    string TargetFramework, string PackageVersion, RequiredCase[] Cases);

/// <summary>Approved logical identities are read only. Runner identifiers belong to this execution only.</summary>
public static class RequiredCases
{
    public static RequiredCasesDocument Load(TestGateConfig config)
    {
        var path = PathSafety.Under(config.TargetRoot, config.RequiredCases!);
        var bytes = File.ReadAllBytes(path);
        if (ContentHash.Bytes(bytes) != config.RequiredCasesSha256)
            throw new ConfigurationException("Required-case manifest bytes changed.");
        var manifest = JsonSerializer.Deserialize<RequiredCasesDocument>(bytes, JsonContract.Options)
            ?? throw new ConfigurationException("Required-case manifest is missing.");
        Validate(manifest);
        if (manifest.TargetFramework != config.TargetFramework || manifest.PackageVersion != config.ToolIdentity ||
            config.RunnerFormat != "mtp-ctrf")
            throw new ConfigurationException("Required-case compatibility contract differs.");
        return manifest;
    }

    public static void Validate(RequiredCasesDocument manifest)
    {
        if (manifest.SchemaVersion != 1 || manifest.Kind != "archsift-required-cases" ||
            manifest.Sdk != "10.0.303" || manifest.Runner != "mtp-ctrf/xunit-v3-4.0.1" ||
            manifest.TargetFramework != "net10.0" || string.IsNullOrWhiteSpace(manifest.PackageVersion) ||
            manifest.Cases is not { Length: > 0 } || manifest.Cases.Any(item =>
                item is null || string.IsNullOrWhiteSpace(item.Group) || item.Identity is null))
            throw new InvalidDataException("Unsupported or incomplete required-case manifest.");
        foreach (var item in manifest.Cases)
            _ = CaseIdentities.Read(CaseIdentities.Marker + CaseIdentities.Serialize(new("validation", "validation", item.Identity)));
        if (manifest.Cases.Select(item => CaseIdentities.Key(item.Identity)).Distinct(StringComparer.Ordinal).Count() != manifest.Cases.Length)
            throw new InvalidDataException("Duplicate required logical identity.");
    }

    public static GateCoverage Assess(RequiredCasesDocument manifest, MtpRun run,
        IReadOnlyDictionary<string, string> jsonCases, int exitCode, bool subset = false)
    {
        Validate(manifest);
        var expected = manifest.Cases.Select(item => CaseIdentities.Key(item.Identity)).ToArray();
        var reasons = new List<string>();
        var xunitIds = new HashSet<string>(StringComparer.Ordinal);
        var actualKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var test in run.Tests.Where(item => item.JsonCaseId is null))
        {
            var projection = test.Identity;
            if (projection is null || projection.ExecutionId != test.Id ||
                projection.Identity.Type != test.ClassName || projection.Identity.Method != test.MethodName)
            { reasons.Add("Required typed case projection is missing or mismatched."); continue; }
            if (!xunitIds.Add(projection.XunitCaseId) || !actualKeys.Add(CaseIdentities.Key(projection.Identity)))
                reasons.Add("Duplicate or ambiguous current-run logical association.");
        }
        if (!actualKeys.SetEquals(expected)) reasons.Add("Actual logical cases differ from the approved manifest.");
        // Preserve runner IDs in the actual report; use logical keys only for coverage comparison.
        var logicalRun = run with { Tests = run.Tests.Select(item => item.JsonCaseId is null
            ? item with { Id = item.Identity is null ? "missing-projection:" + item.Id : CaseIdentities.Key(item.Identity.Identity) }
            : item).ToArray() };
        var coverage = MtpEvents.Assess(logicalRun, expected, jsonCases, exitCode, subset);
        return reasons.Count == 0 ? coverage : coverage with { Complete = false, ExitCode = 4,
            Reasons = coverage.Reasons.Concat(reasons).Distinct(StringComparer.Ordinal).ToArray() };
    }
}
