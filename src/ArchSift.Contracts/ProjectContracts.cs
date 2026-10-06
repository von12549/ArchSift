namespace ArchSift.Contracts;

public sealed record ProjectReferenceFact(string Include, string? TargetId, string Status);
public sealed record PackageReferenceFact(string Id, string? Version, string Status);
public sealed record ProjectModel(
    string Id, string Name, string AssemblyName, bool Supported,
    string[] Frameworks, string? FrameworkSource, bool FrameworkComplete,
    ProjectReferenceFact[] References, bool ReferencesComplete,
    PackageReferenceFact[] Packages, bool PackagesComplete, string[] Limitations);
public sealed record ProjectSnapshot(
    ProjectModel[] Projects, AnalysisScope Scope, InputIdentity InputIdentity, string[] SourceFiles,
    string? Entry, string? TargetFramework);
