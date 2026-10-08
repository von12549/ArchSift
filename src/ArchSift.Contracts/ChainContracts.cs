namespace ArchSift.Contracts;

public sealed record LibraryEntry(string EntryId, string FileName, string RulesetId, bool Deleted);
public sealed record LibraryRegistry(int SchemaVersion, LibraryEntry[] Entries);
public sealed record LibraryCard(string EntryId, string FileName, string RulesetId, bool Available,
    RulesetIdentity? Identity, Ruleset? Ruleset, string? Error);
public sealed record ChainEntry(string EntryId);
public sealed record ChainDocument(int SchemaVersion, string Id, string Version, string Description, ChainEntry[] Entries);
public sealed record ChainEntrySnapshot(string EntryId, string? SourcePath, RulesetIdentity? RulesetIdentity,
    string? ErrorCode, string? ErrorMessage);
public sealed record ChainSnapshot(string ChainId, string ChainVersion, ChainEntrySnapshot[] Entries,
    TargetSettings Target, BuildSettings Build, InputIdentity InputIdentity, InputIdentity BuildInputIdentity,
    string OutputDirectory, string TargetKind);
public sealed record ChainDiagnostic(string EntryId, string Code, string Message)
{
    public int SchemaVersion { get; init; } = 1;
    public string Kind { get; init; } = "chain-entry-diagnostic";
}
public sealed record ChainChild(string EntryId, string Execution, string Compliance, int? ExitCode,
    string? ReportDirectory, ChainDiagnostic? Diagnostic, Coverage Coverage, string Binding,
    RuleResult[] RuleResults, Finding[] Findings, string[] Limitations);
public sealed record ChainSummary(ChainSnapshot Snapshot, string Execution, string Compliance, int ExitCode,
    int ProjectCount, ChainChild[] Entries, string[] Limitations, RunMetadata RunMetadata)
{
    public int SchemaVersion { get; init; } = 1;
    public string Kind { get; init; } = "chain-summary";
    public string ToolVersion { get; init; } = ToolIdentity.Version;
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, AnalysisReport> AnalysisReports { get; init; } = new Dictionary<string, AnalysisReport>();
}
