# Operator-only final 0.5.0 read-only audit. No installation, target analysis, build, cleanup or publication.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RollbackRun,
    [string]$NewInstallRoot='D:\IFX-10-Root\ArchSift-0.5-review',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory='D:\IFX-10-Root\ArchSift\versions\0.4.0',
    [string]$AssetRoot='D:\ArchSift-lab\packages\draft-0.5-accepted-candidate-f8be63c2c1bb4ce9ac44eb7b80bc24b5'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$RollbackRun=Safe-Path $RollbackRun;$NewInstallRoot=Safe-Path $NewInstallRoot;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$ExistingVersionDirectory=Safe-Path $ExistingVersionDirectory;$AssetRoot=Safe-Path $AssetRoot
if($RollbackRun-cne'D:\ArchSift-lab\evidence\ifx-0.5-plan-acc5c1fb295a4aac9530e67c66577d3c\apply-05d9f6e06ee74ac8b231634aff54acda\legacy-copy-5435ee29964b4516aefb29f1f32d4e9c\adopt-apply-7e07037b52f340819152f26818aad003\upgrade-apply-6f081b1c5c5f42a1b1b430349d36309e\rollback-3e9c971bee5946159fc192dd0ad4e618'){throw 'Only the reviewed rollback evidence root is allowed.'}
$UpgradeRun=Safe-Path ([IO.Path]::GetDirectoryName($RollbackRun));$AdoptRun=Safe-Path ([IO.Path]::GetDirectoryName($UpgradeRun));$CopyPlanRun=Safe-Path ([IO.Path]::GetDirectoryName($AdoptRun));$copyRoot=Safe-Path (Join-Path $CopyPlanRun 'installation');$repoRoot=Safe-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
foreach($protected in @($NewInstallRoot,$TargetRoot,$ExistingInstallRoot,$repoRoot)){
    if($RollbackRun-eq$protected-or$RollbackRun.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($RollbackRun+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/protected roots overlap.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null;$reportsBefore=$null;$copyBefore=$null;$newBefore=$null;$sourceBefore=$null;$assetsBefore=$null;$processBefore=$null;$completed=$false
$run=Join-Path $RollbackRun ('final-audit-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory((Safe-Path $run))
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
function Payload-State([string]$VersionRoot,[string]$ExpectedManifest,[string]$Version,[int]$Count,[string]$Commit){
    $VersionRoot=Safe-Path $VersionRoot;$manifestPath=Safe-Path (Join-Path $VersionRoot 'package-manifest.json')
    if((Hash-File $manifestPath)-cne$ExpectedManifest){throw 'Package manifest differs.'}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
    if($manifest.version-cne$Version-or$manifest.sourceCommit-cne$Commit-or$manifest.entries.Count-ne$Count){throw 'Package identity/count differs.'}
    foreach($entry in $manifest.entries){
        $file=Safe-Path (Join-Path $VersionRoot $entry.path)
        if(-not$file.StartsWith($VersionRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $file)-cne$entry.sha256-or([IO.FileInfo]$file).Length-ne$entry.bytes){throw 'Package payload differs.'}
    }
    $all=@(Get-ChildItem -LiteralPath $VersionRoot -Recurse -File -Force)
    if($all.Count-ne($Count+1)){throw 'Package directory contains an unexpected file count.'}
    foreach($file in $all){[void](Safe-Path $file.FullName)}
    return [ordered]@{manifestSha256=$ExpectedManifest;fileCount=$Count;sourceCommit=$Commit}
}
function Copy-State {
    $selection=Safe-Path (Join-Path $copyRoot 'install.json');$config=Safe-Path (Join-Path $copyRoot 'config/ifx.json')
    $old=Payload-State (Join-Path $copyRoot 'versions/0.4.0') 'd35fea42475633eb290c858e4df4aec4387dad639d2d03480d20e9edd532849c' '0.4.0' 621 'bb599bd87aaa3322229c049442e135f3c091b72a'
    $new=Payload-State (Join-Path $copyRoot 'versions/0.5.0') 'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225' '0.5.0' 626 'c4010d8c45eecadb3c51272334ceb4169e27b6ce'
    return [ordered]@{selectionSha256=(Hash-File $selection);configSha256=(Hash-File $config);library=(Tree-State (Join-Path $copyRoot 'rules'));reports=(Tree-State (Join-Path $copyRoot 'reports'));old=$old;new=$new}
}
function NewInstall-State {
    $selection=Safe-Path (Join-Path $NewInstallRoot 'install.json');$config=Safe-Path (Join-Path $NewInstallRoot 'config/project.json');$library=Safe-Path (Join-Path $NewInstallRoot 'rules')
    $payload=Payload-State (Join-Path $NewInstallRoot 'versions/0.5.0') 'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225' '0.5.0' 626 'c4010d8c45eecadb3c51272334ceb4169e27b6ce'
    $files=@(Get-ChildItem -LiteralPath $library -Recurse -File -Force)
    if($files.Count-ne2-or@( $files|Where-Object {[IO.Path]::GetRelativePath($library,$_.FullName)-notin@('.archsift-library.json','.archsift-library.lock')} ).Count-ne0){throw 'New installation library gained user files.'}
    $registry=Get-Content -LiteralPath (Safe-Path (Join-Path $library '.archsift-library.json')) -Raw|ConvertFrom-Json
    if($registry.schemaVersion-ne1-or$registry.entries.Count-ne0-or[IO.Directory]::Exists((Join-Path $NewInstallRoot 'reports'))){throw 'New installation gained policy/report state.'}
    return [ordered]@{selectionSha256=(Hash-File $selection);configSha256=(Hash-File $config);payload=$payload;library=(Tree-State $library);reportsAbsent=$true}
}
function Backup-Equal {
    $plan=Get-Content -LiteralPath (Safe-Path (Join-Path $AdoptRun 'upgrade-plan.json')) -Raw|ConvertFrom-Json
    $backup=Safe-Path (Join-Path $copyRoot 'operations/6efbaf703da244aa8ee24f175fa6952e/backup')
    if($plan.state.files.Count-ne3){return $false}
    foreach($file in $plan.state.files){
        if($file.scope-cnotin@('config','library')){return $false}
        $path=Safe-Path (Join-Path $backup ($file.scope+'/'+$file.path))
        if(-not$path.StartsWith($backup+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $path)-cne$file.sha256){return $false}
    }
    return @(Get-ChildItem -LiteralPath $backup -Recurse -File -Force).Count-eq3
}
function Git-Product([string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command git).Source;$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in (@('--no-optional-locks','--no-replace-objects','-c','core.fsmonitor=false','-c','core.untrackedCache=false','-C',$repoRoot)+$Arguments)){$start.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{if(-not$process.WaitForExit(30000)){$process.Kill($true);$process.WaitForExit();throw 'Read-only product Git timeout.'};[Threading.Tasks.Task]::WaitAll($stdout,$stderr);if($process.ExitCode-ne0){throw 'Read-only product Git metadata failed.'};return $stdout.Result}finally{$process.Dispose()}
}
function Source-State {
    $product='c4010d8c45eecadb3c51272334ceb4169e27b6ce'
    if((Git-Product @('status','--porcelain=v1','--untracked-files=normal')).Trim()){throw 'ArchSift checkout is not clean.'}
    [void](Git-Product @('merge-base','--is-ancestor',$product,'HEAD'))
    $diff=Git-Product @('diff','--name-only',$product,'HEAD')
    $changes=@(($diff -split '[\r\n]+')|Where-Object {$_})
    foreach($path in $changes){if($path-notmatch'^docs/(acceptance|plans)/'){throw 'Product inputs changed after frozen product commit.'}}
    return [ordered]@{head=(Git-Product @('rev-parse','HEAD')).Trim();productCommit=$product;documentationOnlyChangeCount=$changes.Count;clean=$true}
}
function Assets-State {
    $expected=[ordered]@{'archsift-0.5.0-win-x64.zip'=@(89042456,'9071b35036ea8d7b0a52e00bbd078b4de70ccc5b4a661a3f0daf10f5e6f9de85');'ArchSift.Setup.exe'=@(74071838,'f3eb9e4d166efc7c3ddb9bb738571016807f795b0cc68aeb6cb4f2668c5fad07');'SHA256SUMS.txt'=@(178,'142518db350fe68a187676487342b7115b554d1419e46be72685ac2a0463a9b7');'acceptance.json'=@(1585,'3bd7c99ae930e923f4693e69efa3895a7182ab7a47ae7e3a3d423ab8bf3cf614')}
    foreach($name in $expected.Keys){$path=Safe-Path (Join-Path $AssetRoot $name);if(([IO.FileInfo]$path).Length-ne$expected[$name][0]-or(Hash-File $path)-cne$expected[$name][1]){throw 'Candidate asset differs from reviewed bytes.'}}
    return [ordered]@{fileCount=4;allExpectedHashesEqual=$true}
}
function Process-State {
    $running=@(Get-Process -Name 'archsift','ArchSift.Web','ArchSift.Setup' -ErrorAction SilentlyContinue)
    if($running.Count-ne0){throw 'ArchSift native process remains; inspect owner before release.'}
    $known=@(18356,40888,34560)
    $live=@($known|Where-Object {Get-Process -Id $_ -ErrorAction SilentlyContinue})
    $listening=@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object {$_.OwningProcess-in$known})
    if($live.Count-ne0-or$listening.Count-ne0){throw 'Recorded Web PID/process/listener remains or was reused; review before release.'}
    return [ordered]@{archsiftProcessCount=0;recordedWebPidAliveCount=0;recordedWebPidListenerCount=0}
}
function Operations-Complete {
    $expected=@('77534c6576a1405d93fffb69031f3893','6efbaf703da244aa8ee24f175fa6952e','8d04aec04cf043d1acd9468b45b901a2')
    $directories=@(Get-ChildItem -LiteralPath (Safe-Path (Join-Path $copyRoot 'operations')) -Directory -Force)
    if($directories.Count-ne3){throw 'External copy operation count changed.'}
    foreach($item in $directories){
        if($item.Name-notin$expected){throw 'Unexpected external copy operation.'}
        $journal=Get-Content -LiteralPath (Safe-Path (Join-Path $item.FullName 'journal.json')) -Raw|ConvertFrom-Json
        $receipt=Get-Content -LiteralPath (Safe-Path (Join-Path $item.FullName 'receipt.json')) -Raw|ConvertFrom-Json
        if($journal.phase-cne'completed'-or$receipt.id-cne$item.Name-or$receipt.outcome-cne'success'){throw 'Pending/failed external copy operation.'}
    }
    return $true
}
try{
    if((Hash-File (Join-Path $RollbackRun 'result.json'))-cne'ae0d3488f7b3d98a2f676abc7f9c937f0d982fc1e248cf7da30896aa9cc52aec'-or(Hash-File (Join-Path $RollbackRun 'before.json'))-cne'7de8faa1dd61042a2facfb0c2e8e5e872b759c2cf8484b804fc29da73ac05976'-or(Hash-File (Join-Path $RollbackRun 'rollback-receipt.json'))-cne'575471ea15cb25db3f527a40e3d3e1adedebdca288040cd497fcbc6ab25c7b1d'-or(Hash-File (Join-Path $RollbackRun 'rollback-inspect.json'))-cne'13d224520a8655aa7b8f6dd7277e72b3e7908118d021d006068eea372d334f97'){throw 'Reviewed rollback evidence changed.'}
    $previous=Get-Content -LiteralPath (Safe-Path (Join-Path $RollbackRun 'result.json')) -Raw|ConvertFrom-Json
    $previousBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $RollbackRun 'before.json')) -Raw|ConvertFrom-Json
    $receipt=Get-Content -LiteralPath (Safe-Path (Join-Path $RollbackRun 'rollback-receipt.json')) -Raw|ConvertFrom-Json
    $inspect=Get-Content -LiteralPath (Safe-Path (Join-Path $RollbackRun 'rollback-inspect.json')) -Raw|ConvertFrom-Json
    if($previous.status-cne'external-copy-rollback-passed'-or$previous.runRoot-cne$RollbackRun-or$previous.copyRoot-cne$copyRoot-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.existing040StateEqual-or-not$previous.existingReportsEqual-or-not$previous.copyUserStateEqual-or-not$previous.upgradeBackupEqual-or-not$previous.rollbackApplied-or$previous.analysisOrBuildPerformed-or$previous.nextGate-cne'operator-final-audit'-or$receipt.id-cne$previous.rollbackReceiptId-or$receipt.operation-cne'rollback'-or$receipt.outcome-cne'success'-or$inspect.status-cne'verified'-or$inspect.installation.selectedVersion-cne'0.4.0'-or$inspect.installation.lastOperation-cne$receipt.id-or$inspect.selectionSha256-cne$previous.selectionAfterSha256-or$inspect.pendingOperations.Count-ne0){throw 'Previous rollback gate is not ready.'}
    $targetBefore=Target-State;$existingBefore=Existing-State;$reportsBefore=Original-Reports;$copyBefore=Copy-State;$newBefore=NewInstall-State;$sourceBefore=Source-State;$assetsBefore=Assets-State;$processBefore=Process-State
    if((Hash-Json $targetBefore)-cne(Hash-Json $previousBefore.target)-or(Hash-Json $existingBefore)-cne(Hash-Json $previousBefore.existing)-or(Hash-Json $reportsBefore)-cne(Hash-Json $previousBefore.originalReports)-or$copyBefore.selectionSha256-cne'5a17bee6f33bc73dd7806cb77df58e011e606eb7cc4e04c446cf5821d326c091'-or$copyBefore.configSha256-cne$previousBefore.copy.configSha256-or$copyBefore.library.sha256-cne$previousBefore.copy.library.sha256-or$copyBefore.library.fileCount-ne2-or$copyBefore.reports.sha256-cne$previousBefore.copy.reports.sha256-or$copyBefore.reports.fileCount-ne6-or$copyBefore.old.manifestSha256-cne$previousBefore.copy.oldManifestSha256-or$copyBefore.new.manifestSha256-cne$previousBefore.copy.newManifestSha256-or$newBefore.selectionSha256-cne'bcf65d2082de735f980ce7ae526fe9e937de4c7ef31ed8aa2f709d155c831a41'-or$newBefore.configSha256-cne'a88140c84d003303d66e904a62eaf8b08e9012d0f40f2f8ad236bd8d0d271f2f'-or-not(Backup-Equal)-or-not(Operations-Complete)){throw 'Final audit identity differs from reviewed state.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore;originalReports=$reportsBefore;copy=$copyBefore;newInstall=$newBefore;source=$sourceBefore;assets=$assetsBefore;process=$processBefore;upgradeBackupEqual=$true;operationsComplete=$true}|ConvertTo-Json -Depth 10))
    $completed=$true
}catch{
    [IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message}|ConvertTo-Json));throw
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$auditErrors=[Collections.Generic.List[string]]::new();$checks=[ordered]@{target=$null;existing=$null;originalReports=$null;copy=$null;newInstall=$null;source=$null;assets=$null;process=$null;backup=$null;operations=$null}
    foreach($entry in @(@('target',$targetBefore,{Target-State}),@('existing',$existingBefore,{Existing-State}),@('originalReports',$reportsBefore,{Original-Reports}),@('copy',$copyBefore,{Copy-State}),@('newInstall',$newBefore,{NewInstall-State}),@('source',$sourceBefore,{Source-State}),@('assets',$assetsBefore,{Assets-State}),@('process',$processBefore,{Process-State}))){
        if($null-ne$entry[1]){try{$checks[$entry[0]]=(Hash-Json (& $entry[2]))-ceq(Hash-Json $entry[1])}catch{$checks[$entry[0]]=$false;$auditErrors.Add($_.Exception.Message)}}
    }
    if($completed){try{$checks.backup=Backup-Equal;$checks.operations=Operations-Complete}catch{$checks.backup=$false;$checks.operations=$false;$auditErrors.Add($_.Exception.Message)}}
    $passed=$completed-and$hostEqual-and(@($checks.Values|Where-Object {$_ -cne $true}).Count-eq0)
    $result=[ordered]@{status=$(if($passed){'final-operator-audit-passed'}else{'stop'});runRoot=$run;productSourceFrozen=$checks.source;candidateAssetsEqual=$checks.assets;targetStateEqual=$checks.target;existing040StateEqual=$checks.existing;existingReportsEqual=$checks.originalReports;externalCopyEqual=$checks.copy;newInstallationEqual=$checks.newInstall;noArchSiftProcessOrRecordedListener=$checks.process;upgradeBackupEqual=$checks.backup;operationsComplete=$checks.operations;hostStateEqual=$hostEqual;analysisOrBuildPerformed=$false;nextGate=$(if($passed){'release-readiness-review'}else{$null})}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors.ToArray()));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or(@($checks.Values|Where-Object {$_ -ceq $false}).Count-gt0)){throw 'Safety drift/audit failure; preserve evidence and stop. No repair.'}
}
