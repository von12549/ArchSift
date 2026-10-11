[CmdletBinding()]
param([string]$LabRoot='D:\ArchSift-lab',[string]$LocalFeed=(Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'),[string]$Version='0.6.0',[switch]$RequireCommittedSource)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lab=[IO.Path]::GetFullPath($LabRoot)
if($lab-eq$root-or$lab.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package output must be outside source.'}
if($Version-notmatch'^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$'){throw 'Invalid package version.'}
$productScope=@('src','schemas','templates','samples','scripts','.github','Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config','LICENSE','docs/migration','docs/package-readme.md','docs/installation.md','docs/installation-0.4.md','docs/installation-0.5.md','docs/setup-0.5.md','docs/setup.md','docs/cli.md','docs/changes.md','docs/reports.md','docs/rules.md','docs/build-inputs.md')
$sourceCommitBefore=(& git -C $root rev-parse HEAD).Trim()
function Product-Hash {
    $paths=((& git -C $root ls-files -z --cached --others --exclude-standard -- @productScope)-join "`n").Split([char]0,[StringSplitOptions]::RemoveEmptyEntries)
    $records=@($paths|Sort-Object|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash.ToLowerInvariant()}})
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($records|ConvertTo-Json -Compress)))).ToLowerInvariant()
}
$productHashBefore=Product-Hash
$productDirty=[bool](& git -C $root status --porcelain -- @productScope)
if($RequireCommittedSource-and$productDirty){throw 'Release product sources must be committed; unrelated documentation changes are preserved.'}
$run=Join-Path $lab ('packages/release-'+[Guid]::NewGuid().ToString('N'))
$payload=Join-Path $run 'payload'
[void][IO.Directory]::CreateDirectory($payload)
function State-Hash {
    $values=[ordered]@{}
    foreach($scope in @('User','Machine')){$items=[Environment]::GetEnvironmentVariables($scope);foreach($key in @($items.Keys)|Sort-Object){$values["$scope/$key"]=[string]$items[$key]}}
    $values['process-path']=[Environment]::GetEnvironmentVariable('Path')
    foreach($name in @('AllUsersAllHosts','AllUsersCurrentHost','CurrentUserAllHosts','CurrentUserCurrentHost')){$p=[string]$PROFILE.$name;$values[$name]=if([IO.File]::Exists($p)){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}else{'absent'}}
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($values|ConvertTo-Json -Compress)))).ToLowerInvariant()
}
$before=State-Hash
$checks=[Collections.Generic.List[object]]::new()
function Run-Dotnet([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command dotnet).Source;$start.WorkingDirectory=$root;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1';$start.Environment['NUGET_CERT_REVOCATION_MODE']='offline';$start.Environment['NUGET_PACKAGES']=(Join-Path $run 'nuget-cache');$start.Environment['MSBUILDDISABLENODEREUSE']='1'
    $p=[Diagnostics.Process]::Start($start);$out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync()
    try{if(-not$p.WaitForExit(180000)){$p.Kill($true);throw 'Publish command timed out.'};[Threading.Tasks.Task]::WaitAll($out,$err);[IO.File]::WriteAllText((Join-Path $run ($Name+'.stdout.log')),$out.Result);[IO.File]::WriteAllText((Join-Path $run ($Name+'.stderr.log')),$err.Result);$equal=(State-Hash)-ceq$before;$checks.Add([ordered]@{name=$Name;exitCode=$p.ExitCode;hostStateEqual=$equal});Write-Output ($Name+' exit='+$p.ExitCode+' host-state-equal='+$equal);if(-not$equal){throw 'Host state drift; no repair.'};if($p.ExitCode-ne0){Write-Output $out.Result;Write-Output $err.Result;throw 'Package build failed.'}}finally{$p.Dispose()}
}
try {
    foreach($project in @('Cli','Web','Setup')){
        $path='src/ArchSift.'+$project+'/ArchSift.'+$project+'.csproj'
        Run-Dotnet ('restore-'+$project) @('restore',$path,'--locked-mode','-r','win-x64','-p:RuntimeIdentifier=win-x64','-p:SelfContained=true','--configfile',(Join-Path $root 'NuGet.Config'),'--source',[IO.Path]::GetFullPath($LocalFeed),'--packages',(Join-Path $run 'nuget-cache'),'--disable-build-servers','-p:NuGetAudit=false')
        $destination=if($project-eq'Cli'){$payload}elseif($project-eq'Web'){Join-Path $payload 'web'}else{Join-Path $run 'setup'}
        $publishArguments=@('publish',$path,'--no-restore','-c','Release','-r','win-x64','--self-contained','true','--disable-build-servers','-p:UseSharedCompilation=false',('-p:Version='+$Version),'-o',$destination)
        if($project-eq'Setup'){
            $assets=Get-Content -LiteralPath (Join-Path $root 'src/ArchSift.Setup/obj/project.assets.json') -Raw|ConvertFrom-Json
            $runtimeDependency=@($assets.project.frameworks.'net10.0'.downloadDependencies|Where-Object name -eq 'Microsoft.NETCore.App.Runtime.win-x64')
            if($runtimeDependency.Count-ne1){throw 'Setup runtime identity unavailable.'}
            $runtimeRange=$runtimeDependency[0].version.Trim('[',']').Split(',')
            if($runtimeRange.Count-ne2-or$runtimeRange[0].Trim()-cne$runtimeRange[1].Trim()){throw 'Setup runtime version must be exact.'}
            $runtimeNotices=Join-Path (Join-Path (Join-Path $run 'nuget-cache') 'microsoft.netcore.app.runtime.win-x64') $runtimeRange[0].Trim()
            foreach($notice in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')){if(-not[IO.File]::Exists((Join-Path $runtimeNotices $notice))){throw 'Setup runtime notices missing.'}}
            $publishArguments+=@('-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true',('-p:SetupNoticesDirectory='+$runtimeNotices))
        }
        Run-Dotnet ('publish-'+$project) $publishArguments
    }
    $licenseRoot=Join-Path $payload 'licenses'
    [void][IO.Directory]::CreateDirectory($licenseRoot)
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $payload 'LICENSE')
    Copy-Item -LiteralPath (Join-Path $root 'docs/package-readme.md') -Destination (Join-Path $payload 'README.md')
    foreach($folder in @('schemas','templates','samples')){Copy-Item -LiteralPath (Join-Path $root $folder) -Destination (Join-Path $payload $folder) -Recurse}
    [void][IO.Directory]::CreateDirectory((Join-Path $payload 'docs'))
    foreach($file in @('cli.md','changes.md','reports.md','rules.md','build-inputs.md','installation.md','installation-0.4.md','installation-0.5.md','setup.md','setup-0.5.md')){Copy-Item -LiteralPath (Join-Path $root ('docs/'+$file)) -Destination (Join-Path $payload ('docs/'+$file))}
    [IO.File]::WriteAllText((Join-Path $payload 'setup-compatibility.json'),'{"schemaVersion":1,"configSchemaVersion":1,"librarySchemaVersion":1,"chainSchemaVersion":1}')
    Copy-Item -LiteralPath (Join-Path $root 'docs/migration/third-party-notices.md') -Destination (Join-Path $licenseRoot 'third-party-notices.md')
    Copy-Item -LiteralPath (Join-Path $root 'docs/migration/licenses') -Destination (Join-Path $licenseRoot 'libraries') -Recurse
    foreach($runtimeConfig in @((Join-Path $payload 'archsift.runtimeconfig.json'),(Join-Path $payload 'web/ArchSift.Web.runtimeconfig.json'))){
        $runtime=Get-Content -LiteralPath $runtimeConfig -Raw|ConvertFrom-Json
        foreach($framework in $runtime.runtimeOptions.includedFrameworks){
            $package=$framework.name.ToLowerInvariant()+'.runtime.win-x64'
            $directory=Join-Path (Join-Path (Join-Path $run 'nuget-cache') $package) $framework.version
            $notices=@(Get-ChildItem -LiteralPath $directory -File | Where-Object {$_.Name-match'(?i)license|third.party|notice'})
            if($notices.Count-lt2){throw 'Runtime license/notice payload incomplete.'}
            foreach($file in $notices){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $licenseRoot ($package+'-'+$framework.version+'-'+$file.Name))}
        }
    }
    $entries=@(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object {[ordered]@{path=[IO.Path]::GetRelativePath($payload,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
    $commit=(& git -C $root rev-parse HEAD).Trim();$dirty=[bool](& git -C $root status --porcelain)
    if($commit-cne$sourceCommitBefore-or(Product-Hash)-cne$productHashBefore){throw 'Product/commit identity changed during package build.'}
    if($RequireCommittedSource-and[bool](& git -C $root status --porcelain -- @productScope)){throw 'Product inputs changed during release build.'}
    [IO.File]::WriteAllText((Join-Path $payload 'package-manifest.json'),([ordered]@{schemaVersion=1;version=$Version;rid='win-x64';selfContained=$true;acceptanceStatus='candidate';sourceCommit=$commit;sourceDirty=$dirty;productSourceDirty=$productDirty;productInputsSha256=$productHashBefore;entries=$entries;manifestSelfHashOmitted=$true}|ConvertTo-Json -Depth 8))
    $zip=Join-Path $run ('archsift-'+$Version+'-win-x64.zip')
    [IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip)
    $setupExecutable=Join-Path $run 'setup/ArchSift.Setup.exe'
    $result=[ordered]@{zipPath=$zip;zipSha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant();setupPath=$setupExecutable;setupSha256=(Get-FileHash -LiteralPath $setupExecutable -Algorithm SHA256).Hash.ToLowerInvariant();payloadRoot=$payload;runRoot=$run;checks=$checks.ToArray();hostStateEqual=((State-Hash)-ceq$before);version=$Version;sourceCommit=$commit;sourceDirty=$dirty;productSourceDirty=$productDirty;acceptanceStatus='candidate'}
    [IO.File]::WriteAllText((Join-Path $run 'package-result.json'),($result|ConvertTo-Json -Depth 8));$result|ConvertTo-Json -Depth 8
} finally {Write-Output ('package-final-host-state-equal='+((State-Hash)-ceq$before))}
