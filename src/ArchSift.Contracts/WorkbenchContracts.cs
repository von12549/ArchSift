namespace ArchSift.Contracts;

// Runtime projections. Saved report/chain schemas remain authoritative and unchanged.
public sealed record RuleStatusCounts(int Pass, int Violation, int Inconclusive, int Error, int NotApplicable);
public sealed record ChainAggregate(int PlannedEntries, int ReportedEntries, int EndedEntries,
    int CompletedEntries, int PartialEntries, int FailedEntries, int CancelledEntries, int SkippedEntries,
    int MissingEntries, int DiagnosticCount, int RecordedRules, RuleStatusCounts StatusCounts,
    int FindingCount, int ProjectCount, bool SourceBound, int BoundEntries, int? AssemblyContextCount,
    string Execution, string Compliance, int ExitCode);
public sealed record HistoryQuery(int Offset = 0, int Limit = 25, string? Operation = null, string? State = null);
public sealed record HistoryItem(string Id, string? RunId, string Operation, string State, int? ExitCode,
    string? StartedUtc, long? ElapsedMilliseconds, string TargetRoot, string? Selection,
    string? ArtifactPath, string? ArtifactSha256, string SourceDirectory, string ToolVersion,
    string Freshness, string? Diagnostic);
public sealed record HistoryPage(HistoryItem[] Items, int Offset, int Limit, int Total);
public sealed record HistoryDirectoryRegistration(string Directory, string TargetRoot);
public sealed record HistoryCatalog(int SchemaVersion, HistoryDirectoryRegistration[] Directories);
public sealed record HistoryJobRecord(int SchemaVersion, string Kind, string Id, string Operation,
    string State, int? ExitCode, string? Error, RunConfiguration Context, string TargetKind,
    string? Selection, string CreatedUtc, string StartedUtc, string? FinishedUtc,
    long ElapsedMilliseconds, string ToolVersion, string? ArtifactPath, EvidenceValidationResult? EvidenceValidation = null);
public sealed record HistoryView(string Id, string Operation, string State, int? ExitCode, string? Error,
    RunConfiguration? Context, string TargetKind, string? Selection, AnalysisReport? Report,
    ComparisonReport? Comparison, ChainSummary? Chain, ChainAggregate? Aggregate, HistoryItem History, EvidenceValidationResult? EvidenceValidation = null);
public sealed record EvidenceStorageRequest(string? DataRoot, string? EvidenceRoot,
    string ProjectId, string RunId, string[] ProtectedRoots);
public sealed record EvidenceStoragePlan(string EvidenceRoot, string ProjectId, string RunId,
    string RunDirectory, string ManifestPath);
public sealed record EvidenceValidationResult(string Status, string StartedUtc, long ElapsedMilliseconds,
    bool SourceBound, int OwnerCount, int ContextCount, bool CoverageComplete,
    string? EvidenceOrigin, string? ValidationInputsSha256, string[] Limitations);
public sealed record EvidenceDetails(string ManifestPath, string ManifestSha256, int SchemaVersion,
    string Freshness, string Sdk, string TargetFramework, string Configuration, string Generation,
    string SourceInputSha256, string BuildInputSha256, AssemblyEntry[] Owners,
    ReviewedAssemblyContext[] Contexts, string? CapturePath, string? ReviewPath, string? Reviewer,
    bool? ReviewApproved, string[] Unknowns, Dictionary<string, int> MaterialCounts, int ExternalInputCount);
