namespace ArchSift.Contracts;

public sealed record AssemblyInputs(
    string[] Paths, Dictionary<string, string> Sha256, bool SourceBound, bool CoverageComplete,
    string[] Limitations, string? Sdk, string? TargetFramework, string Configuration)
{
    public string? EvidenceOrigin { get; init; }
    public Dictionary<string, string> EvidenceHashes { get; init; } = new(StringComparer.Ordinal);
    public AssemblyAnalysisContext[] Contexts { get; init; } = [];
}
public sealed record AssemblyAnalysisContext(string Id, string SourceProject, string PrimaryPath, string[] Paths)
{
    public string[] FrameworkPaths { get; init; } = [];
}
public sealed record WorkerRequest(AssemblyInputs Inputs, Ruleset[] Rulesets)
{
    public string? ContextWorkspace { get; init; }
}
public sealed record AssemblyEvaluation(RuleResult[] Results, Finding[] Findings, int AssemblyCount, string[] Errors,
    Dictionary<string, string> EngineVersions);
