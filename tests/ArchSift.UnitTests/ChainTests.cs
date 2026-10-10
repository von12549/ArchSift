using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class ChainTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "archsift-chain-" + Guid.NewGuid().ToString("N"));
    private RunConfiguration Config => new() { SchemaVersion = 1, Target = new() { Root = Path.Combine(root, "source") },
        Output = new() { Directory = Path.Combine(root, "reports") }, RulesDirectory = Path.Combine(root, "rules") };
    private RulesetLibrary Library => new(Config.RulesDirectory!, Config.Target.Root);
    public ChainTests()
    {
        Directory.CreateDirectory(Config.Target.Root);
        File.WriteAllText(Path.Combine(Config.Target.Root, "Good.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
    }
    private LibraryCard Add(string id, string requiredName = "Good", bool allowEmpty = false, string scope = "**")
    {
        var rules = new Ruleset { SchemaVersion = 1, Id = id, Version = "1", Description = "User policy 原文", Exceptions = [],
            Rules = [new() { Id = "shared-rule-id", Type = "naming", Enabled = true, Scope = new() { Kind = "project", Match = "glob", Value = scope, AllowEmpty = allowEmpty },
                Severity = "warning", Reason = "User reason 原文", Parameters = JsonSerializer.SerializeToElement(new { subjectKind = "project", requiredName = new { match = "exact", value = requiredName } }) }] };
        return Library.Import(id + ".json", JsonSerializer.SerializeToUtf8Bytes(rules, JsonContract.Options));
    }
    private static ChainDocument Chain(params LibraryCard[] cards) => new(1, "chain", "1", "Ordered checks", cards.Select(c => new ChainEntry(c.EntryId)).ToArray());

    [Fact]
    public async Task ProgressIsCurrentOrderedAndPublishedAfterChildFilesExistWithLegacyReadCompatibility()
    {
        var first = Add("one"); var second = Add("two");
        var updates = new List<ChainProgress>();
        var summary = await new ChainService(new AnalysisService()).RunAsync(Config, Chain(first, second), Library,
            progress: progress =>
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(progress, JsonContract.Options));
                SchemaValidation.Validate(doc.RootElement, "chain-progress");
                Assert.Equal(new[] { first.EntryId, second.EntryId }, progress.Entries.Select(e => e.EntryId));
                foreach (var entry in progress.Entries.Where(e => e.OutputAvailable))
                    Assert.NotEmpty(Directory.GetFiles(Path.Combine(Config.Output.Directory, progress.RunId, entry.EntryId), "report.json", SearchOption.AllDirectories));
                updates.Add(progress);
            });
        Assert.Equal("preparing", updates[0].Stage);
        Assert.Equal("finished", updates[^1].Stage);
        Assert.Equal(2, updates[^1].ExecutedCount); Assert.Equal(0, updates[^1].SkippedCount);
        Assert.Equal(updates.Count, updates.Select(p => p.Revision).Distinct().Count());
        Assert.Contains(updates, p => p.Entries.Any(e => e.State == "writing" && !e.OutputAvailable));
        Assert.True(File.Exists(Path.Combine(summary.Snapshot.OutputDirectory, "chain-summary.json")));
        var legacy = summary with { SchemaVersion = 1, ToolVersion = "0.4.0", Snapshot = summary.Snapshot with { ExecutionOptions = null } };
        var text = ChainWriter.Json(legacy);
        var restored = JsonSerializer.Deserialize<ChainSummary>(text, JsonContract.Options)!;
        Assert.Equal("0.4.0", restored.ToolVersion); Assert.Null(restored.Snapshot.ExecutionOptions);
    }

    [Fact]
    public async Task CancellationInPreparationAndDriftAfterLastChildRemainAuditable()
    {
        var first = Add("one"); using var cancel = new CancellationTokenSource();
        var service = new ChainService(new AnalysisService());
        var cancelled = await service.RunAsync(Config, Chain(first), Library, token: cancel.Token,
            progress: p => { if (p.Stage == "preparing") cancel.Cancel(); });
        Assert.Equal(130, cancelled.ExitCode); Assert.Equal("skipped", cancelled.Entries[0].Execution);
        Assert.Contains("preparation-cancelled", cancelled.Limitations);
        var drift = await service.RunAsync(Config, Chain(first), Library,
            progress: p => { if (p.Stage == "evaluating" && p.EndedCount == 1) File.WriteAllText(Path.Combine(Config.Target.Root, "Later.cs"), "class Later {}"); });
        Assert.Equal(4, drift.ExitCode); Assert.Equal("inconclusive", drift.Compliance);
        Assert.Contains("global-input-drift", drift.Limitations);
    }

    [Fact]
    public async Task IndependentDuplicateRuleIdsAndReorderKeepFindingsAndSemanticsButLegacyRejectsComposition()
    {
        var first = Add("one", "Wrong"); var second = Add("two");
        var service = new ChainService(new AnalysisService());
        var forward = await service.RunAsync(Config, Chain(first, second), Library);
        var reverse = await service.RunAsync(Config, Chain(second, first), Library);
        Assert.Equal("noncompliant", forward.Compliance); Assert.Equal(0, forward.ExitCode); Assert.Equal(1, forward.ProjectCount);
        Assert.Equal(new[] { second.EntryId, first.EntryId }, reverse.Entries.Select(e => e.EntryId));
        foreach (var child in forward.Entries)
        {
            var other = reverse.Entries.Single(e => e.EntryId == child.EntryId);
            Assert.Equal(child.Compliance, other.Compliance); Assert.Equal(child.Findings.Select(f => f.Id), other.Findings.Select(f => f.Id));
            Assert.Equal(1, child.Coverage.ProjectCount);
            Assert.True(File.Exists(Path.Combine(forward.Snapshot.OutputDirectory, child.ReportDirectory!, "report.json")));
        }
        using var json = JsonDocument.Parse(ChainWriter.Json(forward)); SchemaValidation.Validate(json.RootElement, "chain-summary-v2");
        var sarif = JsonNode.Parse(ChainWriter.Sarif(forward))!;
        Assert.Equal(3, sarif["runs"]!.AsArray().Count);
        Assert.StartsWith(first.EntryId + "/", sarif["runs"]![0]!["results"]![0]!["ruleId"]!.GetValue<string>(), StringComparison.Ordinal);
        var originalProjection = ChainWriter.Sarif(forward);
        File.WriteAllText(Path.Combine(forward.Snapshot.OutputDirectory, forward.Entries[0].ReportDirectory!, "report.sarif"), "{}");
        Assert.Equal(originalProjection, ChainWriter.Sarif(forward)); // Current downloads use frozen evidence, not mutable saved projections.
        Assert.Throws<ConfigurationException>(() => RuleLoader.Compose([Library.Capture(first.EntryId), Library.Capture(second.EntryId)]));
    }
    [Fact]
    public async Task DeletedReferenceRetainsChainAndDiagnosticWhileReimportGetsNewIdentityAndLaterChildRuns()
    {
        var first = Add("one"); var second = Add("two"); var chain = Chain(first, second); var store = new ChainStore(Library);
        store.Save(chain.Id, JsonSerializer.SerializeToUtf8Bytes(chain, JsonContract.Options));
        Library.Delete(first.EntryId); var reimported = Add("one");
        Assert.NotEqual(first.EntryId, reimported.EntryId);
        store.Save(chain.Id, JsonSerializer.SerializeToUtf8Bytes(chain with { Description = "Metadata edit keeps dangling reference" }, JsonContract.Options));
        var summary = await new ChainService(new AnalysisService()).RunAsync(Config, store.Read("chain"), Library);
        Assert.Equal(4, summary.ExitCode); Assert.Equal("inconclusive", summary.Compliance);
        Assert.Equal("missing-ruleset", summary.Entries[0].Diagnostic!.Code); Assert.Null(summary.Snapshot.Entries[0].RulesetIdentity);
        Assert.Empty(summary.Entries[0].RuleResults); Assert.Empty(summary.Entries[0].Findings);
        Assert.Equal("completed", summary.Entries[1].Execution);
        var diagnostic = JsonNode.Parse(File.ReadAllText(Path.Combine(summary.Snapshot.OutputDirectory, first.EntryId, "diagnostic.sarif")))!;
        Assert.Empty(diagnostic["runs"]![0]!["results"]!.AsArray());
        Assert.Single(diagnostic["runs"]![0]!["invocations"]![0]!["toolExecutionNotifications"]!.AsArray());
    }
    [Fact]
    public async Task InvalidChildAndZeroMatchesRemainInconclusiveAndContinue()
    {
        var first = Add("one"); var second = Add("two", scope: "Absent.csproj");
        File.WriteAllText(Library.FilePath(first.FileName), "{}");
        var summary = await new ChainService(new AnalysisService()).RunAsync(Config, Chain(first, second), Library);
        Assert.Equal("invalid-ruleset", summary.Entries[0].Diagnostic!.Code);
        Assert.Equal("inconclusive", summary.Entries[1].Compliance); Assert.Equal("partial", summary.Entries[1].Execution);
        Assert.Equal(4, summary.ExitCode);
    }
    [Fact]
    public async Task AllNotApplicableIsDistinctAndUnsavedOrLaterSavedEditsCannotChangeFrozenBytes()
    {
        var first = Add("one", allowEmpty: true, scope: "Absent.csproj"); var second = Add("two", allowEmpty: true, scope: "Absent.csproj");
        var prepared = ChainService.Prepare(Config, Chain(first, second), Library);
        Library.Save(first.FileName, JsonSerializer.SerializeToUtf8Bytes(first.Ruleset! with { Version = "2" }, JsonContract.Options));
        var summary = await new ChainService(new AnalysisService()).RunAsync(Config, prepared);
        Assert.Equal("not-applicable", summary.Compliance); Assert.Equal(0, summary.ExitCode);
        Assert.Equal(first.Identity!.Sha256, summary.Snapshot.Entries[0].RulesetIdentity!.Sha256);
        Assert.NotEqual(first.Identity.Sha256, Library.Capture(first.EntryId).Identity.Sha256);
    }
    [Fact]
    public async Task InputDriftStopsAllUnstartedEntriesAndCancellationProducesCurrentSummary()
    {
        var first = Add("one"); var second = Add("two"); var prepared = ChainService.Prepare(Config, Chain(first, second), Library);
        File.WriteAllText(Path.Combine(Config.Target.Root, "Added.cs"), "class Added {}");
        var drift = await new ChainService(new AnalysisService()).RunAsync(Config, prepared);
        Assert.Equal(4, drift.ExitCode); Assert.All(drift.Entries, e => Assert.Equal("skipped", e.Execution));
        Assert.Contains("global-input-drift", drift.Limitations);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var cancelled = await new ChainService(new AnalysisService()).RunAsync(Config, Chain(first, second), Library, token: cancel.Token);
        Assert.Equal(130, cancelled.ExitCode); Assert.Equal("cancelled", cancelled.Execution);
        Assert.True(File.Exists(Path.Combine(cancelled.Snapshot.OutputDirectory, "chain-summary.json")));
        Assert.All(cancelled.Entries, e => Assert.Equal("skipped", e.Execution));
    }
    [Fact]
    public async Task PartialCompliantRollupAndGlobalInvalidTargetAreConservative()
    {
        var first = Add("one"); var second = Add("two");
        File.WriteAllText(Path.Combine(Config.Target.Root, "Unsupported.fsproj"), "<Project />");
        var summary = await new ChainService(new AnalysisService()).RunAsync(Config, Chain(first, second), Library);
        Assert.Equal("partial", summary.Execution); Assert.Equal("compliant", summary.Compliance); Assert.Equal(4, summary.ExitCode);
        Assert.All(summary.Entries, e => Assert.NotEmpty(e.Limitations));
        await Assert.ThrowsAsync<ConfigurationException>(() => new ChainService(new AnalysisService()).RunAsync(
            Config with { Target = Config.Target with { Entry = "missing.sln" } }, Chain(first, second), Library));
    }
    [Fact]
    public async Task CancellationDuringAChildRetainsCompletedChildAndSkipsOnlyRemainingEntries()
    {
        var first = Add("one");
        var compiled = RuleLoader.Parse(System.Text.Encoding.UTF8.GetBytes(RuleTemplates.Json("type-dependency"))) with { Id = "compiled" };
        var second = Library.Import("compiled.json", JsonSerializer.SerializeToUtf8Bytes(compiled, JsonContract.Options));
        var third = Add("three");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        var analysis = new AnalysisService(async (_, _, _, token) =>
        {
            entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Expected child cancellation.");
        });
        var running = new ChainService(analysis).RunAsync(Config, Chain(first, second, third), Library, token: cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancel.Cancel();
        var summary = await running;
        Assert.Equal(130, summary.ExitCode); Assert.Equal("completed", summary.Entries[0].Execution);
        Assert.Equal("cancelled", summary.Entries[1].Execution); Assert.Equal("skipped", summary.Entries[2].Execution);
        Assert.True(File.Exists(Path.Combine(summary.Snapshot.OutputDirectory, summary.Entries[0].ReportDirectory!, "report.json")));
        Assert.True(File.Exists(Path.Combine(summary.Snapshot.OutputDirectory, summary.Entries[1].ReportDirectory!, "report.json")));
        Assert.Null(summary.Entries[2].ReportDirectory);
    }
    public void Dispose() { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
