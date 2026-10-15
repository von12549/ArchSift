namespace ArchSift.Contracts;

public sealed record InProjectEvidenceFile(string Path, long Bytes, string Sha256);
public sealed record InProjectEvidenceClosure(string OriginalPath, string LogicalPath, string Sha256);
public sealed record InProjectEvidenceManifest(int SchemaVersion, string Product,
    string ProductVersion, string[] RulesetVersions, string RunId,
    string WorktreeId, string InputSha256, string RulesSha256, string ToolSha256,
    InProjectEvidenceFile[] Files);

public sealed record InProjectEvidencePrunePreview(string RunId, string BundlePath,
    string ArchivePath, string ArchiveSha256, long Bytes, int Files,
    string[] ProtectedBy, bool CanDelete);
