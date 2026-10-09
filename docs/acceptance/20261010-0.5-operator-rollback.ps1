# Operator-only step 04d: roll back the reviewed external copy from 0.5.0 to 0.4.0; retain both versions.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$UpgradeRun,
    [string]$SetupPath='D:\ArchSift-lab\packages\draft-0.5-accepted-candidate-f8be63c2c1bb4ce9ac44eb7b80bc24b5\ArchSift.Setup.exe',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory='D:\IFX-10-Root\ArchSift\versions\0.4.0'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$UpgradeRun=Safe-Path $UpgradeRun;$SetupPath=Safe-Path $SetupPath;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$ExistingVersionDirectory=Safe-Path $ExistingVersionDirectory
if($UpgradeRun-cne'D:\ArchSift-lab\evidence\ifx-0.5-plan-acc5c1fb295a4aac9530e67c66577d3c\apply-05d9f6e06ee74ac8b231634aff54acda\legacy-copy-5435ee29964b4516aefb29f1f32d4e9c\adopt-apply-7e07037b52f340819152f26818aad003\upgrade-apply-6f081b1c5c5f42a1b1b430349d36309e'){throw 'Only the reviewed upgraded-copy evidence root is allowed.'}
$AdoptRun=Safe-Path ([IO.Path]::GetDirectoryName($UpgradeRun));$CopyPlanRun=Safe-Path ([IO.Path]::GetDirectoryName($AdoptRun));$copyRoot=Safe-Path (Join-Path $CopyPlanRun 'installation')
foreach($protected in @($TargetRoot,$ExistingInstallRoot,[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))){
    if($UpgradeRun-eq$protected-or$UpgradeRun.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($UpgradeRun+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/protected roots overlap.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null;$reportsBefore=$null;$copyBefore=$null;$selectionBefore=$null;$selectionAfter=$null;$receiptId=$null;$upgradePlan=$null;$upgradeReceipt=$null;$rollbackStarted=$false;$rolledBack=$false;$backupEqual=$false;$completed=$false
$run=Join-Path $UpgradeRun ('rollback-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory((Safe-Path $run))
function Tree-State([string]$Root){
    $Root=Safe-Path $Root
    if(-not[IO.Directory]::Exists($Root)){throw 'Expected state directory missing.'}
    $records=[Collections.Generic.List[object]]::new()
    foreach($item in @(Get-ChildItem -LiteralPath $Root -Recurse -Force)){
        $path=Safe-Path $item.FullName
        if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'State item escapes root.'}
        if($item.PSIsContainer){continue}
        if(-not($item -is [IO.FileInfo])){throw 'Unsupported state item.'}
        $records.Add([ordered]@{path=[IO.Path]::GetRelativePath($Root,$path);length=$item.Length;sha256=(Hash-File $path)})
        if($records.Count-gt10000){throw 'State file-count limit.'}
    }
    $ordered=@($records|Sort-Object path)
    return [ordered]@{sha256=(Hash-Json $ordered);fileCount=$ordered.Count}
}
function Original-Reports {
    $config=Get-Content -LiteralPath (Safe-Path (Join-Path $ExistingInstallRoot 'config/ifx.json')) -Raw|ConvertFrom-Json
    $path=Safe-Path ([string]$config.output.directory)
    if(-not$path.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original reports path escapes installation.'}
    return Tree-State $path
}
function Copy-State {
    $config=Safe-Path (Join-Path $copyRoot 'config/ifx.json');$library=Safe-Path (Join-Path $copyRoot 'rules');$reports=Safe-Path (Join-Path $copyRoot 'reports')
    $old=Safe-Path (Join-Path $copyRoot 'versions/0.4.0/package-manifest.json');$new=Safe-Path (Join-Path $copyRoot 'versions/0.5.0/package-manifest.json')
    return [ordered]@{configSha256=(Hash-File $config);library=(Tree-State $library);reports=(Tree-State $reports);oldManifestSha256=(Hash-File $old);newManifestSha256=(Hash-File $new)}
}
function Backup-Equal([object]$Plan,[string]$UpgradeId){
    $backup=Safe-Path (Join-Path $copyRoot ('operations/'+$UpgradeId+'/backup'))
    if($Plan.state.files.Count-ne3){return $false}
    foreach($file in $Plan.state.files){
        if($file.scope-cnotin@('config','library')){return $false}
        $path=Safe-Path (Join-Path $backup ($file.scope+'/'+$file.path))
        if(-not$path.StartsWith($backup+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $path)-cne$file.sha256){return $false}
    }
    return @(Get-ChildItem -LiteralPath $backup -Recurse -File -Force).Count-eq$Plan.state.files.Count
}
function Run-Setup([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'setup-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    [IO.File]::WriteAllText((Join-Path $run ($Name+'-owner.json')),(@{pid=$process.Id;executable=$SetupPath;operation=$Name}|ConvertTo-Json))
    try{
        if(-not$process.WaitForExit(180000)){throw ('Setup '+$Name+' timed out; preserve owner/journal evidence.')}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run ($Name+'.json')),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $run ($Name+'.stderr.log')),$stderr.Result)
        if(($stdout.Result+$stderr.Result).Contains('#session=')){throw 'Setup leaked a UI session token.'}
        if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift after Setup; preserve evidence.'}
        if($process.ExitCode-ne0){throw ('Setup '+$Name+' stopped with exit '+$process.ExitCode+'.')}
        return $stdout.Result|ConvertFrom-Json
    }finally{$process.Dispose()}
}
try{
    if((Hash-File (Join-Path $UpgradeRun 'result.json'))-cne'122c9636a9824851d9b9b57a41c2a4efc5939506436f858e4058c022901febd4'-or(Hash-File (Join-Path $UpgradeRun 'before.json'))-cne'8695dd16be4822223772b1d534aab445ac9d4eaede2664b5600d87dc3e5c9a57'-or(Hash-File (Join-Path $UpgradeRun 'upgrade-receipt.json'))-cne'dbdf318d27acee0cf7b1d284922019a0ad93fa3975c6d91ac3505eafbebe8ec2'-or(Hash-File (Join-Path $UpgradeRun 'upgrade-inspect.json'))-cne'9b958715ff5390033a9e2a7b301ee6d04145b4dfc6ab214a1531079a5e5d8c9d'-or(Hash-File (Join-Path $AdoptRun 'upgrade-plan.json'))-cne'69bc2c984d3d1849713b3795b1c990dc6d76de11bf8d5a72177735aa0fe06332'){throw 'Reviewed upgrade evidence changed.'}
    if((Hash-File $SetupPath)-cne'f3eb9e4d166efc7c3ddb9bb738571016807f795b0cc68aeb6cb4f2668c5fad07'){throw 'Setup asset changed.'}
    $previous=Get-Content -LiteralPath (Safe-Path (Join-Path $UpgradeRun 'result.json')) -Raw|ConvertFrom-Json
    $previousBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $UpgradeRun 'before.json')) -Raw|ConvertFrom-Json
    $upgradeReceipt=Get-Content -LiteralPath (Safe-Path (Join-Path $UpgradeRun 'upgrade-receipt.json')) -Raw|ConvertFrom-Json
    $upgradeInspect=Get-Content -LiteralPath (Safe-Path (Join-Path $UpgradeRun 'upgrade-inspect.json')) -Raw|ConvertFrom-Json
    $upgradePlan=Get-Content -LiteralPath (Safe-Path (Join-Path $AdoptRun 'upgrade-plan.json')) -Raw|ConvertFrom-Json
    if($previous.status-cne'external-copy-upgrade-applied'-or$previous.runRoot-cne$UpgradeRun-or$previous.copyRoot-cne$copyRoot-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.existing040StateEqual-or-not$previous.existingReportsEqual-or-not$previous.copyUserStateEqual-or-not$previous.backupEqual-or-not$previous.upgradeApplied-or$previous.analysisOrBuildPerformed-or$previous.nextGate-cne'operator-external-copy-rollback'){throw 'Previous upgrade gate is not ready.'}
    if($upgradeReceipt.schemaVersion-ne1-or$upgradeReceipt.operation-cne'upgrade'-or$upgradeReceipt.outcome-cne'success'-or-not$upgradeReceipt.hostStateEqual-or$upgradeReceipt.id-cne'6efbaf703da244aa8ee24f175fa6952e'-or$upgradeReceipt.planId-cne$upgradePlan.planId-or$upgradeReceipt.before.selectedVersion-cne'0.4.0'-or$upgradeReceipt.after.selectedVersion-cne'0.5.0'-or$upgradeReceipt.beforeState.sha256-cne$upgradePlan.state.sha256-or$upgradeReceipt.afterState.sha256-cne$upgradePlan.state.sha256-or$upgradeInspect.status-cne'verified'-or$upgradeInspect.installation.selectedVersion-cne'0.5.0'-or$upgradeInspect.selectionSha256-cne'dadb56f3f16e7e479b7784ca0061f92f389001e168ed7a9af34b0e29c406c1a7'-or$upgradeInspect.pendingOperations.Count-ne0-or(Hash-Json $upgradeInspect.installation)-cne(Hash-Json $upgradeReceipt.after)){throw 'Upgrade receipt/inspection differs from reviewed rollback authority.'}
    $targetBefore=Target-State;$existingBefore=Existing-State;$reportsBefore=Original-Reports;$copyBefore=Copy-State;$selectionBefore=Hash-File (Join-Path $copyRoot 'install.json')
    if((Hash-Json $targetBefore)-cne(Hash-Json $previousBefore.target)-or(Hash-Json $existingBefore)-cne(Hash-Json $previousBefore.existing)-or(Hash-Json $reportsBefore)-cne(Hash-Json $previousBefore.originalReports)-or$copyBefore.configSha256-cne$previousBefore.copy.configSha256-or$copyBefore.library.sha256-cne$previousBefore.copy.library.sha256-or$copyBefore.library.fileCount-ne2-or$copyBefore.reports.sha256-cne$previousBefore.copy.reports.sha256-or$copyBefore.reports.fileCount-ne6-or$copyBefore.oldManifestSha256-cne$previousBefore.copy.oldManifestSha256-or$copyBefore.newManifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'-or$selectionBefore-cne$previous.selectionAfterSha256){throw 'IFX/original installation/external-copy state changed since upgrade.'}
    $backupEqual=Backup-Equal $upgradePlan $upgradeReceipt.id
    if(-not$backupEqual){throw 'Upgrade backup differs from reviewed pre-upgrade state.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore;originalReports=$reportsBefore;copy=$copyBefore;selectionSha256=$selectionBefore;upgradeReceiptId=$upgradeReceipt.id;upgradeReceiptSha256='dbdf318d27acee0cf7b1d284922019a0ad93fa3975c6d91ac3505eafbebe8ec2'}|ConvertTo-Json -Depth 10))
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        $image=$running.Path
        if(-not$image){throw ('Cannot inspect an ArchSift process; review PID '+$running.Id)}
        if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$image.StartsWith($copyRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('Original/copy installation is running; review PID '+$running.Id)}
    }
    $rollbackStarted=$true
    $receipt=Run-Setup 'rollback-receipt' @('rollback','--root',$copyRoot,'--receipt',$upgradeReceipt.id,'--selection-sha256',$selectionBefore)
    $rolledBack=$true;$receiptId=$receipt.id
    if($receipt.schemaVersion-ne1-or$receipt.operation-cne'rollback'-or$receipt.outcome-cne'success'-or-not$receipt.hostStateEqual-or$receipt.before.selectedVersion-cne'0.5.0'-or$receipt.after.selectedVersion-cne'0.4.0'-or$receipt.after.root-cne$copyRoot-or$receipt.after.lastOperation-cne$receiptId-or$receipt.beforeState.sha256-cne$upgradePlan.state.sha256-or$receipt.afterState.sha256-cne$upgradePlan.state.sha256-or$receipt.after.versions.Count-ne2-or(Hash-Json $receipt.before)-cne(Hash-Json $upgradeInspect.installation)){throw 'Rollback receipt differs from reviewed upgrade.'}
    $persisted=Get-Content -LiteralPath (Safe-Path (Join-Path $copyRoot ('operations/'+$receiptId+'/receipt.json'))) -Raw|ConvertFrom-Json
    if((Hash-Json $persisted)-cne(Hash-Json $receipt)){throw 'Persisted rollback receipt differs from returned receipt.'}
    $inspect=Run-Setup 'rollback-inspect' @('inspect','--root',$copyRoot)
    $selectionAfter=Hash-File (Join-Path $copyRoot 'install.json')
    if($inspect.status-cne'verified'-or$inspect.installation.lastOperation-cne$receiptId-or$inspect.installation.selectedVersion-cne'0.4.0'-or$inspect.selectionSha256-cne$selectionAfter-or$selectionAfter-ceq$selectionBefore-or$inspect.state.sha256-cne$upgradePlan.state.sha256-or$inspect.pendingOperations.Count-ne0-or$inspect.installation.versions.Count-ne2-or(Hash-Json $inspect.installation)-cne(Hash-Json $receipt.after)){throw 'Rolled-back copy inspection differs from receipt.'}
    $old=@($inspect.installation.versions|Where-Object version -EQ '0.4.0');$new=@($inspect.installation.versions|Where-Object version -EQ '0.5.0')
    if($old.Count-ne1-or$new.Count-ne1-or$old[0].manifestSha256-cne'd35fea42475633eb290c858e4df4aec4387dad639d2d03480d20e9edd532849c'-or$new[0].manifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'){throw 'Two-version ownership changed during rollback.'}
    $backupEqual=Backup-Equal $upgradePlan $upgradeReceipt.id
    if((Hash-Json (Copy-State))-cne(Hash-Json $copyBefore)-or-not$backupEqual){throw 'Rollback changed saved state, packages or upgrade backup.'}
    $completed=$true
}catch{
    [IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;copyRoot=$copyRoot;rolledBack=$rolledBack}|ConvertTo-Json));throw
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$existingEqual=$null;$reportsEqual=$null;$copyEqual=$null;$auditErrors=[Collections.Generic.List[string]]::new()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$existingBefore){try{$existingEqual=(Hash-Json (Existing-State))-ceq(Hash-Json $existingBefore)}catch{$existingEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$reportsBefore){try{$reportsEqual=(Hash-Json (Original-Reports))-ceq(Hash-Json $reportsBefore)}catch{$reportsEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$copyBefore){try{$copyEqual=(Hash-Json (Copy-State))-ceq(Hash-Json $copyBefore)}catch{$copyEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$upgradePlan-and$null-ne$upgradeReceipt){try{$backupEqual=Backup-Equal $upgradePlan $upgradeReceipt.id}catch{$backupEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    $passed=$completed-and$hostEqual-and$targetEqual-and$existingEqual-and$reportsEqual-and$copyEqual-and$backupEqual
    $result=[ordered]@{status=$(if($passed){'external-copy-rollback-passed'}else{'stop'});runRoot=$run;copyRoot=$copyRoot;upgradeReceiptId='6efbaf703da244aa8ee24f175fa6952e';rollbackReceiptId=$receiptId;selectionBeforeSha256=$selectionBefore;selectionAfterSha256=$selectionAfter;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;existingReportsEqual=$reportsEqual;copyUserStateEqual=$copyEqual;upgradeBackupEqual=$backupEqual;upgradeApplied=$true;rollbackApplied=$(if($rolledBack){$true}elseif($rollbackStarted){$null}else{$false});analysisOrBuildPerformed=$false;nextGate=$(if($passed){'operator-final-audit'}else{$null})}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors.ToArray()));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or($null-ne$targetEqual-and-not$targetEqual)-or($null-ne$existingEqual-and-not$existingEqual)-or($null-ne$reportsEqual-and-not$reportsEqual)-or($null-ne$copyEqual-and-not$copyEqual)){throw 'Safety drift/audit failure; preserve evidence and stop. No repair.'}
}
