using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class ComparisonTests
{
    [Fact]
    public async Task HeadAgainstFinalDiskIncludesStagedUnstagedUntrackedAndCrossProjectFindings()
    {
        using var fixture = new GitFixture(); await fixture.Initialize();
        fixture.Project("A", "B"); fixture.Project("B"); fixture.Rules(); await fixture.Commit();
        fixture.Project("A"); await fixture.Git("add", "A/A.csproj"); // staged clean, disk violation wins
        fixture.Project("A", "B"); fixture.Project("C", "B");
        File.WriteAllText(Path.Combine(fixture.Source, "note.md"), "excluded");
        var before = await fixture.Git("status", "--porcelain=v1");
        var result = await fixture.Compare();
        Assert.Equal(0, result.ExitCode); Assert.Equal("completed", result.Report.Status);
        Assert.Single(result.Report.Existing); Assert.Single(result.Report.Added); Assert.Empty(result.Report.Resolved);
        Assert.Contains(result.Report.TargetIdentity.Inputs.Files, f => f.Path == "C/C.csproj");
        Assert.Contains("note.md", result.Report.TargetIdentity.ExcludedFiles);
        Assert.Equal(before, await fixture.Git("status", "--porcelain=v1"));
        Assert.Contains("\"changes\"", ComparisonWriter.Json(result.Report));
    }

    [Fact]
    public async Task ExplicitRevisionsResolveRenamesDeletionsAndDoNotUseWorkingTree()
    {
        using var fixture = new GitFixture(); await fixture.Initialize();
        fixture.Project("A", "B"); fixture.Project("B"); fixture.Rules();
        File.WriteAllText(Path.Combine(fixture.Source, "Before.cs"), "class Marker {}"); await fixture.Commit();
        var baseline = (await fixture.Git("rev-parse", "HEAD")).Trim();
        fixture.Project("A"); File.Move(Path.Combine(fixture.Source, "Before.cs"), Path.Combine(fixture.Source, "After.cs"));
        await fixture.Commit(); fixture.Project("A", "B");
        var result = await fixture.Compare(new(baseline, "HEAD"));
        Assert.Equal(0, result.ExitCode); Assert.Single(result.Report.Resolved); Assert.Empty(result.Report.Added);
        Assert.Contains(result.Report.FileChanges, f => f.Kind == "renamed" && f.PreviousPath == "Before.cs");
        Assert.Equal("commit", result.Report.TargetIdentity.Kind);
        File.Delete(Path.Combine(fixture.Source, "A", "A.csproj"));
        var deleted = await fixture.Compare(); Assert.Contains(deleted.Report.FileChanges, f => f.Kind == "deleted");
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("scope")]
    [InlineData("exception")]
    public async Task PolicyEditsAreNeverResolutions(string kind)
    {
        using var fixture = new GitFixture(); await fixture.Initialize();
        fixture.Project("A", "B"); fixture.Project("B"); fixture.Rules();
        var path = Path.Combine(fixture.Source, "policy.json"); var json = JsonNode.Parse(File.ReadAllText(path))!;
        if (kind == "remove")
        {
            var disabled = json["rules"]![0]!.DeepClone(); disabled["id"] = "unused"; disabled["enabled"] = false;
            json["rules"]!.AsArray().Add(disabled); File.WriteAllText(path, json.ToJsonString());
        }
        await fixture.Commit();
        if (kind == "remove") json["rules"]!.AsArray().RemoveAt(0);
        if (kind == "scope") json["rules"]![0]!["scope"]!["value"] = "B/B.csproj";
        if (kind == "exception") json["exceptions"] = JsonSerializer.SerializeToNode(new[] { new {
            id = "legacy", ruleId = "edge", scope = new { kind = "project", match = "glob", value = "**" }, reason = "Reviewed exception" } });
        File.WriteAllText(path, json.ToJsonString());
        var result = await fixture.Compare();
        Assert.Equal(4, result.ExitCode); Assert.Empty(result.Report.Resolved); Assert.Empty(result.Report.Added);
        Assert.Single(result.Report.PolicyChanges); Assert.NotEmpty(result.Report.Unclassified);
    }

    [Fact]
    public async Task BaselineFailureKeepsTargetWithoutInventingAddedEvidence()
    {
        using var fixture = new GitFixture(); await fixture.Initialize();
        fixture.Project("A", "B"); fixture.Project("B"); await fixture.Commit(); fixture.Rules();
        var result = await fixture.Compare();
        Assert.Equal(3, result.ExitCode); Assert.Null(result.Report.Baseline); Assert.NotNull(result.Report.Target);
        Assert.Empty(result.Report.Added); Assert.Empty(result.Report.Resolved); Assert.Single(result.Report.Unclassified);
    }

    [Fact]
    public async Task RootSubdirectoryAndExportAttributesDoNotChangeGitBlobIdentity()
    {
        using var fixture = new GitFixture(); await fixture.Initialize();
        fixture.Project("A"); fixture.Rules(); File.WriteAllText(Path.Combine(fixture.Source, ".gitattributes"), "*.csproj export-ignore\n");
        await fixture.Commit();
        var config = fixture.Config with { Target = new() { Root = Path.Combine(fixture.Source, "A") }, Rulesets = [] };
        var result = await new ComparisonService(new AnalysisService()).RunAsync(config, new());
        Assert.Equal(0, result.ExitCode); Assert.Single(result.Report.Baseline!.Projects);
        Assert.Equal(result.Report.BaselineIdentity.Inputs.Sha256, result.Report.TargetIdentity.Inputs.Sha256);
    }

    [Fact]
    public async Task BadRefsAndOneEndedRequestsAreConfigurationErrorsAndNoFetchOccurs()
    {
        using var fixture = new GitFixture(); await fixture.Initialize(); fixture.Project("A"); fixture.Rules(); await fixture.Commit();
        await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Compare(new("HEAD", null)));
        await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Compare(new("--upload-pack=bad", "HEAD")));
        await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Compare(new("origin/not-local", "HEAD")));
    }

    [Fact]
    public async Task CliChangesUsesSharedServiceAndReportsConfigurationFailure()
    {
        using var fixture = new GitFixture(); await fixture.Initialize(); fixture.Project("A"); fixture.Project("B"); fixture.Rules(); await fixture.Commit(); fixture.Project("A", "B");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
        var configuration = typeof(ComparisonTests).Assembly.GetCustomAttributes(false).OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
        var cli = Path.Combine(directory!.FullName, "src", "ArchSift.Cli", "bin", configuration, "net10.0", "archsift.dll");
        var config = Path.Combine(fixture.Root, "config.json"); File.WriteAllText(config, JsonSerializer.Serialize(fixture.Config, JsonContract.Options));
        var result = await SafeProcess.RunAsync(SafeProcess.Dotnet, directory.FullName, [cli, "changes", "--config", config], Path.Combine(fixture.Root, "home"), null, default);
        Assert.Equal(0, result.ExitCode); var report = JsonSerializer.Deserialize<ComparisonReport>(result.Output, JsonContract.Options)!;
        Assert.Single(report.Added); Assert.Equal((await fixture.Compare()).Report.Added.Select(f => f.Id), report.Added.Select(f => f.Id));
        Assert.True(File.Exists(Path.Combine(fixture.Output, report.RunMetadata.RunId, "comparison.html")));
        var invalid = await SafeProcess.RunAsync(SafeProcess.Dotnet, directory.FullName, [cli, "changes", "--config", config, "--head", "HEAD"], Path.Combine(fixture.Root, "home"), null, default);
        Assert.Equal(2, invalid.ExitCode);
    }
}

internal sealed class GitFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "archsift-changes-" + Guid.NewGuid().ToString("N"));
    public string Source => Path.Combine(Root, "source");
    public string Output => Path.Combine(Root, "output");
    public RunConfiguration Config => new() { SchemaVersion = 1, Target = new() { Root = Source },
        Rulesets = [Path.Combine(Source, "policy.json")], Output = new() { Directory = Output } };
    public async Task Initialize() { Directory.CreateDirectory(Source); await Git("init"); }
    public async Task<string> Git(params string[] args)
    {
        var result = await SafeProcess.RunAsync("git", Source, args, Path.Combine(Root, "home"), null, default);
        Assert.Equal(0, result.ExitCode); return result.Output;
    }
    public async Task Commit()
    {
        await Git("add", ".");
        await Git("-c", "user.name=ArchSift Fixture", "-c", "user.email=fixture@archsift.invalid", "commit", "-m", "Synthetic revision fixture\n\nCo-Authored-By: Codex <noreply@openai.com>");
    }
    public void Project(string name, string? reference = null)
    {
        var path = Path.Combine(Source, name); Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, name + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>" +
            (reference is null ? "" : "<ItemGroup><ProjectReference Include=\"../" + reference + "/" + reference + ".csproj\" /></ItemGroup>") + "</Project>");
    }
    public void Rules() => File.WriteAllText(Path.Combine(Source, "policy.json"), JsonSerializer.Serialize(new Ruleset
    {
        SchemaVersion = 1, Id = "changes", Version = "1", Description = "fixture", Exceptions = [], Rules = [new()
        {
            Id = "edge", Type = "project-reference", Enabled = true, Severity = "warning", Reason = "No dependency on B", Scope = new() { Kind = "project", Match = "glob", Value = "**" },
            Parameters = JsonSerializer.SerializeToElement(new { source = new { kind = "project", match = "glob", value = "**" }, target = new { kind = "project", match = "exact", value = "B/B.csproj" } })
        }]
    }, JsonContract.Options));
    public Task<ComparisonOutcome> Compare(ChangeRequest? request = null) => new ComparisonService(new AnalysisService()).RunAsync(Config, request ?? new());
    public void Dispose()
    {
        // Only this precisely registered synthetic fixture; Git objects are read-only on Windows.
        if (!PathSafety.IsUnder(Root, Path.GetTempPath()) || !Path.GetFileName(Root).StartsWith("archsift-changes-", StringComparison.Ordinal))
            throw new InvalidOperationException("Fixture cleanup containment failed.");
        if (!Directory.Exists(Root)) return;
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        { PathSafety.EnsureNoLinks(file); File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly); }
        Directory.Delete(Root, true);
    }
}
