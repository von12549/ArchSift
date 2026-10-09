using ArchSift.Contracts;

namespace ArchSift.Setup;

public sealed record PackageFile(string Path, long Bytes, string Sha256);
public sealed record PackageManifest(int SchemaVersion, string Version, string Rid, bool SelfContained,
    string AcceptanceStatus, string SourceCommit, bool SourceDirty, bool ProductSourceDirty,
    string ProductInputsSha256, PackageFile[] Entries, bool ManifestSelfHashOmitted);
public sealed record PackageIdentity(string Path, string Sha256, string Version, string ManifestSha256, string SourceCommit);
public sealed record OwnedVersion(string Version, string Directory, string ManifestSha256, string PackageSha256);
public sealed record Installation(int SchemaVersion, string Root, string TargetRoot, string ConfigPath, string LibraryPath,
    string SelectedVersion, OwnedVersion[] Versions, string LastOperation);
public sealed record StateFile(string Scope, string Path, string Sha256);
public sealed record StateIdentity(bool ConfigExists, bool LibraryExists, StateFile[] Files, string Sha256);
public sealed record SetupPlan(int SchemaVersion, string PlanId, string Operation, DateTimeOffset ExpiresUtc,
    string Root, PackageIdentity? Package, OwnedVersion? AdoptedVersion, string ConfigPath, RunConfiguration Config,
    string? SelectionSha256, StateIdentity State, bool Smoke);
public sealed record Receipt(int SchemaVersion, string Id, string Operation, string PlanId, Installation? Before,
    Installation After, StateIdentity BeforeState, StateIdentity AfterState, string BackupDirectory, string HostSha256,
    bool HostStateEqual, string Outcome);
public sealed record Journal(int SchemaVersion, string Id, string Phase, SetupPlan Plan, Installation? Before,
    Installation After, StateIdentity BeforeState, StateIdentity? AfterState, string HostSha256);
public sealed record SetupCompatibility(int SchemaVersion, int ConfigSchemaVersion, int LibrarySchemaVersion, int ChainSchemaVersion);
