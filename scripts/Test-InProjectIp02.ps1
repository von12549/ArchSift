param(
  [string]$LabRoot = 'D:\ArchSift-lab',
  [Parameter(Mandatory)][string]$TargetPackageSource
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lab = [IO.Path]::GetFullPath($LabRoot)
if ($lab.Equals($repo, [StringComparison]::OrdinalIgnoreCase) -or
    $lab.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
  throw 'Synthetic lab must be outside the source repository.'
}
$id = [Guid]::NewGuid().ToString('N')
$fixture = Join-Path $lab "fixtures/inproject-ip02-probe-$id"
$run = Join-Path $lab "runs/inproject-ip02-probe-$id"
[void][IO.Directory]::CreateDirectory($fixture)
[void][IO.Directory]::CreateDirectory($run)
[void][IO.Directory]::CreateDirectory((Join-Path $fixture 'A'))
[void][IO.Directory]::CreateDirectory((Join-Path $fixture 'B'))
[void][IO.Directory]::CreateDirectory((Join-Path $fixture 'C'))
[void][IO.Directory]::CreateDirectory((Join-Path $fixture 'D'))
[IO.File]::WriteAllText((Join-Path $fixture 'Product.slnx'), '<Solution><Project Path="A/A.csproj" /><Project Path="B/B.csproj" /><Project Path="C/C.csproj" /><Project Path="D/D.csproj" /></Solution>')
[IO.File]::WriteAllText((Join-Path $fixture 'A/A.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework><AssemblyName>Shared</AssemblyName><Version>1.0.0</Version></PropertyGroup></Project>')
[IO.File]::WriteAllText((Join-Path $fixture 'A/A.cs'), 'namespace A; public class Foo {}')
[IO.File]::WriteAllText((Join-Path $fixture 'B/B.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include="../A/A.csproj" /></ItemGroup></Project>')
[IO.File]::WriteAllText((Join-Path $fixture 'B/B.cs'), 'namespace B; public class Bar : A.Foo {}')
$entryMarker=Join-Path $run 'target-entry-marker.txt'
[IO.File]::WriteAllText((Join-Path $fixture 'B/ModuleInitializer.cs'),
  'internal static class Probe { [System.Runtime.CompilerServices.ModuleInitializer] internal static void Init() => System.IO.File.WriteAllText(@"' + $entryMarker + '", "executed"); }')
[IO.File]::WriteAllText((Join-Path $fixture 'C/C.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework><AssemblyName>Shared</AssemblyName><Version>2.0.0</Version><RuntimeFrameworkVersion>9.0.17</RuntimeFrameworkVersion><TargetLatestRuntimePatch>false</TargetLatestRuntimePatch></PropertyGroup></Project>')
[IO.File]::WriteAllText((Join-Path $fixture 'C/C.cs'), 'namespace C; public class Foo {}')
[IO.File]::WriteAllText((Join-Path $fixture 'D/D.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include="../C/C.csproj" /></ItemGroup></Project>')
[IO.File]::WriteAllText((Join-Path $fixture 'D/D.cs'), 'namespace D; public class Bar : C.Foo {}')
$projectIds=@{A='11111111-1111-1111-1111-111111111111';B='22222222-2222-2222-2222-222222222222';C='33333333-3333-3333-3333-333333333333';D='44444444-4444-4444-4444-444444444444'}
$solutionLines=[Collections.Generic.List[string]]::new()
$solutionLines.Add('Microsoft Visual Studio Solution File, Format Version 12.00')
$solutionLines.Add('# Visual Studio Version 17')
foreach($name in @('A','B','C','D')){
  $solutionLines.Add('Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "' + $name + '", "' + $name + '\' + $name + '.csproj", "{' + $projectIds[$name] + '}"')
  $solutionLines.Add('EndProject')
}
$solutionLines.Add('Global')
$solutionLines.Add('  GlobalSection(SolutionConfigurationPlatforms) = preSolution')
foreach($configuration in @('Debug','Release')){$solutionLines.Add("    $configuration|Any CPU = $configuration|Any CPU")}
$solutionLines.Add('  EndGlobalSection')
$solutionLines.Add('  GlobalSection(ProjectConfigurationPlatforms) = postSolution')
foreach($name in @('A','B','C','D')){
  foreach($configuration in @('Debug','Release')){
    $solutionLines.Add(('    {{{0}}}.{1}|Any CPU.ActiveCfg = {1}|Any CPU' -f $projectIds[$name],$configuration))
    $solutionLines.Add(('    {{{0}}}.{1}|Any CPU.Build.0 = {1}|Any CPU' -f $projectIds[$name],$configuration))
  }
}
$solutionLines.Add('  EndGlobalSection')
$solutionLines.Add('EndGlobal')
[IO.File]::WriteAllLines((Join-Path $fixture 'Product.sln'),$solutionLines)
[IO.File]::WriteAllText((Join-Path $fixture 'NuGet.Config'), '<configuration><packageSources><clear /></packageSources></configuration>')
function HashText([string]$value) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($value))).ToLowerInvariant() }
function ScopeHash([EnvironmentVariableTarget]$scope) {
  $values=[Environment]::GetEnvironmentVariables($scope); $ordered=[ordered]@{}
  foreach($key in @($values.Keys)|Sort-Object){$ordered[[string]$key]=[string]$values[$key]}
  HashText (ConvertTo-Json -InputObject $ordered -Compress -Depth 5)
}
function HostHash {
  $profiles=@()
  foreach($name in @('AllUsersAllHosts','AllUsersCurrentHost','CurrentUserAllHosts','CurrentUserCurrentHost')){
    $path=[string]$PROFILE.$name; $profiles+=@{name=$name;exists=[IO.File]::Exists($path);sha256=$(if([IO.File]::Exists($path)){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}else{$null})}
  }
  HashText (ConvertTo-Json -InputObject @{user=ScopeHash User;machine=ScopeHash Machine;processPath=HashText ([Environment]::GetEnvironmentVariable('Path','Process'));profiles=$profiles} -Compress -Depth 8)
}
$baseline=HostHash
$dotnet=(Get-Command dotnet -ErrorAction Stop).Source
$feed=Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'
$targetFeedUrl=$TargetPackageSource
function Run([string]$name,[string[]]$arguments){
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$fixture; $start.UseShellExecute=$false; $start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
  foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
  $start.Environment['NUGET_PACKAGES']=Join-Path $run 'packages'
  $start.Environment['ARCHSIFT_TEST_LAB_ROOT']=Join-Path $run 'test-fixtures'
  $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
  $process=[Diagnostics.Process]::Start($start)
  try{
    $out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync()
    if(-not $process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
    [Threading.Tasks.Task]::WaitAll($out,$err)
    [IO.File]::WriteAllText((Join-Path $run "$name.stdout.log"),$out.Result)
    [IO.File]::WriteAllText((Join-Path $run "$name.stderr.log"),$err.Result)
    $equal=(HostHash)-ceq$baseline
    Write-Output "$name exit=$($process.ExitCode) host-state-equal=$equal"
    if(-not $equal){throw 'Host drift; evidence retained.'}
    if($process.ExitCode -ne 0){Write-Output $out.Result;Write-Output $err.Result;throw "$name failed"}
  }finally{$process.Dispose()}
}
Write-Output "run=$run fixture=$fixture baseline-sha256=$baseline"
Run 'sdk' @('--version')
Run 'restore' @('restore','Product.sln','--configfile',(Join-Path $fixture 'NuGet.Config'),'--source',$targetFeedUrl,'--packages',(Join-Path $run 'packages'),'--disable-parallel','--disable-build-servers','-p:NuGetAudit=false','--verbosity','minimal')
Run 'build' @('build','Product.sln','--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-c','Debug','--verbosity','diagnostic')
Run 'evaluate-a' @('msbuild','A/A.csproj','-target:ResolveReferences','-property:TargetFramework=net8.0','-property:Configuration=Debug','-getProperty:TargetFramework,Configuration,TargetPath','-getItem:ReferencePath,ReferenceCopyLocalPaths')
Run 'evaluate-b' @('msbuild','B/B.csproj','-target:ResolveReferences','-property:TargetFramework=net10.0','-property:Configuration=Debug','-getProperty:TargetFramework,Configuration,TargetPath','-getItem:ReferencePath,ReferenceCopyLocalPaths')
Run 'evaluate-full' @('msbuild','B/B.csproj','-target:ResolveReferences','-property:TargetFramework=net10.0','-property:Configuration=Debug','-getProperty:MSBuildAllProjects,TargetFramework,Configuration,TargetPath','-getItem:ReferencePath,ReferenceCopyLocalPaths,Compile,Analyzer,AdditionalFiles,EmbeddedResource')
Run 'restore-runtime' @('restore',(Join-Path $repo 'src/ArchSift.InProject.Runtime/ArchSift.InProject.Runtime.csproj'),'--configfile',(Join-Path $repo 'NuGet.Config'),'--source',$feed,'--packages',(Join-Path $run 'packages'),'--disable-parallel','--disable-build-servers','--locked-mode','-p:NuGetAudit=false','--verbosity','minimal')
Run 'build-runtime' @('build',(Join-Path $repo 'src/ArchSift.InProject.Runtime/ArchSift.InProject.Runtime.csproj'),'--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-c','Release','--verbosity','minimal')
Run 'restore-cli' @('restore',(Join-Path $repo 'src/ArchSift.InProject/ArchSift.InProject.csproj'),'--configfile',(Join-Path $repo 'NuGet.Config'),'--source',$feed,'--packages',(Join-Path $run 'packages'),'--disable-parallel','--disable-build-servers','--locked-mode','-p:NuGetAudit=false','--verbosity','minimal')
Run 'build-cli' @('build',(Join-Path $repo 'src/ArchSift.InProject/ArchSift.InProject.csproj'),'--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-c','Release','--verbosity','minimal')
Run 'restore-bridge' @('restore',(Join-Path $repo 'src/ArchSift.InProject.TestBridge/ArchSift.InProject.TestBridge.csproj'),'--configfile',(Join-Path $repo 'NuGet.Config'),'--source',$feed,'--packages',(Join-Path $run 'packages'),'--disable-parallel','--disable-build-servers','--locked-mode','-p:NuGetAudit=false','--verbosity','minimal')
Run 'build-bridge' @('build',(Join-Path $repo 'src/ArchSift.InProject.TestBridge/ArchSift.InProject.TestBridge.csproj'),'--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-c','Release','--verbosity','minimal')
Run 'restore-cancel-probe' @('restore',(Join-Path $repo 'scripts/InProjectCancelProbe/ArchSift.InProject.CancelProbe.csproj'),'--configfile',(Join-Path $repo 'NuGet.Config'),'--source',$feed,'--packages',(Join-Path $run 'packages'),'--disable-parallel','--disable-build-servers','--locked-mode','-p:NuGetAudit=false','--verbosity','minimal')
Run 'build-cancel-probe' @('build',(Join-Path $repo 'scripts/InProjectCancelProbe/ArchSift.InProject.CancelProbe.csproj'),'--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-c','Release','--verbosity','minimal')
Run 'restore-integration-tests' @('restore',(Join-Path $repo 'tests/ArchSift.IntegrationTests/ArchSift.IntegrationTests.csproj'),'--configfile',(Join-Path $repo 'NuGet.Config'),'--source',$feed,'--packages',(Join-Path $run 'packages'),'--disable-parallel','--disable-build-servers','--locked-mode','-p:NuGetAudit=false','--verbosity','minimal')
Run 'test-inproject' @('test',(Join-Path $repo 'tests/ArchSift.IntegrationTests/ArchSift.IntegrationTests.csproj'),'--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-c','Release','--filter','FullyQualifiedName~InProjectBuildCaptureTests|FullyQualifiedName~InProjectInstallTests|FullyQualifiedName~InProjectGatePolicyTests','--verbosity','minimal')
git -C $fixture init -q
if($LASTEXITCODE -ne 0){throw 'git init failed'}
$package = & (Join-Path $repo 'scripts/Build-InProjectDevPackage.ps1') -OutputDirectory (Join-Path $run 'package')
if ((HostHash) -cne $baseline) { throw 'Host drift after package creation; evidence retained.' }
$zip=$package.path
$cli=Join-Path $repo 'src/ArchSift.InProject/bin/Release/net10.0/archsift-inproject.dll'
Run 'init' @($cli,'init','--target',$fixture,'--package',$zip,'--sha256',$package.sha256,'--solution','Product.sln')
$combinedFeed=Join-Path $run 'local-feed'
[void][IO.Directory]::CreateDirectory($combinedFeed)
function AddLocalPackage([string]$source){
  if(-not [IO.File]::Exists($source)){throw "Locked local package is missing: $source"}
  $destination=Join-Path $combinedFeed ([IO.Path]::GetFileName($source))
  if([IO.File]::Exists($destination)){
    if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash){throw "Conflicting local package: $destination"}
  }else{[IO.File]::Copy($source,$destination)}
}
foreach($packageFile in Get-ChildItem (Join-Path $run 'packages') -Recurse -Filter '*.nupkg' -File){AddLocalPackage $packageFile.FullName}
$testLock=Get-Content (Join-Path $repo 'assets/inproject/tests/packages.lock.json') -Raw | ConvertFrom-Json
foreach($entry in $testLock.dependencies.'net10.0'.PSObject.Properties){
  $id=$entry.Name.ToLowerInvariant();$version=([string]$entry.Value.resolved).ToLowerInvariant()
  AddLocalPackage (Join-Path $feed "$id/$version/$id.$version.nupkg")
}
[IO.File]::WriteAllText((Join-Path $fixture '.archsift/state/local-settings.json'),(ConvertTo-Json -InputObject @{schemaVersion=1;localFeed=$combinedFeed} -Compress))
$installed=Join-Path $fixture '.archsift/runtime/archsift-inproject.dll'
Run 'inproject-build' @($installed,'build','--target',$fixture)
if([IO.File]::Exists($entryMarker)){throw 'Target module initializer executed during analysis.'}
Write-Output 'target-module-initializer-executed=False'
function RunExpectedFailure([string]$name,[string[]]$arguments,[int]$expected){
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$fixture; $start.UseShellExecute=$false; $start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
  foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $start.Environment['NUGET_PACKAGES']=Join-Path $run 'packages'
  $process=[Diagnostics.Process]::Start($start)
  try{
    $out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync()
    if(-not $process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "$name timed out"}
    [Threading.Tasks.Task]::WaitAll($out,$err)
    [IO.File]::WriteAllText((Join-Path $run "$name.stdout.log"),$out.Result)
    [IO.File]::WriteAllText((Join-Path $run "$name.stderr.log"),$err.Result)
    $equal=(HostHash)-ceq$baseline
    Write-Output "$name exit=$($process.ExitCode) expected=$expected host-state-equal=$equal"
    if(-not $equal){throw 'Host drift; evidence retained.'}
    if($process.ExitCode -ne $expected){throw "$name unexpected exit"}
  }finally{$process.Dispose()}
}
$bPath=Join-Path $fixture 'B/B.csproj'
$originalB=[IO.File]::ReadAllBytes($bPath)
try{
  [IO.File]::WriteAllText($bPath,([Text.Encoding]::UTF8.GetString($originalB)).Replace('</Project>','<Target Name="Unreviewed" BeforeTargets="Build"><Exec Command="echo unsafe" /></Target></Project>'))
  RunExpectedFailure 'reject-custom-target' @($installed,'build','--target',$fixture) 5
}finally{[IO.File]::WriteAllBytes($bPath,$originalB)}
try{
  $externalDirectory=Join-Path $run 'external-reference'
  [void][IO.Directory]::CreateDirectory($externalDirectory)
  $externalReference=Join-Path $externalDirectory 'Shared.dll'
  [IO.File]::Copy((Join-Path $fixture 'A/bin/Debug/net8.0/Shared.dll'),$externalReference)
  [IO.File]::WriteAllText($bPath,([Text.Encoding]::UTF8.GetString($originalB)).Replace('<ProjectReference Include="../A/A.csproj" />','<Reference Include="Shared"><HintPath>' + $externalReference + '</HintPath></Reference>'))
  RunExpectedFailure 'reject-external-reference' @($installed,'build','--target',$fixture) 5
}finally{[IO.File]::WriteAllBytes($bPath,$originalB)}
$projectPath=Join-Path $fixture '.archsift/project.json'
$originalProject=[IO.File]::ReadAllBytes($projectPath)
try{
  $configuration=Get-Content $projectPath -Raw | ConvertFrom-Json
  $configuration.build.contexts=@(@{project='../A/A.csproj';targetFramework='net10.0'})
  [IO.File]::WriteAllText($projectPath,(ConvertTo-Json -InputObject $configuration -Depth 8))
  RunExpectedFailure 'reject-wrong-tfm' @($installed,'build','--target',$fixture) 5
}finally{[IO.File]::WriteAllBytes($projectPath,$originalProject)}
$solutionPath=Join-Path $fixture 'Product.sln'
$originalSolution=[IO.File]::ReadAllBytes($solutionPath)
try{
  $wrong=([Text.Encoding]::UTF8.GetString($originalSolution)).Replace('{22222222-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU','{22222222-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Release|Any CPU')
  [IO.File]::WriteAllText($solutionPath,$wrong)
  RunExpectedFailure 'reject-solution-mapping' @($installed,'build','--target',$fixture) 5
}finally{[IO.File]::WriteAllBytes($solutionPath,$originalSolution)}
$aPath=Join-Path $fixture 'A/A.csproj'
$originalA=[IO.File]::ReadAllBytes($aPath)
try{
  [IO.File]::WriteAllText($aPath,([Text.Encoding]::UTF8.GetString($originalA)).Replace('<TargetFramework>net8.0</TargetFramework>','<TargetFrameworks>net8.0;net9.0</TargetFrameworks>'))
  RunExpectedFailure 'reject-ambiguous-multitarget' @($installed,'build','--target',$fixture) 5
}finally{[IO.File]::WriteAllBytes($aPath,$originalA)}
try{
  [IO.File]::WriteAllText($bPath,([Text.Encoding]::UTF8.GetString($originalB)).Replace('</Project>','<ItemGroup><Analyzer Include="../unreviewed.dll" /></ItemGroup></Project>'))
  RunExpectedFailure 'reject-unknown-generator' @($installed,'build','--target',$fixture) 5
}finally{[IO.File]::WriteAllBytes($bPath,$originalB)}
try{
  [IO.File]::WriteAllText($bPath,([Text.Encoding]::UTF8.GetString($originalB)).Replace('</Project>','<ItemGroup><Compile Include="Absent.cs" /></ItemGroup></Project>'))
  RunExpectedFailure 'reject-missing-source' @($installed,'build','--target',$fixture) 5
}finally{[IO.File]::WriteAllBytes($bPath,$originalB)}
$cancelProbe=Join-Path $repo 'scripts/InProjectCancelProbe/bin/Release/net10.0/ArchSift.InProject.CancelProbe.dll'
Run 'reject-cancel' @($cancelProbe,$fixture)
function RunSourceDrift {
  $known=@(Get-ChildItem (Join-Path $fixture '.archsift/state/build') -Directory | ForEach-Object Name)
  $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
  $start.WorkingDirectory=$fixture; $start.UseShellExecute=$false; $start.CreateNoWindow=$true
  $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
  foreach($argument in @($installed,'build','--target',$fixture)){$start.ArgumentList.Add($argument)}
  $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
  $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
  $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
  $start.Environment['NUGET_PACKAGES']=Join-Path $run 'packages'
  $process=[Diagnostics.Process]::Start($start)
  try{
    $out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync()
    $changed=$false
    $deadline=[DateTime]::UtcNow.AddMinutes(3)
    while(-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline){
      $new=Get-ChildItem (Join-Path $fixture '.archsift/state/build') -Directory | Where-Object { $_.Name -notin $known -and [IO.File]::Exists((Join-Path $_.FullName 'owner.json')) } | Select-Object -First 1
      if($new){[IO.File]::AppendAllText((Join-Path $fixture 'B/B.cs'),"`n// changed after run snapshot began`n");$changed=$true;break}
      [Threading.Thread]::Sleep(50)
    }
    if(-not $process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw 'drift build timed out'}
    [Threading.Tasks.Task]::WaitAll($out,$err)
    [IO.File]::WriteAllText((Join-Path $run 'reject-source-drift.stdout.log'),$out.Result)
    [IO.File]::WriteAllText((Join-Path $run 'reject-source-drift.stderr.log'),$err.Result)
    $equal=(HostHash)-ceq$baseline
    Write-Output "reject-source-drift exit=$($process.ExitCode) mutated=$changed host-state-equal=$equal"
    if(-not $equal){throw 'Host drift; evidence retained.'}
    if(-not $changed -or $process.ExitCode -ne 4){throw 'Source drift was not blocked as inconclusive'}
  }finally{$process.Dispose()}
}
$sourcePath=Join-Path $fixture 'B/B.cs'
$originalSource=[IO.File]::ReadAllBytes($sourcePath)
try{RunSourceDrift}finally{[IO.File]::WriteAllBytes($sourcePath,$originalSource)}
try{
  [IO.File]::WriteAllText($sourcePath,'namespace B; public class Bar : A.Foo { syntax error }')
  RunExpectedFailure 'reject-build-failure' @($installed,'build','--target',$fixture) 3
}finally{[IO.File]::WriteAllBytes($sourcePath,$originalSource)}
try{
  [IO.File]::WriteAllText($bPath,([Text.Encoding]::UTF8.GetString($originalB)).Replace('</Project>','<ItemGroup><PackageReference Include="Definitely.Missing.InProject.Package" Version="999.0.0" /></ItemGroup></Project>'))
  RunExpectedFailure 'reject-restore-failure' @($installed,'build','--target',$fixture) 3
  $failed=Get-ChildItem (Join-Path $fixture '.archsift/reports') -Recurse -Filter build-state.json | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
  if(-not $failed){throw 'Failed restore has no state record'}
  $state=Get-Content $failed.FullName -Raw | ConvertFrom-Json
  $verdict=Get-Content (Join-Path $failed.DirectoryName 'binding-verdict.json') -Raw | ConvertFrom-Json
  if($state.state -ne 'failed' -or $state.sourceBound -ne $false -or $verdict.sourceBound -ne $false){throw 'Failed restore retained a current success'}
  Write-Output "failed-restore-state=$($state.state) source-bound=$($verdict.sourceBound)"
}finally{[IO.File]::WriteAllBytes($bPath,$originalB)}
