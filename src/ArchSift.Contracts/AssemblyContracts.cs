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
}
