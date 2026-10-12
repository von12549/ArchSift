[CmdletBinding()]
param([string]$LabRoot='D:\ArchSift-lab')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('runs/budget-0.7-'+[Guid]::NewGuid().ToString('N'))
if($run.StartsWith($repository+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Reference artifacts must be external.'}
$fixture=Join-Path $run 'fixture'
try{
    for($index=0;$index-lt100;$index++){
        $name='P'+$index.ToString('D3');$directory=Join-Path $fixture $name;[void][IO.Directory]::CreateDirectory($directory)
        $reference=if($index-gt0){'<ItemGroup><ProjectReference Include="../P'+($index-1).ToString('D3')+'/P'+($index-1).ToString('D3')+'.csproj" /></ItemGroup>'}else{''}
        [IO.File]::WriteAllText((Join-Path $directory ($name+'.csproj')),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>'+$reference+'</Project>')
        [IO.File]::WriteAllText((Join-Path $directory ($name+'.cs')),'namespace '+$name+'; public class Marker {}')
    }
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command dotnet).Source;$start.WorkingDirectory=$repository;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in @((Join-Path $repository 'src/ArchSift.Cli/bin/Release/net10.0/archsift.dll'),'analyze','--target',$fixture,'--output',(Join-Path $run 'reports'))){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start)
    try{$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();if(-not$process.WaitForExit(30000)){$process.Kill($true);throw 'Reference timeout.'};[Threading.Tasks.Task]::WaitAll($stdout,$stderr);[IO.File]::WriteAllText((Join-Path $run 'stdout.json'),$stdout.Result);[IO.File]::WriteAllText((Join-Path $run 'stderr.log'),$stderr.Result);if($process.ExitCode-ne0){throw 'Reference analysis failed.'};$report=$stdout.Result|ConvertFrom-Json}finally{$process.Dispose()}
    if($report.toolVersion-cne'0.7.0'-or$report.coverage.projectCount-ne100){throw 'Reference version/count differs.'}
    $budget=Get-Content -Raw -LiteralPath (Join-Path $repository 'docs/performance/0.6-budget.json')|ConvertFrom-Json
    $budget.date='2026-10-12';$budget.toolVersion='0.7.0';$budget.referenceSourceCommit=(& git -C $repository rev-parse HEAD).Trim();$budget.inputSha256=$report.inputIdentity.sha256
    $budget.status='frozen-carried-thresholds-candidate-qualification-pending';$budget.releasePassed=$false
    $budget.rationale='Ordinary-analysis thresholds are carried unchanged from accepted 0.6.0. Refresh the version-bound identity on the same 100-project fixture before native 0.7.0 candidate qualification. Prior chain reference data remains historical 0.6.0 evidence, not a new 0.7.0 performance claim.'
    [IO.File]::WriteAllText((Join-Path $repository 'docs/performance/0.7-budget.json'),($budget|ConvertTo-Json -Depth 20)+"`n",[Text.UTF8Encoding]::new($false))
    [ordered]@{evidence=$run;inputSha256=$report.inputIdentity.sha256;analysisMilliseconds=$report.runMetadata.elapsedMilliseconds;thresholdsChanged=$false;hostStateEqual=((Get-ArchSiftHostHash)-ceq$before)}|ConvertTo-Json
}finally{if((Get-ArchSiftHostHash)-cne$before){throw 'Host drift; preserve evidence.'}}
