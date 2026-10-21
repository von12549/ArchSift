// ArchSift-owned synthetic target. No IFX checkout or historical run is an input.
using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Testing;
using Xunit;

namespace Synthetic;

public sealed class PortableTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("first")]
    [InlineData("second")]
    public void OriginalRows(string value) => Assert.NotEmpty(value);

    private static string RulesPath
    {
        get
        {
            using var config = JsonDocument.Parse(File.ReadAllBytes(
                Environment.GetEnvironmentVariable("ARCHSIFT_TEST_GATE_CONFIG")
                ?? throw new InvalidOperationException("Synthetic gate config is required.")));
            return Path.Combine(config.RootElement.GetProperty("targetRoot").GetString()!, "rules.json");
        }
    }

    public static IEnumerable<object[]> Rules() => ArchitectureTestAdapter.LoadCases([RulesPath]).Cases
        .Select(item => new object[] { item.CaseId });

    [Theory]
    [MemberData(nameof(Rules))]
    public void JsonRule(string id)
    {
        var (bundle, cases) = ArchitectureTestAdapter.LoadCases([RulesPath]);
        var item = Assert.Single(cases, item => item.CaseId == id);
        output.WriteLine("ARCHSIFT_JSON_CASE:" + id + ":" + item.Ruleset.Identity.Sha256);
        var evaluation = ArchitectureTestAdapter.EvaluateCase(bundle, item,
            TypeDependencyTestEngine.LoadContext([typeof(AllowedSource).Assembly.Location]));
        output.WriteLine("ARCHSIFT_JSON_EVALUATION:" + ArchitectureTestAdapter.SerializeEvaluation(evaluation));
        ArchitectureTestAdapter.AssertPassed(evaluation);
    }
}

public sealed class AllowedSource { public string Name => "synthetic"; }
