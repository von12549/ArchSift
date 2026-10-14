namespace ArchSift.Contracts;

public sealed record InProjectTaskState(int SchemaVersion, string Product, string RunId,
    string WorktreeId, string Phase, long Revision, string StartedUtc, string? FinishedUtc,
    string? RequestSha256, string? InputSha256, string? RulesSha256, string? ToolSha256,
    string? ResultPath, int? ExitCode, string? Diagnostic, string Freshness);

public sealed record InProjectTaskOutcome(InProjectTaskState Task, InProjectGateResult? Result,
    bool Joined, bool Conflict);
