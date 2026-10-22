[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$FixtureRoot,
    [Parameter(Mandatory)][string[]]$OfflineSources,
    [Parameter(Mandatory)][string]$PackageCache,
    [Parameter(Mandatory)][string]$Version
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
$inputs=Get-ArchSiftReleaseInputs $repo
if($Version -cne $inputs.version){throw 'Synthetic consumer must use the unchanged central release version.'}
$root=Assert-ArchSiftExternalPath $repo $FixtureRoot
$cache=Assert-ArchSiftExternalPath $repo $PackageCache
if(Test-Path -LiteralPath $root){throw 'A fresh external synthetic fixture root is required.'}
$feeds=@(foreach($feed in $OfflineSources){
    $full=[IO.Path]::GetFullPath($feed)
    if(-not(Test-Path -LiteralPath $full -PathType Container)){throw 'Explicit offline source is missing.'}
    $full
})
if($feeds.Count -eq 0 -or @($feeds|Sort-Object -Unique).Count -ne $feeds.Count){throw 'Explicit unique offline sources are required.'}
$source=Join-Path $root 'target'
$null=Assert-ArchSiftExternalPath $source $cache
if((Test-Path -LiteralPath $cache) -and @(Get-ChildItem -LiteralPath $cache -Force).Count){throw 'Package qualification requires an empty external cache.'}
$config=Join-Path $root 'state/config/full.json'
$run=Join-Path $root 'state/runs/full'
foreach($directory in @($source,(Join-Path $source '.config'),(Split-Path $config),$cache)){
    [void][IO.Directory]::CreateDirectory($directory)
}
function Save([string]$Path,$Value){[IO.File]::WriteAllText($Path,((ConvertTo-Json -InputObject $Value -Depth 35)+"`n"),[Text.UTF8Encoding]::new($false))}
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
Save (Join-Path $source 'global.json') ([ordered]@{sdk=[ordered]@{version=$inputs.profile.sdk;rollForward='disable';allowPrerelease=$false}})
Save (Join-Path $source '.config/dotnet-tools.json') ([ordered]@{version=1;isRoot=$true;tools=[ordered]@{'archsift.cli'=[ordered]@{version=$Version;commands=@('archsift')}}})
$feedElements=for($index=0;$index -lt $feeds.Count;$index++){
    '<add key="approved-'+$index+'" value="'+[Security.SecurityElement]::Escape($feeds[$index])+'" />'
}
[IO.File]::WriteAllText((Join-Path $source 'NuGet.Config'),'<configuration><packageSources><clear />'+($feedElements -join '')+'</packageSources></configuration>',[Text.UTF8Encoding]::new($false))
$project=Join-Path $source 'Portable.Architecture.Tests.csproj'
[IO.File]::WriteAllText($project,@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <NuGetAudit>false</NuGetAudit><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ArchSift.Testing" Version="[$Version]" />
    <PackageReference Include="xunit.v3" Version="[4.0.1]" />
  </ItemGroup>
</Project>
"@,[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $repo 'samples/portable-verification/PortableTests.cs') -Destination $source
Copy-Item -LiteralPath (Join-Path $repo 'samples/ifx-formal/ArchSiftCaseIdentityAttribute.cs') -Destination $source
[IO.File]::WriteAllText((Join-Path $source 'PackageContractTests.cs'),@'
using System.Text.Json;
using ArchSift.Testing;
using Xunit;
namespace Synthetic;
public sealed class PackageContractTests
{
    [Fact]
    public void ProductPackagesMatchApprovedClosure()
    {
        using var config = JsonDocument.Parse(File.ReadAllBytes(
            Environment.GetEnvironmentVariable("ARCHSIFT_TEST_GATE_CONFIG")
            ?? throw new InvalidOperationException("Synthetic gate configuration is required.")));
        var root = config.RootElement.GetProperty("targetRoot").GetString()!;
        PackageToolApproval.Verify(root, "Portable.Architecture.Tests.csproj", "package-approval.json");
    }
}
'@,[Text.UTF8Encoding]::new($false))
$rule=[ordered]@{schemaVersion=1;id='generic-workbench-consumer';version='1.0.0';description='Owned synthetic package qualification; not a consumer policy';rules=@([ordered]@{id='synthetic-no-transactions';type='type-dependency';enabled=$true;scope=[ordered]@{kind='type';match='exact';value='Synthetic.AllowedSource'};parameters=[ordered]@{source=[ordered]@{kind='type';match='exact';value='Synthetic.AllowedSource'};forbiddenTarget=[ordered]@{kind='type';match='exact';value='System.Transactions.TransactionScope'}};severity='warning';reason='Independent synthetic package qualification'});exceptions=@()}
Save (Join-Path $source 'rules.json') $rule
$textType='System.String, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e'
$cases=@(foreach($value in @('first','second')){
    [ordered]@{group='synthetic-original';identity=[ordered]@{assembly='Portable.Architecture.Tests';type='Synthetic.PortableTests';method='OriginalRows';parameterTypes=@($textType);arguments=@([ordered]@{declaredType=$textType;runtimeType=$textType;kind='string';value=$value})}}
})
$cases+=[ordered]@{group='synthetic-package-contract';identity=[ordered]@{assembly='Portable.Architecture.Tests';type='Synthetic.PackageContractTests';method='ProductPackagesMatchApprovedClosure';parameterTypes=@();arguments=@()}}
Save (Join-Path $source 'required-cases.json') ([ordered]@{schemaVersion=1;kind='archsift-required-cases';sdk=$inputs.profile.sdk;runner='mtp-ctrf/xunit-v3-4.0.1';targetFramework='net10.0';packageVersion=$Version;cases=$cases})
Save (Join-Path $source 'policy.json') ([ordered]@{schemaVersion=1;version='generic-synthetic-1';rulesets=@([ordered]@{path='rules.json';sha256=(Hash (Join-Path $source 'rules.json'))});sources=@()})
Save $config ([ordered]@{schemaVersion=2;targetRoot=$source;testProject='Portable.Architecture.Tests.csproj';testExecutableName='Portable.Architecture.Tests';targetFramework='net10.0';configuration='Release';nuGetConfig='NuGet.Config';localFeed=$feeds[0];packageCache=$cache;rulesets=@('rules.json');runDirectory=$run;dotnetCliHome=(Join-Path $run 'cli-home');runnerFormat='mtp-ctrf';auditMode='offline-architecture';toolIdentity=$Version;policyFile='policy.json';policySha256=(Hash (Join-Path $source 'policy.json'));requiredCases='required-cases.json';requiredCasesSha256=(Hash (Join-Path $source 'required-cases.json'))})
$result=[ordered]@{schemaVersion=1;kind='archsift-owned-packaged-consumer-fixture';version=$Version;root=$root;source=$source;project=$project;config=$config;packageCache=$cache;offlineSources=$feeds;expectedCases=4;originalLogicalCases=3;jsonCases=1;packageApproval='pending-reviewed-lock-closure';executed=$false;policyAdoption=$false}
Save (Join-Path $root 'fixture-inputs.json') $result
$result|ConvertTo-Json -Depth 10
