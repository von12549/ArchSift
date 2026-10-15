[CmdletBinding()]
param([string] $OutputRoot = [IO.Path]::GetTempPath())

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'HostState.ps1')
$hostBefore = Get-ArchSiftHostHash
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$scratch = [IO.Path]::GetFullPath((Join-Path $OutputRoot ('archsift-lt02-' + [Guid]::NewGuid().ToString('N'))))
if ($scratch.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Synthetic evidence root overlaps source.' }
[IO.Directory]::CreateDirectory($scratch) | Out-Null
$originalGate = [Environment]::GetEnvironmentVariable('ARCHSIFT_TEST_GATE_CONFIG')
$originalNegative = [Environment]::GetEnvironmentVariable('ARCHSIFT_LT01_NEGATIVE')
$originalSnapshot = [Environment]::GetEnvironmentVariable('IFX_UPDATE_SNAPSHOTS')

try {
    [Environment]::SetEnvironmentVariable('ARCHSIFT_TEST_GATE_CONFIG', $null, 'Process')
    [Environment]::SetEnvironmentVariable('ARCHSIFT_LT01_NEGATIVE', $null, 'Process')
    $testExecutable = Join-Path $root 'tests/ArchSift.Testing.Tests/bin/Release/net10.0/ArchSift.Testing.Tests.exe'
    if (-not [IO.File]::Exists($testExecutable)) { throw 'Build the LT02 synthetic test project first.' }
    $nativeLines = @(& $testExecutable -reporter json -noLogo)
    if ($LASTEXITCODE -ne 0) {
        $nativeLines | Where-Object { $_ -like '*"$type":"test-failed"*' } | Select-Object -First 5 | Write-Output
        throw 'Synthetic original-case baseline run failed.'
    }
    $events = @($nativeLines | ForEach-Object { $_ | ConvertFrom-Json })
    $jsonIds = @($events | Where-Object { $_.'$type' -eq 'test-output' -and $_.Output -like 'ARCHSIFT_JSON_CASE:*' } | ForEach-Object { $_.TestUniqueID })
    $originalIds = @($events | Where-Object { $_.'$type' -eq 'test-starting' -and $_.TestUniqueID -notin $jsonIds } | ForEach-Object { $_.TestUniqueID })
    if ($originalIds.Count -lt 1 -or $jsonIds.Count -ne 1) { throw 'Unexpected synthetic native case identity count.' }
    $baseline = Join-Path $scratch 'original-cases.json'
    [IO.File]::WriteAllText($baseline, (ConvertTo-Json -InputObject $originalIds -Compress), [Text.UTF8Encoding]::new($false))
    $baselineSha = (Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash.ToLowerInvariant()
    $config = [ordered]@{
        schemaVersion = 1
        targetRoot = $root
        testProject = 'tests/ArchSift.Testing.Tests/ArchSift.Testing.Tests.csproj'
        testExecutableName = 'ArchSift.Testing.Tests'
        targetFramework = 'net10.0'
        configuration = 'Release'
        nuGetConfig = 'NuGet.config'
        localFeed = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages')
        packageCache = (Join-Path $root 'artifacts/development/packages')
        rulesets = @('tests/ArchSift.Testing.Tests/rules.json')
        originalBaseline = $baseline
        originalBaselineSha256 = $baselineSha
        runDirectory = ''
    }
    $cli = Join-Path $root 'src/ArchSift.Cli/bin/Release/net10.0/archsift.dll'
    [Environment]::SetEnvironmentVariable('IFX_UPDATE_SNAPSHOTS', '1', 'Process')

    function Invoke-Gate([string] $Name, [hashtable] $Changes, [int] $ExpectedExit) {
        $copy = [ordered]@{}
        foreach ($key in $config.Keys) { $copy[$key] = $config[$key] }
        foreach ($key in $Changes.Keys) { $copy[$key] = $Changes[$key] }
        $copy.runDirectory = Join-Path $scratch ('run-' + $Name)
        $configPath = Join-Path $scratch ('config-' + $Name + '.json')
        [IO.File]::WriteAllText($configPath, (ConvertTo-Json -InputObject $copy -Depth 5), [Text.UTF8Encoding]::new($false))
        $resultText = & dotnet $cli test-gate --config $configPath
        $exit = $LASTEXITCODE
        if (-not $resultText) { throw "No test gate result for $Name." }
        $result = $resultText | ConvertFrom-Json
        if ($exit -ne $ExpectedExit -or $result.exitCode -ne $ExpectedExit) { throw "Unexpected $Name gate exit: $exit / $($result.exitCode)." }
        if ([Environment]::GetEnvironmentVariable('IFX_UPDATE_SNAPSHOTS') -ne '1') { throw 'Parent snapshot setting changed.' }
        $tests = if ($null -eq $result.nativeRun) { @() } else { @($result.nativeRun.tests) }
        Write-Output ([ordered]@{ name = $Name; exitCode = $exit; gate = $result.gate; reasons = @($result.reasons); originalStarted = @($tests | Where-Object { -not $_.jsonCaseId }).Count; jsonStarted = @($tests | Where-Object { $_.jsonCaseId }).Count; runDirectory = $copy.runDirectory })
    }

    $full = Invoke-Gate 'full' @{} 0
    $negative = Invoke-Gate 'violation' @{ rulesets = @('tests/ArchSift.Testing.Tests/rules-violation.json') } 2
    $missingIds = @($originalIds) + @('synthetic-missing-original-row')
    $missingBaseline = Join-Path $scratch 'missing-cases.json'
    [IO.File]::WriteAllText($missingBaseline, (ConvertTo-Json -InputObject $missingIds -Compress), [Text.UTF8Encoding]::new($false))
    $missingSha = (Get-FileHash -LiteralPath $missingBaseline -Algorithm SHA256).Hash.ToLowerInvariant()
    $missing = Invoke-Gate 'missing' @{ originalBaseline = $missingBaseline; originalBaselineSha256 = $missingSha } 4
    $firstOriginalCase = @($events | Where-Object { $_.'$type' -eq 'test-starting' -and $_.TestUniqueID -in $originalIds })[0].TestCaseUniqueID
    $subset = Invoke-Gate 'subset' @{ filterCaseId = $firstOriginalCase } 4
    $emptyFeed = Join-Path $scratch 'empty-feed'
    $emptyCache = Join-Path $scratch 'empty-cache'
    [IO.Directory]::CreateDirectory($emptyFeed) | Out-Null
    [IO.Directory]::CreateDirectory($emptyCache) | Out-Null
    $restoreFailure = Invoke-Gate 'restore-failure' @{ localFeed = $emptyFeed; packageCache = $emptyCache } 3
    $recovery = Invoke-Gate 'recovery' @{} 0
    $invalidConfig = Join-Path $scratch 'config-invalid.json'
    $invalid = [ordered]@{}
    foreach ($key in $config.Keys) { $invalid[$key] = $config[$key] }
    $invalid.rulesets = @()
    $invalid.runDirectory = Join-Path $scratch 'run-invalid'
    [IO.File]::WriteAllText($invalidConfig, (ConvertTo-Json -InputObject $invalid -Depth 5), [Text.UTF8Encoding]::new($false))
    $invalidOutput = & dotnet $cli test-gate --config $invalidConfig 2>&1
    $invalidExit = $LASTEXITCODE
    if ($invalidExit -ne 5 -or [IO.Directory]::Exists($invalid.runDirectory)) { throw "Invalid config was not rejected before execution: $invalidExit." }
    $invalidResult = [ordered]@{ name = 'invalid-config'; exitCode = $invalidExit; gate = 'block'; reasons = @('No enabled ruleset paths.'); originalStarted = 0; jsonStarted = 0; runDirectory = $invalid.runDirectory }
    $hostAfter = Get-ArchSiftHostHash
    if ($hostBefore -ne $hostAfter) { throw 'Host environment/Profile hash drifted.' }
    [ordered]@{ evidenceRoot = $scratch; originalBaselineSha256 = $baselineSha; originalCaseCount = $originalIds.Count; jsonCaseCount = $jsonIds.Count; hostBefore = $hostBefore; hostAfter = $hostAfter; results = @($full, $negative, $missing, $subset, $restoreFailure, $recovery, $invalidResult) } | ConvertTo-Json -Depth 8
}
finally {
    [Environment]::SetEnvironmentVariable('ARCHSIFT_TEST_GATE_CONFIG', $originalGate, 'Process')
    [Environment]::SetEnvironmentVariable('ARCHSIFT_LT01_NEGATIVE', $originalNegative, 'Process')
    [Environment]::SetEnvironmentVariable('IFX_UPDATE_SNAPSHOTS', $originalSnapshot, 'Process')
}
