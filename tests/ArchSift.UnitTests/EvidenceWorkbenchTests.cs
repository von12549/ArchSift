using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class EvidenceWorkbenchTests
{
    private static readonly string Fixture = Path.Combine(Path.GetTempPath(), "archsift-evidence-plan-" + Guid.NewGuid().ToString("N"));
    private static RunConfiguration Config => new() { SchemaVersion = 1, Target = new() { Root = Path.Combine(Fixture, "source") }, Output = new() { Directory = Path.Combine(Fixture, "reports") } };
    [Fact]
    public void DefaultAndCustomRootAreExplicitAndDoNotCreateDirectories()
    {
        var data = Path.Combine(Fixture, "ArchSift");
        var plan = EvidenceWorkbench.Plan(Config, new(data, null, "project", "run", []), []);
        Assert.Equal(Path.Combine(data, "evidence", "project", "run", "assembly-manifest-v3.json"), plan.ManifestPath);
        Assert.False(Directory.Exists(plan.RunDirectory));
        var custom = Path.Combine(Fixture, "custom");
        Assert.Equal(custom, EvidenceWorkbench.Plan(Config, new(data, custom, "project", "run", []), []).EvidenceRoot);
        Assert.Throws<ConfigurationException>(() => EvidenceWorkbench.Plan(Config, new(null, null, "project", "run", []), []));
        Assert.Throws<ConfigurationException>(() => EvidenceWorkbench.Plan(Config, new(data, Config.Target.Root, "project", "run", []), []));
        Assert.Throws<ConfigurationException>(() => EvidenceWorkbench.Plan(Config, new(data, Path.Combine(data, "versions", "evidence"), "project", "run", []), []));
        Assert.Throws<ConfigurationException>(() => EvidenceWorkbench.Plan(Config, new(data, custom, "project", "run", []), [Fixture]));
    }
    [Theory]
    [InlineData("../other")]
    [InlineData("CON")]
    [InlineData("NUL.json")]
    [InlineData("name.")]
    public void IdentityCannotEscapeOrUseReservedNames(string id) => Assert.Throws<ConfigurationException>(() =>
        EvidenceWorkbench.Plan(Config, new(Fixture, null, id, "run", []), []));
    [Fact]
    public void CancelledValidationAndHashingDoNotReadOrExecuteTarget()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => AssemblyArtifacts.FileHash("nonexistent", cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => EvidenceWorkbench.Validate(Config with { Build = new() { AssemblyManifest = "nonexistent" } }, cancelled.Token));
    }
}
