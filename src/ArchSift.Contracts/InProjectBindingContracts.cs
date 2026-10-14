namespace ArchSift.Contracts;

public sealed record InProjectBindingMaterial(string Path, string Kind, string Sha256);
public sealed record InProjectBindingReference(string Path, string AssemblyIdentity, string Sha256, string Role);
public sealed record InProjectBindingRestore(string Status, string[] Sources, string AssetsSha256);

public sealed record InProjectBindingContext(
    string Project, string TargetFramework, string Configuration,
    string GlobalPropertiesSha256, string CompilerCommandSha256, string CompilerPath,
    InProjectBindingMaterial[] Materials, InProjectBindingReference[] References,
    string OutputPath, string OutputSha256);

public sealed record InProjectDailyBinding(
    int SchemaVersion, string Product, string RunId, string WorktreeId,
    string Solution, string Configuration, string InputSha256, string RulesSha256,
    string ToolSha256, string SdkVersion, InProjectBindingRestore Restore,
    InProjectBindingContext[] Contexts, string StartInputSha256, string EndInputSha256);

public sealed record InProjectBindingVerdict(int SchemaVersion, string Product, string RunId,
    string CaptureSha256, string VerifierVersion, bool SourceBound, string[] Limitations);
