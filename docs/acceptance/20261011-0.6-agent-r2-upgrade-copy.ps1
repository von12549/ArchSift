# Agent-authorized block 05c: upgrade only the adopted external 0.5.0 copy to frozen 0.6.0.
[CmdletBinding()]
param(
    [string]$AdoptRun='D:\ArchSift-lab\evidence\ifx-0.6-r2-lifecycle-91c27481fd5443f09eefb0a541c37e00\adopt-f41a328d552147f381c0ff96112f21e8',
    [string]$SetupPath='D:\ArchSift-lab\packages\draft-0.6-replacement-independent-20261011\ArchSift.Setup.exe',
    [string]$ZipPath='D:\ArchSift-lab\packages\draft-0.6-replacement-independent-20261011\archsift-0.6.0-win-x64.zip',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$AdoptRun=Safe-Path $AdoptRun;$SetupPath=Safe-Path $SetupPath;$ZipPath=Safe-Path $ZipPath;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
if($AdoptRun-cne'D:\ArchSift-lab\evidence\ifx-0.6-r2-lifecycle-91c27481fd5443f09eefb0a541c37e00\adopt-f41a328d552147f381c0ff96112f21e8'){throw 'Only the reviewed adoption run is allowed.'}
$copyRun=Safe-Path ([IO.Path]::GetDirectoryName($AdoptRun));$copyRoot=Safe-Path (Join-Path $copyRun 'installation');$hostBefore=Get-ArchSiftHostHash
$run=Safe-Path (Join-Path $AdoptRun ('upgrade-'+[Guid]::NewGuid().ToString('N')));[void][IO.Directory]::CreateDirectory($run)
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
function Run-Setup([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){[void]$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'dotnet-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';[void][IO.Directory]::CreateDirectory($start.Environment['DOTNET_CLI_HOME'])
    $process=[Diagnostics.Process]::Start($start)
    try{$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();if(-not$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "Setup $Name timed out."};[Threading.Tasks.Task]::WaitAll($stdout,$stderr);[IO.File]::WriteAllText((Join-Path $run "$Name.json"),$stdout.Result);[IO.File]::WriteAllText((Join-Path $run "$Name.stderr.txt"),$stderr.Result);if($process.ExitCode-ne0-or($stdout.Result+$stderr.Result).Contains('#session=')){throw "Setup $Name failed or leaked token."};if((Get-ArchSiftHostHash)-cne$hostBefore){throw "Host drift after Setup $Name."};return $stdout.Result|ConvertFrom-Json}finally{$process.Dispose()}
}
$targetBefore=$null;$originalBefore=$null;$configHash=$null;$libraryHash=$null;$reportsHash=$null;$selectionBefore=$null;$plan=$null;$receipt=$null;$completed=$false;$applied=$false;$backupEqual=$false
try{
    if((Hash-File (Join-Path $AdoptRun 'result.json'))-cne'e819ff6bdd15159ea1011d61269d03319170e2b53d8e2ab78ef65c2ec330e42d'-or(Hash-File (Join-Path $AdoptRun 'adopt-receipt.json'))-cne'74514eed681ad2c28629eeb77763f42abd17340b767de0ba30a6b377ec27d50c'-or(Hash-File (Join-Path $AdoptRun 'adopt-inspect.json'))-cne'bca53724cef13a439ce5439bdd08485b5798e14cd834d5250b1be1c84fd398fa'-or(Hash-File $SetupPath)-cne'6327fcf10700aeec00f0b84da4201d4c3ad8b10d51d3f894766db4a2c55debab'-or(Hash-File $ZipPath)-cne'64af0fb70ea8548b99373151c7005c15b0365784d8faedd3890989d3d040a560'){throw 'Reviewed evidence or assets changed.'}
    $previous=Get-Content -LiteralPath (Join-Path $AdoptRun 'result.json') -Raw|ConvertFrom-Json
    $adoptReceipt=Get-Content -LiteralPath (Join-Path $AdoptRun 'adopt-receipt.json') -Raw|ConvertFrom-Json
    $adoptInspect=Get-Content -LiteralPath (Join-Path $AdoptRun 'adopt-inspect.json') -Raw|ConvertFrom-Json
    $copySummary=Get-Content -LiteralPath (Join-Path $copyRun 'copy-summary.json') -Raw|ConvertFrom-Json
    $copyBefore=Get-Content -LiteralPath (Join-Path $copyRun 'before.json') -Raw|ConvertFrom-Json
    if($previous.status-cne'external-copy-adopted'-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.originalInstallationStateEqual-or-not$previous.adoptApplied-or$previous.copyRoot-cne$copyRoot-or$adoptReceipt.operation-cne'adopt'-or$adoptReceipt.outcome-cne'success'-or$adoptInspect.status-cne'verified'-or$adoptInspect.installation.selectedVersion-cne'0.5.0'-or$adoptInspect.pendingOperations.Count-ne0){throw 'Adoption gate is not ready.'}
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){try{$path=$running.Path;if(-not$path){throw 'Cannot inspect active ArchSift process.'};if($path.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$path.StartsWith($copyRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original or copy installation is active.'}}finally{$running.Dispose()}}
    $targetBefore=Target-State;$originalBefore=Original-State
    if(-not(Same-Target $targetBefore $copyBefore.target)-or-not(Same-Original $originalBefore $copyBefore.original)){throw 'Protected state changed since copy.'}
    $selectionBefore=Hash-File (Join-Path $copyRoot 'install.json');$configHash=Hash-File (Join-Path $copyRoot 'config/ifx.json');$libraryHash=Tree-Hash (Join-Path $copyRoot 'rules');$reportsHash=Tree-Hash (Join-Path $copyRoot 'reports')
    if($selectionBefore-cne$adoptInspect.selectionSha256-or$configHash-cne$copySummary.copyConfigSha256-or$libraryHash-cne$copySummary.librarySha256-or$reportsHash-cne$copySummary.reportsSha256-or(Test-Path -LiteralPath (Join-Path $copyRoot 'versions/0.6.0'))){throw 'Adopted copy state changed.'}
    $plan=Run-Setup 'upgrade-plan' @('plan','upgrade','--root',$copyRoot,'--package',$ZipPath,'--sha256','64af0fb70ea8548b99373151c7005c15b0365784d8faedd3890989d3d040a560','--smoke','true')
    $planPath=Join-Path $run 'upgrade-plan.json'
    if($plan.schemaVersion-ne1-or$plan.operation-cne'upgrade'-or$plan.root-cne$copyRoot-or$plan.selectionSha256-cne$selectionBefore-or$plan.state.sha256-cne$adoptInspect.state.sha256-or-not$plan.smoke-or$plan.package.version-cne'0.6.0'-or$plan.package.sha256-cne'64af0fb70ea8548b99373151c7005c15b0365784d8faedd3890989d3d040a560'-or$plan.package.manifestSha256-cne'a70f55859cf276ea786df2e68fcae4599cacc7c62cca9fcde20d27d6655f5e2b'-or$plan.package.sourceCommit-cne'de222c2462e182191b2f71499421bfe086ea7bab'-or$plan.configPath-cne(Join-Path $copyRoot 'config/ifx.json')-or$plan.config.build.mode-cne'existing'-or$plan.config.build.allowNetwork-or[DateTimeOffset]::Parse($plan.expiresUtc)-le[DateTimeOffset]::UtcNow){throw 'Upgrade plan differs from reviewed scope.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;configSha256=$configHash;librarySha256=$libraryHash;reportsSha256=$reportsHash;selectionSha256=$selectionBefore;planId=$plan.planId;planSha256=(Hash-File $planPath)}|ConvertTo-Json -Depth 10))
    $receipt=Run-Setup 'upgrade-receipt' @('apply','--plan',$planPath,'--plan-id',$plan.planId);$applied=$true
    if($receipt.schemaVersion-ne1-or$receipt.operation-cne'upgrade'-or$receipt.outcome-cne'success'-or-not$receipt.hostStateEqual-or$receipt.planId-cne$plan.planId-or$receipt.before.selectedVersion-cne'0.5.0'-or$receipt.after.selectedVersion-cne'0.6.0'-or$receipt.beforeState.sha256-cne$plan.state.sha256-or$receipt.afterState.sha256-cne$plan.state.sha256-or$receipt.after.versions.Count-ne2){throw 'Upgrade receipt differs from plan.'}
    $backup=Safe-Path $receipt.backupDirectory
    if($backup-cne(Safe-Path (Join-Path $copyRoot ('operations/'+$receipt.id+'/backup')))){throw 'Upgrade backup escaped copy root.'}
    foreach($file in $plan.state.files){if($file.scope-cnotin@('config','library')){throw 'Unexpected state scope.'};$path=Safe-Path (Join-Path $backup ($file.scope+'/'+$file.path));if(-not$path.StartsWith($backup+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $path)-cne$file.sha256){throw 'Upgrade backup differs.'}}
    $backupEqual=@(Get-ChildItem -LiteralPath $backup -Recurse -File -Force).Count-eq$plan.state.files.Count
    if(-not$backupEqual){throw 'Upgrade backup file count differs.'}
    $inspect=Run-Setup 'upgrade-inspect' @('inspect','--root',$copyRoot)
    $selectionAfter=Hash-File (Join-Path $copyRoot 'install.json')
    if($inspect.status-cne'verified'-or$inspect.installation.selectedVersion-cne'0.6.0'-or$inspect.installation.lastOperation-cne$receipt.id-or$inspect.selectionSha256-cne$selectionAfter-or$selectionAfter-ceq$selectionBefore-or$inspect.state.sha256-cne$plan.state.sha256-or$inspect.pendingOperations.Count-ne0){throw 'Upgraded copy inspection differs.'}
    if((Hash-File (Join-Path $copyRoot 'config/ifx.json'))-cne$configHash-or(Tree-Hash (Join-Path $copyRoot 'rules'))-cne$libraryHash-or(Tree-Hash (Join-Path $copyRoot 'reports'))-cne$reportsHash){throw 'Upgrade changed copied user state.'}
    $completed=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;applied=$applied}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$audit=@()
    if($null-ne$targetBefore){try{$targetEqual=Same-Target (Target-State) $targetBefore}catch{$targetEqual=$false;$audit+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=Same-Original (Original-State) $originalBefore}catch{$originalEqual=$false;$audit+=$_.Exception.Message}}
    $result=@{status=$(if($completed-and$hostEqual-and$targetEqual-and$originalEqual-and$backupEqual){'external-copy-upgraded'}else{'stop'});runRoot=$run;copyRoot=$copyRoot;upgradeReceiptId=$receipt.id;selectionBeforeSha256=$selectionBefore;selectionAfterSha256=$(if($completed){$selectionAfter}else{$null});backupEqual=$backupEqual;upgradeApplied=$applied;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;analysisOrBuildPerformed=$false}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $audit));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false){throw 'Safety audit failed; preserve evidence.'}
}
