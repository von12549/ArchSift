param([Parameter(Mandatory)][string]$OutputRoot,[switch]$CheckHostedCapture)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'SessionHostBaseline.ps1')
. (Join-Path $PSScriptRoot 'HostState.ps1')
$hostBefore=Get-ArchSiftHostHash
if(Test-Path -LiteralPath $OutputRoot){throw 'Preserve earlier test evidence; use a new output directory.'}
[void][IO.Directory]::CreateDirectory($OutputRoot)
function Copy-Fixture($Value){$Value|ConvertTo-Json -Depth 40|ConvertFrom-Json -AsHashtable}
$sha='a'*64;$other='b'*64
$persistent=@{User=$sha;Machine=$sha;'User/PATH'=$sha;'Machine/PATH'=$sha;'profile/one'=$sha;'profile/two'='absent';'profile/three'='absent';'profile/four'='absent';'Process/PATH'=$other}
$context=@{ownerPid=123;ownerStartedUtc='2026-10-24T00:00:00Z';ownerExecutableSha256=$sha;ownerExecutablePathSha256=$sha;shellExecutableSha256=$sha;shellExecutablePathSha256=$sha;shellVersion='7.4.0';noProfile=$true;workingDirectorySha256=$sha;launchPolicySha256=$sha}
$snapshot=@{schemaVersion=1;persistent=(Copy-Fixture $persistent);context=$context;operation=@{pid=234;startedUtc='2026-10-24T01:00:00Z'};processPath=@{sha256=$sha;characters=12;entryCount=2;entrySha256=@($sha,$other)}}
$candidate=@{schemaVersion=1;kind='archsift-session-candidate';sessionId='11111111-1111-1111-1111-111111111111';persistentBaselineSha256=$sha;snapshot=(Copy-Fixture $snapshot)}
$approval=@{schemaVersion=1;kind='archsift-session-confirmation';scope='baseline-only-no-operation-authorization';sessionId=$candidate.sessionId;candidateSha256=$sha;confirmedBy='SYNTHETIC-TEST-OPERATOR';confirmedUtc='2026-10-24T01:01:00Z';confirmationId='22222222-2222-2222-2222-222222222222'}
$results=[Collections.Generic.List[object]]::new()
function Run-Case([string]$Name,[string]$Expected,[scriptblock]$Change) {
 $caseArguments=@{Original=(Copy-Fixture $persistent);Snapshot=(Copy-Fixture $snapshot);Candidate=(Copy-Fixture $candidate);Approval=(Copy-Fixture $approval);OriginalFileSha256=$sha;ExpectedOriginalFileSha256=$sha;CandidateFileSha256=$sha}
 & $Change $caseArguments | Out-Null
 $r=Compare-ArchSiftSessionBaseline @caseArguments
 if($r.status -cne $Expected -or $r.operationAuthorized -or $r.automaticRepair -or ($r.baselineMatches -ne ($Expected -ceq 'matched'))){throw ('Unexpected gate result: '+$Name+' / '+$r.status)}
 $results.Add(@{name=$Name;status='passed';expected=$Expected})
}
try {
 Run-Case 'confirmed-session-matches-despite-legacy-process-hash' 'matched' {param($a)}
 Run-Case 'no-candidate-or-confirmation' 'session-confirmation-required' {param($a) $a.Candidate=$null;$a.Approval=$null}
 Run-Case 'candidate-alone-is-not-approval' 'session-confirmation-required' {param($a) $a.Approval=$null}
 Run-Case 'missing-candidate-even-with-receipt' 'session-confirmation-required' {param($a) $a.Candidate=$null}
 Run-Case 'original-baseline-file-replaced' 'persistent-baseline-file-changed' {param($a) $a.OriginalFileSha256=$other}
 foreach($key in @('User','Machine','User/PATH','Machine/PATH','profile/one')) {Run-Case ('persisted-drift-'+$key) 'persistent-drift' {param($a) $a.Snapshot.persistent[$key]=$other}}
 Run-Case 'persisted-drift-not-masked-by-missing-approval' 'persistent-drift' {param($a) $a.Snapshot.persistent.User=$other;$a.Approval=$null}
 Run-Case 'missing-profile-identity' 'invalid-input' {param($a) $a.Snapshot.persistent.Remove('profile/four')}
 Run-Case 'wrong-confirmation-digest' 'invalid-confirmation' {param($a) $a.Approval.candidateSha256=$other}
 Run-Case 'tampered-candidate-file' 'invalid-confirmation' {param($a) $a.CandidateFileSha256=$other}
 Run-Case 'wrong-confirmation-session' 'invalid-confirmation' {param($a) $a.Approval.sessionId='33333333-3333-3333-3333-333333333333'}
 Run-Case 'blank-confirmer' 'invalid-confirmation' {param($a) $a.Approval.confirmedBy=''}
 Run-Case 'confirmation-cannot-authorize-operations' 'invalid-confirmation' {param($a) $a.Approval.scope='resume-dotnet'}
 Run-Case 'invalid-confirmation-time' 'invalid-confirmation' {param($a) $a.Approval.confirmedUtc='invalid'}
 Run-Case 'candidate-bound-to-different-persisted-baseline' 'invalid-input' {param($a) $a.Candidate.persistentBaselineSha256=$other}
 Run-Case 'boolean-snapshot-schema-rejected' 'invalid-input' {param($a) $a.Snapshot.schemaVersion=$true}
 Run-Case 'boolean-candidate-schema-rejected' 'invalid-input' {param($a) $a.Candidate.schemaVersion=$true}
 Run-Case 'boolean-confirmation-schema-rejected' 'invalid-confirmation' {param($a) $a.Approval.schemaVersion=$true}
 Run-Case 'empty-session-id-rejected' 'invalid-input' {param($a) $a.Candidate.sessionId=[Guid]::Empty.ToString()}
 Run-Case 'empty-confirmation-id-rejected' 'invalid-confirmation' {param($a) $a.Approval.confirmationId=[Guid]::Empty.ToString()}
 Run-Case 'new-parent-same-PATH' 'execution-context-changed' {param($a) $a.Snapshot.context.ownerPid=456}
 Run-Case 'PID-reused-new-start-time' 'execution-context-changed' {param($a) $a.Snapshot.context.ownerStartedUtc='2026-10-24T02:00:00Z'}
 foreach($key in @('ownerExecutableSha256','ownerExecutablePathSha256','shellExecutableSha256','shellExecutablePathSha256','workingDirectorySha256','launchPolicySha256')) {Run-Case ('changed-context-'+$key) 'execution-context-changed' {param($a) $a.Snapshot.context[$key]=$other}}
 Run-Case 'PID-reuse-with-subsecond-start-difference' 'execution-context-changed' {param($a) $a.Candidate.snapshot.context.ownerStartedUtc='2026-10-24T00:00:00.0000001Z';$a.Snapshot.context.ownerStartedUtc='2026-10-24T00:00:00.0000002Z'}
 Run-Case 'same-start-in-equivalent-timezone' 'matched' {param($a) $a.Candidate.snapshot.context.ownerStartedUtc='2026-10-24T01:00:00+01:00'}
 Run-Case 'unqualified-timezone-is-invalid' 'invalid-input' {param($a) $a.Snapshot.context.ownerStartedUtc='2026-10-24T00:00:00'}
 Run-Case 'profile-loading-not-accepted' 'invalid-input' {param($a) $a.Snapshot.context.noProfile=$false}
 Run-Case 'same-context-PATH-byte-change' 'process-path-drift' {param($a) $a.Snapshot.processPath.sha256=$other}
 Run-Case 'entry-order-not-normalized' 'process-path-drift' {param($a) $a.Snapshot.processPath.entrySha256=@($other,$sha)}
 Run-Case 'PATH-length-not-normalized' 'process-path-drift' {param($a) $a.Snapshot.processPath.characters=13}
 Run-Case 'temporary-entry-not-ignored' 'process-path-drift' {param($a) $a.Snapshot.processPath.entrySha256[0]=$other}
 Run-Case 'entry-count-inconsistent' 'invalid-input' {param($a) $a.Snapshot.processPath.entryCount=3}
 Run-Case 'new-command-process-same-approved-session' 'matched' {param($a) $a.Snapshot.operation.pid=999;$a.Snapshot.operation.startedUtc='2026-10-24T02:00:00Z'}
 foreach($pair in @(@('unchanged','matched'),@('path','process-path-drift'),@('persisted','persistent-drift'),@('operation','operation-context-changed'))) {
  $after=Copy-Fixture $snapshot
  switch($pair[0]){'path'{$after.processPath.sha256=$other};'persisted'{$after.persistent.Machine=$other};'operation'{$after.operation.pid=999}}
  $r=Compare-ArchSiftHostOperation $snapshot $after
  if($r.status -cne $pair[1] -or $r.operationAuthorized){throw 'Operation checkpoint comparison failed.'}
  $results.Add(@{name=('operation-'+$pair[0]);status='passed';expected=$pair[1]})
 }
 $path=Join-Path ([IO.Path]::GetFullPath($OutputRoot)) 'synthetic-new-record.json'
 Write-ArchSiftNewBaselineDocument $path @{synthetic=$true}
 $first=(Read-ArchSiftBaselineDocument $path).sha256
 $rejected=$false;try{Write-ArchSiftNewBaselineDocument $path @{overwrite=$true}}catch{$rejected=$true}
 if(-not $rejected -or (Read-ArchSiftBaselineDocument $path).sha256 -cne $first){throw 'Evidence overwrite was not rejected.'}
 $results.Add(@{name='immutable-existing-record';status='passed';expected='reject-overwrite'})
 if(-not(Test-ArchSiftNoProfileLaunch @('pwsh','-NoProfile','-File','test.ps1'))){throw 'Actual startup NoProfile option rejected.'}
 if(Test-ArchSiftNoProfileLaunch @('pwsh','-Command','script','-NoProfile')){throw 'Script argument must not impersonate startup NoProfile.'}
 $results.Add(@{name='actual-NoProfile-startup-option';status='passed';expected='accept'})
 $results.Add(@{name='NoProfile-after-command-is-not-startup-policy';status='passed';expected='reject'})
 $confirm=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Confirm-SessionHostBaseline.ps1'))
 if($confirm -notmatch 'Read-Host' -or $confirm -notmatch 'CONFIRM ' -or $confirm -match '\[switch\]|-Force'){throw 'Confirmation must remain an explicit operator interaction.'}
 foreach($name in @('SessionHostBaseline.ps1','New-SessionHostBaselineCandidate.ps1','Confirm-SessionHostBaseline.ps1','Test-SessionHostBaseline.ps1')) {
  $file=Join-Path $PSScriptRoot $name;$text=[IO.File]::ReadAllText($file)
  if($text -match '(?i)SetEnvironmentVariable|\bsetx\b|Registry(?:Key)?\s*\.\s*(?:SetValue|CreateSubKey)|\$env:\w+\s*=|Process\]::Start|Start-Process|nuget\s+push|NuGet/login|ACTIONS_ID_TOKEN'){throw 'Forbidden host mutation or execution capability.'}
  $errors=$null;[void][Management.Automation.Language.Parser]::ParseFile($file,[ref]$null,[ref]$errors);if($errors.Count){throw 'Baseline tool syntax invalid.'}
 }
 if($CheckHostedCapture) {
  if($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted'){throw 'Live metadata capture test is restricted to hosted CI; no local candidate is authorized.'}
  $policy=Join-Path ([IO.Path]::GetFullPath($OutputRoot)) 'synthetic-launch-policy.json'
  Write-ArchSiftNewBaselineDocument $policy @{schemaVersion=1;label='hosted-read-only-metadata-test';isolationPolicy='capture-only-no-operation'}
  $first=Get-ArchSiftSessionSnapshot $policy
  $last=Get-ArchSiftSessionSnapshot $policy
  if(-not (Compare-ArchSiftHostOperation $first $last).unchanged){throw 'Hosted metadata snapshot unstable.'}
  $results.Add(@{name='hosted-read-only-context-capture';status='passed';expected='stable-no-candidate-or-approval'})
 }
 $equal=(Get-ArchSiftHostHash) -ceq $hostBefore
 if(-not $equal){throw 'Host drift; stop without repair.'}
 @{status='passed';cases=$results;count=$results.Count;hostEqual=$equal;fixtures='synthetic-only';liveSessionCandidateCreated=$false;liveSessionApproved=$false;guardOrDotnetInvoked=$false}|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $OutputRoot 'result.json') -Encoding utf8NoBOM
 Write-Output ('Passed '+$results.Count+' synthetic session-baseline cases; no live candidate, confirmation, Guard or dotnet.')
} finally {if((Get-ArchSiftHostHash) -cne $hostBefore){throw 'Host drift; stop without repair.'}}
