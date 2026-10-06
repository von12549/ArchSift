namespace ArchSift.Contracts;

public sealed record ChangeRequest(string? Base = null, string? Head = null);
public sealed record SnapshotIdentity(string Kind, string? Commit, InputIdentity Inputs, string[] ExcludedFiles);
public sealed record FileChange(string Path, string Kind, string? PreviousPath = null);
public sealed record PolicyChange(string RuleId, string Kind);
public sealed record ComparisonReport
{
    public int SchemaVersion { get; init; } = 1;
    public string Operation { get; init; } = "changes";
    public string ToolVersion { get; init; } = ToolIdentity.Version;
    public required string Status { get; init; }
    public required SnapshotIdentity BaselineIdentity { get; init; }
    public required SnapshotIdentity TargetIdentity { get; init; }
    public AnalysisReport? Baseline { get; init; }
    public AnalysisReport? Target { get; init; }
    public Finding[] Added { get; init; } = [];
    public Finding[] Existing { get; init; } = [];
    public Finding[] Resolved { get; init; } = [];
    public Finding[] Unclassified { get; init; } = [];
    public FileChange[] FileChanges { get; init; } = [];
    public PolicyChange[] PolicyChanges { get; init; } = [];
    public string[] Limitations { get; init; } = [];
    public string[] ExecutionErrors { get; init; } = [];
    public required RunMetadata RunMetadata { get; init; }
}
