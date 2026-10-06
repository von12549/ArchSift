using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class WebWorkbenchTests
{
    [Fact]
    public async Task LoopbackSecurityRulesPersistenceAndUiCliParityAreRealHttpOperations()
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-web-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source"); var output = Path.Combine(root, "output"); var rulesDirectory = Path.Combine(root, "rules");
        Directory.CreateDirectory(source); Directory.CreateDirectory(rulesDirectory);
        Process? process = null;
        try
        {
            File.WriteAllText(Path.Combine(source, "Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = source }, Output = new() { Directory = output }, RulesDirectory = rulesDirectory };
            var path = Path.Combine(root, "config.json"); File.WriteAllText(path, JsonSerializer.Serialize(config, JsonContract.Options));
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
            var build = typeof(WebWorkbenchTests).Assembly.GetCustomAttributes(false).OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
            var web = Path.Combine(directory!.FullName, "src", "ArchSift.Web", "bin", build, "net10.0", "ArchSift.Web.dll");
            var start = new ProcessStartInfo(SafeProcess.Dotnet) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
            foreach (var arg in new[] { web, "--config", path }) start.ArgumentList.Add(arg);
            start.Environment["DOTNET_CLI_HOME"] = Path.Combine(root, "cli-home"); start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
            process = Process.Start(start)!; var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            string? line; Uri? address = null;
            while ((line = await process.StandardOutput.ReadLineAsync(timeout.Token)) is not null)
                if (line.StartsWith("ARCHSIFT_UI=", StringComparison.Ordinal)) { address = new Uri(line[12..]); break; }
            Assert.NotNull(address); Assert.Equal("127.0.0.1", address.Host);
            using var client = new HttpClient { BaseAddress = new Uri(address.GetLeftPart(UriPartial.Authority)) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/config", timeout.Token)).StatusCode);
            client.DefaultRequestHeaders.Add("X-ArchSift-Token", address.Fragment[9..]);
            client.DefaultRequestHeaders.Add("Origin", "https://evil.invalid");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/config", timeout.Token)).StatusCode);
            client.DefaultRequestHeaders.Remove("Origin");
            var page = await client.GetAsync("/", timeout.Token); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.True(page.Headers.Contains("Content-Security-Policy"));
            Assert.Contains("本次检查范围", await page.Content.ReadAsStringAsync(timeout.Token));
            var templateResponse = await client.GetAsync("/api/templates", timeout.Token);
            using var templates = JsonDocument.Parse(await templateResponse.Content.ReadAsStringAsync(timeout.Token));
            var template = templates.RootElement.GetProperty("naming").GetRawText();
            var saved = await client.PostAsync("/api/rules/policy.json", new StringContent(template, Encoding.UTF8, "application/json"), timeout.Token);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            Assert.True(File.Exists(Path.Combine(rulesDirectory, "policy.json")));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/rules/policy.json/markdown", timeout.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/rules/policy.json", new StringContent("{}", Encoding.UTF8, "application/json"), timeout.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/config", JsonContent.Create(config with { Output = new() { Directory = source } }, options: JsonContract.Options), timeout.Token)).StatusCode);
            var started = await client.PostAsJsonAsync("/api/run/verify", new { }, timeout.Token);
            using var startJson = JsonDocument.Parse(await started.Content.ReadAsStringAsync(timeout.Token));
            var id = startJson.RootElement.GetProperty("jobId").GetString(); JsonDocument? job = null;
            while (true)
            {
                var response = await client.GetAsync("/api/jobs/" + id, timeout.Token);
                job?.Dispose(); job = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                if (job.RootElement.GetProperty("state").GetString() != "running") break;
                await Task.Delay(50, timeout.Token);
            }
            using (job)
            {
                var report = job.RootElement.GetProperty("report").Deserialize<AnalysisReport>(JsonContract.Options)!;
                var direct = await new AnalysisService().RunAsync(config with { Rulesets = [Path.Combine(rulesDirectory, "policy.json")] }, "verify");
                Assert.Equal(direct.Report.Findings.Select(f => f.Id), report.Findings.Select(f => f.Id));
                Assert.Equal(direct.ExitCode, job.RootElement.GetProperty("exitCode").GetInt32());
            }
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/jobs/" + id + "/report/json", timeout.Token)).StatusCode);
            Assert.False(Directory.Exists(Path.Combine(source, "obj")));
        }
        finally
        {
            if (process is not null) { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); process.Dispose(); }
            Directory.Delete(root, true);
        }
    }
}
