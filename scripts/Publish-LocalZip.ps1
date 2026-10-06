[CmdletBinding()]
param([string]$LabRoot='D:\ArchSift-lab',[string]$LocalFeed=(Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'))
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lab=[IO.Path]::GetFullPath($LabRoot)
if($lab-eq$root-or$lab.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package output must be outside source.'}
$run=Join-Path $lab ('packages/w08-'+[Guid]::NewGuid().ToString('N'))
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
    foreach($project in @('Cli','Web')){
        $path='src/ArchSift.'+$project+'/ArchSift.'+$project+'.csproj'
        Run-Dotnet ('restore-'+$project) @('restore',$path,'-r','win-x64','-p:RuntimeIdentifier=win-x64','-p:SelfContained=true','--configfile',(Join-Path $root 'NuGet.Config'),'--source',[IO.Path]::GetFullPath($LocalFeed),'--packages',(Join-Path $run 'nuget-cache'),'--disable-build-servers','-p:NuGetAudit=false')
        $destination=if($project-eq'Cli'){$payload}else{Join-Path $payload 'web'}
        Run-Dotnet ('publish-'+$project) @('publish',$path,'--no-restore','-c','Release','-r','win-x64','--self-contained','true','--disable-build-servers','-p:UseSharedCompilation=false','-o',$destination)
    }
    $licenseRoot=Join-Path $payload 'licenses'
    [void][IO.Directory]::CreateDirectory($licenseRoot)
    Copy-Item -LiteralPath (Join-Path $root 'docs/migration/third-party-notices.md') -Destination (Join-Path $licenseRoot 'third-party-notices.md')
    Copy-Item -LiteralPath (Join-Path $root 'docs/migration/licenses') -Destination (Join-Path $licenseRoot 'libraries') -Recurse
    foreach($package in @('microsoft.netcore.app.runtime.win-x64','microsoft.aspnetcore.app.runtime.win-x64')){
        $directory=Join-Path (Join-Path (Join-Path $run 'nuget-cache') $package) '10.0.11'
        foreach($file in Get-ChildItem -LiteralPath $directory -File | Where-Object {$_.Name-match'(?i)license|third.party|notice'}){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $licenseRoot ($package+'-'+$file.Name))}
    }
    $entries=@(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object {[ordered]@{path=[IO.Path]::GetRelativePath($payload,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
    [IO.File]::WriteAllText((Join-Path $payload 'package-manifest.json'),([ordered]@{schemaVersion=1;version='0.1.0-dev';rid='win-x64';selfContained=$true;w08Accepted=$false;entries=$entries;manifestSelfHashOmitted=$true}|ConvertTo-Json -Depth 8))
    $zip=Join-Path $run 'archsift-0.1.0-dev-win-x64.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip)
    $result=[ordered]@{zipPath=$zip;zipSha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant();payloadRoot=$payload;runRoot=$run;checks=$checks.ToArray();hostStateEqual=((State-Hash)-ceq$before);w08Accepted=$false}
    [IO.File]::WriteAllText((Join-Path $run 'package-result.json'),($result|ConvertTo-Json -Depth 8));$result|ConvertTo-Json -Depth 8
} finally {Write-Output ('package-final-host-state-equal='+((State-Hash)-ceq$before))}
