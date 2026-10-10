using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class ChainCommandTests
{
    [Fact]
    public async Task CliChainMatchesCoreAndLegacyCompositionStillRejectsDuplicateIds()
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-chain-cli-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source"); Directory.CreateDirectory(source);
        try
        {
            File.WriteAllText(Path.Combine(source, "Good.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = source }, Output = new() { Directory = Path.Combine(root, "reports") }, RulesDirectory = Path.Combine(root, "rules") };
            var library = new RulesetLibrary(config.RulesDirectory, source);
            var first = library.Import("first.json", Encoding.UTF8.GetBytes(RuleTemplates.Json("naming")));
            var second = library.Import("second.json", Encoding.UTF8.GetBytes(RuleTemplates.Json("naming").Replace("template-naming", "second-policy", StringComparison.Ordinal)));
            var chain = new ChainDocument(1, "cli", "1", "CLI parity", [new(first.EntryId), new(second.EntryId)]);
            var chainPath = Path.Combine(root, "chain.json"); File.WriteAllText(chainPath, JsonSerializer.Serialize(chain, JsonContract.Options));
            var configPath = Path.Combine(root, "config.json"); File.WriteAllText(configPath, JsonSerializer.Serialize(config with { Rulesets = [first.Identity!.Path, second.Identity!.Path] }, JsonContract.Options));
            var repo = new DirectoryInfo(AppContext.BaseDirectory); while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "ArchSift.slnx"))) repo = repo.Parent;
            var build = typeof(ChainCommandTests).Assembly.GetCustomAttributes(false).OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
            var cli = Path.Combine(repo!.FullName, "src", "ArchSift.Cli", "bin", build, "net10.0", "archsift.dll");
            Task<ProcessResult> Run(params string[] args) => SafeProcess.RunAsync(SafeProcess.Dotnet, repo.FullName, [cli, .. args], Path.Combine(root, "cli-home"), null, CancellationToken.None);
            var result = await Run("chain", "verify", "--config", configPath, "--chain", chainPath, "--target-kind", "fixture");
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("preparing", result.Error); Assert.Contains("finished", result.Error);
            var actual = JsonSerializer.Deserialize<ChainSummary>(result.Output, JsonContract.Options)!;
            var expected = await new ChainService(new AnalysisService()).RunAsync(config, chain, library, "fixture");
            Assert.Equal(expected.Compliance, actual.Compliance); Assert.Equal("fixture", actual.Snapshot.TargetKind);
            Assert.Equal(expected.Entries.SelectMany(e => e.Findings).Select(f => f.Id), actual.Entries.SelectMany(e => e.Findings).Select(f => f.Id));
            foreach (var count in new[] { 1, 2, 3, 4 })
            {
                var concurrent = await Run("chain", "verify", "--config", configPath, "--chain", chainPath, "--max-concurrency", count.ToString());
                Assert.Equal(0, concurrent.ExitCode);
                Assert.Equal(count, JsonSerializer.Deserialize<ChainSummary>(concurrent.Output, JsonContract.Options)!.Snapshot.ExecutionOptions!.MaxConcurrency);
            }
            foreach (var invalid in new[] { "0", "5", "1.5", "invalid" })
                Assert.Equal(2, (await Run("chain", "verify", "--config", configPath, "--chain", chainPath, "--max-concurrency", invalid)).ExitCode);
            Assert.Equal(2, (await Run("chain", "verify", "--config", configPath, "--chain", chainPath, "--max-concurrency", "1", "--max-concurrency", "2")).ExitCode);
            Assert.Equal(2, (await Run("verify", "--config", configPath)).ExitCode);
            library.Delete(first.EntryId);
            Assert.Equal(4, (await Run("chain", "verify", "--config", configPath, "--chain", chainPath)).ExitCode);
            File.WriteAllText(configPath, JsonSerializer.Serialize(config with { Target = new() { Root = Path.Combine(root, "absent") } }, JsonContract.Options));
            Assert.Equal(2, (await Run("chain", "verify", "--config", configPath, "--chain", chainPath)).ExitCode);
        }
        finally { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); Directory.Delete(root, true); }
    }
}
