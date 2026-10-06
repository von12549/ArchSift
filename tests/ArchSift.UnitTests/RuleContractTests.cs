using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class RuleContractTests
{
    public static TheoryData<string> Types => new()
    { "project-reference", "graph-integrity", "target-framework", "nuget-denylist", "type-dependency", "naming" };

    [Theory]
    [MemberData(nameof(Types))]
    public void EachTemplatePassesSchemaAndSemantics(string type)
    {
        var loaded = Template(type);
        var bundle = RuleLoader.Compose([loaded]);
        Assert.Single(bundle.Rules);
        Assert.Equal(type, bundle.Rules[0].Type);
        Assert.False(bundle.Rules[0].Scope.AllowEmpty);
        Assert.Equal(64, loaded.Identity.Sha256.Length);
        Assert.Contains(loaded.Identity.Sha256, RuleMarkdown.Render(loaded));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void UnknownParameterIsRejectedForEveryRuleType(string type)
    {
        var document = JsonNode.Parse(File.ReadAllText(TemplatePath(type)))!;
        document["rules"]![0]!["parameters"]!["unexpected"] = true;
        Assert.Throws<ConfigurationException>(() => Parse(document));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void MissingRequiredParameterIsRejectedForEveryRuleType(string type)
    {
        var document = JsonNode.Parse(File.ReadAllText(TemplatePath(type)))!;
        var parameters = document["rules"]![0]!["parameters"]!.AsObject();
        parameters.Remove(parameters.First().Key);
        Assert.Throws<ConfigurationException>(() => Parse(document));
    }

    [Theory]
    [InlineData("type", "custom-code")]
    [InlineData("severity", "blocking")]
    [InlineData("reason", " ")]
    public void UnsupportedOrBlankRuleFieldsAreRejected(string field, string value)
    {
        var document = JsonNode.Parse(File.ReadAllText(TemplatePath("project-reference")))!;
        document["rules"]![0]![field] = value;
        Assert.Throws<ConfigurationException>(() => Parse(document));
    }

    [Fact]
    public void DuplicateIdsAcrossFilesAndDuplicateJsonPropertiesAreRejected()
    {
        var template = Template("project-reference");
        Assert.Throws<ConfigurationException>(() => RuleLoader.Compose([template, template]));
        var json = File.ReadAllText(TemplatePath("project-reference")).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1");
        Assert.Throws<ConfigurationException>(() => RuleLoader.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void ExceptionsMustReferenceARealRuleAndRetainTheReason()
    {
        var doc = JsonNode.Parse(File.ReadAllText(TemplatePath("project-reference")))!;
        doc["exceptions"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"exception-01","ruleId":"missing","scope":{"kind":"project","match":"glob","value":"**"},"reason":"Documented migration window"}
            """));
        var parsed = Parse(doc);
        var loaded = new LoadedRuleset(parsed, Template("project-reference").Identity);
        Assert.Throws<ConfigurationException>(() => RuleLoader.Compose([loaded]));
        doc["exceptions"]![0]!["ruleId"] = "project-reference-01";
        parsed = Parse(doc);
        Assert.Equal("Documented migration window", parsed.Exceptions[0].Reason);
        RuleLoader.Compose([new(parsed, loaded.Identity)]);
    }

    [Fact]
    public void DisjointTfmPoliciesAreConfigurationErrors()
    {
        var a = JsonNode.Parse(File.ReadAllText(TemplatePath("target-framework")))!;
        var b = a.DeepClone();
        a["rules"]![0]!["parameters"]!["allowedFrameworks"] = new JsonArray("net8.0");
        b["rules"]![0]!["id"] = "other-rule";
        b["rules"]![0]!["parameters"]!["allowedFrameworks"] = new JsonArray("net10.0");
        Assert.Throws<ConfigurationException>(() => RuleLoader.Compose([Loaded(a), Loaded(b)]));
    }

    [Fact]
    public void DisjointNamingGlobsAreConfigurationErrors()
    {
        var a = JsonNode.Parse(File.ReadAllText(TemplatePath("naming")))!;
        var b = a.DeepClone();
        a["rules"]![0]!["parameters"]!["requiredName"]!["value"] = "Foo*";
        b["rules"]![0]!["id"] = "other-naming";
        b["rules"]![0]!["parameters"]!["requiredName"]!["value"] = "Bar*";
        Assert.Throws<ConfigurationException>(() => RuleLoader.Compose([Loaded(a), Loaded(b)]));
    }

    [Fact]
    public void AllowEmptyRemainsAnExplicitChoiceRatherThanAnImplicitPass()
    {
        var doc = JsonNode.Parse(File.ReadAllText(TemplatePath("type-dependency")))!;
        var parsed = Parse(doc);
        Assert.False(parsed.Rules[0].Scope.AllowEmpty);
        doc["rules"]![0]!["scope"]!["allowEmpty"] = true;
        parsed = Parse(doc);
        Assert.True(parsed.Rules[0].Scope.AllowEmpty);
    }

    [Theory]
    [InlineData("src/**/A.csproj", "src/A.csproj", true)]
    [InlineData("src/**/A.csproj", "src/feature/A.csproj", true)]
    [InlineData("src/*.csproj", "src/feature/A.csproj", false)]
    [InlineData("src/?.csproj", "src/A.csproj", true)]
    [InlineData("src/?.csproj", "src/AA.csproj", false)]
    [InlineData("src/A.csproj", "src/a.csproj", false)]
    public void PathGlobGrammarIsExplicit(string pattern, string candidate, bool expected)
    {
        Assert.Equal(expected, SelectorMatcher.Matches(new() { Kind = "project", Match = "glob", Value = pattern }, candidate));
    }

    [Fact]
    public void LongCandidatesUseBoundedStackAndUtf8BomIsAccepted()
    {
        Assert.True(SelectorMatcher.Matches(new() { Kind = "project", Match = "glob", Value = "**/A.csproj" },
            new string('a', 6000) + "/A.csproj"));
        var bytes = new byte[] { 0xef, 0xbb, 0xbf }.Concat(File.ReadAllBytes(TemplatePath("naming"))).ToArray();
        Assert.Equal("template-naming", RuleLoader.Parse(bytes).Id);
    }

    [Theory]
    [InlineData("../*.csproj")]
    [InlineData("D:/src/*.csproj")]
    [InlineData("[a-z]+")]
    public void UnsafeOrRegexPathSelectorsAreRejected(string value) =>
        Assert.Throws<ConfigurationException>(() => RuleSemantics.ValidateSelector(new() { Kind = "project", Match = "glob", Value = value }));

    [Fact]
    public void RendererEscapesHtmlAndDoesNotReadMarkdownAsRules()
    {
        var template = Template("naming");
        var rules = template.Ruleset with { Description = "<script>alert(1)</script>" };
        var output = RuleMarkdown.Render(template with { Ruleset = rules });
        Assert.DoesNotContain("<script>", output);
        Assert.Contains("&lt;script&gt;", output);
        Assert.Equal(template.Identity.Sha256, ContentHash.Bytes(File.ReadAllBytes(TemplatePath("naming"))));
    }

    [Fact]
    public void FourSchemasRejectUnknownTopLevelFields()
    {
        foreach (var name in new[] { "ruleset", "config", "report", "assembly-manifest" })
        {
            using var value = JsonDocument.Parse("{\"schemaVersion\":1,\"unexpected\":true}");
            Assert.Throws<ConfigurationException>(() => SchemaValidation.Validate(value.RootElement, name));
        }
    }

    [Fact]
    public void ReportAndAssemblyManifestRoundTripAgainstTheirSchemas()
    {
        var identity = new InputIdentity(new string('0', 64), []);
        var report = new AnalysisReport
        {
            Operation = "analyze", InputIdentity = identity,
            Scope = new([], [], [], [], []), BuildContext = new("existing", null, "Debug", null, "none"),
            Execution = "completed", Compliance = null, Coverage = new(0, 0, false),
            RunMetadata = new("run-01", "2026-10-06T00:00:00Z", 0, "sample")
        };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report, JsonContract.Options));
        SchemaValidation.Validate(json.RootElement, "report");
        var invalid = JsonNode.Parse(json.RootElement.GetRawText())!;
        invalid["execution"] = "success";
        using var invalidJson = JsonDocument.Parse(invalid.ToJsonString());
        Assert.Throws<ConfigurationException>(() => SchemaValidation.Validate(invalidJson.RootElement, "report"));

        var manifest = new AssemblyManifest
        {
            InputIdentity = identity, BuildInputIdentity = identity, Sdk = "10.0.303",
            TargetFramework = "net10.0", Configuration = "Debug", Generation = "external",
            Assemblies = [new("Sample", "Sample.csproj", "Sample.dll", new string('1', 64))]
        };
        using var assemblyJson = JsonDocument.Parse(JsonSerializer.Serialize(manifest, JsonContract.Options));
        SchemaValidation.Validate(assemblyJson.RootElement, "assembly-manifest");
    }

    [Fact]
    public void ConfigPathResolutionDoesNotDependOnInvocationCwd()
    {
        var directory = Path.Combine(Path.GetTempPath(), "archsift-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "config.json");
            File.WriteAllText(path, """
                {"schemaVersion":1,"target":{"root":"./target","entry":"Sample.sln"},"rulesets":["./rules.json"],"output":{"directory":"../output","formats":["json"]}}
                """);
            var config = ConfigLoader.Load(path);
            Assert.Equal(Path.GetFullPath("target", directory), config.Target.Root);
            Assert.Equal(Path.GetFullPath("rules.json", directory), config.Rulesets[0]);
            Assert.Equal("existing", config.Build.Mode);
            Assert.Equal("Debug", config.Build.Configuration);
            Assert.False(config.Build.AllowNetwork);
            Assert.Equal(Path.GetFullPath("../output", directory), config.Output.Directory);
            using var roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(config, JsonContract.Options));
            SchemaValidation.Validate(roundTrip.RootElement, "config");
        }
        finally { File.Delete(Path.Combine(directory, "config.json")); Directory.Delete(directory); }
    }

    private static Ruleset Parse(JsonNode doc) => RuleLoader.Parse(Encoding.UTF8.GetBytes(doc.ToJsonString()));
    private static LoadedRuleset Loaded(JsonNode doc) => new(Parse(doc), new("test", "1", new string('0', 64), "test.json"));
    private static LoadedRuleset Template(string type) => RuleLoader.Load(TemplatePath(type));
    private static string TemplatePath(string type)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "templates", "rules", type + ".json");
    }
}
