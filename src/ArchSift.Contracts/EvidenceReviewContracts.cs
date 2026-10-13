namespace ArchSift.Contracts;

public sealed record EvidenceReviewRequest(string JobDirectory, string ReviewScopeSha256, string Reviewer,
    string ReviewerKind, string AuthorizationReference, string Reason, string[] GeneratedInputRelationships,
    string ImportTaskAssessment, string[] Boundaries, bool Approve);
public sealed record EvidenceReviewScope(string JobDirectory, string Stage, string ScopeSha256,
    string CaptureSha256, string TemplateSha256, int ProjectCount, int MaterialCount,
    Dictionary<string, int> MaterialRoles, string[] Unknowns, string[] RequiredReview);
public sealed record EvidenceReviewResult(string Stage, string JobDirectory, string? ReviewPath,
    string? ManifestPath, EvidenceValidationResult? Validation, string? ConfigurationCopyPath,
    string[] Limitations, string? Error);
public sealed record EvidenceMaterialPage(EvidenceMaterial[] Items, int Offset, int Limit, int Total);
public sealed record EvidenceMaterialPreview(string Path, string Role, string Sha256, bool Truncated, string? Text, string? Diagnostic);
