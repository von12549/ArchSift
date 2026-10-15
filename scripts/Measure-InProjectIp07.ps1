param(
  [Parameter(Mandatory)][string]$SourceFixture,
  [Parameter(Mandatory)][string]$Package,
  [Parameter(Mandatory)][string]$PackageSha256,
  [string]$LabRoot='D:\ArchSift-lab'
)
$ErrorActionPreference='Stop'
$lab=[IO.Path]::GetFullPath($LabRoot)
$source=[IO.Path]::GetFullPath($SourceFixture)
if(-not $source.StartsWith((Join-Path $lab 'fixtures')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
  throw 'Performance source must be an external synthetic fixture.'
}
$id=[Guid]::NewGuid().ToString('N')
$fixture=Join-Path $lab "fixtures/inproject-ip07-performance-$id"
$run=Join-Path $lab "runs/inproject-ip07-performance-$id"
[void][IO.Directory]::CreateDirectory($fixture)
[void][IO.Directory]::CreateDirectory($run)
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
$watch=[Diagnostics.Stopwatch]::StartNew()
$business=@('Product.sln','Product.slnx','NuGet.Config')
foreach($name in @('A','B','C','D')){
  [void][IO.Directory]::CreateDirectory((Join-Path $fixture $name))
  foreach($file in Get-ChildItem -LiteralPath (Join-Path $source $name) -File){
    if($file.Extension -in @('.cs','.csproj')){
      [IO.File]::Copy($file.FullName,(Join-Path $fixture "$name/$($file.Name)"))
      $business+="$name/$($file.Name)"
    }
  }
}
foreach($name in @('Product.sln','Product.slnx','NuGet.Config')){
  [IO.File]::Copy((Join-Path $source $name),(Join-Path $fixture $name))
}
$watch.Stop();$copyMs=$watch.ElapsedMilliseconds
& git -C $fixture init -q
if($LASTEXITCODE -ne 0){throw 'Synthetic performance Git initialization failed.'}
if((HostHash)-cne$baseline){throw 'Host drift after synthetic copy.'}
function BusinessDigest{
  HashText (($business|Sort-Object -CaseSensitive|ForEach-Object {
    $_+'|'+(Get-FileHash -LiteralPath (Join-Path $fixture $_) -Algorithm SHA256).Hash.ToLowerInvariant()
  }) -join [char]10)
}
$businessBefore=BusinessDigest
$dotnet=(Get-Command dotnet -ErrorAction Stop).Source
$cli=Join-Path $PSScriptRoot '../src/ArchSift.InProject/bin/Release/net10.0/archsift-inproject.dll'
function StartDotnet([string[]]$arguments){
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$fixture;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
  foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
  $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
  [Diagnostics.Process]::Start($start)
}
function OwnedBytes{
  $install=Join-Path $fixture '.archsift'
  if(-not [IO.Directory]::Exists($install)){return 0L}
  $sum=0L
  foreach($file in [IO.Directory]::EnumerateFiles($install,'*',[IO.SearchOption]::AllDirectories)){
    try{$sum+=([IO.FileInfo]$file).Length}catch [IO.IOException]{}
  }
  $sum
}
function InvokeGate([string]$name,[string[]]$arguments,[bool]$measure){
  $process=StartDotnet $arguments
  $timer=[Diagnostics.Stopwatch]::StartNew()
  $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
  $peak=0L;$phases=[Collections.Generic.List[object]]::new();$lastPhase=''
  try{
    while(-not $process.WaitForExit(500)){
      if($timer.Elapsed.TotalMinutes -gt 5){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
      if($measure){
        $peak=[Math]::Max($peak,(OwnedBytes))
        $task=Join-Path $fixture '.archsift/state/current-task.json'
        if([IO.File]::Exists($task)){
          try{$phase=(Get-Content $task -Raw|ConvertFrom-Json).phase}catch{$phase=$lastPhase}
          if($phase -and $phase -ne $lastPhase){$phases.Add(@{phase=$phase;elapsedMs=$timer.ElapsedMilliseconds});$lastPhase=$phase}
        }
      }
    }
    [Threading.Tasks.Task]::WaitAll($output,$errors)
    $timer.Stop()
    [IO.File]::WriteAllText((Join-Path $run "$name.stdout.log"),$output.Result)
    [IO.File]::WriteAllText((Join-Path $run "$name.stderr.log"),$errors.Result)
    $equal=(HostHash)-ceq$baseline
    Write-Host "$name exit=$($process.ExitCode) elapsed-ms=$($timer.ElapsedMilliseconds) host-state-equal=$equal"
    if(-not $equal){throw 'Host drift; performance evidence retained.'}
    if($process.ExitCode -ne 0){Write-Output $output.Result;Write-Output $errors.Result;throw "$name failed"}
    if($measure){
      $peak=[Math]::Max($peak,(OwnedBytes))
      $result=$output.Result|ConvertFrom-Json
      if($result.gate.exitCode -ne 0 -or -not $result.binding.sourceBound){throw "$name did not pass a source-bound gate."}
      $report=Join-Path $fixture ".archsift/reports/$($result.runId)"
      $logs=@(Get-ChildItem -LiteralPath $report -File -Filter '*.stdout.log'|Sort-Object LastWriteTimeUtc|ForEach-Object {
        @{name=$_.Name;finishedUtc=$_.LastWriteTimeUtc.ToString('O');bytes=$_.Length}
      })
      return [ordered]@{name=$name;runId=$result.runId;elapsedMs=$timer.ElapsedMilliseconds;peakOwnedBytes=$peak;finalOwnedBytes=(OwnedBytes);rules=$result.rules.Count;testExecuted=$result.coverage.executedRules.Count;phaseSamples=@($phases);processLogFinishTimes=$logs}
    }
  }finally{$process.Dispose()}
}
InvokeGate 'init' @($cli,'init','--target',$fixture,'--package',$Package,'--sha256',$PackageSha256,'--solution','Product.sln') $false
$rulesSource=Join-Path $PSScriptRoot '../assets/inproject/performance/seven-rules.json'
[IO.File]::Copy($rulesSource,(Join-Path $fixture '.archsift/rules/architecture.json'),$true)
$installed=Join-Path $fixture '.archsift/runtime/archsift-inproject.dll'
$project=Join-Path $fixture '.archsift/project.json'
$cold=InvokeGate 'cold-daily' @($installed,'gate','--project',$project,'--evidence','daily') $true
$hot=InvokeGate 'hot-daily' @($installed,'gate','--project',$project,'--evidence','daily') $true
if((BusinessDigest)-cne$businessBefore){throw 'Synthetic business source changed during measurement.'}
$sdk=(Get-Content (Join-Path $fixture ".archsift/reports/$($cold.runId)/001-sdk.stdout.log") -Raw).Trim()
$record=[ordered]@{schemaVersion=1;product='ArchSift.InProject';sourceFixture=$source;fixture=$fixture;packageSha256=$PackageSha256;copyMs=$copyMs;businessSha256=$businessBefore;sdk=$sdk;configuration='Debug';targetFrameworks=@('net8.0','net9.0','net10.0');cold=$cold;hot=$hot;hostStateEqual=((HostHash)-ceq$baseline)}
$path=Join-Path $run 'measurement.json'
[IO.File]::WriteAllText($path,(ConvertTo-Json -InputObject $record -Depth 12)+[Environment]::NewLine)
Write-Output "measurement=$path copy-ms=$copyMs cold-ms=$($cold.elapsedMs) hot-ms=$($hot.elapsedMs) cold-peak-bytes=$($cold.peakOwnedBytes) hot-peak-bytes=$($hot.peakOwnedBytes) host-state-equal=$($record.hostStateEqual)"
