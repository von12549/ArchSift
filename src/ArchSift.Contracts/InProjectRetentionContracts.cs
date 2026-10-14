namespace ArchSift.Contracts;

public sealed record InProjectRetentionItem(string RunId, string EvidenceGrade, string Phase,
    string? FinishedUtc, string MaterialsState, string[] ProtectedBy,
    bool SelectedForCompaction, string? SelectionReason);

public sealed record InProjectRetentionPreview(int SchemaVersion, string Product, int DailyDays,
    int DailyMaxRuns, string GeneratedUtc, int AvailableDailyRuns, int ProtectedDailyRuns,
    int SelectedRuns, bool OverBudget, InProjectRetentionItem[] Items, string[] Limitations);
