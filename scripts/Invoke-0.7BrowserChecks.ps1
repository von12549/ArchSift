[CmdletBinding()]
param([string]$Modules='C:\Users\von12\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules',[string]$LabRoot='D:\ArchSift-lab',[string]$BrowserChannel='msedge')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('runs/browser-0.7-'+[Guid]::NewGuid().ToString('N'))
if($run.StartsWith($repository+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Browser evidence must be external.'}
[void][IO.Directory]::CreateDirectory($run)
$dotnet=(Get-Command dotnet).Source
try{
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$dotnet;$start.WorkingDirectory=$repository;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in @('build','src/ArchSift.Web/ArchSift.Web.csproj','--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-c','Debug')){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'build-cli-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$start.Environment['MSBUILDDISABLENODEREUSE']='1'
    $process=[Diagnostics.Process]::Start($start)
    try{$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();if(-not $process.WaitForExit(180000)){$process.Kill($true);throw 'Web build timeout.'};[Threading.Tasks.Task]::WaitAll($stdout,$stderr);[IO.File]::WriteAllText((Join-Path $run 'build.stdout.log'),$stdout.Result);[IO.File]::WriteAllText((Join-Path $run 'build.stderr.log'),$stderr.Result);if($process.ExitCode-ne0){throw 'Web build failed; inspect retained logs.'}}finally{$process.Dispose()}
    if((Get-ArchSiftHostHash)-cne$before){throw 'Host drift after build; no repair.'}
    & node (Join-Path $PSScriptRoot 'Test-WorkbenchLifecycle.mjs')
    if($LASTEXITCODE-ne0){throw 'Workbench script regression failed.'}
    & node (Join-Path $PSScriptRoot 'Test-LauncherClose.mjs')
    if($LASTEXITCODE-ne0){throw 'Launcher script regression failed.'}
    & node (Join-Path $PSScriptRoot 'Test-0.7Browser.mjs') $repository $run $Modules $dotnet $BrowserChannel
    if($LASTEXITCODE-ne0){throw 'Browser QA failed; preserve evidence.'}
}finally{
    $after=Get-ArchSiftHostHash;$equal=$before-ceq$after
    [IO.File]::WriteAllText((Join-Path $run 'host-state.json'),(@{beforeSha256=$before;afterSha256=$after;equal=$equal}|ConvertTo-Json))
    Write-Output ('browser-evidence='+$run+' host-state-equal='+$equal)
    if(-not$equal){throw 'Host drift; no repair.'}
}
