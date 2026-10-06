using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Web;

public sealed class Workbench(RunConfiguration initial, string rulesDirectory)
{
    private RunConfiguration current = initial;
    private readonly ConcurrentDictionary<string, Job> jobs = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public void Map(WebApplication app)
    {
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
        app.MapGet("/api/config", () => Results.Json(new { config = current, rulesDirectory }, JsonContract.Options));
        app.MapPost("/api/config", async (HttpContext context) =>
        {
            using var doc = await JsonDocument.ParseAsync(context.Request.Body);
            SchemaValidation.Validate(doc.RootElement, "config");
            var config = doc.RootElement.Deserialize<RunConfiguration>(JsonContract.Options)!;
            if (config.Build.AllowNetwork && config.Build.Sources.Length == 0 || !config.Build.AllowNetwork && config.Build.Sources.Length != 0)
                throw new ConfigurationException("Network restore must be explicitly enabled with explicit sources.");
            PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
            PathSafety.EnsureDisjoint(config.Target.Root, rulesDirectory);
            if (!Path.IsPathRooted(config.Target.Root) || !Path.IsPathRooted(config.Output.Directory))
                throw new ConfigurationException("UI target/output paths must be absolute.");
            current = config;
            return Results.Json(current, JsonContract.Options);
        });
        app.MapGet("/api/templates", () => Results.Json(RuleTemplates.Names.ToDictionary(n => n, n => JsonDocument.Parse(RuleTemplates.Json(n)).RootElement.Clone())));
        app.MapGet("/api/rules", () => Results.Json(Directory.Exists(rulesDirectory)
            ? Directory.GetFiles(rulesDirectory, "*.json").Select(Path.GetFileName).Order().ToArray() : []));
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
            var rules = RuleLoader.Parse(bytes);
            RuleLoader.Compose([new(rules, new(rules.Id, rules.Version, ContentHash.Bytes(bytes), path))]);
            PathSafety.EnsureDisjoint(current.Target.Root, rulesDirectory);
            Directory.CreateDirectory(rulesDirectory);
            PathSafety.EnsureNoLinks(path);
            var temporary = Path.Combine(rulesDirectory, ".save-" + Guid.NewGuid().ToString("N"));
            await File.WriteAllBytesAsync(temporary, bytes);
            File.Move(temporary, path, true);
            if (!current.Rulesets.Contains(path, StringComparer.Ordinal))
                current = current with { Rulesets = [.. current.Rulesets, path] };
            return Results.Json(new { saved = name, sha256 = ContentHash.Bytes(bytes) });
        });
        app.MapGet("/api/rules/{name}/markdown", (string name) =>
        {
            var loaded = RuleLoader.Load(RuleFile(name)); RuleLoader.Compose([loaded]);
            return Results.Text(RuleMarkdown.Render(loaded), "text/markdown", Encoding.UTF8);
        });
        app.MapPost("/api/run/{operation}", async (string operation, HttpContext context) =>
        {
            if (operation is not ("analyze" or "verify" or "draft" or "changes")) return Results.BadRequest(new { error = "Unknown operation." });
            ChangeRequest request = new();
            if (operation == "changes")
            {
                using var document = await JsonDocument.ParseAsync(context.Request.Body);
                if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Any(p => p.Name is not ("base" or "head")))
                    throw new ConfigurationException("Comparison accepts only base/head local commit references.");
                request = document.RootElement.Deserialize<ChangeRequest>(JsonContract.Options)!;
            }
            if (!await gate.WaitAsync(0)) return Results.Conflict(new { error = "已有运行在进行；先取消或等待完成。" });
            var job = new Job(Guid.NewGuid().ToString("N"), operation); jobs[job.Id] = job;
            var config = current;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (operation == "draft")
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
                        var outcome = await new AnalysisService(worker.EvaluateAsync).RunAsync(config, operation, job.Cancel.Token);
                        job.Report = outcome.Report; job.ExitCode = outcome.ExitCode;
                        ReportWriter.Save(config, outcome.Report); job.State = outcome.Report.Execution;
                    }
                }
                catch (OperationCanceledException) { job.State = "cancelled"; job.ExitCode = 130; }
                catch (Exception error) { job.Error = error.Message; job.State = "failed"; job.ExitCode = error is ConfigurationException ? 2 : 3; job.Report = null; job.Comparison = null; }
                finally { gate.Release(); }
            });
            foreach (var old in jobs.Values.Where(j => j.State != "running").OrderBy(j => j.Created).Take(Math.Max(0, jobs.Count - 20)))
                if (jobs.TryRemove(old.Id, out var removed)) removed.Cancel.Dispose();
            return Results.Json(new { jobId = job.Id });
        });
        app.MapGet("/api/jobs", () => Results.Json(jobs.Values.OrderByDescending(j => j.Created).Select(j => new { j.Id, j.Operation, j.State, j.ExitCode, j.Error })));
        app.MapGet("/api/jobs/{id}", (string id) => jobs.TryGetValue(id, out var job)
            ? Results.Json(new { job.Id, job.Operation, job.State, job.ExitCode, job.Error, job.Report, job.Draft, job.Comparison }, JsonContract.Options)
            : Results.NotFound());
        app.MapPost("/api/jobs/{id}/cancel", (string id) =>
        {
            if (!jobs.TryGetValue(id, out var job)) return Results.NotFound();
            if (job.State == "running") job.Cancel.Cancel();
            return Results.Ok();
        });
        app.MapGet("/api/jobs/{id}/report/{format}", (string id, string format) =>
        {
            if (!jobs.TryGetValue(id, out var job)) return Results.NotFound();
            if (job.Comparison is { } comparison) return format switch
            {
                "json" => Results.Text(ComparisonWriter.Json(comparison), "application/json", Encoding.UTF8),
                "html" => Results.Text(ComparisonWriter.Html(comparison), "text/html", Encoding.UTF8),
                _ => Results.BadRequest()
            };
            if (job.Report is null) return Results.NotFound();
            return format switch
            {
                "json" => Results.Text(ReportWriter.Json(job.Report), "application/json", Encoding.UTF8),
                "html" => Results.Text(ReportWriter.Html(job.Report), "text/html", Encoding.UTF8),
                _ => Results.BadRequest()
            };
        });
        app.MapGet("/api/jobs/{id}/draft", (string id) => jobs.TryGetValue(id, out var job) && job.Draft is not null
            ? Results.Text(JsonSerializer.Serialize(job.Draft, JsonContract.Options), "application/json", Encoding.UTF8)
            : Results.NotFound());
    }

    private string RuleFile(string name)
    {
        if (Path.GetFileName(name) != name || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new ConfigurationException("Rule file must be a JSON filename in the explicit rules directory.");
        return PathSafety.Under(rulesDirectory, name);
    }
    private sealed class Job(string id, string operation)
    {
        public string Id { get; } = id;
        public string Operation { get; } = operation;
        public DateTimeOffset Created { get; } = DateTimeOffset.UtcNow;
        public CancellationTokenSource Cancel { get; } = new();
        private volatile string state = "running";
        public string State { get => state; set => state = value; }
        public int? ExitCode { get; set; }
        public string? Error { get; set; }
        public AnalysisReport? Report { get; set; }
        public RuleDraft? Draft { get; set; }
        public ComparisonReport? Comparison { get; set; }
    }
}
