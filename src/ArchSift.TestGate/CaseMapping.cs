using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Testing;

namespace ArchSift.TestGate;

public sealed record CaseInput(string Path, string Sha256);
public sealed record MappedCase(string ExecutionId, string XunitCaseId, CaseIdentity Identity);
public sealed record CaseMap(int SchemaVersion, string TargetRoot, string Sdk, string Runner,
    string ToolIdentity, string SourceCommit, CaseInput[] IdentityInputs, MappedCase[] Cases);
public sealed record CaseMapRequest(string Discovery, string Report, string TargetRoot, string Sdk,
    string ToolIdentity, string SourceCommit, string[] IdentityInputs);

public static class CaseMapping
{
    public static void CheckExecution(string mappingPath, string discoveryPath, string reportPath)
    {
        foreach (var path in new[] { mappingPath, discoveryPath, reportPath }) PathSafety.EnsureNoLinks(path);
        var map = JsonSerializer.Deserialize<CaseMap>(File.ReadAllBytes(mappingPath), JsonContract.Options)
            ?? throw new InvalidDataException("Case map is missing.");
        Assess(map, MtpCtrf.Parse(File.ReadAllBytes(discoveryPath), File.ReadAllBytes(reportPath)));
    }
    public static object WriteProposal(string requestPath, string outputPath)
    {
        PathSafety.EnsureNoLinks(requestPath);
        var request = JsonSerializer.Deserialize<CaseMapRequest>(File.ReadAllBytes(requestPath), JsonContract.Options)
            ?? throw new ConfigurationException("Case mapping request is empty.");
        PathSafety.EnsureNoLinks(request.Discovery);
        PathSafety.EnsureNoLinks(request.Report);
        var run = MtpCtrf.Parse(File.ReadAllBytes(request.Discovery), File.ReadAllBytes(request.Report));
        var inputs = request.IdentityInputs.Select(path => new CaseInput(path,
            ContentHash.Bytes(File.ReadAllBytes(PathSafety.Under(request.TargetRoot, path))))).ToArray();
        var proposal = Propose(run, request.TargetRoot, request.Sdk, request.ToolIdentity, request.SourceCommit, inputs);
        var output = Path.GetFullPath(outputPath);
        PathSafety.EnsureDisjoint(request.TargetRoot, output);
        var idsPath = output + ".ids.json";
        PathSafety.EnsureNoLinks(idsPath);
        if (File.Exists(output) || File.Exists(idsPath)) throw new ConfigurationException("Case proposal output already exists.");
        Write(output, JsonSerializer.SerializeToUtf8Bytes(proposal, JsonContract.Options));
        Write(idsPath, JsonSerializer.SerializeToUtf8Bytes(proposal.Cases.Select(item => item.ExecutionId).Order(StringComparer.Ordinal), JsonContract.Options));
        return new { status = "proposed-review-required", mapping = output, mappingSha256 = ContentHash.Bytes(File.ReadAllBytes(output)),
            baseline = idsPath, baselineSha256 = ContentHash.Bytes(File.ReadAllBytes(idsPath)) };

        static void Write(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
        }
    }
    // Generation is explicit. A caller must review and pin the resulting bytes separately.
    public static CaseMap Propose(MtpRun run, string root, string sdk, string toolIdentity,
        string sourceCommit, CaseInput[] inputs)
    {
        var cases = Rows(run);
        var map = new CaseMap(1, Path.GetFullPath(root), sdk, "mtp-ctrf/xunit-v3-4.0.1", toolIdentity,
            sourceCommit, inputs, cases);
        Validate(map);
        if (run.DuplicateIds.Length != 0 || run.Tests.Any(item => item.Result != "passed") ||
            !run.AssemblyFinished || !run.DiscoveryFinished)
            throw new InvalidDataException("Cannot propose a mapping from incomplete or failed execution.");
        return map;
    }

    public static CaseMap Load(TestGateConfig config)
    {
        if (config.CaseMapping is null || config.CaseMappingSha256 is null)
            throw new ConfigurationException("An explicitly reviewed case mapping is required.");
        if (!Path.IsPathFullyQualified(config.CaseMapping))
            throw new ConfigurationException("Case mapping path must be absolute.");
        PathSafety.EnsureDisjoint(config.TargetRoot, config.CaseMapping);
        PathSafety.EnsureNoLinks(config.CaseMapping);
        var bytes = File.ReadAllBytes(config.CaseMapping);
        if (ContentHash.Bytes(bytes) != config.CaseMappingSha256)
            throw new ConfigurationException("Case mapping bytes changed.");
        var map = JsonSerializer.Deserialize<CaseMap>(bytes, JsonContract.Options)
            ?? throw new ConfigurationException("Case mapping is empty.");
        Validate(map);
        if (!Path.GetFullPath(map.TargetRoot).Equals(Path.GetFullPath(config.TargetRoot), PathSafety.Comparison) ||
            map.ToolIdentity != config.ToolIdentity || config.RunnerFormat != "mtp-ctrf")
            throw new ConfigurationException("Case mapping target, runner or tool identity changed.");
        foreach (var input in map.IdentityInputs)
            if (ContentHash.Bytes(File.ReadAllBytes(PathSafety.Under(config.TargetRoot, input.Path))) != input.Sha256)
                throw new ConfigurationException("Case identity input changed; review the small case manifest.");
        return map;
    }

    public static void Assess(CaseMap map, MtpRun run)
    {
        Validate(map);
        var actual = map with { Cases = Rows(run) };
        Validate(actual);
        var expected = map.Cases.ToDictionary(item => CaseIdentities.Key(item.Identity), StringComparer.Ordinal);
        if (actual.Cases.Length != expected.Count || actual.Cases.Any(item =>
            !expected.TryGetValue(CaseIdentities.Key(item.Identity), out var row) ||
            row.ExecutionId != item.ExecutionId || row.XunitCaseId != item.XunitCaseId))
            throw new InvalidDataException("Actual C# cases do not match the explicitly approved mapping.");
    }

    private static MappedCase[] Rows(MtpRun run) => run.Tests.Where(item => item.JsonCaseId is null).Select(item =>
    {
        var projection = item.Identity ?? throw new InvalidDataException("Required typed case projection is missing.");
        if (projection.ExecutionId != item.Id) throw new InvalidDataException("Projection execution ID changed.");
        return new MappedCase(item.Id, projection.XunitCaseId, projection.Identity);
    }).OrderBy(item => CaseIdentities.Key(item.Identity), StringComparer.Ordinal).ToArray();

    private static void Validate(CaseMap map)
    {
        if (map.SchemaVersion != 1 || !Path.IsPathFullyQualified(map.TargetRoot) ||
            string.IsNullOrWhiteSpace(map.Sdk) || map.Runner != "mtp-ctrf/xunit-v3-4.0.1" ||
            string.IsNullOrWhiteSpace(map.ToolIdentity) || string.IsNullOrWhiteSpace(map.SourceCommit) ||
            map.IdentityInputs is not { Length: > 0 } || map.Cases is not { Length: > 0 } ||
            map.Cases.Any(item => item.Identity is null || string.IsNullOrWhiteSpace(item.ExecutionId) ||
                string.IsNullOrWhiteSpace(item.XunitCaseId)) ||
            map.Cases.Select(item => item.ExecutionId).Distinct(StringComparer.Ordinal).Count() != map.Cases.Length ||
            map.Cases.Select(item => CaseIdentities.Key(item.Identity)).Distinct(StringComparer.Ordinal).Count() != map.Cases.Length)
            throw new InvalidDataException("Case map is incomplete, colliding or ambiguous.");
    }
}
