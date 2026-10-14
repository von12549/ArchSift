param(
  [string]$LabRoot = 'D:\ArchSift-lab',
  [Parameter(Mandatory)][string]$TargetPackageSource,
  [string]$OfficialSarifSchema
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Test-InProjectIp03.ps1') -LabRoot $LabRoot -TargetPackageSource $TargetPackageSource -OfficialSarifSchema $OfficialSarifSchema -ExpectFullUnavailable:$false
$ip05Id=[Guid]::NewGuid().ToString('N')
$rulesPath=Join-Path $fixture '.archsift/rules/architecture.json'
$originalRules=[IO.File]::ReadAllBytes($rulesPath)
try{
  [IO.File]::WriteAllText($rulesPath,$rulesTemplate.Replace('__TARGET__','C'))
  Run 'gate-full-default' @($installed,'gate','--project',$projectConfig,'--evidence','full')
  $full=Get-Content (Join-Path $run 'gate-full-default.stdout.log') -Raw|ConvertFrom-Json
  $defaultBundle=Join-Path $fixture ".archsift/evidence/$($full.runId)"
  if($full.gate.exitCode -ne 0 -or $full.evidenceGrade -ne 'full' -or -not $full.binding.sourceBound -or
     $full.rules.Count -ne $passed.rules.Count -or -not [IO.File]::Exists((Join-Path $defaultBundle 'manifest.json'))){throw 'Default full gate is incomplete.'}
  for($i=0;$i -lt $full.rules.Count;$i++){
    if($full.rules[$i].ruleKey -ne $passed.rules[$i].ruleKey -or $full.rules[$i].status -ne $passed.rules[$i].status){throw 'Full and daily rule results differ.'}
  }
  Run 'verify-full-default' @($installed,'evidence-verify','--bundle',$defaultBundle)
  $manifest=Get-Content (Join-Path $defaultBundle 'manifest.json') -Raw|ConvertFrom-Json
  $closure=Get-Content (Join-Path $defaultBundle 'closure.json') -Raw|ConvertFrom-Json
  if($manifest.runId -ne $full.runId -or $manifest.productVersion -ne '0.1.0-dev' -or
     $manifest.rulesetVersions.Count -lt 1 -or $manifest.files.Count -lt 200 -or
     $closure.Count -lt 100 -or
     @($closure|Where-Object {$_.logicalPath -like 'closure/*'}).Count -lt 1){throw 'Default full manifest lacks raw compiler dependencies.'}
  $sourceFile=Join-Path $defaultBundle 'build/source/B/B.cs'
  $originalSource=[IO.File]::ReadAllBytes($sourceFile)
  try{
    [IO.File]::AppendAllText($sourceFile,"`n// tampered evidence`n")
    RunExpectedFailure 'reject-full-tamper' @($installed,'evidence-verify','--bundle',$defaultBundle) 5
  }finally{[IO.File]::WriteAllBytes($sourceFile,$originalSource)}
  Run 'verify-full-restored' @($installed,'evidence-verify','--bundle',$defaultBundle)
  $archiveDir=Join-Path $lab 'archives'
  [void][IO.Directory]::CreateDirectory($archiveDir)
  $archivePath=Join-Path $archiveDir "inproject-ip05-$ip05Id.zip"
  Run 'archive-full' @($installed,'evidence-archive','--bundle',$defaultBundle,'--archive',$archivePath)
  Run 'verify-full-archive' @($installed,'evidence-verify-archive','--archive',$archivePath)
  $unsafeRestore=Join-Path $fixture 'B/restored-evidence'
  RunExpectedFailure 'reject-business-restore' @($installed,'evidence-restore','--archive',$archivePath,'--destination',$unsafeRestore) 5
  if([IO.Directory]::Exists($unsafeRestore)){throw 'Rejected evidence restore wrote into business source.'}
  $restoredBundle=Join-Path $lab "evidence/inproject-ip05-restored-$ip05Id"
  Run 'restore-full-archive' @($installed,'evidence-restore','--archive',$archivePath,'--destination',$restoredBundle)
  Run 'verify-restored-full' @($installed,'evidence-verify','--bundle',$restoredBundle)
  $archiveTamper=Join-Path $archiveDir "inproject-ip05-tamper-$ip05Id.zip"
  [IO.File]::Copy($archivePath,$archiveTamper)
  $tamperedBytes=[IO.File]::ReadAllBytes($archiveTamper)
  $tamperedBytes[$tamperedBytes.Length-22]=$tamperedBytes[$tamperedBytes.Length-22] -bxor 1
  [IO.File]::WriteAllBytes($archiveTamper,$tamperedBytes)
  RunExpectedFailure 'reject-archive-tamper' @($installed,'evidence-verify-archive','--archive',$archiveTamper) 5
  Write-Output "full-default run=$($full.runId) files=$($manifest.files.Count) source-bound=True host-state-equal=$((HostHash)-ceq$baseline)"

  $external=Join-Path $lab "evidence/inproject-ip05-$ip05Id"
  $settingsPath=Join-Path $fixture '.archsift/state/local-settings.json'
  $settings=Get-Content $settingsPath -Raw|ConvertFrom-Json
  $settings|Add-Member -NotePropertyName evidenceRoot -NotePropertyValue $external
  [IO.File]::WriteAllText($settingsPath,(ConvertTo-Json -InputObject $settings -Compress))
  Run 'gate-full-external' @($installed,'gate','--project',$projectConfig,'--evidence','full')
  $outside=Get-Content (Join-Path $run 'gate-full-external.stdout.log') -Raw|ConvertFrom-Json
  $outsideBundle=Join-Path $external $outside.runId
  if($outside.gate.exitCode -ne 0 -or $outside.evidenceGrade -ne 'full' -or
     -not [IO.File]::Exists((Join-Path $outsideBundle 'manifest.json'))){throw 'External full bundle is missing.'}
  Run 'verify-full-external' @($installed,'evidence-verify','--bundle',$outsideBundle)
  Run 'status-full-current' @($installed,'status','--project',$projectConfig)
  $current=Get-Content (Join-Path $run 'status-full-current.stdout.log') -Raw|ConvertFrom-Json
  if($current.runId -ne $outside.runId -or $current.evidenceGrade -ne 'full' -or $current.freshness -ne 'current'){throw 'External full task identity is stale.'}
  Write-Output "full-external run=$($outside.runId) host-state-equal=$((HostHash)-ceq$baseline)"

  $outsideArchive=Join-Path $archiveDir "inproject-ip05-external-$ip05Id.zip"
  Run 'archive-current-full' @($installed,'evidence-archive','--bundle',$outsideBundle,'--archive',$outsideArchive)
  Run 'prune-current-preview' @($installed,'evidence-prune','--project',$projectConfig,'--run',$outside.runId,'--bundle',$outsideBundle,'--archive',$outsideArchive)
  $currentPreview=Get-Content (Join-Path $run 'prune-current-preview.stdout.log') -Raw|ConvertFrom-Json
  if($currentPreview.canDelete -or 'current' -notin $currentPreview.protectedBy){throw 'Current full evidence was not protected.'}
  RunExpectedFailure 'reject-prune-current' @($installed,'evidence-prune','--project',$projectConfig,'--run',$outside.runId,'--bundle',$outsideBundle,'--archive',$outsideArchive,'--apply','true') 5
  Run 'pin-default-full' @($installed,'pin','--project',$projectConfig,'--run',$full.runId,'--value','true')
  Run 'prune-pinned-preview' @($installed,'evidence-prune','--project',$projectConfig,'--run',$full.runId,'--bundle',$defaultBundle,'--archive',$archivePath)
  $pinnedPreview=Get-Content (Join-Path $run 'prune-pinned-preview.stdout.log') -Raw|ConvertFrom-Json
  if($pinnedPreview.canDelete -or 'pinned' -notin $pinnedPreview.protectedBy){throw 'Pinned full evidence was not protected.'}
  Run 'unpin-default-full' @($installed,'pin','--project',$projectConfig,'--run',$full.runId,'--value','false')
  Run 'prune-default-full' @($installed,'evidence-prune','--project',$projectConfig,'--run',$full.runId,'--bundle',$defaultBundle,'--archive',$archivePath,'--apply','true')
  if([IO.Directory]::Exists($defaultBundle)){throw 'Archived full evidence was not pruned.'}
  Run 'verify-pruned-archive' @($installed,'evidence-verify-archive','--archive',$archivePath)
  Run 'status-archived-full' @($installed,'status','--project',$projectConfig,'--run',$full.runId)
  $archivedTask=Get-Content (Join-Path $run 'status-archived-full.stdout.log') -Raw|ConvertFrom-Json
  if($archivedTask.materialsState -ne 'archived' -or $archivedTask.exitCode -ne 0){throw 'Archived full history lost its gate decision.'}

  Run 'pin-daily-pass' @($installed,'pin','--project',$projectConfig,'--run',$passed.runId,'--value','true')
  Run 'pin-daily-failure' @($installed,'pin','--project',$projectConfig,'--run',$rejected.runId,'--value','true')
  $project=Get-Content $projectConfig -Raw|ConvertFrom-Json
  $project.retention.dailyDays=1; $project.retention.dailyMaxRuns=1
  [IO.File]::WriteAllText($projectConfig,(ConvertTo-Json -InputObject $project -Depth 20))
  $unknown=Join-Path $fixture '.archsift/reports/unknown-unregistered'
  [void][IO.Directory]::CreateDirectory($unknown)
  Run 'retention-preview' @($installed,'retention','--project',$projectConfig)
  $preview=Get-Content (Join-Path $run 'retention-preview.stdout.log') -Raw|ConvertFrom-Json
  if($preview.selectedRuns -lt 1 -or -not $preview.overBudget -or
     -not($preview.limitations -match 'Unregistered report')){throw 'Retention preview did not expose the protected over-budget conflict.'}
  Run 'retention-apply' @($installed,'retention','--project',$projectConfig,'--apply','true')
  if(-not [IO.Directory]::Exists((Join-Path $fixture ".archsift/reports/$($passed.runId)")) -or
     -not [IO.Directory]::Exists((Join-Path $fixture ".archsift/reports/$($rejected.runId)")) -or
     -not [IO.Directory]::Exists((Join-Path $fixture ".archsift/reports/$($outside.runId)")) -or
     -not [IO.Directory]::Exists($unknown) -or
     [IO.Directory]::Exists((Join-Path $fixture ".archsift/reports/$($violated.runId)"))){throw 'Retention deleted protected material or retained an unprotected old run.'}
  Run 'status-compacted' @($installed,'status','--project',$projectConfig,'--run',$violated.runId)
  $compacted=Get-Content (Join-Path $run 'status-compacted.stdout.log') -Raw|ConvertFrom-Json
  if($compacted.materialsState -ne 'compacted' -or $compacted.exitCode -ne 2){throw 'Compact history lost the original violation or material state.'}
  Run 'verify-full-after-retention' @($installed,'evidence-verify','--bundle',$outsideBundle)
  Write-Output "retention selected=$($preview.selectedRuns) pinned-preserved=True full-preserved=True unknown-preserved=True host-state-equal=$((HostHash)-ceq$baseline)"

  Run 'worktree-export-preview' @($installed,'worktree-export','--project',$projectConfig)
  $exportPreview=Get-Content (Join-Path $run 'worktree-export-preview.stdout.log') -Raw|ConvertFrom-Json
  if($exportPreview.limitations.Count -ne 0 -or
     @($exportPreview.sources|Where-Object {$_.kind -eq 'full-evidence'}).Count -lt 1 -or
     @($exportPreview.sources|Where-Object {$_.kind -eq 'archive'}).Count -lt 1){throw 'Pre-removal export preview omitted full or archived material.'}
  $exportDirectory=Join-Path $archiveDir "inproject-worktree-export-$ip05Id"
  Run 'worktree-export' @($installed,'worktree-export','--project',$projectConfig,'--destination',$exportDirectory)
  Run 'worktree-export-verify' @($installed,'worktree-export-verify','--directory',$exportDirectory)
  $exportConfig=Join-Path $exportDirectory 'configuration/project.json'
  $savedExport=[IO.File]::ReadAllBytes($exportConfig)
  try{
    [IO.File]::AppendAllText($exportConfig,"`n// changed after export`n")
    RunExpectedFailure 'reject-export-tamper' @($installed,'worktree-export-verify','--directory',$exportDirectory) 5
  }finally{[IO.File]::WriteAllBytes($exportConfig,$savedExport)}
  Run 'worktree-export-verify-restored' @($installed,'worktree-export-verify','--directory',$exportDirectory)
  Write-Output "worktree-export sources=$($exportPreview.sources.Count) host-state-equal=$((HostHash)-ceq$baseline)"
}finally{[IO.File]::WriteAllBytes($rulesPath,$originalRules)}
