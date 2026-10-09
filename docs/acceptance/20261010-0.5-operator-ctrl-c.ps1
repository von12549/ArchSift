# Operator-only step 03b: the native CLI receives Ctrl+C in its own visible console.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreviousSafeRun,
    [string]$InstallRoot='D:\IFX-10-Root\ArchSift-0.5-review',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory='D:\IFX-10-Root\ArchSift\versions\0.4.0'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$PreviousSafeRun=Safe-Path $PreviousSafeRun;$ApplyRun=Safe-Path ([IO.Path]::GetDirectoryName($PreviousSafeRun))
$InstallRoot=Safe-Path $InstallRoot;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
foreach($protected in @($InstallRoot,$TargetRoot,$ExistingInstallRoot,[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))){
    if($ApplyRun-eq$protected-or$ApplyRun.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($ApplyRun+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Terminal evidence/protected roots overlap.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null;$configBefore=$null
$run=Join-Path $ApplyRun ('terminal-cancel-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory((Safe-Path $run))
$process=$null;$cliExit=$null;$webId=$null;$webExited=$false;$webListening=$null;$startupConfirmed=$false;$ctrlCConfirmed=$false;$completed=$false
function Payload-State {
    $path=Join-Path $InstallRoot 'versions/0.5.0/package-manifest.json'
    if((Hash-File $path)-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'){throw 'Installed 0.5.0 manifest changed.'}
    $manifest=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    $versionRoot=Safe-Path (Join-Path $InstallRoot 'versions/0.5.0')
    foreach($entry in $manifest.entries){$file=Safe-Path (Join-Path $versionRoot $entry.path);if(-not$file.StartsWith($versionRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $file)-cne$entry.sha256){throw 'Installed payload changed.'}}
    return $true
}
try{
    if((Hash-File (Join-Path $PreviousSafeRun 'result.json'))-cne'8ff273619d2bb7f2dc60ce8196174c84911394ccc62b7329e8cb0c09c4aa0ac1'){throw 'Reviewed Safe shutdown evidence changed.'}
    $previous=Get-Content -LiteralPath (Safe-Path (Join-Path $PreviousSafeRun 'result.json')) -Raw|ConvertFrom-Json
    $previousBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $PreviousSafeRun 'before.json')) -Raw|ConvertFrom-Json
    if($previous.status-cne'native-console-safe-shutdown-passed'-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.existing040StateEqual-or-not$previous.newConfigEqual-or-not$previous.newPayloadEqual-or$previous.nextGate-cne'operator-native-cli-ctrl-c'){throw 'Previous terminal gate is not ready.'}
    if($previous.runRoot-cne$PreviousSafeRun-or$previousBefore.configSha256-cne'a88140c84d003303d66e904a62eaf8b08e9012d0f40f2f8ad236bd8d0d271f2f'){throw 'Reviewed terminal identity differs.'}
    [void](Payload-State)
    $config=Safe-Path (Join-Path $InstallRoot 'config/project.json');$configBefore=Hash-File $config
    if($configBefore-cne$previousBefore.configSha256){throw 'Installed config changed since Safe shutdown.'}
    $targetBefore=Target-State;$existingBefore=Existing-State
    if((Hash-Json $targetBefore)-cne(Hash-Json $previousBefore.target)-or(Hash-Json $existingBefore)-cne(Hash-Json $previousBefore.existing)){throw 'Target/original install changed since Safe shutdown.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore;configSha256=$configBefore;previousResultSha256='8ff273619d2bb7f2dc60ce8196174c84911394ccc62b7329e8cb0c09c4aa0ac1'}|ConvertTo-Json -Depth 9))
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        $image=$running.Path
        if(-not$image){throw ('Cannot inspect an existing ArchSift process; resolve before starting another session. PID '+$running.Id)}
        if($image.StartsWith($InstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('An owned session is already running; resolve it first. PID '+$running.Id)}
    }
    Write-Host 'A separate visible ArchSift CLI console will open. Observe ARCHSIFT_UI / ARCHSIFT_PID / ARCHSIFT_STATE=waiting.'
    Write-Host 'Record only the numeric Web PID, then press Ctrl+C once in that CLI console. Do not use browser Safe shutdown in this step.'
    Write-Host 'The console should close and this evidence terminal should continue. Do not analyze/build/import/save or share URL/token.'
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=Safe-Path (Join-Path $InstallRoot 'versions/0.5.0/archsift.exe')
    $start.UseShellExecute=$true;$start.CreateNoWindow=$false;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Normal
    foreach($arg in @('ui','--config',$config)){$start.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::Start($start)
    [IO.File]::WriteAllText((Join-Path $run 'session-owner.json'),(@{cliPid=$process.Id;executable=$start.FileName;configPath=$config}|ConvertTo-Json))
    if(-not$process.WaitForExit(600000)){throw 'Ctrl+C test timed out. Preserve session-owner.json; identify exact owned processes before further action.'}
    $cliExit=$process.ExitCode
    if($cliExit-ne130){throw ('Ctrl+C expected CLI exit 130; actual '+$cliExit+'. Preserve evidence for review.')}
    $startupConfirmed=(Read-Host 'Were URL, PID and waiting lines all visible in the CLI console? Enter yes/no')-ceq'yes'
    $ctrlCConfirmed=(Read-Host 'Did you press Ctrl+C once in that CLI console and see it close? Enter yes/no')-ceq'yes'
    $pidText=Read-Host 'Enter only the numeric ARCHSIFT_PID from this session (no URL/token)'
    if($pidText-notmatch'^[1-9][0-9]{0,9}$'){throw 'Invalid session Web PID.'};$webId=[int]$pidText
    try{$web=[Diagnostics.Process]::GetProcessById($webId);try{$webExited=$web.HasExited}finally{$web.Dispose()}}catch [ArgumentException]{$webExited=$true}
    $webListening=@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object OwningProcess -eq $webId).Count -gt 0
    if(-not$startupConfirmed-or-not$ctrlCConfirmed-or-not$webExited-or$webListening){throw 'Operator Ctrl+C/Web/listener check did not pass; preserve evidence.'}
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        $image=$running.Path
        if(-not$image){throw ('Cannot inspect a remaining ArchSift process; review PID '+$running.Id)}
        if($image.StartsWith($InstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('An owned process remains after Ctrl+C; review PID '+$running.Id)}
    }
    $library=Safe-Path (Join-Path $InstallRoot 'rules')
    if(Test-Path -LiteralPath $library){
        $files=@(Get-ChildItem -LiteralPath $library -Recurse -File)
        foreach($file in $files){[void](Safe-Path $file.FullName);$relative=[IO.Path]::GetRelativePath($library,$file.FullName);if($relative-notin@('.archsift-library.json','.archsift-library.lock')){throw 'Installation-only session created user rule/chain files.'}}
        $registry=Join-Path $library '.archsift-library.json';if(Test-Path -LiteralPath $registry){$data=Get-Content -LiteralPath $registry -Raw|ConvertFrom-Json;if($data.schemaVersion-ne1-or$data.entries.Count-ne0){throw 'Library is not empty installation metadata.'}}
    }
    if(Test-Path -LiteralPath (Join-Path $InstallRoot 'reports')){throw 'Installation-only terminal check produced a report directory.'}
    $completed=$true
}catch{
    [IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message}|ConvertTo-Json));throw
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$existingEqual=$null;$configEqual=$null;$payloadEqual=$false;$auditErrors=[Collections.Generic.List[string]]::new()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$existingBefore){try{$existingEqual=(Hash-Json (Existing-State))-ceq(Hash-Json $existingBefore)}catch{$existingEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$configBefore){try{$configEqual=(Hash-File (Join-Path $InstallRoot 'config/project.json'))-ceq$configBefore;$payloadEqual=Payload-State}catch{$auditErrors.Add($_.Exception.Message)}}
    $result=[ordered]@{status=$(if($completed-and$hostEqual-and$targetEqual-and$existingEqual-and$configEqual-and$payloadEqual){'native-console-ctrl-c-passed'}else{'stop'});runRoot=$run;cliExitCode=$cliExit;webPid=$webId;webProcessExited=$webExited;webPidListening=$webListening;startupLinesConfirmed=$startupConfirmed;operatorCtrlCConfirmed=$ctrlCConfirmed;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;newConfigEqual=$configEqual;newPayloadEqual=$payloadEqual;analysisOrBuildPerformed=$false;nextGate='operator-native-cli-pipe-file'}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors.ToArray()));$result|ConvertTo-Json -Depth 6
    if($process){$process.Dispose()}
    if(-not$hostEqual-or($null-ne$targetEqual-and-not$targetEqual)-or($null-ne$existingEqual-and-not$existingEqual)-or($null-ne$configEqual-and-not$configEqual)){throw 'Safety drift/audit failure; preserve evidence and stop. No repair.'}
}
