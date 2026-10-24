param([Parameter(Mandatory)][string]$PersistentBaselinePath,[Parameter(Mandatory)][string]$ExpectedPersistentBaselineSha256,[Parameter(Mandatory)][string]$LaunchPolicyPath,[Parameter(Mandatory)][string]$CandidatePath,[Parameter(Mandatory)][string]$ApprovalPath)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'SessionHostBaseline.ps1')
$original=Read-ArchSiftBaselineDocument $PersistentBaselinePath
$candidate=Read-ArchSiftBaselineDocument $CandidatePath
$before=Get-ArchSiftSessionSnapshot $LaunchPolicyPath
# This provisional value is never written unless the operator explicitly types the exact digest.
$approval=@{schemaVersion=1;kind='archsift-session-confirmation';scope='baseline-only-no-operation-authorization';sessionId=$candidate.value.sessionId;candidateSha256=$candidate.sha256;confirmedBy=[Environment]::UserName;confirmedUtc=[DateTimeOffset]::UtcNow.ToString('O');confirmationId=[Guid]::NewGuid().ToString('D')}
$check=Compare-ArchSiftSessionBaseline -Original $original.value -Snapshot $before -Candidate $candidate.value -Approval $approval -OriginalFileSha256 $original.sha256 -ExpectedOriginalFileSha256 $ExpectedPersistentBaselineSha256 -CandidateFileSha256 $candidate.sha256
if(-not $check.baselineMatches){throw ('Candidate cannot be confirmed: '+$check.status)}
Write-Output ('Candidate SHA-256: '+$candidate.sha256)
Write-Output 'Confirming a baseline does not authorize Guard/dotnet execution, credentials or publication.'
$answer=Read-Host 'Type CONFIRM followed by a space and the exact candidate SHA-256 after human review'
if($answer -cne ('CONFIRM '+$candidate.sha256)){throw 'Human confirmation not supplied; nothing approved.'}
$after=Get-ArchSiftSessionSnapshot $LaunchPolicyPath
if(-not (Compare-ArchSiftHostOperation $before $after).unchanged){throw 'Host changed during confirmation; no approval written.'}
if((Read-ArchSiftBaselineDocument $CandidatePath).sha256 -cne $candidate.sha256 -or (Read-ArchSiftBaselineDocument $PersistentBaselinePath).sha256 -cne $original.sha256){throw 'Evidence changed during confirmation; nothing approved.'}
$approval.confirmedUtc=[DateTimeOffset]::UtcNow.ToString('O')
Write-ArchSiftNewBaselineDocument $ApprovalPath $approval
@{status='baseline-confirmed-only';sessionId=$approval.sessionId;operationAuthorized=$false}|ConvertTo-Json
