param(
  [Parameter(Mandatory)][string]$OutputDirectory,
  [Parameter(Mandatory)][string]$TestPackageCache,
  [Parameter(Mandatory)][string]$TargetPackageCache,
  [string]$LabRoot='D:\ArchSift-lab'
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lab=[IO.Path]::GetFullPath($LabRoot)
if($lab.Equals($repo,[StringComparison]::OrdinalIgnoreCase) -or
   $lab.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
  throw 'Candidate build lab must be outside source.'
}
$run=Join-Path $lab ('runs/inproject-ip07-candidate-build-'+[Guid]::NewGuid().ToString('N'))
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
    if(-not $equal){throw 'Host drift; candidate build evidence retained.'}
    if($process.ExitCode -ne 0){Write-Output $output.Result;Write-Output $errors.Result;throw "$name failed"}
  }finally{$process.Dispose()}
}
Run 'sdk' @('--version')
foreach($item in @(
  @('worker','src/ArchSift.Cli/ArchSift.Cli.csproj'),
  @('bridge','src/ArchSift.InProject.TestBridge/ArchSift.InProject.TestBridge.csproj'),
  @('cli','src/ArchSift.InProject/ArchSift.InProject.csproj'),
  @('maintenance','src/ArchSift.InProject.Maintenance/ArchSift.InProject.Maintenance.csproj'))){
  $project=Join-Path $repo $item[1]
  Run "restore-$($item[0])" @('restore',$project,'--configfile',(Join-Path $repo 'NuGet.Config'),'--source',([IO.Path]::GetFullPath($TestPackageCache)),'--packages',(Join-Path $run 'packages'),'--locked-mode','--disable-parallel','--disable-build-servers','-p:BuildInParallel=false','-p:NuGetAudit=false','--verbosity','minimal')
  Run "build-$($item[0])" @('build',$project,'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:BuildInParallel=false','-p:UseSharedCompilation=false','-p:VersionSuffix=','--verbosity','minimal')
}
$package=& (Join-Path $PSScriptRoot 'Build-InProjectCandidate.ps1') -OutputDirectory $OutputDirectory -TestPackageCache $TestPackageCache -TargetPackageCache $TargetPackageCache
if(-not $package -or (HostHash)-cne$baseline){throw 'Candidate package build or host state is invalid.'}
Write-Output "candidate-build-run=$run package-sha256=$($package.packageSha256) source-commit=$($package.sourceCommit) host-state-equal=True"
Write-Output "candidate-metadata=$(Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'candidate-metadata.json')"
