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
        if (args is not ["--config", var configFile])
        { Console.Error.WriteLine("Local workbench: use --config <JSON> or --version."); return 2; }
        try
        {
            var config = ConfigLoader.Load(configFile);
            PathSafety.EnsureDisjoint(config.Target.Root, config.Output.Directory);
            var rulesDirectory = config.RulesDirectory ?? Path.Combine(config.Output.Directory, "rules");
            PathSafety.EnsureDisjoint(config.Target.Root, rulesDirectory);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
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
            var workbench = new Workbench(config, rulesDirectory); workbench.Map(app);
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            Console.WriteLine("ARCHSIFT_UI=" + address + "/#session=" + workbench.Token);
            Console.WriteLine("ARCHSIFT_PID=" + Environment.ProcessId);
            Console.WriteLine("ARCHSIFT_STATE=waiting; reports are downloadable only after a job produces them; close safely in the UI or press Ctrl+C.");
            await app.WaitForShutdownAsync();
            await workbench.DrainShutdownAsync();
            return 0;
        }
        catch (ConfigurationException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { Console.Error.WriteLine(error.Message); return 3; }
    }
}
