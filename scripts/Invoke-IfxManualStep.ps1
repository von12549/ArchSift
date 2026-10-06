[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageRoot,
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$Entry='IFX.sln',
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$target=[IO.Path]::GetFullPath($TargetRoot)
$package=[IO.Path]::GetFullPath($PackageRoot)
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('runs/ifx-discovery-'+[Guid]::NewGuid().ToString('N'))
if($run.StartsWith($target+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output overlaps target.'}
function Host-Hash {
    $values=[ordered]@{}
    foreach($scope in @('User','Machine')){$items=[Environment]::GetEnvironmentVariables($scope);foreach($key in @($items.Keys)|Sort-Object){$values["$scope/$key"]=[string]$items[$key]}}
    $values['process-path']=[Environment]::GetEnvironmentVariable('Path')
    foreach($name in @('AllUsersAllHosts','AllUsersCurrentHost','CurrentUserAllHosts','CurrentUserCurrentHost')){$p=[string]$PROFILE.$name;$values[$name]=if([IO.File]::Exists($p)){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}else{'absent'}}
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($values|ConvertTo-Json -Compress)))).ToLowerInvariant()
}
$before=Host-Hash
$head=(& git -C $target rev-parse HEAD).Trim();if($LASTEXITCODE-ne0){throw 'Target Git identity read failed.'}
$ordinary=@(& git -C $target status --porcelain)
$exe=Join-Path $package 'archsift.exe';if(-not(Test-Path -LiteralPath $exe)){throw 'Candidate package missing.'}
$manifest=Get-Content -LiteralPath (Join-Path $package 'package-manifest.json') -Raw|ConvertFrom-Json
foreach($file in $manifest.entries){if((Get-FileHash -LiteralPath (Join-Path $package $file.path) -Algorithm SHA256).Hash.ToLowerInvariant()-cne$file.sha256){throw 'Candidate package bytes changed.'}}
[void][IO.Directory]::CreateDirectory($run)
$start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$exe;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
foreach($arg in @('analyze','--target',$target,'--entry',(Join-Path $target $Entry),'--tfm','net10.0','--configuration','Debug','--output',(Join-Path $run 'reports'))){$start.ArgumentList.Add($arg)}
$start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
$p=[Diagnostics.Process]::Start($start);$out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync()
try{
    if(-not$p.WaitForExit(180000)){$p.Kill($true);throw 'Discovery timeout.'};[Threading.Tasks.Task]::WaitAll($out,$err)
    [IO.File]::WriteAllText((Join-Path $run 'stdout.json'),$out.Result);[IO.File]::WriteAllText((Join-Path $run 'stderr.log'),$err.Result)
    $after=Host-Hash;$finalHead=(&git -C $target rev-parse HEAD).Trim();$finalOrdinary=@(&git -C $target status --porcelain)
    $result=[ordered]@{step='ifx-declared-discovery';targetHead=$head;targetHeadEqual=($head-ceq$finalHead);ordinaryStatusEqual=(($ordinary -join "`n")-ceq($finalOrdinary -join "`n"));hostHashBefore=$before;hostHashAfter=$after;hostStateEqual=($before-ceq$after);exitCode=$p.ExitCode;runRoot=$run;targetBuildInvoked=$false;restoreInvoked=$false;nextAction='RETURN OPERATOR RESULT BEFORE NEXT COMMAND'}
    [IO.File]::WriteAllText((Join-Path $run 'operator-result.json'),($result|ConvertTo-Json));$result|ConvertTo-Json
    if($before-cne$after){throw 'Host state drift; stop without repair.'}
}finally{$p.Dispose()}
