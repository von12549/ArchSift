[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$OutputDirectory,
  [Parameter(Mandatory)][string]$TestPackageCache,
  [Parameter(Mandatory)][string]$TargetPackageCache
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sources=@(
  'src/ArchSift.InProject',
  'src/ArchSift.InProject.Runtime',
  'src/ArchSift.InProject.Maintenance',
  'src/ArchSift.InProject.TestBridge',
  'assets/inproject',
  'docs/migration/inproject-third-party-inventory.json',
  'docs/performance/inproject-0.1-budget.json',
  'docs/guides/inproject-0.1-maintenance.md',
  'scripts/Build-InProjectDevPackage.ps1',
  'scripts/Build-InProjectCandidate.ps1',
  'scripts/Measure-InProjectIp07.ps1',
  'scripts/Test-InProjectIp07Lifecycle.ps1',
  'scripts/Test-InProjectIp07Candidate.ps1',
  'scripts/Test-InProjectIp07Regression.ps1',
  'scripts/Update-InProjectThirdPartyInventory.ps1',
  'tests/ArchSift.ArchitectureTests/DependencyDirectionTests.cs',
  'tests/ArchSift.IntegrationTests/InProjectInstallTests.cs'
)
& git -C $repo diff --quiet HEAD -- $sources
if($LASTEXITCODE -ne 0){throw 'Candidate source differs from the fixed source commit.'}
$untracked=@(& git -C $repo ls-files --others --exclude-standard -- $sources)
if($LASTEXITCODE -ne 0 -or $untracked.Count -ne 0){throw 'Candidate source has untracked files.'}
$assemblyInfo=Join-Path $repo 'src/ArchSift.InProject/obj/Release/net10.0/ArchSift.InProject.AssemblyInfo.cs'
if(-not [IO.File]::Exists($assemblyInfo) -or
   [IO.File]::ReadAllText($assemblyInfo) -notmatch 'AssemblyInformationalVersionAttribute\("0\.1\.0"\)'){
  throw 'Candidate CLI must be built as Release/net10.0 with VersionSuffix empty.'
}
$package=& (Join-Path $PSScriptRoot 'Build-InProjectDevPackage.ps1') -OutputDirectory $OutputDirectory -Version '0.1.0' -IncludeMaintenance -OfflineTestPackageCache $TestPackageCache -OfflineTargetPackageCache $TargetPackageCache
if(-not $package -or -not [IO.File]::Exists($package.path)){throw 'Candidate package was not produced.'}
$metadata=[ordered]@{
  schemaVersion=1
  product='ArchSift.InProject'
  version='0.1.0'
  rid='win-x64'
  sourceCommit=$package.sourceCommit
  packagePath=$package.path
  packageSha256=$package.sha256
  manifestSha256=$package.manifestSha256
  files=$package.files
  createdUtc=[DateTimeOffset]::UtcNow.ToString('O')
}
$record=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'candidate-metadata.json'
if([IO.File]::Exists($record)){throw 'Candidate metadata already exists.'}
[IO.File]::WriteAllText($record,(ConvertTo-Json -InputObject $metadata -Depth 5)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
[pscustomobject]$metadata
