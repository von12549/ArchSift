using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Setup;
using ArchSift.Web;

namespace ArchSift.IntegrationTests;

public sealed class LauncherTests
{
    [Fact]
    public void PathsIdentityProtectionAndSelectionKeepSetupStateIndependent()
    {
        using var fixture = new Fixture(); var store = new ProfileStore(fixture.Install);
        var setupHash = SetupFiles.FileHash(SetupService.Selection(fixture.Install));
        var stateHash = SetupFiles.Capture(fixture.ConfigPath, fixture.Config.RulesDirectory!).Sha256;
        Assert.False(File.Exists(store.CatalogPath)); Assert.Empty(store.ReadCatalog().Profiles);
        var first = store.Register("default.json", fixture.ConfigPath, null);
        Assert.True(store.Inspect(first).Session!.ConfigProtected);
        var otherPath = Path.Combine(fixture.Root, "default.json"); File.Copy(fixture.ConfigPath, otherPath);
        var other = store.Register("default.json", otherPath, store.CatalogHash);
        Assert.NotEqual(first.ProfileId, other.ProfileId);
        Assert.False(store.Inspect(other).Session!.ConfigProtected); Assert.True(store.Inspect(other).Session!.LibraryProtected);
        Assert.Throws<ConfigurationException>(() => store.Register("duplicate", otherPath, store.CatalogHash));
        Assert.Throws<ConfigurationException>(() => store.Register("stale", Path.Combine(fixture.Root, "missing.json"), null));
        store.Select(other.ProfileId, null);
        Assert.Equal(other.ProfileId, store.ReadSelection()!.SelectedProfileId);
        Assert.Throws<ConfigurationException>(() => store.Select(first.ProfileId, null));
        Assert.Equal(setupHash, SetupFiles.FileHash(SetupService.Selection(fixture.Install)));
        Assert.Equal(stateHash, SetupFiles.Capture(fixture.ConfigPath, fixture.Config.RulesDirectory!).Sha256);
        File.Delete(otherPath); Assert.Equal("missing", store.Inspect(other).Status);
        File.WriteAllText(otherPath, "{}"); Assert.Equal("invalid", store.Inspect(other).Status);
        File.WriteAllText(otherPath, "{\"schemaVersion\":2}"); Assert.Equal("incompatible", store.Inspect(other).Status);
        File.WriteAllText(store.SelectionPath, "{\"schemaVersion\":2,\"selectedProfileId\":\"future\"}");
        var corruptHash = SetupFiles.FileHash(store.SelectionPath);
        Assert.Throws<ConfigurationException>(() => store.ReadSelection());
        Assert.Throws<ConfigurationException>(() => store.Select(first.ProfileId, corruptHash));
        Assert.Equal(corruptHash, SetupFiles.FileHash(store.SelectionPath));
    }

    [Fact]
    public void CreateConflictsBrokenManagedConfigAndMetadataNeverRepairOrTouchTarget()
    {
        using var fixture = new Fixture(); var store = new ProfileStore(fixture.Install);
        var target = InputCapture.Capture(fixture.Target).Sha256;
        Assert.Throws<ConfigurationException>(() => store.Register("outside", Path.Combine(fixture.Root, "new.json"), null, fixture.Config));
        Assert.Throws<ConfigurationException>(() => store.Register("managed", fixture.ConfigPath, null, fixture.Config));
        Assert.Throws<ConfigurationException>(() => store.Register("metadata", store.CatalogPath, null, fixture.Config));
        Assert.Throws<ConfigurationException>(() => store.Preview(Path.Combine(fixture.Install, "config", "CON.json"), fixture.Config));
        var path = Path.Combine(fixture.Install, "config", "custom.json");
        var relative = fixture.Config with { Target = fixture.Config.Target with { Root = "../../target" },
            Output = fixture.Config.Output with { Directory = "../reports" }, RulesDirectory = "../rules" };
        var preview = store.Preview(path, relative);
        Assert.Equal(fixture.Target, preview.Target.Root); Assert.False(File.Exists(path));
        var entry = store.Register("custom", path, null, relative);
        Assert.False(store.Inspect(entry).Session!.ConfigProtected);
        Assert.Throws<ConfigurationException>(() => store.Register("overwrite", path, store.CatalogHash, fixture.Config));
        File.WriteAllText(fixture.ConfigPath, "{}");
        Assert.NotNull(SetupService.LoadInstallation(fixture.Install));
        using var inspect = JsonDocument.Parse(JsonSerializer.Serialize(new SetupService().Inspect(fixture.Install), JsonContract.Options));
        Assert.Equal("state-invalid", inspect.RootElement.GetProperty("status").GetString());
        Assert.Equal("valid", store.Inspect(entry).Status);
        Assert.Equal(target, InputCapture.Capture(fixture.Target).Sha256);
        File.WriteAllText(store.CatalogPath, "{\"schemaVersion\":2,\"defaultProfileId\":null,\"profiles\":[]}");
        var hash = SetupFiles.FileHash(store.CatalogPath);
        Assert.Throws<ConfigurationException>(() => store.ReadCatalog()); Assert.Equal(hash, SetupFiles.FileHash(store.CatalogPath));
    }

    [Fact]
    public async Task NativeLauncherWithMissingManagedConfigOwnsSwitchingAndParentExitWithoutLeakingTokens()
    {
        if (!OperatingSystem.IsWindows()) return; // The delivered launcher platform is Windows x64.
        using var fixture = new Fixture(); var targetHash = InputCapture.Capture(fixture.Target).Sha256;
        File.Delete(fixture.ConfigPath);
        var external = Path.Combine(fixture.Root, "external.json"); SetupFiles.Atomic(external, fixture.Config);
        using var parent = Process.Start(fixture.Start())!;
        var stderr = parent.StandardError.ReadToEndAsync();
        Uri? address = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try
        {
            while (await parent.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
                if (line.StartsWith("ARCHSIFT_UI=", StringComparison.Ordinal)) { address = new(line[12..]); break; }
            Assert.NotNull(address);
            var stdout = parent.StandardOutput.ReadToEndAsync();
            using var client = new HttpClient { BaseAddress = new(address.GetLeftPart(UriPartial.Authority)) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/launcher/state", timeout.Token)).StatusCode);
            client.DefaultRequestHeaders.Add("X-ArchSift-Token", address.Fragment[9..]);
            client.DefaultRequestHeaders.Add("Origin", "https://evil.invalid");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/launcher/state", timeout.Token)).StatusCode); client.DefaultRequestHeaders.Remove("Origin");
            Assert.Contains("Configuration profiles", await client.GetStringAsync("/", timeout.Token));
            async Task<JsonDocument> State() => JsonDocument.Parse(await client.GetStringAsync("/api/launcher/state", timeout.Token));
            using (var state = await State()) Assert.Equal("missing", state.RootElement.GetProperty("managedCandidate").GetProperty("status").GetString());
            Assert.Throws<IOException>(() => { using var probe = new FileStream(SetupFiles.Under(fixture.Install, ".archsift-setup.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
            var registration = await client.PostAsJsonAsync("/api/launcher/register", new { name = "external", configPath = external, catalogSha256 = (string?)null }, timeout.Token);
            Assert.True(registration.IsSuccessStatusCode);
            var entry = await registration.Content.ReadFromJsonAsync<ProfileEntry>(JsonContract.Options, timeout.Token); Assert.NotNull(entry);
            async Task<Uri> Start()
            {
                using var state = await State();
                var response = await client.PostAsJsonAsync("/api/launcher/start", new { profileId = entry.ProfileId,
                    catalogSha256 = state.RootElement.GetProperty("catalogSha256").GetString(), configSha256 = SetupFiles.FileHash(external),
                    selectionSha256 = state.RootElement.GetProperty("selectionSha256").GetString() }, timeout.Token);
                Assert.True(response.IsSuccessStatusCode);
                using var ready = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                return new(ready.RootElement.GetProperty("workbenchUrl").GetString()!);
            }
            var workbenchAddress = await Start();
            using var workbench = new HttpClient { BaseAddress = new(workbenchAddress.GetLeftPart(UriPartial.Authority)) };
            workbench.DefaultRequestHeaders.Add("X-ArchSift-Token", workbenchAddress.Fragment[9..]);
            using (var config = JsonDocument.Parse(await workbench.GetStringAsync("/api/config", timeout.Token)))
            {
                Assert.Equal(external, config.RootElement.GetProperty("profile").GetProperty("configPath").GetString());
                Assert.False(config.RootElement.GetProperty("profile").GetProperty("configProtected").GetBoolean());
            }
            var activeRejected = await client.PostAsJsonAsync("/api/launcher/start", new { profileId = entry.ProfileId }, timeout.Token);
            Assert.Equal(HttpStatusCode.BadRequest, activeRejected.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await workbench.GetAsync("/api/session", timeout.Token)).StatusCode); // Rejection must not cancel the active child.
            await workbench.PostAsJsonAsync("/api/session/dirty", new { dirty = true }, timeout.Token);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/launcher/stop", new { discardChanges = false }, timeout.Token)).StatusCode);
            Assert.True((await client.PostAsJsonAsync("/api/launcher/stop", new { discardChanges = true }, timeout.Token)).IsSuccessStatusCode);
            var next = await Start(); Assert.NotEqual(workbenchAddress.Fragment, next.Fragment);
            using var nextClient = new HttpClient { BaseAddress = new(next.GetLeftPart(UriPartial.Authority)) };
            nextClient.DefaultRequestHeaders.Add("X-ArchSift-Token", next.Fragment[9..]);
            using var session = JsonDocument.Parse(await nextClient.GetStringAsync("/api/session", timeout.Token));
            var childPid = session.RootElement.GetProperty("processId").GetInt32();
            parent.StandardInput.Close(); await parent.WaitForExitAsync(timeout.Token);
            Assert.DoesNotContain(System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners(), endpoint => endpoint.Port == next.Port);
            try { using var child = Process.GetProcessById(childPid); Assert.True(child.HasExited); }
            catch (ArgumentException) { } // No such PID proves the owned child was reaped.
            var retained = File.ReadAllText(Path.Combine(fixture.Install, "config", "profiles.json")) + File.ReadAllText(Path.Combine(fixture.Install, "config", "selection.json"));
            Assert.DoesNotContain(workbenchAddress.Fragment[9..], retained); Assert.DoesNotContain(address.Fragment[9..], retained);
            Assert.DoesNotContain(workbenchAddress.Fragment[9..], await stdout); Assert.DoesNotContain(workbenchAddress.Fragment[9..], await stderr);
            Assert.Equal(targetHash, InputCapture.Capture(fixture.Target).Sha256);
            Assert.False(File.Exists(fixture.ConfigPath));
        }
        finally { if (!parent.HasExited) { parent.Kill(true); await parent.WaitForExitAsync(); } }
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "archsift-launcher-test-" + Guid.NewGuid().ToString("N"));
        public string Target => Path.Combine(Root, "target");
        public string Install => Path.Combine(Root, "install");
        public string ConfigPath => Path.Combine(Install, "config", "default.json");
        private string WebDirectory => Path.Combine(Install, "versions", "0.6.0", "web");
        public RunConfiguration Config => new() { SchemaVersion = 1, Target = new() { Root = Target, Entry = "Project.csproj" },
            Build = new() { TargetFramework = "net10.0" }, RulesDirectory = Path.Combine(Install, "rules"),
            Output = new() { Directory = Path.Combine(Install, "reports") } };
        public Fixture()
        {
            Directory.CreateDirectory(Target); File.WriteAllText(Path.Combine(Target, "Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "ArchSift.slnx"))) repo = repo.Parent;
            var configuration = typeof(LauncherTests).Assembly.GetCustomAttributes(false).OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
            var source = Path.Combine(repo!.FullName, "src", "ArchSift.Web", "bin", configuration, "net10.0");
            Directory.CreateDirectory(WebDirectory);
            // Use actual ArchSift bytes; the fixture manifest is synthetic, not release/self-contained qualification.
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var path = Path.Combine(WebDirectory, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.Copy(file, path);
            }
            var version = Path.GetDirectoryName(WebDirectory)!;
            File.WriteAllText(Path.Combine(version, "archsift.exe"), "Synthetic CLI placeholder; never execute.");
            File.WriteAllText(Path.Combine(version, "LICENSE"), "Synthetic manifest fixture.");
            SetupFiles.Atomic(Path.Combine(version, "setup-compatibility.json"), new SetupCompatibility(1, 1, 1, 1));
            var files = Directory.GetFiles(version, "*", SearchOption.AllDirectories).Select(path => new PackageFile(Path.GetRelativePath(version, path).Replace('\\', '/'), new FileInfo(path).Length, SetupFiles.FileHash(path))).ToArray();
            var manifest = Path.Combine(version, "package-manifest.json"); SetupFiles.Atomic(manifest, new PackageManifest(1, "0.6.0", "win-x64", true, "synthetic-fixture", new string('a', 40), false, false, new string('b', 64), files, true));
            SetupFiles.Atomic(ConfigPath, Config);
            SetupFiles.Atomic(SetupService.Selection(Install), new Installation(1, Install, Target, ConfigPath, Config.RulesDirectory!, "0.6.0",
                [new("0.6.0", version, SetupFiles.FileHash(manifest), new string('c', 64))], new string('d', 32)));
        }
        public ProcessStartInfo Start()
        {
            var start = new ProcessStartInfo(Path.Combine(WebDirectory, "ArchSift.Web.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("--launcher"); start.ArgumentList.Add(Install);
            start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Root, "cli-home"); start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
            start.Environment["ARCHSIFT_UI_PARENT_CONTROL"] = "stdin-v1"; return start;
        }
        public void Dispose() { Assert.True(PathSafety.IsUnder(Root, Path.GetTempPath())); Directory.Delete(Root, true); }
    }
}
