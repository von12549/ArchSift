#Requires -Version 7.4
[CmdletBinding()]
param([ValidateSet('Launcher','Workbench')][string]$Mode='Launcher',
      [ValidateSet('Start','Init','Doctor')][string]$Action='Start',
      [string]$Config='',[string]$LocalConfig='',[string]$Migration='', [switch]$NoBrowser,[switch]$InteractiveRestore)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Need([bool]$condition,[string]$message){if(-not $condition){throw $message}}
function Hash([string]$file){(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()}
function HashText([string]$value){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($value))).ToLowerInvariant()}
function Host-State {
 $values=[ordered]@{}
 foreach($scope in @('User','Machine')){$items=[Environment]::GetEnvironmentVariables($scope);foreach($key in @($items.Keys)|Sort-Object){$values["$scope/$key"]=[string]$items[$key]}}
 foreach($scope in @('User','Machine','Process')){$values["$scope/PATH"]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::Unicode.GetBytes([string][Environment]::GetEnvironmentVariable('PATH',$scope))))}
 foreach($path in @($PROFILE.AllUsersAllHosts,$PROFILE.AllUsersCurrentHost,$PROFILE.CurrentUserAllHosts,$PROFILE.CurrentUserCurrentHost)){$values["profile/$path"]=if([IO.File]::Exists($path)){Hash $path}else{'absent'}}
 HashText ($values|ConvertTo-Json -Compress)
}
$baseline=Host-State
$consumerNuGetHome=if($env:DOTNET_CLI_HOME){[IO.Path]::GetFullPath($env:DOTNET_CLI_HOME)}else{[Environment]::GetFolderPath('UserProfile')}
function Check-Host {Need ((Host-State) -ceq $baseline) 'Host environment/Profile drift; stop all tool commands and preserve evidence. No repair is attempted.'}
function Canonical([string]$path,[string]$base=''){
 Need (-not [string]::IsNullOrWhiteSpace($path)) 'An explicit nonempty path is required.'
 $full=if($base){[IO.Path]::GetFullPath($path,$base)}else{[IO.Path]::GetFullPath($path)}
 $full=[IO.Path]::TrimEndingDirectorySeparator($full);$probe=$full
 while($probe){if(Test-Path -LiteralPath $probe){Need (-not((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Path crosses a link/reparse point.'};$probe=[IO.Path]::GetDirectoryName($probe)}
 return $full
}
function Disjoint([string]$a,[string]$b){Need ($a -ine $b -and -not $a.StartsWith($b+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -and -not $b.StartsWith($a+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) 'Integration roots overlap.'}
function Read-Json([string]$path){
 $path=Canonical $path;Need ((Test-Path -LiteralPath $path -PathType Leaf) -and (Get-Item -LiteralPath $path).Length -le 4194304) ('Missing/oversize JSON: '+$path)
 $text=Get-Content -LiteralPath $path -Raw
 $document=[Text.Json.JsonDocument]::Parse($text)
 function Unique([Text.Json.JsonElement]$value){
  if($value.ValueKind -eq [Text.Json.JsonValueKind]::Object){$keys=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);foreach($property in $value.EnumerateObject()){Need ($keys.Add($property.Name)) 'Duplicate JSON property.';Unique $property.Value}}
  elseif($value.ValueKind -eq [Text.Json.JsonValueKind]::Array){foreach($item in $value.EnumerateArray()){Unique $item}}
 }
 try{Unique $document.RootElement}finally{$document.Dispose()}
 return ($text|ConvertFrom-Json -AsHashtable -Depth 50)
}
if($PSBoundParameters.ContainsKey('Config')){Need (-not [string]::IsNullOrWhiteSpace($Config)) 'Explicit workspace config cannot be empty.'}
if(-not $Config){$Config=Join-Path $PSScriptRoot 'workspace.json'}
$Config=Canonical $Config;$configDirectory=Split-Path $Config
$settings=Read-Json $Config
Need ($settings.Contains('kind') -and $settings.Contains('schemaVersion') -and $settings.kind -ceq 'archsift-workspace' -and $settings.schemaVersion -eq 1) 'Expected workspace schema 1; startup/daily-test JSON belongs in external state.'
Need ($settings.Contains('project') -and $settings.project -is [Collections.IDictionary]) 'Workspace project settings must be an object.'
if(-not $settings.project.Contains('root')){$settings.project.root='..'}
$explicitLocal=$PSBoundParameters.ContainsKey('LocalConfig')
if($explicitLocal){Need (-not [string]::IsNullOrWhiteSpace($LocalConfig)) 'Explicit local config cannot be empty.'}
if(-not $LocalConfig -and (Split-Path $Config -Leaf) -ceq 'workspace.json'){$LocalConfig=Join-Path $configDirectory 'workspace.local.json'}
if($LocalConfig){
 $LocalConfig=Canonical $LocalConfig $configDirectory
 if(Test-Path -LiteralPath $LocalConfig){
  $local=Read-Json $LocalConfig;Need ($local.Contains('kind') -and $local.Contains('schemaVersion') -and $local.kind -ceq 'archsift-workspace-local' -and $local.schemaVersion -eq 1) 'Expected local workspace schema 1.'
  foreach($key in $local.Keys){
   if($key -cin @('kind','schemaVersion')){continue}
   $allowed=@(switch -CaseSensitive ($key){'state'{@('root')};'runtime'{@('dotnetPath')};'nuget'{@('cacheDirectory','configFile')};default{@()}})
   Need ($allowed.Count -gt 0 -and $local[$key] -is [Collections.IDictionary]) 'Local settings cannot override workspace policy.'
   if(-not $settings.Contains($key)){$settings[$key]=@{}}
   foreach($field in $local[$key].Keys){Need ($field -cin $allowed) ('Forbidden local override: '+$key+'.'+$field);$settings[$key][$field]=$local[$key][$field]}
  }
 }elseif($explicitLocal){throw 'Explicit local configuration is missing.'}
}
$root=Canonical $settings.project.root $configDirectory
Need (Test-Path -LiteralPath $root -PathType Container) 'Project root is missing.'
Need ($settings.project.id -cmatch '^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$') 'Invalid stable project ID.'
if(-not $settings.Contains('state')){$settings.state=@{root='C:/ArchSift-lab'}}
Need ($settings.state -is [Collections.IDictionary]) 'Workspace state settings must be an object.'
if(-not $settings.state.Contains('root')){$settings.state.root='C:/ArchSift-lab'}
if(-not $settings.Contains('runtime')){$settings.runtime=@{mode='host';minimumSdkVersion='10.0.0';allowPrerelease=$false}}
if(-not $settings.Contains('nuget')){$settings.nuget=@{mode='offline';allowNetwork=$false}}
Need ($settings.runtime -is [Collections.IDictionary] -and $settings.nuget -is [Collections.IDictionary]) 'Runtime/NuGet settings must be objects.'
foreach($pair in @(@('mode','host'),@('minimumSdkVersion','10.0.0'),@('allowPrerelease',$false))){if(-not $settings.runtime.Contains($pair[0])){$settings.runtime[$pair[0]]=$pair[1]}}
if(-not $settings.nuget.Contains('mode')){$settings.nuget.mode='offline'}
if(-not $settings.nuget.Contains('allowNetwork')){$settings.nuget.allowNetwork=$false}
Need ($settings.nuget.allowNetwork -is [bool] -and $settings.runtime.allowPrerelease -is [bool]) 'Network/prerelease permission fields must be JSON booleans.'
Need ($settings.runtime.mode -cin @('host','approvedExact')) 'Runtime mode must be host or approvedExact.'
foreach($selection in @(@($settings.runtime,'dotnetPath'),@($settings.nuget,'configFile'),@($settings.nuget,'cacheDirectory'))){if($selection[0].Contains($selection[1]) -and $null -ne $selection[0][$selection[1]]){Need (-not [string]::IsNullOrWhiteSpace([string]$selection[0][$selection[1]])) 'Explicit host/config/cache paths cannot be empty.'}}
$offline=$settings.nuget.mode -ceq 'offline'
Need (($offline -and -not $settings.nuget.allowNetwork) -or ($settings.nuget.mode -ceq 'configured' -and $settings.nuget.allowNetwork)) 'Select offline/allowNetwork=false or explicitly configured/allowNetwork=true.'
$minimum=if($settings.runtime.Contains('minimumSdkVersion')){$settings.runtime.minimumSdkVersion}else{'10.0.0'}
Need ([version]$minimum -ge [version]'10.0.0') 'SDK minimum cannot weaken the 10.0.0 floor.'
$stateBase=Canonical $settings.state.root $configDirectory;Disjoint $root $stateBase
$userKey=(HashText ([Environment]::UserDomainName+'\'+[Environment]::UserName)).Substring(0,16)
$bootstrap=Canonical (Join-Path $stateBase ('bootstrap/'+$userKey+'/'+$settings.project.id+'/'+[Guid]::NewGuid().ToString('N')))
$projectState=Canonical (Join-Path $stateBase ('users/'+$userKey+'/projects/'+$settings.project.id))
Disjoint $root $bootstrap
$manifestPath=Canonical (Join-Path $root '.config/dotnet-tools.json');$manifest=Read-Json $manifestPath
Need ($manifest.isRoot -and $manifest.tools.Contains('archsift.cli')) 'Use the project-root local tool manifest; global/ancestor tool substitution is forbidden.'
$version=$manifest.tools.'archsift.cli'.version
Need ($version -cmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$' -and -not $manifest.tools.'archsift.cli'.rollForward) 'ArchSift tool version must be exact without major roll-forward.'
$nugetConfig=if($settings.nuget.Contains('configFile') -and $settings.nuget.configFile){Canonical $settings.nuget.configFile $configDirectory}else{''}
$feed='';$gate=$null
if($offline){
 Need ([bool]$nugetConfig) 'Offline startup needs an explicit standard local-only NuGet.Config.'
 $xmlSettings=[Xml.XmlReaderSettings]::new();$xmlSettings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$xmlSettings.XmlResolver=$null
 $reader=[Xml.XmlReader]::Create($nugetConfig,$xmlSettings)
 try{$nugetXml=[xml]::new();$nugetXml.Load($reader)}finally{$reader.Dispose()}
 Need ($null -ne $nugetXml.SelectSingleNode('/configuration/packageSources/clear')) 'Offline NuGet.Config must clear inherited sources.'
 $sourceNodes=@($nugetXml.SelectNodes('/configuration/packageSources/add'))
 Need ($sourceNodes.Count -gt 0) 'Offline NuGet.Config has no local package source.'
 foreach($source in $sourceNodes){$value=[string]$source.value;$uri=$null;Need ($value -and $value -notmatch '[%$]' -and (-not [Uri]::TryCreate($value,[UriKind]::Absolute,[ref]$uri) -or ($uri.IsFile -and -not $uri.IsUnc))) 'Offline mode rejects remote/dynamic package sources.';$path=Canonical $value (Split-Path $nugetConfig);Need (Test-Path -LiteralPath $path -PathType Container) 'Offline source directory is missing.';Disjoint $root $path;Disjoint $bootstrap $path;$feed=$path}
 Need ($nugetXml.SelectNodes('/configuration/fallbackPackageFolders/add|/configuration/auditSources/add').Count -eq 0) 'Offline config cannot add fallback/audit sources.'
}
$ifx=Test-Path -LiteralPath (Join-Path $root 'architecture/portable-gate.json') -PathType Leaf
if($ifx){
 Need ($settings.Contains('gate') -and $settings.gate.kind -ceq 'ifx' -and $settings.runtime.mode -ceq 'approvedExact' -and $offline -and $sourceNodes.Count -eq 1) 'IFX retains exact runtime and approved offline source policy.'
 foreach($binding in @(@($settings.gate.metadataFile,'architecture/portable-gate.json'),@($settings.runtime.approvalFile,'architecture/runtime-approval.json'),@($settings.nuget.sourceApprovalFile,'architecture/package-source.json'))){Need ((Canonical $binding[0] $configDirectory) -ieq (Canonical (Join-Path $root $binding[1]))) 'IFX policy/approval paths must remain unchanged.'}
 $gate=Read-Json (Join-Path $root 'architecture/portable-gate.json')
 Need ($gate.schemaVersion -eq 1 -and $gate.version -ceq $version) 'Tool manifest differs from IFX gate approval.'
 foreach($pair in @(@('policyFile','policySha256'),@('requiredCases','requiredCasesSha256'))){Need ((Hash (Canonical (Join-Path $root $gate[$pair[0]]))) -ceq $gate[$pair[1]]) 'IFX case/policy bytes differ.'}
 $approval=Read-Json (Join-Path $root 'architecture/package-source.json');$expected=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 Need ($approval.schemaVersion -eq 1 -and $approval.kind -ceq 'archsift-approved-offline-source') 'Unsupported source approval.'
 foreach($package in $approval.packages){Need ($package.file -cmatch '^[A-Za-z0-9_.+-]+\.nupkg$' -and $expected.Add($package.file)) 'Unsafe/duplicate source package.';Need ((Hash (Canonical (Join-Path $feed $package.file))) -ceq $package.sha256) 'Approved feed bytes differ.'}
 Need (@(Get-ChildItem -LiteralPath $feed -File -Filter '*.nupkg').Count -eq $expected.Count) 'Offline feed has unapproved extra packages.'
 Need ((Hash (Join-Path $feed ('ArchSift.Cli.'+$version+'.nupkg'))) -ceq $gate.cliPackageSha256) 'CLI archive is unapproved.'
}
[IO.Directory]::CreateDirectory($bootstrap)|Out-Null
$child=@{DOTNET_CLI_HOME=(Join-Path $bootstrap 'cli-home');DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0';DOTNET_CLI_TELEMETRY_OPTOUT='1';DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true';DOTNET_GENERATE_ASPNET_CERTIFICATE='false';DOTNET_NOLOGO='1';DOTNET_CLI_UI_LANGUAGE='en-US';DOTNET_ROLL_FORWARD='LatestPatch';GIT_REDIRECT_STDIN='off';MSBUILDDISABLENODEREUSE='1';ARCHSIFT_PROFILE_PATHS=(ConvertTo-Json -Compress -InputObject @($PROFILE.AllUsersAllHosts,$PROFILE.AllUsersCurrentHost,$PROFILE.CurrentUserAllHosts,$PROFILE.CurrentUserCurrentHost))}
if($offline){$child.NUGET_CERT_REVOCATION_MODE='offline';$child.NUGET_HTTP_CACHE_PATH=Join-Path $bootstrap 'http-cache';$child.NUGET_PLUGINS_CACHE_PATH=Join-Path $bootstrap 'plugins-cache';$child.NUGET_PLUGIN_PATHS='';$child.NUGET_PLUGIN_NETCORE_PATHS=''}
function Child([string]$executable,[string[]]$arguments,[string]$stage,[switch]$Interactive,[switch]$InheritInput){
 Check-Host
 $start=[Diagnostics.ProcessStartInfo]::new($executable);$start.WorkingDirectory=$root;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.StandardOutputEncoding=[Text.UTF8Encoding]::new($false);$start.StandardErrorEncoding=[Text.UTF8Encoding]::new($false)
 if($Interactive -and -not $InheritInput){$start.RedirectStandardInput=$true;$start.Environment['ARCHSIFT_WORKSPACE_PARENT_CONTROL']='stdin-v1'}
 foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
 foreach($key in $child.Keys){$start.Environment[$key]=$child[$key]}
 [void]$start.Environment.Remove('ARCHSIFT_TEST_GATE_CONFIG');[void]$start.Environment.Remove('DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX')
 $process=[Diagnostics.Process]::Start($start)
 try{
  if($Interactive){
   if(-not $InheritInput){$process.StandardInput.WriteLine('parent-owned');$process.StandardInput.Flush()}
   $deadline=if($InheritInput){[DateTime]::UtcNow.AddMinutes(10)}else{[DateTime]::MaxValue}
   $stdout=$process.StandardOutput.ReadLineAsync();$stderr=$process.StandardError.ReadLineAsync()
   while($null -ne $stdout -or $null -ne $stderr){Need ([DateTime]::UtcNow -lt $deadline) 'Explicit interactive restore timed out.';foreach($which in @('stdout','stderr')){$task=Get-Variable -Name $which -ValueOnly;if($null -ne $task -and $task.IsCompleted){$line=$task.GetAwaiter().GetResult();if($null -eq $line){Set-Variable -Name $which -Value $null}else{if($which -eq 'stdout'){[Console]::Out.WriteLine($line);$stdout=$process.StandardOutput.ReadLineAsync()}else{[Console]::Error.WriteLine($line);$stderr=$process.StandardError.ReadLineAsync()}}}};$tasks=@(@($stdout,$stderr)|Where-Object {$null -ne $_});if($tasks.Count){[void][Threading.Tasks.Task]::WaitAny([Threading.Tasks.Task[]]$tasks,100)}}
   $process.WaitForExit();$output=''
  }else{
   $outputTask=$process.StandardOutput.ReadToEndAsync();$errorTask=$process.StandardError.ReadToEndAsync()
   if(-not $process.WaitForExit(120000)){$process.Kill($true);$process.WaitForExit();throw ('Operation timed out: '+$stage)}
   $output=$outputTask.GetAwaiter().GetResult();$errorText=$errorTask.GetAwaiter().GetResult()
  }
  Check-Host
  @{stage=$stage;exitCode=$process.ExitCode;executable=$executable;hostEqual=$true;utc=[DateTimeOffset]::UtcNow.ToString('O')}|ConvertTo-Json -Compress|Add-Content -LiteralPath (Join-Path $bootstrap 'operations.jsonl') -Encoding utf8NoBOM
  if($process.ExitCode -ne 0 -and -not $Interactive){
   $diagnostic=($output+[Environment]::NewLine+$errorText) -replace '(?i)(https?://)[^/\s:@]+:[^@\s/]+@','$1[redacted]@'
   $diagnostic=$diagnostic -replace '(?i)(https?://[^\s?]+)\?[^\s]+','$1?[redacted]'
   $diagnostic=$diagnostic -replace '(?im)(authorization\s*[=:]\s*)[^\r\n]+','$1[redacted]'
   $diagnostic=$diagnostic -replace '(?i)\b(Bearer|Basic)\s+[A-Za-z0-9+/=_-]+','$1 [redacted]'
   $diagnostic=$diagnostic -replace '(?i)(password|token|apikey|authorization)\s*[=:]\s*[^\s]+','$1=[redacted]'
   $diagnostic=(@($diagnostic -split '\r?\n'|Select-Object -Last 20) -join [Environment]::NewLine)
   [IO.File]::WriteAllText((Join-Path $bootstrap ($stage+'.diagnostic.log')),$diagnostic)
   [Console]::Error.WriteLine($diagnostic)
  }
  Need ($process.ExitCode -eq 0) ('Operation failed: '+$stage+'. Check selected host/global.json, NuGet configuration and pinned package availability; no fallback or repair was attempted.')
  return $output
 }finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose();Check-Host}
}
$success=$false;$dotnet='';$sdk='';$cache=''
try{
 $explicit=if($settings.runtime.Contains('dotnetPath')){$settings.runtime.dotnetPath}else{$null};$candidates=[Collections.Generic.List[string]]::new()
 if($explicit){$candidates.Add((Canonical $explicit $configDirectory))}else{
  if($env:DOTNET_HOST_PATH){$candidates.Add($env:DOTNET_HOST_PATH)}
  foreach($variable in @('DOTNET_ROOT_X64','DOTNET_ROOT')){$value=[Environment]::GetEnvironmentVariable($variable);if($value){$candidates.Add((Join-Path $value $(if($IsWindows){'dotnet.exe'}else{'dotnet'})))}}
  foreach($item in ([string]$env:PATH).Split([IO.Path]::PathSeparator,[StringSplitOptions]::RemoveEmptyEntries)){$candidates.Add((Join-Path $item.Trim('"') $(if($IsWindows){'dotnet.exe'}else{'dotnet'})))}
 }
 $dotnet='';$sdk='';$attempt=0
 foreach($candidate in $candidates|Select-Object -Unique){
  $attempt++
  try{
   Need ([IO.Path]::IsPathFullyQualified($candidate) -and (Test-Path -LiteralPath $candidate -PathType Leaf)) 'dotnet candidate is unavailable.'
   $candidate=Canonical $candidate;$sdk=(Child $candidate @('--version') ('sdk-'+$attempt)).Trim()
   Need (($settings.runtime.allowPrerelease -or -not $sdk.Contains('-')) -and [version]($sdk.Split('-')[0]) -ge [version]$minimum) 'Selected SDK does not satisfy the workspace floor/global.json.'
   $info=Child $candidate @('--info') ('info-'+$attempt);$runtimes=Child $candidate @('--list-runtimes') ('frameworks-'+$attempt)
   if($IsWindows){Need ($info -match '(?m)^\s*Architecture:\s*x64\s*$') 'Windows interactive host must be x64.'}
   foreach($framework in @('Microsoft.NETCore.App','Microsoft.AspNetCore.App')){Need ($runtimes -match ('(?m)^'+[regex]::Escape($framework)+' 10\.0\.\d+ \[')) ('Missing required framework: '+$framework+' 10.0')}
   if($settings.runtime.mode -ceq 'approvedExact'){
    $runtimeApproval=Read-Json (Canonical $settings.runtime.approvalFile $configDirectory);$runtimeRoot=Split-Path $candidate
    Need ($sdk -ceq $runtimeApproval.sdk -and $info -match ('(?ms)^Host:\s*\r?\n\s*Version:\s+'+[regex]::Escape($runtimeApproval.runtime)+'\s*$')) 'IFX SDK/host differs from approval.'
    foreach($file in $runtimeApproval.files){Need ($file.path -notmatch '(^|[\\/])\.\.([\\/]|$)' -and -not [IO.Path]::IsPathFullyQualified($file.path)) 'Unsafe runtime approval path.';Need ((Hash (Canonical (Join-Path $runtimeRoot $file.path))) -ceq $file.sha256) 'Approved runtime bytes differ.'}
    foreach($framework in @('Microsoft.NETCore.App','Microsoft.AspNetCore.App')){$versions=@(Get-ChildItem -LiteralPath (Join-Path $runtimeRoot ('shared/'+$framework)) -Directory -Filter '10.*');Need ($versions.Count -eq 1 -and $versions[0].Name -ceq $runtimeApproval.runtime) 'IFX requires exactly the approved .NET 10 framework patch.'}
   }
   $dotnet=$candidate;break
  }catch{Check-Host;if($explicit){throw}}
 }
 Need ([bool]$dotnet) 'No eligible host: install/select stable SDK >=10.0.0 plus required .NET 10 Core/ASP.NET frameworks; project global.json remains authoritative.'
 $runtimeRoot=Split-Path $dotnet;Disjoint $root $runtimeRoot;Disjoint $bootstrap $runtimeRoot;Disjoint $projectState $runtimeRoot
 $child.DOTNET_ROOT=$runtimeRoot;$child.DOTNET_ROOT_X64=$runtimeRoot;$child.DOTNET_HOST_PATH=$dotnet;$child.DOTNET_MULTILEVEL_LOOKUP='0'
 $cache=if($settings.nuget.Contains('cacheDirectory') -and $settings.nuget.cacheDirectory){Canonical $settings.nuget.cacheDirectory $configDirectory}elseif($env:NUGET_PACKAGES){Canonical $env:NUGET_PACKAGES}else{''}
 if(-not $cache -and $nugetConfig){
  if(-not $offline){$nugetXml=[xml](Get-Content -LiteralPath $nugetConfig -Raw)}
  $cacheSetting=$nugetXml.SelectSingleNode('/configuration/config/add[@key="globalPackagesFolder"]')
  if($null -ne $cacheSetting){$cache=Canonical ([Environment]::ExpandEnvironmentVariables([string]$cacheSetting.value)) (Split-Path $nugetConfig)}
  else{$cache=Canonical (Join-Path $consumerNuGetHome '.nuget/packages')}
 }
 if(-not $cache){
  $cacheOutput=Child $dotnet @('nuget','locals','global-packages','--list','--force-english-output') 'cache-location';Need ($cacheOutput -match '(?m)^global-packages:\s*(.+)\s*$') 'NuGet did not resolve its global cache.';$cache=Canonical $Matches[1].Trim()
  # SDK safety isolation must not turn the normal NuGet cache into a per-launch cache.
  if($cache -ieq (Canonical (Join-Path $child.DOTNET_CLI_HOME '.nuget/packages'))){$cache=Canonical (Join-Path $consumerNuGetHome '.nuget/packages')}
 }
 Disjoint $root $cache;Disjoint $bootstrap $cache;Disjoint $projectState $cache;Disjoint $runtimeRoot $cache
 if($offline){foreach($source in $sourceNodes){$sourcePath=Canonical ([string]$source.value) (Split-Path $nugetConfig);Disjoint $cache $sourcePath;Disjoint $runtimeRoot $sourcePath;Disjoint $projectState $sourcePath}}
 $child.NUGET_PACKAGES=$cache
 $arguments=@('tool','restore','--tool-manifest',$manifestPath)
 if($nugetConfig){$arguments+=@('--configfile',$nugetConfig)}
 if($InteractiveRestore){Need ($Action -cne 'Doctor') 'Doctor does not restore tools or prompt for authentication.';$arguments+='--interactive'}
 if($Action -cne 'Doctor'){if($InteractiveRestore){[void](Child $dotnet $arguments 'tool-restore' -Interactive -InheritInput)}else{[void](Child $dotnet $arguments 'tool-restore')}}
 $payload=Canonical (Join-Path $cache ('archsift.cli/'+$version+'/tools/net10.0/any'))
 Need (Test-Path -LiteralPath (Join-Path $payload 'archsift.dll') -PathType Leaf) ('Pinned tool is not restored in '+$cache+'. Run this entry with -Action Init; doctor does not install tools.')
 if($ifx){
  $zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $feed ('ArchSift.Cli.'+$version+'.nupkg')));$expected=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  try{foreach($entry in $zip.Entries|Where-Object {$_.FullName.StartsWith('tools/net10.0/any/') -and -not $_.FullName.EndsWith('/')}){$relative=$entry.FullName.Substring('tools/net10.0/any/'.Length);Need ($relative -notmatch '(^|/)\.\.(/|$)|\\|:' -and $expected.Add($relative)) 'Unsafe/duplicate package payload.';$stream=$entry.Open();try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}finally{$stream.Dispose()};Need ((Hash (Canonical (Join-Path $payload $relative))) -ceq $hash) 'Restored package/Web differs from approved bytes.'};Need (@(Get-ChildItem -LiteralPath $payload -File -Recurse -Force).Count -eq $expected.Count) 'Extra restored tool/Web files.'}finally{$zip.Dispose()}
 }
 $actualVersion=(Child $dotnet @((Join-Path $payload 'archsift.dll'),'--version') 'tool-version').Trim()
 Need ($actualVersion -ceq ('archsift '+$version)) 'Cached CLI identity differs from the pinned local tool version.'
 $packageArguments=@((Join-Path $payload 'archsift.dll'),'workspace',$Action.ToLowerInvariant(),'--config',$Config,'--mode',$Mode.ToLowerInvariant())
 if($LocalConfig -and (Test-Path -LiteralPath $LocalConfig)){$packageArguments+=@('--local',$LocalConfig)}
 if($Migration){$packageArguments+=@('--migration',(Canonical $Migration))}
 if($NoBrowser){$packageArguments+='--no-browser'}
 [void](Child $dotnet $packageArguments 'workspace' -Interactive)
 $success=$true
}finally{
 Check-Host
 @{schemaVersion=1;status=$(if($success){'passed'}else{'stopped'});targetRoot=$root;selectedVersion=$version;selectedDotnet=$dotnet;selectedSdk=$sdk;cacheDirectory=$cache;hostEqual=$true;action=$Action;mode=$Mode;utc=[DateTimeOffset]::UtcNow.ToString('O')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $bootstrap 'closeout.json') -Encoding utf8NoBOM
}
