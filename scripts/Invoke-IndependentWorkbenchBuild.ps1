[CmdletBinding()]
param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$SourceCommit,[Parameter(Mandatory)][string]$OutputRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath($SourceRoot);$out=[IO.Path]::GetFullPath($OutputRoot)
if($SourceCommit -cnotmatch '^[a-f0-9]{40}$' -or (Test-Path $out)){throw 'Pinned source and new output directory required.'}
if((& git -C $source rev-parse HEAD).Trim() -cne $SourceCommit -or @(& git -C $source status --porcelain).Count){throw 'Pinned product source must be clean.'}
. (Join-Path $source 'scripts/HostState.ps1')
$parentBefore=Get-ArchSiftHostHash
[void][IO.Directory]::CreateDirectory($out)
function Save([string]$Path,$Value){[IO.File]::WriteAllText($Path,(($Value|ConvertTo-Json -Depth 30)+"`n"),[Text.UTF8Encoding]::new($false))}
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
$http=[Net.Http.HttpClient]::new();$http.Timeout=[TimeSpan]::FromMinutes(5)
try{
    $metadataUrl='https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $raw=$http.GetByteArrayAsync($metadataUrl).GetAwaiter().GetResult();[IO.File]::WriteAllBytes((Join-Path $out 'official-dotnet-releases.json'),$raw)
    $metadata=[Text.Encoding]::UTF8.GetString($raw)|ConvertFrom-Json
    $tools=Join-Path $out 'private-tools';[void][IO.Directory]::CreateDirectory($tools)
    $sdkProof=@(foreach($version in @('10.0.303','10.0.402')){
        $matches=@($metadata.releases|ForEach-Object{$_.sdks}|Where-Object version -CEQ $version|ForEach-Object{$_.files}|Where-Object{$_.rid -ceq 'win-x64' -and $_.name.EndsWith('.zip')}|Sort-Object url -Unique)
        if($matches.Count -ne 1){throw 'Ambiguous official SDK archive.'};$entry=$matches[0]
        $zip=Join-Path $out ('dotnet-sdk-'+$version+'-win-x64.zip');[IO.File]::WriteAllBytes($zip,$http.GetByteArrayAsync($entry.url).GetAwaiter().GetResult())
        $actual=(Get-FileHash -LiteralPath $zip -Algorithm SHA512).Hash.ToLowerInvariant()
        if($actual -cne $entry.hash.ToLowerInvariant()){throw 'Official SDK archive SHA512 differs.'}
        if($version -ceq '10.0.402'){
            $profile=Get-Content (Join-Path $source 'docs/releases/0.10.0/build-profile.json') -Raw|ConvertFrom-Json
            if($actual -cne $profile.appHostTemplate.providerSdkArchiveSha512){throw 'Runtime provider differs from reviewed profile.'}
        }
        [IO.Compression.ZipFile]::ExtractToDirectory($zip,$tools,$true)
        @{version=$version;url=$entry.url;sha512=$actual;archiveSha256=(Hash $zip);installedToPrivateDirectory=$true}
    })
    $feed=Join-Path $out 'explicit-build-source';[void][IO.Directory]::CreateDirectory($feed)
    $approved=Get-Content (Join-Path $PSScriptRoot 'independent-build-feed.json') -Raw|ConvertFrom-Json
    if($approved.packages.Count -ne 47){throw 'Reviewed build package count differs.'}
    foreach($entry in $approved.packages){
        $file=Join-Path $feed $entry.file;[IO.File]::WriteAllBytes($file,$http.GetByteArrayAsync($entry.url).GetAwaiter().GetResult())
        if((Hash $file) -cne $entry.sha256){throw ('Explicit build input differs: '+$entry.file)}
    }
    $parentAfterPreparation=Get-ArchSiftHostHash
    if($parentAfterPreparation -cne $parentBefore){throw 'Parent host changed during private tool preparation.'}
    Save (Join-Path $out 'preparation.json') @{sourceCommit=$SourceCommit;sourceTree=(& git -C $source rev-parse 'HEAD^{tree}').Trim();sdkArchives=$sdkProof;officialMetadataUrl=$metadataUrl;officialMetadataSha256=(Hash (Join-Path $out 'official-dotnet-releases.json'));packages=$approved.packages;parentHostBefore=$parentBefore;parentHostAfter=$parentAfterPreparation;hostEqual=$true;persistedEnvironmentChanged=$false}
    $build=Join-Path $out 'build'
    $childScript=Join-Path $out 'build-child.ps1'
    [IO.File]::WriteAllText($childScript,@'
param([string]$Source,[string]$Output,[string]$Feed)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $Source 'scripts/HostState.ps1')
$before=Get-ArchSiftHostHash
try{& (Join-Path $Source 'scripts/Build-WorkbenchRelease.ps1') -OutputRoot $Output -LocalFeed $Feed -ExpectedHostHash $before}
finally{if((Get-ArchSiftHostHash) -cne $before){throw 'Independent build child host drift.'}}
'@,[Text.UTF8Encoding]::new($false))
    $psi=[Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME 'pwsh.exe'));$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
    $psi.StandardOutputEncoding=[Text.UTF8Encoding]::new($false,$true);$psi.StandardErrorEncoding=[Text.UTF8Encoding]::new($false,$true)
    foreach($arg in @('-NoProfile','-NonInteractive','-File',$childScript,'-Source',$source,'-Output',$build,'-Feed',$feed)){$psi.ArgumentList.Add($arg)}
    $psi.Environment['PATH']=(@($PSHOME,$tools)+@($env:PATH.Split([IO.Path]::PathSeparator)|Where-Object{-not $_.TrimEnd('\').Equals($PSHOME,[StringComparison]::OrdinalIgnoreCase)})) -join [IO.Path]::PathSeparator
    foreach($name in @('DOTNET_ROOT','DOTNET_ROOT_X64')){$psi.Environment[$name]=$tools}
    $psi.Environment['DOTNET_HOST_PATH']=Join-Path $tools 'dotnet.exe';$psi.Environment['DOTNET_MULTILEVEL_LOOKUP']='0';$psi.Environment['DOTNET_CLI_HOME']=Join-Path $out 'outer-cli-home';$psi.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $p=[Diagnostics.Process]::Start($psi)
    try{
        $stdout=$p.StandardOutput.ReadToEndAsync();$stderr=$p.StandardError.ReadToEndAsync()
        if(-not $p.WaitForExit(1200000)){$p.Kill($true);$p.WaitForExit();throw 'Independent build timed out.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $out 'build.stdout.log'),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $out 'build.stderr.log'),$stderr.Result,[Text.UTF8Encoding]::new($false))
        if($p.ExitCode -ne 0){throw 'Independent product build stopped; retain logs.'}
    }finally{$p.Dispose()}
    $result=Get-Content (Join-Path $build 'build-result.json') -Raw|ConvertFrom-Json
    if($result.sourceCommit -cne $SourceCommit -or -not $result.hostEqual){throw 'Independent build source/host mismatch.'}
    # Export exact assets and evidence without redundant development caches/tools.
    $transfer=Join-Path $out 'transfer';[void][IO.Directory]::CreateDirectory($transfer)
    foreach($file in Get-ChildItem -LiteralPath $build -Recurse -File){
        $relative=[IO.Path]::GetRelativePath($build,$file.FullName);$parts=$relative.Split([IO.Path]::DirectorySeparatorChar)
        if($parts[0] -in @('build-cache','build-home','closure-discovery-cache','consumer-cold-cache','consumer-moved-cache','moved sources')){continue}
        if($parts[0] -ceq 'windows' -and ($parts[1] -in @('payload','setup') -or $file.Extension -ceq '.zip')){continue}
        $destination=Join-Path $transfer $relative;[void][IO.Directory]::CreateDirectory((Split-Path $destination));[IO.File]::Copy($file.FullName,$destination,$false)
        if((Hash $destination) -cne (Hash $file.FullName)){throw 'Transfer copy changed.'}
    }
    Save (Join-Path $out 'build-transfer.json') @{status='built-not-release-accepted';sourceCommit=$SourceCommit;sourceTree=(& git -C $source rev-parse 'HEAD^{tree}').Trim();version=$result.version;originalBuildRoot=$build;transferRoot=$transfer;parentHostEqual=((Get-ArchSiftHostHash)-ceq$parentBefore);privateSdkOnly=$true;assets=@(Get-ChildItem (Join-Path $transfer 'assets') -File|ForEach-Object{@{file=$_.Name;bytes=$_.Length;sha256=(Hash $_.FullName)}})}
    Write-Output ('independent-build=passed source='+$SourceCommit+' version='+$result.version)
}catch{Save (Join-Path $out 'failure.json') @{status='stopped';message=$_.Exception.Message;hostEqual=((Get-ArchSiftHostHash)-ceq$parentBefore)};throw}
finally{$http.Dispose();if((Get-ArchSiftHostHash)-cne$parentBefore){throw 'Independent parent host drift; no repair.'}}
