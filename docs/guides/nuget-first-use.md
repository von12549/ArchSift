# NuGet first use: a new project

This recipe applies to ArchSift 0.11.0's five-package release. Public availability is established only by the matching publication record, not by this source guide. Before public release, use the final review assets supplied by the maintainer and their manifest. The package includes the matching Workbench; no installer or external startup script is needed.

The following adoption block is for a **new project integration** with no existing .archsift/ directory, NuGet.Config or tool manifest. For an existing integration, review and merge those files instead of replacing them. Obtain ArchSift.Cli.0.11.0.nupkg from the verified release assets or configured registry, and take its SHA-256 from the matching artifact manifest/checksum channel. Never fill the expected hash by hashing an untrusted download alone. Nuget.org's repository-signed archive may have a different hash from the original submission; use the published served hash for a served archive.

Save the block below as an external temporary script, then provide the four mandatory inputs. It performs no dotnet command, installs no global tool and modifies only the explicitly selected project integration files. TargetRoot must be an existing absolute source directory; ProjectId must be a unique stable ID. The template's default external state root is C:/ArchSift-lab; change state.root or use the allowed ignored local override when needed. No cache/feed/runtime directories need global registration.

```powershell
param(
  [Parameter(Mandatory)][string]$PackagePath,
  [Parameter(Mandatory)][string]$ExpectedSha256,
  [Parameter(Mandatory)][string]$TargetRoot,
  [Parameter(Mandatory)][string]$ProjectId
)
$ErrorActionPreference='Stop'
if(-not [IO.Path]::IsPathFullyQualified($TargetRoot) -or
   -not (Test-Path -LiteralPath $TargetRoot -PathType Container) -or
   $ProjectId -notmatch '^[a-z0-9][a-z0-9-]{0,63}$' -or
   $ExpectedSha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid adoption inputs.' }
$TargetRoot=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($TargetRoot))
foreach($relative in @('.archsift','NuGet.Config','.config/dotnet-tools.json')) {
  if(Test-Path -LiteralPath (Join-Path $TargetRoot $relative)) { throw 'Existing integration requires a reviewed merge.' }
}
if((Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash -ine $ExpectedSha256) { throw 'Package checksum mismatch.' }
$zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackagePath))
try {
  $prefix='tools/net10.0/any/templates/project/.archsift/'
  $pairs=@(@('start.ps1.template','start.ps1'),@('workspace.json','workspace.json'),@('README.md','README.md'))
  foreach($pair in $pairs) { if($null -eq $zip.GetEntry($prefix+$pair[0])) { throw 'Package template is incomplete.' } }
  $entryRoot=Join-Path $TargetRoot '.archsift'
  [IO.Directory]::CreateDirectory($entryRoot)|Out-Null
  foreach($pair in $pairs) { [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry($prefix+$pair[0]),(Join-Path $entryRoot $pair[1]),$false) }
} finally { $zip.Dispose() }
$workspacePath=Join-Path $entryRoot 'workspace.json'
$workspace=Get-Content -LiteralPath $workspacePath -Raw|ConvertFrom-Json -AsHashtable
$workspace.project.id=$ProjectId
$workspace.project.root='..'
$workspace.project.entry=$null
$workspace.nuget.mode='configured'
$workspace.nuget.allowNetwork=$true
$workspace.nuget.configFile='../NuGet.Config'
$workspace.nuget.cacheDirectory=$null
$workspace|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $workspacePath -Encoding utf8NoBOM
[IO.Directory]::CreateDirectory((Join-Path $TargetRoot '.config'))|Out-Null
@{version=1;isRoot=$true;tools=@{'archsift.cli'=@{version='0.11.0';commands=@('archsift');rollForward=$false}}}|
  ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $TargetRoot '.config/dotnet-tools.json') -Encoding utf8NoBOM
$nuget='<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3"/></packageSources><disabledPackageSources><clear/></disabledPackageSources><fallbackPackageFolders><clear/></fallbackPackageFolders><packageSourceMapping><clear/><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>'
[IO.File]::WriteAllText((Join-Path $TargetRoot 'NuGet.Config'),$nuget)
$ignore=Join-Path $TargetRoot '.gitignore'
$rule='/.archsift/workspace.local.json'
if(-not (Test-Path -LiteralPath $ignore) -or -not (Get-Content -LiteralPath $ignore|Where-Object {$_ -ceq $rule})) {
  [IO.File]::AppendAllText($ignore,[Environment]::NewLine+$rule+[Environment]::NewLine)
}
Write-Output 'Project files prepared. Review workspace/configuration, then invoke the project entry.'
```

Review workspace.json and choose a project-root-relative solution entry if needed. The example permits network restore only from nuget.org; it is not appropriate for private-source or IFX projects without deliberate configuration review. For offline use, select mode offline, allowNetwork=false and the verified offline bundle's local-only NuGet.Config with its actual extracted feed path. Do not list audit/network fallback sources in that offline configuration.

After verifying the installed stable SDK, project global.json and compatible .NET 10 Core/ASP.NET frameworks, run the **absolute** adopted entry with `-Action Init`. It restores the pinned local tool and creates external state using package-owned initialization. Then use `-Action Doctor` and start the default Launcher or `-Mode Workbench`. Each command is reviewed/executed separately for human integration acceptance. PowerShell 7.4+ is required. The entry discovers existing host settings or accepts the allowed explicit runtime.dotnetPath; it does not install an SDK or change persisted environment/PATH/Profiles.

Standard NuGet cache selection is preserved; isolated child CLI homes disable global-tool PATH registration. Keep schema-1 startup configuration distinct from the schema-2 daily-test configuration printed by each invocation. Safe shutdown ends owned services; browser-tab closure alone does not. Unknown or busy state remains preserved. See [project workspace](project-workspace.md) for source/cache/state precedence, compatibility, diagnostics and rollback.
