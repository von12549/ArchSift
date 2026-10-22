[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputPath,[Parameter(Mandatory)][string]$SourceCommit)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
$output=Assert-ArchSiftExternalPath $repo $OutputPath
if(Test-Path -LiteralPath $output){throw 'Keep the existing generated notice; use a fresh output path.'}
if($SourceCommit -notmatch '^[a-f0-9]{40}$'){throw 'A fixed source commit is required for notice references.'}
$source=Join-Path $repo 'docs/migration/third-party-notices.md'
$text=[IO.File]::ReadAllText($source)
$text=[regex]::Replace($text,'(!?\[[^\]]*\])\(([^)]+)\)',{
    param($match)
    $link=$match.Groups[2].Value
    if($link -match '^[A-Za-z][A-Za-z0-9+.-]*:' -or $link.StartsWith('#')){return $match.Value}
    if($link.StartsWith('licenses/')){return $match.Value}
    $parts=$link.Split('#',2)
    $path=[IO.Path]::GetFullPath((Join-Path (Split-Path $source) ([uri]::UnescapeDataString($parts[0]))))
    $relative=[IO.Path]::GetRelativePath($repo,$path).Replace('\','/')
    if($relative.StartsWith('../') -or -not(Test-Path -LiteralPath $path)){throw 'Unknown legal-provenance link.'}
    $encoded=($relative.Split('/')|ForEach-Object{[uri]::EscapeDataString($_)}) -join '/'
    $fragment=if($parts.Count -eq 2){'#'+$parts[1]}else{''}
    $match.Groups[1].Value+'(https://github.com/von12549/ArchSift/blob/'+$SourceCommit+'/'+$encoded+$fragment+')'
})
[void][IO.Directory]::CreateDirectory((Split-Path $output))
[IO.File]::WriteAllText($output,$text,[Text.UTF8Encoding]::new($false))
# Only ArchSift-authored reference URLs changed; upstream license/NOTICE files remain byte-identical.
