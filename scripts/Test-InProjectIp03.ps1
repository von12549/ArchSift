param(
  [string]$LabRoot = 'D:\ArchSift-lab',
  [Parameter(Mandatory)][string]$TargetPackageSource,
  [string]$OfficialSarifSchema,
  [bool]$ExpectFullUnavailable = $false
)
$ErrorActionPreference='Stop'
$sarifSchema = if($OfficialSarifSchema){[IO.Path]::GetFullPath($OfficialSarifSchema)}else{Join-Path $LabRoot 'evidence/sarif-2.1.0/sarif-schema-2.1.0.json'}
if(-not [IO.File]::Exists($sarifSchema)){throw 'Official SARIF 2.1.0 schema is required for IP04 projection verification.'}
. (Join-Path $PSScriptRoot 'Test-InProjectIp02.ps1') -LabRoot $LabRoot -TargetPackageSource $TargetPackageSource
$projectConfig=Join-Path $fixture '.archsift/project.json'
$rulesPath=Join-Path $fixture '.archsift/rules/architecture.json'
$originalRules=[IO.File]::ReadAllBytes($rulesPath)
$rulesTemplate=@'
{
  "schemaVersion": 1,
  "id": "ip03-fixture",
  "version": "1.0.0",
  "description": "Six-rule-type synthetic InProject acceptance fixture.",
  "rules": [
    {"id":"project-ref","type":"project-reference","enabled":true,"scope":{"kind":"project","match":"glob","value":"**"},"parameters":{"source":{"kind":"project","match":"exact","value":"B/B.csproj"},"target":{"kind":"project","match":"exact","value":"D/D.csproj"}},"severity":"warning","reason":"Synthetic gate"},
    {"id":"graph","type":"graph-integrity","enabled":true,"scope":{"kind":"project","match":"glob","value":"**"},"parameters":{"check":"resolved-references"},"severity":"warning","reason":"Synthetic gate"},
    {"id":"tfm","type":"target-framework","enabled":true,"scope":{"kind":"project","match":"glob","value":"**"},"parameters":{"allowedFrameworks":["net8.0","net9.0","net10.0"]},"severity":"warning","reason":"Synthetic gate"},
    {"id":"nuget","type":"nuget-denylist","enabled":true,"scope":{"kind":"project","match":"glob","value":"**"},"parameters":{"forbiddenPackageIds":["Forbidden.Package"]},"severity":"warning","reason":"Synthetic gate"},
    {"id":"naming","type":"naming","enabled":true,"scope":{"kind":"project","match":"glob","value":"**"},"parameters":{"subjectKind":"project","requiredName":{"match":"glob","value":"*"}},"severity":"warning","reason":"Synthetic gate"},
    {"id":"type-naming","type":"naming","enabled":true,"scope":{"kind":"type","match":"glob","value":"*"},"parameters":{"subjectKind":"type","requiredName":{"match":"glob","value":"*"}},"severity":"warning","reason":"Synthetic gate"},
    {"id":"type-dep","type":"type-dependency","enabled":true,"scope":{"kind":"namespace","match":"glob","value":"*"},"parameters":{"source":{"kind":"namespace","match":"exact","value":"B"},"forbiddenTarget":{"kind":"namespace","match":"exact","value":"__TARGET__"}},"severity":"error","reason":"Synthetic gate"}
  ],
  "exceptions": []
}
'@
try{
  [IO.File]::WriteAllText($rulesPath,$rulesTemplate.Replace('__TARGET__','C'))
  Run 'gate-pass' @($installed,'gate','--project',$projectConfig)
  if(-not(Test-Json -LiteralPath (Join-Path $run 'gate-pass.stdout.log') -SchemaFile (Join-Path $PSScriptRoot '../schemas/inproject/gate-result.schema.json'))){throw 'Passing gate result does not match the versioned schema.'}
  $passed=Get-Content (Join-Path $run 'gate-pass.stdout.log') -Raw | ConvertFrom-Json
  if($passed.gate.exitCode -ne 0 -or $passed.coverage.requiredRules.Count -ne 7 -or
     $passed.coverage.executedRules.Count -ne 7 -or $passed.rules.Count -ne 7 -or
     @($passed.rules|Where-Object {$_.classification -eq 'B' -and $_.discovered -eq 1 -and $_.finished -eq 1}).Count -ne 2 -or
     -not $passed.binding.sourceBound){throw 'Six-type, seven-rule passing gate coverage is incomplete.'}
  Write-Output "gate-pass run=$($passed.runId) rules=$($passed.rules.Count) source-bound=$($passed.binding.sourceBound)"
  $passReport=Join-Path $fixture ".archsift/reports/$($passed.runId)"
  if(-not(Test-Json -LiteralPath (Join-Path $passReport 'gate-result.sarif') -SchemaFile $sarifSchema)){throw 'Passing SARIF projection is invalid.'}
  if(-not([IO.File]::Exists((Join-Path $passReport 'gate-result.html')) -and [IO.File]::Exists((Join-Path $passReport 'daily-index.json')))){throw 'Daily projections or index are missing.'}
  $index=Get-Content (Join-Path $passReport 'daily-index.json') -Raw|ConvertFrom-Json
  if($index.runId -ne $passed.runId -or $index.evidenceGrade -ne 'daily' -or $index.files.Count -lt 10){throw 'Daily index identity or coverage is incomplete.'}
  foreach($item in $index.files){
    $material=Join-Path $passReport $item.path
    if(-not [IO.File]::Exists($material) -or (Get-FileHash -LiteralPath $material -Algorithm SHA256).Hash.ToLowerInvariant() -cne $item.sha256){throw "Daily indexed material differs: $($item.path)"}
  }
  Run 'status-after-pass' @($installed,'status','--project',$projectConfig)
  $task=Get-Content (Join-Path $run 'status-after-pass.stdout.log') -Raw|ConvertFrom-Json
  if($task.runId -ne $passed.runId -or $task.phase -ne 'completed' -or $task.exitCode -ne 0 -or $task.freshness -ne 'current'){throw 'Current task does not match passing gate.'}
  [IO.File]::WriteAllText($rulesPath,$rulesTemplate.Replace('__TARGET__','A'))
  Run 'status-pass-stale' @($installed,'status','--project',$projectConfig,'--run',$passed.runId)
  $old=Get-Content (Join-Path $run 'status-pass-stale.stdout.log') -Raw|ConvertFrom-Json
  if($old.freshness -ne 'stale'){throw 'Rule change did not stale the historical passing gate.'}
  RunExpectedFailure 'gate-violation' @($installed,'gate','--project',$projectConfig) 2
  if(-not(Test-Json -LiteralPath (Join-Path $run 'gate-violation.stdout.log') -SchemaFile (Join-Path $PSScriptRoot '../schemas/inproject/gate-result.schema.json'))){throw 'Violation gate result does not match the versioned schema.'}
  $violated=Get-Content (Join-Path $run 'gate-violation.stdout.log') -Raw | ConvertFrom-Json
  if($violated.gate.reason -ne 'violation' -or $violated.findings.Count -lt 1 -or
     @($violated.rules|Where-Object {$_.ruleKey -eq 'ip03-fixture@1.0.0/type-dep' -and $_.status -eq 'violation'}).Count -ne 1){
    throw 'Real ArchUnitNET violation was not preserved as a distinct finding.'
  }
  Write-Output "gate-violation run=$($violated.runId) findings=$($violated.findings.Count) exit=$($violated.gate.exitCode)"
  $violationReport=Join-Path $fixture ".archsift/reports/$($violated.runId)"
  if(-not(Test-Json -LiteralPath (Join-Path $violationReport 'gate-result.sarif') -SchemaFile $sarifSchema)){throw 'Violation SARIF projection is invalid.'}
  Run 'status-after-violation' @($installed,'status','--project',$projectConfig)
  $task=Get-Content (Join-Path $run 'status-after-violation.stdout.log') -Raw|ConvertFrom-Json
  if($task.runId -ne $violated.runId -or $task.phase -ne 'completed' -or $task.exitCode -ne 2){throw 'Current task does not show the violation.'}
  if([IO.File]::Exists($entryMarker)){throw 'Target module initializer executed by Worker or xUnit.'}
  Write-Output 'target-module-initializer-after-gates=False'
  if($ExpectFullUnavailable){RunExpectedFailure 'gate-full-unavailable' @($installed,'gate','--project',$projectConfig,'--evidence','full') 5}
  $testSource=Join-Path $fixture '.archsift/tests/ArchitectureRules.cs'
  $originalTestSource=[IO.File]::ReadAllBytes($testSource)
  try{
    [IO.File]::AppendAllText($testSource,"`n// unauthorized test change`n")
    RunExpectedFailure 'gate-reject-test-tamper' @($installed,'gate','--project',$projectConfig) 5
    $rejected=Get-Content (Join-Path $run 'gate-reject-test-tamper.stdout.log') -Raw|ConvertFrom-Json
    if($rejected.phase -ne 'failed' -or $rejected.exitCode -ne 5 -or $rejected.runId -eq $violated.runId){throw 'Failed gate did not replace the current task.'}
    $failureReport=Join-Path $fixture ".archsift/reports/$($rejected.runId)/gate-result.json"
    if(-not(Test-Json -LiteralPath $failureReport -SchemaFile (Join-Path $PSScriptRoot '../schemas/inproject/gate-result.schema.json'))){throw 'Failed gate report does not match the schema.'}
    Run 'status-after-tamper' @($installed,'status','--project',$projectConfig)
    $latest=Get-Content (Join-Path $run 'status-after-tamper.stdout.log') -Raw|ConvertFrom-Json
    if($latest.runId -ne $rejected.runId -or $latest.exitCode -ne 5){throw 'Old success remained the current gate after failure.'}
  }finally{[IO.File]::WriteAllBytes($testSource,$originalTestSource)}
}finally{[IO.File]::WriteAllBytes($rulesPath,$originalRules)}

$before=Get-Content (Join-Path $fixture '.archsift/state/current-task.json') -Raw|ConvertFrom-Json
$start=[Diagnostics.ProcessStartInfo]::new($dotnet)
$start.WorkingDirectory=$fixture; $start.UseShellExecute=$false; $start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
foreach($argument in @($installed,'gate','--project',$projectConfig)){$start.ArgumentList.Add($argument)}
$start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
$start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
$start.Environment['NUGET_PACKAGES']=Join-Path $run 'packages'
$start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
$gateProcess=[Diagnostics.Process]::Start($start)
try{
  $output=$gateProcess.StandardOutput.ReadToEndAsync(); $errors=$gateProcess.StandardError.ReadToEndAsync()
  $active=$null
  for($attempt=0;$attempt -lt 100;$attempt++){
    Start-Sleep -Milliseconds 100
    $current=Get-Content (Join-Path $fixture '.archsift/state/current-task.json') -Raw|ConvertFrom-Json
    if($current.runId -ne $before.runId -and $current.phase -notin @('completed','failed','cancelled')){$active=$current;break}
    if($gateProcess.HasExited){break}
  }
  if(-not $active){throw 'Concurrent gate did not expose an active task.'}
  RunExpectedFailure 'gate-duplicate' @($installed,'gate','--project',$projectConfig) 4
  $duplicate=Get-Content (Join-Path $run 'gate-duplicate.stdout.log') -Raw|ConvertFrom-Json
  if($duplicate.runId -ne $active.runId){throw 'Duplicate gate did not join the current run.'}
  Run 'gate-cancel-request' @($installed,'cancel','--project',$projectConfig,'--run',$active.runId)
  if(-not $gateProcess.WaitForExit(180000)){throw 'Cancelled gate did not stop its owned process tree.'}
  [Threading.Tasks.Task]::WaitAll($output,$errors)
  [IO.File]::WriteAllText((Join-Path $run 'gate-cancelled.stdout.log'),$output.Result)
  [IO.File]::WriteAllText((Join-Path $run 'gate-cancelled.stderr.log'),$errors.Result)
  if($gateProcess.ExitCode -ne 130 -or (HostHash) -cne $baseline){throw 'Gate cancellation exit or host state is wrong.'}
  Run 'status-after-cancel' @($installed,'status','--project',$projectConfig)
  $cancelled=Get-Content (Join-Path $run 'status-after-cancel.stdout.log') -Raw|ConvertFrom-Json
  if($cancelled.runId -ne $active.runId -or $cancelled.phase -ne 'cancelled' -or $cancelled.exitCode -ne 130){throw 'Cancelled task did not become current.'}
  if(-not(Test-Json -LiteralPath (Join-Path $fixture ".archsift/reports/$($active.runId)/gate-result.json") -SchemaFile (Join-Path $PSScriptRoot '../schemas/inproject/gate-result.schema.json'))){throw 'Cancelled gate report is invalid.'}
  Write-Output "gate-cancelled run=$($active.runId) exit=130 host-state-equal=True"
}finally{
  if(-not $gateProcess.HasExited){$gateProcess.Kill($true);$gateProcess.WaitForExit()}
  $gateProcess.Dispose()
}

$lostId=[Guid]::NewGuid().ToString('N')
$lost=Get-Content (Join-Path $fixture '.archsift/state/current-task.json') -Raw|ConvertFrom-Json
$lost.runId=$lostId; $lost.phase='build'; $lost.revision=1; $lost.finishedUtc=$null
$lost.exitCode=$null; $lost.diagnostic=$null; $lost.resultPath=$null
$lostJson=ConvertTo-Json -InputObject $lost -Depth 10
[IO.File]::WriteAllText((Join-Path $fixture '.archsift/state/current-task.json'),$lostJson)
[IO.File]::WriteAllText((Join-Path $fixture ".archsift/state/tasks/$lostId.json"),$lostJson)
Run 'status-recover-lost-owner' @($installed,'status','--project',$projectConfig)
$recovered=Get-Content (Join-Path $run 'status-recover-lost-owner.stdout.log') -Raw|ConvertFrom-Json
if($recovered.runId -ne $lostId -or $recovered.phase -ne 'failed' -or $recovered.exitCode -ne 3){throw 'Lost task owner reused an old success.'}
if(-not $recovered.resultPath -or -not(Test-Json -LiteralPath (Join-Path $fixture ".archsift/reports/$lostId/gate-result.json") -SchemaFile (Join-Path $PSScriptRoot '../schemas/inproject/gate-result.schema.json'))){throw 'Recovered lost task has no versioned failure report.'}
Write-Output "status-lost-owner run=$lostId exit=3 host-state-equal=$((HostHash)-ceq$baseline)"
