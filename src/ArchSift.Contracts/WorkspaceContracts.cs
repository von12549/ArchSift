namespace ArchSift.Contracts;

public sealed record WorkspaceConfiguration
{
    public required string Kind { get; init; }
    public required int SchemaVersion { get; init; }
    public required WorkspaceProject Project { get; init; }
    public WorkspaceState State { get; init; } = new();
    public WorkspaceNuGet Nuget { get; init; } = new();
    public WorkspaceRuntime Runtime { get; init; } = new();
    public WorkspaceWorkbench Workbench { get; init; } = new();
    public WorkspaceGate? Gate { get; init; }
}
public sealed record WorkspaceProject
{
    public required string Id { get; init; }
    public string Root { get; init; } = "..";
    public string? Entry { get; init; }
}
public sealed record WorkspaceState { public string Root { get; init; } = "C:/ArchSift-lab"; }
public sealed record WorkspaceNuGet
{
    public string Mode { get; init; } = "offline";
    public bool AllowNetwork { get; init; }
    public string? ConfigFile { get; init; }
    public string? CacheDirectory { get; init; }
    public string? SourceApprovalFile { get; init; }
}
public sealed record WorkspaceRuntime
{
    public string Mode { get; init; } = "host";
    public string? DotnetPath { get; init; }
    public string MinimumSdkVersion { get; init; } = "10.0.0";
    public bool AllowPrerelease { get; init; }
    public string? ApprovalFile { get; init; }
}
public sealed record WorkspaceWorkbench
{
    public string[] Rulesets { get; init; } = [];
    public BuildSettings Build { get; init; } = new();
}
public sealed record WorkspaceGate { public required string Kind { get; init; } public required string MetadataFile { get; init; } }
