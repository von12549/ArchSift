namespace ArchSift.Contracts;

public sealed record AssemblyInputs(
    string[] Paths, Dictionary<string, string> Sha256, bool SourceBound, bool CoverageComplete,
    string[] Limitations, string? Sdk, string? TargetFramework, string Configuration);
public sealed record WorkerRequest(AssemblyInputs Inputs, Ruleset[] Rulesets);
public sealed record AssemblyEvaluation(RuleResult[] Results, Finding[] Findings, int AssemblyCount, string[] Errors,
    Dictionary<string, string> EngineVersions);
