[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('10.0.100','10.0.303','10.0.402')][string]$Sdk)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
& (Join-Path $PSScriptRoot 'Assert-NR06HostedContext.ps1')
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root=Join-Path $env:RUNNER_TEMP 'nr06-work'
$evidence=Join-Path $env:RUNNER_TEMP 'nr06-evidence'
$dotnet=Join-Path $env:DOTNET_INSTALL_DIR $(if($IsWindows){'dotnet.exe'}else{'dotnet'})
if(-not(Test-Path -LiteralPath $dotnet -PathType Leaf) -or (Test-Path -LiteralPath $root) -or (Test-Path -LiteralPath $evidence)){throw 'Explicit preprovisioned host and fresh external roots required.'}
[void][IO.Directory]::CreateDirectory($evidence)
$checks=[Collections.Generic.List[object]]::new()
function Save([string]$File,$Value){[void][IO.Directory]::CreateDirectory((Split-Path $File));[IO.File]::WriteAllText($File,((ConvertTo-Json -InputObject $Value -Depth 60)+"`n"),[Text.UTF8Encoding]::new($false))}
function Hash([string]$File){(Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash.ToLowerInvariant()}
function Check-Host {if((Get-ArchSiftHostHash) -cne $before){throw 'Host environment/PATH/Profile drift; preserve without repair.'}}
function Invoke-Step([string]$Name,[string]$File,[string[]]$Arguments,[string]$Directory,[hashtable]$Overrides=@{},[int[]]$ExitCodes=@(0)){
 Check-Host
 $start=[Diagnostics.ProcessStartInfo]::new($File);$start.WorkingDirectory=$Directory;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
 $child=@{
  DOTNET_CLI_HOME=(Join-Path $root 'home');DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0';DOTNET_CLI_TELEMETRY_OPTOUT='1';DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='1';DOTNET_GENERATE_ASPNET_CERTIFICATE='false';DOTNET_NOLOGO='1';DOTNET_CLI_UI_LANGUAGE='en-US';DOTNET_ROOT=$env:DOTNET_INSTALL_DIR;DOTNET_ROOT_X64=$env:DOTNET_INSTALL_DIR;DOTNET_ROLL_FORWARD='LatestPatch';
  NUGET_PACKAGES=(Join-Path $root 'packages');NUGET_HTTP_CACHE_PATH=(Join-Path $root 'http');NUGET_PLUGINS_CACHE_PATH=(Join-Path $root 'plugins');NUGET_SCRATCH=(Join-Path $root 'scratch');NUGET_EXE_NO_PROMPT='true';NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT='1';NUGET_PLUGIN_PATHS='';NUGET_PLUGIN_NETCORE_PATHS='';MSBUILDDISABLENODEREUSE='1';
  ARCHSIFT_PROFILE_PATHS=(ConvertTo-Json -InputObject @($PROFILE.AllUsersAllHosts,$PROFILE.AllUsersCurrentHost,$PROFILE.CurrentUserAllHosts,$PROFILE.CurrentUserCurrentHost) -Compress)
 }
 foreach($key in $child.Keys){$start.Environment[$key]=[string]$child[$key]}
 foreach($key in @('GH_TOKEN','GITHUB_TOKEN','NUGET_API_KEY','ACTIONS_ID_TOKEN_REQUEST_TOKEN','ACTIONS_ID_TOKEN_REQUEST_URL','ARCHSIFT_SP02_REPORT_FIXTURE','ARCHSIFT_TEST_GATE_CONFIG','ARCHSIFT_LT01_NEGATIVE','IFX_UPDATE_SNAPSHOTS','MSBUILD_EXE_PATH','MSBuildSDKsPath')){[void]$start.Environment.Remove($key)}
 foreach($key in $Overrides.Keys){if($null -eq $Overrides[$key]){[void]$start.Environment.Remove($key)}else{$start.Environment[$key]=[string]$Overrides[$key]}}
 if($start.Environment.ContainsKey('DOTNET_CLI_HOME') -and $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] -cne '0'){throw 'Child isolation pairing required.'}
 $process=[Diagnostics.Process]::new();$process.StartInfo=$start
 try{
  [void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
  if(-not$process.WaitForExit(600000)){$process.Kill($true);$process.WaitForExit();throw ('Owned synthetic step timed out: '+$Name)}
  $out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
  [IO.File]::WriteAllText((Join-Path $root ($Name+'.stdout.log')),$out,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $root ($Name+'.stderr.log')),$err,[Text.UTF8Encoding]::new($false))
  Check-Host
  $met=$process.ExitCode -in $ExitCodes
  $checks.Add([ordered]@{name=$Name;fileName=[IO.Path]::GetFileName($File);arguments=$Arguments;exitCode=$process.ExitCode;expectedExitCodes=$ExitCodes;expectationMet=$met;hostEqual=$true})
  Save (Join-Path $root 'checks.json') $checks.ToArray()
  Write-Host ("NR06 $Name exit="+$process.ExitCode+" expected=$met host-equal=true")
  if(-not$met){throw ('Consumer step failed; preserve stdout/stderr without retry: '+$Name)}
  return $out
 }finally{$process.Dispose();Check-Host}
}
function Dotnet([string]$Name,[string[]]$Arguments,[string]$Directory,[hashtable]$Overrides=@{},[int[]]$ExitCodes=@(0)) {Invoke-Step $Name $dotnet $Arguments $Directory $Overrides $ExitCodes}
$node=(Get-Command node -CommandType Application|Select-Object -First 1).Source
$prepare=Join-Path $PSScriptRoot 'nuget-consumers/prepare.mjs'
$completed=$false
try{
 # Preparation downloads public data only; no source build or prepublication feed.
 & $node $prepare prepare $root $Sdk
 if($LASTEXITCODE){throw 'Public-byte/owner/payload freeze failed; no SDK operation follows.'}
 Check-Host
 $f=Get-Content -LiteralPath (Join-Path $root 'fixtures.json') -Raw|ConvertFrom-Json
 $publication=Get-Content -LiteralPath (Join-Path $repo 'docs/releases/0.11.0/nr06-publication.json') -Raw|ConvertFrom-Json
 $nuget=Join-Path $f.tool 'NuGet.Config';$manifest=Join-Path $f.tool '.config/dotnet-tools.json'
 $actual=(Dotnet 'sdk' @('--version') $f.tool).Trim();if($actual -cne $Sdk){throw 'Selected SDK differs; global.json substitution is prohibited.'}
 [void](Dotnet 'sdk-info' @('--info') $f.tool)
 [void](Dotnet 'installed-sdks' @('--list-sdks') $f.tool)
 $runtimes=Dotnet 'frameworks' @('--list-runtimes') $f.tool
 foreach($framework in @('Microsoft.NETCore.App','Microsoft.AspNetCore.App')){if($runtimes -cnotmatch ('(?m)^'+[regex]::Escape($framework)+' 10\.0\.13 \[')){throw 'Compatible approved runtime 10.0.13 not provisioned.'}}
 # Verify actual repository signatures/owner constraints natively, separately from raw/payload hashing.
 $index=Invoke-RestMethod -Uri 'https://api.nuget.org/v3/index.json' -TimeoutSec 30 -MaximumRedirection 0
 $resources=@($index.resources|Where-Object {$_.'@type' -ceq 'RepositorySignatures/5.0.0'})
 if($resources.Count -ne 1){throw 'Official repository signing resource unavailable.'}
 $signatureUri=[uri]$resources[0].'@id';if($signatureUri.Scheme -cne 'https' -or $signatureUri.Host -cne 'api.nuget.org'){throw 'Unexpected signature metadata origin.'}
 $signing=Invoke-RestMethod -Uri $signatureUri -TimeoutSec 30 -MaximumRedirection 0
 if($signing.allRepositorySigned -ne $true -or $signing.signingCertificates.Count -lt 1){throw 'Repository signature prerequisites missing.'}
 Save (Join-Path $root 'repository-signing-metadata.json') $signing
 $certificateXml='';$fingerprints=@()
 foreach($certificate in $signing.signingCertificates){$fingerprint=[string]$certificate.fingerprints.'2.16.840.1.101.3.4.2.1';if($fingerprint -cnotmatch '^[a-f0-9]{64}$'){throw 'Invalid official signing fingerprint.'};$fingerprints+=@('--certificate-fingerprint',$fingerprint);$certificateXml+='<certificate fingerprint="'+$fingerprint+'" hashAlgorithm="SHA256" allowUntrustedRoot="false" />'}
 $verifyConfig=Join-Path $root 'NuGet.Signature.Config'
 [IO.File]::WriteAllText($verifyConfig,('<configuration><config><add key="signatureValidationMode" value="require"/></config><packageSources><clear/></packageSources><trustedSigners><clear/><repository name="nuget.org" serviceIndex="https://api.nuget.org/v3/index.json"><owners>XiaolongFeng</owners>'+$certificateXml+'</repository></trustedSigners></configuration>'))
 foreach($package in $publication.packages){[void](Dotnet ('signature-'+$package.id) (@('nuget','verify',(Join-Path $root ('signed-public-inputs/'+$package.id+'.0.11.0.nupkg')),'--all','--configfile',$verifyConfig,'--verbosity','normal')+$fingerprints) $f.tool)}
 [void](Dotnet 'tool-restore' @('tool','restore','--tool-manifest',$manifest,'--configfile',$nuget,'--disable-parallel') $f.tool)
 if((Dotnet 'tool-version' @('tool','run','archsift','--','--version') $f.tool).Trim() -cne 'archsift 0.11.0'){throw 'Actual normal local tool identity differs.'}
 $toolPath=Join-Path $root 'tool path'
 [void](Dotnet 'tool-path-install' @('tool','install','ArchSift.Cli','--version','0.11.0','--tool-path',$toolPath,'--configfile',$nuget) $f.tool)
 $toolExe=Join-Path $toolPath $(if($IsWindows){'archsift.exe'}else{'archsift'})
 if((Invoke-Step 'tool-path-version' $toolExe @('--version') $f.tool).Trim() -cne 'archsift 0.11.0'){throw 'Actual tool-path identity differs.'}
 $payload=Join-Path $root 'packages/archsift.cli/0.11.0/tools/net10.0/any/archsift.dll'
 if(-not(Test-Path -LiteralPath $payload)){throw 'Public CLI was not restored in the owned cache.'}
 $mapped=@{DOTNET_CLI_HOME=(Join-Path $root 'mapping-home');NUGET_PACKAGES=(Join-Path $root 'mapping-packages');NUGET_HTTP_CACHE_PATH=(Join-Path $root 'mapping-http')}
 [void](Dotnet 'mapped-source-restore' @('tool','restore','--tool-manifest',$manifest,'--configfile',(Join-Path $f.tool 'NuGet.Mapping.Config'),'--disable-parallel') $f.tool $mapped)
 $cliRecord=@($publication.packages|Where-Object id -ceq 'ArchSift.Cli')[0]
 if((Hash (Join-Path $mapped.NUGET_PACKAGES 'archsift.cli/0.11.0/archsift.cli.0.11.0.nupkg')) -cne $cliRecord.servedSha256){throw 'Source mapping accepted poison/cached substitute.'}
 $poison=@{DOTNET_CLI_HOME=(Join-Path $root 'poison-home');NUGET_PACKAGES=(Join-Path $root 'poison-packages');NUGET_HTTP_CACHE_PATH=(Join-Path $root 'poison-http')}
 [void](Dotnet 'poison-source-rejection' @('tool','restore','--tool-manifest',$manifest,'--configfile',(Join-Path $f.tool 'NuGet.Poison.Config'),'--disable-parallel') $f.tool $poison @(1))
 $badManifest=Join-Path $f.tool '.config/missing-tools.json'
 Save $badManifest @{version=1;isRoot=$true;tools=@{'archsift.cli'=@{version='0.11.999';commands=@('archsift');rollForward=$false}}}
 [void](Dotnet 'missing-version-rejection' @('tool','restore','--tool-manifest',$badManifest,'--configfile',$nuget,'--disable-parallel') $f.tool @{DOTNET_CLI_HOME=(Join-Path $root 'missing-home');NUGET_PACKAGES=(Join-Path $root 'missing-packages')} @(1))
 [void](Dotnet 'unavailable-source-rejection' @('tool','restore','--tool-manifest',$manifest,'--configfile',(Join-Path $f.tool 'NuGet.Unavailable.Config'),'--disable-parallel') $f.tool @{DOTNET_CLI_HOME=(Join-Path $root 'unavailable-home');NUGET_PACKAGES=(Join-Path $root 'unavailable-packages')} @(1))
 foreach($library in $f.libraries){
  $project=Join-Path $library.directory 'Consumer.csproj';$config=Join-Path $library.directory 'NuGet.Config'
  [void](Dotnet ('restore-'+$library.kind) @('restore',$project,'--configfile',$config,'--disable-build-servers','-p:NuGetAudit=false') $library.directory)
  $lock=Join-Path $library.directory 'packages.lock.json';$lockHash=Hash $lock
  [void](Dotnet ('locked-'+$library.kind) @('restore',$project,'--locked-mode','--configfile',$config,'--disable-build-servers','-p:NuGetAudit=false') $library.directory)
  if((Hash $lock) -cne $lockHash){throw 'Reviewed generated consumer lock changed.'}
  [void](Dotnet ('build-'+$library.kind) @('build',$project,'--no-restore','--disable-build-servers','-c','Release','-p:UseSharedCompilation=false') $library.directory)
  [void](Dotnet ('api-'+$library.kind) @((Join-Path $library.directory 'bin/Release/net10.0/Consumer.dll')) $library.directory)
 }
 foreach($unsupported in $f.unsupported){$tfm=Split-Path $unsupported -Leaf;[void](Dotnet ('unsupported-'+$tfm) @('restore',(Join-Path $unsupported 'Consumer.csproj'),'--configfile',(Join-Path $unsupported 'NuGet.Config'),'--disable-build-servers','-p:NuGetAudit=false','-p:CheckEolTargetFramework=false') $unsupported @{} @(1));$messages=[IO.File]::ReadAllText((Join-Path $root ('unsupported-'+$tfm+'.stdout.log')))+[IO.File]::ReadAllText((Join-Path $root ('unsupported-'+$tfm+'.stderr.log')));if($messages -notmatch 'NU1202'){throw 'Unsupported net8/net9 reference did not reject for package TFM compatibility.'}}
 foreach($target in $f.targets){
  [void](Dotnet ('target-'+$target.tfm+'-pass') @($payload,'verify','--config',$target.config) $f.tool)
  $configuration=Get-Content -LiteralPath $target.config -Raw|ConvertFrom-Json;$configuration.rulesets=@($target.badRules);Save $target.config $configuration
  [void](Dotnet ('target-'+$target.tfm+'-violation') @($payload,'verify','--config',$target.config) $f.tool)
 }
 [void](Dotnet 'mtp-seed-lock' @('restore',(Join-Path $f.mtp 'Portable.Architecture.Tests.csproj'),'--configfile',(Join-Path $f.mtp 'NuGet.Config'),'--disable-build-servers','-p:NuGetAudit=false') $f.mtp)
 $locked=Get-Content -LiteralPath (Join-Path $f.mtp 'packages.lock.json') -Raw|ConvertFrom-Json -AsHashtable
 $graph=$locked.dependencies['net10.0'];$closure=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 function Visit([string]$Id){if(-not$closure.Add($Id)){return};$keys=@($graph.Keys|Where-Object{[string]::Equals($_,$Id,[StringComparison]::OrdinalIgnoreCase)});if($keys.Count -ne 1){throw 'Ambiguous package graph.'};$item=$graph[$keys[0]];if($item.type -eq 'Project' -or -not$item.resolved -or -not$item.contentHash){throw 'Public package graph required.'};if($item.ContainsKey('dependencies')){foreach($child in $item.dependencies.Keys){Visit $child}}}
 Visit 'ArchSift.Testing'
 Save (Join-Path $f.mtp 'package-approval.json') @{schemaVersion=1;testingVersion='0.11.0';cliVersion='0.11.0';packages=@(foreach($id in ($closure|Sort-Object)){$key=@($graph.Keys|Where-Object{[string]::Equals($_,$id,[StringComparison]::OrdinalIgnoreCase)})[0];@{id=$key;version=$graph[$key].resolved;contentHash=$graph[$key].contentHash}})}
 $copied=@(foreach($packageFile in Get-ChildItem -LiteralPath (Join-Path $root 'packages') -Recurse -File -Filter '*.nupkg'){$destination=Join-Path $f.feed $packageFile.Name;if(Test-Path -LiteralPath $destination){throw 'Duplicate package file in graph.'};Copy-Item -LiteralPath $packageFile.FullName -Destination $destination;@{file=$packageFile.Name;sha256=(Hash $destination)}})
 Save (Join-Path $root 'same-network-gate-feed.json') @{scope='Only the same cell network-restored packages for the existing offline-architecture gate; not NR06.04 closure or strict offline acceptance';packages=$copied}
 if($Sdk -ceq '10.0.303'){
  [void](Dotnet 'full-gate' @($payload,'test-gate','--config',$f.gate) $f.mtp)
 }else{
  [void](Dotnet 'full-gate-sdk-contract-rejection' @($payload,'test-gate','--config',$f.gate) $f.mtp @{} @(3))
  $rejection=[IO.File]::ReadAllText((Join-Path $root 'full-gate-sdk-contract-rejection.stderr.log'))
  if(-not $rejection.Contains('Unsupported or incomplete required-case manifest.')){throw 'MTP must reject the existing exact SDK contract, not an unrelated failure.'}
 }
 foreach($package in $publication.packages){$cacheFile=Join-Path $root ('packages/'+$package.id.ToLowerInvariant()+'/0.11.0/'+$package.id.ToLowerInvariant()+'.0.11.0.nupkg');if((Hash $cacheFile) -cne $package.servedSha256){throw 'Restored signed package is not the NR05 archive.'}}
 [void](Dotnet 'host-shim-restore' @('restore',(Join-Path $f.shim 'Consumer.csproj'),'--configfile',(Join-Path $f.shim 'NuGet.Config'),'--disable-build-servers','-p:NuGetAudit=false') $f.shim)
 $shimOutput=Join-Path $root 'probe-shim-output'
 [void](Dotnet 'host-shim-build' @('build',(Join-Path $f.shim 'Consumer.csproj'),'--no-restore','--disable-build-servers','-c','Release','--output',$shimOutput,'-p:UseSharedCompilation=false') $f.shim)
 $shimExe=Join-Path $shimOutput $(if($IsWindows){'Consumer.exe'}else{'Consumer'})
 $probeTarget=Join-Path $root 'probe-target';[void][IO.Directory]::CreateDirectory($probeTarget)
 [IO.File]::WriteAllText((Join-Path $probeTarget 'Consumer.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
 Save (Join-Path $probeTarget 'global.json') @{sdk=@{version=$Sdk;rollForward='disable';allowPrerelease=$false}}
 $probeState=Join-Path $root 'probe-state'
 & $node $prepare workspace $root $probeTarget $probeState $dotnet null null;if($LASTEXITCODE){throw 'Probe configuration preparation failed'}
 $probeConfig=Join-Path $probeTarget '.archsift/workspace.json'
 $diagnostic=Dotnet 'doctor-no-init' @($payload,'workspace','doctor','--config',$probeConfig) $probeTarget
 if(($diagnostic|ConvertFrom-Json).initialized -or (Test-Path -LiteralPath $probeState)){throw 'Doctor must not initialize state.'}
 $probeCases=@(
  @{name='below-floor';sdk='9.0.314';runtimes="Microsoft.NETCore.App 10.0.13 [host]`nMicrosoft.AspNetCore.App 10.0.13 [host]"},
  @{name='prerelease';sdk='10.0.100-rc.1';runtimes="Microsoft.NETCore.App 10.0.13 [host]`nMicrosoft.AspNetCore.App 10.0.13 [host]"},
  @{name='missing-core';sdk=$Sdk;runtimes='Microsoft.AspNetCore.App 10.0.13 [host]'},
  @{name='missing-aspnet';sdk=$Sdk;runtimes='Microsoft.NETCore.App 10.0.13 [host]'}
 )
 foreach($case in $probeCases){
  Save (Join-Path $shimOutput 'probe.json') @{sdk=$case.sdk;info='Architecture: x64';runtimes=$case.runtimes}
  $settings=Get-Content -LiteralPath $probeConfig -Raw|ConvertFrom-Json;$settings.runtime.dotnetPath=$shimExe;Save $probeConfig $settings
  [void](Dotnet ('probe-reject-'+$case.name) @($payload,'workspace','doctor','--config',$probeConfig) $probeTarget @{} @(2))
 }
 $settings.runtime.dotnetPath=Join-Path $root 'missing-host/dotnet.exe';Save $probeConfig $settings
 [void](Dotnet 'explicit-bad-host' @($payload,'workspace','doctor','--config',$probeConfig) $probeTarget @{} @(2))
 $settings.runtime.dotnetPath=$null;Save $probeConfig $settings
 $implicit=Dotnet 'implicit-bad-hint' @($payload,'workspace','doctor','--config',$probeConfig) $probeTarget @{DOTNET_HOST_PATH=(Join-Path $root 'absent-hint')}
 if(($implicit|ConvertFrom-Json).host.sdk -cne $Sdk){throw 'Implicit fallback selected wrong actual host.'}
 $settings.runtime.dotnetPath=$dotnet;Save $probeConfig $settings
 Save (Join-Path $probeTarget 'global.json') @{sdk=@{version='9.0.314';rollForward='disable';allowPrerelease=$false}}
 [void](Dotnet 'global-json-conflict' @($payload,'workspace','doctor','--config',$probeConfig) $probeTarget @{} @(2))
 Save (Join-Path $probeTarget 'global.json') @{sdk=@{version=$Sdk;rollForward='disable';allowPrerelease=$false}}
 [void](Dotnet 'duplicate-workspace-argument' @($payload,'workspace','doctor','--config',$probeConfig,'--config',$probeConfig) $probeTarget @{} @(2))
 $invalid=Join-Path $probeTarget '.archsift/invalid.json';[IO.File]::WriteAllText($invalid,'{"kind":"archsift-workspace","kind":"duplicate"}')
 [void](Dotnet 'invalid-workspace-json' @($payload,'workspace','doctor','--config',$invalid) $probeTarget @{} @(2))
 if(Test-Path -LiteralPath $probeState){throw 'Rejected host/configuration probes mutated state.'}
 Save (Join-Path $root 'negative-host-scope.json') @{scope='Real published CLI checks; below-floor/prerelease/missing-framework cases use a labeled synthetic probe executable. global.json conflict uses the actual installed host; Windows 10.0.303 also has real SDK 9.0.314. No claim that an installed prerelease runtime was qualified.'}
 if($IsWindows){
  $pwsh=(Get-Command pwsh -CommandType Application|Select-Object -First 1).Source
  foreach($mode in @('default','config','environment','explicit','local-override')){
   $target=Join-Path $root ('workspaces/'+$mode+'/target');$state=Join-Path $root ('state-'+$mode);$entryHome=Join-Path $root ('entry-home-'+$mode)
   [void][IO.Directory]::CreateDirectory((Join-Path $target '.archsift'));[void][IO.Directory]::CreateDirectory((Join-Path $target '.config'))
   [IO.File]::WriteAllText((Join-Path $target 'Consumer.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
   Save (Join-Path $target 'global.json') @{sdk=@{version=$Sdk;rollForward='disable';allowPrerelease=$false}}
   Copy-Item -LiteralPath $manifest -Destination (Join-Path $target '.config/dotnet-tools.json')
   Copy-Item -LiteralPath (Join-Path $root 'package-start.ps1') -Destination (Join-Path $target '.archsift/start.ps1')
   $config=Join-Path $target 'NuGet.Config';$expectedCache=Join-Path $entryHome '.nuget/packages';$workspaceCache='null';$environmentCache=$null
   if($mode -eq 'config'){$expectedCache=Join-Path $root ('workspaces/'+$mode+'/configured-cache')}
   if($mode -eq 'environment'){$expectedCache=Join-Path $root 'entry-environment-cache';$environmentCache=$expectedCache}
   if($mode -in @('explicit','local-override')){$expectedCache=Join-Path $root ('entry-'+$mode+'-cache');$workspaceCache=$expectedCache;$environmentCache=Join-Path $root ('ignored-env-'+$mode)}
   $xml='<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders><auditSources><clear/></auditSources>'
   if($mode -eq 'config'){$xml+='<config><add key="globalPackagesFolder" value="../configured-cache"/></config>'}
   $xml+='</configuration>';[IO.File]::WriteAllText($config,$xml)
   & $node $prepare workspace $root $target $state $dotnet $workspaceCache $config;if($LASTEXITCODE){throw 'Entry configuration preparation failed'}
   if($mode -eq 'local-override'){
    $shared=Get-Content -LiteralPath (Join-Path $target '.archsift/workspace.json') -Raw|ConvertFrom-Json;$shared.nuget.cacheDirectory=Join-Path $root 'ignored-shared-cache';Save (Join-Path $target '.archsift/workspace.json') $shared
    Save (Join-Path $target '.archsift/workspace.local.json') @{kind='archsift-workspace-local';schemaVersion=1;nuget=@{cacheDirectory=$expectedCache}}
   }
   $entryOverrides=@{DOTNET_CLI_HOME=$entryHome;NUGET_PACKAGES=$environmentCache;NUGET_HTTP_CACHE_PATH=(Join-Path $root ('entry-http-'+$mode));NUGET_PLUGINS_CACHE_PATH=(Join-Path $root ('entry-plugin-'+$mode))}
   $start=Join-Path $target '.archsift/start.ps1';$entryHash=Hash $start;$configHash=Hash (Join-Path $target '.archsift/workspace.json')
   [void](Invoke-Step ('entry-'+$mode+'-init') $pwsh @('-NoProfile','-NonInteractive','-File',$start,'-Action','Init','-NoBrowser') $target $entryOverrides)
   [void](Invoke-Step ('entry-'+$mode+'-doctor') $pwsh @('-NoProfile','-NonInteractive','-File',$start,'-Action','Doctor','-NoBrowser') $target $entryOverrides)
   $closeouts=@(Get-ChildItem -LiteralPath $state -Recurse -File -Filter 'closeout.json'|ForEach-Object{Get-Content -LiteralPath $_.FullName -Raw|ConvertFrom-Json}|Where-Object{$_.PSObject.Properties.Name -contains 'cacheDirectory'})
   if($closeouts.Count -lt 1 -or @($closeouts|Where-Object{[IO.Path]::GetFullPath($_.cacheDirectory) -cne [IO.Path]::GetFullPath($expectedCache)}).Count){throw 'Project entry cache precedence/default cache differs.'}
   if((Hash $start) -cne $entryHash -or (Hash (Join-Path $target '.archsift/workspace.json')) -cne $configHash){throw 'Entry modified tracked input bytes.'}
   if((Hash (Join-Path $expectedCache 'archsift.cli/0.11.0/archsift.cli.0.11.0.nupkg')) -cne $cliRecord.servedSha256){throw 'Entry restored different public CLI bytes.'}
   Save (Join-Path $root ('entry-'+$mode+'-receipt.json')) @{status='passed';mode=$mode;cacheDirectory=$expectedCache;entrySha256=$entryHash;selectedSdk=$Sdk;publicSignedPackage=$true;isolatedDisposableHome=$true;scope='Normal cache semantics inside a fresh child CLI home; not a claim about the author user profile'}
  }
  [void](Invoke-Step 'embedded-ui-smoke' $node @((Join-Path $PSScriptRoot 'nuget-consumers/ui-smoke.mjs'),$dotnet,$payload,(Join-Path $probeTarget '.archsift/workspace.json'),$root) $probeTarget)
 }
 & $node $prepare finish $root $Sdk
 if($LASTEXITCODE){throw 'Consumer receipts failed independent final qualification.'}
 Check-Host
 if(@(& git -C $repo status --porcelain=v1).Count){throw 'Consumer tests modified the source checkout.'}
 $completed=$true
}finally{
 $after=Get-ArchSiftHostHash
 Save (Join-Path $evidence 'host-final.json') @{before=$before;after=$after;equal=($before -ceq $after);completed=$completed;localAuthorRuntimeUsed=$false;strictOfflineExecuted=$false;humanAcceptanceExecuted=$false}
 if(Test-Path -LiteralPath $root){
  foreach($file in Get-ChildItem -LiteralPath $root -File){if($file.Extension -in @('.json','.log','.Config')){Copy-Item -LiteralPath $file.FullName -Destination $evidence}}
  foreach($directory in @('signed-public-inputs','gate-results')){if(Test-Path -LiteralPath (Join-Path $root $directory)){Copy-Item -LiteralPath (Join-Path $root $directory) -Destination $evidence -Recurse}}
  foreach($directory in @('Contracts','Core','ArchUnit','Testing')){$target=Join-Path $evidence ('library-'+$directory);[void][IO.Directory]::CreateDirectory($target);$library=Join-Path $root ('libraries/'+$directory);foreach($relative in @('packages.lock.json','obj/project.assets.json','Consumer.csproj','Program.cs')){if(Test-Path -LiteralPath (Join-Path $library $relative)){Copy-Item -LiteralPath (Join-Path $library $relative) -Destination (Join-Path $target ([IO.Path]::GetFileName($relative)))}}}
  $mtpEvidence=Join-Path $evidence 'MTP-fixture';[void][IO.Directory]::CreateDirectory($mtpEvidence)
  foreach($relative in @('required-cases.json','policy.json','package-approval.json','packages.lock.json','NuGet.Gate.Config','Portable.Architecture.Tests.csproj')){$file=Join-Path $root ('MTP target/'+$relative);if(Test-Path -LiteralPath $file){Copy-Item -LiteralPath $file -Destination $mtpEvidence}}
  $analysisRoot=Join-Path $root 'analysis results'
  if(Test-Path -LiteralPath $analysisRoot){foreach($file in Get-ChildItem -LiteralPath $analysisRoot -Recurse -File -Filter 'assembly-manifest.json'){$target=Join-Path $evidence ('assembly-manifests/'+[IO.Path]::GetRelativePath($analysisRoot,$file.FullName));[void][IO.Directory]::CreateDirectory((Split-Path $target));Copy-Item -LiteralPath $file.FullName -Destination $target}}
 }
 if($before -cne $after){throw 'Final host drift; no repair performed.'}
}
