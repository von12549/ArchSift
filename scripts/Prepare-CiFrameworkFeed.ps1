# Explicit online prerequisite acquisition for disposable CI only. Product restore remains offline.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Feed)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$feedPath=[IO.Path]::GetFullPath($Feed)
if($feedPath.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'CI feed must be outside source.'}
$work=Join-Path ([IO.Path]::GetDirectoryName($feedPath)) ('framework-prerequisites-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($work)
function Invoke-CiDotnet([string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command dotnet).Source;$start.WorkingDirectory=$work;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $work 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $p=[Diagnostics.Process]::Start($start);$out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync()
    try{if(-not$p.WaitForExit(180000)){$p.Kill($true);throw 'CI prerequisite timeout.'};[Threading.Tasks.Task]::WaitAll($out,$err);if($p.ExitCode-ne0){throw ($err.Result+$out.Result)};if((Get-ArchSiftHostHash)-cne$before){throw 'Host drift, no repair.'};return $out.Result}finally{$p.Dispose()}
}
try{
    $sdks=Invoke-CiDotnet @('--list-sdks')
    $match=[regex]::Match($sdks,'(?m)^9\.0\.314 \[(.+)\]\r?$')
    if(-not$match.Success){throw 'Required SDK 9.0.314 not installed.'}
    $metadata=Join-Path $match.Groups[1].Value '9.0.314/Microsoft.NETCoreSdk.BundledVersions.props'
    [xml]$known=Get-Content -LiteralPath $metadata -Raw
    $packs=@{}
    foreach($reference in $known.SelectNodes('//KnownFrameworkReference')){
        if($reference.TargetFramework-notin@('net8.0','net9.0')){continue}
        $name=[string]$reference.TargetingPackName;$version=[string]$reference.TargetingPackVersion
        if($name-and$version){if($version-notmatch'^\d+\.\d+\.\d+$'){throw 'Nonliteral SDK pack metadata.'};$packs[$name+'/'+$version]=@{name=$name;version=$version}}
    }
    if($packs.Count-lt4){throw 'Incomplete framework prerequisite metadata.'}
    $items=($packs.Values|Sort-Object name,version|ForEach-Object{'<PackageDownload Include="'+$_.name+'" Version="['+$_.version+']" />'})-join''
    [IO.File]::WriteAllText((Join-Path $work 'global.json'),'{"sdk":{"version":"9.0.314","rollForward":"disable","allowPrerelease":false}}')
    [IO.File]::WriteAllText((Join-Path $work 'Warm.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup><ItemGroup>'+$items+'</ItemGroup></Project>')
    [IO.File]::WriteAllText((Join-Path $work 'NuGet.Config'),'<configuration><packageSources><clear /></packageSources></configuration>')
    $output=Invoke-CiDotnet @('restore','Warm.csproj','--configfile','NuGet.Config','--source','https://api.nuget.org/v3/index.json','--packages',$feedPath,'--disable-build-servers','-p:NuGetAudit=false')
    Write-Output $output
    $packs.Values|Sort-Object name,version|ConvertTo-Json
}finally{if((Get-ArchSiftHostHash)-cne$before){throw 'CI prerequisite host drift; no repair.'};Write-Output 'framework-feed-host-state-equal=True'}
