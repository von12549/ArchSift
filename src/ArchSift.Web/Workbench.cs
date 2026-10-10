using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Web;

public sealed class Workbench(RunConfiguration initial, string rulesDirectory, SessionProfile? profile = null)
{
    private RunConfiguration current = initial;
    private readonly ConcurrentDictionary<string, Job> jobs = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile bool dirty;
    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public void Map(WebApplication app)
    {
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            foreach (var job in jobs.Values.Where(j => j.State == "running"))
                try { job.Cancel.Cancel(); } catch (ObjectDisposedException) { }
        });
        app.Use(async (context, next) =>
        {
            var host = context.Request.Host;
            if ((host.Host is not ("127.0.0.1" or "localhost")) || host.Port != context.Connection.LocalPort)
            { context.Response.StatusCode = 421; return; }
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (!context.Request.Path.StartsWithSegments("/api")) { await next(); return; }
            var origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && origin != "http://" + context.Request.Host.Value) { context.Response.StatusCode = 403; return; }
            var supplied = Encoding.UTF8.GetBytes(context.Request.Headers["X-ArchSift-Token"].ToString());
            if (!CryptographicOperations.FixedTimeEquals(supplied, Encoding.UTF8.GetBytes(Token)))
            { context.Response.StatusCode = 401; return; }
            try { await next(); }
            catch (ConfigurationException error) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = error.Message }); }
            catch (JsonException error) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = error.Message }); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { error = error.Message }); }
        });
        app.UseDefaultFiles(); app.UseStaticFiles();
        app.MapGet("/api/config", () => Results.Json(new { config = current with { RulesDirectory = rulesDirectory }, rulesDirectory, toolVersion = ToolIdentity.Version, profile }, JsonContract.Options));
        app.MapGet("/api/session", () => Results.Json(new { processId = Environment.ProcessId, running = jobs.Values.Any(j => j.State == "running"), dirty }));
        app.MapPost("/api/session/dirty", async (HttpContext context) =>
        {
            using var doc = await JsonDocument.ParseAsync(context.Request.Body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || doc.RootElement.EnumerateObject().Count() != 1 ||
                !doc.RootElement.TryGetProperty("dirty", out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new ConfigurationException("Expected a dirty boolean.");
            dirty = value.GetBoolean(); return Results.Ok();
        });
        app.MapPost("/api/config", async (HttpContext context) =>
        {
            using var doc = await JsonDocument.ParseAsync(context.Request.Body);
            SchemaValidation.Validate(doc.RootElement, "config");
            var config = doc.RootElement.Deserialize<RunConfiguration>(JsonContract.Options)!;
            if (config.RulesDirectory is { } requestedRules && !Path.GetFullPath(requestedRules).Equals(Path.GetFullPath(rulesDirectory), PathSafety.Comparison))
                throw new ConfigurationException("The library directory is fixed for this session. Restart with a new config to change it.");
            config = config with { RulesDirectory = rulesDirectory };
            if (config.Build.AllowNetwork && config.Build.Sources.Length == 0 || !config.Build.AllowNetwork && config.Build.Sources.Length != 0)
                throw new ConfigurationException("Network restore must be explicitly enabled with explicit sources.");
            PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
            PathSafety.EnsureDisjoint(config.Target.Root, rulesDirectory);
            if (!Path.IsPathRooted(config.Target.Root) || !Path.IsPathRooted(config.Output.Directory))
                throw new ConfigurationException("UI target/output paths must be absolute.");
            if (!Directory.Exists(config.Target.Root))
                throw new ConfigurationException("UI target root does not exist.");
            if (config.Target.Entry is { } entry && !File.Exists(PathSafety.Under(config.Target.Root, entry)))
                throw new ConfigurationException("UI target entry does not exist under the target root.");
            current = config;
            return Results.Json(current, JsonContract.Options);
        });
        app.MapGet("/api/templates", () => Results.Json(RuleTemplates.Names.ToDictionary(n => n, n => JsonDocument.Parse(RuleTemplates.Json(n)).RootElement.Clone())));
        RulesetLibrary Library() => new(rulesDirectory, current.Target.Root);
        app.MapGet("/api/library", () => Results.Json(new
        {
            entries = Library().List(),
            references = Library().References(),
            external = current.Rulesets.Where(p => !Path.GetDirectoryName(Path.GetFullPath(p))!.Equals(Path.GetFullPath(rulesDirectory), PathSafety.Comparison)).ToArray()
        }, JsonContract.Options));
        app.MapPost("/api/library/import/{name}", async (string name, HttpContext context) =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
            return Results.Json(Library().Import(name, buffer.ToArray()), JsonContract.Options);
        });
        app.MapGet("/api/library/{entryId}/json", (string entryId) =>
        {
            var library = Library(); var entry = library.Find(entryId) ?? throw new ConfigurationException("Missing library entry.");
            return Results.File(library.Export(entryId), "application/json", entry.FileName);
        });
        app.MapPost("/api/library/{entryId}/delete", (string entryId) => { Library().Delete(entryId); return Results.Ok(); });
        app.MapGet("/api/chains", () => Results.Json(new ChainStore(Library()).List(), JsonContract.Options));
        app.MapGet("/api/chains/{id}", (string id) => Results.Json(new ChainStore(Library()).Read(id), JsonContract.Options));
        app.MapPost("/api/chains/{id}", async (string id, HttpContext context) =>
        {
            using var buffer = new MemoryStream(); await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
            new ChainStore(Library()).Save(id, buffer.ToArray()); return Results.Ok();
        });
        app.MapPost("/api/chains/{id}/delete", (string id) => { new ChainStore(Library()).Delete(id); return Results.Ok(); });
        app.MapGet("/api/rules", () => Results.Json(Directory.Exists(rulesDirectory)
            ? Directory.GetFiles(rulesDirectory, "*.json").Select(Path.GetFileName).Where(n => !n!.StartsWith('.')).Order().ToArray() : []));
        app.MapGet("/api/rules/{name}", (string name) =>
        {
            var path = RuleFile(name); var loaded = RuleLoader.Load(path);
            return Results.Json(loaded.Ruleset, JsonContract.Options);
        });
        app.MapPost("/api/rules/{name}", async (string name, HttpContext context) =>
        {
            var path = RuleFile(name);
            using var document = await JsonDocument.ParseAsync(context.Request.Body);
            var bytes = Encoding.UTF8.GetBytes(document.RootElement.GetRawText());
            var saved = Library().Save(name, bytes);
            if (!current.Rulesets.Contains(path, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
                current = current with { Rulesets = [.. current.Rulesets, path] };
            return Results.Json(new { saved = name, sha256 = saved.Identity!.Sha256, entryId = saved.EntryId });
        });
        app.MapGet("/api/rules/{name}/markdown", (string name) =>
        {
            var loaded = RuleLoader.Load(RuleFile(name)); RuleLoader.Compose([loaded]);
            return Results.Text(RuleMarkdown.Render(loaded), "text/markdown", Encoding.UTF8);
        });
        app.MapPost("/api/run/{operation}", async (string operation, HttpContext context) =>
        {
            if (operation is not ("analyze" or "verify" or "draft" or "changes" or "ruleset" or "chain")) return Results.BadRequest(new { error = "Unknown operation." });
            ChangeRequest request = new();
            using var document = await JsonDocument.ParseAsync(context.Request.Body);
            SchemaValidation.ValidateDocument(document.RootElement, JsonSerializer.SerializeToElement(new { type = "object" }));
            if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Any(p =>
                    p.Name != "targetKind" && (operation != "changes" || p.Name is not ("base" or "head")) &&
                    (operation != "ruleset" || p.Name != "entryId") && (operation != "chain" || p.Name is not ("chainId" or "maxConcurrency"))))
                throw new ConfigurationException("Run request contains unsupported fields.");
            var targetKind = document.RootElement.TryGetProperty("targetKind", out var kind) ? kind.GetString() : "real";
            if (targetKind is not ("real" or "fixture")) throw new ConfigurationException("Target kind must be real or fixture.");
            if (operation == "changes") request = new(
                document.RootElement.TryGetProperty("base", out var @base) && @base.ValueKind != JsonValueKind.Null ? @base.GetString() : null,
                document.RootElement.TryGetProperty("head", out var head) && head.ValueKind != JsonValueKind.Null ? head.GetString() : null);
            var config = current;
            var options = new ChainRunOptions();
            if (operation == "chain" && document.RootElement.TryGetProperty("maxConcurrency", out var concurrency))
            {
                var optionJson = JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement> { ["maxConcurrency"] = concurrency });
                SchemaValidation.Validate(optionJson, "chain-run-options"); options = optionJson.Deserialize<ChainRunOptions>(JsonContract.Options)!;
            }
            if (!await gate.WaitAsync(0)) return Results.Conflict(new { error = "A job is running. Cancel it or wait for completion." });
            LoadedRuleset? selected = null; CapturedChain? chain = null;
            try
            {
            if (operation == "ruleset")
            {
                if (!document.RootElement.TryGetProperty("entryId", out var entryId) || entryId.ValueKind != JsonValueKind.String)
                    throw new ConfigurationException("Ruleset verification requires a saved library entry ID.");
                selected = Library().Capture(entryId.GetString()!);
                config = config with { Rulesets = [selected.Identity.Path], Output = config.Output with { Formats = ["json", "html", "sarif"] } };
            }
            if (operation == "chain")
            {
                if (!document.RootElement.TryGetProperty("chainId", out var chainId) || chainId.ValueKind != JsonValueKind.String)
                    throw new ConfigurationException("Chain verification requires a saved chain ID.");
                var library = new RulesetLibrary(rulesDirectory, config.Target.Root);
                chain = ChainService.Capture(config, new ChainStore(library).Read(chainId.GetString()!), library, targetKind!);
                config = config with { Rulesets = chain.Rulesets.Where(r => r is not null).Select(r => r!.Identity.Path).ToArray() };
            }
            }
            catch { gate.Release(); throw; }
            var job = new Job(Guid.NewGuid().ToString("N"), operation, config, targetKind, selected?.Ruleset.Id ?? chain?.Chain.Id); jobs[job.Id] = job;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (operation == "chain")
                    {
                        var worker = new AssemblyWorkerClient(typeof(Program).Assembly.Location);
                        job.Chain = await new ChainService(new AnalysisService(worker.EvaluateAsync)).RunCapturedAsync(config, chain!, job.Cancel.Token,
                            progress => job.Progress = progress, job.Id, options);
                        job.ExitCode = job.Chain.ExitCode; job.State = job.Chain.Execution;
                    }
                    else if (operation == "draft")
                    {
                        var snapshot = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework, job.Cancel.Token);
                        job.Draft = RuleDraftService.Create(snapshot); job.ExitCode = 0; job.State = "completed";
                    }
                    else if (operation == "changes")
                    {
                        var worker = new AssemblyWorkerClient(typeof(Program).Assembly.Location);
                        var outcome = await new ComparisonService(new AnalysisService(worker.EvaluateAsync)).RunAsync(config, request, job.Cancel.Token);
                        job.Comparison = outcome.Report; job.ExitCode = outcome.ExitCode;
                        ComparisonWriter.Save(config, outcome.Report); job.State = outcome.Report.Status == "completed" ? "completed" : outcome.Report.Status == "cancelled" ? "cancelled" : "partial";
                    }
                    else
                    {
                        var worker = new AssemblyWorkerClient(typeof(Program).Assembly.Location);
                        var service = new AnalysisService(worker.EvaluateAsync);
                        var outcome = selected is null ? await service.RunAsync(config, operation, job.Cancel.Token)
                            : await service.RunSelectedAsync(config, selected, job.Cancel.Token);
                        job.Report = outcome.Report; job.ExitCode = outcome.ExitCode;
                        ReportWriter.Save(config, outcome.Report); job.State = outcome.Report.Execution;
                    }
                }
                catch (OperationCanceledException) { job.State = "cancelled"; job.ExitCode = 130; }
                catch (Exception error) { job.Report = null; job.Comparison = null; job.Chain = null; job.Error = error.Message; job.ExitCode = error is ConfigurationException ? 2 : 3; job.State = "failed"; }
                finally { gate.Release(); }
            });
            foreach (var old in jobs.Values.Where(j => j.State != "running").OrderBy(j => j.Created).Take(Math.Max(0, jobs.Count - 20)))
                if (jobs.TryRemove(old.Id, out var removed)) removed.Cancel.Dispose();
            return Results.Json(new { jobId = job.Id, context = job.Context, targetKind = job.TargetKind, selection = job.Selection }, JsonContract.Options);
        });
        app.MapGet("/api/jobs", () => Results.Json(jobs.Values.OrderByDescending(j => j.Created).Select(j => new { j.Id, j.Operation, j.State, j.ExitCode, j.Error })));
        app.MapGet("/api/jobs/{id}", (string id) => jobs.TryGetValue(id, out var job)
            ? Results.Json(new { job.Id, job.Operation, job.State, job.ExitCode, job.Error, job.Context, job.TargetKind, job.Selection, job.Report, job.Draft, job.Comparison, job.Chain, job.Progress }, JsonContract.Options)
            : Results.NotFound());
        app.MapPost("/api/jobs/{id}/cancel", (string id) =>
        {
            if (!jobs.TryGetValue(id, out var job)) return Results.NotFound();
            if (job.State == "running") job.Cancel.Cancel();
            return Results.Ok();
        });
        app.MapGet("/api/jobs/{id}/report/{format}", (string id, string format) =>
        {
            if (!jobs.TryGetValue(id, out var job) || job.State is "running" or "failed") return Results.NotFound();
            if (job.Chain is { } chain) return format switch
            {
                "json" => Results.Text(ChainWriter.Json(chain), "application/json", Encoding.UTF8),
                "html" => Results.Text(ChainWriter.Html(chain), "text/html", Encoding.UTF8),
                "sarif" => Results.Text(ChainWriter.Sarif(chain), "application/sarif+json", Encoding.UTF8),
                _ => Results.BadRequest()
            };
            if (job.Comparison is { } comparison) return format switch
            {
                "json" => Results.Text(ComparisonWriter.Json(comparison), "application/json", Encoding.UTF8),
                "html" => Results.Text(ComparisonWriter.Html(comparison), "text/html", Encoding.UTF8),
                "sarif" => Results.Text(SarifWriter.Json(comparison), "application/sarif+json", Encoding.UTF8),
                _ => Results.BadRequest()
            };
            if (job.Report is null) return Results.NotFound();
            return format switch
            {
                "json" => Results.Text(ReportWriter.Json(job.Report), "application/json", Encoding.UTF8),
                "html" => Results.Text(ReportWriter.Html(job.Report), "text/html", Encoding.UTF8),
                "sarif" => Results.Text(SarifWriter.Json(job.Report), "application/sarif+json", Encoding.UTF8),
                _ => Results.BadRequest()
            };
        });
        app.MapGet("/api/jobs/{id}/draft", (string id) => jobs.TryGetValue(id, out var job) && job.Draft is not null
            ? Results.Text(JsonSerializer.Serialize(job.Draft, JsonContract.Options), "application/json", Encoding.UTF8)
            : Results.NotFound());
        app.MapGet("/api/jobs/{id}/child/{entryId}/{format}", (string id, string entryId, string format) =>
        {
            if (!jobs.TryGetValue(id, out var job) || job.Chain is null || format is not ("json" or "html" or "sarif")) return Results.NotFound();
            var entry = job.Chain.Entries.SingleOrDefault(e => e.EntryId == entryId);
            if (entry?.ReportDirectory is not { } relative) return Results.NotFound();
            if (entry.Diagnostic is null && job.Chain.AnalysisReports.TryGetValue(entryId, out var report)) return format switch
            {
                "json" => Results.Text(ReportWriter.Json(report), "application/json", Encoding.UTF8),
                "html" => Results.Text(ReportWriter.Html(report), "text/html", Encoding.UTF8),
                "sarif" => Results.Text(SarifWriter.Json(report), "application/sarif+json", Encoding.UTF8),
                _ => Results.BadRequest()
            };
            var path = PathSafety.Under(job.Chain.Snapshot.OutputDirectory, relative + "/" + (entry.Diagnostic is null ? "report." : "diagnostic.") + format);
            return Results.File(File.ReadAllBytes(path), format == "html" ? "text/html" : format == "sarif" ? "application/sarif+json" : "application/json", Path.GetFileName(path));
        });
        app.MapPost("/api/shutdown", async (HttpContext context) =>
        {
            if (jobs.Values.Any(job => job.State == "running"))
                return Results.Conflict(new { error = "A job is running. Cancel it or wait for completion." });
            if (dirty)
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body);
                if (!doc.RootElement.TryGetProperty("discardChanges", out var discard) || discard.ValueKind != JsonValueKind.True)
                    return Results.Conflict(new { error = "Unsaved changes remain. Export/save them or explicitly discard them." });
            }
            _ = Task.Run(async () => { await Task.Delay(100); app.Lifetime.StopApplication(); });
            return Results.Accepted();
        });
    }

    public async Task DrainShutdownAsync()
    {
        // Job completion releases the gate only after process cleanup and current audit writes.
        // An owning CLI remains alive and can terminate the whole tree if graceful cleanup stalls.
        if (!await gate.WaitAsync(TimeSpan.FromSeconds(8)))
        {
            Console.Error.WriteLine("ARCHSIFT_SHUTDOWN=forced; owned process tree did not finish cancellation.");
            await Console.Error.FlushAsync();
            using var owned = System.Diagnostics.Process.GetCurrentProcess();
            owned.Kill(entireProcessTree: true);
            return;
        }
        gate.Release();
    }

    private string RuleFile(string name)
    {
        if (Path.GetFileName(name) != name || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new ConfigurationException("Rule file must be a JSON filename in the explicit rules directory.");
        return PathSafety.Under(rulesDirectory, name);
    }
    private sealed class Job(string id, string operation, RunConfiguration context, string targetKind, string? selection)
    {
        public string Id { get; } = id;
        public string Operation { get; } = operation;
        public RunConfiguration Context { get; } = context;
        public string TargetKind { get; } = targetKind;
        public string? Selection { get; } = selection;
        public DateTimeOffset Created { get; } = DateTimeOffset.UtcNow;
        public CancellationTokenSource Cancel { get; } = new();
        private volatile string state = "running";
        public string State { get => state; set => state = value; }
        public int? ExitCode { get; set; }
        public string? Error { get; set; }
        public AnalysisReport? Report { get; set; }
        public RuleDraft? Draft { get; set; }
        public ComparisonReport? Comparison { get; set; }
        public ChainSummary? Chain { get; set; }
        private ChainProgress? progress;
        public ChainProgress? Progress { get => Volatile.Read(ref progress); set => Volatile.Write(ref progress, value); }
    }
}
