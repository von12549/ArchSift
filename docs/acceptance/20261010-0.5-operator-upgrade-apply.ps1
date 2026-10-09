# Operator-only step 04c: apply the reviewed 0.5.0 upgrade to the external copy; stop before rollback.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AdoptRun,
    [string]$SetupPath='D:\ArchSift-lab\packages\draft-0.5-accepted-candidate-f8be63c2c1bb4ce9ac44eb7b80bc24b5\ArchSift.Setup.exe',
    [string]$ZipPath='D:\ArchSift-lab\packages\draft-0.5-accepted-candidate-f8be63c2c1bb4ce9ac44eb7b80bc24b5\archsift-0.5.0-win-x64.zip',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory='D:\IFX-10-Root\ArchSift\versions\0.4.0'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$AdoptRun=Safe-Path $AdoptRun;$SetupPath=Safe-Path $SetupPath;$ZipPath=Safe-Path $ZipPath
$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$ExistingVersionDirectory=Safe-Path $ExistingVersionDirectory
if($AdoptRun-cne'D:\ArchSift-lab\evidence\ifx-0.5-plan-acc5c1fb295a4aac9530e67c66577d3c\apply-05d9f6e06ee74ac8b231634aff54acda\legacy-copy-5435ee29964b4516aefb29f1f32d4e9c\adopt-apply-7e07037b52f340819152f26818aad003'){throw 'Only the reviewed adopted-copy evidence root is allowed.'}
$CopyPlanRun=Safe-Path ([IO.Path]::GetDirectoryName($AdoptRun));$copyRoot=Safe-Path (Join-Path $CopyPlanRun 'installation')
foreach($protected in @($TargetRoot,$ExistingInstallRoot,[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))){
    if($AdoptRun-eq$protected-or$AdoptRun.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($AdoptRun+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/protected roots overlap.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null;$reportsBefore=$null;$copyBefore=$null;$selectionBefore=$null;$receiptId=$null;$selectionAfter=$null;$backupEqual=$false;$applyStarted=$false;$upgraded=$false;$completed=$false
$run=Join-Path $AdoptRun ('upgrade-apply-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory((Safe-Path $run))
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
    $manifest=Safe-Path (Join-Path $copyRoot 'versions/0.4.0/package-manifest.json')
    return [ordered]@{configSha256=(Hash-File $config);library=(Tree-State $library);reports=(Tree-State $reports);oldManifestSha256=(Hash-File $manifest)}
}
function Run-Setup([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'setup-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    [IO.File]::WriteAllText((Join-Path $run ($Name+'-owner.json')),(@{pid=$process.Id;executable=$SetupPath;operation=$Name}|ConvertTo-Json))
    try{
        if(-not$process.WaitForExit(300000)){throw ('Setup '+$Name+' timed out; preserve owner/journal evidence.')}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run ($Name+'.json')),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $run ($Name+'.stderr.log')),$stderr.Result)
        if(($stdout.Result+$stderr.Result).Contains('#session=')){throw 'Setup leaked a UI session token.'}
        if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift after Setup; preserve evidence.'}
        if($process.ExitCode-ne0){throw ('Setup '+$Name+' stopped with exit '+$process.ExitCode+'.')}
        return $stdout.Result|ConvertFrom-Json
    }finally{$process.Dispose()}
}
try{
    if((Hash-File (Join-Path $AdoptRun 'result.json'))-cne'bb965444c8b6e2ebdbd0c9b73b0e94c787f0f87adc1b34b1016e2bba74829914'-or(Hash-File (Join-Path $AdoptRun 'before.json'))-cne'7ade8ef59877a2f7272c954a0244dde9b39bf441564a73bf79560a1ac120fe67'-or(Hash-File (Join-Path $AdoptRun 'upgrade-plan.json'))-cne'69bc2c984d3d1849713b3795b1c990dc6d76de11bf8d5a72177735aa0fe06332'-or(Hash-File (Join-Path $AdoptRun 'adopt-receipt.json'))-cne'f6a7acb429a03b1278bc41b7ce3d754a3928569454fe154003158670e6586d91'-or(Hash-File (Join-Path $AdoptRun 'adopt-inspect.json'))-cne'd754dddb4d2d2097a98bb78d2cd009e985d73c9f99eb8dac85c7023268b30856'){throw 'Reviewed adopt/upgrade-plan evidence changed.'}
    if((Hash-File $SetupPath)-cne'f3eb9e4d166efc7c3ddb9bb738571016807f795b0cc68aeb6cb4f2668c5fad07'-or(Hash-File $ZipPath)-cne'9071b35036ea8d7b0a52e00bbd078b4de70ccc5b4a661a3f0daf10f5e6f9de85'){throw 'Setup/ZIP asset changed.'}
    $previous=Get-Content -LiteralPath (Safe-Path (Join-Path $AdoptRun 'result.json')) -Raw|ConvertFrom-Json
    $previousBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $AdoptRun 'before.json')) -Raw|ConvertFrom-Json
    $adoptReceipt=Get-Content -LiteralPath (Safe-Path (Join-Path $AdoptRun 'adopt-receipt.json')) -Raw|ConvertFrom-Json
    $adoptInspect=Get-Content -LiteralPath (Safe-Path (Join-Path $AdoptRun 'adopt-inspect.json')) -Raw|ConvertFrom-Json
    $planPath=Safe-Path (Join-Path $AdoptRun 'upgrade-plan.json');$plan=Get-Content -LiteralPath $planPath -Raw|ConvertFrom-Json
    if($previous.status-cne'external-copy-adopted-upgrade-plan-ready'-or$previous.runRoot-cne$AdoptRun-or$previous.copyRoot-cne$copyRoot-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.existing040StateEqual-or-not$previous.existingReportsEqual-or-not$previous.copyUserStateEqual-or-not$previous.adoptApplied-or$previous.upgradeApplied-or$previous.nextGate-cne'operator-external-copy-upgrade-apply'){throw 'Previous operator gate is not ready.'}
    if($plan.schemaVersion-ne1-or$plan.operation-cne'upgrade'-or$plan.planId-cne'b66f8bb972f168bbfea6cac91da4b7e47b794d0bbd4cdc458c05b2afe5b620bf'-or$plan.root-cne$copyRoot-or$plan.selectionSha256-cne'81c1d78cb4b024fb51046926a07ebd34b3a1f8576106f4e7feaf9fd9ec6d1317'-or$plan.state.sha256-cne'd5f2d02476a88821d8a9e623164a5cc88fdc0f5beffeb5e8f6a0eb62d823c858'-or-not$plan.smoke-or$plan.adoptedVersion-or$plan.package.version-cne'0.5.0'-or$plan.package.path-cne$ZipPath-or$plan.package.sha256-cne'9071b35036ea8d7b0a52e00bbd078b4de70ccc5b4a661a3f0daf10f5e6f9de85'-or$plan.package.manifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'-or$plan.package.sourceCommit-cne'c4010d8c45eecadb3c51272334ceb4169e27b6ce'-or$plan.configPath-cne(Join-Path $copyRoot 'config/ifx.json')-or(Safe-Path $plan.config.target.root)-cne$TargetRoot-or$plan.config.target.entry-cne'IFX.sln'-or$plan.config.build.mode-cne'existing'-or$plan.config.build.targetFramework-cne'net10.0'-or$plan.config.build.configuration-cne'Debug'-or$plan.config.build.allowNetwork-or$plan.config.rulesets.Count-ne0-or$plan.config.output.directory-cne(Join-Path $copyRoot 'reports')-or$plan.config.rulesDirectory-cne(Join-Path $copyRoot 'rules')){throw 'Upgrade plan differs from reviewed external-copy scope.'}
    if([DateTimeOffset]::Parse($plan.expiresUtc)-le[DateTimeOffset]::UtcNow){throw 'Reviewed upgrade plan expired; regenerate/review before applying.'}
    if($adoptReceipt.operation-cne'adopt'-or$adoptReceipt.outcome-cne'success'-or$adoptReceipt.id-cne$previous.adoptReceiptId-or$adoptReceipt.after.selectedVersion-cne'0.4.0'-or$adoptReceipt.afterState.sha256-cne$plan.state.sha256-or$adoptInspect.status-cne'verified'-or$adoptInspect.selectionSha256-cne$plan.selectionSha256-or$adoptInspect.installation.selectedVersion-cne'0.4.0'-or$adoptInspect.pendingOperations.Count-ne0){throw 'Adopt receipt/inspection differs from reviewed upgrade plan.'}
    $targetBefore=Target-State;$existingBefore=Existing-State;$reportsBefore=Original-Reports;$copyBefore=Copy-State;$selectionBefore=Hash-File (Join-Path $copyRoot 'install.json')
    if((Hash-Json $targetBefore)-cne(Hash-Json $previousBefore.target)-or(Hash-Json $existingBefore)-cne(Hash-Json $previousBefore.existing)-or(Hash-Json $reportsBefore)-cne(Hash-Json $previousBefore.originalReports)-or$copyBefore.configSha256-cne$previousBefore.copy.configSha256-or$copyBefore.library.sha256-cne$previousBefore.copy.library.sha256-or$copyBefore.library.fileCount-ne2-or$copyBefore.reports.sha256-cne$previousBefore.copy.reports.sha256-or$copyBefore.reports.fileCount-ne6-or$copyBefore.oldManifestSha256-cne$previousBefore.copy.manifestSha256-or$selectionBefore-cne$plan.selectionSha256){throw 'IFX/original installation/external-copy state changed since planning.'}
    if(Test-Path -LiteralPath (Join-Path $copyRoot 'versions/0.5.0')){throw '0.5.0 version already exists in copy; stop for review.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore;originalReports=$reportsBefore;copy=$copyBefore;selectionSha256=$selectionBefore;reviewedPlanId=$plan.planId;reviewedPlanSha256='69bc2c984d3d1849713b3795b1c990dc6d76de11bf8d5a72177735aa0fe06332'}|ConvertTo-Json -Depth 10))
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        $image=$running.Path
        if(-not$image){throw ('Cannot inspect an ArchSift process; review PID '+$running.Id)}
        if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$image.StartsWith($copyRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('Original/copy installation is running; review PID '+$running.Id)}
    }
    $applyStarted=$true
    $receipt=Run-Setup 'upgrade-receipt' @('apply','--plan',$planPath,'--plan-id',$plan.planId)
    $upgraded=$true;$receiptId=$receipt.id
    if($receipt.schemaVersion-ne1-or$receipt.operation-cne'upgrade'-or$receipt.planId-cne$plan.planId-or$receipt.outcome-cne'success'-or-not$receipt.hostStateEqual-or$receipt.before.selectedVersion-cne'0.4.0'-or$receipt.before.lastOperation-cne$previous.adoptReceiptId-or$receipt.after.root-cne$copyRoot-or$receipt.after.selectedVersion-cne'0.5.0'-or$receipt.beforeState.sha256-cne$plan.state.sha256-or$receipt.afterState.sha256-cne$plan.state.sha256-or$receipt.after.versions.Count-ne2){throw 'Upgrade receipt differs from reviewed plan.'}
    if((Hash-Json $receipt.before)-cne(Hash-Json $adoptInspect.installation)){throw 'Upgrade did not start from reviewed adopted selection.'}
    $persisted=Get-Content -LiteralPath (Safe-Path (Join-Path $copyRoot ('operations/'+$receiptId+'/receipt.json'))) -Raw|ConvertFrom-Json
    if((Hash-Json $persisted)-cne(Hash-Json $receipt)){throw 'Persisted upgrade receipt differs from returned receipt.'}
    $backup=Safe-Path $receipt.backupDirectory
    if($backup-cne(Safe-Path (Join-Path $copyRoot ('operations/'+$receiptId+'/backup')))-or$plan.state.files.Count-ne3){throw 'Upgrade backup location/state inventory differs.'}
    foreach($file in $plan.state.files){
        if($file.scope-cnotin@('config','library')){throw 'Unexpected state backup scope.'}
        $path=Safe-Path (Join-Path $backup ($file.scope+'/'+$file.path))
        if(-not$path.StartsWith($backup+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $path)-cne$file.sha256){throw 'Upgrade backup file differs from reviewed state.'}
    }
    if(@(Get-ChildItem -LiteralPath $backup -Recurse -File -Force).Count-ne$plan.state.files.Count){throw 'Upgrade backup file count differs.'}
    $backupEqual=$true
    $inspect=Run-Setup 'upgrade-inspect' @('inspect','--root',$copyRoot)
    $selectionAfter=Hash-File (Join-Path $copyRoot 'install.json')
    if($inspect.status-cne'verified'-or$inspect.installation.lastOperation-cne$receiptId-or$inspect.installation.selectedVersion-cne'0.5.0'-or$inspect.selectionSha256-cne$selectionAfter-or$selectionAfter-ceq$selectionBefore-or$inspect.state.sha256-cne$plan.state.sha256-or$inspect.pendingOperations.Count-ne0-or$inspect.installation.versions.Count-ne2){throw 'Upgraded copy inspection differs from receipt.'}
    if((Hash-Json $inspect.installation)-cne(Hash-Json $receipt.after)){throw 'Selected installation differs from upgrade receipt.'}
    $old=@($inspect.installation.versions|Where-Object version -EQ '0.4.0');$new=@($inspect.installation.versions|Where-Object version -EQ '0.5.0')
    if($old.Count-ne1-or$new.Count-ne1-or$old[0].manifestSha256-cne'd35fea42475633eb290c858e4df4aec4387dad639d2d03480d20e9edd532849c'-or$new[0].manifestSha256-cne$plan.package.manifestSha256-or$new[0].packageSha256-cne$plan.package.sha256){throw 'Two-version ownership differs from reviewed plan.'}
    $manifestPath=Safe-Path (Join-Path $copyRoot 'versions/0.5.0/package-manifest.json')
    if((Hash-File $manifestPath)-cne$plan.package.manifestSha256){throw 'New payload manifest differs.'}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
    if($manifest.version-cne'0.5.0'-or$manifest.sourceCommit-cne$plan.package.sourceCommit-or$manifest.entries.Count-ne626){throw 'New payload identity differs.'}
    if((Hash-Json (Copy-State))-cne(Hash-Json $copyBefore)){throw 'Upgrade changed config/library/reports/old manifest.'}
    $completed=$true
}catch{
    [IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;copyRoot=$copyRoot;upgraded=$upgraded}|ConvertTo-Json));throw
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$existingEqual=$null;$reportsEqual=$null;$copyEqual=$null;$auditErrors=[Collections.Generic.List[string]]::new()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$existingBefore){try{$existingEqual=(Hash-Json (Existing-State))-ceq(Hash-Json $existingBefore)}catch{$existingEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$reportsBefore){try{$reportsEqual=(Hash-Json (Original-Reports))-ceq(Hash-Json $reportsBefore)}catch{$reportsEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$copyBefore){try{$copyEqual=(Hash-Json (Copy-State))-ceq(Hash-Json $copyBefore)}catch{$copyEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    $passed=$completed-and$hostEqual-and$targetEqual-and$existingEqual-and$reportsEqual-and$copyEqual-and$backupEqual
    $result=[ordered]@{status=$(if($passed){'external-copy-upgrade-applied'}else{'stop'});runRoot=$run;copyRoot=$copyRoot;upgradeReceiptId=$receiptId;selectionBeforeSha256=$selectionBefore;selectionAfterSha256=$selectionAfter;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;existingReportsEqual=$reportsEqual;copyUserStateEqual=$copyEqual;backupEqual=$backupEqual;adoptApplied=$true;upgradeApplied=$(if($upgraded){$true}elseif($applyStarted){$null}else{$false});privateUiSmokeRequested=$applyStarted;analysisOrBuildPerformed=$false;nextGate=$(if($passed){'operator-external-copy-rollback'}else{$null})}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors.ToArray()));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or($null-ne$targetEqual-and-not$targetEqual)-or($null-ne$existingEqual-and-not$existingEqual)-or($null-ne$reportsEqual-and-not$reportsEqual)-or($null-ne$copyEqual-and-not$copyEqual)){throw 'Safety drift/audit failure; preserve evidence and stop. No repair.'}
}
