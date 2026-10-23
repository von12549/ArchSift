[CmdletBinding()]
param(
    [string]$LabRoot='D:\ArchSift-lab',
    [string]$LocalFeed=(Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'),
    [string]$Version='',
    [string]$RunRoot='',
    [switch]$RequireCommittedSource,
    [switch]$PrepareLocks
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
. (Join-Path $PSScriptRoot 'HostState.ps1')
$release=Get-ArchSiftReleaseInputs $root
if(-not $Version){$Version=$release.version}
if($Version -cne $release.version){throw 'Publish must consume the central source version unchanged; use a reviewed version-cut commit.'}
if(-not(Test-Path -LiteralPath $LocalFeed -PathType Container)){throw 'Explicit offline source is missing.'}
$lab=[IO.Path]::GetFullPath($LabRoot)
if($lab-eq$root-or$lab.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package output must be outside source.'}
if($Version-notmatch'^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$'){throw 'Invalid package version.'}
$productScope=@('src','schemas','templates','samples','scripts','.github','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','LICENSE','docs/migration','docs/package-readme.md','README.md','RELEASE_NOTES.md','docs/getting-started.md','docs/project-integration.md','docs/architecture-test-workbench.md','docs/portable-workbench.md','docs/releases/0.10.1','docs/installation.md','docs/installation-0.4.md','docs/installation-0.5.md','docs/setup-0.5.md','docs/setup.md','docs/cli.md','docs/changes.md','docs/reports.md','docs/rules.md','docs/build-inputs.md','docs/external-build-evidence.md')
$sourceCommitBefore=(& git -C $root rev-parse HEAD).Trim()
function Product-Hash {
    $paths=((& git -C $root ls-files -z --cached --others --exclude-standard -- @productScope)-join "`n").Split([char]0,[StringSplitOptions]::RemoveEmptyEntries)
    $records=@($paths|Sort-Object|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash.ToLowerInvariant()}})
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($records|ConvertTo-Json -Compress)))).ToLowerInvariant()
}
$productHashBefore=Product-Hash
$productDirty=[bool](& git -C $root status --porcelain -- @productScope)
if($RequireCommittedSource-and$productDirty){throw 'Release product sources must be committed; unrelated documentation changes are preserved.'}
$run=if($RunRoot){Assert-ArchSiftExternalPath $root $RunRoot}else{Join-Path $lab ('packages/release-'+[Guid]::NewGuid().ToString('N'))}
if(Test-Path -LiteralPath $run){throw 'Preserve the existing publish run; select a fresh output directory.'}
$payload=Join-Path $run 'payload'
[void][IO.Directory]::CreateDirectory($payload)
function State-Hash { Get-ArchSiftHostHash }
$before=State-Hash
$checks=[Collections.Generic.List[object]]::new()
function Run-Dotnet([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command dotnet).Source;$start.WorkingDirectory=$root;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE']='false';$start.Environment['DOTNET_NOLOGO']='1';$start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1';$start.Environment['NUGET_CERT_REVOCATION_MODE']='offline';$start.Environment['NUGET_PACKAGES']=(Join-Path $run 'nuget-cache');$start.Environment['MSBUILDDISABLENODEREUSE']='1'
    $p=[Diagnostics.Process]::Start($start);$out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync()
    try{if(-not$p.WaitForExit(180000)){$p.Kill($true);throw 'Publish command timed out.'};[Threading.Tasks.Task]::WaitAll($out,$err);[IO.File]::WriteAllText((Join-Path $run ($Name+'.stdout.log')),$out.Result);[IO.File]::WriteAllText((Join-Path $run ($Name+'.stderr.log')),$err.Result);$equal=(State-Hash)-ceq$before;$checks.Add([ordered]@{name=$Name;exitCode=$p.ExitCode;hostStateEqual=$equal});Write-Output ($Name+' exit='+$p.ExitCode+' host-state-equal='+$equal);if(-not$equal){throw 'Host state drift; no repair.'};if($p.ExitCode-ne0){Write-Output $out.Result;Write-Output $err.Result;throw 'Package build failed.'}}finally{$p.Dispose()}
}
try {
    Run-Dotnet 'sdk' @('--version')
    $actualSdk=([IO.File]::ReadAllText((Join-Path $run 'sdk.stdout.log'))).Trim()
    if($actualSdk -cne $release.profile.sdk){throw 'Actual SDK differs from the reviewed release profile.'}
    foreach($project in @('Cli','Web','Setup')){
        $path='src/ArchSift.'+$project+'/ArchSift.'+$project+'.csproj'
        $restore=@('restore',$path,'-r','win-x64','-p:RuntimeIdentifier=win-x64','-p:SelfContained=true',('-p:RuntimeFrameworkVersion='+$release.profile.runtime),('-p:ArchSiftReleaseVersion='+$Version),'--configfile',(Join-Path $root 'NuGet.Config'),'--source',[IO.Path]::GetFullPath($LocalFeed),'--packages',(Join-Path $run 'nuget-cache'),'--disable-build-servers','-p:NuGetAudit=false')
        if($PrepareLocks){$restore+='--force-evaluate'}else{$restore+='--locked-mode'}
        Run-Dotnet ('restore-'+$project) $restore
        if($PrepareLocks){
            Run-Dotnet ('locked-'+$project) ($restore+'--locked-mode')
            continue
        }
        $destination=if($project-eq'Cli'){$payload}elseif($project-eq'Web'){Join-Path $payload 'web'}else{Join-Path $run 'setup'}
        Run-Dotnet ('apphost-'+$project) @('msbuild',$path,'-t:ResolveFrameworkReferences','-p:Configuration=Release','-p:RuntimeIdentifier=win-x64','-p:SelfContained=true',('-p:RuntimeFrameworkVersion='+$release.profile.runtime),('-p:ArchSiftReleaseVersion='+$Version),'-getProperty:AppHostSourcePath','-getItem:ResolvedAppHostPack')
        $resolvedHost=Get-Content -LiteralPath (Join-Path $run ('apphost-'+$project+'.stdout.log')) -Raw|ConvertFrom-Json
        $hostPacks=@($resolvedHost.Items.ResolvedAppHostPack)
        if($hostPacks.Count -ne 1 -or $hostPacks[0].RuntimeIdentifier -cne 'win-x64'){throw 'Resolved native apphost scope differs.'}
        $appHostPath=[IO.Path]::GetFullPath([string]$resolvedHost.Properties.AppHostSourcePath)
        $hostDirectory=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([string]$hostPacks[0].PackageDirectory))
        $hostPolicy=$release.profile.appHostTemplate
        if([IO.Path]::GetFileName($hostDirectory) -cne $hostPolicy.version -or $hostPolicy.version -cne $release.profile.runtime -or
            [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($hostDirectory)) -ine $hostPolicy.packageId -or
            ([string]$hostPacks[0].PathInPackage).Replace('\','/') -cne $hostPolicy.pathInPackage -or
            $appHostPath -ine [IO.Path]::GetFullPath((Join-Path $hostDirectory $hostPolicy.pathInPackage)) -or
            $appHostPath -ine [IO.Path]::GetFullPath([string]$hostPacks[0].Path)){throw 'Resolved native apphost package identity differs.'}
        if(-not(Test-Path -LiteralPath $appHostPath -PathType Leaf)){throw 'Resolved native apphost template is unavailable.'}
        $hostHash=(Get-FileHash -LiteralPath $appHostPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if($hostHash -cne $hostPolicy.sha256){throw 'Native apphost bytes differ from the reviewed official patched template.'}
        [IO.File]::WriteAllText((Join-Path $run ('apphost-'+$project+'-identity.json')),(@{version=$release.profile.runtime;path=$appHostPath;sha256=$hostHash;approvedOfficialCodeHashEqual=$true;unbrandedTemplateHasNoPeVersionResource=$true}|ConvertTo-Json))
        $publishArguments=@('publish',$path,'--no-restore','-c','Release','-r','win-x64','--self-contained','true','--disable-build-servers','-p:UseSharedCompilation=false',('-p:ArchSiftReleaseVersion='+$Version),('-p:RuntimeFrameworkVersion='+$release.profile.runtime),'-p:ContinuousIntegrationBuild=true',('-p:SourceRevisionId='+$sourceCommitBefore),'-o',$destination)
        if($project-eq'Setup'){
            $runtimeNotices=Join-Path (Join-Path (Join-Path $run 'nuget-cache') 'microsoft.netcore.app.runtime.win-x64') $release.profile.runtime
            foreach($notice in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')){if(-not[IO.File]::Exists((Join-Path $runtimeNotices $notice))){throw 'Setup runtime notices missing.'}}
            $publishArguments+=@('-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true',('-p:SetupNoticesDirectory='+$runtimeNotices))
        }
        Run-Dotnet ('publish-'+$project) $publishArguments
    }
    if($PrepareLocks){
        $prepared=@{status='RID locks prepared; commit reviewed lock changes before release build';version=$Version;sdk=$actualSdk;runtime=$release.profile.runtime;checks=$checks.ToArray();hostStateEqual=((State-Hash)-ceq$before)}
        [IO.File]::WriteAllText((Join-Path $run 'lock-preparation.json'),($prepared|ConvertTo-Json -Depth 10))
        $prepared|ConvertTo-Json -Depth 10
        return
    }
    $licenseRoot=Join-Path $payload 'licenses'
    [void][IO.Directory]::CreateDirectory($licenseRoot)
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $payload 'LICENSE')
    foreach($folder in @('schemas','templates')){Copy-Item -LiteralPath (Join-Path $root $folder) -Destination (Join-Path $payload $folder) -Recurse}
    & (Join-Path $PSScriptRoot 'Copy-ReleaseDocumentation.ps1') -PayloadRoot $payload -SourceCommit $sourceCommitBefore -Version $Version
    [IO.File]::WriteAllText((Join-Path $payload 'setup-compatibility.json'),'{"schemaVersion":1,"configSchemaVersion":1,"librarySchemaVersion":1,"chainSchemaVersion":1}')
    foreach($runtimeConfig in @((Join-Path $payload 'archsift.runtimeconfig.json'),(Join-Path $payload 'web/ArchSift.Web.runtimeconfig.json'))){
        $runtime=Get-Content -LiteralPath $runtimeConfig -Raw|ConvertFrom-Json
        foreach($framework in $runtime.runtimeOptions.includedFrameworks){
            if($framework.version -cne $release.profile.runtime){throw 'Bundled runtime differs from the release profile.'}
            $package=$framework.name.ToLowerInvariant()+'.runtime.win-x64'
            $directory=Join-Path (Join-Path (Join-Path $run 'nuget-cache') $package) $framework.version
            $notices=@(Get-ChildItem -LiteralPath $directory -File | Where-Object {$_.Name-match'(?i)license|third.party|notice'})
            if($notices.Count-lt2){throw 'Runtime license/notice payload incomplete.'}
            foreach($file in $notices){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $licenseRoot ($package+'-'+$framework.version+'-'+$file.Name))}
        }
    }
    $webNotices=Join-Path $payload 'web/notices'
    [void][IO.Directory]::CreateDirectory($webNotices)
    & (Join-Path $PSScriptRoot 'Write-ReleaseNotices.ps1') -OutputPath (Join-Path $webNotices 'third-party-notices.md') -SourceCommit $sourceCommitBefore
    Copy-Item -LiteralPath (Join-Path $root 'docs/migration/licenses') -Destination (Join-Path $webNotices 'licenses') -Recurse
    & (Join-Path $PSScriptRoot 'New-WebPayloadInventory.ps1') -PayloadRoot (Join-Path $payload 'web') -ExpectedVersion $Version -SourceCommit $sourceCommitBefore
    . (Join-Path $PSScriptRoot 'ReleaseRuntimeProof.ps1')
    $runtimeProof=@(Get-ArchSiftRuntimeCodeProof $payload (Join-Path $run 'nuget-cache') $release)
    [IO.File]::WriteAllText((Join-Path $run 'runtime-code-proof.json'),($runtimeProof|ConvertTo-Json -Depth 10))
    $entries=@(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object {[ordered]@{path=[IO.Path]::GetRelativePath($payload,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
    $commit=(& git -C $root rev-parse HEAD).Trim();$dirty=[bool](& git -C $root status --porcelain)
    if($commit-cne$sourceCommitBefore-or(Product-Hash)-cne$productHashBefore){throw 'Product/commit identity changed during package build.'}
    if($RequireCommittedSource-and[bool](& git -C $root status --porcelain -- @productScope)){throw 'Product inputs changed during release build.'}
    [IO.File]::WriteAllText((Join-Path $payload 'package-manifest.json'),([ordered]@{schemaVersion=1;version=$Version;rid='win-x64';selfContained=$true;acceptanceStatus='candidate';sourceCommit=$commit;sourceDirty=$dirty;productSourceDirty=$productDirty;productInputsSha256=$productHashBefore;entries=$entries;manifestSelfHashOmitted=$true}|ConvertTo-Json -Depth 8))
    $zip=Join-Path $run (Get-ArchSiftReleaseArchiveName $Version)
    [IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip)
    $setupExecutable=Join-Path $run 'setup/ArchSift.Setup.exe'
    $result=[ordered]@{zipPath=$zip;zipSha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant();setupPath=$setupExecutable;setupSha256=(Get-FileHash -LiteralPath $setupExecutable -Algorithm SHA256).Hash.ToLowerInvariant();payloadRoot=$payload;runRoot=$run;checks=$checks.ToArray();hostStateEqual=((State-Hash)-ceq$before);version=$Version;sdk=$actualSdk;runtime=$release.profile.runtime;sourceCommit=$commit;sourceDirty=$dirty;productSourceDirty=$productDirty;acceptanceStatus='candidate'}
    [IO.File]::WriteAllText((Join-Path $run 'package-result.json'),($result|ConvertTo-Json -Depth 8));$result|ConvertTo-Json -Depth 8
} finally {Write-Output ('package-final-host-state-equal='+((State-Hash)-ceq$before))}
