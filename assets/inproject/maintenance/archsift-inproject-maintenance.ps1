[CmdletBinding()]
param(
  [Parameter(Mandatory)][ValidateSet('inspect','upgrade','rollback','remove','recover')][string]$Command,
  [Parameter(Mandatory)][string]$Target,
  [string]$Package,
  [string]$Sha256,
  [string]$Receipt
)
$ErrorActionPreference='Stop'
$install=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root=[IO.Path]::GetFullPath($Target)
if(-not $install.Equals((Join-Path $root '.archsift'),[StringComparison]::OrdinalIgnoreCase)){
  throw 'Maintenance target does not match this installed tool.'
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
$runnerRoot=[IO.Path]::GetFullPath((Join-Path $install 'state/maintenance-runners'))
$runner=[IO.Path]::GetFullPath((Join-Path $runnerRoot ([Guid]::NewGuid().ToString('N'))))
if(-not $runner.StartsWith($runnerRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
  throw 'Maintenance runner path escaped its owned root.'
}
[void][IO.Directory]::CreateDirectory($runner)
$exitCode=3
$hostDrift=$false
try{
  foreach($file in Get-ChildItem -LiteralPath $PSScriptRoot -File){
    if(($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked maintenance payload.'}
    if($file.Extension -notin @('.dll','.exe','.json')){continue}
    $copy=Join-Path $runner $file.Name
    [IO.File]::Copy($file.FullName,$copy)
    if((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne
       (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash){throw 'Maintenance runner copy changed.'}
  }
  if((HostHash)-cne$baseline){$hostDrift=$true;throw 'Host drift during maintenance preparation; evidence retained.'}
  $dotnet=(Get-Command dotnet -ErrorAction Stop).Source
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$root;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
  foreach($argument in @((Join-Path $runner 'archsift-inproject-maintenance.dll'),$Command,'--target',$root)){
    $start.ArgumentList.Add($argument)
  }
  if($Command -eq 'upgrade'){
    if(-not $Package -or -not $Sha256){throw 'Upgrade requires Package and Sha256.'}
    foreach($argument in @('--package',$Package,'--sha256',$Sha256)){$start.ArgumentList.Add($argument)}
  }elseif($Command -eq 'rollback'){
    if(-not $Receipt){throw 'Rollback requires Receipt.'}
    foreach($argument in @('--receipt',$Receipt)){$start.ArgumentList.Add($argument)}
  }
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $runner 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
  $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
  $process=[Diagnostics.Process]::Start($start)
  try{
    $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
    if(-not $process.WaitForExit(300000)){$process.Kill($true);$process.WaitForExit();throw 'Maintenance timed out.'}
    [Threading.Tasks.Task]::WaitAll($output,$errors)
    $exitCode=$process.ExitCode
    if($output.Result){Write-Output $output.Result.TrimEnd()}
    if($errors.Result){[Console]::Error.WriteLine($errors.Result.TrimEnd())}
  }finally{$process.Dispose()}
  if((HostHash)-cne$baseline){$hostDrift=$true;throw 'Host drift during maintenance; evidence retained.'}
}finally{
  if(-not $hostDrift -and [IO.Directory]::Exists($runner) -and
     $runner.StartsWith($runnerRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
    Remove-Item -LiteralPath $runner -Recurse -Force
  }
}
exit $exitCode
