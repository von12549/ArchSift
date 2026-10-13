[CmdletBinding()]
param(
    [string]$Node = 'C:\Users\von12\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe',
    [string]$Modules = 'C:\Users\von12\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules',
    [string]$Dotnet = 'C:\Program Files\dotnet\dotnet.exe',
    [string]$LabRoot = 'D:\ArchSift-lab',
    [string]$HistoricalReports = 'D:\IFX-10-Root\validation\20261013-ifx-v3-7e9c2a1b\ui-reports',
    [switch]$BuildEvidence
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'HostState.ps1')
$taskRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskOutput = [IO.Path]::GetFullPath((Join-Path $LabRoot ('runs/workbench-0.8-' + [Guid]::NewGuid().ToString('N'))))
if ($taskOutput.StartsWith($taskRepository+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Browser evidence must be outside product source.' }
$taskBaseline = Get-ArchSiftHostHash
[void][IO.Directory]::CreateDirectory($taskOutput)
[IO.File]::WriteAllText((Join-Path $taskOutput 'host-baseline.json'),(@{sha256=$taskBaseline}|ConvertTo-Json))
foreach ($taskTool in @($Node,$Dotnet)) {
    if (-not [IO.Path]::IsPathFullyQualified($taskTool) -or -not [IO.File]::Exists($taskTool)) { throw 'Explicit tool path is unavailable.' }
}
try {
    # Refresh current product/UI assets only; full offline regression has its separate evidence.
    $taskStart = [Diagnostics.ProcessStartInfo]::new()
    $taskStart.FileName = $Dotnet; $taskStart.WorkingDirectory = $taskRepository
    $taskStart.UseShellExecute = $false; $taskStart.CreateNoWindow = $true
    $taskStart.RedirectStandardOutput = $true; $taskStart.RedirectStandardError = $true
    foreach ($taskArgument in @('build','ArchSift.slnx','--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-c','Release','--verbosity','minimal')) { $taskStart.ArgumentList.Add($taskArgument) }
    $taskStart.Environment['DOTNET_CLI_HOME'] = Join-Path $taskOutput 'build-cli-home'
    $taskStart.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] = '0'
    $taskStart.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $taskStart.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE'] = '1'
    $taskStart.Environment['MSBUILDDISABLENODEREUSE'] = '1'
    $taskBuild = [Diagnostics.Process]::Start($taskStart)
    try {
        $taskStdout = $taskBuild.StandardOutput.ReadToEndAsync(); $taskStderr = $taskBuild.StandardError.ReadToEndAsync()
        if (-not $taskBuild.WaitForExit(180000)) { $taskBuild.Kill($true); $taskBuild.WaitForExit(); throw 'Owned asset build timed out.' }
        [Threading.Tasks.Task]::WaitAll($taskStdout,$taskStderr)
        [IO.File]::WriteAllText((Join-Path $taskOutput 'asset-build.stdout.log'),$taskStdout.Result)
        [IO.File]::WriteAllText((Join-Path $taskOutput 'asset-build.stderr.log'),$taskStderr.Result)
        $taskBuildEqual = (Get-ArchSiftHostHash) -ceq $taskBaseline
        [IO.File]::WriteAllText((Join-Path $taskOutput 'asset-build.json'),(@{exitCode=$taskBuild.ExitCode;hostStateEqual=$taskBuildEqual}|ConvertTo-Json))
        if (-not $taskBuildEqual) { throw 'Host drift after asset build; stop.' }
        if ($taskBuild.ExitCode -ne 0) { throw 'Current asset build failed; preserve logs.' }
    }
    finally { $taskBuild.Dispose() }
    $taskArguments = @((Join-Path $PSScriptRoot 'Test-0.8Workbench.mjs'),$taskRepository,$taskOutput,$Modules,$Dotnet,'C:\Program Files\PowerShell\7\pwsh.exe',$HistoricalReports)
    if ($BuildEvidence) { $taskArguments += (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages') }
    & $Node @taskArguments
    $taskExit = $LASTEXITCODE
    if ($taskExit -ne 0) { throw '0.8 machine browser QA failed; preserve its evidence.' }
}
finally {
    $taskAfter = Get-ArchSiftHostHash
    [IO.File]::WriteAllText((Join-Path $taskOutput 'host-final.json'),(@{beforeSha256=$taskBaseline;afterSha256=$taskAfter;equal=($taskBaseline -ceq $taskAfter)}|ConvertTo-Json))
    Write-Output "browser-evidence=$taskOutput host-state-equal=$($taskBaseline -ceq $taskAfter)"
    if ($taskBaseline -cne $taskAfter) { throw 'Host drift; stop and preserve evidence. No repair.' }
}
