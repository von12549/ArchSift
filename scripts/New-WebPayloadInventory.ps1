[CmdletBinding()]
param([Parameter(Mandatory)][string]$PayloadRoot,[Parameter(Mandatory)][string]$ExpectedVersion,
    [Parameter(Mandatory)][string]$SourceCommit,[switch]$VerifyOnly)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
$root=[IO.Path]::GetFullPath($PayloadRoot)
Assert-ArchSiftReleaseNoLinks $root
if($SourceCommit -notmatch '^[a-f0-9]{40}$'){throw 'An exact source commit is required.'}
$manifest=Join-Path $root 'web-payload.json'
$entries=@(Get-ChildItem -LiteralPath $root -Recurse -File|Where-Object FullName -ne $manifest|Sort-Object FullName|ForEach-Object{
    Assert-ArchSiftReleaseNoLinks $_.FullName
    @{path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace([IO.Path]::DirectorySeparatorChar,[char]'/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}
})
foreach($required in @('ArchSift.Web.dll','ArchSift.Web.deps.json','ArchSift.Web.runtimeconfig.json','wwwroot/index.html','wwwroot/launcher.html','wwwroot/test-workbench.js','notices/third-party-notices.md')){
    if($required -cnotin $entries.path){throw ('Incomplete Web publish closure: '+$required)}
}
foreach($entry in $entries|Where-Object {$_.path -match '^ArchSift\.[^/]+\.dll$'}){
    $version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root $entry.path)).ProductVersion.Split('+')[0]
    if($version -cne $ExpectedVersion){throw ('Mixed product assembly: '+$entry.path)}
}
$inventory=@{schemaVersion=1;version=$ExpectedVersion;sourceCommit=$SourceCommit;entries=$entries}
if($VerifyOnly){
    if(-not(Test-Path -LiteralPath $manifest)){throw 'Missing Web inventory.'}
    $actual=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json
    if($actual.schemaVersion -ne 1 -or $actual.version -cne $ExpectedVersion -or $actual.sourceCommit -cne $SourceCommit -or $actual.entries.Count -ne $entries.Count){throw 'Stale Web inventory identity.'}
    foreach($entry in $entries){$match=@($actual.entries|Where-Object path -CEQ $entry.path);if($match.Count -ne 1 -or $match[0].bytes -ne $entry.bytes -or $match[0].sha256 -cne $entry.sha256){throw 'Stale or changed Web payload.'}}
}else{
    if(Test-Path -LiteralPath $manifest){throw 'Never overwrite a Web inventory; publish into fresh staging.'}
    [IO.File]::WriteAllText($manifest,(($inventory|ConvertTo-Json -Depth 8)+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
}
Write-Output ('web-payload-verified files='+$entries.Count+' version='+$ExpectedVersion)
