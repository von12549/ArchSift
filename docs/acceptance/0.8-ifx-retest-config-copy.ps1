[CmdletBinding()]
param([Parameter(Mandatory)][string]$JobDirectory,[Parameter(Mandatory)][string]$OriginalConfig,
    [Parameter(Mandatory)][string]$PreflightResult,[string]$LabRoot='D:\ArchSift-lab')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
$baseline=Get-ArchSiftHostHash
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
try {
    $job=[IO.Path]::GetFullPath($JobDirectory)
    $source=[IO.Path]::GetFullPath($OriginalConfig)
    if(-not$job.StartsWith('D:\ArchSift-lab\evidence\ifx-0.8\',[StringComparison]::OrdinalIgnoreCase)-or
        -not$source.StartsWith($job+'\',[StringComparison]::OrdinalIgnoreCase)-or
        [IO.Path]::GetFileName($source)-cne'config-bound.json') {throw 'Expected the sealed IFX job and its existing configuration copy.'}
    $preflight=Get-Content -LiteralPath $PreflightResult -Raw|ConvertFrom-Json
    if($preflight.errors.Count-ne0-or-not$preflight.hostStateEqual){throw 'Revised candidate preflight did not pass.'}
    $before=Hash $source
    $config=Get-Content -LiteralPath $source -Raw|ConvertFrom-Json
    if($config.build.mode-cne'existing'-or$config.build.assemblyManifest-cne(Join-Path $job 'assembly-manifest-v3.json')-or
        -not$config.rulesDirectory.StartsWith($job+'\',[StringComparison]::OrdinalIgnoreCase)-or
        -not$config.output.directory.StartsWith($job+'\',[StringComparison]::OrdinalIgnoreCase)-or
        -not(Test-Path -LiteralPath $config.output.directory)) {throw 'Original bound configuration/report location differs.'}
    $id=[Guid]::NewGuid().ToString('N')
    $lab=[IO.Path]::GetFullPath($LabRoot)
    $run=Join-Path $lab ('evidence/ifx-0.8-retest-'+$id)
    $reports=Join-Path $lab ('runs/ifx-0.8-bound-retest-'+$id+'/reports')
    $copy=Join-Path $run 'config-bound-retest.json'
    if(-not$run.StartsWith('D:\ArchSift-lab\evidence\ifx-0.8-retest-',[StringComparison]::OrdinalIgnoreCase)-or
        -not$reports.StartsWith('D:\ArchSift-lab\runs\ifx-0.8-bound-retest-',[StringComparison]::OrdinalIgnoreCase)-or
        (Test-Path -LiteralPath $run)-or(Test-Path -LiteralPath (Split-Path $reports -Parent))){throw 'Retest copy and reports must use new external roots.'}
    [void][IO.Directory]::CreateDirectory($run)
    $config.output.directory=$reports
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes(($config|ConvertTo-Json -Depth 30))
    $stream=[IO.FileStream]::new($copy,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write($bytes);$stream.Flush($true)}finally{$stream.Dispose()}
    if((Hash $source)-cne$before){throw 'Original configuration copy changed; preserve the new copy and stop.'}
    $record=[ordered]@{kind='ifx-0.8-retest-config-copy';jobDirectory=$job;preflightResult=([IO.Path]::GetFullPath($PreflightResult));preflightSha256=(Hash $PreflightResult);originalConfig=$source;originalSha256=$before;newConfig=$copy;newSha256=(Hash $copy);newReports=$reports;manifest=$config.build.assemblyManifest;hostStateEqual=((Get-ArchSiftHostHash)-ceq$baseline)}
    if(-not$record.hostStateEqual){throw 'Host drift; preserve evidence and stop.'}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($record|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
    $record|ConvertTo-Json -Depth 6
}finally{$equal=(Get-ArchSiftHostHash)-ceq$baseline;Write-Output ('ifx-0.8-retest-copy-final-host-state-equal='+$equal);if(-not$equal){throw 'Host drift; preserve evidence without repair.'}}
