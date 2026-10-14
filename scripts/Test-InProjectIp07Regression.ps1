param(
  [string]$LabRoot='D:\ArchSift-lab',
  [string]$PackageCache
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lab=[IO.Path]::GetFullPath($LabRoot)
if($lab.Equals($repo,[StringComparison]::OrdinalIgnoreCase) -or
   $lab.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
  throw 'Regression lab must be outside source.'
}
$run=Join-Path $lab ('runs/inproject-ip07-regression-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
$feed=if($PackageCache){[IO.Path]::GetFullPath($PackageCache)}else{Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'}
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
$dotnet=(Get-Command dotnet -ErrorAction Stop).Source
function Run([string]$name,[string[]]$arguments){
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$repo;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
  foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
  $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
  $start.Environment['ARCHSIFT_TEST_LAB_ROOT']=Join-Path $run 'test-fixtures'
  $start.Environment['NUGET_PACKAGES']=Join-Path $run 'packages'
  $process=[Diagnostics.Process]::Start($start)
  try{
    $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
    if(-not $process.WaitForExit(300000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
    [Threading.Tasks.Task]::WaitAll($output,$errors)
    [IO.File]::WriteAllText((Join-Path $run "$name.stdout.log"),$output.Result)
    [IO.File]::WriteAllText((Join-Path $run "$name.stderr.log"),$errors.Result)
    $equal=(HostHash)-ceq$baseline
    Write-Output "$name exit=$($process.ExitCode) host-state-equal=$equal"
    if(-not $equal){throw 'Host drift; regression evidence retained.'}
    if($process.ExitCode -ne 0){Write-Output $output.Result;Write-Output $errors.Result;throw "$name failed"}
    if($name -like 'test-*'){Write-Output $output.Result.TrimEnd()}
  }finally{$process.Dispose()}
}
$projects=@(
  'tests/ArchSift.UnitTests/ArchSift.UnitTests.csproj',
  'tests/ArchSift.ArchitectureTests/ArchSift.ArchitectureTests.csproj',
  'tests/ArchSift.IntegrationTests/ArchSift.IntegrationTests.csproj'
)
foreach($project in $projects){
  $name=[IO.Path]::GetFileNameWithoutExtension($project)
  Run "restore-$name" @('restore',(Join-Path $repo $project),'--configfile',(Join-Path $repo 'NuGet.Config'),'--source',$feed,'--packages',(Join-Path $run 'packages'),'--locked-mode','--disable-parallel','--disable-build-servers','-p:BuildInParallel=false','-p:NuGetAudit=false','--verbosity','minimal')
  Run "test-$name" @('test',(Join-Path $repo $project),'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:BuildInParallel=false','-p:UseSharedCompilation=false','--verbosity','minimal')
}
Write-Output "regression-run=$run host-state-equal=$((HostHash)-ceq$baseline)"
