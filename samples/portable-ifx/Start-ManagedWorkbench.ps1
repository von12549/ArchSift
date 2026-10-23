[CmdletBinding()]
param([ValidateSet('Launcher','Workbench')][string]$Mode='Launcher',[string]$InstallationRoot=(Join-Path $PSScriptRoot 'ArchSift'))
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Get-LaunchHostHash {
    $values=[ordered]@{}
    foreach($scope in @('User','Machine')){$items=[Environment]::GetEnvironmentVariables($scope);foreach($key in @($items.Keys)|Sort-Object){$values["$scope/$key"]=[string]$items[$key]}}
    $values['process-path']=[Environment]::GetEnvironmentVariable('Path')
    foreach($name in @('AllUsersAllHosts','AllUsersCurrentHost','CurrentUserAllHosts','CurrentUserCurrentHost')){$path=[string]$PROFILE.$name;$values[$name]=if([IO.File]::Exists($path)){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}else{'absent'}}
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($values|ConvertTo-Json -Compress)))).ToLowerInvariant()
}
$before=Get-LaunchHostHash
$root=[IO.Path]::GetFullPath($InstallationRoot).TrimEnd('\','/')
$installation=Get-Content -LiteralPath (Join-Path $root 'install.json') -Raw|ConvertFrom-Json
if($installation.schemaVersion-ne1-or$installation.root-cne$root){throw 'Managed installation identity differs.'}
$selected=@($installation.versions|Where-Object version -CEQ $installation.selectedVersion)
if($selected.Count-ne1){throw 'Ambiguous managed version selection.'}
$directory=[IO.Path]::GetFullPath($selected[0].directory)
if(-not$directory.StartsWith($root+'\versions\',[StringComparison]::OrdinalIgnoreCase)){throw 'Selected version escapes installation.'}
$manifestPath=Join-Path $directory 'package-manifest.json'
if((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()-cne$selected[0].manifestSha256){throw 'Selected manifest differs.'}
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
if($manifest.version-cne$installation.selectedVersion){throw 'Selected payload version differs.'}
$exe=Join-Path $directory 'archsift.exe'
$exeEntry=@($manifest.entries|Where-Object path -CEQ 'archsift.exe')
if($exeEntry.Count-ne1-or(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()-cne$exeEntry[0].sha256){throw 'Selected CLI bytes differ.'}
$settings=Get-Content -LiteralPath (Join-Path $root 'config/runtime-0.10.0.json') -Raw|ConvertFrom-Json
$runtime=[IO.Path]::GetFullPath($settings.root).TrimEnd('\','/')
if($settings.sdk-cne'10.0.303'-or$settings.runtime-cne'10.0.13'){throw 'Unsupported runtime selection.'}
foreach($entry in $settings.files){
    if([IO.Path]::IsPathFullyQualified($entry.path)-or$entry.path-match'(^|[\\/])\.\.([\\/]|$)'){throw 'Unsafe runtime entry.'}
    if((Get-FileHash -LiteralPath (Join-Path $runtime $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant()-cne$entry.sha256){throw ('Runtime input differs: '+$entry.path)}
}
$state=[IO.Path]::GetFullPath($settings.stateRoot)
if($state.Equals($installation.targetRoot,[StringComparison]::OrdinalIgnoreCase)-or$state.StartsWith($installation.targetRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Launch state overlaps target source.'}
$launch=Join-Path $state ('launches/'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($launch)
$psi=[Diagnostics.ProcessStartInfo]::new($exe);$psi.WorkingDirectory=$root;$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true
$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
$psi.StandardOutputEncoding=[Text.UTF8Encoding]::new($false,$true);$psi.StandardErrorEncoding=[Text.UTF8Encoding]::new($false,$true)
if($Mode-ceq'Launcher'){$arguments=@('launch','--root',$root)}else{$config=Join-Path $root 'config/ifx-stable-0.10.0.json';$arguments=@('ui','--config',$config)}
foreach($argument in $arguments){$psi.ArgumentList.Add($argument)}
$retained=@($psi.Environment['PATH'].Split([IO.Path]::PathSeparator)|Where-Object{$_-and-not$_.TrimEnd('\','/').Equals($runtime,[StringComparison]::OrdinalIgnoreCase)})
$psi.Environment['PATH']=(@($runtime)+$retained)-join[IO.Path]::PathSeparator
$psi.Environment['DOTNET_ROOT']=$runtime;$psi.Environment['DOTNET_ROOT_X64']=$runtime;$psi.Environment['DOTNET_HOST_PATH']=Join-Path $runtime 'dotnet.exe';$psi.Environment['DOTNET_MULTILEVEL_LOOKUP']='0';$psi.Environment['DOTNET_ROLL_FORWARD']='LatestPatch'
$psi.Environment['DOTNET_CLI_HOME']=Join-Path $launch 'cli-home';$psi.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$psi.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$psi.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE']='false';$psi.Environment['IFX_UPDATE_SNAPSHOTS']='0'
# Git for Windows must not inherit the Web parent's active stdin control pipe.
# This documented Git option applies only to Git descendants of this child.
$psi.Environment['GIT_REDIRECT_STDIN']='off'
$process=[Diagnostics.Process]::Start($psi)
try{
    Write-Host ('ArchSift '+$installation.selectedVersion+' '+$Mode+'; use Safe shutdown to close the session.')
    # Drain both streams while the session is active, so the direct entry exposes
    # its loopback address immediately and neither redirected stream can block.
    $stdout=$process.StandardOutput.ReadLineAsync();$stderr=$process.StandardError.ReadLineAsync()
    while($null-ne$stdout-or$null-ne$stderr){
        if($null-ne$stdout-and$stdout.IsCompleted){$line=$stdout.GetAwaiter().GetResult();if($null-eq$line){$stdout=$null}else{[Console]::Out.WriteLine($line);[Console]::Out.Flush();$stdout=$process.StandardOutput.ReadLineAsync()}}
        if($null-ne$stderr-and$stderr.IsCompleted){$line=$stderr.GetAwaiter().GetResult();if($null-eq$line){$stderr=$null}else{[Console]::Error.WriteLine($line);[Console]::Error.Flush();$stderr=$process.StandardError.ReadLineAsync()}}
        $pending=@(@($stdout,$stderr)|Where-Object{$null-ne$_})
        if($pending.Count){[void][Threading.Tasks.Task]::WaitAny([Threading.Tasks.Task[]]$pending,100)}
    }
    $process.WaitForExit();$after=Get-LaunchHostHash
    $record=@{schemaVersion=1;mode=$Mode;selectedVersion=$installation.selectedVersion;runtime='10.0.13';sdk='10.0.303';childExit=$process.ExitCode;hostBefore=$before;hostAfter=$after;hostEqual=($before-ceq$after);childOnlyRuntimeIsolation=$true;persistedEnvironmentChanged=$false;utc=[DateTimeOffset]::UtcNow.ToString('O')}
    [IO.File]::WriteAllText((Join-Path $launch 'closeout.json'),(($record|ConvertTo-Json -Depth 8)+"`n"),[Text.UTF8Encoding]::new($false))
    Write-Host ('ArchSift child-exit='+$process.ExitCode+' host-equal='+$record.hostEqual)
    if(-not$record.hostEqual){throw 'Host drift; preserve launch evidence without repair.'}
    if($process.ExitCode-ne0){throw ('ArchSift exited with '+$process.ExitCode+'; inspect retained launch evidence.')}
}finally{$process.Dispose()}
