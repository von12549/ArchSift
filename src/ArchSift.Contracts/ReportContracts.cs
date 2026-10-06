namespace ArchSift.Contracts;

public sealed record InputFile(string Path, string Sha256, long Length, string Kind);
public sealed record InputIdentity(string Sha256, InputFile[] Files);
public sealed record AnalysisScope(
    string[] IncludedProjects, string[] UnlistedProjects, string[] ExternalReferences,
    string[] UnresolvedReferences, string[] UnsupportedConstructs, string Model = "declared");
public sealed record BuildContext(
    string Mode, string? TargetFramework, string Configuration, string? Sdk, string Binding);
public sealed record Finding(
    string Id, string RuleId, string SubjectKind, string Subject, string Message, string Severity,
    string? Source = null, string? Target = null, string? Location = null,
    string? ExceptionId = null, string? ExceptionReason = null);
public sealed record RuleResult(string RuleId, string Status, int Matched, string[] FindingIds, string[] Limitations);
public sealed record Coverage(int ProjectCount, int AssemblyCount, bool SourceBound);
public sealed record RunMetadata(string RunId, string StartedUtc, long ElapsedMilliseconds, string TargetRoot);

public sealed record AnalysisReport
{
    public int SchemaVersion { get; init; } = 1;
    public required string Operation { get; init; }
    public string ToolVersion { get; init; } = ToolIdentity.Version;
    public required InputIdentity InputIdentity { get; init; }
    public RulesetIdentity[] RulesetIdentities { get; init; } = [];
    public required AnalysisScope Scope { get; init; }
    public ProjectModel[] Projects { get; init; } = [];
    public required BuildContext BuildContext { get; init; }
    public Dictionary<string, string> EngineVersions { get; init; } = new(StringComparer.Ordinal);
    public required string Execution { get; init; }
    public string? Compliance { get; init; }
    public RuleResult[] RuleResults { get; init; } = [];
    public Finding[] Findings { get; init; } = [];
    public required Coverage Coverage { get; init; }
    public string[] Limitations { get; init; } = [];
    public string[] ExecutionErrors { get; init; } = [];
    public required RunMetadata RunMetadata { get; init; }
}
