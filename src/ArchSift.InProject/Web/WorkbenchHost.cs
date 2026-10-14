using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.InProject.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArchSift.InProject;

internal static class WorkbenchHost
{
    private sealed record GateRequest(string EvidenceGrade);
    private sealed record CancelRequest(string RunId);
    private sealed record SaveRulesRequest(string Name, string ExpectedSha256, string Content);

    public static async Task RunAsync(string root, int port)
    {
        _ = InProjectConfiguration.Load(root);
        var session = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:" + port);
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        string? origin = null;
        app.Use(async (context, next) =>
        {
            var bodyLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodyLimit is not null && !bodyLimit.IsReadOnly)
                bodyLimit.MaxRequestBodySize = RuleLoader.MaximumJsonBytes + 4096;
            var expectedHost = origin is null ? null : new Uri(origin).Authority;
            if (expectedHost is null || !string.Equals(context.Request.Host.Value, expectedHost,
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; " +
                "img-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
            if (context.Request.Method is not ("GET" or "POST") ||
                context.Request.Method == "POST" &&
                (!string.Equals(context.Request.Headers.Origin, origin, StringComparison.Ordinal) ||
                 !string.Equals(context.Request.Headers["X-ArchSift-Session"], session,
                     StringComparison.Ordinal) ||
                 context.Request.ContentType?.StartsWith("application/json",
                     StringComparison.OrdinalIgnoreCase) != true ||
                 context.Request.ContentLength > RuleLoader.MaximumJsonBytes + 4096))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (context.Request.Path == "/" &&
                string.Equals(context.Request.Query["session"], session, StringComparison.Ordinal))
            {
                context.Response.Cookies.Append("archsift_session", session,
                    new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict,
                        Secure = false, Path = "/" });
                context.Response.Redirect("/");
                return;
            }
            if (!string.Equals(context.Request.Cookies["archsift_session"], session,
                    StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next();
        });
        app.MapGet("/", () => Results.Content(LoadPage(), "text/html; charset=utf-8"));
        app.MapGet("/api/session", () => Results.Json(new { token = session }, JsonContract.Options));
        app.MapGet("/workbench.css", () => Results.Content(LoadResource("workbench.css"),
            "text/css; charset=utf-8"));
        app.MapGet("/workbench.js", () => Results.Content(LoadResource("workbench.js"),
            "text/javascript; charset=utf-8"));
        app.MapGet("/api/state", () =>
        {
            var config = InProjectConfiguration.Load(root);
            InProjectTaskState? current;
            try { current = InProjectTaskService.Status(root); }
            catch (ConfigurationException error) when (error.Message.Contains("not found",
                StringComparison.OrdinalIgnoreCase)) { current = null; }
            var directory = Path.Combine(config.InstallationRoot, "state", "tasks");
            PathSafety.EnsureNoLinks(directory);
            var ids = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc).Take(20)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(id => Guid.TryParseExact(id, "N", out _)).ToArray()
                : [];
            var history = ids.Select(id =>
            {
                var path = Path.Combine(directory, id + ".json");
                PathSafety.EnsureNoLinks(path);
                return JsonSerializer.Deserialize<InProjectTaskState>(File.ReadAllBytes(path),
                    JsonContract.Options);
            }).Where(state => state?.Product == "ArchSift.InProject")
                .Select(state => state!.RunId == current?.RunId ? current :
                    state with { Freshness = "historical" }).ToArray();
            return Results.Json(new
            {
                product = "ArchSift.InProject", version = LocalPackage.VerifyInstalled(root),
                target = config.SolutionPath, worktree = config.Root,
                worktreeId = ContentHash.Text(config.Root), config.Configuration,
                contexts = config.Contexts, current, history,
                rulesSha256 = config.RulesSha256, toolSha256 = config.ToolSha256
            }, JsonContract.Options);
        });
        app.MapGet("/api/rules", () =>
        {
            var config = InProjectConfiguration.Load(root);
            return Results.Json(config.Rules.Select(item => new
            {
                name = Path.GetRelativePath(config.InstallationRoot, item.Identity.Path)
                    .Replace('\\', '/'),
                sha256 = item.Identity.Sha256,
                content = File.ReadAllText(item.Identity.Path)
            }).ToArray(), JsonContract.Options);
        });
        app.MapPost("/api/rules", async (HttpContext context) =>
        {
            var request = await Read<SaveRulesRequest>(context);
            if (request is null || string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.ExpectedSha256) || request.Content is null)
                return Results.BadRequest(new { error = "规则提交不完整。" });
            var config = InProjectConfiguration.Load(root);
            var selected = config.Rules.SingleOrDefault(item =>
                Path.GetRelativePath(config.InstallationRoot, item.Identity.Path)
                    .Replace('\\', '/') == request.Name);
            if (selected is null) return Results.BadRequest(new { error = "规则文件未登记。" });
            var leasePath = Path.Combine(config.InstallationRoot, "state", "task-lease.lock");
            PathSafety.EnsureNoLinks(leasePath);
            FileStream lease;
            try { lease = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { return Results.Conflict(new { error = "门禁运行中，规则暂不能保存。" }); }
            using (lease)
            {
                PathSafety.EnsureNoLinks(selected.Identity.Path);
                if (LocalPackage.HashFile(selected.Identity.Path) != request.ExpectedSha256)
                    return Results.Conflict(new { error = "规则已在别处修改，请重新载入。" });
                var bytes = new UTF8Encoding(false).GetBytes(request.Content);
                var parsed = RuleLoader.Parse(bytes);
                RuleLoader.Compose(config.Rules.Select(item => item == selected
                    ? new LoadedRuleset(parsed, new(parsed.Id, parsed.Version,
                        ContentHash.Bytes(bytes), item.Identity.Path)) : item));
                var temp = selected.Identity.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                PathSafety.EnsureNoLinks(temp);
                try
                {
                    await File.WriteAllBytesAsync(temp, bytes);
                    File.Move(temp, selected.Identity.Path, true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
                return Results.Json(new { saved = true, sha256 = ContentHash.Bytes(bytes) },
                    JsonContract.Options);
            }
        });
        app.MapPost("/api/gate", async (HttpContext context) =>
        {
            var request = await Read<GateRequest>(context);
            if (request?.EvidenceGrade is not ("daily" or "full"))
                return Results.BadRequest(new { error = "证据等级须为 daily 或 full。" });
            _ = Task.Run(async () =>
            {
                try { await InProjectTaskService.GateAsync(root, request.EvidenceGrade); }
                catch (Exception error) { Console.Error.WriteLine("Workbench task: " + error.Message); }
            });
            return Results.Accepted("/api/state", new { accepted = true,
                evidenceGrade = request.EvidenceGrade });
        });
        app.MapPost("/api/cancel", async (HttpContext context) =>
        {
            var request = await Read<CancelRequest>(context);
            if (request is null) return Results.BadRequest(new { error = "缺少运行编号。" });
            return Results.Json(InProjectTaskService.Cancel(root, request.RunId), JsonContract.Options);
        });
        app.MapGet("/api/report/{runId}", (string runId) =>
        {
            var state = InProjectTaskService.Status(root, runId);
            if (state.MaterialsState != "available" || state.ResultPath is null)
                return Results.NotFound();
            var config = InProjectConfiguration.Load(root);
            var path = Path.GetFullPath(state.ResultPath.Replace('/', Path.DirectorySeparatorChar),
                config.InstallationRoot);
            if (!PathSafety.IsUnder(path, Path.Combine(config.InstallationRoot, "reports")) ||
                !path.EndsWith(Path.Combine(runId, "gate-result.json"), StringComparison.Ordinal) ||
                !File.Exists(path)) return Results.NotFound();
            PathSafety.EnsureNoLinks(path);
            return Results.File(path, "application/json");
        });
        app.MapGet("/api/retention", () => Results.Json(
            InProjectRetentionService.Preview(root), JsonContract.Options));
        app.MapGet("/api/evidence/{runId}", (string runId) =>
        {
            var state = InProjectTaskService.Status(root, runId);
            var config = InProjectConfiguration.Load(root);
            var path = Path.Combine(config.EvidenceRoot, runId, "manifest.json");
            PathSafety.EnsureNoLinks(path);
            return Results.Json(new { state.RunId, state.EvidenceGrade, state.MaterialsState,
                state.ArchivePath, fullManifestAvailable = state.EvidenceGrade == "full" &&
                    File.Exists(path) }, JsonContract.Options);
        });
        app.MapGet("/api/evidence/{runId}/manifest", (string runId) =>
        {
            var state = InProjectTaskService.Status(root, runId);
            if (state.EvidenceGrade != "full" || state.MaterialsState != "available")
                return Results.NotFound();
            var config = InProjectConfiguration.Load(root);
            var path = Path.Combine(config.EvidenceRoot, runId, "manifest.json");
            PathSafety.EnsureNoLinks(path);
            return File.Exists(path) ? Results.File(path, "application/json") : Results.NotFound();
        });
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = context.Features.Get<IExceptionHandlerFeature>()
                ?.Error is ConfigurationException or InvalidDataException or JsonException
                ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "请求失败，请检查输入或任务状态。" });
        }));
        await app.StartAsync();
        var addresses = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()?.Addresses;
        origin = addresses?.Single() ?? throw new InvalidOperationException("Workbench address unavailable.");
        Console.WriteLine(origin + "/?session=" + session);
        await app.WaitForShutdownAsync();
    }

    private static async Task<T?> Read<T>(HttpContext context) =>
        await JsonSerializer.DeserializeAsync<T>(context.Request.Body, JsonContract.Options);

    private static string LoadPage() => LoadResource("workbench.html");
    private static string LoadResource(string name)
    {
        using var stream = typeof(WorkbenchHost).Assembly.GetManifestResourceStream(
            "ArchSift.InProject." + name) ?? throw new InvalidOperationException("Workbench asset missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
