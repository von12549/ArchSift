[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductPackages,
    [Parameter(Mandatory)][string]$ConsumerLock,
    [Parameter(Mandatory)][string]$ConsumerCache,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$SourceCommit,
    [Parameter(Mandatory)][string]$ArchivePath,
    [switch]$DevelopmentProbe
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
$release=Get-ArchSiftReleaseInputs $repo
if($SourceCommit -notmatch '^[a-f0-9]{40}$'){throw 'A fixed package source commit is required.'}
$output=Assert-ArchSiftExternalPath $repo $OutputDirectory
$archive=Assert-ArchSiftExternalPath $repo $ArchivePath
if(Test-Path -LiteralPath $output){throw 'Keep the existing offline-feed attempt; use a fresh directory.'}
if(Test-Path -LiteralPath $archive){throw 'Do not overwrite an existing offline-feed archive.'}
if($archive.StartsWith($output+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Archive must be outside the feed being archived.'}
$allowlist=Get-Content (Join-Path $repo 'docs/releases/0.10.1/consumer-dependency-allowlist.json') -Raw|ConvertFrom-Json
if($allowlist.schemaVersion -ne 1){throw 'Unknown generic dependency approval.'}
$lock=Get-Content -LiteralPath $ConsumerLock -Raw|ConvertFrom-Json -AsHashtable
$graph=$lock.dependencies['net10.0']
if($null -eq $graph){throw 'Actual net10.0 package graph is required.'}
$selected=[Collections.Generic.List[object]]::new()
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Identity([string]$Package){
    $zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
    try{
        $nuspecs=@($zip.Entries|Where-Object FullName -like '*.nuspec')
        if($nuspecs.Count -ne 1){throw 'Package nuspec is ambiguous.'}
        $reader=[IO.StreamReader]::new($nuspecs[0].Open())
        try{[xml]$xml=$reader.ReadToEnd()}finally{$reader.Dispose()}
        $metadata=$xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
        $id=$metadata.SelectSingleNode('*[local-name()="id"]').InnerText
        $version=$metadata.SelectSingleNode('*[local-name()="version"]').InnerText
        $repository=$metadata.SelectSingleNode('*[local-name()="repository"]')
        [pscustomobject]@{id=$id;version=$version;sourceCommit=$(if($repository){$repository.GetAttribute('commit')}else{''})}
    }finally{$zip.Dispose()}
}
foreach($id in $release.profile.productPackages){
    $path=Join-Path ([IO.Path]::GetFullPath($ProductPackages)) ($id+'.'+$release.version+'.nupkg')
    if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw 'One of the five new product packages is missing.'}
    $identity=Identity $path
    if($identity.id -cne $id -or $identity.version -cne $release.version -or $identity.sourceCommit -cne $SourceCommit){throw 'Product package version/source metadata differs.'}
    $selected.Add(@{id=$id;version=$release.version;file=[IO.Path]::GetFileName($path);sourcePath=$path;sha256=(Hash $path);bytes=(Get-Item $path).Length;kind='product'})
}
foreach($id in ($graph.Keys|Sort-Object)){
    $node=$graph[$id]
    if($node.type -eq 'Project' -or -not $node.resolved -or -not $node.contentHash){throw 'The minimal feed requires an actual package lock, without source references.'}
    $directory=Join-Path (Join-Path ([IO.Path]::GetFullPath($ConsumerCache)) $id.ToLowerInvariant()) $node.resolved
    $path=Join-Path $directory ($id.ToLowerInvariant()+'.'+$node.resolved+'.nupkg')
    $storedHash=Join-Path $directory ($id.ToLowerInvariant()+'.'+$node.resolved+'.nupkg.sha512')
    $metadataPath=Join-Path $directory '.nupkg.metadata'
    if(-not(Test-Path -LiteralPath $path -PathType Leaf) -or -not(Test-Path -LiteralPath $storedHash -PathType Leaf) -or -not(Test-Path -LiteralPath $metadataPath -PathType Leaf)){throw ('Package cache identity files missing: '+$id)}
    $metadata=Get-Content -LiteralPath $metadataPath -Raw|ConvertFrom-Json
    $rawSha512=[Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($path)))
    # Signed raw archives and NuGet lock content hashes are separate identity domains.
    if(([IO.File]::ReadAllText($storedHash)).Trim() -cne $rawSha512 -or $metadata.contentHash -cne $node.contentHash){throw ('Package raw/lock hash verification failed: '+$id)}
    if($id.StartsWith('ArchSift.',[StringComparison]::OrdinalIgnoreCase)){
        $product=@($selected|Where-Object{[string]::Equals($_.id,$id,[StringComparison]::OrdinalIgnoreCase)})
        if($product.Count -ne 1 -or $node.resolved -cne $release.version -or (Hash $path) -cne $product[0].sha256){throw 'Consumer restored a different product package.'}
        $product[0]['lockContentHash']=$node.contentHash
        $product[0]['rawSha512']=$rawSha512
        continue
    }
    $approved=@($allowlist.packages|Where-Object{[string]::Equals($_.id,$id,[StringComparison]::OrdinalIgnoreCase) -and $_.version -ceq $node.resolved})
    if($approved.Count -ne 1 -or (Hash $path) -cne $approved[0].sha256){throw ('Unapproved generic dependency bytes/version: '+$id)}
    $identity=Identity $path
    if(-not[string]::Equals($identity.id,$id,[StringComparison]::OrdinalIgnoreCase) -or $identity.version -cne $node.resolved){throw 'Dependency nuspec identity differs from approval.'}
    $selected.Add(@{id=$identity.id;version=$node.resolved;file=[IO.Path]::GetFileName($path);sourcePath=$path;sha256=$approved[0].sha256;bytes=(Get-Item $path).Length;lockContentHash=$node.contentHash;rawSha512=$rawSha512;licenseMetadata=$approved[0].licenseMetadata;kind='third-party'})
}
if(@($selected|Group-Object file|Where-Object Count -gt 1).Count){throw 'Duplicate/case-colliding package filename.'}
[void][IO.Directory]::CreateDirectory($output)
foreach($folder in @('schemas','templates')){Copy-Item -LiteralPath (Join-Path $repo $folder) -Destination (Join-Path $output $folder) -Recurse}
& (Join-Path $PSScriptRoot 'Copy-ReleaseDocumentation.ps1') -PayloadRoot $output -SourceCommit $SourceCommit -Version $release.version
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $output 'LICENSE')
foreach($entry in $selected){
    $destination=Join-Path $output $entry.file
    [IO.File]::Copy($entry.sourcePath,$destination,$false)
    if((Hash $destination) -cne $entry.sha256){throw 'Offline-feed copy hash mismatch.'}
}
$public=@(foreach($entry in $selected){
    $record=[ordered]@{id=$entry.id;version=$entry.version;file=$entry.file;bytes=$entry.bytes;sha256=$entry.sha256;kind=$entry.kind}
    if($entry.ContainsKey('lockContentHash')){$record['lockContentHash']=$entry.lockContentHash}
    if($entry.ContainsKey('rawSha512')){$record['rawSha512']=$entry.rawSha512}
    if($entry.ContainsKey('licenseMetadata')){$record['licenseMetadata']=$entry.licenseMetadata}
    $record
})
$manifest=@{schemaVersion=1;product='ArchSift Workbench';version=$release.version;sourceCommit=$SourceCommit;scope='Minimal actually resolved generic net10.0 architecture-test/tool closure; consumer-specific packages are separate';packageCount=$selected.Count;developmentProbe=[bool]$DevelopmentProbe;packages=$public;privateIfxFeedIncluded=$false;checksumsExcludeSelf=$true;vulnerabilityAudit='separate release gate; not established by this integrity manifest'}
[IO.File]::WriteAllText((Join-Path $output 'feed-manifest.json'),(($manifest|ConvertTo-Json -Depth 12)+"`n"),[Text.UTF8Encoding]::new($false))
$readme="# ArchSift Workbench $($release.version) offline package source`n`nExtract this complete source to a local directory. Verify SHA256SUMS.txt, then configure that directory as an explicit NuGet source with <clear />. It supplies the generic ArchSift/xUnit v3 net10.0 closure only; add separately approved consumer dependencies. Source commit: $SourceCommit. Read docs/project-integration.md and RELEASE_NOTES.md; samples/workbench contains version-matched configuration templates, and schemas/templates are included. The Windows UI is a separate delivery. Original nupkg bytes retain their upstream licenses/notices. The manifest records package integrity; vulnerability/license review and final release acceptance are separate records.`n"
[IO.File]::WriteAllText((Join-Path $output 'OFFLINE_FEED.md'),$readme,[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Test-ReleaseDocumentation.ps1') -PayloadRoot $output -SourceCommit $SourceCommit -Version $release.version
$sums=@(Get-ChildItem -LiteralPath $output -Recurse -File|Sort-Object FullName|ForEach-Object{(Hash $_.FullName)+'  '+[IO.Path]::GetRelativePath($output,$_.FullName).Replace('\','/')}) -join "`n"
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'),$sums+"`n",[Text.UTF8Encoding]::new($false))
[void][IO.Directory]::CreateDirectory((Split-Path $archive))
[IO.Compression.ZipFile]::CreateFromDirectory($output,$archive)
@{scope='generic offline source integrity';version=$release.version;sourceCommit=$SourceCommit;packageCount=$selected.Count;feedRoot=$output;archivePath=$archive;archiveSha256=(Hash $archive);developmentProbe=[bool]$DevelopmentProbe}|ConvertTo-Json -Depth 8
