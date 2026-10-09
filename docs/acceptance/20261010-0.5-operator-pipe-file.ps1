# Operator-only step 03c: redirected UI pipe, plus direct file stdout/stderr without a session token file.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreviousCancelRun,
    [string]$InstallRoot='D:\IFX-10-Root\ArchSift-0.5-review',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory='D:\IFX-10-Root\ArchSift\versions\0.4.0'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$PreviousCancelRun=Safe-Path $PreviousCancelRun;$ApplyRun=Safe-Path ([IO.Path]::GetDirectoryName($PreviousCancelRun))
$InstallRoot=Safe-Path $InstallRoot;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
foreach($protected in @($InstallRoot,$TargetRoot,$ExistingInstallRoot,[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))){
    if($ApplyRun-eq$protected-or$ApplyRun.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($ApplyRun+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/protected roots overlap.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null;$configBefore=$null;$completed=$false
$redirect=[ordered]@{pipeReady=$false;pipeCliExitCode=$null;pipeWebPid=$null;pipeWebProcessExited=$false;pipeWebPidListening=$null;fileStdoutVersionPassed=$false;fileStderrInvalidConfigPassed=$false;fileSessionTokenAbsent=$false}
$run=Join-Path $ApplyRun ('terminal-redirect-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory((Safe-Path $run))
function Payload-State {
    $path=Join-Path $InstallRoot 'versions/0.5.0/package-manifest.json'
    if((Hash-File $path)-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'){throw 'Installed 0.5.0 manifest changed.'}
    $manifest=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    $versionRoot=Safe-Path (Join-Path $InstallRoot 'versions/0.5.0')
    foreach($entry in $manifest.entries){$file=Safe-Path (Join-Path $versionRoot $entry.path);if(-not$file.StartsWith($versionRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $file)-cne$entry.sha256){throw 'Installed payload changed.'}}
    return $true
}
try{
    if((Hash-File (Join-Path $PreviousCancelRun 'result.json'))-cne'1e6d8cbdb74f75ade0765681ef9a688933b33ceb1b35d1508e5226c9ea1ca7ba'){throw 'Reviewed Ctrl+C evidence changed.'}
    $previous=Get-Content -LiteralPath (Safe-Path (Join-Path $PreviousCancelRun 'result.json')) -Raw|ConvertFrom-Json
    $previousBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $PreviousCancelRun 'before.json')) -Raw|ConvertFrom-Json
    if($previous.runRoot-cne$PreviousCancelRun-or$previous.status-cne'native-console-ctrl-c-passed'-or$previous.cliExitCode-ne130-or-not$previous.webProcessExited-or$previous.webPidListening-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.existing040StateEqual-or-not$previous.newConfigEqual-or-not$previous.newPayloadEqual-or$previous.nextGate-cne'operator-native-cli-pipe-file'){throw 'Previous Ctrl+C gate is not ready.'}
    [void](Payload-State)
    $config=Safe-Path (Join-Path $InstallRoot 'config/project.json');$configBefore=Hash-File $config
    if($configBefore-cne$previousBefore.configSha256){throw 'Installed config changed since Ctrl+C.'}
    $targetBefore=Target-State;$existingBefore=Existing-State
    if((Hash-Json $targetBefore)-cne(Hash-Json $previousBefore.target)-or(Hash-Json $existingBefore)-cne(Hash-Json $previousBefore.existing)){throw 'Target/original install changed since Ctrl+C.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore;configSha256=$configBefore;previousResultSha256='1e6d8cbdb74f75ade0765681ef9a688933b33ceb1b35d1508e5226c9ea1ca7ba'}|ConvertTo-Json -Depth 9))
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        $image=$running.Path
        if(-not$image){throw ('Cannot inspect an existing ArchSift process; review PID '+$running.Id)}
        if($image.StartsWith($InstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('An owned session is already running; review PID '+$running.Id)}
    }
    $redirect=& (Join-Path $PSScriptRoot '0.5-ui-redirect-smoke.ps1') -Executable (Join-Path $InstallRoot 'versions/0.5.0/archsift.exe') -Config $config -RunRoot $run
    if(-not$redirect.pipeReady-or$redirect.pipeCliExitCode-ne0-or-not$redirect.pipeWebProcessExited-or$redirect.pipeWebPidListening-or-not$redirect.fileStdoutVersionPassed-or-not$redirect.fileStderrInvalidConfigPassed-or-not$redirect.fileSessionTokenAbsent){throw 'Redirected UI/file output gate did not pass.'}
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        $image=$running.Path
        if(-not$image){throw ('Cannot inspect a remaining ArchSift process; review PID '+$running.Id)}
        if($image.StartsWith($InstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('An owned process remains after redirected UI; review PID '+$running.Id)}
    }
    $library=Safe-Path (Join-Path $InstallRoot 'rules')
    if(Test-Path -LiteralPath $library){
        $files=@(Get-ChildItem -LiteralPath $library -Recurse -File)
        foreach($file in $files){[void](Safe-Path $file.FullName);$relative=[IO.Path]::GetRelativePath($library,$file.FullName);if($relative-notin@('.archsift-library.json','.archsift-library.lock')){throw 'Installation-only session created user rule/chain files.'}}
        $registry=Join-Path $library '.archsift-library.json';if(Test-Path -LiteralPath $registry){$data=Get-Content -LiteralPath $registry -Raw|ConvertFrom-Json;if($data.schemaVersion-ne1-or$data.entries.Count-ne0){throw 'Library is not empty installation metadata.'}}
    }
    if(Test-Path -LiteralPath (Join-Path $InstallRoot 'reports')){throw 'Redirected UI check produced a report directory.'}
    $completed=$true
}catch{
    [IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message}|ConvertTo-Json));throw
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$existingEqual=$null;$configEqual=$null;$payloadEqual=$false;$auditErrors=[Collections.Generic.List[string]]::new()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$existingBefore){try{$existingEqual=(Hash-Json (Existing-State))-ceq(Hash-Json $existingBefore)}catch{$existingEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$configBefore){try{$configEqual=(Hash-File (Join-Path $InstallRoot 'config/project.json'))-ceq$configBefore;$payloadEqual=Payload-State}catch{$auditErrors.Add($_.Exception.Message)}}
    $result=[ordered]@{status=$(if($completed-and$hostEqual-and$targetEqual-and$existingEqual-and$configEqual-and$payloadEqual){'native-cli-pipe-file-passed'}else{'stop'});runRoot=$run;pipeReady=$redirect.pipeReady;pipeCliExitCode=$redirect.pipeCliExitCode;pipeWebPid=$redirect.pipeWebPid;pipeWebProcessExited=$redirect.pipeWebProcessExited;pipeWebPidListening=$redirect.pipeWebPidListening;fileStdoutVersionPassed=$redirect.fileStdoutVersionPassed;fileStderrInvalidConfigPassed=$redirect.fileStderrInvalidConfigPassed;fileSessionTokenAbsent=$redirect.fileSessionTokenAbsent;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;newConfigEqual=$configEqual;newPayloadEqual=$payloadEqual;analysisOrBuildPerformed=$false;nextGate='operator-external-copy-upgrade-rollback'}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors.ToArray()));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or($null-ne$targetEqual-and-not$targetEqual)-or($null-ne$existingEqual-and-not$existingEqual)-or($null-ne$configEqual-and-not$configEqual)){throw 'Safety drift/audit failure; preserve evidence and stop. No repair.'}
}
