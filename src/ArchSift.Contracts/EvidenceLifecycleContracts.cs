namespace ArchSift.Contracts;

public sealed record EvidenceRecovery(string JobDirectory, string RecordedStage, string RecoveryState,
    string SourceFreshness, string[] Differences, bool AutomaticRestart, bool CurrentSourceVerified,
    EvidenceJobView Job);
public sealed record EvidenceArchiveRequest(string JobDirectory, string ArchiveDirectory, string[] ReferenceRoots);
public sealed record EvidenceArchiveFile(string RelativePath, string Sha256, long Length);
public sealed record EvidenceArchivePlan(string JobDirectory, string ArchiveDirectory, string PlanSha256,
    EvidenceArchiveFile[] Files, string[] ReferenceRoots, string[] References, string[] Blockers, bool SourceRetained);
public sealed record EvidenceArchiveResult(string Status, string ArchiveDirectory, int FileCount,
    bool OriginalRetained, bool DirectlyPortable, string ReceiptPath);
public sealed record EvidenceCleanupPlan(string JobDirectory, string ArchiveDirectory, string PlanSha256,
    EvidenceArchiveFile[] SelectedOwnedFiles, string[] RetainedUnknownFiles, string[] ReferenceRoots, string[] References, string[] Blockers);
