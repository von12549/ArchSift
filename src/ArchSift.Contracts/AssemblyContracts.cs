namespace ArchSift.Contracts;

public sealed record AssemblyEntry(string AssemblyName, string SourceProject, string Path, string Sha256);
public sealed record AssemblyManifest
{
    public int SchemaVersion { get; init; } = 1;
    public required InputIdentity InputIdentity { get; init; }
    public required InputIdentity BuildInputIdentity { get; init; }
    public required string Sdk { get; init; }
    public required string TargetFramework { get; init; }
    public required string Configuration { get; init; }
    public required string Generation { get; init; }
    public required AssemblyEntry[] Assemblies { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ReviewedBuildLink? ReviewedBuild { get; init; }
}

public sealed record ReviewedBuildLink(string CapturePath, string CaptureSha256, string ReviewPath, string ReviewSha256);
public sealed record EvidenceCapture(int SchemaVersion, string TargetRoot, string? Entry, string TargetFramework,
    string Configuration, InputIdentity InputIdentity, InputIdentity BuildInputIdentity,
    InputFile[] ExternalInputs, Dictionary<string, string> EnvironmentHashes, string[] PropertyNames);
public sealed record EvidenceMaterial(string Path, string Sha256, long Length, string Role);
public sealed record ReviewedProject(string SourceProject, string AssemblyName, string TargetFramework,
    Dictionary<string, string> EvaluatedProperties);
public sealed record ReviewedAssembly(string SourceProject, string Path);
public sealed record ExternalBuildReview
{
    public int SchemaVersion { get; init; } = 1;
    public string Origin { get; init; } = "external-reviewed";
    public required string CaptureSha256 { get; init; }
    public bool Approved { get; init; }
    public bool InputsComplete { get; init; }
    public required string Reviewer { get; init; }
    public required string Sdk { get; init; }
    public required string SdkToolPath { get; init; }
    public required string TargetFramework { get; init; }
    public required string Configuration { get; init; }
    public string[] Command { get; init; } = [];
    public int ExitCode { get; init; }
    public Dictionary<string, string> GlobalProperties { get; init; } = new(StringComparer.Ordinal);
    public EvidenceMaterial[] Materials { get; init; } = [];
    public ReviewedProject[] Projects { get; init; } = [];
    public ReviewedAssembly[] Assemblies { get; init; } = [];
    public string[] FrameworkPaths { get; init; } = [];
    public string[] Imports { get; init; } = [];
    public string[] GeneratorsAndTasks { get; init; } = [];
    public string[] Unknowns { get; init; } = ["Review the actual build input trace before approval."];
}
