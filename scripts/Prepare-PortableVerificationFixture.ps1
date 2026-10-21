[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$FixtureRoot,
    [Parameter(Mandatory)][string]$LocalFeed,
    [Parameter(Mandatory)][string]$PackageCache,
    [ValidateSet('Debug','Release')][string]$Configuration='Release'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'HostState.ps1')
$baseline=Get-ArchSiftHostHash
$root=[IO.Path]::GetFullPath($FixtureRoot)
$feed=[IO.Path]::GetFullPath($LocalFeed)
$cache=[IO.Path]::GetFullPath($PackageCache)
$comparison=if($IsWindows){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
if($root.Equals($repo,$comparison) -or $root.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,$comparison) -or
    $repo.StartsWith($root+[IO.Path]::DirectorySeparatorChar,$comparison) -or (Test-Path -LiteralPath $root)){
    throw 'A fresh synthetic fixture directory outside the repository is required.'
}
if(-not(Test-Path -LiteralPath $feed -PathType Container)){throw 'Explicit offline feed is missing.'}
$cli=Join-Path $repo "src/ArchSift.Cli/bin/$Configuration/net10.0/archsift.dll"
if(-not(Test-Path -LiteralPath $cli)){throw 'Build the current CLI before preparing the synthetic fixture.'}
$source=Join-Path $root 'source'
$state=Join-Path $root 'state'
[void][IO.Directory]::CreateDirectory($source)
[void][IO.Directory]::CreateDirectory((Join-Path $state 'config'))
[void][IO.Directory]::CreateDirectory($cache)
function Save([string]$Path,$Value){ConvertTo-Json -InputObject $Value -Depth 35|Set-Content -LiteralPath $Path -Encoding utf8NoBOM}
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Invoke-FixtureDotnet([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -ErrorAction Stop).Source)
    $start.WorkingDirectory=$source;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $root 'cli-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
    $start.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE']='false'
    $start.Environment['DOTNET_NOLOGO']='1'
    $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
    foreach($key in @('ARCHSIFT_SP02_REPORT_FIXTURE','ARCHSIFT_TEST_GATE_CONFIG','ARCHSIFT_LT01_NEGATIVE','IFX_UPDATE_SNAPSHOTS')){[void]$start.Environment.Remove($key)}
    $process=[Diagnostics.Process]::Start($start)
    try{
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw 'Synthetic fixture process timeout.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $root ($Name+'.stdout.log')),$stdout.Result,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $root ($Name+'.stderr.log')),$stderr.Result,[Text.UTF8Encoding]::new($false))
        if((Get-ArchSiftHostHash) -cne $baseline){throw 'Host drift; preserve evidence without repair.'}
        if($process.ExitCode -ne 0){throw "Synthetic $Name failed; inspect retained logs."}
        return $stdout.Result.Trim()
    }finally{$process.Dispose()}
}
try{
    Save (Join-Path $root 'host-before.json') @{sha256=$baseline}
    Save (Join-Path $source 'global.json') @{sdk=@{version='10.0.303';rollForward='disable';allowPrerelease=$false}}
    '<configuration><packageSources><clear /></packageSources></configuration>'|Set-Content (Join-Path $source 'NuGet.Config') -Encoding utf8NoBOM
    $version=(Invoke-FixtureDotnet 'tool-version' @($cli,'--version')) -replace '^archsift ',''
    if($version -notmatch '^\d+\.\d+\.\d+[-+A-Za-z0-9.]*$'){throw 'Unexpected current CLI version.'}
    $reference=[Security.SecurityElement]::Escape((Join-Path $repo 'src/ArchSift.Testing/ArchSift.Testing.csproj'))
    $project=Join-Path $source 'Portable.Architecture.Tests.csproj'
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile><UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner><NuGetAudit>false</NuGetAudit><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
  <ItemGroup><PackageReference Include="xunit.v3" Version="[4.0.1]"/><ProjectReference Include="$reference"/></ItemGroup>
</Project>
"@|Set-Content -LiteralPath $project -Encoding utf8NoBOM
    Copy-Item -LiteralPath (Join-Path $repo 'samples/portable-verification/PortableTests.cs') -Destination $source
    Copy-Item -LiteralPath (Join-Path $repo 'samples/ifx-formal/ArchSiftCaseIdentityAttribute.cs') -Destination $source
    $rules=@{schemaVersion=1;id='source-archive-synthetic';version='1.0.0';description='Owned synthetic verification fixture; no IFX adoption';rules=@(@{id='synthetic-no-transactions';type='type-dependency';enabled=$true;scope=@{kind='type';match='exact';value='Synthetic.AllowedSource'};parameters=@{source=@{kind='type';match='exact';value='Synthetic.AllowedSource'};forbiddenTarget=@{kind='type';match='exact';value='System.Transactions.TransactionScope'}};severity='warning';reason='Synthetic verification only'});exceptions=@()}
    Save (Join-Path $source 'rules.json') $rules
    $textType='System.String, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e'
    $cases=@(foreach($value in @('first','second')){@{group='synthetic-original';identity=@{assembly='Portable.Architecture.Tests';type='Synthetic.PortableTests';method='OriginalRows';parameterTypes=@($textType);arguments=@(@{declaredType=$textType;runtimeType=$textType;kind='string';value=$value})}}})
    Save (Join-Path $source 'required-cases.json') @{schemaVersion=1;kind='archsift-required-cases';sdk='10.0.303';runner='mtp-ctrf/xunit-v3-4.0.1';targetFramework='net10.0';packageVersion=$version;cases=$cases}
    Save (Join-Path $source 'policy.json') @{schemaVersion=1;version='source-archive-synthetic';rulesets=@(@{path='rules.json';sha256=(Hash (Join-Path $source 'rules.json'))});sources=@()}
    # Initial restore creates a fresh synthetic lock. The production gate must then restore locked/offline.
    [void](Invoke-FixtureDotnet 'seed-lock' @('restore',$project,'--configfile',(Join-Path $source 'NuGet.Config'),'--source',$feed,'--packages',$cache,'--disable-parallel','--disable-build-servers','-p:NuGetAudit=false'))
    $configPath=Join-Path $state 'config/full.json'
    $run=Join-Path $state 'runs/full'
    Save $configPath @{schemaVersion=2;targetRoot=$source;testProject='Portable.Architecture.Tests.csproj';testExecutableName='Portable.Architecture.Tests';targetFramework='net10.0';configuration=$Configuration;nuGetConfig='NuGet.Config';localFeed=$feed;packageCache=$cache;rulesets=@('rules.json');runDirectory=$run;dotnetCliHome=(Join-Path $run 'cli-home');runnerFormat='mtp-ctrf';auditMode='offline-architecture';toolIdentity=$version;policyFile='policy.json';policySha256=(Hash (Join-Path $source 'policy.json'));requiredCases='required-cases.json';requiredCasesSha256=(Hash (Join-Path $source 'required-cases.json'))}
    [void](Invoke-FixtureDotnet 'portable-gate' @($cli,'test-gate','--config',$configPath))
    $report=Get-Content (Join-Path $run 'architecture-test-run.json') -Raw|ConvertFrom-Json
    $ctrf=Get-Content (Join-Path $run 'test-results.ctrf.json') -Raw|ConvertFrom-Json
    if($report.outcome.runGate -cne 'allow' -or -not $report.testScope.coverageComplete -or
        $ctrf.results.summary.tests -ne 3 -or $ctrf.results.summary.passed -ne 3 -or
        $ctrf.results.summary.failed -ne 0 -or $ctrf.results.summary.skipped -ne 0 -or
        $report.process.audit.status -cne 'not-run'){throw 'Synthetic fixture coverage/audit contract differs.'}
    Save (Join-Path $root 'fixture.json') @{kind='archsift-owned-source-archive-synthetic';sdk='10.0.303';configuration=$Configuration;toolVersion=$version;actualNativeCases=3;passed=3;failed=0;skipped=0;coverageComplete=$true;policyAdoption=$false;report=@{path=(Join-Path $run 'architecture-test-run.json');sha256=(Hash (Join-Path $run 'architecture-test-run.json'))};hostHash=$baseline}
    Write-Output "Synthetic portable fixture ready: 3 passed / 0 failed / 0 skipped; $root"
}finally{
    $after=Get-ArchSiftHostHash
    Save (Join-Path $root 'host-after.json') @{before=$baseline;after=$after;equal=($after -ceq $baseline)}
    if($after -cne $baseline){throw 'Host drift; no repair.'}
}
