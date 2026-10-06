using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class AnalysisCommandTests
{
    [Fact]
    public async Task CliAnalyzeVerifyDraftAndOverridesUseTheSameContracts()
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-cli-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source"); var output = Path.Combine(root, "output");
        Directory.CreateDirectory(source);
        try
        {
            File.WriteAllText(Path.Combine(source, "Good.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var config = Path.Combine(root, "config.json"); var rules = Path.Combine(root, "rules.json");
            var set = new Ruleset
            {
                SchemaVersion = 1, Id = "cli", Version = "1", Description = "CLI parity", Exceptions = [],
                Rules = [new() { Id = "name", Type = "naming", Enabled = true, Scope = new() { Kind = "project", Match = "glob", Value = "**" },
                    Severity = "warning", Reason = "Naming rule", Parameters = JsonSerializer.SerializeToElement(new
                    { subjectKind = "project", requiredName = new { match = "exact", value = "Wrong" } }) }]
            };
            File.WriteAllText(rules, JsonSerializer.Serialize(set, JsonContract.Options), new UTF8Encoding(false));
            File.WriteAllText(config, JsonSerializer.Serialize(new RunConfiguration
            {
                SchemaVersion = 1, Target = new() { Root = "source" }, Rulesets = ["rules.json"], Output = new() { Directory = "output", Formats = ["json", "html", "sarif"] }
            }, JsonContract.Options), new UTF8Encoding(false));
            var analyze = await Run(root, "analyze", "--config", config);
            Assert.Equal(0, analyze.ExitCode);
            var report = JsonSerializer.Deserialize<AnalysisReport>(analyze.Output, JsonContract.Options)!;
            Assert.Null(report.Compliance); Assert.Empty(report.RuleResults);
            var verify = await Run(root, "verify", "--config", config);
            Assert.Equal(0, verify.ExitCode);
            report = JsonSerializer.Deserialize<AnalysisReport>(verify.Output, JsonContract.Options)!;
            Assert.Equal("noncompliant", report.Compliance);
            Assert.Contains("生效目标", verify.Error);
            Assert.True(File.Exists(Path.Combine(output, report.RunMetadata.RunId, "report.html")));
            Assert.Equal("2.1.0", JsonNode.Parse(File.ReadAllText(Path.Combine(output, report.RunMetadata.RunId, "report.sarif")))!["version"]!.GetValue<string>());
            var direct = await new AnalysisService().RunAsync(ConfigLoader.Load(config), "verify");
            Assert.Equal(direct.Report.Findings.Select(f => f.Id), report.Findings.Select(f => f.Id));
            Assert.Equal(0, (await Run(root, "analyze", "--config", config, "--configuration", "Debug", "--tfm", "net10.0")).ExitCode);
            Assert.Equal(0, (await Run(root, "rules", "draft", "--config", config, "--output", Path.Combine(root, "draft"))).ExitCode);
            var candidate = Directory.GetFiles(Path.Combine(root, "draft"), "candidate-rules.json", SearchOption.AllDirectories).Single();
            var loaded = RuleLoader.Load(candidate); Assert.All(loaded.Ruleset.Rules, r => Assert.False(r.Enabled));
            Assert.Equal(2, (await Run(root, "verify", "--config", config, "--bogus", "x")).ExitCode);

            var node = JsonNode.Parse(File.ReadAllText(rules))!; node["rules"]![0]!["scope"]!["value"] = "Absent.csproj";
            File.WriteAllText(rules, node.ToJsonString());
            Assert.Equal(4, (await Run(root, "verify", "--config", config)).ExitCode);
            Assert.Equal(3, (await Run(root, "analyze", "--config", Path.Combine(root, "missing.json"))).ExitCode);
            Assert.False(Directory.Exists(Path.Combine(source, "bin"))); Assert.False(Directory.Exists(Path.Combine(source, "obj")));
        }
        finally { Directory.Delete(root, true); }
    }

    private static Task<ProcessResult> Run(string root, params string[] args)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
        var configuration = typeof(AnalysisCommandTests).Assembly.GetCustomAttributes(false)
            .OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
        var cli = Path.Combine(directory!.FullName, "src", "ArchSift.Cli", "bin", configuration, "net10.0", "archsift.dll");
        return SafeProcess.RunAsync(SafeProcess.Dotnet, directory.FullName, [cli, .. args], Path.Combine(root, "cli-home"), null, CancellationToken.None);
    }
}
