using System.Net;
using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ArchSift.Web;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args is ["--version"]) { Console.WriteLine($"archsift-web {RuntimeInfo.ProductVersion}"); return 0; }
        if (args is ["__worker"])
        {
            try
            {
                var input = await Console.In.ReadToEndAsync();
                if (input.Length > 16 * 1024 * 1024) return 2;
                var request = JsonSerializer.Deserialize<WorkerRequest>(input, JsonContract.Options)!;
                Console.WriteLine(JsonSerializer.Serialize(AssemblyWorker.Evaluate(request), JsonContract.Options)); return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error.Message); return 3; }
        }
        var launcherRoot = args is ["--launcher", var root] ? root : null;
        var configFile = args is ["--config", var file] ? file : null;
        if (launcherRoot is null && configFile is null)
        { Console.Error.WriteLine("Local workbench: use --config <JSON>, --launcher <install-root> or --version."); return 2; }
        try
        {
            SessionProfile? profile = null;
            if (Environment.GetEnvironmentVariable("ARCHSIFT_UI_PROFILE_CONTROL") == "stdin-profile-v1")
            {
                var message = await Console.In.ReadLineAsync();
                if (message is null || !message.StartsWith("profile:", StringComparison.Ordinal) || message.Length > 16384)
                    throw new ConfigurationException("Missing or invalid private session profile.");
                profile = JsonSerializer.Deserialize<SessionProfile>(message[8..], JsonContract.Options);
            }
            RunConfiguration? config = null; string? rulesDirectory = null;
            if (configFile is not null)
            {
                configFile = Path.GetFullPath(configFile); config = ConfigLoader.Load(configFile);
                PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
                rulesDirectory = config.RulesDirectory ?? Path.Combine(config.Output.Directory, "rules");
                PathSafety.EnsureDisjoint(config.Target.Root, rulesDirectory);
                if (profile is not null && !profile.ConfigPath.Equals(configFile, PathSafety.Comparison)) throw new ConfigurationException("Session profile path mismatch.");
                profile ??= new(Path.GetFileName(configFile), configFile, false, false);
            }
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory,
                WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"), EnvironmentName = Environments.Production });
            builder.WebHost.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            builder.WebHost.ConfigureKestrel(server =>
            {
                server.Listen(IPAddress.Loopback, 0);
                server.Limits.MaxRequestBodySize = 4 * 1024 * 1024;
            });
            var app = builder.Build();
            // The owned pipe closes on CLI death as well as normal cancellation. Direct Web remains standalone.
            if (Environment.GetEnvironmentVariable("ARCHSIFT_UI_PARENT_CONTROL") == "stdin-v1")
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (await Console.In.ReadLineAsync() is { } command)
                            if (command == "shutdown") break;
                    }
                    catch (IOException) { }
                    app.Lifetime.StopApplication();
                });
            await using var launcher = launcherRoot is null ? null : new Launcher(launcherRoot);
            Workbench? workbench = null;
            if (launcher is not null) launcher.Map(app);
            else { workbench = new Workbench(config!, rulesDirectory!, profile); workbench.Map(app); }
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            Console.WriteLine("ARCHSIFT_UI=" + address + "/#session=" + (launcher?.Token ?? workbench!.Token));
            Console.WriteLine("ARCHSIFT_PID=" + Environment.ProcessId);
            Console.WriteLine("ARCHSIFT_STATE=waiting; reports are downloadable only after a job produces them; close safely in the UI or press Ctrl+C.");
            await app.WaitForShutdownAsync();
            if (workbench is not null) await workbench.DrainShutdownAsync();
            return 0;
        }
        catch (ConfigurationException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { Console.Error.WriteLine(error.Message); return 3; }
    }
}
