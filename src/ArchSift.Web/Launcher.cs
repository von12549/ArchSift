using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Hosting;
using ArchSift.Setup;

namespace ArchSift.Web;

public sealed class Launcher(string root) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string installRoot = SetupFiles.Safe(root);
    private FileStream? installationLease;
    private CancellationTokenSource? ownedCancellation;
    private Task<int>? ownedTask;
    private Uri? address;
    private SessionProfile? current;
    private string? childError;
    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private ProfileStore Store() => new(installRoot);

    public void Map(WebApplication app)
    {
        // An untrusted/broken installation still gets a read-only explanation page. No target JSON is loaded to host it.
        try { _ = Store(); installationLease = new(SetupFiles.Under(installRoot, ".archsift-setup.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (Exception error) when (error is IOException or ConfigurationException or UnauthorizedAccessException) { childError = error.Message; }
        app.Lifetime.ApplicationStopping.Register(() => ownedCancellation?.Cancel());
        app.Use(async (context, next) =>
        {
            var host = context.Request.Host;
            if (host.Host is not ("127.0.0.1" or "localhost") || host.Port != context.Connection.LocalPort) { context.Response.StatusCode = 421; return; }
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";
            context.Response.Headers["Referrer-Policy"] = "no-referrer"; context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (!context.Request.Path.StartsWithSegments("/api")) { await next(); return; }
            var origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && origin != "http://" + host.Value || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(context.Request.Headers["X-ArchSift-Token"].ToString()), Encoding.UTF8.GetBytes(Token)))
            { context.Response.StatusCode = origin.Length > 0 && origin != "http://" + host.Value ? 403 : 401; return; }
            try { await next(); }
            catch (Exception error) when (error is ConfigurationException or JsonException or IOException or UnauthorizedAccessException)
            { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = error.Message }); }
        });
        app.UseStaticFiles();
        app.MapGet("/", () => Results.File(Path.Combine(AppContext.BaseDirectory, "wwwroot", "launcher.html"), "text/html"));
        app.MapGet("/api/launcher/session", () => Results.Json(new { busy = ownedTask is { IsCompleted: false },
            session = ownedTask is { IsCompleted: false } ? current : null,
            workbenchUrl = ownedTask is { IsCompleted: false } ? address?.AbsoluteUri : null }, JsonContract.Options));
        app.MapGet("/api/launcher/state", () =>
        {
            try
            {
                var store = Store(); var catalog = store.ReadCatalog(); var selection = store.ReadSelection();
                var candidate = new ProfileEntry("", Path.GetFileName(store.Installation.ConfigPath), store.Installation.ConfigPath);
                return Results.Json(new { toolVersion = ToolIdentity.Version, installation = store.Installation,
                    catalogSha256 = store.CatalogHash, selectionSha256 = File.Exists(store.SelectionPath) ? SetupFiles.FileHash(store.SelectionPath) : null,
                    suggestedProfileId = selection?.SelectedProfileId ?? catalog.DefaultProfileId, profiles = catalog.Profiles.Select(store.Inspect).ToArray(),
                    managedCandidate = catalog.Profiles.Any(p => p.ConfigPath.Equals(candidate.ConfigPath, PathSafety.Comparison)) ? null : store.Inspect(candidate),
                    session = current, workbenchUrl = address?.AbsoluteUri, childError }, JsonContract.Options);
            }
            catch (Exception error) when (error is ConfigurationException or IOException or UnauthorizedAccessException or JsonException)
            { return Results.Json(new { toolVersion = ToolIdentity.Version, error = error.Message, childError }, JsonContract.Options); }
        });
        app.MapPost("/api/launcher/register", async (HttpContext context) =>
        {
            EnsureInstallationLease();
            using var doc = await Body(context, ["name", "configPath", "catalogSha256"]);
            var entry = Store().Register(Text(doc, "name"), Text(doc, "configPath"), NullableText(doc, "catalogSha256"));
            return Results.Json(entry, JsonContract.Options);
        });
        app.MapPost("/api/launcher/create", async (HttpContext context) =>
        {
            EnsureInstallationLease();
            using var doc = await Body(context, ["name", "configPath", "catalogSha256", "config"]);
            SchemaValidation.Validate(doc.RootElement.GetProperty("config"), "config");
            var entry = Store().Register(Text(doc, "name"), Text(doc, "configPath"), NullableText(doc, "catalogSha256"),
                doc.RootElement.GetProperty("config").Deserialize<RunConfiguration>(JsonContract.Options)!);
            return Results.Json(entry, JsonContract.Options);
        });
        app.MapPost("/api/launcher/preview", async (HttpContext context) =>
        {
            using var doc = await Body(context, ["configPath", "config"]);
            SchemaValidation.Validate(doc.RootElement.GetProperty("config"), "config");
            return Results.Json(Store().Preview(Text(doc, "configPath"), doc.RootElement.GetProperty("config").Deserialize<RunConfiguration>(JsonContract.Options)!), JsonContract.Options);
        });
        app.MapPost("/api/launcher/start", async (HttpContext context) =>
        {
            EnsureInstallationLease();
            using var doc = await Body(context, ["profileId", "catalogSha256", "configSha256", "selectionSha256"]);
            await gate.WaitAsync(context.RequestAborted);
            var startedNew = false;
            try
            {
                if (ownedTask is { IsCompleted: false }) throw new ConfigurationException("Close the current workbench safely before switching profiles.");
                var store = Store();
                if (store.CatalogHash != NullableText(doc, "catalogSha256")) throw new ConfigurationException("Catalog changed; reload before starting.");
                var profile = store.ReadCatalog().Profiles.SingleOrDefault(p => p.ProfileId == Text(doc, "profileId")) ?? throw new ConfigurationException("Missing registered profile.");
                var health = store.Inspect(profile);
                if (health.Status != "valid" || health.Sha256 != Text(doc, "configSha256")) throw new ConfigurationException(health.Error ?? "Configuration changed; reload and review.");
                var version = store.Installation.Versions.Single(v => v.Version == store.Installation.SelectedVersion);
                ownedCancellation?.Dispose(); ownedCancellation = new();
                var ready = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
                address = null; current = health.Session; childError = null;
                var cancellation = ownedCancellation.Token;
                ownedTask = Task.Run(async () =>
                {
                    try
                    {
                        var code = await UiProcess.RunArgumentsAsync(SetupFiles.Under(version.Directory, "web/ArchSift.Web.exe"), null,
                            ["--config", profile.ConfigPath], TextWriter.Null, TextWriter.Null, cancellation,
                            onAddress: uri => ready.TrySetResult(uri), profile: health.Session);
                        if (code != 0) childError = "Workbench exited with code " + code;
                        if (!ready.Task.IsCompleted) ready.TrySetException(new IOException("Workbench exited before readiness."));
                        return code;
                    }
                    catch (Exception error) { childError = "Workbench startup or shutdown failed; review the selected configuration."; ready.TrySetException(new IOException(childError)); return error is OperationCanceledException ? 130 : 3; }
                    finally { address = null; current = null; }
                });
                startedNew = true;
                var next = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), context.RequestAborted);
                if (SetupFiles.FileHash(profile.ConfigPath) != health.Sha256 || ownedTask.IsCompleted)
                    throw new ConfigurationException("Configuration changed or workbench exited during startup.");
                store.Select(profile.ProfileId, NullableText(doc, "selectionSha256"));
                address = next; return Results.Json(new { workbenchUrl = next.AbsoluteUri, profile = health.Session }, JsonContract.Options);
            }
            catch { if (startedNew) { ownedCancellation?.Cancel(); if (ownedTask is not null) await ownedTask; } throw; }
            finally { gate.Release(); }
        });
        app.MapPost("/api/launcher/stop", async (HttpContext context) =>
        {
            using var doc = await Body(context, ["discardChanges"]);
            await gate.WaitAsync(context.RequestAborted);
            try { await StopAsync(doc.RootElement.TryGetProperty("discardChanges", out var discard) && discard.ValueKind == JsonValueKind.True); return Results.Ok(); }
            finally { gate.Release(); }
        });
        app.MapPost("/api/launcher/shutdown", async (HttpContext context) =>
        {
            using var doc = await Body(context, []);
            if (ownedTask is { IsCompleted: false }) throw new ConfigurationException("Close the current workbench safely before closing the launcher.");
            _ = Task.Run(async () => { await Task.Delay(100); app.Lifetime.StopApplication(); }); return Results.Accepted();
        });
    }
    private void EnsureInstallationLease() { if (installationLease is null) throw new ConfigurationException(childError ?? "Installation is unavailable or busy."); }
    private async Task StopAsync(bool discardChanges)
    {
        if (ownedTask is not { IsCompleted: false }) return;
        if (address is null) throw new ConfigurationException("Workbench is still starting; wait for readiness.");
        using var client = new HttpClient { BaseAddress = new(address.GetLeftPart(UriPartial.Authority)), Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Add("X-ArchSift-Token", address.Fragment[9..]);
        var stateResponse = await client.GetAsync("/api/session");
        if (stateResponse.IsSuccessStatusCode)
        {
            using var state = JsonDocument.Parse(await stateResponse.Content.ReadAsStringAsync());
            if (state.RootElement.GetProperty("running").GetBoolean()) throw new ConfigurationException("A job is running. Cancel it in the workbench or wait for completion.");
            if (state.RootElement.GetProperty("dirty").GetBoolean() && !discardChanges)
                throw new ConfigurationException("Unsaved changes remain. Export/save them in the workbench or explicitly discard them.");
        }
        else if (!discardChanges) throw new ConfigurationException("This older workbench cannot report unsaved edits; review it and explicitly confirm closing.");
        using var response = await client.PostAsync("/api/shutdown", new StringContent(JsonSerializer.Serialize(new { discardChanges }), Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode) throw new ConfigurationException("Workbench refused shutdown; finish its job or review unsaved edits.");
        await ownedTask.WaitAsync(TimeSpan.FromSeconds(15));
    }
    public async ValueTask DisposeAsync()
    {
        ownedCancellation?.Cancel(); if (ownedTask is not null) await ownedTask;
        ownedCancellation?.Dispose(); installationLease?.Dispose(); gate.Dispose();
    }
    private static async Task<JsonDocument> Body(HttpContext context, string[] allowed)
    {
        var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        try
        {
            SetupFiles.CheckKeys(doc.RootElement);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || doc.RootElement.EnumerateObject().Any(p => !allowed.Contains(p.Name, StringComparer.Ordinal)))
                throw new ConfigurationException("Unsupported launcher request fields.");
            return doc;
        }
        catch { doc.Dispose(); throw; }
    }
    private static string Text(JsonDocument doc, string name) => NullableText(doc, name) is { Length: > 0 } value ? value : throw new ConfigurationException("Missing " + name);
    private static string? NullableText(JsonDocument doc, string name)
    {
        if (!doc.RootElement.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new ConfigurationException("Expected text for " + name);
        return value.GetString();
    }
}
