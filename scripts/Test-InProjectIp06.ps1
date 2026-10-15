param(
  [string]$LabRoot = 'D:\ArchSift-lab',
  [Parameter(Mandatory)][string]$TargetPackageSource,
  [string]$OfficialSarifSchema
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Test-InProjectIp03.ps1') -LabRoot $LabRoot -TargetPackageSource $TargetPackageSource -OfficialSarifSchema $OfficialSarifSchema
$rulesPath=Join-Path $fixture '.archsift/rules/architecture.json'
$originalRules=[IO.File]::ReadAllBytes($rulesPath)
$webProcess=$null
$siblingProcess=$null
try {
  [IO.File]::WriteAllText($rulesPath,$rulesTemplate.Replace('__TARGET__','C'))
  Run 'ip06-cli-parity' @($installed,'gate','--project',$projectConfig)
  $cliResult=Get-Content (Join-Path $run 'ip06-cli-parity.stdout.log') -Raw|ConvertFrom-Json
  if($cliResult.gate.exitCode -ne 0){throw 'CLI parity baseline is not passing.'}
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$fixture; $start.UseShellExecute=$false; $start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
  foreach($argument in @($installed,'ui','--project',$projectConfig,'--port','0')){$start.ArgumentList.Add($argument)}
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
  $start.Environment['NUGET_PACKAGES']=Join-Path $run 'packages'
  $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
  $webProcess=[Diagnostics.Process]::Start($start)
  $errors=$webProcess.StandardError.ReadToEndAsync()
  $line=$webProcess.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(20)).GetAwaiter().GetResult()
  if($line -notmatch '^http://127\.0\.0\.1:[0-9]+/\?session=[0-9a-f]{64}$'){throw "Workbench did not emit a loopback session URL: $line"}
  $session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
  $bootstrap=Invoke-WebRequest -Uri $line -WebSession $session -SkipHttpErrorCheck
  if($bootstrap.StatusCode -ne 200 -or $bootstrap.Content -notmatch '项目架构检查'){throw 'Workbench bootstrap page failed.'}
  $base=([uri]$line).GetLeftPart([UriPartial]::Authority)
  $token=(Invoke-RestMethod -Uri "$base/api/session" -WebSession $session).token
  $headers=@{'Origin'=$base;'X-ArchSift-Session'=$token}
  function PostApi([string]$path,[object]$payload) {
    Invoke-RestMethod -Uri "$base$path" -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' -Body (ConvertTo-Json -InputObject $payload -Compress -Depth 20)
  }
  function WaitTask([string]$previous) {
    for($attempt=0;$attempt -lt 360;$attempt++){
      Start-Sleep -Milliseconds 500
      $state=Invoke-RestMethod -Uri "$base/api/state" -WebSession $session
      if($state.current.runId -ne $previous -and $state.current.phase -in @('completed','failed','cancelled')){return $state.current}
    }
    throw 'Web gate did not finish within 180 seconds.'
  }
  $state=Invoke-RestMethod -Uri "$base/api/state" -WebSession $session
  if($state.current.runId -ne $cliResult.runId -or $state.current.exitCode -ne 0 -or
     $state.worktreeId -ne $cliResult.worktreeId){throw 'Web current state differs from CLI.'}
  $unsafe=Invoke-WebRequest -Uri "$base/api/state" -SkipHttpErrorCheck
  if($unsafe.StatusCode -ne 403){throw 'Unauthenticated state request was accepted.'}
  $cross=Invoke-WebRequest -Uri "$base/api/gate" -Method Post -WebSession $session -SkipHttpErrorCheck -ContentType 'application/json' -Headers @{'Origin'='http://evil.invalid';'X-ArchSift-Session'=$token} -Body '{"evidenceGrade":"daily"}'
  if($cross.StatusCode -ne 403){throw 'Cross-origin gate request was accepted.'}
  $wrong=Invoke-WebRequest -Uri "$base/api/gate" -Method Post -WebSession $session -SkipHttpErrorCheck -ContentType 'application/json' -Headers @{'Origin'=$base;'X-ArchSift-Session'='wrong'} -Body '{"evidenceGrade":"daily"}'
  if($wrong.StatusCode -ne 403){throw 'Missing session header was accepted.'}
  $ruleList=Invoke-RestMethod -Uri "$base/api/rules" -WebSession $session
  $bad=Invoke-WebRequest -Uri "$base/api/rules" -Method Post -WebSession $session -SkipHttpErrorCheck -ContentType 'application/json' -Headers $headers -Body (ConvertTo-Json -InputObject @{name='../B/B.csproj';expectedSha256=$ruleList[0].sha256;content=$ruleList[0].content} -Compress)
  if($bad.StatusCode -ne 400){throw 'Undeclared path write was accepted.'}
  Write-Output "web-loopback-origin-session-path-rejection=True host-state-equal=$((HostHash)-ceq$baseline)"

  [void](PostApi '/api/gate' @{evidenceGrade='daily'})
  $webTask=WaitTask $cliResult.runId
  if($webTask.exitCode -ne 0 -or $webTask.freshness -ne 'current'){throw 'Web daily gate did not pass.'}
  $webResult=Invoke-RestMethod -Uri "$base/api/report/$($webTask.runId)" -WebSession $session
  if($webResult.gate.decision -ne $cliResult.gate.decision -or
     $webResult.gate.reason -ne $cliResult.gate.reason -or
     $webResult.coverage.complete -ne $cliResult.coverage.complete -or
     $webTask.inputSha256 -ne $state.current.inputSha256 -or
     $webResult.rules.Count -ne $cliResult.rules.Count){throw 'CLI/Web daily result identity or decision differs.'}
  for($i=0;$i -lt $cliResult.rules.Count;$i++){
    if($webResult.rules[$i].ruleKey -ne $cliResult.rules[$i].ruleKey -or
       $webResult.rules[$i].status -ne $cliResult.rules[$i].status){throw 'CLI/Web rule result differs.'}
  }
  Write-Output "web-daily-parity run=$($webTask.runId) rules=$($webResult.rules.Count) host-state-equal=$((HostHash)-ceq$baseline)"

  [void](PostApi '/api/gate' @{evidenceGrade='full'})
  $fullTask=WaitTask $webTask.runId
  $fullResult=Invoke-RestMethod -Uri "$base/api/report/$($fullTask.runId)" -WebSession $session
  $evidence=Invoke-RestMethod -Uri "$base/api/evidence/$($fullTask.runId)" -WebSession $session
  $manifest=Invoke-RestMethod -Uri "$base/api/evidence/$($fullTask.runId)/manifest" -WebSession $session
  if($fullTask.exitCode -ne 0 -or $fullResult.evidenceGrade -ne 'full' -or
     -not $evidence.fullManifestAvailable -or $manifest.runId -ne $fullTask.runId -or
     $fullResult.gate.decision -ne $webResult.gate.decision){throw 'Web full evidence or rule parity failed.'}
  $retention=Invoke-RestMethod -Uri "$base/api/retention" -WebSession $session
  if($retention.product -ne 'ArchSift.InProject'){throw 'Web retention preview is missing.'}
  Write-Output "web-full run=$($fullTask.runId) manifest=True host-state-equal=$((HostHash)-ceq$baseline)"

  $changed=$ruleList[0].content.Replace('Six-rule-type synthetic InProject acceptance fixture.','IP06 edited fixture.')
  [void](PostApi '/api/rules' @{name=$ruleList[0].name;expectedSha256=$ruleList[0].sha256;content=$changed})
  $stale=Invoke-RestMethod -Uri "$base/api/state" -WebSession $session
  if($stale.current.freshness -ne 'stale'){throw 'Web rule edit did not stale old success.'}
  $newRules=Invoke-RestMethod -Uri "$base/api/rules" -WebSession $session
  [void](PostApi '/api/rules' @{name=$newRules[0].name;expectedSha256=$newRules[0].sha256;content=$ruleList[0].content})
  Write-Output "web-rule-edit-stales-result=True host-state-equal=$((HostHash)-ceq$baseline)"

  [void](PostApi '/api/gate' @{evidenceGrade='daily'})
  $active=$null
  for($attempt=0;$attempt -lt 100;$attempt++){
    Start-Sleep -Milliseconds 100
    $current=Invoke-RestMethod -Uri "$base/api/state" -WebSession $session
    if($current.current.runId -ne $fullTask.runId -and $current.current.phase -notin @('completed','failed','cancelled')){$active=$current.current;break}
  }
  if(-not $active){throw 'Web gate did not expose an active run for cancellation.'}
  [void](PostApi '/api/cancel' @{runId=$active.runId})
  $cancelled=WaitTask $fullTask.runId
  if($cancelled.runId -ne $active.runId -or $cancelled.exitCode -ne 130){throw 'Web cancellation did not stop the run.'}
  Write-Output "web-cancel run=$($cancelled.runId) host-state-equal=$((HostHash)-ceq$baseline)"

  $sibling=Join-Path $lab "fixtures/inproject-ip06-sibling-$id"
  [void][IO.Directory]::CreateDirectory($sibling)
  foreach($name in @('Product.sln','Product.slnx','NuGet.Config')){
    [IO.File]::Copy((Join-Path $fixture $name),(Join-Path $sibling $name))
  }
  foreach($name in @('A','B','C','D')){
    $destination=Join-Path $sibling $name
    [void][IO.Directory]::CreateDirectory($destination)
    foreach($source in Get-ChildItem -LiteralPath (Join-Path $fixture $name) -File |
      Where-Object {$_.Extension -in @('.cs','.csproj')}){
      [IO.File]::Copy($source.FullName,(Join-Path $destination $source.Name))
    }
  }
  & git -C $sibling init -q
  if($LASTEXITCODE -ne 0){throw 'Sibling git init failed.'}
  function RunSibling([string]$name,[string[]]$arguments){
    $child=[Diagnostics.ProcessStartInfo]::new($dotnet)
    $child.WorkingDirectory=$sibling; $child.UseShellExecute=$false; $child.CreateNoWindow=$true
    $child.RedirectStandardOutput=$true; $child.RedirectStandardError=$true
    foreach($argument in $arguments){$child.ArgumentList.Add($argument)}
    $child.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
    $child.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $child.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    $child.Environment['NUGET_PACKAGES']=Join-Path $run 'packages'
    $process=[Diagnostics.Process]::Start($child)
    try{
      $output=$process.StandardOutput.ReadToEndAsync()
      $errors=$process.StandardError.ReadToEndAsync()
      if(-not $process.WaitForExit(180000)){throw "$name timed out"}
      [Threading.Tasks.Task]::WaitAll($output,$errors)
      if($process.ExitCode -ne 0){throw "$name failed: $($errors.Result)"}
    }finally{$process.Dispose()}
  }
  RunSibling 'sibling-init' @($cli,'init','--target',$sibling,'--package',$zip,'--sha256',$package.sha256,'--solution','Product.sln')
  [IO.File]::WriteAllText((Join-Path $sibling '.archsift/state/local-settings.json'),
    (ConvertTo-Json -InputObject @{schemaVersion=1;localFeed=$combinedFeed} -Compress))
  $siblingInstalled=Join-Path $sibling '.archsift/runtime/archsift-inproject.dll'
  $siblingProject=Join-Path $sibling '.archsift/project.json'
  $siblingStart=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $siblingStart.WorkingDirectory=$sibling; $siblingStart.UseShellExecute=$false
  $siblingStart.CreateNoWindow=$true; $siblingStart.RedirectStandardOutput=$true
  $siblingStart.RedirectStandardError=$true
  foreach($argument in @($siblingInstalled,'gate','--project',$siblingProject)){
    $siblingStart.ArgumentList.Add($argument)
  }
  $siblingStart.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $siblingStart.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $siblingStart.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $siblingStart.Environment['NUGET_PACKAGES']=Join-Path $run 'packages'
  $siblingStart.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
  $siblingProcess=[Diagnostics.Process]::Start($siblingStart)
  $siblingOutput=$siblingProcess.StandardOutput.ReadToEndAsync()
  $siblingErrors=$siblingProcess.StandardError.ReadToEndAsync()
  [void](PostApi '/api/gate' @{evidenceGrade='daily'})
  $overlap=$false
  for($attempt=0;$attempt -lt 100;$attempt++){
    Start-Sleep -Milliseconds 100
    $first=Invoke-RestMethod -Uri "$base/api/state" -WebSession $session
    $secondPath=Join-Path $sibling '.archsift/state/current-task.json'
    if([IO.File]::Exists($secondPath)){
      $second=Get-Content $secondPath -Raw|ConvertFrom-Json
      if($first.current.phase -notin @('completed','failed','cancelled') -and
         $second.phase -notin @('completed','failed','cancelled') -and
         $first.worktreeId -ne $second.worktreeId){$overlap=$true;break}
    }
  }
  if(-not $overlap){throw 'Concurrent worktrees did not expose isolated active runs.'}
  if(-not $siblingProcess.WaitForExit(180000)){throw 'Sibling gate timed out.'}
  [Threading.Tasks.Task]::WaitAll($siblingOutput,$siblingErrors)
  if($siblingProcess.ExitCode -ne 0){throw "Sibling gate failed: $($siblingErrors.Result)"}
  $finalFirst=WaitTask $cancelled.runId
  $finalSecond=Get-Content $secondPath -Raw|ConvertFrom-Json
  if($finalFirst.exitCode -ne 0 -or $finalSecond.exitCode -ne 0 -or
     $finalFirst.runId -eq $finalSecond.runId -or
     $finalFirst.worktreeId -eq $finalSecond.worktreeId){throw 'Concurrent worktree isolation failed.'}
  Write-Output "web-concurrent-worktrees isolated=True host-state-equal=$((HostHash)-ceq$baseline)"
} finally {
  [IO.File]::WriteAllBytes($rulesPath,$originalRules)
  if($webProcess){
    if(-not $webProcess.HasExited){$webProcess.Kill($true);$webProcess.WaitForExit()}
    $webProcess.Dispose()
  }
  if($siblingProcess){
    if(-not $siblingProcess.HasExited){$siblingProcess.Kill($true);$siblingProcess.WaitForExit()}
    $siblingProcess.Dispose()
  }
}
if((HostHash)-cne$baseline){throw 'Host drift after Web acceptance; evidence retained.'}
Write-Output "ip06-run=$run fixture=$fixture"
