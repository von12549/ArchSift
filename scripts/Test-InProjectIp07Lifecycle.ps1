param(
  [Parameter(Mandatory)][string]$Fixture,
  [string]$LabRoot='D:\ArchSift-lab',
  [string]$TestPackageCache,
  [string]$TargetPackageCache,
  [string]$CandidatePackage,
  [string]$CandidateSha256
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lab=[IO.Path]::GetFullPath($LabRoot)
$fixture=[IO.Path]::GetFullPath($Fixture)
if(-not $fixture.StartsWith($lab+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
   -not [IO.Directory]::Exists($fixture)){throw 'Lifecycle fixture must be an existing external synthetic checkout.'}
$run=Join-Path $lab ('runs/inproject-ip07-lifecycle-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
$testCache=if($TestPackageCache){[IO.Path]::GetFullPath($TestPackageCache)}else{
  Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'
}
$targetCache=if($TargetPackageCache){[IO.Path]::GetFullPath($TargetPackageCache)}else{
  Join-Path $lab 'runs/inproject-ip02-probe-49c6da36a2724b44a04b29241180e5ab/packages'
}
function HashText([string]$value){
  [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($value))).ToLowerInvariant()
}
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
function Run([string]$name,[string]$cwd,[string[]]$arguments,[int]$expected=0){
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$cwd;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
  foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
  $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
  $process=[Diagnostics.Process]::Start($start)
  try{
    $output=$process.StandardOutput.ReadToEndAsync()
    $errors=$process.StandardError.ReadToEndAsync()
    if(-not $process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
    [Threading.Tasks.Task]::WaitAll($output,$errors)
    [IO.File]::WriteAllText((Join-Path $run "$name.stdout.log"),$output.Result)
    [IO.File]::WriteAllText((Join-Path $run "$name.stderr.log"),$errors.Result)
    $equal=(HostHash)-ceq$baseline
    Write-Output "$name exit=$($process.ExitCode) expected=$expected host-state-equal=$equal"
    if(-not $equal){throw 'Host drift; lifecycle evidence retained.'}
    if($process.ExitCode -ne $expected){
      Write-Output $output.Result;Write-Output $errors.Result
      throw "$name returned an unexpected exit code."
    }
  }finally{$process.Dispose()}
}
function RunInstalled([string]$name,[string[]]$arguments){
  $pwsh=(Get-Command pwsh -ErrorAction Stop).Source
  $start=[Diagnostics.ProcessStartInfo]::new($pwsh)
  $start.WorkingDirectory=$fixture;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
  foreach($argument in @('-NoProfile','-File',(Join-Path $fixture '.archsift/maintenance/archsift-inproject-maintenance.ps1'))+$arguments){
    $start.ArgumentList.Add($argument)
  }
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $process=[Diagnostics.Process]::Start($start)
  try{
    $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
    if(-not $process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
    [Threading.Tasks.Task]::WaitAll($output,$errors)
    [IO.File]::WriteAllText((Join-Path $run "$name.stdout.log"),$output.Result)
    [IO.File]::WriteAllText((Join-Path $run "$name.stderr.log"),$errors.Result)
    $equal=(HostHash)-ceq$baseline
    Write-Output "$name exit=$($process.ExitCode) expected=0 host-state-equal=$equal"
    if(-not $equal){throw 'Host drift; installed maintenance evidence retained.'}
    if($process.ExitCode -ne 0){Write-Output $output.Result;Write-Output $errors.Result;throw "$name failed"}
  }finally{$process.Dispose()}
}
function DataDigest{
  $install=Join-Path $fixture '.archsift'
  $sources=@(
    (Join-Path $install 'project.json'),(Join-Path $install 'rules'),
    (Join-Path $install 'reports'),(Join-Path $install 'evidence'),
    (Join-Path $install 'state/tasks'),(Join-Path $install 'state/current-task.json'),
    (Join-Path $install 'state/local-settings.json')
  )
  $lines=[Collections.Generic.List[string]]::new()
  foreach($source in $sources){
    if([IO.File]::Exists($source)){$paths=@($source)}
    elseif([IO.Directory]::Exists($source)){$paths=@([IO.Directory]::EnumerateFiles($source,'*',[IO.SearchOption]::AllDirectories))}
    else{$paths=@()}
    foreach($path in $paths){
      if(([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked user-data material.'}
      $relative=[IO.Path]::GetRelativePath($install,$path).Replace('\','/')
      $lines.Add($relative+'|'+(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant())
    }
  }
  HashText (($lines|Sort-Object -CaseSensitive) -join [char]10)
}
$source=Join-Path $repo 'src/ArchSift.InProject/ArchSift.InProject.csproj'
$maintenanceProject=Join-Path $repo 'src/ArchSift.InProject.Maintenance/ArchSift.InProject.Maintenance.csproj'
foreach($item in @(@('cli',$source),@('maintenance',$maintenanceProject))){
  Run "restore-$($item[0])" $repo @('restore',$item[1],'--configfile',(Join-Path $repo 'NuGet.Config'),'--source',$testCache,'--locked-mode','--disable-parallel','--disable-build-servers','-p:BuildInParallel=false','-p:NuGetAudit=false','--verbosity','minimal')
  Run "build-$($item[0])" $repo @('build',$item[1],'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:BuildInParallel=false','-p:UseSharedCompilation=false','--verbosity','minimal')
}
$package=if($CandidatePackage){
  if(-not $CandidateSha256){throw 'Candidate package requires CandidateSha256.'}
  [pscustomobject]@{path=[IO.Path]::GetFullPath($CandidatePackage);sha256=$CandidateSha256}
}else{
  & (Join-Path $PSScriptRoot 'Build-InProjectDevPackage.ps1') -OutputDirectory (Join-Path $run 'package') -IncludeMaintenance -OfflineTestPackageCache $testCache -OfflineTargetPackageCache $targetCache
}
if(-not $package -or (HostHash)-cne$baseline){throw 'Development lifecycle package or host state is invalid.'}
$manager=Join-Path $repo 'src/ArchSift.InProject.Maintenance/bin/Release/net10.0/archsift-inproject-maintenance.dll'
$current=Join-Path $fixture '.archsift/runtime/archsift-inproject.dll'
$lock=Join-Path $fixture '.archsift/version.lock.json'
$originalLock=(Get-Content $lock -Raw|ConvertFrom-Json).packageSha256
if($CandidatePackage){
  Run 'baseline-gate' $fixture @($current,'gate','--project',(Join-Path $fixture '.archsift/project.json'))
  $baselineGate=Get-Content (Join-Path $run 'baseline-gate.stdout.log') -Raw|ConvertFrom-Json
  if($baselineGate.gate.exitCode -ne 0){throw 'Previous version did not establish a passing baseline gate.'}
}
$originalData=DataDigest
Run 'inspect-before' $fixture @($manager,'inspect','--target',$fixture)
Run 'reject-sha' $fixture @($manager,'upgrade','--target',$fixture,'--package',$package.path,'--sha256',('0'*64)) 5
Run 'upgrade' $fixture @($manager,'upgrade','--target',$fixture,'--package',$package.path,'--sha256',$package.sha256)
$upgrade=Get-Content (Join-Path $run 'upgrade.stdout.log') -Raw|ConvertFrom-Json
Run 'verify-upgrade' $fixture @($current,'verify','--target',$fixture)
if($CandidatePackage){
  Run 'status-tool-stale' $fixture @($current,'status','--project',(Join-Path $fixture '.archsift/project.json'))
  $stale=Get-Content (Join-Path $run 'status-tool-stale.stdout.log') -Raw|ConvertFrom-Json
  if($stale.runId -ne $baselineGate.runId -or $stale.freshness -ne 'stale'){
    throw 'Changed tool did not stale the previous version gate.'
  }
}
if((Get-Content $lock -Raw|ConvertFrom-Json).packageSha256 -ne $package.sha256 -or
   (DataDigest) -cne $originalData){throw 'Upgrade changed saved data or did not select the new package.'}
RunInstalled 'rollback' @('-Command','rollback','-Target',$fixture,'-Receipt',$upgrade.operationId)
Run 'verify-rollback' $fixture @($current,'verify','--target',$fixture)
if((Get-Content $lock -Raw|ConvertFrom-Json).packageSha256 -ne $originalLock -or
   (DataDigest) -cne $originalData){throw 'Rollback changed saved data or failed to restore old bytes.'}
Run 'upgrade-again' $fixture @($manager,'upgrade','--target',$fixture,'--package',$package.path,'--sha256',$package.sha256)
RunInstalled 'remove' @('-Command','remove','-Target',$fixture)
$removed=Get-Content (Join-Path $run 'remove.stdout.log') -Raw|ConvertFrom-Json
Run 'inspect-removed' $fixture @($manager,'inspect','--target',$fixture)
$removedView=Get-Content (Join-Path $run 'inspect-removed.stdout.log') -Raw|ConvertFrom-Json
if($removedView.state -ne 'removed' -or [IO.File]::Exists($current) -or (DataDigest) -cne $originalData){
  throw 'Remove did not preserve user data or left active runtime.'
}
Run 'restore-removed' $fixture @($manager,'rollback','--target',$fixture,'--receipt',$removed.operationId)
Run 'verify-restored' $fixture @($current,'verify','--target',$fixture)
if((DataDigest)-cne$originalData){throw 'Restoring removed payload changed user data.'}
$recoveryId=[Guid]::NewGuid().ToString('N')
$old=Join-Path $fixture ".archsift/state/maintenance/$recoveryId/old"
[void][IO.Directory]::CreateDirectory($old)
$beforeLock=Get-Content $lock -Raw|ConvertFrom-Json
$journal=[ordered]@{
  schemaVersion=1;product='ArchSift.InProject';operationId=$recoveryId;operation='remove'
  oldEntries=@('version.lock.json');newEntries=@();oldVersion=$beforeLock.version
  newVersion=$null;oldPackageSha256=$beforeLock.packageSha256;newPackageSha256=$null
}
[IO.File]::WriteAllText((Join-Path $fixture '.archsift/state/maintenance-journal.json'),
  (ConvertTo-Json -InputObject $journal -Compress -Depth 5))
[IO.File]::Move($lock,(Join-Path $old 'version.lock.json'))
Run 'recover-interrupted' $fixture @($manager,'recover','--target',$fixture)
Run 'verify-after-recover' $fixture @($current,'verify','--target',$fixture)
if((DataDigest)-cne$originalData -or [IO.File]::Exists((Join-Path $fixture '.archsift/state/maintenance-journal.json'))){
  throw 'Interrupted maintenance recovery did not restore the original state.'
}
Write-Output "lifecycle-run=$run fixture=$fixture package-sha256=$($package.sha256) user-data-equal=True host-state-equal=$((HostHash)-ceq$baseline)"
