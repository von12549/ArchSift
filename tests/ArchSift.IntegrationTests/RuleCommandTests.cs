using System.Diagnostics;

namespace ArchSift.IntegrationTests;

public sealed class RuleCommandTests
{
    [Theory]
    [InlineData("project-reference")]
    [InlineData("project-reference-allowlist")]
    [InlineData("graph-integrity")]
    [InlineData("target-framework")]
    [InlineData("nuget-denylist")]
    [InlineData("nuget-allowlist")]
    [InlineData("type-dependency")]
    [InlineData("naming")]
    public async Task CliValidatesAndRendersEveryTemplateWithoutMutatingTheSource(string type)
    {
        var root = FindRoot();
        var rules = Path.Combine(root, "templates", "rules", type + ".json");
        var before = File.ReadAllBytes(rules);
        var output = Path.Combine(Path.GetTempPath(), "archsift-rule-" + Guid.NewGuid().ToString("N") + ".md");
        try
        {
            Assert.Equal(0, await Run(root, "rules", "validate", "--file", rules));
            Assert.Equal(0, await Run(root, "rules", "render", "--file", rules, "--output", output));
            Assert.Contains("SHA-256", File.ReadAllText(output));
            File.AppendAllText(output, "\nEdited reading projection.");
            Assert.Equal(0, await Run(root, "rules", "validate", "--file", rules));
            Assert.Equal(before, File.ReadAllBytes(rules));
            Assert.Equal(2, await Run(root, "rules", "render", "--file", rules, "--output", rules));
            Assert.Equal(before, File.ReadAllBytes(rules));
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }

    [Fact]
    public async Task InvalidRuleConfigurationHasExitTwo()
    {
        var file = Path.Combine(Path.GetTempPath(), "archsift-invalid-" + Guid.NewGuid().ToString("N") + ".json");
        try { File.WriteAllText(file, "{}"); Assert.Equal(2, await Run(FindRoot(), "rules", "validate", "--file", file)); }
        finally { File.Delete(file); }
    }

    private static async Task<int> Run(string root, params string[] args)
    {
        var start = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0";
        var configuration = typeof(RuleCommandTests).Assembly.GetCustomAttributes(false)
            .OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
        start.ArgumentList.Add(Path.Combine(root, "src", "ArchSift.Cli", "bin", configuration, "net10.0", "archsift.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw; }
        await stdout; await stderr;
        return process.ExitCode;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
