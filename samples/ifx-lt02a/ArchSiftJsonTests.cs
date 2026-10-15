using System.Text.Json;
using ArchSift.ArchUnit;
using ArchSift.Testing;
using IFX.Architecture.Tests.Infrastructure;

namespace IFX.Architecture.Tests;

public sealed class ArchSiftJsonTests(ITestOutputHelper output)
{
    private static readonly Lazy<TypeDependencyTestContext> SharedContext = new(() =>
        TypeDependencyTestEngine.LoadContext(SourceAssemblies.All.Select(assembly => assembly.Location)));

    private static string[] RulePaths
    {
        get
        {
            var configPath = Environment.GetEnvironmentVariable("ARCHSIFT_TEST_GATE_CONFIG")
                ?? throw new InvalidOperationException("ARCHSIFT_TEST_GATE_CONFIG is required for JSON architecture cases.");
            using var document = JsonDocument.Parse(File.ReadAllBytes(configPath));
            var root = document.RootElement;
            var target = root.GetProperty("targetRoot").GetString()!;
            return root.GetProperty("rulesets").EnumerateArray()
                .Select(item => Path.GetFullPath(Path.Combine(target, item.GetString()!))).ToArray();
        }
    }

    public static IEnumerable<object[]> JsonCases() => ArchitectureTestAdapter.LoadCases(RulePaths).Cases
        .Select(testCase => new object[] { testCase.CaseId });

    [Theory]
    [MemberData(nameof(JsonCases))]
    public void JsonRuleIsAnActualTest(string caseId)
    {
        var (bundle, cases) = ArchitectureTestAdapter.LoadCases(RulePaths);
        var testCase = Assert.Single(cases, item => item.CaseId == caseId);
        output.WriteLine("ARCHSIFT_JSON_CASE:" + testCase.CaseId + ":" + testCase.Ruleset.Identity.Sha256);
        var evaluation = ArchitectureTestAdapter.EvaluateCase(bundle, testCase, SharedContext.Value);
        output.WriteLine("ARCHSIFT_JSON_EVALUATION:" + ArchitectureTestAdapter.SerializeEvaluation(evaluation));
        ArchitectureTestAdapter.AssertPassed(evaluation);
    }
}
