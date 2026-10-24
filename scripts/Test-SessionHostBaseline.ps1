param([Parameter(Mandatory)][string]$PersistentBaselinePath,[Parameter(Mandatory)][string]$ExpectedPersistentBaselineSha256,[Parameter(Mandatory)][string]$LaunchPolicyPath,[string]$CandidatePath,[string]$ApprovalPath)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'SessionHostBaseline.ps1')
$original=Read-ArchSiftBaselineDocument $PersistentBaselinePath
$before=Get-ArchSiftSessionSnapshot $LaunchPolicyPath
$candidate=if($CandidatePath){Read-ArchSiftBaselineDocument $CandidatePath}else{$null}
$approval=if($ApprovalPath){Read-ArchSiftBaselineDocument $ApprovalPath}else{$null}
$result=Compare-ArchSiftSessionBaseline -Original $original.value -Snapshot $before -Candidate $(if($candidate){$candidate.value}else{$null}) -Approval $(if($approval){$approval.value}else{$null}) -OriginalFileSha256 $original.sha256 -ExpectedOriginalFileSha256 $ExpectedPersistentBaselineSha256 -CandidateFileSha256 $(if($candidate){$candidate.sha256}else{''})
$after=Get-ArchSiftSessionSnapshot $LaunchPolicyPath
if(-not (Compare-ArchSiftHostOperation $before $after).unchanged){throw 'Host changed during baseline check; stop without repair.'}
if((Read-ArchSiftBaselineDocument $PersistentBaselinePath).sha256 -cne $original.sha256 -or ($candidate -and (Read-ArchSiftBaselineDocument $CandidatePath).sha256 -cne $candidate.sha256) -or ($approval -and (Read-ArchSiftBaselineDocument $ApprovalPath).sha256 -cne $approval.sha256)){throw 'Evidence changed during baseline check.'}
$result|ConvertTo-Json
if(-not $result.baselineMatches){throw ('Baseline gate stopped: '+$result.status+'; no operation authorized.')}
