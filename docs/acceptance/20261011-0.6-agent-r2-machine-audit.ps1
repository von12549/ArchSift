# Agent-authorized read-only machine audit. Repeat after the independent human UI gate.
[CmdletBinding()]
param(
    [string]$FreezeRoot='D:\ArchSift-lab\evidence\ifx-0.6-r2-policy-freeze-6432925c039843caa1b881c5372427eb',
    [string]$CliRoot='D:\ArchSift-lab\evidence\ifx-0.6-r2-cli-measure-0faaef411d6243109618c8f9a2b1909b',
    [string]$UiRoot='D:\ArchSift-lab\evidence\ifx-0.6-r2-ui-measure-28288d02baa848fc868abec6bb607f4e',
    [string]$RollbackRoot='D:\ArchSift-lab\evidence\ifx-0.6-r2-lifecycle-91c27481fd5443f09eefb0a541c37e00\adopt-f41a328d552147f381c0ff96112f21e8\upgrade-77fc080fb0734d9288961c37f3ecc1f8\rollback-5b7aed3be8e54c83917b59a464e67b07',
    [string]$NewInstallRoot='D:\IFX-10-Root\ArchSift-0.6-review-r2',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$AssetRoot='D:\ArchSift-lab\packages\draft-0.6-replacement-independent-20261011',
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$FreezeRoot=Safe-Path $FreezeRoot;$CliRoot=Safe-Path $CliRoot;$UiRoot=Safe-Path $UiRoot;$RollbackRoot=Safe-Path $RollbackRoot;$NewInstallRoot=Safe-Path $NewInstallRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$TargetRoot=Safe-Path $TargetRoot;$AssetRoot=Safe-Path $AssetRoot;$LabRoot=Safe-Path $LabRoot
$repoRoot=Safe-Path (Join-Path $PSScriptRoot '../..')
$copyRoot=Safe-Path (Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetDirectoryName([IO.Path]::GetDirectoryName($RollbackRoot)))) 'installation')
foreach($protected in @($repoRoot,$TargetRoot,$ExistingInstallRoot,$NewInstallRoot)){
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence root overlaps protected state.'}
}
function Original-State {
    $selection=Get-Content -LiteralPath (Safe-Path (Join-Path $ExistingInstallRoot 'install.json')) -Raw|ConvertFrom-Json
    if($selection.root-cne$ExistingInstallRoot-or$selection.selectedVersion-cne'0.5.0'){throw 'Original selection changed.'}
    $records=[Collections.Generic.List[object]]::new()
    function Walk([string]$Directory){foreach($path in [IO.Directory]::EnumerateFileSystemEntries($Directory)){[void](Safe-Path $path);if([IO.Directory]::Exists($path)){Walk $path}else{$records.Add([pscustomobject][ordered]@{path=[IO.Path]::GetRelativePath($ExistingInstallRoot,$path);sha256=(Hash-File $path)});if($records.Count-gt10000){throw 'Original file-count limit.'}}}}
    Walk $ExistingInstallRoot
    return @{selectedVersion=$selection.selectedVersion;configPath=$selection.configPath;filesSha256=(Hash-Json @($records|Sort-Object path));fileCount=$records.Count}
}
function Same-Target($A,$B){return $A.head-ceq$B.head-and$A.ordinaryStatusSha256-ceq$B.ordinaryStatusSha256-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
function Same-Original($A,$B){return $A.selectedVersion-ceq$B.selectedVersion-and$A.configPath-ceq$B.configPath-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
function Tree-Hash([string]$Root){$Root=Safe-Path $Root;$records=[Collections.Generic.List[object]]::new();foreach($item in @(Get-ChildItem -LiteralPath $Root -Recurse -Force)){$path=Safe-Path $item.FullName;if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'State tree escapes root.'};if($item.PSIsContainer){continue};if(-not($item -is [IO.FileInfo])){throw 'Unsupported state item.'};$records.Add([ordered]@{path=[IO.Path]::GetRelativePath($Root,$path);length=$item.Length;sha256=(Hash-File $path)});if($records.Count-gt10000){throw 'State file-count limit.'}};return Hash-Json @($records|Sort-Object path)}
function Check-Payload([string]$Root,[string]$ManifestHash,[string]$Version,[string]$Commit){
    $Root=Safe-Path $Root;$manifestPath=Safe-Path (Join-Path $Root 'package-manifest.json')
    if((Hash-File $manifestPath)-cne$ManifestHash){throw 'Payload manifest differs.'}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
    if($manifest.version-cne$Version-or$manifest.sourceCommit-cne$Commit){throw 'Payload identity differs.'}
    foreach($entry in $manifest.entries){$path=Safe-Path (Join-Path $Root $entry.path);if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $path)-cne$entry.sha256-or([IO.FileInfo]$path).Length-ne$entry.bytes){throw 'Payload file differs.'}}
    if(@(Get-ChildItem -LiteralPath $Root -Recurse -File -Force).Count-ne($manifest.entries.Count+1)){throw 'Payload contains unexpected files.'}
    return @{version=$Version;manifestSha256=$ManifestHash;fileCount=$manifest.entries.Count}
}
function Product-Git-Read([string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command git).Source;$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in (@('--no-optional-locks','--no-replace-objects','-c','core.fsmonitor=false','-c','core.untrackedCache=false','-C',$repoRoot)+$Arguments)){[void]$start.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::Start($start)
    try{$out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync();if(-not$process.WaitForExit(30000)){$process.Kill($true);$process.WaitForExit();throw 'Git metadata timeout.'};[Threading.Tasks.Task]::WaitAll($out,$err);if($process.ExitCode-ne0){throw 'Git metadata failed.'};return $out.Result}finally{$process.Dispose()}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$originalBefore=$null;$completed=$false;$checks=[ordered]@{}
$run=Safe-Path (Join-Path $LabRoot ('evidence/ifx-0.6-r2-machine-audit-'+[Guid]::NewGuid().ToString('N')));[void][IO.Directory]::CreateDirectory($run)
try{
    if((Hash-File (Join-Path $FreezeRoot 'frozen-input.json'))-cne'0ff6038c85d162506853d441b025e6cb4cb9a5b91e445123fe04ac46c407d1ab'-or(Hash-File (Join-Path $CliRoot 'measurements.json'))-cne'b45bd183737122c3b75cd81a9b44ebed4aec421cde0beb04fb1f404cedaeab92'-or(Hash-File (Join-Path $UiRoot 'measurements.json'))-cne'888dedaa3d9ec8ee89edef2dcf39a43ee23b82a6a76a52b3bbde901e1508f890'-or(Hash-File (Join-Path $RollbackRoot 'result.json'))-cne'2de9a4e6713faf2384b8080e07b8b2bb3db44e9ce39c3742bb65555371e8f268'){throw 'Reviewed evidence changed.'}
    $freeze=Get-Content -LiteralPath (Join-Path $FreezeRoot 'frozen-input.json') -Raw|ConvertFrom-Json
    $cli=Get-Content -LiteralPath (Join-Path $CliRoot 'result.json') -Raw|ConvertFrom-Json
    $ui=Get-Content -LiteralPath (Join-Path $UiRoot 'result.json') -Raw|ConvertFrom-Json
    $rollback=Get-Content -LiteralPath (Join-Path $RollbackRoot 'result.json') -Raw|ConvertFrom-Json
    if($cli.status-cne'cli-measurements-for-review'-or$ui.status-cne'ui-measurements-for-review'-or$cli.completedCount-ne18-or$ui.completedCount-ne18-or$cli.semanticSha256-cne$ui.semanticSha256-or$rollback.status-cne'external-copy-rollback-passed'-or-not$rollback.laterManagedEditRejected-or-not$rollback.backupEqual){throw 'Prerequisite gates are incomplete.'}
    $targetBefore=Target-State;$originalBefore=Original-State
    if(-not(Same-Target $targetBefore $freeze.target)-or-not(Same-Original $originalBefore $freeze.original)){throw 'Target or original installation differs from frozen input.'}
    if((Product-Git-Read @('status','--porcelain=v1','--untracked-files=normal')).Trim()){throw 'ArchSift checkout is not clean.'}
    [void](Product-Git-Read @('merge-base','--is-ancestor','de222c2462e182191b2f71499421bfe086ea7bab','HEAD'))
    $changes=@((Product-Git-Read @('diff','--name-only','de222c2462e182191b2f71499421bfe086ea7bab','HEAD') -split '[\r\n]+')|Where-Object {$_})
    foreach($path in $changes){if($path-cne'AGENTS.md'-and$path-notmatch'^docs/(acceptance|plans)/'){throw 'Product sources changed after frozen candidate.'}}
    $checks.source=@{head=(Product-Git-Read @('rev-parse','HEAD')).Trim();productCommit='de222c2462e182191b2f71499421bfe086ea7bab';documentationOnlyChanges=$changes.Count;clean=$true}
    if((Hash-File (Join-Path $AssetRoot 'archsift-0.6.0-win-x64.zip'))-cne'64af0fb70ea8548b99373151c7005c15b0365784d8faedd3890989d3d040a560'-or(Hash-File (Join-Path $AssetRoot 'ArchSift.Setup.exe'))-cne'6327fcf10700aeec00f0b84da4201d4c3ad8b10d51d3f894766db4a2c55debab'){throw 'Independent candidate assets differ.'}
    $checks.assets=$true
    $install=Get-Content -LiteralPath (Safe-Path (Join-Path $NewInstallRoot 'install.json')) -Raw|ConvertFrom-Json
    if($install.root-cne$NewInstallRoot-or$install.selectedVersion-cne'0.6.0'-or$install.configPath-cne(Join-Path $NewInstallRoot 'config/default.json')-or$install.libraryPath-cne(Join-Path $NewInstallRoot 'rules')){throw 'Replacement installation selection differs.'}
    $checks.newPayload=Check-Payload (Join-Path $NewInstallRoot 'versions/0.6.0') 'a70f55859cf276ea786df2e68fcae4599cacc7c62cca9fcde20d27d6655f5e2b' '0.6.0' 'de222c2462e182191b2f71499421bfe086ea7bab'
    if((Hash-File (Join-Path $NewInstallRoot 'config/default.json'))-cne'7ceb551d59b284728cc7a5b6f840196e3e09d8c9b27bc16d024efd52a1faaeec'){throw 'Replacement managed config changed.'}
    foreach($file in $freeze.policyFiles){$path=Safe-Path (Join-Path $NewInstallRoot ('rules/'+$file.path));if(-not$path.StartsWith((Join-Path $NewInstallRoot 'rules')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $path)-cne$file.sha256){throw 'Frozen replacement policy changed.'}}
    $catalog=Get-Content -LiteralPath (Safe-Path (Join-Path $NewInstallRoot 'config/profiles.json')) -Raw|ConvertFrom-Json
    $selection=Get-Content -LiteralPath (Safe-Path (Join-Path $NewInstallRoot 'config/selection.json')) -Raw|ConvertFrom-Json
    if($catalog.schemaVersion-ne1-or$selection.schemaVersion-ne1-or$catalog.profiles.Count-lt1-or$catalog.profiles.Count-gt100-or@($catalog.profiles|Where-Object configPath -eq $install.configPath).Count-ne1-or@($catalog.profiles|Where-Object profileId -eq $catalog.defaultProfileId).Count-ne1-or@($catalog.profiles|Where-Object profileId -eq $selection.selectedProfileId).Count-ne1){throw 'Replacement launcher metadata is inconsistent.'}
    foreach($profile in $catalog.profiles){$path=Safe-Path $profile.configPath;if(-not$path.StartsWith((Join-Path $NewInstallRoot 'config')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not[IO.File]::Exists($path)){throw 'Replacement profile escapes config or is missing.'}}
    $checks.newState=@{configSha256=(Hash-File (Join-Path $NewInstallRoot 'config/default.json'));catalogSha256=(Hash-File (Join-Path $NewInstallRoot 'config/profiles.json'));selectionSha256=(Hash-File (Join-Path $NewInstallRoot 'config/selection.json'));policyCount=@($freeze.policyFiles).Count}
    $copySelection=Get-Content -LiteralPath (Safe-Path (Join-Path $copyRoot 'install.json')) -Raw|ConvertFrom-Json
    if($copySelection.root-cne$copyRoot-or$copySelection.selectedVersion-cne'0.5.0'-or$copySelection.versions.Count-ne2-or(Hash-File (Join-Path $copyRoot 'install.json'))-cne$rollback.selectionAfterSha256){throw 'External copy selection differs after rollback.'}
    $checks.copyOldPayload=Check-Payload (Join-Path $copyRoot 'versions/0.5.0') 'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225' '0.5.0' 'c4010d8c45eecadb3c51272334ceb4169e27b6ce'
    $checks.copyNewPayload=Check-Payload (Join-Path $copyRoot 'versions/0.6.0') 'a70f55859cf276ea786df2e68fcae4599cacc7c62cca9fcde20d27d6655f5e2b' '0.6.0' 'de222c2462e182191b2f71499421bfe086ea7bab'
    $upgradeBefore=Get-Content -LiteralPath (Safe-Path (Join-Path ([IO.Path]::GetDirectoryName($RollbackRoot)) 'before.json')) -Raw|ConvertFrom-Json
    if((Hash-File (Join-Path $copyRoot 'config/ifx.json'))-cne$upgradeBefore.configSha256-or(Tree-Hash (Join-Path $copyRoot 'rules'))-cne$upgradeBefore.librarySha256-or(Tree-Hash (Join-Path $copyRoot 'reports'))-cne$upgradeBefore.reportsSha256-or(Hash-File (Join-Path $copyRoot 'config/profiles.json'))-cne$rollback.launcherCatalogSha256-or(Hash-File (Join-Path $copyRoot 'config/selection.json'))-cne$rollback.launcherSelectionSha256){throw 'External copy user state changed.'}
    $operations=@(Get-ChildItem -LiteralPath (Safe-Path (Join-Path $copyRoot 'operations')) -Directory -Force)
    if($operations.Count-ne3){throw 'External copy operation count differs.'}
    foreach($item in $operations){$journal=Get-Content -LiteralPath (Safe-Path (Join-Path $item.FullName 'journal.json')) -Raw|ConvertFrom-Json;$receipt=Get-Content -LiteralPath (Safe-Path (Join-Path $item.FullName 'receipt.json')) -Raw|ConvertFrom-Json;if($journal.phase-cne'completed'-or$receipt.id-cne$item.Name-or$receipt.outcome-cne'success'){throw 'External copy operation incomplete.'}}
    $checks.copyState=@{operations=$operations.Count;catalogSha256=$rollback.launcherCatalogSha256;selectionSha256=$rollback.launcherSelectionSha256;laterEditRejected=$true}
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web','ArchSift.Setup' -ErrorAction SilentlyContinue)){try{$path=$running.Path;if(-not$path){throw 'Cannot inspect ArchSift process.'};foreach($root in @($ExistingInstallRoot,$NewInstallRoot,$copyRoot)){if($path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Owned ArchSift process remains.'}}}finally{$running.Dispose()}}
    $checks.processesStopped=$true
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;checks=$checks}|ConvertTo-Json -Depth 14))
    $completed=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$audit=@()
    if($null-ne$targetBefore){try{$targetEqual=Same-Target (Target-State) $targetBefore}catch{$targetEqual=$false;$audit+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=Same-Original (Original-State) $originalBefore}catch{$originalEqual=$false;$audit+=$_.Exception.Message}}
    $result=@{status=$(if($completed-and$hostEqual-and$targetEqual-and$originalEqual){'machine-audit-passed'}else{'stop'});runRoot=$run;productSourceFrozen=$completed;candidateAssetsEqual=$completed;replacementInstallationEqual=$completed;externalCopyEqual=$completed;ownedProcessesStopped=$completed;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;analysisOrBuildPerformed=$false;humanUiAcceptancePerformed=$false}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $audit));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false){throw 'Safety audit failed; preserve evidence.'}
}
