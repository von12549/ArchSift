using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class ProjectRuleTests
{
    public static TheoryData<string> Types => new()
    {
        "project-reference", "project-reference-allowlist", "graph-integrity", "target-framework",
        "nuget-denylist", "nuget-allowlist", "naming"
    };

    [Theory]
    [MemberData(nameof(Types))]
    public void CleanAndViolationUseRealDeclaredFixtures(string type)
    {
        using var clean = new Fixture(); using var bad = new Fixture();
        var good = clean.Create(type, false); var wrong = bad.Create(type, true);
        var rule = Bundle(type);
        Assert.Equal("pass", Assert.Single(ProjectRuleEvaluator.Evaluate(good, rule).Results).Status);
        var result = ProjectRuleEvaluator.Evaluate(wrong, rule);
        Assert.Equal("violation", Assert.Single(result.Results).Status);
        Assert.NotEmpty(result.Findings);
        Assert.Equal(result.Findings.Select(f => f.Id), ProjectRuleEvaluator.Evaluate(wrong, rule).Findings.Select(f => f.Id));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void MissingAndUnsupportedNeverBecomePass(string type)
    {
        using var empty = new Fixture(); using var legacy = new Fixture();
        var rule = Bundle(type);
        Assert.Equal("inconclusive", Assert.Single(ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(empty.Root), rule).Results).Status);
        legacy.Write("Domain.csproj", "<Project/>");
        var unsupported = ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(legacy.Root), rule);
        Assert.NotEqual("pass", Assert.Single(unsupported.Results).Status);
    }

    [Fact]
    public void NugetDenylistIsIdOnlyAndCaseInsensitive()
    {
        using var fixture = new Fixture();
        fixture.Write("Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"FORBIDDEN.PACKAGE\" Version=\"$(Unknown)\"/></ItemGroup></Project>");
        var result = ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(fixture.Root), Bundle("nuget-denylist"));
        Assert.Equal("violation", Assert.Single(result.Results).Status);
        Assert.Equal("forbidden.package", Assert.Single(result.Findings).Target);
    }

    [Fact]
    public void NugetAllowlistIsIdOnlyAndCaseInsensitive()
    {
        using var fixture = new Fixture();
        fixture.Write("Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"ALLOWED.PACKAGE\" Version=\"$(Unknown)\"/></ItemGroup></Project>");
        var result = ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(fixture.Root), Bundle("nuget-allowlist"));
        Assert.Equal("pass", Assert.Single(result.Results).Status);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void EmptyProjectAllowlistMeansNoDirectReferencesAndNoReferencesCanPass()
    {
        using var fixture = new Fixture();
        fixture.Write("Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var empty = WithProjectPolicy(Bundle("project-reference-allowlist").Documents[0], "deny-all-project-refs",
            "allowedTargets", Array.Empty<Selector>());
        Assert.Equal("pass", Assert.Single(ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(fixture.Root), RuleLoader.Compose([empty])).Results).Status);

        fixture.Write("Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"Target.csproj\"/></ItemGroup></Project>");
        fixture.Write("Target.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var rejected = ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(fixture.Root), RuleLoader.Compose([empty]));
        Assert.Equal("violation", Assert.Single(rejected.Results).Status);
        Assert.Equal("Target.csproj", Assert.Single(rejected.Findings).Target);
    }

    [Fact]
    public void ProjectAllowlistGlobMatchesResolvedEdgesButMissingEdgesStayInconclusive()
    {
        using var fixture = new Fixture();
        fixture.Write("src/Application/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"../Domain/Domain.csproj\"/></ItemGroup></Project>");
        fixture.Write("src/Domain/Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var glob = WithProjectPolicy(Bundle("project-reference-allowlist").Documents[0], "allow-glob", "source",
            new Selector { Kind = "project", Match = "glob", Value = "src/Application/**" });
        glob = WithProjectPolicy(glob, "allow-glob", "allowedTargets",
            new[] { new Selector { Kind = "project", Match = "glob", Value = "src/Domain/**" } });
        var policy = RuleLoader.Compose([glob]);
        var completeSnapshot = ProjectDiscovery.Discover(fixture.Root);
        var complete = ProjectRuleEvaluator.Evaluate(completeSnapshot, policy);
        Assert.Equal("pass", Assert.Single(complete.Results).Status);

        fixture.Write("src/Application/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"../Missing/Missing.csproj\"/></ItemGroup></Project>");
        var incomplete = ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(fixture.Root), policy);
        Assert.Equal("inconclusive", Assert.Single(incomplete.Results).Status);
        Assert.Empty(incomplete.Findings);
        Assert.NotEmpty(incomplete.Results[0].Limitations);
    }

    [Fact]
    public void NugetAllowlistUsesTheDirectDeclarationWhenVersionComesFromCentralManagement()
    {
        using var fixture = new Fixture();
        fixture.Write("Directory.Packages.props", "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageVersion Include=\"Allowed.Package\" Version=\"1.2.3\"/></ItemGroup></Project>");
        fixture.Write("Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"Allowed.Package\"/></ItemGroup></Project>");
        var result = ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(fixture.Root), Bundle("nuget-allowlist"));
        Assert.Equal("pass", Assert.Single(result.Results).Status);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void AllowRulesIntersectAndDenyRulesRemainIndependent()
    {
        using var fixture = new Fixture();
        fixture.Write("Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"Allowed.csproj\"/><ProjectReference Include=\"Denied.csproj\"/></ItemGroup></Project>");
        fixture.Write("Allowed.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        fixture.Write("Denied.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        var broad = WithProjectPolicy(Bundle("project-reference-allowlist").Documents[0], "allow-broad",
            "allowedTargets", new[] { ProjectSelector("Allowed.csproj"), ProjectSelector("Denied.csproj") });
        var narrow = WithProjectPolicy(Bundle("project-reference-allowlist").Documents[0], "allow-narrow",
            "allowedTargets", new[] { ProjectSelector("Denied.csproj") });
        var deny = WithProjectPolicy(Bundle("project-reference").Documents[0], "deny-allowed",
            "target", ProjectSelector("Allowed.csproj"));
        var denyRule = deny.Ruleset.Rules[0] with { Severity = "error" };
        deny = deny with { Ruleset = deny.Ruleset with { Rules = [denyRule] } };
        var snapshot = ProjectDiscovery.Discover(fixture.Root);
        var first = ProjectRuleEvaluator.Evaluate(snapshot, RuleLoader.Compose([broad, narrow, deny]));
        var reordered = ProjectRuleEvaluator.Evaluate(snapshot, RuleLoader.Compose([deny, broad, narrow]));

        Assert.Equal(JsonSerializer.Serialize(first, JsonContract.Options), JsonSerializer.Serialize(reordered, JsonContract.Options));
        Assert.Equal("pass", first.Results.Single(result => result.RuleId == "allow-broad").Status);
        Assert.Equal("violation", first.Results.Single(result => result.RuleId == "allow-narrow").Status);
        Assert.Equal("violation", first.Results.Single(result => result.RuleId == "deny-allowed").Status);
        Assert.Equal(2, first.Findings.Length);
        Assert.All(first.Findings, finding => Assert.Equal("Allowed.csproj", finding.Target));
        Assert.Equal(["error", "warning"], first.Findings.Select(finding => finding.Severity).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ExceptionForOneRuleCannotAuthorizeAnotherRule()
    {
        using var fixture = new Fixture();
        fixture.Write("Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"Target.csproj\"/></ItemGroup></Project>");
        fixture.Write("Target.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var allow = WithProjectPolicy(Bundle("project-reference-allowlist").Documents[0], "allow-none", "allowedTargets", Array.Empty<Selector>());
        var deny = WithProjectPolicy(Bundle("project-reference").Documents[0], "deny-target", "target", ProjectSelector("Target.csproj"));
        var exception = new RuleException
        {
            Id = "deny-only", RuleId = "deny-target", Scope = ProjectSelector("Domain.csproj"), Reason = "Temporary deny exception"
        };
        deny = deny with { Ruleset = deny.Ruleset with { Exceptions = [exception] } };

        var result = ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(fixture.Root), RuleLoader.Compose([deny, allow]));
        Assert.Equal("pass", result.Results.Single(item => item.RuleId == "deny-target").Status);
        Assert.Equal("violation", result.Results.Single(item => item.RuleId == "allow-none").Status);
        Assert.Null(result.Findings.Single(item => item.RuleId == "allow-none").ExceptionId);
        Assert.Equal("deny-only", result.Findings.Single(item => item.RuleId == "deny-target").ExceptionId);
    }

    [Fact]
    public void ExceptionsRetainTheOriginalFindingAndReason()
    {
        using var fixture = new Fixture();
        var model = fixture.Create("nuget-denylist", true);
        var bundle = Bundle("nuget-denylist");
        var exception = new RuleException
        {
            Id = "migration", RuleId = "nuget-denylist-01", Scope = new() { Kind = "project", Match = "exact", Value = "Domain.csproj" },
            Reason = "Time-limited migration dependency"
        };
        var document = bundle.Documents[0] with { Ruleset = bundle.Documents[0].Ruleset with { Exceptions = [exception] } };
        var result = ProjectRuleEvaluator.Evaluate(model, RuleLoader.Compose([document]));
        Assert.Equal("pass", Assert.Single(result.Results).Status);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("migration", finding.ExceptionId);
        Assert.Equal(exception.Reason, finding.ExceptionReason);
        Assert.Equal("warning", finding.Severity);
    }

    [Fact]
    public void OverlappingExceptionsChooseTheSameMostSpecificMatchRegardlessOfOrder()
    {
        var finding = new Finding("finding", "rule", "project", "src/Domain.csproj", "message", "warning");
        var broad = new RuleException
        {
            Id = "broad", RuleId = "rule", Scope = new() { Kind = "project", Match = "glob", Value = "src/**" },
            Reason = "Broad migration"
        };
        var exact = new RuleException
        {
            Id = "exact", RuleId = "rule", Scope = new() { Kind = "project", Match = "exact", Value = "src/Domain.csproj" },
            Reason = "Exact migration"
        };

        var forward = Assert.Single(ProjectRuleEvaluator.ApplyExceptions([finding], [broad, exact]));
        var reverse = Assert.Single(ProjectRuleEvaluator.ApplyExceptions([finding], [exact, broad]));
        Assert.Equal("exact", forward.ExceptionId);
        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void ExplicitAllowEmptyIsNotApplicableAndDisabledRulesDoNotRun()
    {
        using var fixture = new Fixture(); var snapshot = ProjectDiscovery.Discover(fixture.Root);
        var bundle = Bundle("naming"); var loaded = bundle.Documents[0];
        var rule = loaded.Ruleset.Rules[0] with { Scope = loaded.Ruleset.Rules[0].Scope with { AllowEmpty = true } };
        var allowed = RuleLoader.Compose([loaded with { Ruleset = loaded.Ruleset with { Rules = [rule] } }]);
        Assert.Equal("not-applicable", Assert.Single(ProjectRuleEvaluator.Evaluate(snapshot, allowed).Results).Status);
        var disabled = RuleLoader.Compose([loaded with { Ruleset = loaded.Ruleset with { Rules = [rule with { Enabled = false }] } }]);
        Assert.Empty(ProjectRuleEvaluator.Evaluate(snapshot, disabled).Results);
    }

    [Fact]
    public void MembershipAndExternalCoverageAreHonest()
    {
        using var fixture = new Fixture();
        fixture.Write("Domain.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"../External.csproj\"/></ItemGroup></Project>");
        fixture.Write("Sample.slnx", "<Solution/>");
        var snapshot = ProjectDiscovery.Discover(fixture.Root, "Sample.slnx");
        Assert.Equal("inconclusive", Assert.Single(ProjectRuleEvaluator.Evaluate(snapshot, Bundle("graph-integrity")).Results).Status);
        var bundle = Bundle("graph-integrity"); var loaded = bundle.Documents[0]; var rule = loaded.Ruleset.Rules[0];
        rule = rule with { Parameters = JsonSerializer.SerializeToElement(new { check = "solution-membership" }) };
        var membership = RuleLoader.Compose([loaded with { Ruleset = loaded.Ruleset with { Rules = [rule] } }]);
        Assert.Equal("violation", Assert.Single(ProjectRuleEvaluator.Evaluate(snapshot, membership).Results).Status);
        Assert.Equal("inconclusive", Assert.Single(ProjectRuleEvaluator.Evaluate(ProjectDiscovery.Discover(fixture.Root), membership).Results).Status);
    }

    [Fact]
    public void SourceFileNamingAndDraftFactsDoNotAutomaticallyAllowExistingDependencies()
    {
        using var fixture = new Fixture(); fixture.Write("src/Bad.cs", "class Bad {}");
        var snapshot = ProjectDiscovery.Discover(fixture.Root);
        var bundle = Bundle("naming"); var loaded = bundle.Documents[0]; var rule = loaded.Ruleset.Rules[0];
        rule = rule with
        {
            Scope = new() { Kind = "source-file", Match = "glob", Value = "**" },
            Parameters = JsonSerializer.SerializeToElement(new { subjectKind = "source-file", requiredName = new { match = "glob", value = "src/Good*.cs" } })
        };
        var result = ProjectRuleEvaluator.Evaluate(snapshot, RuleLoader.Compose([loaded with { Ruleset = loaded.Ruleset with { Rules = [rule] } }]));
        Assert.Equal("violation", Assert.Single(result.Results).Status);
        Assert.Equal("src/Bad.cs", Assert.Single(result.Findings).Location);
        var draft = RuleDraftService.Create(snapshot);
        Assert.Equal(snapshot.InputIdentity, draft.ObservedFacts.InputIdentity);
        Assert.All(draft.CandidateRules.Rules, candidate => Assert.False(candidate.Enabled));
        Assert.NotEmpty(draft.UnresolvedDecisions);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(draft.CandidateRules, JsonContract.Options));
        SchemaValidation.Validate(json.RootElement, "ruleset");
    }

    private static RuleBundle Bundle(string type)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ArchSift.slnx"))) root = root.Parent;
        var path = Path.Combine(root!.FullName, "templates", "rules", type + ".json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        var rule = document["rules"]![0]!;
        if (type == "project-reference")
        {
            rule["parameters"]!["source"]!["match"] = "exact"; rule["parameters"]!["source"]!["value"] = "Domain.csproj";
            rule["parameters"]!["target"]!["match"] = "exact"; rule["parameters"]!["target"]!["value"] = "Infrastructure.csproj";
        }
        if (type == "project-reference-allowlist")
        {
            rule["parameters"]!["source"]!["match"] = "exact"; rule["parameters"]!["source"]!["value"] = "Domain.csproj";
            rule["parameters"]!["allowedTargets"] = new JsonArray(JsonNode.Parse("""{"kind":"project","match":"exact","value":"Allowed.csproj"}"""));
        }
        if (type == "target-framework") rule["parameters"]!["allowedFrameworks"] = new JsonArray("net10.0");
        if (type == "nuget-allowlist") rule["parameters"]!["allowedPackageIds"] = new JsonArray("Allowed.Package");
        if (type == "naming") rule["parameters"]!["requiredName"]!["value"] = "Domain";
        var rules = RuleLoader.Parse(Encoding.UTF8.GetBytes(document.ToJsonString()));
        return RuleLoader.Compose([new(rules, new(rules.Id, rules.Version, new string('0', 64), path))]);
    }

    private static Selector ProjectSelector(string value) => new() { Kind = "project", Match = "exact", Value = value };

    private static LoadedRuleset WithProjectPolicy(LoadedRuleset loaded, string id, string parameter, object value)
    {
        var original = loaded.Ruleset.Rules[0];
        var parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(original.Parameters, JsonContract.Options)!;
        parameters[parameter] = JsonSerializer.SerializeToElement(value, JsonContract.Options);
        var rule = original with { Id = id, Parameters = JsonSerializer.SerializeToElement(parameters, JsonContract.Options) };
        return loaded with { Ruleset = loaded.Ruleset with { Rules = [rule] } };
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "archsift-rules-" + Guid.NewGuid().ToString("N"));
        public Fixture() => Directory.CreateDirectory(Root);
        public void Write(string path, string value)
        {
            var full = PathSafety.Under(Root, path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllText(full, value);
        }
        public ProjectSnapshot Create(string type, bool bad)
        {
            var name = type == "naming" && bad ? "Wrong" : "Domain";
            var tfm = type == "target-framework" && bad ? "net9.0" : "net10.0";
            var items = type == "project-reference" && bad ? "<ProjectReference Include=\"Infrastructure.csproj\"/>"
                : type == "project-reference-allowlist" ? $"<ProjectReference Include=\"{(bad ? "Infrastructure" : "Allowed")}.csproj\"/>"
                : type == "graph-integrity" && bad ? "<ProjectReference Include=\"Missing.csproj\"/>"
                : type == "nuget-denylist" && bad ? "<PackageReference Include=\"Forbidden.Package\" Version=\"1.0.0\"/>"
                : type == "nuget-allowlist" ? $"<PackageReference Include=\"{(bad ? "Other" : "Allowed")}.Package\" Version=\"1.0.0\"/>" : "";
            Write(name + ".csproj", $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>{tfm}</TargetFramework></PropertyGroup><ItemGroup>{items}</ItemGroup></Project>");
            if (type == "project-reference" && bad) Write("Infrastructure.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            if (type == "project-reference-allowlist")
                Write((bad ? "Infrastructure" : "Allowed") + ".csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            return ProjectDiscovery.Discover(Root);
        }
        public void Dispose()
        {
            if (!Path.GetFileName(Root).StartsWith("archsift-rules-", StringComparison.Ordinal)) throw new InvalidOperationException();
            Directory.Delete(Root, true);
        }
    }
}
