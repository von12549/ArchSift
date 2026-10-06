using System.Reflection;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class ProductIdentityTests
{
    [Fact]
    public void ProductAssembliesShareTheVersionReportedByEntrypoints()
    {
        foreach (var assembly in new[]
                 {
                     typeof(ToolIdentity).Assembly,
                     typeof(RuntimeInfo).Assembly,
                     typeof(ArchUnitEngine).Assembly
                 })
        {
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion.Split('+')[0];
            Assert.Equal(ToolIdentity.Version, version);
        }
        Assert.Equal(ToolIdentity.Version, RuntimeInfo.ProductVersion);
    }

    [Fact]
    public void EngineIdentityDistinguishesPinnedPackageFromLoadedAssembly()
    {
        var adapter = new ArchUnitEngine();
        IAssemblyAnalysisEngine engine = adapter;
        Assert.Equal("archunitnet", engine.Name);
        Assert.Equal("0.13.4", engine.Version);
        Assert.Equal(typeof(ArchUnitNET.Loader.ArchLoader).Assembly.GetName().Version!.ToString(), adapter.LoadedAssemblyVersion);
    }
}
