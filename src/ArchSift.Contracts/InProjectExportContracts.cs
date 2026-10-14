namespace ArchSift.Contracts;

public sealed record InProjectExportSource(string Kind, string SourcePath, string LogicalPrefix,
    int Files, long Bytes);

public sealed record InProjectExportPreview(int SchemaVersion, string Product, string WorktreeId,
    InProjectExportSource[] Sources, string[] Limitations);

public sealed record InProjectExportManifest(int SchemaVersion, string Product, string WorktreeId,
    string CreatedUtc, InProjectEvidenceFile[] Files);
