param([Parameter(Mandatory)][string]$PersistentBaselinePath,[Parameter(Mandatory)][string]$ExpectedPersistentBaselineSha256,[Parameter(Mandatory)][string]$LaunchPolicyPath,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'SessionHostBaseline.ps1')
$original=Read-ArchSiftBaselineDocument $PersistentBaselinePath
if($original.sha256 -cne $ExpectedPersistentBaselineSha256){throw 'Original persisted-baseline file differs; stop without repair.'}
$before=Get-ArchSiftSessionSnapshot $LaunchPolicyPath
if(Get-ArchSiftPersistentDifference $original.value $before.persistent){throw 'Persisted environment/Profile drift; no candidate may be created.'}
$candidate=@{schemaVersion=1;kind='archsift-session-candidate';sessionId=[Guid]::NewGuid().ToString('D');persistentBaselineSha256=$original.sha256;snapshot=$before;createdUtc=[DateTimeOffset]::UtcNow.ToString('O')}
$after=Get-ArchSiftSessionSnapshot $LaunchPolicyPath
if(-not (Compare-ArchSiftHostOperation $before $after).unchanged){throw 'State changed while preparing candidate; stop without repair.'}
Write-ArchSiftNewBaselineDocument $OutputPath $candidate
$document=Read-ArchSiftBaselineDocument $OutputPath
@{status='candidate-only-human-confirmation-required';candidateSha256=$document.sha256;sessionId=$candidate.sessionId;approved=$false;operationAuthorized=$false}|ConvertTo-Json
