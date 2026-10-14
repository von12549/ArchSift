[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$Version = '0.1.0-dev',
    [switch]$IncludeMaintenance,
    [string]$OfflineTestPackageCache,
    [string]$OfflineTargetPackageCache
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($output.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $output -eq $root) {
    throw 'Development package output must be outside the source repository.'
}
[void][IO.Directory]::CreateDirectory($output)
$hostBin = Join-Path $root "src/ArchSift.InProject/bin/$Configuration/net10.0"
$workerBin = Join-Path $root "src/ArchSift.Cli/bin/$Configuration/net10.0"
$bridgeBin = Join-Path $root "src/ArchSift.InProject.TestBridge/bin/$Configuration/net10.0"
$maintenanceBin = Join-Path $root "src/ArchSift.InProject.Maintenance/bin/$Configuration/net10.0"
$assets = Join-Path $root 'assets/inproject'
$files = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
function Add-Payload([string]$name,[string]$source) {
    $path = [IO.Path]::GetFullPath($source)
    if (-not [IO.File]::Exists($path)) { throw "Missing development payload: $name" }
    if ($files.ContainsKey($name)) {
        $old = (Get-FileHash -LiteralPath $files[$name] -Algorithm SHA256).Hash
        $new = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($old -cne $new) { throw "Conflicting development payload: $name" }
        return
    }
    $files.Add($name,$path)
}
foreach ($file in Get-ChildItem -LiteralPath $hostBin -File) {
    if ($file.Extension -notin @('.dll','.exe','.json')) { continue }
    $name = if ($file.Name -eq 'ArchSift.InProject.exe') { 'archsift-inproject.exe' } else { $file.Name }
    Add-Payload "runtime/$name" $file.FullName
}
foreach ($name in @('archsift.exe','archsift.dll','archsift.deps.json','archsift.runtimeconfig.json')) {
    Add-Payload "runtime/$name" (Join-Path $workerBin $name)
}
Add-Payload 'runtime/ArchSift.InProject.TestBridge.dll' (Join-Path $bridgeBin 'ArchSift.InProject.TestBridge.dll')
Add-Payload 'runtime/LICENSE' (Join-Path $root 'LICENSE')
Add-Payload 'runtime/THIRD-PARTY-NOTICES.md' (Join-Path $root 'docs/migration/third-party-notices.md')
if ($IncludeMaintenance) {
    foreach ($file in Get-ChildItem -LiteralPath $maintenanceBin -File) {
        if ($file.Extension -notin @('.dll','.exe','.json')) { continue }
        Add-Payload "maintenance/$($file.Name)" $file.FullName
    }
    Add-Payload 'maintenance/archsift-inproject-maintenance.ps1' (Join-Path $assets 'maintenance/archsift-inproject-maintenance.ps1')
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'docs/migration/licenses') -File) {
        Add-Payload "licenses/$($file.Name)" $file.FullName
    }
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'docs/dependencies/w01/licenses') -File){
        Add-Payload "licenses/$($file.Name)" $file.FullName
    }
    Add-Payload 'licenses/third-party-inventory.json' (Join-Path $root 'docs/migration/inproject-third-party-inventory.json')
}
if ($OfflineTestPackageCache) {
    $testCache=[IO.Path]::GetFullPath($OfflineTestPackageCache)
    $targetCache=if($OfflineTargetPackageCache){[IO.Path]::GetFullPath($OfflineTargetPackageCache)}else{$testCache}
    $lock=Get-Content (Join-Path $assets 'tests/packages.lock.json') -Raw | ConvertFrom-Json
    foreach($entry in $lock.dependencies.'net10.0'.PSObject.Properties){
        $id=$entry.Name.ToLowerInvariant()
        $packageVersion=([string]$entry.Value.resolved).ToLowerInvariant()
        Add-Payload "offline/$id.$packageVersion.nupkg" (Join-Path $testCache "$id/$packageVersion/$id.$packageVersion.nupkg")
    }
    foreach($reference in @(
        'microsoft.netcore.app.ref/8.0.30',
        'microsoft.netcore.app.ref/9.0.19',
        'microsoft.aspnetcore.app.ref/8.0.30',
        'microsoft.aspnetcore.app.ref/9.0.19',
        'microsoft.windowsdesktop.app.ref/8.0.30',
        'microsoft.windowsdesktop.app.ref/9.0.19')){
        $parts=$reference.Split('/')
        Add-Payload "offline/$($parts[0]).$($parts[1]).nupkg" (Join-Path $targetCache "$reference/$($parts[0]).$($parts[1]).nupkg")
    }
    $inventory=Get-Content (Join-Path $root 'docs/migration/inproject-third-party-inventory.json') -Raw|ConvertFrom-Json
    $offline=@($files.Keys|Where-Object {$_ -like 'offline/*'})
    if($inventory.packageCount -ne $offline.Count -or $inventory.packages.Count -ne $offline.Count){
        throw 'Offline package count differs from the InProject license inventory.'
    }
    foreach($entry in $inventory.packages){
        if(-not $files.ContainsKey([string]$entry.packagePath) -or
           (Get-FileHash -LiteralPath $files[[string]$entry.packagePath] -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.sha256){
            throw "Offline package differs from the InProject license inventory: $($entry.packagePath)"
        }
    }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $assets 'tests') -File) {
    if ($file.Name -notin @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','ArchSift.InProject.ArchitectureTests.csproj','ArchitectureRules.cs','packages.lock.json')) { continue }
    Add-Payload "tests/$($file.Name)" $file.FullName
}
Add-Payload 'bootstrap/archsift-inproject.ps1' (Join-Path $assets 'bootstrap/archsift-inproject.ps1')
Add-Payload 'rules/architecture.json' (Join-Path $root 'templates/rules/target-framework.json')
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') { throw 'Cannot identify the source commit.' }
$list = @($files.Keys | Sort-Object -CaseSensitive | ForEach-Object {
    [ordered]@{ path=$_; bytes=([IO.FileInfo]$files[$_]).Length; sha256=(Get-FileHash -LiteralPath $files[$_] -Algorithm SHA256).Hash.ToLowerInvariant() }
})
if($Version -notmatch '^0\.1\.0(?:-[0-9A-Za-z.-]+)?$'){throw 'Unsupported development package version.'}
$manifest = [ordered]@{ schemaVersion=1; product='ArchSift.InProject'; version=$Version; rid='win-x64'; sourceCommit=$commit; contractVersion=1; files=$list }
$manifestBytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $manifest -Depth 8) + "`n")
$temp = Join-Path $output ('.inproject-package-' + [Guid]::NewGuid().ToString('N') + '.zip')
try {
    $archive = [IO.Compression.ZipFile]::Open($temp,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in $list) {
            $entry = $archive.CreateEntry($item.path,[IO.Compression.CompressionLevel]::Optimal)
            $source = [IO.File]::OpenRead($files[$item.path])
            $target = $entry.Open()
            try { $source.CopyTo($target) } finally { $target.Dispose(); $source.Dispose() }
        }
        $entry = $archive.CreateEntry('payload-manifest.json',[IO.Compression.CompressionLevel]::Optimal)
        $stream = $entry.Open()
        try { $stream.Write($manifestBytes,0,$manifestBytes.Length) } finally { $stream.Dispose() }
    } finally { $archive.Dispose() }
    $sha = (Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash.ToLowerInvariant()
    $final = Join-Path $output ("archsift-inproject-$Version-win-x64-$($sha.Substring(0,12)).zip")
    if ([IO.File]::Exists($final)) { throw 'The target development package name already exists.' }
    [IO.File]::Move($temp,$final)
    [pscustomobject]@{ path=$final; sha256=$sha; manifestSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($manifestBytes)).ToLowerInvariant(); sourceCommit=$commit; files=$list.Count; developmentOnly=($Version -ne '0.1.0') }
} finally {
    if ([IO.File]::Exists($temp)) { [IO.File]::Delete($temp) }
}
