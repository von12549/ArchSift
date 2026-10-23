using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;

namespace ArchSift.IntegrationTests;

public sealed class WorkspaceTests
{
    [Fact]
    public async Task ProjectEntryKeepsNormalDefaultCacheWhenIsolatingSdkHome()
    {
        using var fixture = new WorkspaceFixture();
        Directory.CreateDirectory(Path.Combine(fixture.Target, ".config"));
        File.WriteAllText(Path.Combine(fixture.Target, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
        File.WriteAllText(Path.Combine(fixture.Target, ".config", "dotnet-tools.json"), JsonSerializer.Serialize(new
        {
            version = 1, isRoot = true, tools = new Dictionary<string, object>
            { ["archsift.cli"] = new { version = typeof(WorkspaceConfiguration).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0], commands = new[] { "archsift" }, rollForward = false } }
        }));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ArchSift.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var callerHome = Path.Combine(fixture.Root, "caller-home");
        var start = new ProcessStartInfo("pwsh") { WorkingDirectory = fixture.Root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root.FullName, "templates", "project", ".archsift", "start.ps1"), "-Config", fixture.Config, "-Action", "Doctor", "-NoBrowser" }) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_HOME"] = callerHome; start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["APPDATA"] = Path.Combine(fixture.Root, "appdata"); start.Environment.Remove("NUGET_PACKAGES");
        using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); Assert.NotEqual(0, process.ExitCode); var diagnostic = await output + await error;
            Assert.Contains("Pinned tool is not restored", diagnostic);
            var closeout = Assert.Single(Directory.GetFiles(fixture.State, "closeout.json", SearchOption.AllDirectories));
            using var record = JsonDocument.Parse(File.ReadAllBytes(closeout));
            Assert.Equal(Path.Combine(callerHome, ".nuget", "packages"), record.RootElement.GetProperty("cacheDirectory").GetString());
            Assert.False(Directory.Exists(callerHome)); }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
    }
    [Theory]
    [InlineData("state")]
    [InlineData("runtime")]
    public async Task ProjectEntryAcceptsSingleFieldLocalPathSectionsBeforeHostValidation(string section)
    {
        using var fixture = new WorkspaceFixture();
        var missingHost = Path.Combine(fixture.Root, "missing", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        fixture.Edit(node => node["runtime"]!["dotnetPath"] = missingHost);
        fixture.Edit(node => node["project"]!.AsObject().Remove("root"));
        fixture.Local(section == "state"
            ? (object)new { kind = "archsift-workspace-local", schemaVersion = 1, state = new { root = Path.Combine(fixture.Root, "alternate-state") } }
            : new { kind = "archsift-workspace-local", schemaVersion = 1, runtime = new { dotnetPath = missingHost } });
        Directory.CreateDirectory(Path.Combine(fixture.Target, ".config"));
        File.WriteAllText(Path.Combine(fixture.Target, ".config", "dotnet-tools.json"), JsonSerializer.Serialize(new
        {
            version = 1, isRoot = true, tools = new Dictionary<string, object>
            { ["archsift.cli"] = new { version = typeof(WorkspaceConfiguration).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0], commands = new[] { "archsift" }, rollForward = false } }
        }));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ArchSift.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo("pwsh") { WorkingDirectory = fixture.Root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root.FullName, "templates", "project", ".archsift", "start.ps1"), "-Config", fixture.Config, "-Action", "Doctor", "-NoBrowser" }) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_HOME"] = Path.Combine(fixture.Root, "cli-home");
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); Assert.NotEqual(0, process.ExitCode); Assert.Contains("dotnet candidate is unavailable", await output + await error); }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
        var selectedState = section == "state" ? Path.Combine(fixture.Root, "alternate-state") : fixture.State;
        Assert.Empty(Directory.GetFiles(selectedState, "workspace-state.json", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task DoctorResolvesHostWithoutInitializingState()
    {
        using var fixture = new WorkspaceFixture();
        var result = await fixture.Run("doctor"); Assert.Equal(0, result.Code);
        using var value = JsonDocument.Parse(result.Output);
        Assert.False(value.RootElement.GetProperty("initialized").GetBoolean());
        Assert.False(Directory.Exists(value.RootElement.GetProperty("stateRoot").GetString()));
        Assert.Equal(fixture.Target, value.RootElement.GetProperty("projectRoot").GetString());
        Assert.Equal("ready", value.RootElement.GetProperty("status").GetString());
    }
    [Fact]
    public async Task InitializationIsExternalAndIdempotent()
    {
        using var fixture = new WorkspaceFixture();
        var first = await fixture.Run("init"); Assert.Equal(0, first.Code);
        using var value = JsonDocument.Parse(first.Output);
        var path = value.RootElement.GetProperty("startupConfig").GetString()!; var before = File.ReadAllBytes(path);
        var second = await fixture.Run("init"); Assert.Equal(0, second.Code);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(path.StartsWith(fixture.Target + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        Assert.Equal(new[] { "workspace.json" }, Directory.GetFiles(Path.GetDirectoryName(fixture.Config)!).Select(Path.GetFileName));
        using var startup = JsonDocument.Parse(before);
        Assert.Equal(1, startup.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(fixture.Target, startup.RootElement.GetProperty("target").GetProperty("root").GetString());
        Assert.Equal(0, startup.RootElement.GetProperty("rulesets").GetArrayLength());
    }
    [Fact]
    public async Task LocalHostPathsOverrideDefaultsWithoutBecomingProjectPolicy()
    {
        using var fixture = new WorkspaceFixture();
        var alternate = Path.Combine(fixture.Root, "alternate-state");
        fixture.Local(new { kind = "archsift-workspace-local", schemaVersion = 1, state = new { root = alternate } });
        var result = await fixture.Run("init"); Assert.Equal(0, result.Code);
        using var value = JsonDocument.Parse(result.Output);
        Assert.StartsWith(alternate + Path.DirectorySeparatorChar, value.RootElement.GetProperty("stateRoot").GetString());
    }
    [Theory]
    [InlineData("nuget", "allowNetwork", "true")]
    [InlineData("runtime", "mode", "\"host\"")]
    [InlineData("runtime", "minimumSdkVersion", "\"8.0.0\"")]
    [InlineData("gate", "metadataFile", "\"elsewhere.json\"")]
    public async Task LocalSettingsCannotWeakenPolicy(string section, string key, string json)
    {
        using var fixture = new WorkspaceFixture();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(fixture.Config)!, "workspace.local.json"),
            "{\"kind\":\"archsift-workspace-local\",\"schemaVersion\":1,\"" + section + "\":{\"" + key + "\":" + json + "}}");
        var result = await fixture.Run("init"); Assert.Equal(2, result.Code); Assert.Contains("override", result.Error);
    }
    [Fact]
    public async Task UnknownAndDuplicateFieldsAreRejected()
    {
        using var fixture = new WorkspaceFixture();
        var original = File.ReadAllText(fixture.Config);
        File.WriteAllText(fixture.Config, original.Replace("\"kind\":", "\"unexpected\":1,\"kind\":"));
        Assert.Equal(2, (await fixture.Run("init")).Code);
        File.WriteAllText(fixture.Config, original.Replace("\"schemaVersion\": 1", "\"schemaVersion\":1,\"schemaVersion\":1"));
        var duplicate = await fixture.Run("init"); Assert.Equal(2, duplicate.Code); Assert.Contains("Duplicate", duplicate.Error);
    }
    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("kind", "\"startup-config\"")]
    public async Task WrongWorkspaceSchemaDoesNotCreateState(string property, string value)
    {
        using var fixture = new WorkspaceFixture(); fixture.Edit(node => node[property] = JsonNode.Parse(value));
        Assert.Equal(2, (await fixture.Run("init")).Code);
        Assert.False(Directory.Exists(fixture.State));
    }
    [Fact]
    public async Task AnExplicitMissingHostDoesNotFallBackToPath()
    {
        using var fixture = new WorkspaceFixture(); fixture.Edit(node => node["runtime"]!["dotnetPath"] = Path.Combine(fixture.Root, "missing-dotnet"));
        var result = await fixture.Run("doctor"); Assert.Equal(2, result.Code); Assert.Contains("missing", result.Error);
    }
    [Fact]
    public async Task ExistingRootBindingCannotBeBorrowedByAnotherTarget()
    {
        using var fixture = new WorkspaceFixture(); Assert.Equal(0, (await fixture.Run("init")).Code);
        var other = Path.Combine(fixture.Root, "other-target"); Directory.CreateDirectory(other);
        fixture.Edit(node => node["project"]!["root"] = other);
        var result = await fixture.Run("init"); Assert.Equal(2, result.Code); Assert.Contains("binding", result.Error);
    }
    [Fact]
    public async Task UnknownNonemptyStateIsPreserved()
    {
        using var fixture = new WorkspaceFixture(); var doctor = await fixture.Run("doctor"); Assert.Equal(0, doctor.Code);
        using var value = JsonDocument.Parse(doctor.Output); var state = value.RootElement.GetProperty("stateRoot").GetString()!;
        Directory.CreateDirectory(state); var unknown = Path.Combine(state, "user-owned.txt"); File.WriteAllText(unknown, "retain");
        var result = await fixture.Run("init"); Assert.Equal(2, result.Code); Assert.Equal("retain", File.ReadAllText(unknown));
        Assert.False(File.Exists(Path.Combine(state, "workspace-state.json")));
    }
    [Fact]
    public async Task UserEditedStartupConfigurationIsPreservedUntilConflictingProjectEdit()
    {
        using var fixture = new WorkspaceFixture(); var first = await fixture.Run("init"); Assert.Equal(0, first.Code);
        using var value = JsonDocument.Parse(first.Output); var path = value.RootElement.GetProperty("startupConfig").GetString()!;
        var startup = JsonNode.Parse(File.ReadAllText(path))!; startup["build"]!["configuration"] = "Release"; File.WriteAllText(path, startup.ToJsonString());
        var edited = File.ReadAllBytes(path); Assert.Equal(0, (await fixture.Run("init")).Code); Assert.Equal(edited, File.ReadAllBytes(path));
        fixture.Edit(node => node["workbench"]!["build"]!["configuration"] = "Other");
        Assert.Equal(2, (await fixture.Run("init")).Code); Assert.Equal(edited, File.ReadAllBytes(path));
    }
    [Fact]
    public async Task IfxMetadataCannotBeBypassedByRemovingGateSettings()
    {
        using var fixture = new WorkspaceFixture(); Directory.CreateDirectory(Path.Combine(fixture.Target, "architecture"));
        File.WriteAllText(Path.Combine(fixture.Target, "architecture", "portable-gate.json"), "{}");
        var result = await fixture.Run("init"); Assert.Equal(2, result.Code); Assert.Contains("IFX workspace", result.Error);
    }
    [Fact]
    public async Task OfflineModeRejectsNetworkSourcesBeforeInitializingState()
    {
        using var fixture = new WorkspaceFixture(); var nuget = Path.Combine(fixture.Root, "NuGet.Config");
        File.WriteAllText(nuget, "<configuration><packageSources><clear/><add key=\"remote\" value=\"https://invalid.example/v3/index.json\"/></packageSources></configuration>");
        fixture.Edit(node => { node["nuget"]!["mode"] = "offline"; node["nuget"]!["allowNetwork"] = false; node["nuget"]!["configFile"] = nuget; });
        var result = await fixture.Run("init"); Assert.Equal(2, result.Code); Assert.Contains("Offline mode", result.Error); Assert.False(Directory.Exists(fixture.State));
    }

    [Fact]
    public async Task DuplicateOptionsAndExplicitMissingLocalFileCannotFallBack()
    {
        using var fixture = new WorkspaceFixture();
        Assert.Equal(2, (await fixture.Run("doctor", "--config", fixture.Config)).Code);
        var missing = Path.Combine(fixture.Root, "missing", "workspace.local.json");
        Assert.Equal(2, (await fixture.Run("doctor", "--local", missing)).Code);
        Assert.False(Directory.Exists(fixture.State));
    }
    [Fact]
    public async Task BusyWorkspaceRejectsInitializationAndDoctorExplainsIt()
    {
        using var fixture = new WorkspaceFixture(); var initial = await fixture.Run("init"); Assert.Equal(0, initial.Code);
        using var value = JsonDocument.Parse(initial.Output); var state = value.RootElement.GetProperty("stateRoot").GetString()!;
        var marker = File.ReadAllBytes(Path.Combine(state, "workspace-state.json"));
        using var lease = new FileStream(Path.Combine(state, ".workspace.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var rejected = await fixture.Run("init"); Assert.Equal(2, rejected.Code); Assert.Contains("busy", rejected.Error);
        var doctor = await fixture.Run("doctor"); Assert.Equal(0, doctor.Code);
        using var diagnostic = JsonDocument.Parse(doctor.Output); Assert.Equal("busy", diagnostic.RootElement.GetProperty("status").GetString());
        Assert.Equal(marker, File.ReadAllBytes(Path.Combine(state, "workspace-state.json")));
    }
    [Fact]
    public async Task ReviewedImportIsHashBoundIdempotentAndRejectsDestinationAliases()
    {
        using var fixture = new WorkspaceFixture(); var source = Path.Combine(fixture.Root, "original.txt"); File.WriteAllText(source, "retain-original");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();
        var migration = Path.Combine(fixture.Root, "migration.json");
        void Write(string[] paths) => File.WriteAllText(migration, JsonSerializer.Serialize(new { kind = "archsift-workspace-migration", schemaVersion = 1,
            targetRoot = fixture.Target, files = paths.Select(destination => new { source, destination, sha256 = hash }).ToArray() }));
        Write(["library/original.txt"]);
        var first = await fixture.Run("init", "--migration", migration); Assert.Equal(0, first.Code);
        using var value = JsonDocument.Parse(first.Output); var state = value.RootElement.GetProperty("stateRoot").GetString()!;
        Assert.Equal("retain-original", File.ReadAllText(Path.Combine(state, "library", "original.txt")));
        Assert.Equal(0, (await fixture.Run("init", "--migration", migration)).Code);
        Write(["library/another.txt", "library//another.txt"]);
        Assert.Equal(2, (await fixture.Run("init", "--migration", migration)).Code);
        Assert.False(File.Exists(Path.Combine(state, "library", "another.txt")));
        Assert.Equal("retain-original", File.ReadAllText(source));
    }
    [Fact]
    public async Task RecordedInterruptedInitializationResumesOnlyWithMatchingBytes()
    {
        using var fixture = new WorkspaceFixture(); var initial = await fixture.Run("init"); Assert.Equal(0, initial.Code);
        using var value = JsonDocument.Parse(initial.Output); var state = value.RootElement.GetProperty("stateRoot").GetString()!;
        var config = value.RootElement.GetProperty("startupConfig").GetString()!; var bytes = File.ReadAllBytes(config);
        var marker = Path.Combine(state, "workspace-state.json"); File.Copy(marker, Path.Combine(state, "initialization.json")); File.Delete(marker);
        foreach (var launch in Directory.GetDirectories(Path.Combine(state, "launches")))
        { File.Delete(Path.Combine(launch, "initialization.json")); Directory.Delete(launch); }
        Assert.Equal(0, (await fixture.Run("init")).Code); Assert.Equal(bytes, File.ReadAllBytes(config));
        Assert.True(File.Exists(marker)); Assert.False(File.Exists(Path.Combine(state, "initialization.json")));
    }
    [Fact]
    public async Task InterruptedBeforeJournalResumesOnlyAnEmptyLease()
    {
        using var fixture = new WorkspaceFixture(); var doctor = await fixture.Run("doctor"); Assert.Equal(0, doctor.Code);
        using var value = JsonDocument.Parse(doctor.Output); var state = value.RootElement.GetProperty("stateRoot").GetString()!;
        Directory.CreateDirectory(state); var lease = Path.Combine(state, ".workspace.lock"); File.WriteAllText(lease, "user-owned");
        Assert.Equal(2, (await fixture.Run("init")).Code); Assert.Equal("user-owned", File.ReadAllText(lease));
        File.WriteAllBytes(lease, []);
        Assert.Equal(0, (await fixture.Run("doctor")).Code);
        Assert.False(File.Exists(Path.Combine(state, "workspace-state.json")));
        Assert.Equal(0, (await fixture.Run("init")).Code);
        Assert.True(File.Exists(Path.Combine(state, "workspace-state.json")));
    }
    [Fact]
    public async Task FutureMarkerAndHostOnlyChangesPreserveUserConfiguration()
    {
        using var fixture = new WorkspaceFixture(); var initial = await fixture.Run("init"); Assert.Equal(0, initial.Code);
        using var value = JsonDocument.Parse(initial.Output); var state = value.RootElement.GetProperty("stateRoot").GetString()!;
        var config = value.RootElement.GetProperty("startupConfig").GetString()!;
        var edited = JsonNode.Parse(File.ReadAllText(config))!; edited["build"]!["configuration"] = "Release"; File.WriteAllText(config, edited.ToJsonString());
        var bytes = File.ReadAllBytes(config);
        fixture.Local(new { kind = "archsift-workspace-local", schemaVersion = 1, nuget = new { cacheDirectory = Path.Combine(fixture.Root, "different-cache") } });
        Assert.Equal(0, (await fixture.Run("init")).Code); Assert.Equal(bytes, File.ReadAllBytes(config));
        var marker = Path.Combine(state, "workspace-state.json"); var future = JsonNode.Parse(File.ReadAllText(marker))!; future["schemaVersion"] = 2; File.WriteAllText(marker, future.ToJsonString());
        var original = File.ReadAllBytes(marker); Assert.Equal(2, (await fixture.Run("init")).Code); Assert.Equal(original, File.ReadAllBytes(marker));
    }

    private sealed class WorkspaceFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "archsift-workspace-tests-" + Guid.NewGuid().ToString("N"));
        public string Target => Path.Combine(Root, "target with spaces");
        public string State => Path.Combine(Root, "external-state");
        public string Config => Path.Combine(Target, ".archsift", "workspace.json");
        public WorkspaceFixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Config)!);
            File.WriteAllText(Config, JsonSerializer.Serialize(new WorkspaceConfiguration { Kind = "archsift-workspace", SchemaVersion = 1,
                Project = new() { Id = "test-project", Root = ".." }, State = new() { Root = State },
                Nuget = new() { Mode = "configured", AllowNetwork = true }, Runtime = new() { DotnetPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") } }, JsonContract.Options));
        }
        public void Edit(Action<JsonNode> edit) { var node = JsonNode.Parse(File.ReadAllText(Config))!; edit(node); File.WriteAllText(Config, node.ToJsonString(JsonContract.Options)); }
        public void Local(object value) => File.WriteAllText(Path.Combine(Path.GetDirectoryName(Config)!, "workspace.local.json"), JsonSerializer.Serialize(value));
        public async Task<(int Code, string Output, string Error)> Run(string action, params string[] additional)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "ArchSift.slnx"))) root = root.Parent;
            Assert.NotNull(root);
            var configuration = typeof(WorkspaceTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet") { WorkingDirectory = Root,
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root.FullName, "src", "ArchSift.Cli", "bin", configuration, "net10.0", "archsift.dll"));
            foreach (var arg in new[] { "workspace", action, "--config", Config }) start.ArgumentList.Add(arg);
            foreach (var arg in additional) start.ArgumentList.Add(arg);
            start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Root, "cli-home"); start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
            start.Environment["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false";
            if (!start.Environment.ContainsKey("ARCHSIFT_PROFILE_PATHS")) start.Environment["ARCHSIFT_PROFILE_PATHS"] =
                JsonSerializer.Serialize(Enumerable.Range(0, 4).Select(i => Path.Combine(Root, "profiles", i + ".ps1")).ToArray());
            using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); return (process.ExitCode, await output, await error); }
            finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
