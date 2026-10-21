using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.TestGate;
using Xunit;

namespace ArchSift.Testing.Tests;

public sealed class PortableRunFailureTests
{
    [Fact]
    public async Task CancelledActualPortableConfigPublishesBlockedCurrentRun()
    {
        var fixture=Environment.GetEnvironmentVariable("ARCHSIFT_SP02_REPORT_FIXTURE")
            ?? throw new InvalidOperationException("Prepare the owned synthetic fixture via Invoke-DevelopmentChecks.ps1.");
        Assert.False(string.IsNullOrWhiteSpace(fixture), "Prepare the owned synthetic fixture via Invoke-DevelopmentChecks.ps1.");
        var config=TestGateService.Load(Path.Combine(fixture,"state/config/full.json"));
        var run=Path.Combine(fixture,"unit-cancel-"+Guid.NewGuid().ToString("N"));
        config=config with{RunDirectory=run,DotnetCliHome=Path.Combine(run,"cli-home")};
        var path=Path.Combine(fixture,"cancel-"+Guid.NewGuid().ToString("N")+".json");
        File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(config,JsonContract.Options));
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        var result=await TestGateService.RunAsync(config,path,cancel.Token);
        Assert.Equal(130,result.ExitCode);Assert.Equal("block",result.Gate);
        using var saved=JsonDocument.Parse(File.ReadAllBytes(result.ReportPath!));
        Assert.False(saved.RootElement.GetProperty("testScope").GetProperty("coverageComplete").GetBoolean());
        Assert.Equal(130,saved.RootElement.GetProperty("outcome").GetProperty("exitCode").GetInt32());
    }

    [Fact]
    public void ReportWriteErrorCannotPublishAuthoritativeSuccess()
    {
        var directory=Path.Combine(Path.GetTempPath(),"archsift-report-error-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory,"architecture-test-run.html"));
        var source=Path.Combine(Path.GetTempPath(),"archsift-report-source-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        var config=FormalAdoptionTests.EmptyConfig() with{TargetRoot=source,RunDirectory=directory};
        var bundle=RuleLoader.Compose([RuleLoader.Load(Path.Combine(AppContext.BaseDirectory,"rules.json"))]);
        var stage=new GateStage("passed",0,null);
        var result=new TestGateResult(0,"allow",[],stage,stage,stage,null,null,directory,true,false);
        Assert.ThrowsAny<IOException>(()=>TestRunReportWriter.Save(config,result,bundle,["original"],"report-error",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow));
        Assert.False(File.Exists(Path.Combine(directory,"architecture-test-run.json")));
    }
}
