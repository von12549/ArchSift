[CmdletBinding()]
param(
    [string] $CliEvidenceRoot = 'D:\ArchSift-lab\runs\w11-ifx-allowlists-3ba4055f16ab446ba47652515c47d8d3',
    [string] $TargetRoot = 'D:\IFX-10-Root\IFX-New',
    [string] $ExpectedTargetHead = '64ef2674c57e6cf9031481d6d4410cca55b0dad5',
    [string] $LabRoot = 'D:\ArchSift-lab',
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'HostState.ps1')

$productRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$target = [IO.Path]::GetFullPath($TargetRoot)
$evidence = [IO.Path]::GetFullPath($CliEvidenceRoot)
$runRoot = Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('evidence/w11-ifx-ui-' + [Guid]::NewGuid().ToString('N'))
$webDirectory = Join-Path $productRoot "src/ArchSift.Web/bin/$Configuration/net10.0"
$web = Join-Path $webDirectory 'ArchSift.Web.dll'
$uiAssets = @(
    (Join-Path $webDirectory 'wwwroot/index.html'),
    (Join-Path $webDirectory 'wwwroot/app.js'),
    (Join-Path $webDirectory 'wwwroot/style.css')
)
$rulesPath = Join-Path $runRoot 'rules/w11-ifx-allowlists.json'
$configPath = Join-Path $runRoot 'archsift.json'
$reports = Join-Path $runRoot 'reports'
$rulesDirectory = Join-Path $runRoot 'rules-editor'
$stdoutPath = Join-Path $runRoot 'web.stdout.log'
$stderrPath = Join-Path $runRoot 'web.stderr.log'
$sessionPath = Join-Path $runRoot 'session.json'

if (-not [IO.Directory]::Exists($target)) { throw 'IFX target root does not exist.' }
if (-not [IO.Directory]::Exists($evidence)) { throw 'Accepted W11 CLI evidence root does not exist.' }
if (-not [IO.File]::Exists($web)) { throw 'Tested W11 Web build is missing.' }
foreach ($asset in $uiAssets) {
    if (-not [IO.File]::Exists($asset)) { throw "Tested W11 UI asset is missing: $asset" }
}
if ($runRoot.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $runRoot.StartsWith($productRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'UI evidence output overlaps a source root.'
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

$hostBefore = Get-ArchSiftHostHash
$targetHead = Git-Text $target @('rev-parse', 'HEAD')
$targetStatus = Git-Text $target @('status', '--porcelain=v1', '--untracked-files=all')
$productHead = Git-Text $productRoot @('rev-parse', 'HEAD')
$productStatus = Git-Text $productRoot @('status', '--porcelain=v1', '--untracked-files=all')
$webIdentity = File-Identity $webDirectory
$uiAssetIdentity = @($uiAssets | ForEach-Object {
    [ordered]@{
        path = [IO.Path]::GetRelativePath($webDirectory, $_).Replace('\', '/')
        bytes = ([IO.FileInfo]$_).Length
        sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$indexText = [IO.File]::ReadAllText($uiAssets[0])
$scriptText = [IO.File]::ReadAllText($uiAssets[1])
$styleText = [IO.File]::ReadAllText($uiAssets[2])
if (-not $indexText.Contains('这些是被分析对象，不是规则结论、违规证据或覆盖限制。') -or
    -not $scriptText.Contains('执行完整性与规则合规是两个独立维度。') -or
    -not $scriptText.Contains('逐规则结论') -or
    -not $scriptText.Contains('违规与例外') -or
    -not $scriptText.Contains('这些信息影响结果完整性，不是项目名称，也不是额外 finding。') -or
    -not $styleText.Contains('.finding-region') -or
    -not $styleText.Contains('.coverage-region') -or
    -not $styleText.Contains('.project-region') -or
    -not $styleText.Contains('.finding-table td:nth-child(5)::before')) {
    throw 'Tested W11 Web build does not contain the reviewed result-hierarchy fix.'
}
if ($targetHead -cne $ExpectedTargetHead) { throw 'IFX HEAD differs from the reviewed W11 target identity.' }
if ($targetStatus.Length -ne 0) { throw 'IFX worktree is not clean; preserve user changes and stop.' }

$cliResultPath = Join-Path $evidence 'operator-result.json'
$sourceRulesPath = Join-Path $evidence 'w11-ifx-allowlists.json'
$sourceConfigPath = Join-Path $evidence 'archsift.json'
if (-not [IO.File]::Exists($cliResultPath) -or -not [IO.File]::Exists($sourceRulesPath) -or -not [IO.File]::Exists($sourceConfigPath)) {
    throw 'Accepted W11 CLI evidence is incomplete.'
}
$cliResult = Get-Content -LiteralPath $cliResultPath -Raw | ConvertFrom-Json
if ($cliResult.status -cne 'pass' -or $cliResult.targetHead -cne $ExpectedTargetHead) { throw 'W11 CLI evidence did not pass for the expected IFX identity.' }
$sourceRulesHash = (Get-FileHash -LiteralPath $sourceRulesPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sourceRulesHash -cne [string]$cliResult.rulesSha256) { throw 'W11 CLI rules changed after acceptance.' }

[void][IO.Directory]::CreateDirectory((Split-Path $rulesPath -Parent))
[void][IO.Directory]::CreateDirectory($rulesDirectory)
[IO.File]::WriteAllBytes($rulesPath, [IO.File]::ReadAllBytes($sourceRulesPath))
$config = Get-Content -LiteralPath $sourceConfigPath -Raw | ConvertFrom-Json
$config.rulesets = @($rulesPath)
$config.output.directory = $reports
$config.output.formats = @('json', 'html', 'sarif')
$config | Add-Member -NotePropertyName rulesDirectory -NotePropertyValue $rulesDirectory -Force
[IO.File]::WriteAllText($configPath, (ConvertTo-Json -InputObject $config -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$arguments = @($web, '--config', $configPath)
$childEnvironment = @{
    DOTNET_CLI_HOME = (Join-Path $runRoot 'cli-home')
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
    DOTNET_NOLOGO = '1'
    MSBUILDDISABLENODEREUSE = '1'
}
$process = Start-Process -FilePath $dotnet -ArgumentList $arguments -PassThru -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -Environment $childEnvironment
try {
    $url = $null
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
    while ([DateTimeOffset]::UtcNow -lt $deadline -and -not $process.HasExited) {
        if ([IO.File]::Exists($stdoutPath)) {
            $line = @(Get-Content -LiteralPath $stdoutPath | Where-Object { $_.StartsWith('ARCHSIFT_UI=', [StringComparison]::Ordinal) } | Select-Object -Last 1)
            if ($line.Count -eq 1) { $url = $line[0].Substring(12); break }
        }
        Start-Sleep -Milliseconds 100
    }
    if ($null -eq $url) { throw 'W11 UI did not publish its authenticated loopback URL.' }
    $uri = [Uri]$url
    if ($uri.Host -cne '127.0.0.1' -or $uri.Fragment -notmatch '^#session=[0-9a-f]{64}$') { throw 'W11 UI URL is not an authenticated loopback address.' }
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $uri.Port -ErrorAction Stop | Where-Object OwningProcess -eq $process.Id)
    if ($listeners.Count -ne 1) { throw 'W11 UI listener is not uniquely owned by the recorded process.' }
    $hostAfterLaunch = Get-ArchSiftHostHash
    $session = [ordered]@{
        schemaVersion = 1
        step = 'w11-ifx-ui-manual-session'
        status = 'awaiting-human-observation'
        startedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        runRoot = $runRoot
        sessionPath = $sessionPath
        processId = $process.Id
        url = $url
        port = $uri.Port
        targetKind = 'real'
        targetRoot = $target
        targetEntry = 'IFX.sln'
        targetHead = $targetHead
        rulesPath = $rulesPath
        rulesSha256 = (Get-FileHash -LiteralPath $rulesPath -Algorithm SHA256).Hash.ToLowerInvariant()
        configPath = $configPath
        configSha256 = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash.ToLowerInvariant()
        reportsRoot = $reports
        rulesDirectory = $rulesDirectory
        productRoot = $productRoot
        productHead = $productHead
        productStatusBefore = $productStatus
        webSha256 = (Get-FileHash -LiteralPath $web -Algorithm SHA256).Hash.ToLowerInvariant()
        webIdentity = $webIdentity
        uiAssetIdentity = $uiAssetIdentity
        resultHierarchyFixVerified = $true
        hostHashBefore = $hostBefore
        hostHashAfterLaunch = $hostAfterLaunch
        hostStateEqual = $hostBefore -ceq $hostAfterLaunch
        expected = [ordered]@{
            state = 'partial'
            exitCode = 4
            execution = 'partial'
            compliance = 'noncompliant'
            projectCount = 105
            projectRule = 'w11-ifx-project-allowlist'
            packageRule = 'w11-ifx-nuget-allowlist'
            projectFilterVisibleCount = 1
            reportFormats = @('json', 'html', 'sarif')
            resultRegions = @('运行结论', '规则结论', '规则证据', '覆盖边界', '目标清单')
            limitationsInitiallyCollapsed = $true
            projectListVisuallyDistinct = $true
            narrowLayoutHasNoHorizontalOverflow = $true
        }
        targetBuildInvoked = $false
        restoreInvoked = $false
        ifxApplicationExecuted = $false
        launcherAttachedUntilServiceExit = $true
        cleanupPending = $true
        nextAction = 'KEEP THIS POWERSHELL OPEN; COMPLETE THE REVIEW AT THE PRINTED URL; RETURN ATTESTATION AFTER SAFE SHUTDOWN'
    }
    [IO.File]::WriteAllText($sessionPath, (ConvertTo-Json -InputObject $session -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
    $session | ConvertTo-Json -Depth 12
    if (-not $session.hostStateEqual) { throw 'Host state drift detected after UI launch; stop without repair.' }
    $process.WaitForExit()
}
catch {
    if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    throw
}
finally {
    $process.Dispose()
}
