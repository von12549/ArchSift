[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputRoot,[Parameter(Mandatory)][string]$LocalFeed,
      [Parameter(Mandatory)][string]$DotnetPath,[switch]$Development)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
function Check-Host {if((Get-ArchSiftHostHash) -cne $before){throw 'Host drift; stop without repair and preserve evidence.'}}
function External([string]$path){
 $full=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($path))
 if($full -ieq $repo -or $full.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $repo.StartsWith($full+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package build output/feed must be disjoint from product source.'}
 for($cursor=$full;$cursor;$cursor=[IO.Path]::GetDirectoryName($cursor)){if(Test-Path -LiteralPath $cursor){if((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Package paths cross a link.'}}};$full
}
$output=External $OutputRoot;$feed=External $LocalFeed
if(Test-Path -LiteralPath $output){throw 'Use a fresh build output; previous attempts remain evidence.'}
$dirty=@(& git -C $repo status --porcelain=v1).Count -ne 0
if($dirty -and -not $Development){throw 'Qualified package build requires clean committed source.'}
$commit=(& git -C $repo rev-parse HEAD).Trim();$tree=(& git -C $repo rev-parse 'HEAD^{tree}').Trim()
[xml]$props=Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props') -Raw
$version=$props.SelectSingleNode('/Project/PropertyGroup/ArchSiftReleaseVersion').InnerText.Trim()
if($version -notmatch '^\d+\.\d+\.\d+(?:-rc\.\d+)?$'){throw 'Explicit reviewed package version required.'}
$dotnet=[IO.Path]::GetFullPath($DotnetPath)
if(-not(Test-Path -LiteralPath $dotnet -PathType Leaf)){throw 'Explicit dotnet host missing.'}
[IO.Directory]::CreateDirectory($output)|Out-Null
function Save([string]$name,$value){$value|ConvertTo-Json -Depth 35|Set-Content -LiteralPath (Join-Path $output $name) -Encoding utf8NoBOM}
$checks=[Collections.Generic.List[object]]::new()
function Invoke-Dotnet([string]$name,[string[]]$arguments){
 Check-Host
 $start=[Diagnostics.ProcessStartInfo]::new($dotnet);$start.WorkingDirectory=$repo;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
 $start.Environment['DOTNET_CLI_HOME']=Join-Path $output 'cli-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
 $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1';$start.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE']='false'
 $start.Environment['DOTNET_HOST_PATH']=$dotnet;$start.Environment['DOTNET_ROOT']=Split-Path $dotnet;$start.Environment['DOTNET_ROOT_X64']=Split-Path $dotnet;$start.Environment['DOTNET_ROLL_FORWARD']='LatestPatch'
 $start.Environment['NUGET_PACKAGES']=Join-Path $output 'build-cache';$start.Environment['NUGET_CERT_REVOCATION_MODE']='offline';$start.Environment['MSBUILDDISABLENODEREUSE']='1'
 $process=[Diagnostics.Process]::Start($start)
 try{$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();$process.WaitForExit();$text=$stdout.GetAwaiter().GetResult();[IO.File]::WriteAllText((Join-Path $output ($name+'.stdout.log')),$text);[IO.File]::WriteAllText((Join-Path $output ($name+'.stderr.log')),$stderr.GetAwaiter().GetResult());Check-Host;$checks.Add(@{stage=$name;exitCode=$process.ExitCode;hostEqual=$true});Save 'checks.json' $checks.ToArray();if($process.ExitCode -ne 0){throw ('Package build stage failed: '+$name)};return $text}finally{$process.Dispose();Check-Host}
}
try{
 $sdk=(Invoke-Dotnet 'sdk' @('--version')).Trim();if($sdk -cne '10.0.303'){throw 'Package build keeps the existing exact SDK approval.'}
 [void](Invoke-Dotnet 'restore' @('restore','ArchSift.slnx','--locked-mode','--configfile','NuGet.Config','--source',$feed,'--packages',(Join-Path $output 'build-cache'),'-p:NuGetAudit=false'))
 $common=@(('-p:SourceRevisionId='+$commit),'-p:UseSharedCompilation=false','--disable-build-servers')
 [void](Invoke-Dotnet 'build' (@('build','ArchSift.slnx','--no-restore','-c','Release')+$common))
 $web=Join-Path $output 'web'
 [void](Invoke-Dotnet 'publish-web' (@('publish','src/ArchSift.Web/ArchSift.Web.csproj','--no-restore','-c','Release','--self-contained','false','-p:UseAppHost=false','--output',$web)+$common))
 [IO.Directory]::CreateDirectory((Join-Path $web 'notices'))|Out-Null
 Copy-Item -LiteralPath (Join-Path $repo 'docs/migration/third-party-notices.md') -Destination (Join-Path $web 'notices/third-party-notices.md')
 Copy-Item -LiteralPath (Join-Path $repo 'docs/migration/licenses') -Destination (Join-Path $web 'notices/licenses') -Recurse
 & (Join-Path $PSScriptRoot 'New-WebPayloadInventory.ps1') -PayloadRoot $web -ExpectedVersion $version -SourceCommit $commit
 $packages=Join-Path $output 'packages';[IO.Directory]::CreateDirectory($packages)|Out-Null
 foreach($project in @('Contracts','Core','ArchUnit','Testing','Cli')){
  $arguments=@('pack',('src/ArchSift.'+$project+'/ArchSift.'+$project+'.csproj'),'--no-build','--no-restore','-c','Release','--output',$packages,('-p:SourceRevisionId='+$commit),('-p:ArchSiftWebPayloadRoot='+$web),('-p:ArchSiftPackageReleaseNotes='+(Join-Path $repo ('docs/releases/'+$version+'/RELEASE_NOTES.md'))))
  [void](Invoke-Dotnet ('pack-'+$project) $arguments)
 }
 $records=@(Get-ChildItem -LiteralPath $packages -File -Filter '*.nupkg'|ForEach-Object {@{file=$_.Name;length=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
 if($records.Count -ne 5){throw 'Expected the five matching product packages.'}
 Save 'package-build.json' @{schemaVersion=1;status=$(if($Development){'development-unqualified'}else{'built-qualification-pending'});version=$version;sourceCommit=$commit;sourceTree=$tree;sourceDirty=$dirty;sdk=$sdk;frameworkDependent=$true;distribution='NuGet-with-embedded-Web-and-project-template';publiclyPublished=$false;hostEqual=$true;packages=$records}
}finally{Check-Host}
