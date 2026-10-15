using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.Testing;
using Xunit;

namespace ArchSift.Testing.Tests
{
public sealed class ArchitectureRuleTests
{
    private static readonly string Policy = Path.Combine(AppContext.BaseDirectory, "rules.json");
    private static readonly string AssemblyPath = Assembly.GetExecutingAssembly().Location;
    private static string CasePolicy => Environment.GetEnvironmentVariable("ARCHSIFT_LT01_NEGATIVE") == "1"
        ? Path.Combine(AppContext.BaseDirectory, "rules-violation.json") : Policy;

    public static IEnumerable<object[]> JsonCases() => ArchitectureTestAdapter.LoadCases([CasePolicy]).Cases
        .Select(testCase => new object[] { testCase.CaseId });

    [Theory]
    [MemberData(nameof(JsonCases))]
    public void JsonRuleIsAnActualTest(string caseId)
    {
        var (bundle, cases) = ArchitectureTestAdapter.LoadCases([CasePolicy]);
        var testCase = Assert.Single(cases, item => item.CaseId == caseId);
        ArchitectureTestAdapter.AssertPassed(ArchitectureTestAdapter.EvaluateCase(bundle, testCase, [AssemblyPath]));
    }

    [Theory]
    [InlineData("ForbiddenSource", "Infrastructure.ForbiddenTarget")]
    [InlineData("GenericSource", "Infrastructure.ForbiddenTarget")]
    [InlineData("SignatureSource", "Infrastructure.ForbiddenTarget")]
    public void RealDependencyFailsWithStableFinding(string source, string target)
    {
        var (bundle, testCase) = LoadModified(root =>
            root["rules"]![0]!["parameters"]!["source"]!["value"] =
                "ArchSift.Testing.Tests.Fixtures.Domain." + source);
        var first = ArchitectureTestAdapter.EvaluateCase(bundle, testCase, [AssemblyPath]);
        var second = ArchitectureTestAdapter.EvaluateCase(bundle, testCase, [AssemblyPath]);
        Assert.Equal("violation", first.Results.Single().Status);
        var finding = Assert.Single(first.Findings, item => item.ExceptionId is null);
        Assert.Contains(source, finding.Source);
        Assert.Contains(target, finding.Target);
        Assert.Equal(finding.Id, Assert.Single(second.Findings).Id);
        var failure = Assert.Throws<ArchitectureRuleFailureException>(() => ArchitectureTestAdapter.AssertPassed(first));
        Assert.Contains(finding.Source!, failure.Message);
        Assert.Contains(finding.Target!, failure.Message);
    }

    [Fact]
    public void DocumentedExceptionRetainsFindingAndPasses()
    {
        var (bundle, testCase) = LoadModified(root =>
        {
            root["rules"]![0]!["parameters"]!["source"]!["value"] =
                "ArchSift.Testing.Tests.Fixtures.Domain.ForbiddenSource";
            root["exceptions"]!.AsArray().Add(JsonNode.Parse("""
                {"id":"synthetic-exception","ruleId":"allowed-source","scope":{"kind":"namespace","match":"exact","value":"ArchSift.Testing.Tests.Fixtures.Domain"},"reason":"Synthetic exception review"}
                """));
        });
        var evaluation = ArchitectureTestAdapter.EvaluateCase(bundle, testCase, [AssemblyPath]);
        Assert.Equal("pass", evaluation.Results.Single().Status);
        var finding = Assert.Single(evaluation.Findings);
        Assert.Equal("synthetic-exception", finding.ExceptionId);
        Assert.Equal("Synthetic exception review", finding.ExceptionReason);
        ArchitectureTestAdapter.AssertPassed(evaluation);
    }

    [Fact]
    public void ZeroMatchIsInconclusiveByDefault()
    {
        var (bundle, testCase) = LoadModified(root =>
            root["rules"]![0]!["parameters"]!["source"]!["value"] = "Missing.Source");
        var evaluation = ArchitectureTestAdapter.EvaluateCase(bundle, testCase, [AssemblyPath]);
        Assert.Equal("inconclusive", evaluation.Results.Single().Status);
        Assert.Throws<ArchitectureRuleFailureException>(() => ArchitectureTestAdapter.AssertPassed(evaluation));
    }

    [Fact]
    public void UnsupportedAndEmptyConfigurationAreRejected()
    {
        var unsupported = JsonNode.Parse(File.ReadAllText(Policy))!;
        unsupported["rules"]![0]!["type"] = "naming";
        unsupported["rules"]![0]!["parameters"] = JsonNode.Parse("""{"subjectKind":"type","requiredName":{"match":"glob","value":"*"}}""");
        var empty = JsonNode.Parse(File.ReadAllText(Policy))!;
        empty["rules"]![0]!["enabled"] = false;
        var unsupportedPath = Write(unsupported);
        var emptyPath = Write(empty);
        try
        {
            Assert.Throws<ConfigurationException>(() => ArchitectureTestAdapter.LoadCases([unsupportedPath]));
            Assert.Throws<ConfigurationException>(() => ArchitectureTestAdapter.LoadCases([emptyPath]));
            Assert.Throws<ConfigurationException>(() => ArchitectureTestAdapter.LoadCases([Policy, Policy]));
        }
        finally
        {
            File.Delete(unsupportedPath);
            File.Delete(emptyPath);
        }
    }

    private static (RuleBundle Bundle, ArchitectureRuleCase Case) LoadModified(Action<JsonNode> change)
    {
        var root = JsonNode.Parse(File.ReadAllText(Policy))!;
        change(root);
        var loaded = RuleLoader.Parse(Encoding.UTF8.GetBytes(root.ToJsonString()));
        var document = new LoadedRuleset(loaded, new(loaded.Id, loaded.Version, "synthetic", Policy));
        var bundle = RuleLoader.Compose([document]);
        return (bundle, new("archsift-json:" + bundle.Rules.Single().Id, bundle.Rules.Single(), document));
    }

    private static string Write(JsonNode root)
    {
        var path = Path.Combine(Path.GetTempPath(), "archsift-lt01-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, root.ToJsonString());
        return path;
    }
}
}

namespace ArchSift.Testing.Tests.Fixtures.Domain
{
    public sealed class AllowedSource { public int Value; }
    public sealed class ForbiddenSource { public ArchSift.Testing.Tests.Fixtures.Infrastructure.ForbiddenTarget? Value; }
    public sealed class GenericSource { public List<ArchSift.Testing.Tests.Fixtures.Infrastructure.ForbiddenTarget>? Values; }
    public sealed class SignatureSource
    {
        public ArchSift.Testing.Tests.Fixtures.Infrastructure.ForbiddenTarget? Create() => null;
    }
}

namespace ArchSift.Testing.Tests.Fixtures.Infrastructure
{
    public sealed class ForbiddenTarget { }
}
