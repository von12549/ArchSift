# Agent-authorized block 05d: prove later managed edits are refused, then roll back only the external copy.
[CmdletBinding()]
param(
    [string]$UpgradeRun='D:\ArchSift-lab\evidence\ifx-0.6-r2-lifecycle-91c27481fd5443f09eefb0a541c37e00\adopt-f41a328d552147f381c0ff96112f21e8\upgrade-77fc080fb0734d9288961c37f3ecc1f8',
    [string]$SetupPath='D:\ArchSift-lab\packages\draft-0.6-replacement-independent-20261011\ArchSift.Setup.exe',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$UpgradeRun=Safe-Path $UpgradeRun;$SetupPath=Safe-Path $SetupPath;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
if($UpgradeRun-cne'D:\ArchSift-lab\evidence\ifx-0.6-r2-lifecycle-91c27481fd5443f09eefb0a541c37e00\adopt-f41a328d552147f381c0ff96112f21e8\upgrade-77fc080fb0734d9288961c37f3ecc1f8'){throw 'Only the reviewed upgrade run is allowed.'}
$copyRoot=Safe-Path (Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetDirectoryName($UpgradeRun))) 'installation')
$hostBefore=Get-ArchSiftHostHash;$run=Safe-Path (Join-Path $UpgradeRun ('rollback-'+[Guid]::NewGuid().ToString('N')));[void][IO.Directory]::CreateDirectory($run)
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
function Tree-Hash([string]$Root){$Root=Safe-Path $Root;$records=[Collections.Generic.List[object]]::new();foreach($item in @(Get-ChildItem -LiteralPath $Root -Recurse -Force)){$path=Safe-Path $item.FullName;if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Copy tree escapes root.'};if($item.PSIsContainer){continue};if(-not($item -is [IO.FileInfo])){throw 'Unsupported copy item.'};$records.Add([ordered]@{path=[IO.Path]::GetRelativePath($Root,$path);length=$item.Length;sha256=(Hash-File $path)});if($records.Count-gt10000){throw 'Copy file-count limit.'}};return Hash-Json @($records|Sort-Object path)}
function Run-Setup([string]$Name,[string[]]$Arguments,[bool]$AllowFailure=$false){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){[void]$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'dotnet-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';[void][IO.Directory]::CreateDirectory($start.Environment['DOTNET_CLI_HOME'])
    $process=[Diagnostics.Process]::Start($start)
    try{$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();if(-not$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "Setup $Name timed out."};[Threading.Tasks.Task]::WaitAll($stdout,$stderr);[IO.File]::WriteAllText((Join-Path $run "$Name.json"),$stdout.Result);[IO.File]::WriteAllText((Join-Path $run "$Name.stderr.txt"),$stderr.Result);if(($stdout.Result+$stderr.Result).Contains('#session=')){throw "Setup $Name leaked token."};if((Get-ArchSiftHostHash)-cne$hostBefore){throw "Host drift after Setup $Name."};if(-not$AllowFailure-and$process.ExitCode-ne0){throw "Setup $Name failed."};return @{exitCode=$process.ExitCode;stdout=$stdout.Result;stderr=$stderr.Result;value=$(if($process.ExitCode-eq0){$stdout.Result|ConvertFrom-Json}else{$null})}}finally{$process.Dispose()}
}
$targetBefore=$null;$originalBefore=$null;$completed=$false;$rolledBack=$false;$laterEditRejected=$false;$receipt=$null;$configHash=$null;$libraryHash=$null;$reportsHash=$null;$catalogHash=$null;$profileSelectionHash=$null;$selectionBefore=$null;$backupEqual=$false
try{
    if((Hash-File (Join-Path $UpgradeRun 'result.json'))-cne'cc6041c71cbc7c9508a64ca9f65d7a6dcf9072aa1c53f26e21291f678377b276'-or(Hash-File (Join-Path $UpgradeRun 'upgrade-plan.json'))-cne'79376404ddefdc2be05d6daf4236ac96b2239ecf7ec3cc376e4ce94e2e800a19'-or(Hash-File (Join-Path $UpgradeRun 'upgrade-receipt.json'))-cne'e9900cdb4ca326f6d392114f667e94283d19712b4a3299a42215ed072d1bfc2f'-or(Hash-File (Join-Path $UpgradeRun 'upgrade-inspect.json'))-cne'3581b99ee54d63a8ea1672bcee2b93be8e235f9fe4fb89e9955102a7f99b5039'-or(Hash-File $SetupPath)-cne'6327fcf10700aeec00f0b84da4201d4c3ad8b10d51d3f894766db4a2c55debab'){throw 'Reviewed upgrade evidence or Setup changed.'}
    $previous=Get-Content -LiteralPath (Join-Path $UpgradeRun 'result.json') -Raw|ConvertFrom-Json
    $plan=Get-Content -LiteralPath (Join-Path $UpgradeRun 'upgrade-plan.json') -Raw|ConvertFrom-Json
    $upgradeReceipt=Get-Content -LiteralPath (Join-Path $UpgradeRun 'upgrade-receipt.json') -Raw|ConvertFrom-Json
    $upgradeInspect=Get-Content -LiteralPath (Join-Path $UpgradeRun 'upgrade-inspect.json') -Raw|ConvertFrom-Json
    $before=Get-Content -LiteralPath (Join-Path $UpgradeRun 'before.json') -Raw|ConvertFrom-Json
    if($previous.status-cne'external-copy-upgraded'-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.originalInstallationStateEqual-or-not$previous.backupEqual-or$previous.copyRoot-cne$copyRoot-or$upgradeReceipt.id-cne$previous.upgradeReceiptId-or$upgradeReceipt.operation-cne'upgrade'-or$upgradeReceipt.outcome-cne'success'-or$upgradeInspect.status-cne'verified'-or$upgradeInspect.installation.selectedVersion-cne'0.6.0'-or$upgradeInspect.pendingOperations.Count-ne0){throw 'Upgrade gate is not ready.'}
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){try{$path=$running.Path;if(-not$path){throw 'Cannot inspect active ArchSift process.'};if($path.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$path.StartsWith($copyRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original or copy installation is active.'}}finally{$running.Dispose()}}
    $targetBefore=Target-State;$originalBefore=Original-State
    if(-not(Same-Target $targetBefore $before.target)-or-not(Same-Original $originalBefore $before.original)){throw 'Protected state changed since upgrade.'}
    $installPath=Safe-Path (Join-Path $copyRoot 'install.json');$configPath=Safe-Path (Join-Path $copyRoot 'config/ifx.json');$catalogPath=Safe-Path (Join-Path $copyRoot 'config/profiles.json');$profileSelectionPath=Safe-Path (Join-Path $copyRoot 'config/selection.json')
    if((Test-Path -LiteralPath $catalogPath)-or(Test-Path -LiteralPath $profileSelectionPath)){throw 'Launcher metadata already exists on external copy.'}
    $selectionBefore=Hash-File $installPath;$configHash=Hash-File $configPath;$libraryHash=Tree-Hash (Join-Path $copyRoot 'rules');$reportsHash=Tree-Hash (Join-Path $copyRoot 'reports')
    if($selectionBefore-cne$previous.selectionAfterSha256-or$configHash-cne$before.configSha256-or$libraryHash-cne$before.librarySha256-or$reportsHash-cne$before.reportsSha256){throw 'Upgraded copy state changed.'}
    $profileId=[Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($catalogPath,(@{schemaVersion=1;defaultProfileId=$profileId;profiles=@(@{profileId=$profileId;name='ifx.json';configPath=$configPath})}|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($profileSelectionPath,(@{schemaVersion=1;selectedProfileId=$profileId}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    $catalogHash=Hash-File $catalogPath;$profileSelectionHash=Hash-File $profileSelectionPath
    $metadataInspect=(Run-Setup 'metadata-inspect' @('inspect','--root',$copyRoot)).value
    if($metadataInspect.status-cne'verified'-or$metadataInspect.state.sha256-cne$plan.state.sha256-or$metadataInspect.selectionSha256-cne$selectionBefore){throw 'Launcher metadata changed Setup managed state.'}
    $operationsBefore=@([IO.Directory]::EnumerateDirectories((Join-Path $copyRoot 'operations'))).Count
    $originalBytes=[IO.File]::ReadAllBytes($configPath)
    if($originalBytes.Length-gt4MB){throw 'Managed config unexpectedly large.'}
    [IO.File]::WriteAllBytes($configPath,($originalBytes+[byte[]]@(32)))
    $reject=Run-Setup 'later-edit-rejection' @('rollback','--root',$copyRoot,'--receipt',$upgradeReceipt.id,'--selection-sha256',$selectionBefore) $true
    if($reject.exitCode-eq0-or-not($reject.stdout+$reject.stderr).Contains('Later user edits detected')-or(Hash-File $installPath)-cne$selectionBefore-or@([IO.Directory]::EnumerateDirectories((Join-Path $copyRoot 'operations'))).Count-ne$operationsBefore){throw 'Later managed edit was not safely refused; preserve modified copy.'}
    $laterEditRejected=$true
    [IO.File]::WriteAllBytes($configPath,$originalBytes)
    if((Hash-File $configPath)-cne$configHash){throw 'Controlled edit restoration differs.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;configSha256=$configHash;librarySha256=$libraryHash;reportsSha256=$reportsHash;catalogSha256=$catalogHash;profileSelectionSha256=$profileSelectionHash;selectionSha256=$selectionBefore;upgradeReceiptId=$upgradeReceipt.id;laterEditRejected=$true}|ConvertTo-Json -Depth 10))
    $receipt=(Run-Setup 'rollback-receipt' @('rollback','--root',$copyRoot,'--receipt',$upgradeReceipt.id,'--selection-sha256',$selectionBefore)).value;$rolledBack=$true
    if($receipt.schemaVersion-ne1-or$receipt.operation-cne'rollback'-or$receipt.outcome-cne'success'-or-not$receipt.hostStateEqual-or$receipt.before.selectedVersion-cne'0.6.0'-or$receipt.after.selectedVersion-cne'0.5.0'-or$receipt.beforeState.sha256-cne$plan.state.sha256-or$receipt.afterState.sha256-cne$plan.state.sha256-or$receipt.after.versions.Count-ne2){throw 'Rollback receipt differs.'}
    $inspect=(Run-Setup 'rollback-inspect' @('inspect','--root',$copyRoot)).value
    $selectionAfter=Hash-File $installPath
    if($inspect.status-cne'verified'-or$inspect.installation.selectedVersion-cne'0.5.0'-or$inspect.installation.lastOperation-cne$receipt.id-or$inspect.selectionSha256-cne$selectionAfter-or$selectionAfter-ceq$selectionBefore-or$inspect.state.sha256-cne$plan.state.sha256-or$inspect.pendingOperations.Count-ne0){throw 'Rolled-back copy inspection differs.'}
    $backup=Safe-Path $upgradeReceipt.backupDirectory
    $backupEqual=@($plan.state.files|Where-Object {(Hash-File (Safe-Path (Join-Path $backup ($_.scope+'/'+$_.path))))-cne$_.sha256}).Count-eq0
    if(-not$backupEqual-or(Hash-File $configPath)-cne$configHash-or(Tree-Hash (Join-Path $copyRoot 'rules'))-cne$libraryHash-or(Tree-Hash (Join-Path $copyRoot 'reports'))-cne$reportsHash-or(Hash-File $catalogPath)-cne$catalogHash-or(Hash-File $profileSelectionPath)-cne$profileSelectionHash){throw 'Rollback changed saved user state, launcher metadata or backup.'}
    $completed=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;rolledBack=$rolledBack;laterEditRejected=$laterEditRejected}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$audit=@()
    if($null-ne$targetBefore){try{$targetEqual=Same-Target (Target-State) $targetBefore}catch{$targetEqual=$false;$audit+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=Same-Original (Original-State) $originalBefore}catch{$originalEqual=$false;$audit+=$_.Exception.Message}}
    $result=@{status=$(if($completed-and$hostEqual-and$targetEqual-and$originalEqual-and$backupEqual-and$laterEditRejected){'external-copy-rollback-passed'}else{'stop'});runRoot=$run;copyRoot=$copyRoot;upgradeReceiptId='4ac0821eee4b4ff8b103f2a91ac46f8b';rollbackReceiptId=$receipt.id;selectionBeforeSha256=$selectionBefore;selectionAfterSha256=$(if($completed){$selectionAfter}else{$null});launcherCatalogSha256=$catalogHash;launcherSelectionSha256=$profileSelectionHash;laterManagedEditRejected=$laterEditRejected;backupEqual=$backupEqual;rollbackApplied=$rolledBack;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;analysisOrBuildPerformed=$false}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $audit));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false){throw 'Safety audit failed; preserve evidence.'}
}
