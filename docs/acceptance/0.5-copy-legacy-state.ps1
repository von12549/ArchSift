# Copy an independently verified 0.4.0 installation into a new external review root.
# Rebase only the copied config's report/library paths; source bytes are read-only.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceInstallRoot,
    [Parameter(Mandatory)][string]$SourceVersionDirectory,
    [Parameter(Mandatory)][string]$SourceConfigPath,
    [Parameter(Mandatory)][string]$DestinationRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$source=Safe-Path $SourceInstallRoot;$version=Safe-Path $SourceVersionDirectory;$configPath=Safe-Path $SourceConfigPath;$destination=Safe-Path $DestinationRoot
if(-not$version.StartsWith($source+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not$configPath.StartsWith($source+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Source version/config must be inside the source installation.'}
if($destination-eq$source-or$destination.StartsWith($source+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$source.StartsWith($destination+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Source and external copy roots overlap.'}
if(Test-Path -LiteralPath $destination){throw 'Destination copy root already exists; never overwrite it.'}
$manifestPath=Safe-Path (Join-Path $version 'package-manifest.json')
if((Hash-File $manifestPath)-cne'd35fea42475633eb290c858e4df4aec4387dad639d2d03480d20e9edd532849c'){throw 'Source 0.4.0 manifest differs.'}
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
if($manifest.version-cne'0.4.0'-or$manifest.sourceCommit-cne'bb599bd87aaa3322229c049442e135f3c091b72a'-or$manifest.entries.Count-ne621){throw 'Source 0.4.0 package identity differs.'}
function Tree-Records([string]$Root){
    $records=[Collections.Generic.List[object]]::new()
    if(Test-Path -LiteralPath $Root){
        $Root=Safe-Path $Root
        foreach($item in @(Get-ChildItem -LiteralPath $Root -Recurse -Force)){
            $path=Safe-Path $item.FullName
            if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Tree item escapes root.'}
            if($item.PSIsContainer){continue}
            if(-not($item -is [IO.FileInfo])){throw 'Unsupported tree item.'}
            $records.Add([ordered]@{path=[IO.Path]::GetRelativePath($Root,$path);length=$item.Length;sha256=(Hash-File $path)})
            if($records.Count-gt10000){throw 'State file-count limit.'}
        }
    }
    return @($records|Sort-Object path)
}
function Copy-Records([string]$SourceRoot,[string]$DestinationRoot,[object[]]$Records){
    foreach($record in $Records){
        $from=Safe-Path (Join-Path $SourceRoot $record.path);$to=Safe-Path (Join-Path $DestinationRoot $record.path)
        if(-not$from.StartsWith($SourceRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not$to.StartsWith($DestinationRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'State file escapes root.'}
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to));[IO.File]::Copy($from,$to,$false)
        if((Hash-File $to)-cne$record.sha256-or([IO.FileInfo]$to).Length-ne$record.length){throw 'Copied state file differs.'}
    }
}
$sourceConfigHash=Hash-File $configPath
$sourceConfig=Get-Content -LiteralPath $configPath -Raw|ConvertFrom-Json
if($sourceConfig.schemaVersion-ne1-or-not$sourceConfig.output.directory-or-not$sourceConfig.rulesDirectory){throw 'Source config lacks known copy paths.'}
$sourceLibrary=Safe-Path ([string]$sourceConfig.rulesDirectory);$sourceReports=Safe-Path ([string]$sourceConfig.output.directory)
foreach($path in @($sourceLibrary,$sourceReports)){if(-not$path.StartsWith($source+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Source state path is outside original installation.'}}
$libraryBefore=@(Tree-Records $sourceLibrary);$reportsBefore=@(Tree-Records $sourceReports)
$stateBytes=0L
foreach($record in @($libraryBefore)+@($reportsBefore)){$stateBytes+=[long]$record.length}
if($stateBytes-gt2GB){throw 'Source state exceeds copy size limit.'}
foreach($entry in $manifest.entries){$file=Safe-Path (Join-Path $version $entry.path);if(-not$file.StartsWith($version+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $file)-cne$entry.sha256){throw 'Source package payload differs.'}}
$destVersion=Safe-Path (Join-Path $destination 'versions/0.4.0');$destLibrary=Safe-Path (Join-Path $destination 'rules');$destReports=Safe-Path (Join-Path $destination 'reports');$destConfig=Safe-Path (Join-Path $destination 'config/ifx.json')
[void][IO.Directory]::CreateDirectory($destVersion)
[IO.File]::Copy($manifestPath,(Join-Path $destVersion 'package-manifest.json'),$false)
foreach($entry in $manifest.entries){
    $from=Safe-Path (Join-Path $version $entry.path);$to=Safe-Path (Join-Path $destVersion $entry.path)
    if(-not$to.StartsWith($destVersion+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Destination payload escapes root.'}
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to));[IO.File]::Copy($from,$to,$false)
    if((Hash-File $to)-cne$entry.sha256){throw 'Copied package payload differs.'}
}
Copy-Records $sourceLibrary $destLibrary $libraryBefore
Copy-Records $sourceReports $destReports $reportsBefore
$copyConfig=Get-Content -LiteralPath $configPath -Raw|ConvertFrom-Json
$copyConfig.output.directory=$destReports;$copyConfig.rulesDirectory=$destLibrary
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destConfig))
[IO.File]::WriteAllText($destConfig,($copyConfig|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
$reloaded=Get-Content -LiteralPath $destConfig -Raw|ConvertFrom-Json
if($reloaded.target.root-cne$sourceConfig.target.root-or$reloaded.target.entry-cne$sourceConfig.target.entry-or$reloaded.build.mode-cne$sourceConfig.build.mode-or$reloaded.build.targetFramework-cne$sourceConfig.build.targetFramework-or$reloaded.build.configuration-cne$sourceConfig.build.configuration-or$reloaded.build.allowNetwork-cne$sourceConfig.build.allowNetwork-or$reloaded.rulesets.Count-ne$sourceConfig.rulesets.Count){throw 'Copied config changed more than external state paths.'}
if((Hash-File (Join-Path $destVersion 'package-manifest.json'))-cne(Hash-File $manifestPath)-or(Hash-Json @(Tree-Records $destLibrary))-cne(Hash-Json $libraryBefore)-or(Hash-Json @(Tree-Records $destReports))-cne(Hash-Json $reportsBefore)){throw 'External copy differs from source state.'}
if((Hash-File $configPath)-cne$sourceConfigHash-or(Hash-Json @(Tree-Records $sourceLibrary))-cne(Hash-Json $libraryBefore)-or(Hash-Json @(Tree-Records $sourceReports))-cne(Hash-Json $reportsBefore)){throw 'Original config/state changed during copy.'}
return [ordered]@{destinationRoot=$destination;versionDirectory=$destVersion;configPath=$destConfig;sourceConfigSha256=$sourceConfigHash;copyConfigSha256=(Hash-File $destConfig);manifestSha256=(Hash-File $manifestPath);payloadCount=$manifest.entries.Count;librarySha256=(Hash-Json $libraryBefore);libraryFileCount=$libraryBefore.Count;reportsSha256=(Hash-Json $reportsBefore);reportFileCount=$reportsBefore.Count;targetRoot=$sourceConfig.target.root;targetEntry=$sourceConfig.target.entry;externalPathsRelocated=$true;sourceStateEqual=$true;copyStateEqual=$true}
