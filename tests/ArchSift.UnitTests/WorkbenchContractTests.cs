using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class WorkbenchContractTests
{
    [Theory]
    [InlineData(-1, 25)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public void HistoryRejectsUnboundedOrNegativePages(int offset, int limit) =>
        Assert.Throws<ConfigurationException>(() => WorkbenchRequests.History(new(offset, limit)));

    [Fact]
    public void UnknownHistoryTimeAndFreshnessCannotBecomeCurrentVerification()
    {
        var item = new HistoryItem("artifact", null, "verify", "partial", 4, null, null,
            "source", null, "report.json", "hash", "reports", "0.7.0", "not-revalidated", null);
        var roundtrip = JsonSerializer.Deserialize<HistoryItem>(JsonSerializer.Serialize(item, JsonContract.Options), JsonContract.Options)!;
        Assert.Null(roundtrip.StartedUtc); Assert.Null(roundtrip.ElapsedMilliseconds);
        Assert.Equal("not-revalidated", roundtrip.Freshness);
        Assert.Throws<ConfigurationException>(() => WorkbenchRequests.History(new(Operation: "build-all")));
        Assert.Equal(100, WorkbenchRequests.History(new(Limit: 100)).Limit);
    }
}
