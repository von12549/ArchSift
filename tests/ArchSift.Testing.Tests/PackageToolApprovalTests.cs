using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Testing;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class PackageToolApprovalTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("wrong-version")]
    [InlineData("missing-dependency")]
    [InlineData("unknown-dependency")]
    [InlineData("changed-content")]
    [InlineData("source-project")]
    [InlineData("arbitrary-tool-package")]
    [InlineData("conditional")]
    [InlineData("override")]
    [InlineData("cli-version")]
    [InlineData("cli-command")]
    [InlineData("existing-tool")]
    [InlineData("missing-cli")]
    [InlineData("duplicate-cli")]
    [InlineData("command-collision")]
    [InlineData("unapproved-archsift-tool")]
    public void ExactToolApprovalRetainsClosedBoundary(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-package-approval-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,"tests")); Directory.CreateDirectory(Path.Combine(root,".config"));
        var version = ToolIdentity.Version;
        var declaration = $"<PackageReference Include=\"ArchSift.Testing\" Version=\"[{version}]\" />";
        declaration = scenario switch
        {
            "wrong-version" => declaration.Replace(version,"999.0.0",StringComparison.Ordinal),
            "arbitrary-tool-package" => declaration + "<PackageReference Include=\"ArchSift.Unapproved\" Version=\"[1.0.0]\" />",
            "conditional" => declaration.Replace(" />"," Condition=\"'$(Configuration)' == 'Release'\" />",StringComparison.Ordinal),
            "override" => declaration.Replace(" />"," VersionOverride=\"[999.0.0]\" />",StringComparison.Ordinal),
            _ => declaration
        };
        File.WriteAllText(Path.Combine(root,"tests/test.csproj"),"<Project><ItemGroup>"+declaration+"</ItemGroup></Project>");
        var graph = new Dictionary<string,object>
        {
            ["ArchSift.Testing"] = new { type="Direct",resolved=version,contentHash="test-hash", dependencies=new Dictionary<string,string>{{"ArchSift.ArchUnit","["+version+"]"}} },
            ["ArchSift.ArchUnit"] = new { type=scenario=="source-project"?"Project":"Transitive",resolved=version,contentHash=scenario=="changed-content"?"changed":"engine-hash" }
        };
        if (scenario=="missing-dependency") graph.Remove("ArchSift.ArchUnit");
        if (scenario=="unknown-dependency") graph["ArchSift.Testing"]=new {type="Direct",resolved=version,contentHash="test-hash",dependencies=new Dictionary<string,string>{{"Arbitrary.Dependency","[1.0.0]"}}};
        Write("tests/packages.lock.json",new {version=2,dependencies=new Dictionary<string,object>{{"net10.0",graph}}});
        Write("approval.json",new PackageApproval(1,version,version,[new("ArchSift.Testing",version,"test-hash"),new("ArchSift.ArchUnit",version,"engine-hash")]));
        var tools = new Dictionary<string,object>{{"archsift.cli",new {version=scenario=="cli-version"?"999.0.0":version,commands=new[]{scenario=="cli-command"?"arbitrary":"archsift"}}}};
        if(scenario=="existing-tool") tools.Add("dotnet-ef",new {version="10.0.12",commands=new[]{"dotnet-ef"},rollForward=false});
        if(scenario=="missing-cli") tools.Remove("archsift.cli");
        if(scenario=="duplicate-cli") tools.Add("ArchSift.Cli",new {version,commands=new[]{"archsift"}});
        if(scenario=="command-collision") tools.Add("other-tool",new {version="1.0.0",commands=new[]{"ARCHSIFT"}});
        if(scenario=="unapproved-archsift-tool") tools.Add("ArchSift.Other",new {version,commands=new[]{"other"}});
        Write(".config/dotnet-tools.json",new {version=1,isRoot=true,tools});
        if(scenario is "valid" or "existing-tool") PackageToolApproval.Verify(root,"tests/test.csproj","approval.json");
        else Assert.ThrowsAny<Exception>(()=>PackageToolApproval.Verify(root,"tests/test.csproj","approval.json"));
        void Write(string path,object value)=>File.WriteAllText(Path.Combine(root,path),JsonSerializer.Serialize(value,JsonContract.Options));
    }
}
