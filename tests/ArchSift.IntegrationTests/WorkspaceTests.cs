using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;

namespace ArchSift.IntegrationTests;

public sealed class WorkspaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectEntryKeepsNormalDefaultCacheWhenIsolatingSdkHome(bool explicitConfiguration)
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
        if (explicitConfiguration)
        {
            fixture.Edit(node => node["nuget"]!["configFile"] = Path.Combine(fixture.Target, "NuGet.Config"));
            var userDirectory = Path.Combine(fixture.Root, "appdata", "NuGet"); Directory.CreateDirectory(userDirectory);
            File.WriteAllText(Path.Combine(userDirectory, "NuGet.Config"), "<configuration><config><add key=\"globalPackagesFolder\" value=\"" +
                System.Security.SecurityElement.Escape(Path.Combine(fixture.Root, "inherited-cache")) + "\"/></config></configuration>");
        }
        var start = new ProcessStartInfo("pwsh") { WorkingDirectory = fixture.Root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root.FullName, "templates", "project", ".archsift", "start.ps1"), "-Config", fixture.Config, "-Action", "Doctor", "-NoBrowser" }) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_HOME"] = callerHome; start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.Environment["APPDATA"] = Path.Combine(fixture.Root, "appdata"); start.Environment.Remove("NUGET_PACKAGES");
        using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); Assert.NotEqual(0, process.ExitCode); var diagnostic = await output + await error;
            Assert.Contains("Pinned tool is not restored", diagnostic);
            Assert.Contains(Path.Combine(callerHome, ".nuget", "packages"), diagnostic);
            Assert.False(Directory.Exists(fixture.State));
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
        Assert.False(Directory.Exists(selectedState));
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
        Assert.False(value.RootElement.GetProperty("hostProbed").GetBoolean());
    }
    [Fact]
    public async Task DoctorDoesNotExecuteTheConfiguredHost()
    {
        using var fixture = new WorkspaceFixture();
        var fakeHost = Path.Combine(fixture.Root, "unexecutable-host.exe");
        File.WriteAllText(fakeHost, "This file must never be executed by Doctor.");
        fixture.Edit(node => node["runtime"]!["dotnetPath"] = fakeHost);
        var result = await fixture.Run("doctor");
        Assert.Equal(0, result.Code);
        Assert.False(Directory.Exists(fixture.State));
    }
    [Fact]
    public async Task DoctorResolvesPerUserDefaultStateWithoutCreatingIt()
    {
        using var fixture = new WorkspaceFixture();
        fixture.Edit(node => node["state"]!.AsObject().Remove("root"));
        var result = await fixture.Run("doctor"); Assert.Equal(0, result.Code);
        using var value = JsonDocument.Parse(result.Output);
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArchSift", "state");
        Assert.StartsWith(root + Path.DirectorySeparatorChar, value.RootElement.GetProperty("stateRoot").GetString());
        Assert.False(value.RootElement.GetProperty("initialized").GetBoolean());
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
    [Fact]
    public async Task ProjectPolicyLoadsTrackedRulesAndRejectsChangedBytes()
    {
        using var fixture = new WorkspaceFixture();
        var rules = Path.Combine(fixture.Target, "project-rules.json");
        var source = new DirectoryInfo(AppContext.BaseDirectory);
        while (source is not null && !File.Exists(Path.Combine(source.FullName, "ArchSift.slnx"))) source = source.Parent;
        Assert.NotNull(source);
        File.Copy(Path.Combine(source.FullName, "tests", "ArchSift.Testing.Tests", "rules.json"), rules);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(rules))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(fixture.Target, "policy.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, version = "project-v1", rulesets = new[] { new { path = "project-rules.json", sha256 = hash } },
            sources = Array.Empty<object>()
        }));
        fixture.Edit(node => node["workbench"]!["policyFile"] = "../policy.json");
        var first = await fixture.Run("init"); Assert.Equal(0, first.Code);
        using var result = JsonDocument.Parse(first.Output);
        using var startup = JsonDocument.Parse(File.ReadAllBytes(result.RootElement.GetProperty("startupConfig").GetString()!));
        Assert.Equal(rules, Assert.Single(startup.RootElement.GetProperty("rulesets").EnumerateArray()).GetString());
        File.AppendAllText(rules, " ");
        var changed = await fixture.Run("init"); Assert.Equal(2, changed.Code);
        Assert.Contains("hash changed", changed.Error);
    }
    [Fact]
    public async Task ProjectPolicyAndExplicitRuleListCannotBeCombined()
    {
        using var fixture = new WorkspaceFixture();
        fixture.Edit(node => { node["workbench"]!["policyFile"] = "../policy.json";
            node["workbench"]!["rulesets"] = new JsonArray("../rules.json"); });
        var result = await fixture.Run("init");
        Assert.Equal(2, result.Code);
        Assert.Contains("not both", result.Error);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":1,\"rulesets\":[{\"path\":\"missing.json\"}]}")]
    public async Task ProjectPolicyShapeErrorsAreConfigurationFailures(string json)
    {
        using var fixture = new WorkspaceFixture();
        File.WriteAllText(Path.Combine(fixture.Target, "policy.json"), json);
        fixture.Edit(node => node["workbench"]!["policyFile"] = "../policy.json");
        var result = await fixture.Run("doctor");
        Assert.Equal(2, result.Code);
        Assert.Contains("policy", result.Error);
        Assert.False(Directory.Exists(fixture.State));
    }
    [Fact]
    public async Task ProjectPolicyCannotBeBorrowedFromOutsideTheProject()
    {
        using var fixture = new WorkspaceFixture();
        File.WriteAllText(Path.Combine(fixture.Root, "external-policy.json"), "{}");
        fixture.Edit(node => node["workbench"]!["policyFile"] = "../../external-policy.json");
        var result = await fixture.Run("doctor");
        Assert.Equal(2, result.Code);
        Assert.Contains("inside the project", result.Error);
    }
    [Fact]
    public async Task RuntimeDirectoryCannotBeUsedAsThePackageCache()
    {
        using var fixture = new WorkspaceFixture();
        var runtimeRoot = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
        fixture.Edit(node => { node["runtime"]!["dotnetPath"] = Path.Combine(runtimeRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            node["nuget"]!["cacheDirectory"] = runtimeRoot; });
        var result = await fixture.Run("init");
        Assert.Equal(2, result.Code);
        Assert.Contains("disjoint", result.Error);
        Assert.False(Directory.Exists(fixture.State));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedConfigurationRefreshRecoversOnlyForTheBoundInputs(bool changeInputs)
    {
        using var fixture = new WorkspaceFixture();
        var first = await fixture.Run("init"); Assert.Equal(0, first.Code);
        using var initial = JsonDocument.Parse(first.Output);
        var state = initial.RootElement.GetProperty("stateRoot").GetString()!;
        var config = initial.RootElement.GetProperty("startupConfig").GetString()!;
        var marker = Path.Combine(state, "workspace-state.json"); var oldMarker = File.ReadAllBytes(marker);
        using var previous = JsonDocument.Parse(oldMarker);
        var retained = Path.Combine(state, "reports", "retained.txt"); File.WriteAllText(retained, "preserve-history");
        fixture.Edit(node => node["workbench"]!["build"]!["configuration"] = "Release");
        Assert.Equal(0, (await fixture.Run("init")).Code);
        var expectedConfig = File.ReadAllBytes(config);
        using var updated = JsonDocument.Parse(File.ReadAllBytes(marker));
        var journal = Path.Combine(state, "configuration-refresh.json");
        File.WriteAllText(journal, JsonSerializer.Serialize(new { schemaVersion = 1, kind = "archsift-workspace-refresh",
            projectId = "test-project", targetRoot = fixture.Target, stateRoot = state,
            configurationIdentity = updated.RootElement.GetProperty("configurationIdentity").GetString(),
            previousConfigurationSha256 = previous.RootElement.GetProperty("generatedConfigurationSha256").GetString(),
            expectedConfigurationSha256 = updated.RootElement.GetProperty("generatedConfigurationSha256").GetString() }, JsonContract.Options));
        File.WriteAllBytes(marker, oldMarker);
        if (changeInputs) fixture.Edit(node => node["workbench"]!["build"]!["configuration"] = "Debug");
        var recovered = await fixture.Run("init");
        Assert.Equal(changeInputs ? 2 : 0, recovered.Code);
        Assert.Equal(expectedConfig, File.ReadAllBytes(config));
        Assert.Equal("preserve-history", File.ReadAllText(retained));
        Assert.Equal(changeInputs, File.Exists(journal));
        if (changeInputs) Assert.Equal(oldMarker, File.ReadAllBytes(marker));
    }
    [Fact]
    public async Task WorkspaceSdkRangeBoundsSelectedHost()
    {
        using var fixture = new WorkspaceFixture();
        fixture.Edit(node => { node["runtime"]!["minimumSdkVersion"] = "10.0.300";
            node["runtime"]!["maximumSdkVersionExclusive"] = "10.1.0"; });
        var selected = await fixture.Run("doctor");
        Assert.Equal(0, selected.Code);
        fixture.Edit(node => node["runtime"]!["maximumSdkVersionExclusive"] = "10.0.300");
        var invalid = await fixture.Run("doctor");
        Assert.Equal(2, invalid.Code);
        Assert.Contains("maximum", invalid.Error);
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

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"kind\":\"untrusted-marker\"}")]
    public async Task InterruptedMarkerTemporaryIsPreservedAndNeverPromoted(string contents)
    {
        using var fixture = new WorkspaceFixture();
        var snapshot = await PrepareMarkerInterruption(fixture);
        var temporary = Path.Combine(snapshot.State, "workspace-state.json.1ebd2303d047483cb0bfa2b97d1342d9.tmp");
        File.WriteAllText(temporary, contents); var retained = File.ReadAllBytes(temporary);
        var config = File.ReadAllBytes(snapshot.Config);
        Assert.Equal(0, (await fixture.Run("init")).Code);
        Assert.Equal(retained, File.ReadAllBytes(temporary));
        Assert.Equal(snapshot.Marker, File.ReadAllBytes(Path.Combine(snapshot.State, "workspace-state.json")));
        Assert.Equal(config, File.ReadAllBytes(snapshot.Config));
        Assert.False(File.Exists(Path.Combine(snapshot.State, "initialization.json")));
        Assert.Equal(0, (await fixture.Run("doctor")).Code);
        Assert.Equal(0, (await fixture.Run("init")).Code);
        Assert.Equal(retained, File.ReadAllBytes(temporary));
    }

    [Theory]
    [InlineData("workspace-state.json.not-a-guid.tmp")]
    [InlineData("workspace-state.json.1EBD2303D047483CB0BFA2B97D1342D9.tmp")]
    [InlineData("workspace-state.json.1ebd2303d047483cb0bfa2b97d1342d9.tmp.extra")]
    [InlineData("nested/workspace-state.json.1ebd2303d047483cb0bfa2b97d1342d9.tmp")]
    [InlineData("operator-notes.txt")]
    public async Task InterruptedMarkerLookalikesAndUnknownFilesRemainRejected(string relative)
    {
        using var fixture = new WorkspaceFixture();
        var snapshot = await PrepareMarkerInterruption(fixture);
        var unknown = Path.Combine(snapshot.State, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(unknown)!); File.WriteAllText(unknown, "retain");
        var journal = File.ReadAllBytes(Path.Combine(snapshot.State, "initialization.json"));
        Assert.Equal(2, (await fixture.Run("init")).Code);
        Assert.Equal("retain", File.ReadAllText(unknown));
        Assert.Equal(journal, File.ReadAllBytes(Path.Combine(snapshot.State, "initialization.json")));
        Assert.False(File.Exists(Path.Combine(snapshot.State, "workspace-state.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservedTemporaryDoesNotBypassTransactionIdentityOrConfigHash(bool changeConfig)
    {
        using var fixture = new WorkspaceFixture();
        var snapshot = await PrepareMarkerInterruption(fixture);
        var temporary = Path.Combine(snapshot.State, "workspace-state.json.1ebd2303d047483cb0bfa2b97d1342d9.tmp");
        File.WriteAllText(temporary, "retained-partial");
        var journalPath = Path.Combine(snapshot.State, "initialization.json");
        if (changeConfig) File.AppendAllText(snapshot.Config, "changed");
        else { var journal = JsonNode.Parse(File.ReadAllText(journalPath))!; journal["projectId"] = "other"; File.WriteAllText(journalPath, journal.ToJsonString()); }
        var beforeJournal = File.ReadAllBytes(journalPath); var beforeConfig = File.ReadAllBytes(snapshot.Config);
        Assert.Equal(2, (await fixture.Run("init")).Code);
        Assert.Equal(beforeJournal, File.ReadAllBytes(journalPath)); Assert.Equal(beforeConfig, File.ReadAllBytes(snapshot.Config));
        Assert.Equal("retained-partial", File.ReadAllText(temporary));
        Assert.False(File.Exists(Path.Combine(snapshot.State, "workspace-state.json")));
    }

    [Fact]
    public async Task MultipleReservedMarkerTemporariesRemainByteExact()
    {
        using var fixture = new WorkspaceFixture(); var snapshot = await PrepareMarkerInterruption(fixture);
        var files = new Dictionary<string, byte[]>();
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[] { 0, 255, 123 }, snapshot.Marker })
        {
            var path = Path.Combine(snapshot.State, "workspace-state.json." + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllBytes(path, bytes); files.Add(path, bytes);
        }
        Assert.Equal(0, (await fixture.Run("init")).Code);
        foreach (var file in files) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        Assert.Equal(snapshot.Marker, File.ReadAllBytes(Path.Combine(snapshot.State, "workspace-state.json")));
    }

    [Fact]
    public async Task ReservedMarkerTemporaryReparsePointIsRejectedWithoutTouchingItsTarget()
    {
        using var fixture = new WorkspaceFixture(); var snapshot = await PrepareMarkerInterruption(fixture);
        var outside = Path.Combine(fixture.Root, "outside.txt"); File.WriteAllText(outside, "retain-external");
        var link = Path.Combine(snapshot.State, "workspace-state.json." + Guid.NewGuid().ToString("N") + ".tmp");
        if (OperatingSystem.IsWindows())
        {
            var directory = Path.Combine(fixture.Root, "junction-target"); Directory.CreateDirectory(directory);
            var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "New-Item -ItemType Junction -Path '" + link.Replace("'", "''") + "' -Target '" + directory.Replace("'", "''") + "' | Out-Null" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
        }
        else File.CreateSymbolicLink(link, outside);
        try
        {
            var journalPath = Path.Combine(snapshot.State, "initialization.json"); var journal = File.ReadAllBytes(journalPath);
            Assert.Equal(2, (await fixture.Run("init")).Code);
            Assert.Equal("retain-external", File.ReadAllText(outside));
            Assert.Equal(journal, File.ReadAllBytes(journalPath));
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            Assert.False(File.Exists(Path.Combine(snapshot.State, "workspace-state.json")));
        }
        finally { if (OperatingSystem.IsWindows()) Directory.Delete(link); else File.Delete(link); }
    }
    private static async Task<(string State, string Config, byte[] Marker)> PrepareMarkerInterruption(WorkspaceFixture fixture)
    {
        var initial = await fixture.Run("init"); Assert.Equal(0, initial.Code);
        using var value = JsonDocument.Parse(initial.Output);
        var state = value.RootElement.GetProperty("stateRoot").GetString()!;
        var config = value.RootElement.GetProperty("startupConfig").GetString()!;
        var marker = Path.Combine(state, "workspace-state.json"); var bytes = File.ReadAllBytes(marker);
        File.Copy(marker, Path.Combine(state, "initialization.json")); File.Delete(marker);
        foreach (var launch in Directory.GetDirectories(Path.Combine(state, "launches")))
        { File.Delete(Path.Combine(launch, "initialization.json")); Directory.Delete(launch); }
        return (state, config, bytes);
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
