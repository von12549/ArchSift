param(
  [string]$LabRoot = 'D:\ArchSift-lab',
  [Parameter(Mandatory)][string]$TargetPackageSource
)
$ErrorActionPreference='Stop'
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
  [IO.File]::WriteAllText($rulesPath,$rulesTemplate.Replace('__TARGET__','A'))
  RunExpectedFailure 'gate-violation' @($installed,'gate','--project',$projectConfig) 2
  if(-not(Test-Json -LiteralPath (Join-Path $run 'gate-violation.stdout.log') -SchemaFile (Join-Path $PSScriptRoot '../schemas/inproject/gate-result.schema.json'))){throw 'Violation gate result does not match the versioned schema.'}
  $violated=Get-Content (Join-Path $run 'gate-violation.stdout.log') -Raw | ConvertFrom-Json
  if($violated.gate.reason -ne 'violation' -or $violated.findings.Count -lt 1 -or
     @($violated.rules|Where-Object {$_.ruleKey -eq 'ip03-fixture@1.0.0/type-dep' -and $_.status -eq 'violation'}).Count -ne 1){
    throw 'Real ArchUnitNET violation was not preserved as a distinct finding.'
  }
  Write-Output "gate-violation run=$($violated.runId) findings=$($violated.findings.Count) exit=$($violated.gate.exitCode)"
  if([IO.File]::Exists($entryMarker)){throw 'Target module initializer executed by Worker or xUnit.'}
  Write-Output 'target-module-initializer-after-gates=False'
  RunExpectedFailure 'gate-full-unavailable' @($installed,'gate','--project',$projectConfig,'--evidence','full') 5
  $testSource=Join-Path $fixture '.archsift/tests/ArchitectureRules.cs'
  $originalTestSource=[IO.File]::ReadAllBytes($testSource)
  try{
    [IO.File]::AppendAllText($testSource,"`n// unauthorized test change`n")
    RunExpectedFailure 'gate-reject-test-tamper' @($installed,'gate','--project',$projectConfig) 5
  }finally{[IO.File]::WriteAllBytes($testSource,$originalTestSource)}
}finally{[IO.File]::WriteAllBytes($rulesPath,$originalRules)}
