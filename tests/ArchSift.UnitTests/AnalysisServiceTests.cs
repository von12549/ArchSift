using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class AnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeDoesNotRequireRulesAndReportsCurrentScope()
    {
        using var fixture = new Fixture();
        var result = await new AnalysisService().RunAsync(fixture.Config, "analyze");
        Assert.Equal(0, result.ExitCode); Assert.Null(result.Report.Compliance);
        Assert.Equal(1, result.Report.Coverage.ProjectCount);
        Assert.Empty(result.Report.RuleResults);
        SchemaValidation.Validate(JsonDocument.Parse(ReportWriter.Json(result.Report)).RootElement, "report");
    }

    [Fact]
    public async Task ViolationDoesNotBlockButZeroMatchAndDisabledAreDifferent()
    {
        using var fixture = new Fixture();
        fixture.Rules("Wrong", false);
        var violation = await new AnalysisService().RunAsync(fixture.Config, "verify");
        Assert.Equal(0, violation.ExitCode); Assert.Equal("noncompliant", violation.Report.Compliance);
        fixture.Rules("Project", true);
        var empty = await new AnalysisService().RunAsync(fixture.Config, "verify");
        Assert.Equal(4, empty.ExitCode); Assert.Equal("inconclusive", empty.Report.Compliance);
        fixture.Rules("Project", false, enabled: false);
        var disabled = await new AnalysisService().RunAsync(fixture.Config, "verify");
        Assert.Equal("not-applicable", disabled.Report.Compliance);
    }

    [Fact]
    public async Task StableFindingsAreIndependentOfRunIdsAndHtmlEscapesEverything()
    {
        using var fixture = new Fixture(); fixture.Rules("Wrong", false);
        var a = await new AnalysisService().RunAsync(fixture.Config, "verify");
        var b = await new AnalysisService().RunAsync(fixture.Config, "verify");
        Assert.NotEqual(a.Report.RunMetadata.RunId, b.Report.RunMetadata.RunId);
        Assert.Equal(a.Report.Findings.Select(f => f.Id), b.Report.Findings.Select(f => f.Id));
        Assert.Equal(a.Report.InputIdentity, b.Report.InputIdentity, new IdentityComparer());
        var evil = a.Report with { Findings = [a.Report.Findings[0] with { Subject = "<script>alert(1)</script>" }] };
        var html = ReportWriter.Html(evil);
        Assert.DoesNotContain("<script>", html); Assert.Contains("&lt;script&gt;", html);
        ReportWriter.Save(fixture.Config, a.Report);
        Assert.True(File.Exists(Path.Combine(fixture.Output, a.Report.RunMetadata.RunId, "report.json")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Source, "obj")));
    }

    [Fact]
    public async Task CancellationCannotDisplayPreviousSuccessAndHasExit130()
    {
        using var fixture = new Fixture(); fixture.Rules("Project", false);
        var passed = await new AnalysisService().RunAsync(fixture.Config, "verify");
        Assert.Equal("compliant", passed.Report.Compliance);
        var cancelled = await new AnalysisService().RunAsync(fixture.Config, "verify", new CancellationToken(true));
        Assert.Equal(130, cancelled.ExitCode); Assert.Equal("cancelled", cancelled.Report.Execution);
        Assert.Equal("inconclusive", cancelled.Report.Compliance); Assert.Empty(cancelled.Report.Findings);
        Assert.NotEqual(passed.Report.RunMetadata.RunId, cancelled.Report.RunMetadata.RunId);
    }

    [Fact]
    public async Task AssemblyFailureRetainsFinishedProjectResultsAndReturnsThree()
    {
        using var fixture = new Fixture(); fixture.Rules("Wrong", false, includeAssemblyRule: true);
        var service = new AnalysisService((_, _, _, _) => throw new IOException("Synthetic worker failure"));
        var result = await service.RunAsync(fixture.Config, "verify");
        Assert.Equal(3, result.ExitCode); Assert.Equal("partial", result.Report.Execution);
        Assert.Contains(result.Report.RuleResults, r => r.RuleId == "naming" && r.Status == "violation");
        Assert.Contains(result.Report.RuleResults, r => r.RuleId == "types" && r.Status == "inconclusive");
        Assert.NotEmpty(result.Report.ExecutionErrors);
    }

    private sealed class IdentityComparer : IEqualityComparer<InputIdentity>
    {
        public bool Equals(InputIdentity? a, InputIdentity? b) => a?.Sha256 == b?.Sha256;
        public int GetHashCode(InputIdentity value) => value.Sha256.GetHashCode(StringComparison.Ordinal);
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "archsift-service-" + Guid.NewGuid().ToString("N"));
        public string Source => Path.Combine(root, "source");
        public string Output => Path.Combine(root, "output");
        private string RulesFile => Path.Combine(root, "rules.json");
        public RunConfiguration Config => new()
        { SchemaVersion = 1, Target = new() { Root = Source }, Output = new() { Directory = Output }, Rulesets = File.Exists(RulesFile) ? [RulesFile] : [] };
        public Fixture()
        {
            Directory.CreateDirectory(Source);
            File.WriteAllText(Path.Combine(Source, "Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        }
        public void Rules(string name, bool zero, bool enabled = true, bool includeAssemblyRule = false)
        {
            var selector = new Selector { Kind = "project", Match = "exact", Value = zero ? "Absent.csproj" : "Project.csproj" };
            var rules = new List<Rule>
            {
                new() { Id = "naming", Type = "naming", Enabled = enabled, Scope = selector, Severity = "warning", Reason = "Naming rule",
                    Parameters = JsonSerializer.SerializeToElement(new { subjectKind = "project", requiredName = new { match = "exact", value = name } }) }
            };
            if (includeAssemblyRule) rules.Add(new()
            {
                Id = "types", Type = "type-dependency", Enabled = true, Scope = new() { Kind = "namespace", Match = "glob", Value = "*" },
                Severity = "warning", Reason = "Type rule", Parameters = JsonSerializer.SerializeToElement(new
                { source = new { kind = "namespace", match = "glob", value = "*" }, forbiddenTarget = new { kind = "namespace", match = "exact", value = "Forbidden" } })
            });
            File.WriteAllText(RulesFile, JsonSerializer.Serialize(new Ruleset { SchemaVersion = 1, Id = "test", Version = "1", Description = "service fixture", Rules = rules.ToArray(), Exceptions = [] }, JsonContract.Options), new UTF8Encoding(false));
        }
        public void Dispose() => Directory.Delete(root, true);
    }
}
