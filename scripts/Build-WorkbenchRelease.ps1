[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][string]$LocalFeed,
    [string]$ExpectedHostHash=''
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if(-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [Runtime.InteropServices.Architecture]::X64){throw 'This release profile requires Windows x64.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
. (Join-Path $PSScriptRoot 'HostState.ps1')
$release=Get-ArchSiftReleaseInputs $repo
$root=Assert-ArchSiftExternalPath $repo $OutputRoot
$feed=[IO.Path]::GetFullPath($LocalFeed)
Assert-ArchSiftReleaseNoLinks $feed
if(-not(Test-Path -LiteralPath $feed -PathType Container)){throw 'Explicit offline build source is missing.'}
if(Test-Path -LiteralPath $root){throw 'Never overwrite a release build attempt; use a fresh output root.'}
if(@(& git -C $repo status --porcelain=v1).Count){throw 'A clean committed source checkout is required, including release tooling, documentation and reviewed RID locks.'}
$commit=(& git -C $repo rev-parse HEAD).Trim()
$tree=(& git -C $repo rev-parse 'HEAD^{tree}').Trim()
if($commit -notmatch '^[a-f0-9]{40}$' -or $tree -notmatch '^[a-f0-9]{40}$'){throw 'Actual source commit/tree unavailable.'}
$before=Get-ArchSiftHostHash
if($ExpectedHostHash -and $before -cne $ExpectedHostHash){throw 'Host baseline differs; stop without repair.'}
[void][IO.Directory]::CreateDirectory($root)
$assets=Join-Path $root 'assets'
$packages=Join-Path $root 'product-packages'
$cache=Join-Path $root 'build-cache'
foreach($directory in @($assets,$packages,$cache)){[void][IO.Directory]::CreateDirectory($directory)}
function Save([string]$Path,$Value){[IO.File]::WriteAllText($Path,((ConvertTo-Json -InputObject $Value -Depth 35)+"`n"),[Text.UTF8Encoding]::new($false))}
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
$paths=@(& git -C $repo ls-files --cached)
$sourceInputs=@(foreach($relative in ($paths|Sort-Object)){@{path=$relative;sha256=(Hash (Join-Path $repo $relative))}})
Save (Join-Path $root 'source-inputs.json') $sourceInputs
Save (Join-Path $root 'host-before.json') @{sha256=$before}
$checks=[Collections.Generic.List[object]]::new()
function Invoke-ReleaseDotnet([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -ErrorAction Stop).Source)
    $start.WorkingDirectory=$repo;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $root 'build-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='true'
    $start.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE']='false'
    $start.Environment['DOTNET_NOLOGO']='1'
    $start.Environment['NUGET_PACKAGES']=$cache
    $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
    $start.Environment['MSBUILDDISABLENODEREUSE']='1'
    $process=[Diagnostics.Process]::Start($start)
    try{
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(600000)){$process.Kill($true);$process.WaitForExit();throw 'Owned release build process timed out.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $root ($Name+'.stdout.log')),$stdout.Result,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $root ($Name+'.stderr.log')),$stderr.Result,[Text.UTF8Encoding]::new($false))
        $equal=(Get-ArchSiftHostHash) -ceq $before
        $checks.Add(@{name=$Name;arguments=$Arguments;exitCode=$process.ExitCode;hostEqual=$equal})
        Save (Join-Path $root 'checks.json') $checks.ToArray()
        Write-Output ("release-$Name exit="+$process.ExitCode+" host-equal=$equal")
        if(-not $equal){throw 'Host drift; preserve all evidence without repair.'}
        if($process.ExitCode -ne 0){throw ("Release $Name failed; inspect retained stdout/stderr.")}
    }finally{$process.Dispose()}
}
$completed=$false
try{
    Invoke-ReleaseDotnet 'sdk' @('--version')
    if(([IO.File]::ReadAllText((Join-Path $root 'sdk.stdout.log'))).Trim() -cne $release.profile.sdk){throw 'Actual SDK differs from release profile.'}
    Invoke-ReleaseDotnet 'shared-runtimes' @('--list-runtimes')
    $runtimes=[IO.File]::ReadAllText((Join-Path $root 'shared-runtimes.stdout.log'))
    if($runtimes -notmatch '(?m)^Microsoft.NETCore.App 10\.0\.13 \[' -or $runtimes -notmatch '(?m)^Microsoft.AspNetCore.App 10\.0\.13 \['){throw 'Qualification requires the reviewed patched shared runtimes; provision isolated tools without repairing the system.'}
    $common=@(('-p:ArchSiftReleaseVersion='+$release.version),('-p:SourceRevisionId='+$commit),'-p:ContinuousIntegrationBuild=true','-p:UseSharedCompilation=false')
    Invoke-ReleaseDotnet 'restore' (@('restore','ArchSift.slnx','--locked-mode','--configfile',(Join-Path $repo 'NuGet.Config'),'--source',$feed,'--packages',$cache,'--disable-parallel','--disable-build-servers','-p:NuGetAudit=false')+$common)
    Invoke-ReleaseDotnet 'build' (@('build','ArchSift.slnx','--no-restore','-c','Release','--disable-build-servers','-m:1','-v:minimal')+$common)
    $readme=[IO.File]::ReadAllText((Join-Path $repo 'docs/package-readme.md'))
    $readme=[regex]::Replace($readme,'\A#[^\r\n]+',('# ArchSift Workbench '+$release.version))
    $readme=$readme.Replace('(portable-workbench.md)',('(https://github.com/von12549/ArchSift/blob/'+$commit+'/docs/portable-workbench.md)'))
    $readme=$readme+"`nPackage version: $($release.version). Source commit: $commit. Final acceptance is recorded separately for exact asset bytes.`n"
    $readme+="`n[Version-matched project integration](https://github.com/von12549/ArchSift/blob/$commit/docs/project-integration.md).`n"
    $notes=[IO.File]::ReadAllText((Join-Path $repo 'RELEASE_NOTES.md')).Replace('# ArchSift Workbench 0.10.1',('# ArchSift Workbench '+$release.version))
    $notes+="`nPackaging source: $commit. Artifact version: $($release.version).`n"
    $readmePath=Join-Path $root 'package-README.md';$notesPath=Join-Path $root 'package-RELEASE_NOTES.md'
    [IO.File]::WriteAllText($readmePath,$readme,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($notesPath,$notes,[Text.UTF8Encoding]::new($false))
    $noticePath=Join-Path $root 'package-third-party-notices.md'
    & (Join-Path $PSScriptRoot 'Write-ReleaseNotices.ps1') -OutputPath $noticePath -SourceCommit $commit
    $webStage=Join-Path $root 'web-publish'
    Invoke-ReleaseDotnet 'publish-web-fdd' (@('publish','src/ArchSift.Web/ArchSift.Web.csproj','--no-restore','-c','Release','--self-contained','false','-p:UseAppHost=false',('-p:RuntimeFrameworkVersion='+$release.profile.runtime),'--disable-build-servers','--output',$webStage)+$common)
    $webNotices=Join-Path $webStage 'notices'
    [void][IO.Directory]::CreateDirectory($webNotices)
    Copy-Item -LiteralPath $noticePath -Destination (Join-Path $webNotices 'third-party-notices.md')
    Copy-Item -LiteralPath (Join-Path $repo 'docs/migration/licenses') -Destination (Join-Path $webNotices 'licenses') -Recurse
    foreach($folder in @('schemas','templates')){Copy-Item -LiteralPath (Join-Path $repo $folder) -Destination (Join-Path $webStage $folder) -Recurse}
    & (Join-Path $PSScriptRoot 'Copy-ReleaseDocumentation.ps1') -PayloadRoot $webStage -SourceCommit $commit -Version $release.version
    & (Join-Path $PSScriptRoot 'New-WebPayloadInventory.ps1') -PayloadRoot $webStage -ExpectedVersion $release.version -SourceCommit $commit
    foreach($id in $release.profile.productPackages){
        Invoke-ReleaseDotnet ('pack-'+$id) (@('pack',('src/'+$id+'/'+$id+'.csproj'),'--no-build','--no-restore','-c','Release','--output',$packages,'--disable-build-servers',('-p:RepositoryCommit='+$commit),('-p:ArchSiftWebPayloadRoot='+$webStage),('-p:ArchSiftPackageDocumentationRoot='+$webStage),('-p:ArchSiftPackageReadme='+$readmePath),('-p:ArchSiftPackageReleaseNotes='+$notesPath),('-p:ArchSiftPackageNotices='+$noticePath))+$common)
        Copy-Item -LiteralPath (Join-Path $packages ($id+'.'+$release.version+'.nupkg')) -Destination $assets
    }
    $seed=Join-Path $root 'closure-discovery'
    & (Join-Path $PSScriptRoot 'Test-WorkbenchConsumer.ps1') -FixtureRoot $seed -OfflineSources @($packages,$feed) -PackageCache (Join-Path $root 'closure-discovery-cache') -ExpectedHostHash $before *> (Join-Path $root 'closure-discovery.log')
    & (Join-Path $PSScriptRoot 'New-WorkbenchOfflineFeed.ps1') -ProductPackages $packages -ConsumerLock (Join-Path $seed 'target/packages.lock.json') -ConsumerCache (Join-Path $root 'closure-discovery-cache') -OutputDirectory (Join-Path $root 'offline-feed') -SourceCommit $commit -ArchivePath (Join-Path $assets ('archsift-workbench-'+$release.version+'-offline-feed.zip')) *> (Join-Path $root 'offline-feed.log')
    $minimalFeed=Join-Path $root 'offline-feed'
    & (Join-Path $PSScriptRoot 'Test-WorkbenchNuGetWeb.ps1') -OutputRoot (Join-Path $root 'cold-nuget-web') -OfflineFeed $minimalFeed -Dotnet ((Get-Command dotnet -ErrorAction Stop).Source) -ExpectedHostHash $before *> (Join-Path $root 'cold-nuget-web.log')
    $cold=Join-Path $root 'consumer cold'
    & (Join-Path $PSScriptRoot 'Test-WorkbenchConsumer.ps1') -FixtureRoot $cold -OfflineSources @($minimalFeed) -PackageCache (Join-Path $root 'consumer-cold-cache') -ExpectedHostHash $before *> (Join-Path $root 'consumer-cold.log')
    $movedFeed=Join-Path $root 'moved sources/approved generic source'
    [void][IO.Directory]::CreateDirectory($movedFeed)
    foreach($file in Get-ChildItem -LiteralPath $minimalFeed -Recurse -File){
        $relative=[IO.Path]::GetRelativePath($minimalFeed,$file.FullName)
        $destination=Join-Path $movedFeed $relative
        [void][IO.Directory]::CreateDirectory((Split-Path $destination))
        [IO.File]::Copy($file.FullName,$destination,$false)
        if((Hash $destination) -cne (Hash $file.FullName)){throw 'Moved feed differs.'}
    }
    $moved=Join-Path $root 'deeper level/consumer moved'
    & (Join-Path $PSScriptRoot 'Test-WorkbenchConsumer.ps1') -FixtureRoot $moved -OfflineSources @($movedFeed) -PackageCache (Join-Path $root 'consumer-moved-cache') -ExpectedHostHash $before *> (Join-Path $root 'consumer-moved.log')
    $coldResult=Get-Content (Join-Path $cold 'qualification.json') -Raw|ConvertFrom-Json
    $movedResult=Get-Content (Join-Path $moved 'qualification.json') -Raw|ConvertFrom-Json
    if($coldResult.status -cne 'passed' -or $movedResult.status -cne 'passed' -or $coldResult.requiredCasesSha256 -cne $movedResult.requiredCasesSha256 -or $coldResult.policySha256 -cne $movedResult.policySha256){throw 'Cold/moved qualification changed the approved logical baseline/policy.'}
    & (Join-Path $PSScriptRoot 'Publish-LocalZip.ps1') -LocalFeed $feed -RunRoot (Join-Path $root 'windows') -RequireCommittedSource *> (Join-Path $root 'windows.log')
    $native=Get-Content (Join-Path $root 'windows/package-result.json') -Raw|ConvertFrom-Json
    if($native.version -cne $release.version -or $native.sourceCommit -cne $commit -or $native.productSourceDirty -or -not $native.hostStateEqual){throw 'Native publish identity differs.'}
    Copy-Item -LiteralPath $native.zipPath -Destination $assets
    Copy-Item -LiteralPath $native.setupPath -Destination (Join-Path $assets 'ArchSift.Setup.exe')
    & (Join-Path $PSScriptRoot 'Test-WorkbenchArtifacts.ps1') -ProductPackages $packages -NativeResult (Join-Path $root 'windows/package-result.json') -OutputDirectory (Join-Path $root 'artifact-verification') -SourceCommit $commit *> (Join-Path $root 'artifact-verification.log')
    if((& git -C $repo rev-parse HEAD).Trim() -cne $commit -or @(& git -C $repo status --porcelain=v1).Count){throw 'Source/commit changed during build.'}
    foreach($entry in $sourceInputs){if((Hash (Join-Path $repo $entry.path)) -cne $entry.sha256){throw ('Source byte changed during build: '+$entry.path)}}
    $records=@(Get-ChildItem -LiteralPath $assets -File|Sort-Object Name|ForEach-Object{@{file=$_.Name;bytes=$_.Length;sha256=(Hash $_.FullName)}})
    $manifest=@{schemaVersion=1;product='ArchSift';displayName='ArchSift Workbench';version=$release.version;packagingState='candidate';acceptanceAuthority='acceptance.json supplied after mandatory release gates';sourceCommit=$commit;sourceTree=$tree;sourceDirty=$false;sourceInputManifestSha256=(Hash (Join-Path $root 'source-inputs.json'));sdk=$release.profile.sdk;runtime=$release.profile.runtime;tfm='net10.0';configuration='Release';rid='win-x64';selfContained=$true;networkRestore=$false;assets=$records;genericConsumer=@{coldCases=4;movedCases=4;failed=0;skipped=0;logicalBaselineAndPolicyByteEqual=$true};vulnerabilityAudit='pending separate explicit audit';independentWindows='pending';independentHuman='pending-user-only';published=$false}
    Save (Join-Path $assets 'artifact-manifest.json') $manifest
    $sums=@(Get-ChildItem -LiteralPath $assets -File|Sort-Object Name|ForEach-Object{(Hash $_.FullName)+'  '+$_.Name}) -join "`n"
    [IO.File]::WriteAllText((Join-Path $assets 'SHA256SUMS.txt'),$sums+"`n",[Text.UTF8Encoding]::new($false))
    Save (Join-Path $root 'build-result.json') @{status='candidate-build-and-generic-consumption-passed';version=$release.version;sourceCommit=$commit;assetsRoot=$assets;nativePayload=$native.payloadRoot;hostEqual=((Get-ArchSiftHostHash)-ceq$before);finalAcceptance=$false;published=$false;remaining='Full regression/security/license/real IFX/Web/lifecycle/independent Windows/final stable and user gates remain required.'}
    $completed=$true
    Write-Output ('workbench-build-complete version='+$release.version+' source='+$commit+' final-acceptance=False')
}finally{
    $after=Get-ArchSiftHostHash
    Save (Join-Path $root 'host-final.json') @{before=$before;after=$after;equal=($before -ceq $after);completed=$completed}
    if($before -cne $after){throw 'Final release-build host drift; preserve evidence and stop without repair.'}
}
