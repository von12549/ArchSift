using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class ProjectDiscoveryTests
{
    [Fact]
    public void SolutionMembersUnlistedAndExternalMissingReferencesRemainDistinct()
    {
        using var fixture = new Fixture();
        fixture.Write("Directory.Build.props", "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        fixture.Write("src/A/A.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"../B/B.csproj\"/><ProjectReference Include=\"../Missing/Missing.csproj\"/><ProjectReference Include=\"../../../Outside.csproj\"/></ItemGroup></Project>");
        fixture.Write("src/B/B.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"/>");
        fixture.Write("Sample.slnx", "<Solution><Folder Name=\"/src/\"><Project Path=\"src/A/A.csproj\"/></Folder></Solution>");
        var model = ProjectDiscovery.Discover(fixture.Root, "Sample.slnx");
        Assert.Equal(2, model.Projects.Length);
        Assert.Equal(["src/A/A.csproj"], model.Scope.IncludedProjects);
        Assert.Equal(["src/B/B.csproj"], model.Scope.UnlistedProjects);
        Assert.Single(model.Scope.ExternalReferences);
        Assert.Single(model.Scope.UnresolvedReferences);
        Assert.Equal("Directory.Build.props", model.Projects[0].FrameworkSource);
        Assert.Equal("declared", model.Scope.Model);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "src/A/obj")));
    }

    [Fact]
    public void SlnEntryAndLiteralCentralPackagesAreResolvedWithoutMsbuild()
    {
        using var fixture = new Fixture();
        fixture.Write("Directory.Packages.props", "<Project><ItemGroup><PackageVersion Include=\"Example.Package\" Version=\"1.2.3\"/></ItemGroup></Project>");
        fixture.Write("A.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"example.package\"/></ItemGroup></Project>");
        fixture.Write("Sample.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\nProject(\"{GUID}\") = \"A\", \"A.csproj\", \"{ID}\"\nEndProject\nGlobal\nEndGlobal\n");
        var model = ProjectDiscovery.Discover(fixture.Root, "Sample.sln");
        Assert.Equal(["A.csproj"], model.Scope.IncludedProjects);
        Assert.Equal("1.2.3", Assert.Single(model.Projects[0].Packages).Version);
        Assert.True(model.Projects[0].PackagesComplete);
    }

    [Fact]
    public void ConditionalAndExpressionFactsDoNotBecomeEmptyCompleteGraphs()
    {
        using var fixture = new Fixture();
        fixture.Write("A.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup Condition=\"'$(Configuration)' == 'Debug'\"><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"$(Other).csproj\"/><PackageReference Include=\"Example\" Condition=\"'$(Configuration)' == 'Debug'\"/></ItemGroup></Project>");
        var project = Assert.Single(ProjectDiscovery.Discover(fixture.Root).Projects);
        Assert.False(project.FrameworkComplete);
        Assert.False(project.ReferencesComplete);
        Assert.False(project.PackagesComplete);
        Assert.Equal("unsupported", Assert.Single(project.References).Status);
        Assert.Equal("unsupported", Assert.Single(project.Packages).Status);
    }

    [Fact]
    public void MultitfmRequiresExplicitSelectionAndDoesNotInventOtherTfmResults()
    {
        using var fixture = new Fixture();
        fixture.Write("A.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net8.0;net10.0</TargetFrameworks></PropertyGroup></Project>");
        Assert.Throws<ConfigurationException>(() => ProjectDiscovery.Discover(fixture.Root));
        var snapshot = ProjectDiscovery.Discover(fixture.Root, targetFramework: "net10.0");
        Assert.Equal("net10.0", snapshot.TargetFramework);
        Assert.Equal(["net8.0", "net10.0"], snapshot.Projects[0].Frameworks);
    }

    [Fact]
    public void IdeAndOrdinaryGeneratedFilesDoNotInvalidateButExplicitGeneratedInputsDo()
    {
        using var fixture = new Fixture();
        fixture.Write("A.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"obj/Generated.cs\"/><AdditionalFiles Include=\"data.json\"/></ItemGroup></Project>");
        fixture.Write("A.cs", "class A {}"); fixture.Write("obj/Generated.cs", "class Generated {}"); fixture.Write("data.json", "{}");
        var before = InputCapture.Capture(fixture.Root);
        fixture.Write(".vs/cache.cs", "IDE state"); fixture.Write("bin/Other.cs", "irrelevant");
        InputCapture.VerifyUnchanged(fixture.Root, before);
        fixture.Write("obj/Generated.cs", "class Changed {}");
        Assert.Throws<SourceChangedException>(() => InputCapture.VerifyUnchanged(fixture.Root, before));
        Assert.Contains(before.Files, f => f.Path == "obj/Generated.cs");
        Assert.Contains(before.Files, f => f.Path == "data.json");
    }

    [Fact]
    public void RelatedSourceSaveAndNewSourceInvalidateTheIdentity()
    {
        using var fixture = new Fixture(); fixture.Write("A.cs", "class A {}");
        var before = InputCapture.Capture(fixture.Root);
        fixture.Write("B.cs", "class B {}");
        Assert.Throws<SourceChangedException>(() => InputCapture.VerifyUnchanged(fixture.Root, before));
        Assert.Equal(InputCapture.Capture(fixture.Root).Sha256, InputCapture.Capture(fixture.Root).Sha256);
    }

    [Fact]
    public void RootTraversalOutputOverlapAndUnsupportedProjectsAreExplicit()
    {
        using var fixture = new Fixture();
        Assert.Throws<ConfigurationException>(() => PathSafety.Under(fixture.Root, "../outside.sln"));
        Assert.Throws<ConfigurationException>(() => PathSafety.EnsureDisjoint(fixture.Root, Path.Combine(fixture.Root, "reports")));
        fixture.Write("Legacy.csproj", "<Project><PropertyGroup><TargetFrameworkVersion>v4.8</TargetFrameworkVersion></PropertyGroup></Project>");
        fixture.Write("Other.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"/>");
        var model = ProjectDiscovery.Discover(fixture.Root);
        Assert.False(Assert.Single(model.Projects).Supported);
        Assert.Contains(model.Scope.UnsupportedConstructs, s => s.Contains("unsupported project language"));
    }

    [Fact]
    public async Task ReparsePointAndDtdInputsAreNotFollowed()
    {
        using var fixture = new Fixture(); using var external = new Fixture();
        external.Write("Hidden.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"/>");
        var link = Path.Combine(fixture.Root, "linked");
        if (OperatingSystem.IsWindows())
        {
            var start = new System.Diagnostics.ProcessStartInfo("pwsh")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("New-Item -ItemType Junction -Path '" + link.Replace("'", "''") +
                "' -Target '" + external.Root.Replace("'", "''") + "' -ErrorAction Stop | Out-Null");
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw; }
            await output; await error;
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, external.Root);
        try { Assert.Throws<ConfigurationException>(() => InputCapture.Capture(fixture.Root)); }
        finally { Directory.Delete(link); }
        fixture.Write("A.csproj", "<!DOCTYPE Project [<!ENTITY x SYSTEM 'file:///does-not-exist'>]><Project Sdk=\"Microsoft.NET.Sdk\">&x;</Project>");
        var result = ProjectDiscovery.Discover(fixture.Root);
        Assert.False(Assert.Single(result.Projects).Supported);
        Assert.Contains(result.Scope.UnsupportedConstructs, l => l.Contains("Invalid project XML"));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "archsift-discovery-" + Guid.NewGuid().ToString("N"));
        public Fixture() => Directory.CreateDirectory(Root);
        public void Write(string relative, string content)
        {
            var path = PathSafety.Under(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content);
        }
        public void Dispose()
        {
            if (!Path.GetDirectoryName(Root)!.Equals(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), PathSafety.Comparison) ||
                !Path.GetFileName(Root).StartsWith("archsift-discovery-", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe fixture cleanup.");
            Directory.Delete(Root, true);
        }
    }
}
