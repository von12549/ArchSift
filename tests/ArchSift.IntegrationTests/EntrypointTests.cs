using System.Diagnostics;
using System.Reflection;
using ArchSift.Contracts;

namespace ArchSift.IntegrationTests;

public sealed class EntrypointTests
{
    [Theory]
    [InlineData("ArchSift.Cli", "archsift", "archsift")]
    [InlineData("ArchSift.Web", "ArchSift.Web", "archsift-web")]
    public async Task VersionEntrypointsReportTheSharedVersion(string project, string assembly, string name)
    {
        var result = await Run(project, assembly, "--version");
        Assert.Equal(0, result.Code);
        Assert.Equal($"{name} {ToolIdentity.Version}", result.Output.Trim());
        Assert.Empty(result.Error);
    }

    [Fact]
    public async Task UnimplementedCliCommandFailsWithConfigurationError()
    {
        var result = await Run("ArchSift.Cli", "archsift", "not-implemented-command");
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Output);
        Assert.Contains("Unsupported command", result.Error);
    }

    [Fact]
    public async Task HelpDoesNotClaimAnalysisWasPerformed()
    {
        var result = await Run("ArchSift.Cli", "archsift", "--help");
        Assert.Equal(0, result.Code);
        Assert.Contains("changes --config", result.Output);
        Assert.DoesNotContain("已通过", result.Output);
        Assert.Contains("--version", result.Output);
        Assert.Empty(result.Error);
    }

    [Fact]
    public async Task WebWithoutVersionDoesNotStartAnUnimplementedServer()
    {
        var result = await Run("ArchSift.Web", "ArchSift.Web");
        Assert.Equal(2, result.Code);
        Assert.Contains("Local workbench", result.Error);
    }

    private static async Task<(int Code, string Output, string Error)> Run(
        string project, string assembly, params string[] arguments)
    {
        var root = FindRoot();
        var configuration = typeof(EntrypointTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var start = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false
        };
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        start.ArgumentList.Add(Path.Combine(root, "src", project, "bin", configuration, "net10.0", assembly + ".dll"));
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        return (process.ExitCode, await output, await error);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("ArchSift.slnx not found.");
    }
}
