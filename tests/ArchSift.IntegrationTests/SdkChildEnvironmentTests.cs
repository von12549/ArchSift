using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class SdkChildEnvironmentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("false")]
    [InlineData("true")]
    public async Task SafeProcessPinsEffectiveSdkBooleanAfterCallerOverrides(string? supplied)
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-sdk-environment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var cliHome = Path.Combine(root, "home");
        var parentFlag = Environment.GetEnvironmentVariable("DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE");
        var parentHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
        var parentTools = Environment.GetEnvironmentVariable("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH");
        var pathHash = Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(Environment.GetEnvironmentVariable("PATH") ?? "")));
        const string script = """
            $ErrorActionPreference='Stop'
            $disabled=$false
            $parsed=[bool]::TryParse($env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE,[ref]$disabled)
            [Console]::Out.Write((@{parsed=$parsed;disabled=$disabled;value=$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE;home=$env:DOTNET_CLI_HOME;tools=$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH;pathHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::Unicode.GetBytes($env:PATH)))}|ConvertTo-Json -Compress))
            """;
        var overrides = supplied is null ? null : new Dictionary<string, string>
        {
            ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = supplied,
            ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "1",
            ["DOTNET_CLI_HOME"] = Path.Combine(root, "unapproved-home")
        };
        try
        {
            var result = await SafeProcess.RunAsync("pwsh", root,
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script], cliHome, null,
                CancellationToken.None, overrides, TimeSpan.FromSeconds(30));
            Assert.Equal(0, result.ExitCode);
            using var value = JsonDocument.Parse(result.Output);
            Assert.True(value.RootElement.GetProperty("parsed").GetBoolean());
            Assert.True(value.RootElement.GetProperty("disabled").GetBoolean());
            Assert.Equal("true", value.RootElement.GetProperty("value").GetString());
            Assert.Equal(cliHome, value.RootElement.GetProperty("home").GetString());
            Assert.Equal("0", value.RootElement.GetProperty("tools").GetString());
            Assert.Equal(pathHash, value.RootElement.GetProperty("pathHash").GetString());
            Assert.Equal(parentFlag, Environment.GetEnvironmentVariable("DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"));
            Assert.Equal(parentHome, Environment.GetEnvironmentVariable("DOTNET_CLI_HOME"));
            Assert.Equal(parentTools, Environment.GetEnvironmentVariable("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"));
        }
        finally
        {
            Assert.True(PathSafety.IsUnder(root, Path.GetTempPath()));
            Directory.Delete(root, true);
        }
    }
}
