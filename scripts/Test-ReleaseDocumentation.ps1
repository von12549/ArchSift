[CmdletBinding()]
param([Parameter(Mandatory)][string]$PayloadRoot,[Parameter(Mandatory)][string]$SourceCommit,[Parameter(Mandatory)][string]$Version)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
$payload=Assert-ArchSiftExternalPath $repo $PayloadRoot
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
$provenance=Get-Content -LiteralPath (Join-Path $payload 'docs/documentation-provenance.json') -Raw|ConvertFrom-Json
if($provenance.version -cne $Version -or $provenance.sourceCommit -cne $SourceCommit){throw 'Bundled documentation identity differs.'}
foreach($document in $provenance.documents){
    if((Hash (Join-Path $repo $document.source)) -cne $document.sourceSha256 -or (Hash (Join-Path $payload $document.payload)) -cne $document.payloadSha256){throw ('Bundled documentation provenance hash differs: '+$document.payload)}
}
$links=0
foreach($file in Get-ChildItem -LiteralPath $payload -Recurse -File -Filter '*.md'){
    $text=[IO.File]::ReadAllText($file.FullName)
    if($text -match '[\p{IsCJKUnifiedIdeographs}]'){throw ('Bundled authored Markdown is not English: '+$file.Name)}
    foreach($match in [regex]::Matches($text,'!?\[[^\]]*\]\(([^)]+)\)')){
        $link=$match.Groups[1].Value
        if($link -match '^[A-Za-z][A-Za-z0-9+.-]*:' -or $link.StartsWith('//') -or $link.StartsWith('#')){continue}
        $local=[uri]::UnescapeDataString($link.Split('#',2)[0])
        if(-not $local){continue}
        $resolved=[IO.Path]::GetFullPath((Join-Path $file.DirectoryName $local))
        if(-not $resolved.StartsWith($payload+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path -LiteralPath $resolved)){throw ('Bundled Markdown link escapes or is missing: '+[IO.Path]::GetRelativePath($payload,$file.FullName)+' -> '+$link)}
        $links++
    }
}
$tool=Join-Path $payload 'samples/workbench/dotnet-tools.example.json'
if(Test-Path -LiteralPath $tool){
    $value=Get-Content -LiteralPath $tool -Raw|ConvertFrom-Json
    if($value.tools.'archsift.cli'.version -cne $Version){throw 'Bundled tool template version differs.'}
}
Write-Output ('release-documentation=passed documents='+$provenance.documents.Count+' local-links='+$links+' scope=files-and-hashes;anchors-and-remote-URLs-separate')
