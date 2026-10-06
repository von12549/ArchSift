using System.Xml.Linq;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.ArchitectureTests;

public sealed class DependencyDirectionTests
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["ArchSift.Contracts"] = [],
            ["ArchSift.Core"] = ["ArchSift.Contracts"],
            ["ArchSift.ArchUnit"] = ["ArchSift.Contracts", "ArchSift.Core"],
            ["ArchSift.Cli"] = ["ArchSift.Core", "ArchSift.ArchUnit"],
            ["ArchSift.Web"] = ["ArchSift.Core", "ArchSift.ArchUnit"]
        };

    [Fact]
    public void EveryProductionProjectReferenceFollowsTheApprovedDirection()
    {
        var root = FindRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories);
        Assert.Equal(5, projects.Length);
        foreach (var project in projects)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            Assert.True(AllowedReferences.ContainsKey(name), $"Unreviewed production project: {name}");
            var document = XDocument.Load(project);
            foreach (var reference in document.Descendants("ProjectReference"))
            {
                var include = reference.Attribute("Include")!.Value;
                var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include));
                Assert.True(File.Exists(target), $"Missing project reference: {include}");
                Assert.Contains(Path.GetFileNameWithoutExtension(target), AllowedReferences[name]);
            }
            if (name is "ArchSift.Contracts" or "ArchSift.Core")
                Assert.Empty(document.Descendants("PackageReference"));
        }
    }

    [Fact]
    public void CompiledCoreAndContractsHaveNoEntrypointOrGuardDependencies()
    {
        foreach (var assembly in new[] { typeof(RuntimeInfo).Assembly, typeof(ToolIdentity).Assembly })
        {
            var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();
            Assert.DoesNotContain(references, name =>
                name.Contains("Guard", StringComparison.OrdinalIgnoreCase) ||
                name is "archsift" or "ArchSift.Web" or "ArchSift.ArchUnit" or "ArchUnitNET");
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchSift.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("ArchSift.slnx not found.");
    }
}
