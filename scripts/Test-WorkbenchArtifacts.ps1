[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProductPackages,
    [Parameter(Mandatory)][string]$NativeResult,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$SourceCommit
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
$release=Get-ArchSiftReleaseInputs $repo
$output=Assert-ArchSiftExternalPath $repo $OutputDirectory
if(Test-Path -LiteralPath $output){throw 'Preserve prior artifact verification; use a fresh directory.'}
if($SourceCommit -notmatch '^[a-f0-9]{40}$'){throw 'Actual source identity is required.'}
[void][IO.Directory]::CreateDirectory($output)
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Check-Version([string]$Path,[bool]$Managed){
    $info=[Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    if($info.FileVersion -cne $release.assemblyVersion -or $info.ProductVersion -cne ($release.version+'+'+$SourceCommit)){throw ('Actual file/informational identity differs: '+[IO.Path]::GetFileName($Path))}
    if($Managed -and [Reflection.AssemblyName]::GetAssemblyName($Path).Version.ToString() -cne $release.assemblyVersion){throw 'Managed assembly numeric version differs.'}
    @{file=[IO.Path]::GetFileName($Path);fileVersion=$info.FileVersion;informationalVersion=$info.ProductVersion;sha256=(Hash $Path);managed=$Managed}
}
$packages=[Collections.Generic.List[object]]::new()
$expectedDependencies=@{'ArchSift.Contracts'=@();'ArchSift.Core'=@('ArchSift.Contracts');'ArchSift.ArchUnit'=@('ArchSift.Core');'ArchSift.Testing'=@('ArchSift.ArchUnit');'ArchSift.Cli'=@()}
foreach($id in $release.profile.productPackages){
    $path=Join-Path ([IO.Path]::GetFullPath($ProductPackages)) ($id+'.'+$release.version+'.nupkg')
    $zip=[IO.Compression.ZipFile]::OpenRead($path)
    try{
        $nuspecs=@($zip.Entries|Where-Object FullName -like '*.nuspec')
        if($nuspecs.Count -ne 1){throw 'Ambiguous nuspec.'}
        $reader=[IO.StreamReader]::new($nuspecs[0].Open())
        try{[xml]$xml=$reader.ReadToEnd()}finally{$reader.Dispose()}
        $metadata=$xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
        if($metadata.SelectSingleNode('*[local-name()="id"]').InnerText -cne $id -or $metadata.SelectSingleNode('*[local-name()="version"]').InnerText -cne $release.version -or
            $metadata.SelectSingleNode('*[local-name()="repository"]').GetAttribute('commit') -cne $SourceCommit){throw 'Package/source identity differs.'}
        $license=$metadata.SelectSingleNode('*[local-name()="license"]')
        if($license.GetAttribute('type') -cne 'expression' -or $license.InnerText -cne 'MIT' -or $metadata.SelectSingleNode('*[local-name()="readme"]').InnerText -cne 'README.md'){throw 'Product license/readme metadata differs.'}
        foreach($required in @('README.md','RELEASE_NOTES.md','LICENSE')){if($null -eq $zip.GetEntry($required)){throw 'Required package notes/license missing.'}}
        foreach($doc in @('README.md','RELEASE_NOTES.md')){
            $reader=[IO.StreamReader]::new($zip.GetEntry($doc).Open())
            try{$text=$reader.ReadToEnd()}finally{$reader.Dispose()}
            if(-not $text.Contains($release.version) -or -not $text.Contains($SourceCommit) -or $text -match '[\p{IsCJKUnifiedIdeographs}]'){throw 'Package notes are not English/version/source matched.'}
        }
        $stream=$zip.GetEntry('LICENSE').Open();$memory=[IO.MemoryStream]::new()
        try{$stream.CopyTo($memory);$licenseBytes=$memory.ToArray()}finally{$stream.Dispose();$memory.Dispose()}
        if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($licenseBytes)).ToLowerInvariant() -cne (Hash (Join-Path $repo 'LICENSE'))){throw 'Original product license bytes differ.'}
        $dependencies=@($metadata.SelectNodes('.//*[local-name()="dependency"]')|Where-Object{$_.GetAttribute('id').StartsWith('ArchSift.',[StringComparison]::OrdinalIgnoreCase)})
        $ids=@($dependencies|ForEach-Object{$_.GetAttribute('id')}|Sort-Object)
        if(($ids -join '|') -cne ((@($expectedDependencies[$id])|Sort-Object) -join '|')){throw 'Internal package graph differs.'}
        if(@($dependencies|Where-Object{$_.GetAttribute('version') -cne ('['+$release.version+']')}).Count){throw 'Internal dependency range is not exact.'}
        $directory=Join-Path $output $id;[void][IO.Directory]::CreateDirectory($directory)
        $versions=@(foreach($entry in $zip.Entries|Where-Object{[IO.Path]::GetFileName($_.FullName) -match '^(?:ArchSift\..+|archsift)\.dll$'}){
            $destination=Join-Path $directory ([IO.Path]::GetFileName($entry.FullName))
            $source=$entry.Open();$target=[IO.FileStream]::new($destination,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try{$source.CopyTo($target)}finally{$source.Dispose();$target.Dispose()}
            Check-Version $destination $true
        })
        if($versions.Count -eq 0){throw 'Actual product DLL missing from package.'}
        if($id -ceq 'ArchSift.Cli'){
            $settings=$zip.GetEntry('tools/net10.0/any/DotnetToolSettings.xml')
            if($null -eq $settings){throw 'CLI tool settings missing.'}
            $reader=[IO.StreamReader]::new($settings.Open())
            try{[xml]$tool=$reader.ReadToEnd()}finally{$reader.Dispose()}
            if($tool.SelectSingleNode('/DotNetCliTool/Commands/Command').GetAttribute('Name') -cne 'archsift'){throw 'CLI command changed.'}
        }
        $packages.Add(@{id=$id;version=$release.version;sha256=(Hash $path);exactInternalDependencies=$true;matchedNotesAndLicense=$true;dlls=$versions})
    }finally{$zip.Dispose()}
}
$native=Get-Content -LiteralPath $NativeResult -Raw|ConvertFrom-Json
if($native.version -cne $release.version -or $native.sourceCommit -cne $SourceCommit -or $native.productSourceDirty -or -not $native.hostStateEqual){throw 'Native result source identity differs.'}
$payload=[IO.Path]::GetFullPath($native.payloadRoot)
$nativeVersions=@(foreach($path in @((Join-Path $payload 'archsift.exe'),(Join-Path $payload 'web/ArchSift.Web.exe'),$native.setupPath)) {Check-Version $path $false})
foreach($path in Get-ChildItem -LiteralPath $payload -Recurse -Filter '*.dll' -File|Where-Object{$_.Name -match '^(?:ArchSift\..+|archsift)\.dll$'}){$nativeVersions+=Check-Version $path.FullName $true}
$manifest=Get-Content (Join-Path $payload 'package-manifest.json') -Raw|ConvertFrom-Json
if($manifest.version -cne $release.version -or $manifest.sourceCommit -cne $SourceCommit -or $manifest.productSourceDirty -or $manifest.rid -cne 'win-x64' -or -not $manifest.selfContained){throw 'Native manifest identity differs.'}
$zip=[IO.Compression.ZipFile]::OpenRead($native.zipPath)
try{
    $expectedNames=@($manifest.entries|ForEach-Object{$_.path})+@('package-manifest.json')
    $actualNames=@($zip.Entries|Where-Object{-not $_.FullName.EndsWith('/')}|ForEach-Object{$_.FullName})
    if((($expectedNames|Sort-Object) -join '|') -cne (($actualNames|Sort-Object) -join '|')){throw 'Unlisted/missing ZIP files.'}
    if(@($actualNames|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate/case-colliding ZIP entries.'}
    foreach($entry in $manifest.entries){
        $path=Join-Path $payload $entry.path
        if((Hash $path) -cne $entry.sha256 -or (Get-Item -LiteralPath $path).Length -ne $entry.bytes){throw 'Native payload/manifest hash mismatch.'}
        $zipped=$zip.GetEntry($entry.path)
        if($null -eq $zipped -or $zipped.Length -ne $entry.bytes){throw 'ZIP manifest entry missing/changed.'}
        $stream=$zipped.Open()
        try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}finally{$stream.Dispose()}
        if($hash -cne $entry.sha256){throw 'Actual ZIP bytes differ from payload manifest.'}
    }
}finally{$zip.Dispose()}
$contract=Get-Content (Join-Path $repo 'docs/releases/0.10.0/release-contract.json') -Raw|ConvertFrom-Json
foreach($path in $contract.bundledDocumentation.requiredFiles){if(-not(Test-Path -LiteralPath (Join-Path $payload $path) -PathType Leaf)){throw 'Native guide missing.'}}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $payload 'docs') -Recurse -Filter '*.md' -File){if([IO.File]::ReadAllText($file.FullName) -match '[\p{IsCJKUnifiedIdeographs}]'){throw 'Untranslated bundled guide.'}}
& (Join-Path $PSScriptRoot 'Test-ReleaseDocumentation.ps1') -PayloadRoot $payload -SourceCommit $SourceCommit -Version $release.version
$result=@{schemaVersion=1;status='passed';version=$release.version;sourceCommit=$SourceCommit;packages=$packages.ToArray();nativeVersions=$nativeVersions;nativePayloadEntries=$manifest.entries.Count;requiredGuidesPresent=$true;zipPayloadHashesEqual=$true;sourceBoundComplianceClaim=$false;independentMachineAndHuman='separate pending gates'}
[IO.File]::WriteAllText((Join-Path $output 'verification.json'),(($result|ConvertTo-Json -Depth 15)+"`n"),[Text.UTF8Encoding]::new($false))
Write-Output ('artifact-identity=passed packages=5 native-entries='+$manifest.entries.Count)
