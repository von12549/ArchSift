# Operator-only step 03a: visible standard CLI and browser Safe shutdown, no output capture/token log.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ApplyRun,
    [string]$InstallRoot='D:\IFX-10-Root\ArchSift-0.5-review',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory='D:\IFX-10-Root\ArchSift\versions\0.4.0'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$ApplyRun=Safe-Path $ApplyRun;$InstallRoot=Safe-Path $InstallRoot;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
foreach($protected in @($InstallRoot,$TargetRoot,$ExistingInstallRoot,[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))){
    if($ApplyRun-eq$protected-or$ApplyRun.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($ApplyRun+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Terminal evidence/protected roots overlap.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null;$configBefore=$null
$run=Join-Path $ApplyRun ('terminal-safe-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory((Safe-Path $run))
$process=$null;$cliExit=$null;$webId=$null;$webExited=$false;$startupConfirmed=$false;$configConfirmed=$false;$completed=$false
function Payload-State {
    $path=Join-Path $InstallRoot 'versions/0.5.0/package-manifest.json'
    if((Hash-File $path)-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'){throw 'Installed 0.5.0 manifest changed.'}
    $manifest=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    $versionRoot=Safe-Path (Join-Path $InstallRoot 'versions/0.5.0')
    foreach($entry in $manifest.entries){$file=Safe-Path (Join-Path $versionRoot $entry.path);if(-not$file.StartsWith($versionRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $file)-cne$entry.sha256){throw 'Installed payload changed.'}}
    return $true
}
try{
    $previous=Get-Content -LiteralPath (Safe-Path (Join-Path $ApplyRun 'result.json')) -Raw|ConvertFrom-Json
    $previousBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $ApplyRun 'before.json')) -Raw|ConvertFrom-Json
    if($previous.status-cne'installed-private-smoke-passed'-or-not$previous.applied-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.existing040StateEqual){throw 'Apply gate is not ready.'}
    [void](Payload-State)
    $config=Safe-Path (Join-Path $InstallRoot 'config/project.json');$configBefore=Hash-File $config
    $receipt=Get-Content -LiteralPath (Safe-Path (Join-Path $ApplyRun 'receipt.json')) -Raw|ConvertFrom-Json
    if($receipt.after.root-cne$InstallRoot-or$receipt.id-cne$previous.receiptId-or$receipt.afterState.files.Count-ne1-or$configBefore-cne$receipt.afterState.files[0].sha256){throw 'Installed config/receipt changed.'}
    $targetBefore=Target-State;$existingBefore=Existing-State
    if((Hash-Json $targetBefore)-cne(Hash-Json $previousBefore.target)-or(Hash-Json $existingBefore)-cne(Hash-Json $previousBefore.existing)){throw 'Target/original install changed since apply; review required.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore;configSha256=$configBefore}|ConvertTo-Json -Depth 9))
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        $image=$running.Path
        if(-not$image){throw ('Cannot inspect an existing ArchSift process; resolve before starting another session. PID '+$running.Id)}
        if($image.StartsWith($InstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('An owned session is already running; resolve it first. PID '+$running.Id)}
    }
    Write-Host 'Observe ARCHSIFT_UI / ARCHSIFT_PID / ARCHSIFT_STATE=waiting in this terminal.'
    Write-Host 'Open the complete URL locally. Confirm IFX-New / IFX.sln / net10.0 / Debug / existing / new reports and rules.'
    Write-Host 'Use Safe shutdown, then wait for terminal return. Keep this step installation-only; do not run analysis/build/import/save.'
    Write-Host 'Do not record/share the URL or session token. Remember the numeric Web PID for the next prompt.'
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=Safe-Path (Join-Path $InstallRoot 'versions/0.5.0/archsift.exe');$start.UseShellExecute=$false;$start.CreateNoWindow=$false
    foreach($arg in @('ui','--config',$config)){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start)
    [IO.File]::WriteAllText((Join-Path $run 'session-owner.json'),(@{cliPid=$process.Id;executable=$start.FileName;configPath=$config}|ConvertTo-Json))
    if(-not$process.WaitForExit(600000)){throw 'Terminal test timed out. Preserve session-owner.json; identify exact owned processes before further action.'}
    $cliExit=$process.ExitCode
    if($cliExit-ne0){throw ('Safe shutdown expected CLI exit 0; actual '+$cliExit)}
    $startupConfirmed=(Read-Host 'Were URL, PID and waiting lines all visible? Enter yes/no')-ceq'yes'
    $configConfirmed=(Read-Host 'Did the UI show the expected target/config and did Safe shutdown return this terminal? Enter yes/no')-ceq'yes'
    $pidText=Read-Host 'Enter only the numeric ARCHSIFT_PID from this session (no URL/token)'
    if($pidText-notmatch'^[1-9][0-9]{0,9}$'){throw 'Invalid session Web PID.'};$webId=[int]$pidText
    try{$web=[Diagnostics.Process]::GetProcessById($webId);try{$webExited=$web.HasExited}finally{$web.Dispose()}}catch [ArgumentException]{$webExited=$true}
    if(-not$startupConfirmed-or-not$configConfirmed-or-not$webExited){throw 'Operator terminal/config/shutdown check did not pass; preserve evidence.'}
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
    $result=[ordered]@{status=$(if($completed-and$hostEqual-and$targetEqual-and$existingEqual-and$configEqual-and$payloadEqual){'native-console-safe-shutdown-passed'}else{'stop'});runRoot=$run;cliExitCode=$cliExit;webPid=$webId;webProcessExited=$webExited;startupLinesConfirmed=$startupConfirmed;operatorConfigurationConfirmed=$configConfirmed;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;newConfigEqual=$configEqual;newPayloadEqual=$payloadEqual;analysisOrBuildPerformed=$false;nextGate='operator-native-cli-ctrl-c'}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors.ToArray()));$result|ConvertTo-Json -Depth 6
    if($process){$process.Dispose()}
    if(-not$hostEqual-or($null-ne$targetEqual-and-not$targetEqual)-or($null-ne$existingEqual-and-not$existingEqual)-or($null-ne$configEqual-and-not$configEqual)){throw 'Safety drift/audit failure; preserve evidence and stop. No repair.'}
}
