namespace ArchSift.Contracts;

public sealed record EvidenceJobRequest(EvidenceStorageRequest Storage, string DotnetPath, string LocalFeed,
    string? SdkDirectory, string[] ExternalInputRoots, string[] EnvironmentInputNames,
    bool ReviewedBuildLogic, string ExecutionReviewer, string ExecutionReason, int TimeoutSeconds = 1800);
public sealed record EvidenceCommand(string Stage, string Executable, string[] Arguments, string WorkingDirectory);
public sealed record EvidenceJobPreview(EvidenceStoragePlan Storage, string TargetRoot, string? Entry,
    string SourceIdentity, int ProjectCount, string[] Projects, string ToolSha256, string TargetFramework,
    string Configuration, string[] Risks, EvidenceCommand[] Commands, string PreviewSha256);
public sealed record EvidenceExecution(int Attempt, string Stage, string StartedUtc, string FinishedUtc,
    int? ProcessId, int? ExitCode, string[] Arguments, string StdoutPath, string StderrPath,
    bool Cancelled, bool TimedOut, bool OwnedTreeStopped, bool HostStateEqual);
public sealed record EvidenceJobRecord(int SchemaVersion, string Kind, string Id, string Stage,
    string StartedUtc, string? FinishedUtc, long ElapsedMilliseconds, int CompletedProjects,
    EvidenceJobRequest Request, RunConfiguration Configuration, EvidenceJobPreview Preview,
    EvidenceExecution[] Executions, string? CapturePath, string? ReviewTemplatePath,
    string[] Unknowns, string? Error, string HostBaselineSha256, string ToolVersion)
{
    public int EvaluatedProjects { get; init; }
}
public sealed record EvidenceJobView(string Id, string Stage, string StartedUtc, string? FinishedUtc,
    long ElapsedMilliseconds, int ProjectCount, int CompletedProjects, string RunDirectory,
    string? CapturePath, string? ReviewTemplatePath, string[] Unknowns, string? Error,
    EvidenceExecution[] Executions)
{
    public int EvaluatedProjects { get; init; }
}
