[CmdletBinding()]
param(
    [string] $TargetRoot = 'D:\IFX-10-Root\IFX-New',
    [string] $Entry = 'IFX.sln',
    [string] $LabRoot = 'D:\ArchSift-lab',
    [string] $ExpectedTargetHead = '64ef2674c57e6cf9031481d6d4410cca55b0dad5',
    [string] $Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'HostState.ps1')

$productRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$target = [IO.Path]::GetFullPath($TargetRoot)
$lab = [IO.Path]::GetFullPath($LabRoot)
$runRoot = Join-Path $lab ('runs/w11-ifx-allowlists-' + [Guid]::NewGuid().ToString('N'))
$entryPath = [IO.Path]::GetFullPath((Join-Path $target $Entry))
$cliDirectory = Join-Path $productRoot "src/ArchSift.Cli/bin/$Configuration/net10.0"
$cli = Join-Path $cliDirectory 'archsift.dll'
$rulesPath = Join-Path $runRoot 'w11-ifx-allowlists.json'
$configPath = Join-Path $runRoot 'archsift.json'
$reports = Join-Path $runRoot 'reports'

if (-not [IO.Directory]::Exists($target)) { throw 'IFX target root does not exist.' }
if (-not [IO.File]::Exists($entryPath)) { throw 'IFX entry does not exist.' }
if (-not [IO.File]::Exists($cli)) { throw 'Tested W11 CLI build is missing; stop without building from the IFX task.' }
if ($runRoot.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $runRoot.StartsWith($productRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Evidence output overlaps a source root.'
}

function Git-Text([string] $Root, [string[]] $Arguments) {
    $output = @(& git -C $Root @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "Git metadata read failed for $Root." }
    ($output -join "`n").TrimEnd()
}

function File-Identity([string] $Directory) {
    @(Get-ChildItem -LiteralPath $Directory -File | Sort-Object Name | ForEach-Object {
        [ordered]@{
            name = $_.Name
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
}

function Same-Json($Left, $Right) {
    (ConvertTo-Json -InputObject $Left -Depth 10 -Compress) -ceq (ConvertTo-Json -InputObject $Right -Depth 10 -Compress)
}

$hostBefore = Get-ArchSiftHostHash
$targetHeadBefore = Git-Text $target @('rev-parse', 'HEAD')
$targetStatusBefore = Git-Text $target @('status', '--porcelain=v1', '--untracked-files=all')
$productHeadBefore = Git-Text $productRoot @('rev-parse', 'HEAD')
$productStatusBefore = Git-Text $productRoot @('status', '--porcelain=v1', '--untracked-files=all')
$cliIdentityBefore = File-Identity $cliDirectory

if ($targetHeadBefore -cne $ExpectedTargetHead) { throw 'IFX HEAD differs from the reviewed W11 target identity.' }
if ($targetStatusBefore.Length -ne 0) { throw 'IFX worktree is not clean; preserve user changes and stop.' }

[void][IO.Directory]::CreateDirectory($runRoot)
$rules = [ordered]@{
    schemaVersion = 1
    id = 'w11-ifx-allowlists'
    version = '1.0.0'
    description = 'W11 read-only IFX verification for closed project and direct NuGet policies.'
    rules = @(
        [ordered]@{
            id = 'w11-ifx-project-allowlist'
            type = 'project-reference-allowlist'
            enabled = $true
            scope = [ordered]@{ kind = 'project'; match = 'exact'; value = 'src/BuildingBlocks/IFX.Application.Primitives/IFX.Application.Primitives.csproj' }
            parameters = [ordered]@{
                source = [ordered]@{ kind = 'project'; match = 'exact'; value = 'src/BuildingBlocks/IFX.Application.Primitives/IFX.Application.Primitives.csproj' }
                allowedTargets = @()
            }
            severity = 'warning'
            reason = 'W11 fixture: this selected source is expected to expose its direct Domain.Primitives edge when no targets are approved.'
        },
        [ordered]@{
            id = 'w11-ifx-nuget-allowlist'
            type = 'nuget-allowlist'
            enabled = $true
            scope = [ordered]@{ kind = 'project'; match = 'exact'; value = 'src/BuildingBlocks/IFX.Application.Primitives/IFX.Application.Primitives.csproj' }
            parameters = [ordered]@{
                allowedPackageIds = @(
                    'Microsoft.Extensions.DependencyInjection.Abstractions',
                    'Microsoft.Extensions.Logging.Abstractions',
                    'FluentValidation'
                )
            }
            severity = 'error'
            reason = 'W11 fixture: one known direct package is intentionally omitted from the approved set.'
        }
    )
    exceptions = @()
}
[IO.File]::WriteAllText($rulesPath, (ConvertTo-Json -InputObject $rules -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
$config = [ordered]@{
    schemaVersion = 1
    target = [ordered]@{ root = $target; entry = $Entry }
    rulesets = @($rulesPath)
    build = [ordered]@{
        mode = 'existing'
        targetFramework = 'net10.0'
        configuration = 'Debug'
        assemblyPaths = @()
        allowNetwork = $false
        sources = @()
    }
    output = [ordered]@{ directory = $reports; formats = @('json', 'html', 'sarif') }
}
[IO.File]::WriteAllText($configPath, (ConvertTo-Json -InputObject $config -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $dotnet
$start.WorkingDirectory = $productRoot
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($argument in @($cli, 'verify', '--config', $configPath)) { $start.ArgumentList.Add($argument) }
$start.Environment['DOTNET_CLI_HOME'] = Join-Path $runRoot 'cli-home'
$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] = '0'
$start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
$start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE'] = '1'
$start.Environment['DOTNET_NOLOGO'] = '1'
$start.Environment['MSBUILDDISABLENODEREUSE'] = '1'

$process = [Diagnostics.Process]::Start($start)
$stdout = $process.StandardOutput.ReadToEndAsync()
$stderr = $process.StandardError.ReadToEndAsync()
try {
    if (-not $process.WaitForExit(180000)) { $process.Kill($true); throw 'W11 IFX verification timed out.' }
    [Threading.Tasks.Task]::WaitAll($stdout, $stderr)
    [IO.File]::WriteAllText((Join-Path $runRoot 'stdout.json'), $stdout.Result, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $runRoot 'stderr.log'), $stderr.Result, [Text.UTF8Encoding]::new($false))
    $report = $stdout.Result | ConvertFrom-Json
    $projectFinding = @($report.findings | Where-Object ruleId -CEQ 'w11-ifx-project-allowlist')
    $packageFinding = @($report.findings | Where-Object ruleId -CEQ 'w11-ifx-nuget-allowlist')
    $ruleStatuses = [ordered]@{}
    foreach ($rule in $report.ruleResults) { $ruleStatuses[[string]$rule.ruleId] = [string]$rule.status }
    $reportDirectory = Join-Path $reports $report.runMetadata.runId
    $jsonReport = Join-Path $reportDirectory 'report.json'
    $htmlReport = Join-Path $reportDirectory 'report.html'
    $sarifReport = Join-Path $reportDirectory 'report.sarif'
    $reportFilesPresent = [IO.File]::Exists($jsonReport) -and [IO.File]::Exists($htmlReport) -and [IO.File]::Exists($sarifReport)
    $htmlText = if ([IO.File]::Exists($htmlReport)) { [IO.File]::ReadAllText($htmlReport) } else { '' }
    $sarif = if ([IO.File]::Exists($sarifReport)) { [IO.File]::ReadAllText($sarifReport) | ConvertFrom-Json } else { $null }
    $sarifRuleIds = if ($null -ne $sarif) { @($sarif.runs[0].results | ForEach-Object ruleId) } else { @() }

    $hostAfter = Get-ArchSiftHostHash
    $targetHeadAfter = Git-Text $target @('rev-parse', 'HEAD')
    $targetStatusAfter = Git-Text $target @('status', '--porcelain=v1', '--untracked-files=all')
    $productHeadAfter = Git-Text $productRoot @('rev-parse', 'HEAD')
    $productStatusAfter = Git-Text $productRoot @('status', '--porcelain=v1', '--untracked-files=all')
    $cliIdentityAfter = File-Identity $cliDirectory
    $checks = [ordered]@{
        exitCodeFour = $process.ExitCode -eq 4
        executionPartial = $report.execution -ceq 'partial'
        complianceNoncompliant = $report.compliance -ceq 'noncompliant'
        projectCount105 = $report.coverage.projectCount -eq 105
        assemblyCountZero = $report.coverage.assemblyCount -eq 0
        noExecutionErrors = @($report.executionErrors).Count -eq 0
        projectRuleViolation = $ruleStatuses['w11-ifx-project-allowlist'] -ceq 'violation'
        nugetRuleViolation = $ruleStatuses['w11-ifx-nuget-allowlist'] -ceq 'violation'
        oneProjectFinding = $projectFinding.Count -eq 1
        projectFindingExpected = $projectFinding.Count -eq 1 -and $projectFinding[0].source -ceq 'src/BuildingBlocks/IFX.Application.Primitives/IFX.Application.Primitives.csproj' -and $projectFinding[0].target -ceq 'src/BuildingBlocks/IFX.Domain.Primitives/IFX.Domain.Primitives.csproj'
        onePackageFinding = $packageFinding.Count -eq 1
        packageFindingExpected = $packageFinding.Count -eq 1 -and $packageFinding[0].target -ceq 'fluentvalidation.dependencyinjectionextensions'
        threeReportsPresent = $reportFilesPresent
        savedJsonEqualsStdout = $reportFilesPresent -and ([IO.File]::ReadAllText($jsonReport).Trim() -ceq $stdout.Result.Trim())
        htmlContainsBothRules = $htmlText.Contains('w11-ifx-project-allowlist', [StringComparison]::Ordinal) -and $htmlText.Contains('w11-ifx-nuget-allowlist', [StringComparison]::Ordinal)
        sarifContainsBothRules = ($sarifRuleIds -ccontains 'w11-ifx-project-allowlist') -and ($sarifRuleIds -ccontains 'w11-ifx-nuget-allowlist')
        targetHeadEqual = $targetHeadBefore -ceq $targetHeadAfter
        targetStatusEqual = $targetStatusBefore -ceq $targetStatusAfter
        productHeadEqual = $productHeadBefore -ceq $productHeadAfter
        productStatusEqual = $productStatusBefore -ceq $productStatusAfter
        cliFilesEqual = Same-Json $cliIdentityBefore $cliIdentityAfter
        hostStateEqual = $hostBefore -ceq $hostAfter
    }
    $passed = @($checks.Values | Where-Object { -not $_ }).Count -eq 0
    $result = [ordered]@{
        schemaVersion = 1
        step = 'w11-ifx-allowlists'
        status = $(if ($passed) { 'pass' } else { 'error' })
        failure = $(if ($passed) { $null } else { 'One or more W11 IFX assertions failed.' })
        checkedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        runRoot = $runRoot
        targetRoot = $target
        targetHead = $targetHeadBefore
        productHead = $productHeadBefore
        productSourceDirty = $productStatusBefore.Length -ne 0
        cliSha256 = (Get-FileHash -LiteralPath $cli -Algorithm SHA256).Hash.ToLowerInvariant()
        rulesSha256 = (Get-FileHash -LiteralPath $rulesPath -Algorithm SHA256).Hash.ToLowerInvariant()
        configSha256 = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash.ToLowerInvariant()
        exitCode = $process.ExitCode
        execution = $report.execution
        compliance = $report.compliance
        projectCount = $report.coverage.projectCount
        assemblyCount = $report.coverage.assemblyCount
        projectFinding = $(if ($projectFinding.Count -eq 1) { $projectFinding[0] } else { $projectFinding })
        packageFinding = $(if ($packageFinding.Count -eq 1) { $packageFinding[0] } else { $packageFinding })
        reports = $(if ($reportFilesPresent) { @(
            foreach ($path in @($jsonReport, $htmlReport, $sarifReport)) {
                [ordered]@{ path = $path; bytes = (Get-Item -LiteralPath $path).Length; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
            }
        ) } else { @() })
        checks = $checks
        targetBuildInvoked = $false
        restoreInvoked = $false
        ifxApplicationExecuted = $false
        evidencePreserved = $true
        nextAction = 'RETURN THIS RESULT BEFORE ANY NEXT COMMAND'
    }
    $resultPath = Join-Path $runRoot 'operator-result.json'
    [IO.File]::WriteAllText($resultPath, (ConvertTo-Json -InputObject $result -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
    $result | ConvertTo-Json -Depth 12
    if (-not $checks.hostStateEqual) { throw 'Host state drift detected; stop without repair.' }
    if (-not $passed) { throw 'W11 IFX verification assertions failed; preserve evidence and stop.' }
}
finally {
    $process.Dispose()
}
