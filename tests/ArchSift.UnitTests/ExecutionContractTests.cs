using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class ExecutionContractTests
{
    [Theory]
    [InlineData("{}", true)]
    [InlineData("{\"maxConcurrency\":1}", true)]
    [InlineData("{\"maxConcurrency\":4}", true)]
    [InlineData("{\"maxConcurrency\":0}", false)]
    [InlineData("{\"maxConcurrency\":5}", false)]
    [InlineData("{\"maxConcurrency\":1.5}", false)]
    [InlineData("{\"maxConcurrency\":\"2\"}", false)]
    [InlineData("{\"maxConcurrency\":2,\"maxConcurrency\":1}", false)]
    [InlineData("{\"needs\":[]}", false)]
    public void ConcurrencyBoundsAndUnknownFieldsAreEnforced(string json, bool valid)
    {
        using var doc = JsonDocument.Parse(json);
        if (valid)
        {
            SchemaValidation.Validate(doc.RootElement, "chain-run-options");
            Assert.InRange(doc.RootElement.Deserialize<ChainRunOptions>(JsonContract.Options)!.MaxConcurrency, 1, 4);
        }
        else Assert.Throws<ConfigurationException>(() => SchemaValidation.Validate(doc.RootElement, "chain-run-options"));
    }

    [Fact]
    public void CatalogCannotPersistTokenOrClaimItsOwnProtectionAndHasABound()
    {
        var entry = new ProfileEntry(new string('a', 32), "default.json", "D:/fixture/config/default.json");
        void Validate(ProfileCatalog catalog)
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(catalog, JsonContract.Options));
            SchemaValidation.Validate(doc.RootElement, "profile-catalog");
        }
        Validate(new(1, entry.ProfileId, [entry]));
        Assert.Throws<ConfigurationException>(() => Validate(new(1, entry.ProfileId, Enumerable.Repeat(entry, 101).ToArray())));
        foreach (var extra in new[] { "token", "url", "configProtected" })
        {
            var json = JsonSerializer.Serialize(new ProfileCatalog(1, null, []), JsonContract.Options);
            using var doc = JsonDocument.Parse(json.TrimEnd()[..^1] + ",\"" + extra + "\":true}");
            Assert.Throws<ConfigurationException>(() => SchemaValidation.Validate(doc.RootElement, "profile-catalog"));
        }
    }
}
