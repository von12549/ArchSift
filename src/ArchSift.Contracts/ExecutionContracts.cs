namespace ArchSift.Contracts;

public sealed record ChainRunOptions
{
    public int MaxConcurrency { get; init; } = 1;
}

public sealed record ChainEntryProgress(string EntryId, string State, string? Execution, string? Compliance,
    string? StartedUtc, string? FinishedUtc, bool OutputAvailable, string? Error);

public sealed record ChainProgress(string RunId, long Revision, string Stage, long ElapsedMilliseconds,
    int TotalCount, int EndedCount, int ExecutedCount, int SkippedCount, int RunningCount,
    ChainEntryProgress[] Entries)
{
    public int SchemaVersion { get; init; } = 1;
}

public sealed record ProfileEntry(string ProfileId, string Name, string ConfigPath);
public sealed record ProfileCatalog(int SchemaVersion, string? DefaultProfileId, ProfileEntry[] Profiles);
public sealed record ProfileSelection(int SchemaVersion, string SelectedProfileId);
public sealed record SessionProfile(string Name, string ConfigPath, bool ConfigProtected, bool LibraryProtected);
