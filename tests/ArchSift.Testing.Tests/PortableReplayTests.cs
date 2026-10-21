using System.Text.Json;
using System.Text.Json.Nodes;
using ArchSift.Contracts;
using ArchSift.TestGate;
using ArchSift.Testing;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class PortableReplayTests
{
    [Fact]
    public void ActualSyntheticOutputRejectsMissingReplacedDuplicateSkippedUnknownAndEmptyCoverage()
    {
        var fixture = Environment.GetEnvironmentVariable("ARCHSIFT_SP02_REPORT_FIXTURE")
            ?? throw new InvalidOperationException("Prepare the owned synthetic fixture via Invoke-DevelopmentChecks.ps1.");
        Assert.False(string.IsNullOrWhiteSpace(fixture), "Prepare the owned synthetic fixture via Invoke-DevelopmentChecks.ps1.");
        var config=TestGateService.Load(Path.Combine(fixture,"state/config/full.json"));
        var manifest=RequiredCases.Load(config);
        var directory=Path.Combine(fixture,"state/runs/full");
        var discovery=File.ReadAllBytes(Path.Combine(directory,"discovery.log"));
        var report=JsonNode.Parse(File.ReadAllBytes(Path.Combine(directory,"test-results.ctrf.json")))!;
        var expected=ArchitectureTestAdapter.LoadCases(config.Rulesets.Select(path=>Path.Combine(config.TargetRoot,path))).Cases
            .ToDictionary(item=>item.CaseId,item=>item.Ruleset.Identity.Sha256,StringComparer.Ordinal);
        MtpRun Parse(JsonNode input)=>MtpCtrf.Parse(discovery,JsonSerializer.SerializeToUtf8Bytes(input),true);
        GateCoverage Assess(MtpRun run)=>RequiredCases.Assess(manifest,run,expected,0);
        var actual=Parse(report);
        Assert.Equal(0,Assess(actual).ExitCode);
        Assert.All(actual.Tests,item=>{Assert.NotNull(item.DiscoveryUid);Assert.NotEqual(item.Id,item.DiscoveryUid);});
        var original=actual.Tests.First(item=>item.JsonCaseId is null);
        Assert.False(Assess(actual with{Tests=actual.Tests.Where(item=>item!=original).ToArray()}).Complete);
        var identity=original.Identity!;
        var theory=actual.Tests.First(item=>item.Identity!.Identity.Method=="OriginalRows");
        var replaced=theory with{Identity=theory.Identity! with{Identity=theory.Identity.Identity with{Arguments=[theory.Identity.Identity.Arguments[0] with{Value="same-count-replaced"}]}}};
        Assert.False(Assess(actual with{Tests=actual.Tests.Select(item=>item==theory?replaced:item).ToArray()}).Complete);
        Assert.False(Assess(actual with{Tests=[..actual.Tests,original]}).Complete);
        Assert.False(Assess(actual with{Tests=actual.Tests.Select(item=>item==original?item with{Result="skipped"}:item).ToArray()}).Complete);
        Assert.False(Assess(actual with{AssemblyFinished=false}).Complete);
        Assert.False(Assess(actual with{Tests=[]}).Complete);
        var unknown=report.DeepClone();
        var rows=unknown["results"]!["tests"]!.AsArray();
        var marker=rows[0]!["stdout"]![0]!.GetValue<string>();
        var projection=CaseIdentities.Read(marker)!;
        rows[0]!["stdout"]![0]=CaseIdentities.Marker+CaseIdentities.Serialize(projection with{XunitCaseId="unknown-uid"});
        Assert.Throws<InvalidDataException>(()=>Parse(unknown));
        var changed=actual with{Tests=actual.Tests.Select(item=>item.JsonCaseId is null?item:item with{RulesetSha256=new('0',64)}).ToArray()};
        Assert.False(Assess(changed).Complete);
        var history=Path.Combine(fixture,"history-compat-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(history);
        var runId="portable-history-"+Guid.NewGuid().ToString("N");
        TestRunHistory.Begin(history,runId,config.TargetRoot,DateTimeOffset.UtcNow);
        var saved=JsonNode.Parse(File.ReadAllBytes(Path.Combine(directory,"architecture-test-run.json")))!;
        saved["run"]!["id"]=runId;
        var reportPath=Path.Combine(history,"architecture-test-run.json");
        File.WriteAllText(reportPath,saved.ToJsonString());
        File.Copy(Path.Combine(directory,"architecture-test-run.html"),Path.Combine(history,"architecture-test-run.html"));
        var stage=new GateStage("passed",0,null);
        TestRunHistory.Finish(history,runId,new(0,"allow",[],stage,stage,stage,null,null,history,true,false){ReportPath=reportPath});
        var known=TestRunHistory.List(fixture).Items.Single(item=>item.RunId==runId);
        Assert.Equal("allow",known.Gate);
        saved["inputs"]!["caseManifest"]!["schemaVersion"]=99;
        File.WriteAllText(reportPath,saved.ToJsonString());
        var unknownHistory=TestRunHistory.List(fixture).Items.Single(item=>item.RunId==runId);
        Assert.Equal("unavailable",unknownHistory.State);Assert.Null(unknownHistory.Gate);
    }
}
