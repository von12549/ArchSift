param(
  [Parameter(Mandatory)][string]$TestPackageCache,
  [Parameter(Mandatory)][string]$TargetPackageCache
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lock=Get-Content (Join-Path $repo 'assets/inproject/tests/packages.lock.json') -Raw|ConvertFrom-Json
$w01=Get-Content (Join-Path $repo 'docs/dependencies/w01/package-inventory.json') -Raw|ConvertFrom-Json
$items=@()
foreach($entry in $lock.dependencies.'net10.0'.PSObject.Properties){
  $items+=@{id=$entry.Name.ToLowerInvariant();version=([string]$entry.Value.resolved).ToLowerInvariant();cache=[IO.Path]::GetFullPath($TestPackageCache);role='xunit-test-dependency'}
}
foreach($reference in @(
  'microsoft.netcore.app.ref/8.0.30','microsoft.netcore.app.ref/9.0.19',
  'microsoft.aspnetcore.app.ref/8.0.30','microsoft.aspnetcore.app.ref/9.0.19',
  'microsoft.windowsdesktop.app.ref/8.0.30','microsoft.windowsdesktop.app.ref/9.0.19')){
  $parts=$reference.Split('/')
  $items+=@{id=$parts[0];version=$parts[1];cache=[IO.Path]::GetFullPath($TargetPackageCache);role='offline-target-reference'}
}
$packages=@(foreach($item in $items|Sort-Object id,version){
  $name="$($item.id).$($item.version).nupkg"
  $path=Join-Path $item.cache "$($item.id)/$($item.version)/$name"
  if(-not [IO.File]::Exists($path)){throw "Missing locked package: $name"}
  $archive=[IO.Compression.ZipFile]::OpenRead($path)
  try{
    $nuspec=@($archive.Entries|Where-Object FullName -like '*.nuspec')
    if($nuspec.Count -ne 1){throw "Ambiguous nuspec: $name"}
    $reader=[IO.StreamReader]::new($nuspec[0].Open())
    try{[xml]$xml=$reader.ReadToEnd()}finally{$reader.Dispose()}
    $meta=$xml.package.metadata
    $prior=$w01.packages|Where-Object {$_.id -ieq $item.id -and $_.version -eq $item.version}|Select-Object -First 1
    $license=if($meta.license.InnerText){[string]$meta.license.InnerText}elseif($prior){[string]$prior.license}else{'UNRESOLVED'}
    if($license -eq 'UNRESOLVED'){throw "Unresolved package license: $name"}
    [ordered]@{
      id=$item.id;version=$item.version;role=$item.role
      packagePath="offline/$name"
      sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
      license=$license;licenseMetadataKind=$(if($meta.license.InnerText){'nuspec-expression'}else{'legacy-url-and-W01-reviewed-inventory'})
      licenseUrl=[string]$meta.licenseUrl
      copyright=[string]$meta.copyright
      authors=[string]$meta.authors
    }
  }finally{$archive.Dispose()}
})
$record=[ordered]@{
  schemaVersion=1;product='ArchSift.InProject';version='0.1.0'
  lockedTestProject='assets/inproject/tests/packages.lock.json'
  referenceVersions=@('8.0.30','9.0.19')
  packageCount=$packages.Count
  notes=@(
    'This is the actual offline nupkg set bundled for the dedicated xUnit project and net8/net9 target reference restore, not the full runtime DLL list.',
    'Runtime DLL licenses and notices remain in runtime/THIRD-PARTY-NOTICES.md and the archived license texts in licenses/.',
    'xunit.abstractions 2.0.3 carries a legacy license URL; Apache-2.0 is corroborated by the existing W01 reviewed inventory and archived xUnit notice.',
    'Package SHA-256 identifies each bundled nupkg byte-for-byte. Build-InProjectDevPackage.ps1 checks these digests before packaging.'
  )
  packages=$packages
}
$output=Join-Path $repo 'docs/migration/inproject-third-party-inventory.json'
[IO.File]::WriteAllText($output,(ConvertTo-Json -InputObject $record -Depth 8)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output "inventory=$output package-count=$($packages.Count)"
