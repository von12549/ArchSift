using System.Text;
using System.Text.Json;
using ArchSift.TestGate;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class MtpCtrfTests
{
    private const string Sha = "b9669d62d9195f9322a1fda34b46003472564070c8ec492d71234900cc88b341";
    private static readonly IReadOnlyDictionary<string, string> JsonCases =
        new Dictionary<string, string> { ["archsift-json:allowed-source"] = Sha };

    [Fact]
    public void NativeCtrfIdsAndOutputMarkersPermitFullCoverage()
    {
        var run = Parse();
        Assert.Equal(2, run.DiscoveredCaseCount);
        Assert.Equal("OriginalTests", Assert.Single(run.Tests, test => test.Id == "original-id").ClassName);
        Assert.Equal("archsift-json:allowed-source", Assert.Single(run.Tests, test => test.Id == "json-id").JsonCaseId);
        Assert.Equal(0, MtpEvents.Assess(run, ["original-id"], JsonCases, 0).ExitCode);
        Assert.Equal(4, MtpEvents.Assess(run, ["original-id", "missing-id"], JsonCases, 0).ExitCode);
    }

    [Fact]
    public void MtpCtrfCannotTurnFailedOrMalformedRowsIntoPass()
    {
        var failed = Parse(status: "failed", failedCount: 1, passedCount: 1);
        Assert.Equal(2, MtpEvents.Assess(failed, ["original-id"], JsonCases, 1).ExitCode);
        var duplicated = Parse(originalId: "json-id");
        Assert.Equal(4, MtpEvents.Assess(duplicated, ["original-id"], JsonCases, 0).ExitCode);
        Assert.Throws<InvalidDataException>(() => Parse(passedCount: 1));
    }

    private static MtpRun Parse(string status = "passed", int failedCount = 0,
        int passedCount = 2, string originalId = "original-id")
    {
        const string discovery = """{"tests":[{"uid":"mtp-uid-1"},{"uid":"mtp-uid-2"}]}""";
        var report = JsonSerializer.Serialize(new
        {
            reportFormat = "CTRF",
            results = new
            {
                summary = new { tests = 2, passed = passedCount, failed = failedCount,
                    skipped = 0, pending = 0, other = 0 },
                tests = new object[]
                {
                    new { status, extra = new { id = originalId, type = "OriginalTests", method = "OriginalTheory" } },
                    new { status = "passed", stdout = new[] { "ARCHSIFT_JSON_CASE:archsift-json:allowed-source:" + Sha },
                        extra = new { id = "json-id", type = "JsonTests", method = "JsonRule" } }
                }
            }
        });
        return MtpCtrf.Parse(Encoding.UTF8.GetBytes(discovery), Encoding.UTF8.GetBytes(report));
    }
}
