using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.TestGate;

namespace ArchSift.IntegrationTests;

public sealed class WebDailyTestIsolationTests
{
    [Fact]
    public async Task NewAndCopiedDailyRunsOwnTheirCliHomeAndRetainRestoreFailureReports()
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-daily-home-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source"); var output = Path.Combine(root, "reports");
        var library = Path.Combine(root, "library"); var feed = Path.Combine(root, "empty-feed");
        var cache = Path.Combine(root, "cache"); var originalRun = Path.Combine(root, "original-run");
        foreach (var path in new[] { source, library, feed, cache }) Directory.CreateDirectory(path);
        Process? process = null;
        try
        {
            File.WriteAllText(Path.Combine(source, "Tests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"ArchSift.Owned.Missing.Test.Package\" Version=\"1.0.0\" /></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(source, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>");
            var rules = Path.Combine(source, "rules.json"); File.WriteAllText(rules, RuleTemplates.Json("type-dependency"));
            var baseline = Path.Combine(root, "baseline.json"); File.WriteAllText(baseline, "[\"owned-original-case\"]");
            var daily = new TestGateConfig { TargetRoot = source, TestProject = "Tests.csproj", TestExecutableName = "Tests",
                TargetFramework = "net10.0", Configuration = "Release", NuGetConfig = "NuGet.Config", LocalFeed = feed,
                PackageCache = cache, Rulesets = [rules], OriginalBaseline = baseline,
                OriginalBaselineSha256 = ContentHash.Bytes(File.ReadAllBytes(baseline)),
                RunDirectory = originalRun, DotnetCliHome = Path.Combine(originalRun, "cli-home"), AuditMode = "offline-architecture" };
            var dailyPath = Path.Combine(root, "daily.json"); File.WriteAllText(dailyPath, JsonSerializer.Serialize(daily, JsonContract.Options));
            var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = source }, Output = new() { Directory = output }, RulesDirectory = library };
            var configPath = Path.Combine(root, "config.json"); File.WriteAllText(configPath, JsonSerializer.Serialize(config, JsonContract.Options));
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "ArchSift.slnx"))) repo = repo.Parent;
            var build = typeof(WebDailyTestIsolationTests).Assembly.GetCustomAttributes(false).OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
            var web = Path.Combine(repo!.FullName, "src", "ArchSift.Web", "bin", build, "net10.0", "ArchSift.Web.dll");
            var start = new ProcessStartInfo(SafeProcess.Dotnet) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
            foreach (var arg in new[] { web, "--config", configPath }) start.ArgumentList.Add(arg);
            start.Environment["DOTNET_CLI_HOME"] = Path.Combine(root, "outer-home"); start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
            process = Process.Start(start)!; var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            Uri? address = null;
            while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
                if (line.StartsWith("ARCHSIFT_UI=", StringComparison.Ordinal)) { address = new Uri(line[12..]); break; }
            Assert.NotNull(address);
            var remainingOutput = process.StandardOutput.ReadToEndAsync();
            using var client = new HttpClient { BaseAddress = new Uri(address.GetLeftPart(UriPartial.Authority)) };
            client.DefaultRequestHeaders.Add("X-ArchSift-Token", address.Fragment[9..]);
            var selectedResponse = await client.PostAsJsonAsync("/api/test-workbench/config", new { path = dailyPath }, timeout.Token);
            Assert.True(selectedResponse.IsSuccessStatusCode, await selectedResponse.Content.ReadAsStringAsync(timeout.Token));
            // Check both direct selection and the persisted configuration produced by copying a rule.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (attempt == 1)
                {
                    var copied = await client.PostAsJsonAsync("/api/test-workbench/copy-rule", new { index = 0 }, timeout.Token);
                    copied.EnsureSuccessStatusCode();
                    using var body = JsonDocument.Parse(await copied.Content.ReadAsStringAsync(timeout.Token));
                    var selected = TestGateService.Load(body.RootElement.GetProperty("configPath").GetString()!);
                    Assert.Equal(Path.Combine(selected.RunDirectory, "cli-home"), selected.DotnetCliHome);
                }
                var response = await client.PostAsJsonAsync("/api/test-workbench/run", new { mode = "full" }, timeout.Token);
                response.EnsureSuccessStatusCode();
                using var started = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                var run = started.RootElement.GetProperty("runDirectory").GetString()!;
                var job = default(JsonElement);
                while (true)
                {
                    using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/test-workbench/job", timeout.Token));
                    job = doc.RootElement.Clone();
                    if (job.GetProperty("state").GetString() != "running") break;
                    await Task.Delay(30, timeout.Token);
                }
                Assert.Equal("finished", job.GetProperty("state").GetString());
                Assert.Equal(3, job.GetProperty("exitCode").GetInt32());
                Assert.Equal("block", job.GetProperty("gate").GetString());
                var actual = TestGateService.Load(job.GetProperty("configPath").GetString()!);
                Assert.Equal(Path.Combine(run, "cli-home"), actual.DotnetCliHome);
                Assert.False(Directory.Exists(originalRun));
                using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(run, "architecture-test-run.json")));
                Assert.Equal(3, report.RootElement.GetProperty("outcome").GetProperty("exitCode").GetInt32());
                Assert.Contains("Offline restore failed.", report.RootElement.GetProperty("outcome").GetProperty("reasons").EnumerateArray().Select(value => value.GetString()));
            }
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/shutdown", new { }, timeout.Token)).StatusCode);
            await process.WaitForExitAsync(timeout.Token); await Task.WhenAll(errors, remainingOutput);
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (process is not null) { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); process.Dispose(); }
            if (!PathSafety.IsUnder(root, Path.GetTempPath())) throw new InvalidOperationException("Fixture cleanup containment failed.");
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
