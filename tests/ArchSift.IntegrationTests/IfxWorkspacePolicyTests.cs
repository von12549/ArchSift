using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

// Exercise workspace policy without starting the CLI, a host probe, restore or a gate.
// Load the CLI already built by the existing project reference; do not change its public API.
public sealed class IfxWorkspacePolicyTests
{
    private static readonly Lazy<Assembly> Cli = new(() =>
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ArchSift.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var configuration = typeof(IfxWorkspacePolicyTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        return Assembly.LoadFrom(Path.Combine(root.FullName, "src", "ArchSift.Cli", "bin", configuration, "net10.0", "archsift.dll"));
    });

    private static object? Call(string type, string method, params object?[] args)
    {
        var target = Cli.Value.GetType("ArchSift.Workspaces." + type, throwOnError: true)!;
        var member = target.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!;
        try { return member.Invoke(null, args); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitSupportedModesLoadWithoutCreatingState(bool offline)
    {
        using var fixture = new Fixture(offline);
        Assert.NotNull(fixture.Load());
        Assert.False(Directory.Exists(fixture.State));
    }

    [Theory]
    [InlineData("gate", null, "IFX workspace")]
    [InlineData("gate.metadataFile", "other.json", "IFX workspace")]
    [InlineData("runtime.mode", "approvedExact", "no automatic mode fallback")]
    [InlineData("runtime.approvalFile", "../architecture/runtime-approval.json", "null runtime/source approvals")]
    [InlineData("nuget.sourceApprovalFile", "../architecture/package-source.json", "null runtime/source approvals")]
    [InlineData("nuget.configFile", null, "explicit existing NuGet.Config")]
    [InlineData("nuget.configFile", "missing.config", "explicit existing NuGet.Config")]
    public void HostModeCannotDiscardIdentityOrMixExactApprovals(string property, string? value, string message)
    {
        using var fixture = new Fixture(); fixture.Edit(property, value);
        var error = Assert.Throws<ConfigurationException>(() => fixture.Load());
        Assert.Contains(message, error.Message);
        Assert.False(Directory.Exists(fixture.State));
    }

    [Theory]
    [InlineData("runtime.mode", "host", "no automatic mode fallback")]
    [InlineData("runtime.approvalFile", null, "original runtime/source approval paths")]
    [InlineData("nuget.sourceApprovalFile", "other.json", "original runtime/source approval paths")]
    public void OfflineModeRetainsExactApprovalPolicy(string property, string? value, string message)
    {
        using var fixture = new Fixture(offline: true); fixture.Edit(property, value);
        Assert.Contains(message, Assert.Throws<ConfigurationException>(() => fixture.Load()).Message);
    }

    [Fact]
    public void SelectedCacheMustContainTheExactArchiveWithoutFallback()
    {
        using var fixture = new Fixture(); var settings = fixture.Load();
        Directory.CreateDirectory(fixture.Feed);
        File.WriteAllText(Path.Combine(fixture.Feed, "ArchSift.Cli." + RuntimeInfo.ProductVersion + ".nupkg"), "not the selected cache");
        var wrongVersion = Path.Combine(fixture.Root, "cache", "archsift.cli", "0.0.0");
        Directory.CreateDirectory(wrongVersion);
        File.WriteAllText(Path.Combine(wrongVersion, "archsift.cli.0.0.0.nupkg"), "wrong version");
        Assert.Contains("pinned IFX CLI archive is missing", Assert.Throws<ConfigurationException>(() =>
            Call("WorkspaceService", "ResolveIfxArchive", settings, fixture.Feed)).Message);
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.Archive)!);
        File.WriteAllText(fixture.Archive, "archive resolution only");
        Assert.Equal(fixture.Archive, Call("WorkspaceService", "ResolveIfxArchive", settings, null));
        Assert.False(Directory.Exists(fixture.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CacheCannotOverlapTargetOrWorkspaceState(bool useState)
    {
        using var fixture = new Fixture(); var settings = fixture.Load();
        var resolvedState = (string)settings.GetType().GetProperty("StateRoot")!.GetValue(settings)!;
        fixture.Edit("nuget.cacheDirectory", useState ? resolvedState : fixture.Target);
        Assert.Contains("disjoint", Assert.Throws<ConfigurationException>(() => fixture.Load()).Message);
    }

    [Fact]
    public void OfflineArchiveResolutionRequiresFeed()
    {
        using var fixture = new Fixture(offline: true); var settings = fixture.Load();
        Assert.Throws<ConfigurationException>(() => Call("WorkspaceService", "ResolveIfxArchive", settings, null));
        Directory.CreateDirectory(fixture.Feed);
        var archive = Path.Combine(fixture.Feed, "ArchSift.Cli." + RuntimeInfo.ProductVersion + ".nupkg");
        File.WriteAllText(archive, "archive resolution only");
        Assert.Equal(archive, Call("WorkspaceService", "ResolveIfxArchive", settings, fixture.Feed));
    }

    [Fact]
    public void ConfiguredModeDoesNotEmitInvalidDailyGate()
    {
        using var fixture = new Fixture(); var settings = fixture.Load();
        var launch = Path.Combine(fixture.State, "launch");
        Assert.Null(Call("WorkspaceService", "Daily", settings, launch, null));
        Assert.Contains("requires explicit approved offline preparation",
            Assert.IsType<string>(Call("WorkspaceService", "DailyUnavailableReason", settings)));
        Assert.False(Directory.Exists(fixture.State));
    }

    [Fact]
    public void OfflineDailyGateRetainsSchemaTwoAndExplicitFeed()
    {
        using var fixture = new Fixture(offline: true); var settings = fixture.Load();
        var launch = Path.Combine(fixture.Root, "launch"); Directory.CreateDirectory(launch);
        Assert.Throws<ConfigurationException>(() => Call("WorkspaceService", "Daily", settings, launch, null));
        Assert.Empty(Directory.GetFiles(launch));
        var path = Assert.IsType<string>(Call("WorkspaceService", "Daily", settings, launch, fixture.Feed));
        using var config = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(2, config.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(fixture.Feed, config.RootElement.GetProperty("localFeed").GetString());
        Assert.Equal("offline-architecture", config.RootElement.GetProperty("auditMode").GetString());
        Assert.Null(Call("WorkspaceService", "DailyUnavailableReason", settings));
    }

    [Theory]
    [InlineData("matching")]
    [InlineData("altered")]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("traversal")]
    public void ArchivePayloadInventoryAndBytesRemainMandatory(string scenario)
    {
        using var fixture = new Fixture();
        var payload = Path.Combine(fixture.Root, "payload"); Directory.CreateDirectory(payload);
        File.WriteAllText(Path.Combine(payload, "archsift.dll"), scenario == "altered" ? "altered" : "expected");
        if (scenario == "missing") File.Delete(Path.Combine(payload, "archsift.dll"));
        if (scenario == "extra") File.WriteAllText(Path.Combine(payload, "extra.dll"), "unapproved");
        var archive = Path.Combine(fixture.Root, "payload.nupkg");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            void Add(string path) { using var writer = new StreamWriter(zip.CreateEntry(path).Open()); writer.Write("expected"); }
            if (scenario != "empty") Add("tools/net10.0/any/archsift.dll");
            if (scenario == "duplicate") Add("tools/net10.0/any/archsift.dll");
            if (scenario == "traversal") Add("tools/net10.0/any/../escape.dll");
        }
        if (scenario == "matching") Assert.Null(Call("WorkspaceService", "VerifyArchivePayload", archive, payload));
        else if (scenario == "missing") Assert.Throws<FileNotFoundException>(() => Call("WorkspaceService", "VerifyArchivePayload", archive, payload));
        else Assert.Throws<ConfigurationException>(() => Call("WorkspaceService", "VerifyArchivePayload", archive, payload));
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("rollForward")]
    [InlineData("gate")]
    [InlineData("policy")]
    [InlineData("cases")]
    [InlineData("archive")]
    public void HostModeRetainsPinnedIdentityAndApprovalChecks(string scenario)
    {
        using var fixture = new Fixture();
        if (scenario == "manifest") fixture.ManifestEdit("version", "0.0.0");
        if (scenario == "rollForward") fixture.ManifestEdit("rollForward", true);
        if (scenario == "gate") fixture.GateEdit("version", "0.0.0");
        if (scenario == "policy") File.AppendAllText(Path.Combine(fixture.Target, "policy.json"), "changed");
        if (scenario == "cases") File.AppendAllText(Path.Combine(fixture.Target, "cases.json"), "changed");
        if (scenario == "archive")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fixture.Archive)!);
            File.WriteAllText(fixture.Archive, "not approved");
        }
        var message = scenario switch { "manifest" or "rollForward" => "pinned project-root tool manifest",
            "gate" => "approved IFX gate", "archive" => "CLI package archive differs", _ => "policy/case approval differs" };
        Assert.Contains(message, Assert.Throws<ConfigurationException>(() =>
            Call("WorkspaceService", "VerifyIfx", fixture.Load(), null)).Message);
        Assert.False(Directory.Exists(fixture.State));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "archsift-ifx-policy-" + Guid.NewGuid().ToString("N"));
        public string Target => Path.Combine(Root, "target with spaces");
        public string State => Path.Combine(Root, "state");
        public string Feed => Path.Combine(Root, "feed");
        public string Config => Path.Combine(Target, ".archsift", "workspace.json");
        private string Gate => Path.Combine(Target, "architecture", "portable-gate.json");
        private string Manifest => Path.Combine(Target, ".config", "dotnet-tools.json");
        public string Archive => Path.Combine(Root, "cache", "archsift.cli", RuntimeInfo.ProductVersion.ToLowerInvariant(),
            "archsift.cli." + RuntimeInfo.ProductVersion.ToLowerInvariant() + ".nupkg");
        public Fixture(bool offline = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Config)!);
            Directory.CreateDirectory(Path.GetDirectoryName(Gate)!);
            Directory.CreateDirectory(Path.GetDirectoryName(Manifest)!);
            File.WriteAllText(Path.Combine(Target, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            foreach (var name in new[] { "policy.json", "cases.json" }) File.WriteAllText(Path.Combine(Target, name), "{}");
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new { isRoot = true, tools = new Dictionary<string, object>
                { ["archsift.cli"] = new { version = RuntimeInfo.ProductVersion, rollForward = false } } }));
            File.WriteAllText(Gate, JsonSerializer.Serialize(new { schemaVersion = 1, version = RuntimeInfo.ProductVersion,
                policyFile = "policy.json", policySha256 = AssemblyArtifacts.FileHash(Path.Combine(Target, "policy.json")),
                requiredCases = "cases.json", requiredCasesSha256 = AssemblyArtifacts.FileHash(Path.Combine(Target, "cases.json")),
                cliPackageSha256 = new string('0', 64), testProject = "tests/Fixture.csproj", testExecutableName = "Fixture.dll", rulesets = new[] { "policy.json" } }));
            File.WriteAllText(Config, JsonSerializer.Serialize(new WorkspaceConfiguration
            {
                Kind = "archsift-workspace", SchemaVersion = 1, Project = new() { Id = "ifx-test", Root = ".." },
                State = new() { Root = State }, Gate = new() { Kind = "ifx", MetadataFile = "../architecture/portable-gate.json" },
                Runtime = new() { Mode = offline ? "approvedExact" : "host", ApprovalFile = offline ? "../architecture/runtime-approval.json" : null },
                Nuget = new() { Mode = offline ? "offline" : "configured", AllowNetwork = !offline, ConfigFile = "../NuGet.Config",
                    CacheDirectory = Path.Combine(Root, "cache"), SourceApprovalFile = offline ? "../architecture/package-source.json" : null }
            }, JsonContract.Options));
        }
        public object Load() => Call("WorkspaceSettings", "Load", Config, null)!;
        public void Edit(string property, string? value)
        {
            var json = JsonNode.Parse(File.ReadAllText(Config))!; var parts = property.Split('.');
            if (parts.Length == 1) json[property] = value; else json[parts[0]]![parts[1]] = value;
            File.WriteAllText(Config, json.ToJsonString());
        }
        public void GateEdit(string property, string value)
        { var json = JsonNode.Parse(File.ReadAllText(Gate))!; json[property] = value; File.WriteAllText(Gate, json.ToJsonString()); }
        public void ManifestEdit(string property, JsonNode value)
        { var json = JsonNode.Parse(File.ReadAllText(Manifest))!; json["tools"]!["archsift.cli"]![property] = value; File.WriteAllText(Manifest, json.ToJsonString()); }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
