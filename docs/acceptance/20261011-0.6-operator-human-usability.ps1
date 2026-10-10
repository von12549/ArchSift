# Operator-only IFX block 04. The operator, not this script, judges UI usability.
[CmdletBinding()]
param(
    [string]$ReviewedFreezeRoot='D:\ArchSift-lab\evidence\ifx-0.6-policy-freeze-c69cc6d46f8b4675a6dfab5225ff956a',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$NewInstallRoot='D:\IFX-10-Root\ArchSift-0.6-review',
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
$NewInstallRoot=Safe-Path $NewInstallRoot;$LabRoot=Safe-Path $LabRoot;$ReviewedFreezeRoot=Safe-Path $ReviewedFreezeRoot
$sourceRoot=Safe-Path (Join-Path $PSScriptRoot '../..')
foreach($protected in @($sourceRoot,$TargetRoot,$ExistingInstallRoot)){
    if($NewInstallRoot-eq$protected-or$NewInstallRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'New installation overlaps protected source.'}
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence root overlaps protected source.'}
}
if($NewInstallRoot-eq$LabRoot-or$NewInstallRoot.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$LabRoot.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Installation and evidence roots overlap.'}
if(-not$ReviewedFreezeRoot.StartsWith((Join-Path $LabRoot 'evidence')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Frozen evidence is outside lab.'}
function Owned-Path([string]$Root,[string]$Relative){
    $path=Safe-Path (Join-Path $Root $Relative)
    if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escapes reviewed root.'}
    return $path
}
function Original-State {
    $selection=Get-Content -LiteralPath (Owned-Path $ExistingInstallRoot 'install.json') -Raw|ConvertFrom-Json
    if($selection.root-cne$ExistingInstallRoot-or$selection.selectedVersion-cne'0.5.0'){throw 'Original installation selection changed.'}
    $chosen=@($selection.versions|Where-Object version -eq '0.5.0')
    if($chosen.Count-ne1-or$chosen[0].manifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'){throw 'Original manifest identity changed.'}
    $records=[Collections.Generic.List[object]]::new()
    function Walk-Original([string]$Directory){
        foreach($path in [IO.Directory]::EnumerateFileSystemEntries($Directory)){
            [void](Safe-Path $path)
            if([IO.Directory]::Exists($path)){Walk-Original $path}else{
                $records.Add([pscustomobject][ordered]@{path=[IO.Path]::GetRelativePath($ExistingInstallRoot,$path);sha256=(Hash-File $path)})
                if($records.Count-gt10000){throw 'Original installation file-count limit.'}
            }
        }
    }
    Walk-Original $ExistingInstallRoot
    return @{selectedVersion=$selection.selectedVersion;configPath=$selection.configPath;filesSha256=(Hash-Json @($records|Sort-Object path));fileCount=$records.Count}
}
function Same-Target($A,$B){return $A.head-ceq$B.head-and$A.ordinaryStatusSha256-ceq$B.ordinaryStatusSha256-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
function Same-Original($A,$B){return $A.selectedVersion-ceq$B.selectedVersion-and$A.configPath-ceq$B.configPath-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
function Listener-Pids {
    $lines=@(& (Join-Path ([Environment]::SystemDirectory) 'netstat.exe') -ano -p tcp)
    if($LASTEXITCODE-ne0){throw 'TCP listener inventory failed.'}
    foreach($line in $lines){$parts=@($line.Trim() -split '\s+');if($parts.Count-eq5-and$parts[0]-ceq'TCP'-and$parts[3]-ceq'LISTENING'){$listenerId=0;if([int]::TryParse($parts[4],[ref]$listenerId)){Write-Output $listenerId}}}
}
function Answer-YesNo([string]$Question){
    do{$answer=(Read-Host "$Question [yes/no]").Trim().ToLowerInvariant()}while($answer-notin @('yes','no'))
    return $answer
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$originalBefore=$null;$configBefore=$null;$freeze=$null;$process=$null;$launcherPid=$null;$launcherStopped=$false;$listenersClosed=$false;$complete=$false;$answers=$null;$exitCode=$null
$run=Join-Path $LabRoot ('evidence/ifx-0.6-human-usability-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($run)
try{
    $freezeResult=Get-Content -LiteralPath (Owned-Path $ReviewedFreezeRoot 'result.json') -Raw|ConvertFrom-Json
    $freeze=Get-Content -LiteralPath (Owned-Path $ReviewedFreezeRoot 'frozen-input.json') -Raw|ConvertFrom-Json
    if($freezeResult.status-cne'policy-frozen-for-review'-or-not$freezeResult.hostStateEqual-or-not$freezeResult.targetStateEqual-or-not$freezeResult.originalInstallationStateEqual-or(Hash-File (Owned-Path $ReviewedFreezeRoot 'frozen-input.json'))-cne'26eca9a9f63d99b0da5badcb1ba3207220f4854b8333b64d2f6c7251a4f5cf08'-or$freeze.installRoot-cne$NewInstallRoot){throw 'Frozen policy evidence changed.'}
    $targetBefore=Target-State;$originalBefore=Original-State
    if(-not(Same-Target $targetBefore $freeze.target)-or-not(Same-Original $originalBefore $freeze.original)){throw 'Target or original installation differs from freeze.'}
    $installPath=Owned-Path $NewInstallRoot 'install.json';$manifestPath=Owned-Path $NewInstallRoot 'versions/0.6.0/package-manifest.json';$exePath=Owned-Path $NewInstallRoot 'versions/0.6.0/archsift.exe';$configPath=Owned-Path $NewInstallRoot 'config/default.json';$rulesRoot=Owned-Path $NewInstallRoot 'rules';$reportRoot=Owned-Path $NewInstallRoot 'reports'
    $install=Get-Content -LiteralPath $installPath -Raw|ConvertFrom-Json;$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
    $exeRecord=@($manifest.entries|Where-Object path -eq 'archsift.exe')
    if((Hash-File $installPath)-cne'0b8701997ba6f4686b90cc0fe935fc6e12b867840b0f342f13bff2ded7784fc4'-or$install.selectedVersion-cne'0.6.0'-or$install.configPath-cne$configPath-or(Hash-File $manifestPath)-cne'39501df8e2b6e875b9e9c377e7bdf75544281b4ab769a377912c248f01fa3f2f'-or$manifest.sourceCommit-cne'a05c44ea5a1e544f7d3e4ffb5d4a411291180a31'-or$exeRecord.Count-ne1-or(Hash-File $exePath)-cne$exeRecord[0].sha256){throw 'New installation selection or executable changed.'}
    $configBefore=Hash-File $configPath
    if($configBefore-cne$freeze.configSha256){throw 'Managed configuration changed.'}
    foreach($file in $freeze.policyFiles){if((Hash-File (Owned-Path $rulesRoot $file.path))-cne$file.sha256){throw 'Policy file changed.'}}
    foreach($owned in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){try{$image=$owned.Path;if(-not$image){throw 'Cannot inspect active ArchSift process.'};if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$image.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'An installation process is already active.'}}finally{$owned.Dispose()}}
    $beforeReports=@([IO.Directory]::EnumerateDirectories($reportRoot));foreach($path in $beforeReports){[void](Safe-Path $path)}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;managedConfigSha256=$configBefore;catalogSha256=(Hash-File (Owned-Path $NewInstallRoot 'config/profiles.json'));selectionSha256=(Hash-File (Owned-Path $NewInstallRoot 'config/selection.json'));policyFiles=$freeze.policyFiles;reportCount=$beforeReports.Count}|ConvertTo-Json -Depth 12))
    Write-Host 'ArchSift 0.6.0 human review: the launcher will open in the default browser. Complete the checklist in docs/acceptance/20261011-0.6-human-usability-checklist.md. Close the workbench and launcher safely in the UI; closing a tab alone is insufficient. Do not paste the private URL or token into chat.'
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$exePath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in @('launch','--root',$NewInstallRoot)){[void]$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'dotnet-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';[void][IO.Directory]::CreateDirectory($start.Environment['DOTNET_CLI_HOME'])
    $process=[Diagnostics.Process]::Start($start);$launcherPid=$process.Id
    $stdoutTask=$process.StandardOutput.ReadToEndAsync();$stderrTask=$process.StandardError.ReadToEndAsync()
    $process.WaitForExit();[Threading.Tasks.Task]::WaitAll($stdoutTask,$stderrTask);$exitCode=$process.ExitCode
    $safeOut=$stdoutTask.Result -replace 'ARCHSIFT_UI=[^\r\n]+','ARCHSIFT_UI=<redacted>' -replace '#session=[0-9a-fA-F]+','#session=<redacted>'
    $safeErr=$stderrTask.Result -replace 'ARCHSIFT_UI=[^\r\n]+','ARCHSIFT_UI=<redacted>' -replace '#session=[0-9a-fA-F]+','#session=<redacted>'
    [IO.File]::WriteAllText((Join-Path $run 'launcher.stdout-redacted.txt'),$safeOut);[IO.File]::WriteAllText((Join-Path $run 'launcher.stderr-redacted.txt'),$safeErr)
    $launcherStopped=$process.HasExited
    $childPids=@([regex]::Matches($safeOut,'ARCHSIFT_PID=(\d+)')|ForEach-Object {[int]$_.Groups[1].Value}|Select-Object -Unique)
    if($exitCode-ne0-or$childPids.Count-lt1){throw 'Launcher failed or no owned Web PID was reported.'}
    $remaining=@(Listener-Pids|Where-Object {$_ -in $childPids -or $_ -eq $launcherPid})
    $listenersClosed=$remaining.Count-eq0
    if(-not$listenersClosed-or@(Get-Process -Id $childPids -ErrorAction SilentlyContinue).Count-ne0){throw 'Owned Web PID or listener remained after UI close.'}
    $answers=[ordered]@{}
    $answers.versionAndHistory=Answer-YesNo 'Current 0.6.0 and older report versions were distinguishable?'
    $answers.languagesAndWidths=Answer-YesNo 'Chinese/English and desktop/480px were readable without horizontal overflow?'
    $answers.profileAndProtection=Answer-YesNo 'Managed default and same-name external profile stayed distinct with accurate protection labels?'
    $answers.progressAndPartial=Answer-YesNo 'Current job stages/counts and partial versus compliant meaning were clear?'
    $answers.cancelAndDirty=Answer-YesNo 'Cancel and unsaved-change/close confirmation behaved clearly and safely?'
    $answers.invalidExternal=Answer-YesNo 'A damaged external JSON showed its own error while the managed profile remained usable?'
    do{$confidenceText=(Read-Host 'Overall confidence [1-5]').Trim();$confidence=0;$validConfidence=[int]::TryParse($confidenceText,[ref]$confidence)-and$confidence-ge1-and$confidence-le5}while(-not$validConfidence)
    do{$severity=(Read-Host 'Highest issue severity [L0/L1/L2/L3/none]').Trim().ToUpperInvariant()}while($severity-notin @('L0','L1','L2','L3','NONE'))
    do{$notes=Read-Host 'Concise observations or issue IDs (do not include token or URL)'}while($notes-match '#session=|https?://')
    $answers.confidence=$confidence;$answers.highestSeverity=$severity;$answers.notes=$notes
    [IO.File]::WriteAllText((Join-Path $run 'human-answers.json'),(ConvertTo-Json -InputObject $answers -Depth 6))
    $afterReports=@([IO.Directory]::EnumerateDirectories($reportRoot));$newReports=@($afterReports|Where-Object {$_ -notin $beforeReports});foreach($path in $newReports){[void](Safe-Path $path)}
    [IO.File]::WriteAllText((Join-Path $run 'new-reports.json'),(ConvertTo-Json -InputObject $newReports))
    [IO.File]::WriteAllText((Join-Path $run 'after.json'),(@{launcherPid=$launcherPid;webPids=$childPids;catalogSha256=(Hash-File (Owned-Path $NewInstallRoot 'config/profiles.json'));selectionSha256=(Hash-File (Owned-Path $NewInstallRoot 'config/selection.json'));managedConfigSha256=(Hash-File $configPath);reportCount=$afterReports.Count;newReportCount=$newReports.Count}|ConvertTo-Json -Depth 6))
    $complete=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=($_.Exception.Message -replace '#session=[0-9a-fA-F]+','#session=<redacted>')}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$configEqual=$null;$policyEqual=$null;$auditErrors=@()
    if($null-ne$targetBefore){try{$targetEqual=Same-Target (Target-State) $targetBefore}catch{$targetEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=Same-Original (Original-State) $originalBefore}catch{$originalEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$configBefore){try{$configEqual=(Hash-File (Owned-Path $NewInstallRoot 'config/default.json'))-ceq$configBefore}catch{$configEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$freeze){try{$policyEqual=@($freeze.policyFiles|Where-Object {(Hash-File (Owned-Path (Owned-Path $NewInstallRoot 'rules') $_.path))-cne$_.sha256}).Count-eq0}catch{$policyEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$process){$launcherStopped=$process.HasExited;$process.Dispose()}
    $result=@{status=$(if($complete-and$hostEqual-and$targetEqual-and$originalEqual-and$configEqual-and$policyEqual-and$launcherStopped-and$listenersClosed){'human-usability-for-review'}else{'stop'});runRoot=$run;launcherExitCode=$exitCode;launcherStopped=$launcherStopped;ownedListenersClosed=$listenersClosed;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;hostStateEqual=$hostEqual;managedConfigStateEqual=$configEqual;policyStateEqual=$policyEqual;answersRecorded=$null-ne$answers}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false-or$configEqual-eq$false-or$policyEqual-eq$false){throw 'Safety audit failed; preserve evidence without repair.'}
}
