param(
  [Parameter(Mandatory)][string]$CandidateMetadata,
  [Parameter(Mandatory)][string]$SourceFixture,
  [string]$LabRoot='D:\ArchSift-lab'
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lab=[IO.Path]::GetFullPath($LabRoot)
$meta=Get-Content ([IO.Path]::GetFullPath($CandidateMetadata)) -Raw|ConvertFrom-Json
$budget=Get-Content (Join-Path $repo 'docs/performance/inproject-0.1-budget.json') -Raw|ConvertFrom-Json
$head=(& git -C $repo rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0 -or $meta.version -ne '0.1.0' -or $meta.sourceCommit -ne $head -or
   (Get-FileHash -LiteralPath $meta.packagePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $meta.packageSha256){
  throw 'Candidate source commit, version or ZIP identity differs.'
}
function HashText([string]$value){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($value))).ToLowerInvariant()}
function ScopeHash([EnvironmentVariableTarget]$scope){
  $values=[Environment]::GetEnvironmentVariables($scope);$ordered=[ordered]@{}
  foreach($key in @($values.Keys)|Sort-Object){$ordered[[string]$key]=[string]$values[$key]}
  HashText (ConvertTo-Json -InputObject $ordered -Compress -Depth 5)
}
function HostHash{
  $profiles=@()
  foreach($name in @('AllUsersAllHosts','AllUsersCurrentHost','CurrentUserAllHosts','CurrentUserCurrentHost')){
    $path=[string]$PROFILE.$name
    $profiles+=@{name=$name;exists=[IO.File]::Exists($path);sha256=$(if([IO.File]::Exists($path)){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}else{$null})}
  }
  HashText (ConvertTo-Json -InputObject @{user=ScopeHash User;machine=ScopeHash Machine;processPath=HashText ([Environment]::GetEnvironmentVariable('Path','Process'));profiles=$profiles} -Compress -Depth 8)
}
$baseline=HostHash
$id=[Guid]::NewGuid().ToString('N')
$run=Join-Path $lab "runs/inproject-ip07-candidate-$id"
[void][IO.Directory]::CreateDirectory($run)
$measurementOutput=& (Join-Path $PSScriptRoot 'Measure-InProjectIp07.ps1') -SourceFixture $SourceFixture -Package $meta.packagePath -PackageSha256 $meta.packageSha256 -LabRoot $lab
$line=@($measurementOutput|Where-Object {$_ -match '^measurement='})|Select-Object -Last 1
if($line -notmatch '^measurement=([^ ]+) '){throw 'Candidate performance record was not returned.'}
$measurementPath=$Matches[1]
$measurement=Get-Content $measurementPath -Raw|ConvertFrom-Json
if((HostHash)-cne$baseline -or -not $measurement.hostStateEqual -or
   $measurement.businessSha256 -ne $budget.reference.businessInputSha256 -or
   $measurement.sdk -ne $budget.reference.sdk){
  throw 'Candidate measurement host, SDK or input identity differs from frozen budget.'
}
function PhaseTimings($item){
  $map=@{}
  foreach($sample in $item.phaseSamples){$map[[string]$sample.phase]=[int64]$sample.elapsedMs}
  foreach($phase in @('restore','build','bind','test')){if(-not $map.ContainsKey($phase)){throw "Missing sampled phase $phase"}}
  [ordered]@{
    preflightAndSnapshot=$map.restore
    restore=($map.build-$map.restore)
    buildAndEvaluation=($map.bind-$map.build)
    bindingAnalysis=($map.test-$map.bind)
    xunitAndGate=([int64]$item.elapsedMs-$map.test)
  }
}
$cold=PhaseTimings $measurement.cold
$warm=PhaseTimings $measurement.hot
foreach($pair in @(@($cold,$measurement.cold),@($warm,$measurement.hot))){
  foreach($phase in $pair[0].Keys){
    if($pair[0][$phase] -lt 0 -or $pair[0][$phase] -gt $budget.limitsMs.$phase){
      throw "Candidate exceeded frozen $phase budget."
    }
  }
}
if($measurement.copyMs -gt $budget.limitsMs.copyBusinessInput -or
   $measurement.cold.elapsedMs -gt $budget.limitsMs.coldTotal -or
   $measurement.hot.elapsedMs -gt $budget.limitsMs.warmTotal -or
   $measurement.cold.peakOwnedBytes -gt $budget.limitsPeakOwnedBytes.cold -or
   $measurement.hot.peakOwnedBytes -gt $budget.limitsPeakOwnedBytes.warmCumulative){
  throw 'Candidate exceeded frozen copy, whole-run or owned-disk budget.'
}
$fixture=$measurement.fixture
$install=Join-Path $fixture '.archsift'
$cli=Join-Path $install 'runtime/archsift-inproject.dll'
$project=Join-Path $install 'project.json'
$lock=Get-Content (Join-Path $install 'version.lock.json') -Raw|ConvertFrom-Json
if($lock.version -ne '0.1.0' -or $lock.packageSha256 -ne $meta.packageSha256){throw 'Fresh installation selected a different candidate.'}
$coldReport=Join-Path $install "reports/$($measurement.cold.runId)"
$trx=[xml](Get-Content (Join-Path $coldReport 'architecture.trx') -Raw)
$results=@($trx.TestRun.Results.UnitTestResult)
if($results.Count -ne 2 -or @($results|Where-Object outcome -ne 'Passed').Count -ne 0){
  throw 'Candidate dedicated xUnit execution count is not two passing B-rule tests.'
}
$dotnet=(Get-Command dotnet -ErrorAction Stop).Source
function Run([string]$name,[string[]]$arguments,[int]$expected=0,[string]$missingHost=''){
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$fixture;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
  foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
  $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
  if($missingHost){$start.Environment['DOTNET_HOST_PATH']=$missingHost}
  $process=[Diagnostics.Process]::Start($start)
  try{
    $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
    if(-not $process.WaitForExit(300000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
    [Threading.Tasks.Task]::WaitAll($output,$errors)
    [IO.File]::WriteAllText((Join-Path $run "$name.stdout.log"),$output.Result)
    [IO.File]::WriteAllText((Join-Path $run "$name.stderr.log"),$errors.Result)
    $equal=(HostHash)-ceq$baseline
    Write-Output "$name exit=$($process.ExitCode) expected=$expected host-state-equal=$equal"
    if(-not $equal){throw 'Host drift; candidate evidence retained.'}
    if(($expected -eq -1 -and $process.ExitCode -eq 0) -or
       ($expected -ne -1 -and $process.ExitCode -ne $expected)){
      Write-Output $output.Result;Write-Output $errors.Result
      throw "$name returned an unexpected exit code"
    }
  }finally{$process.Dispose()}
}
Run 'version' @($cli,'--version')
if((Get-Content (Join-Path $run 'version.stdout.log') -Raw).Trim() -ne 'ArchSift.InProject 0.1.0'){
  throw 'Candidate CLI prints a different version.'
}
Run 'verify' @($cli,'verify','--target',$fixture)
Run 'full' @($cli,'gate','--project',$project,'--evidence','full')
$full=Get-Content (Join-Path $run 'full.stdout.log') -Raw|ConvertFrom-Json
if($full.gate.exitCode -ne 0 -or $full.evidenceGrade -ne 'full' -or
   $full.rules.Count -ne 7 -or -not $full.binding.sourceBound){throw 'Candidate full gate is incomplete.'}
$bundle=[IO.Path]::GetDirectoryName([string]$full.materials.indexPath)
Run 'full-independent-verify' @($cli,'evidence-verify','--bundle',$bundle)
$verified=Get-Content (Join-Path $run 'full-independent-verify.stdout.log') -Raw|ConvertFrom-Json
if(-not $verified.verified -or $verified.runId -ne $full.runId){
  throw 'Candidate full evidence failed independent verification.'
}
$fullTask=Get-Content (Join-Path $install "state/tasks/$($full.runId).json") -Raw|ConvertFrom-Json
$webProcess=$null
try{
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$fixture;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
  foreach($argument in @($cli,'ui','--project',$project,'--port','0')){$start.ArgumentList.Add($argument)}
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $webProcess=[Diagnostics.Process]::Start($start)
  $webErrors=$webProcess.StandardError.ReadToEndAsync()
  $url=$webProcess.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(20)).GetAwaiter().GetResult()
  if($url -notmatch '^http://127\.0\.0\.1:[0-9]+/\?session=[0-9a-f]{64}$'){throw 'Candidate Web did not bind a private loopback session.'}
  $session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
  $boot=Invoke-WebRequest -Uri $url -WebSession $session -SkipHttpErrorCheck
  if($boot.StatusCode -ne 200){throw 'Candidate Web bootstrap failed.'}
  $base=([uri]$url).GetLeftPart([UriPartial]::Authority)
  $denied=Invoke-WebRequest -Uri "$base/api/state" -SkipHttpErrorCheck
  if($denied.StatusCode -ne 403){throw 'Candidate Web accepted a request without session.'}
  $token=(Invoke-RestMethod -Uri "$base/api/session" -WebSession $session).token
  $headers=@{'Origin'=$base;'X-ArchSift-Session'=$token}
  $cross=Invoke-WebRequest -Uri "$base/api/gate" -Method Post -WebSession $session -SkipHttpErrorCheck -ContentType 'application/json' -Headers @{'Origin'='http://evil.invalid';'X-ArchSift-Session'=$token} -Body '{"evidenceGrade":"daily"}'
  if($cross.StatusCode -ne 403){throw 'Candidate Web accepted a cross-origin gate.'}
  [void](Invoke-RestMethod -Uri "$base/api/gate" -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' -Body '{"evidenceGrade":"daily"}')
  $webTask=$null
  for($attempt=0;$attempt -lt 360;$attempt++){
    Start-Sleep -Milliseconds 500
    $state=Invoke-RestMethod -Uri "$base/api/state" -WebSession $session
    if($state.current.runId -ne $full.runId -and $state.current.phase -in @('completed','failed','cancelled')){$webTask=$state.current;break}
  }
  if(-not $webTask -or $webTask.exitCode -ne 0){throw 'Candidate Web daily gate did not complete successfully.'}
  $webReport=Invoke-RestMethod -Uri "$base/api/report/$($webTask.runId)" -WebSession $session
  if($webReport.gate.decision -ne $full.gate.decision -or $webReport.rules.Count -ne $full.rules.Count -or
     $webTask.inputSha256 -ne $fullTask.inputSha256 -or -not $webReport.binding.sourceBound){
    throw 'Candidate Web and CLI decision or coverage differ.'
  }
  for($i=0;$i -lt $full.rules.Count;$i++){
    if($webReport.rules[$i].ruleKey -ne $full.rules[$i].ruleKey -or
       $webReport.rules[$i].status -ne $full.rules[$i].status){throw 'Candidate Web rule parity failed.'}
  }
  Write-Output "candidate-web-parity run=$($webTask.runId) rules=$($webReport.rules.Count) host-state-equal=$((HostHash)-ceq$baseline)"
}finally{
  if($webProcess){if(-not $webProcess.HasExited){$webProcess.Kill($true);$webProcess.WaitForExit()};$webProcess.Dispose()}
}
Run 'missing-sdk' @($cli,'gate','--project',$project) 3 (Join-Path $run 'missing-dotnet.exe')
$failed=Get-Content (Join-Path $run 'missing-sdk.stdout.log') -Raw|ConvertFrom-Json
if($failed.phase -ne 'failed' -or $failed.runId -eq $webTask.runId -or $failed.exitCode -ne 3){
  throw 'Missing SDK did not replace the current gate with a failure.'
}
Run 'missing-test-runtime' @('exec','--fx-version','99.0.0',$cli,'--version') -1
$runtimeError=Get-Content (Join-Path $run 'missing-test-runtime.stderr.log') -Raw
if($runtimeError -notmatch '(?i)framework|compatible runtime|version'){throw 'Missing runtime host failure was not explicit.'}
$record=[ordered]@{
  schemaVersion=1;product='ArchSift.InProject';version='0.1.0';sourceCommit=$head
  packageSha256=$meta.packageSha256;packagePath=$meta.packagePath;fixture=$fixture
  measurement=$measurementPath;coldMs=$measurement.cold.elapsedMs;warmMs=$measurement.hot.elapsedMs
  coldPeakOwnedBytes=$measurement.cold.peakOwnedBytes;warmPeakOwnedBytes=$measurement.hot.peakOwnedBytes
  fullRunId=$full.runId;fullBundle=$bundle;realXunitTests=$results.Count;webRunId=$webTask.runId
  missingSdkRunId=$failed.runId;missingRuntimeNonzero=$true
  hostStateEqual=((HostHash)-ceq$baseline)
}
$resultPath=Join-Path $run 'candidate-qualification.json'
[IO.File]::WriteAllText($resultPath,(ConvertTo-Json -InputObject $record -Depth 8)+[Environment]::NewLine)
Write-Output "candidate-qualification=$resultPath host-state-equal=$($record.hostStateEqual)"
