param([Parameter(Mandatory)][ValidateSet('Push','Verify')][string]$Operation,[Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
# No local entry, even for a help/version probe. All isolation is child-only.
if($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted' -or $env:GITHUB_REPOSITORY -cne 'von12549/ArchSift' -or $env:GITHUB_REF -cne 'refs/heads/main' -or $env:GITHUB_RUN_ATTEMPT -cne '1'){throw 'Approved hosted main only.'}
$policy=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../docs/releases/0.11.0/publishing-activation.json') -Raw|ConvertFrom-Json
if($policy.publicationEnabled -isnot [bool] -or -not $policy.publicationEnabled -or $policy.realAssetTransferEnabled -isnot [bool] -or -not $policy.realAssetTransferEnabled -or $env:RELEASE_SWITCH -cne 'true'){throw 'Committed publication locks remain closed.'}
$full=[IO.Path]::GetFullPath($PackagePath)
$temp=[IO.Path]::GetFullPath($env:RUNNER_TEMP)+[IO.Path]::DirectorySeparatorChar
if(-not $full.StartsWith($temp,[StringComparison]::Ordinal) -or -not $full.EndsWith('.nupkg',[StringComparison]::Ordinal)){throw 'Expected owned runner package.'}
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
$childRoot=Join-Path $env:RUNNER_TEMP ('archsift-nuget-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($childRoot)
[IO.File]::WriteAllText((Join-Path $childRoot 'global.json'),'{"sdk":{"version":"10.0.303","rollForward":"disable","allowPrerelease":false}}')
$config=Join-Path $childRoot 'NuGet.Config'
[IO.File]::WriteAllText($config,'<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>')
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName=(Get-Command dotnet -CommandType Application|Select-Object -First 1).Source
$start.WorkingDirectory=$childRoot;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
$start.Environment['DOTNET_CLI_HOME']=Join-Path $childRoot 'home'
$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
$start.Environment['NUGET_PACKAGES']=Join-Path $childRoot 'packages'
$start.Environment['NUGET_HTTP_CACHE_PATH']=Join-Path $childRoot 'http-cache'
$start.Environment['DOTNET_CLI_UI_LANGUAGE']='en-US'
$start.Environment['NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT']='1'
$start.Environment['NUGET_EXE_NO_PROMPT']='true'
$start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
foreach($key in @('GH_TOKEN','ACTIONS_ID_TOKEN_REQUEST_TOKEN','ACTIONS_ID_TOKEN_REQUEST_URL','NUGET_API_KEY')){[void]$start.Environment.Remove($key)}
if($Operation -ceq 'Push'){
 if([string]::IsNullOrEmpty($env:NUGET_API_KEY)){throw 'Temporary key required.'}
 $start.Environment['NUGET_API_KEY']=$env:NUGET_API_KEY
 $arguments=@('nuget','push',$full,'--source','https://api.nuget.org/v3/index.json','--configfile',$config,'--no-symbols','--timeout','120')
}else{
 $index=Invoke-RestMethod -Uri 'https://api.nuget.org/v3/index.json' -TimeoutSec 30 -MaximumRedirection 0
 $resources=@($index.resources|Where-Object {$_.'@type' -ceq 'RepositorySignatures/5.0.0'})
 if($resources.Count -ne 1){throw 'Official repository signature resource unavailable.'}
 $uri=[Uri]$resources[0].'@id'
 if($uri.Scheme -cne 'https' -or $uri.Host -cne 'api.nuget.org'){throw 'Unexpected signature resource origin.'}
 $certs=Invoke-RestMethod -Uri $uri -TimeoutSec 30 -MaximumRedirection 0
 if($certs.allRepositorySigned -ne $true -or $certs.signingCertificates.Count -lt 1){throw 'Repository certificate metadata unavailable.'}
 $xml=''
 $arguments=@('nuget','verify',$full,'--all','--configfile',$config,'--verbosity','normal')
 foreach($cert in $certs.signingCertificates){
  $fingerprint=[string]$cert.fingerprints.'2.16.840.1.101.3.4.2.1'
  if($fingerprint -cnotmatch '^[a-f0-9]{64}$'){throw 'Invalid repository certificate fingerprint.'}
  $xml+='<certificate fingerprint="'+$fingerprint+'" hashAlgorithm="SHA256" allowUntrustedRoot="false" />'
  $arguments+=@('--certificate-fingerprint',$fingerprint)
 }
 [IO.File]::WriteAllText($config,('<configuration><config><add key="signatureValidationMode" value="require" /></config><packageSources><clear/></packageSources><trustedSigners><clear/><repository name="nuget.org" serviceIndex="https://api.nuget.org/v3/index.json"><owners>XiaolongFeng</owners>'+$xml+'</repository></trustedSigners></configuration>'))
}
foreach($arg in $arguments){$start.ArgumentList.Add($arg)}
try{
 $process=[Diagnostics.Process]::new();$process.StartInfo=$start
 try{
  [void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
  if(-not $process.WaitForExit(150000)){$process.Kill($true);throw 'NuGet timed out; server state is unknown.'}
  $out=$stdout.GetAwaiter().GetResult();[void]$stderr.GetAwaiter().GetResult()
  if($process.ExitCode -ne 0){throw 'NuGet operation failed; output withheld.'}
  if($Operation -ceq 'Verify'){Write-Output $out}
 }finally{$process.Dispose()}
}finally{if((Get-ArchSiftHostHash) -cne $before){throw 'Host drift; stop without repair.'}}
