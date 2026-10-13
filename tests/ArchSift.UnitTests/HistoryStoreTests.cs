using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class HistoryStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "archsift-history-" + Guid.NewGuid().ToString("N"));
    private RunConfiguration Config => new() { SchemaVersion = 1, Target = new() { Root = Path.Combine(root, "source") }, Output = new() { Directory = Path.Combine(root, "reports") } };
    public HistoryStoreTests()
    {
        Directory.CreateDirectory(Config.Target.Root);
        File.WriteAllText(Path.Combine(Config.Target.Root, "A.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
    }
    private async Task<AnalysisReport> Report(string id, string started = "2026-10-13T14:37:31+11:00")
    {
        var result = await new AnalysisService().RunAsync(Config, "analyze");
        var report = result.Report with { RunMetadata = new(id, started, 253890, Config.Target.Root) };
        ReportWriter.Save(Config, report); return report;
    }

    [Fact]
    public async Task RestartPaginationDirectoryRegistrationAndUtcSortingKeepRealReports()
    {
        for (var i = 0; i < 28; i++) await Report("run-" + i, "2026-10-13T03:37:31Z");
        await Report("later", "2026-10-13T14:41:53+11:00");
        var first = new HistoryStore(Config); var page = first.List(new());
        Assert.Equal(29, page.Total); Assert.Equal("later", page.Items[0].RunId);
        var second = new HistoryStore(Config); var all = second.List(new()).Items.Concat(second.List(new(25)).Items).ToArray();
        Assert.Equal(29, all.Length); Assert.Equal(29, all.Select(i => i.Id).Distinct().Count());
        Assert.All(all, i => Assert.Equal("not-revalidated", i.Freshness));
        var additional = Path.Combine(root, "other"); Directory.CreateDirectory(additional);
        var saved = Path.Combine(Config.Output.Directory, "later", "report.json");
        File.Copy(saved, Path.Combine(additional, "report.json"));
        second.Register(new(additional, Config.Target.Root));
        Assert.Equal(30, new HistoryStore(Config).List(new()).Total);
        Assert.Throws<ConfigurationException>(() => second.Register(new(additional, root)));
        Assert.Throws<ConfigurationException>(() => second.Register(new(Config.Target.Root, Config.Target.Root)));
    }

    [Fact]
    public async Task MissingTimeIgnoresMtimeAndCorruptionDoesNotHideOtherResults()
    {
        await Report("legacy"); var path = Path.Combine(Config.Output.Directory, "legacy", "report.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!; node["runMetadata"]!.AsObject().Remove("startedUtc"); node["runMetadata"]!.AsObject().Remove("elapsedMilliseconds");
        File.WriteAllText(path, node.ToJsonString()); var original = File.ReadAllBytes(path);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddYears(5));
        await Report("valid");
        var bad = Path.Combine(Config.Output.Directory, "bad"); Directory.CreateDirectory(bad); File.WriteAllText(Path.Combine(bad, "report.json"), "{bad");
        var store = new HistoryStore(Config); var page = store.List(new());
        Assert.Equal(3, page.Total); var legacy = page.Items.Single(i => i.RunId == "legacy");
        Assert.Null(legacy.StartedUtc); Assert.Null(legacy.ElapsedMilliseconds);
        Assert.Null(store.Open(legacy.Id).History.StartedUtc); Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Single(page.Items, i => i.State == "unavailable");
        File.AppendAllText(path, " "); Assert.Throws<ConfigurationException>(() => store.Open(legacy.Id));
    }

    [Fact]
    public async Task ChainChildrenAreDetailsAndLiveResultIsNotDuplicated()
    {
        var report = await Report("first");
        var library = new RulesetLibrary(Path.Combine(root, "rules"), Config.Target.Root);
        var card = library.Import("naming.json", System.Text.Encoding.UTF8.GetBytes(RuleTemplates.Json("naming")));
        var summary = await new ChainService(new AnalysisService()).RunAsync(Config, new(1, "chain", "1", "fixture", [new(card.EntryId)]), library, "fixture");
        var store = new HistoryStore(Config); var page = store.List(new()); Assert.Equal(2, page.Total);
        var chain = page.Items.Single(i => i.Operation == "chain"); var view = store.Open(chain.Id);
        Assert.Equal(ChainProjection.Aggregate(summary), view.Aggregate);
        Assert.NotEmpty(store.Download(chain.Id, "json", card.EntryId).Bytes);
        var live = page.Items.Single(i => i.RunId == report.RunMetadata.RunId) with { Id = "live" };
        Assert.Equal(2, store.List(new(), [live]).Total);
        Assert.Throws<ConfigurationException>(() => store.Download(chain.Id, "json", "../../escape"));
    }

    [Fact]
    public void PersistedFailureAndInterruptedJobRemainUnavailableWithoutOldSuccess()
    {
        var job = new HistoryJobRecord(1, "history-job", Guid.NewGuid().ToString("N"), "verify", "failed", 3, "Actual failure",
            Config, "fixture", null, "2026-10-13T00:00:00Z", "2026-10-13T00:00:00Z", "2026-10-13T00:00:01Z", 1000, "0.8.0-dev", null);
        HistoryStore.SaveJob(job); HistoryStore.SaveJob(job with { Id = Guid.NewGuid().ToString("N"), State = "running", FinishedUtc = null, Error = null });
        var store = new HistoryStore(Config); var page = store.List(new()); Assert.Equal(2, page.Total);
        var failure = store.Open(page.Items.Single(i => i.State == "failed").Id);
        Assert.Equal("Actual failure", failure.Error); Assert.Null(failure.Report);
        Assert.Single(page.Items, i => i.State == "unavailable");
    }

    public void Dispose()
    {
        Assert.True(PathSafety.IsUnder(root, Path.GetTempPath()));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
