[CmdletBinding()]
param([Parameter(Mandatory)][string]$PayloadRoot,[Parameter(Mandatory)][string]$SourceCommit,[Parameter(Mandatory)][string]$Version)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
$payload=Assert-ArchSiftExternalPath $repo $PayloadRoot
if($SourceCommit -notmatch '^[a-f0-9]{40}$'){throw 'A fixed Git source commit is required.'}
$release=Get-ArchSiftReleaseInputs $repo
if($Version -cne $release.version){throw 'Documentation must match the unchanged central version.'}
$contract=Get-Content (Join-Path $repo 'docs/releases/0.10.1/release-contract.json') -Raw|ConvertFrom-Json
$mapping=[ordered]@{'docs/package-readme.md'='README.md';'RELEASE_NOTES.md'='RELEASE_NOTES.md'}
foreach($path in $contract.bundledDocumentation.requiredFiles){if($path -notin @('README.md','RELEASE_NOTES.md')){$mapping[[string]$path]=[string]$path}}
$mapping['docs/changes.md']='docs/changes.md'
$mapping['docs/migration/third-party-notices.md']='licenses/third-party-notices.md'
foreach($file in Get-ChildItem -LiteralPath (Join-Path $repo 'docs/migration/licenses') -File){
    $mapping['docs/migration/licenses/'+$file.Name]='licenses/libraries/'+$file.Name
}
foreach($folder in @('samples/workbench','samples/portable-verification')){
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $repo $folder) -File){
        $relative=[IO.Path]::GetRelativePath($repo,$file.FullName).Replace('\','/')
        $mapping[$relative]=$relative
    }
}
foreach($path in @('samples/ifx-formal/ArchSiftCaseIdentityAttribute.cs','samples/ifx-lt02a/ArchSiftJsonTests.cs')){$mapping[$path]=$path}
$linkMapping=@{}
foreach($entry in $mapping.GetEnumerator()){$linkMapping[$entry.Key]=$entry.Value}
foreach($folder in @('schemas','templates')){
    $linkMapping[$folder]=$folder
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $repo $folder) -Recurse -File){
        $relative=[IO.Path]::GetRelativePath($repo,$file.FullName).Replace('\','/')
        $linkMapping[$relative]=$relative
    }
}
$records=[Collections.Generic.List[object]]::new()
foreach($entry in $mapping.GetEnumerator()){
    $source=Join-Path $repo $entry.Key;$destination=Join-Path $payload $entry.Value
    if(-not(Test-Path -LiteralPath $source -PathType Leaf)){throw ('Required release documentation/example missing: '+$entry.Key)}
    [void][IO.Directory]::CreateDirectory((Split-Path $destination))
    $raw=[IO.File]::ReadAllBytes($source)
    $text=[Text.Encoding]::UTF8.GetString($raw).TrimStart([char]0xfeff)
    $rewrites=0
    $renderedVersion=$false
    $script:linkRewritesForDocument=0
    if($entry.Key -like '*.md'){
        $currentDirectory=Split-Path $source
        $destinationDirectory=Split-Path $destination
        $text=[regex]::Replace($text,'(!?\[[^\]]*\])\(([^)]+)\)',{
            param($match)
            $link=$match.Groups[2].Value
            if($link -match '^[A-Za-z][A-Za-z0-9+.-]*:' -or $link.StartsWith('//')){return $match.Value}
            $parts=$link.Split('#',2);$local=[uri]::UnescapeDataString($parts[0])
            if(-not $local){return $match.Value}
            $resolved=[IO.Path]::GetFullPath((Join-Path $currentDirectory $local))
            $relative=[IO.Path]::GetRelativePath($repo,$resolved).Replace('\','/')
            if($relative -eq '..' -or $relative.StartsWith('../') -or [IO.Path]::IsPathRooted($relative)){return $match.Value}
            $fragment=if($parts.Count -eq 2){'#'+$parts[1]}else{''}
            if($linkMapping.ContainsKey($relative)){
                $target=[IO.Path]::GetRelativePath($destinationDirectory,(Join-Path $payload $linkMapping[$relative])).Replace('\','/')+$fragment
            }else{
                if(-not(Test-Path -LiteralPath $resolved)){throw ('Broken source documentation link: '+$entry.Key+' -> '+$link)}
                $kind=if(Test-Path -LiteralPath $resolved -PathType Container){'tree'}else{'blob'}
                $encoded=($relative.Split('/')|ForEach-Object{[uri]::EscapeDataString($_)}) -join '/'
                $target='https://github.com/von12549/ArchSift/'+$kind+'/'+$SourceCommit+'/'+$encoded+$fragment
            }
            $script:linkRewritesForDocument++
            $match.Groups[1].Value+'('+$target+')'
        })
        $text=[regex]::Replace($text,'versions[/\\]0\.10\.[01](?:-rc\.[1-9][0-9]*)?[/\\]',('versions/'+$Version+'/'))
        $text=[regex]::Replace($text,'archsift-workbench-0\.10\.[01](?:-rc\.[1-9][0-9]*)?-win-x64\.zip',(Get-ArchSiftReleaseArchiveName $Version))
        if($entry.Value -eq 'samples/workbench/README.md'){
            $text=[regex]::Replace($text,'The sample uses stable[^\r\n]+',('The bundled machine templates are rendered to '+$Version+'. Paths, hashes, production assemblies and case approvals remain consumer-specific review inputs. The source ledger records packaging-stage progress.'))
        }
        $rewrites=$script:linkRewritesForDocument
        $script:linkRewritesForDocument=0
        if($entry.Value -eq 'README.md'){$text="# ArchSift Workbench $Version`n`nPackaging candidate from source $SourceCommit. The separate final acceptance record binds accepted bytes.`n`n"+$text}
        if($entry.Value -eq 'RELEASE_NOTES.md'){$text=$text.Replace('# ArchSift Workbench 0.10.1',('# ArchSift Workbench '+$Version))}
    }
    if($entry.Key.StartsWith('samples/workbench/') -and $entry.Key -notlike '*.md'){
        $rendered=[regex]::Replace($text,'0\.10\.[01](?:-rc\.[1-9][0-9]*)?',$Version)
        $renderedVersion=$rendered -cne $text
        $text=$rendered
    }
    if($entry.Key -like '*.md' -or $renderedVersion){[IO.File]::WriteAllText($destination,$text,[Text.UTF8Encoding]::new($false))}else{[IO.File]::WriteAllBytes($destination,$raw)}
    $records.Add(@{source=$entry.Key;payload=$entry.Value;sourceSha256=(Get-FileHash $source -Algorithm SHA256).Hash.ToLowerInvariant();payloadSha256=(Get-FileHash $destination -Algorithm SHA256).Hash.ToLowerInvariant();linkRewrites=$rewrites;versionRendered=$renderedVersion})
}
$record=@{schemaVersion=1;version=$Version;sourceCommit=$SourceCommit;sourceDirty=[bool](& git -C $repo status --porcelain);scope='English curated guides and generic examples. Byte hashes identify inputs; sourceCommit fixes link destinations. Final releases require clean committed source. Unbundled links point to that commit.';documents=$records.ToArray()}
[IO.File]::WriteAllText((Join-Path $payload 'docs/documentation-provenance.json'),($record|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
Write-Output ('release-documentation-count='+$records.Count)
