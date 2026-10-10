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
            using (var runtime = JsonDocument.Parse(await client.GetStringAsync("/api/config", timeout.Token)))
                Assert.Equal(ToolIdentity.Version, runtime.RootElement.GetProperty("toolVersion").GetString());
            var page = await client.GetAsync("/", timeout.Token); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.True(page.Headers.Contains("Content-Security-Policy"));
            var pageText = await page.Content.ReadAsStringAsync(timeout.Token);
            Assert.Contains("Target configuration", pageText);
            Assert.Contains("Run context · Edited configuration", pageText);
            Assert.Contains("Safe shutdown", pageText);
            Assert.Contains("Projects and references", pageText);
            Assert.Contains("These are analyzed objects, separate from policy results, findings and coverage limitations.", pageText);
            var scriptText = await client.GetStringAsync("/app.js", timeout.Token);
            Assert.Contains("Execution completeness and policy compliance are separate dimensions.", scriptText);
            Assert.Contains("Per-rule results", scriptText);
            Assert.Contains("Findings and exceptions", scriptText);
            Assert.Contains("These limitations affect completeness; they are separate from projects and findings.", scriptText);
            Assert.Contains("Coverage limitations: passing rules do not imply full coverage", scriptText);
            Assert.Contains("Collapsed; expand for details", scriptText);
            var styleText = await client.GetStringAsync("/style.css", timeout.Token);
            Assert.Contains(".finding-region", styleText);
            Assert.Contains(".coverage-region", styleText);
            Assert.Contains(".project-region", styleText);
            Assert.Contains(".finding-table td:nth-child(5)::before", styleText);
            var templateResponse = await client.GetAsync("/api/templates", timeout.Token);
            using var templates = JsonDocument.Parse(await templateResponse.Content.ReadAsStringAsync(timeout.Token));
            Assert.True(templates.RootElement.TryGetProperty("project-reference-allowlist", out _));
            Assert.True(templates.RootElement.TryGetProperty("nuget-allowlist", out _));
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
                Assert.Equal(source, job.RootElement.GetProperty("context").GetProperty("target").GetProperty("root").GetString());
                Assert.Equal("real", job.RootElement.GetProperty("targetKind").GetString());
                var report = job.RootElement.GetProperty("report").Deserialize<AnalysisReport>(JsonContract.Options)!;
                var direct = await new AnalysisService().RunAsync(config with { Rulesets = [Path.Combine(rulesDirectory, "policy.json")] }, "verify");
                Assert.Equal(direct.Report.Findings.Select(f => f.Id), report.Findings.Select(f => f.Id));
                Assert.Equal(direct.ExitCode, job.RootElement.GetProperty("exitCode").GetInt32());
            }
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/jobs/" + id + "/report/json", timeout.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/jobs/" + id + "/report/sarif", timeout.Token)).StatusCode);
            // New card/chain surfaces are independent of startup composition and preserve exact JSON bytes.
            var sourceBefore = InputCapture.Capture(source).Sha256;
            async Task<LibraryCard[]> Cards()
            {
                using var document = JsonDocument.Parse(await client.GetStringAsync("/api/library", timeout.Token));
                return document.RootElement.GetProperty("entries").Deserialize<LibraryCard[]>(JsonContract.Options)!;
            }
            var firstCard = Assert.Single(await Cards());
            var importedBytes = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(template.Replace("template-naming", "second-policy", StringComparison.Ordinal))).ToArray();
            var imported = await client.PostAsync("/api/library/import/second.json", new ByteArrayContent(importedBytes), timeout.Token);
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
            var secondCard = (await Cards()).Single(c => c.EntryId != firstCard.EntryId);
            Assert.Equal(importedBytes, await client.GetByteArrayAsync("/api/library/" + secondCard.EntryId + "/json", timeout.Token));
            var registryHash = AssemblyArtifacts.FileHash(Path.Combine(rulesDirectory, ".archsift-library.json"));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/library/import/second.json", new ByteArrayContent(importedBytes), timeout.Token)).StatusCode);
            Assert.Equal(registryHash, AssemblyArtifacts.FileHash(Path.Combine(rulesDirectory, ".archsift-library.json")));
            async Task<JsonDocument> NewJob(string operation, object request)
            {
                var response = await client.PostAsJsonAsync("/api/run/" + operation, request, timeout.Token); response.EnsureSuccessStatusCode();
                using var initialJob = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                var newId = initialJob.RootElement.GetProperty("jobId").GetString();
                while (true)
                {
                    var document = JsonDocument.Parse(await client.GetStringAsync("/api/jobs/" + newId, timeout.Token));
                    if (document.RootElement.GetProperty("state").GetString() != "running") return document;
                    document.Dispose(); await Task.Delay(20, timeout.Token);
                }
            }
            var composedConfig = config with { Rulesets = [Path.Combine(rulesDirectory, "policy.json"), Path.Combine(rulesDirectory, "second.json")] };
            (await client.PostAsync("/api/config", JsonContent.Create(composedConfig, options: JsonContract.Options), timeout.Token)).EnsureSuccessStatusCode();
            using (var cardJob = await NewJob("ruleset", new { entryId = firstCard.EntryId, targetKind = "fixture" }))
            {
                var report = cardJob.RootElement.GetProperty("report").Deserialize<AnalysisReport>(JsonContract.Options)!;
                Assert.Single(report.RulesetIdentities); Assert.Equal(firstCard.Identity!.Sha256, report.RulesetIdentities[0].Sha256);
                Assert.Single(cardJob.RootElement.GetProperty("context").GetProperty("rulesets").EnumerateArray());
                Assert.Equal("fixture", cardJob.RootElement.GetProperty("targetKind").GetString());
            }
            var chain = new ChainDocument(1, "http-chain", "1", "User-authored 原文", [new(firstCard.EntryId), new(secondCard.EntryId)]);
            (await client.PostAsync("/api/chains/http-chain", JsonContent.Create(chain, options: JsonContract.Options), timeout.Token)).EnsureSuccessStatusCode();
            using (var chainJob = await NewJob("chain", new { chainId = chain.Id, targetKind = "fixture" }))
            {
                var summary = chainJob.RootElement.GetProperty("chain").Deserialize<ChainSummary>(JsonContract.Options)!;
                Assert.Equal(2, summary.Entries.Length); Assert.Equal(1, summary.ProjectCount); Assert.Equal("noncompliant", summary.Compliance);
                foreach (var child in summary.Entries)
                    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/jobs/" + chainJob.RootElement.GetProperty("id").GetString() + "/child/" + child.EntryId + "/sarif", timeout.Token)).StatusCode);
            }
            (await client.PostAsync("/api/library/" + firstCard.EntryId + "/delete", JsonContent.Create(new { }), timeout.Token)).EnsureSuccessStatusCode();
            using (var missingJob = await NewJob("chain", new { chainId = chain.Id }))
            {
                var summary = missingJob.RootElement.GetProperty("chain").Deserialize<ChainSummary>(JsonContract.Options)!;
                Assert.Equal("missing-ruleset", summary.Entries[0].Diagnostic!.Code); Assert.Empty(summary.Entries[0].Findings);
                Assert.Equal("completed", summary.Entries[1].Execution); Assert.Equal(4, summary.ExitCode);
            }
            (await client.PostAsync("/api/library/import/policy.json", new StringContent(template, Encoding.UTF8, "application/json"), timeout.Token)).EnsureSuccessStatusCode();
            Assert.DoesNotContain(await Cards(), c => c.EntryId == firstCard.EntryId);
            Assert.Equal(firstCard.EntryId, (await client.GetFromJsonAsync<ChainDocument>("/api/chains/http-chain", JsonContract.Options, timeout.Token))!.Entries[0].EntryId);
            (await client.PostAsync("/api/config", JsonContent.Create(config with { Rulesets = [Path.Combine(rulesDirectory, "policy.json")] }, options: JsonContract.Options), timeout.Token)).EnsureSuccessStatusCode();
            Assert.Equal(sourceBefore, InputCapture.Capture(source).Sha256);
            // Compare the same source over the authenticated HTTP surface and reject stale downloads after failure.
            async Task Git(params string[] arguments)
            {
                var result = await SafeProcess.RunAsync("git", source, arguments, Path.Combine(root, "git-home"), null, timeout.Token);
                Assert.Equal(0, result.ExitCode);
            }
            await Git("init"); await Git("add", ".");
            await Git("-c", "user.name=ArchSift Fixture", "-c", "user.email=fixture@archsift.invalid", "commit", "-m", "Synthetic HTTP fixture\n\nCo-Authored-By: Codex <noreply@openai.com>");
            async Task<JsonDocument> Compare(object request)
            {
                var response = await client.PostAsJsonAsync("/api/run/changes", request, timeout.Token);
                response.EnsureSuccessStatusCode();
                using var first = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                var jobId = first.RootElement.GetProperty("jobId").GetString();
                while (true)
                {
                    var json = JsonDocument.Parse(await client.GetStringAsync("/api/jobs/" + jobId, timeout.Token));
                    if (json.RootElement.GetProperty("state").GetString() != "running") return json;
                    json.Dispose(); await Task.Delay(50, timeout.Token);
                }
            }
            using (var compared = await Compare(new { }))
            {
                var comparison = compared.RootElement.GetProperty("comparison").Deserialize<ComparisonReport>(JsonContract.Options)!;
                Assert.Equal("completed", comparison.Status);
                Assert.Equal(directFindingIds(comparison.Target!), directFindingIds(comparison.Baseline!));
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/jobs/" + compared.RootElement.GetProperty("id").GetString() + "/report/html", timeout.Token)).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/jobs/" + compared.RootElement.GetProperty("id").GetString() + "/report/sarif", timeout.Token)).StatusCode);
            }
            using (var failed = await Compare(new { @base = "not-a-local-ref", head = "HEAD" }))
            {
                Assert.Equal("failed", failed.RootElement.GetProperty("state").GetString());
                Assert.Equal(JsonValueKind.Null, failed.RootElement.GetProperty("comparison").ValueKind);
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/jobs/" + failed.RootElement.GetProperty("id").GetString() + "/report/json", timeout.Token)).StatusCode);
            }
            static string[] directFindingIds(AnalysisReport report) => report.Findings.Select(f => f.Id).ToArray();
            Assert.False(Directory.Exists(Path.Combine(source, "obj")));
            var shutdown = await client.PostAsync("/api/shutdown", new StringContent("{}", Encoding.UTF8, "application/json"), timeout.Token);
            Assert.Equal(HttpStatusCode.Accepted, shutdown.StatusCode);
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (process is not null) { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); process.Dispose(); }
            if (Directory.Exists(root))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                { PathSafety.EnsureNoLinks(file); File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly); }
                if (!PathSafety.IsUnder(root, Path.GetTempPath())) throw new InvalidOperationException("Fixture cleanup containment failed.");
                Directory.Delete(root, true);
            }
        }
    }
}
