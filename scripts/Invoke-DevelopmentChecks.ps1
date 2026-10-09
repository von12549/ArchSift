[CmdletBinding()]
param(
    [string] $LocalFeed = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'),
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Debug',
    [switch] $InitializeLocks,
    [switch] $VerifyContracts,
    [switch] $MeasurePerformance,
    [string] $LabRoot = 'D:\ArchSift-lab'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runId = [Guid]::NewGuid().ToString('N')
$runRoot = Join-Path ([IO.Path]::GetFullPath($LabRoot)) "runs/development-$runId"
if ($runRoot.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence output must be outside source.' }
$localFeedPath = [IO.Path]::GetFullPath($LocalFeed)
if (-not [IO.Directory]::Exists($localFeedPath)) { throw 'The explicit local NuGet feed does not exist.' }

function Hash-Text([string] $Value) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Value))).ToLowerInvariant()
}

function Environment-Hash([EnvironmentVariableTarget] $Scope) {
    $values = [Environment]::GetEnvironmentVariables($Scope)
    $ordered = [ordered]@{}
    foreach ($key in @($values.Keys) | Sort-Object) { $ordered[[string]$key] = [string]$values[$key] }
    Hash-Text (ConvertTo-Json -InputObject $ordered -Compress -Depth 5)
}

function Capture-HostState {
    $profiles = @()
    foreach ($name in @('AllUsersAllHosts', 'AllUsersCurrentHost', 'CurrentUserAllHosts', 'CurrentUserCurrentHost')) {
        $path = [string]$PROFILE.$name
        $exists = [IO.File]::Exists($path)
        $profiles += [ordered]@{
            name = $name
            exists = $exists
            sha256 = $(if ($exists) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null })
        }
    }
    [ordered]@{
        userEnvironmentSha256 = (Environment-Hash User)
        machineEnvironmentSha256 = (Environment-Hash Machine)
        processPathSha256 = (Hash-Text ([Environment]::GetEnvironmentVariable('Path', 'Process')))
        profiles = $profiles
    }
}

function Save-Json([string] $Path, $Value) {
    [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Value -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
}

$baseline = Capture-HostState
$baselineIdentity = Hash-Text (ConvertTo-Json -InputObject $baseline -Depth 8 -Compress)
[void][IO.Directory]::CreateDirectory($runRoot)
$cliHome = Join-Path $runRoot 'cli-home'
$packageCache = Join-Path $repositoryRoot 'artifacts/development/packages'
Save-Json (Join-Path $runRoot 'host-baseline.json') $baseline
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$checks = [Collections.Generic.List[object]]::new()
$resultsPath = Join-Path $runRoot 'checks.json'

function Invoke-CheckedDotnet([string] $Name, [string[]] $Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $dotnet
    $start.WorkingDirectory = $repositoryRoot
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $start.Environment['DOTNET_CLI_HOME'] = $cliHome
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] = '0'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE'] = '1'
    $start.Environment['DOTNET_NOLOGO'] = '1'
    $start.Environment['DOTNET_CLI_UI_LANGUAGE'] = 'en-US'
    $start.Environment['MSBUILDDISABLENODEREUSE'] = '1'
    $start.Environment['NUGET_PACKAGES'] = $packageCache
    $start.Environment['ARCHSIFT_TEST_LOCAL_FEED'] = $localFeedPath
    $start.Environment['NUGET_CERT_REVOCATION_MODE'] = 'offline'
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $peak = 0L
        while (-not $process.WaitForExit(50) -and $timer.Elapsed.TotalSeconds -lt 180) {
            try { $peak = [Math]::Max($peak, $process.PeakWorkingSet64) } catch { }
        }
        $timedOut = -not $process.HasExited
        if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
        [Threading.Tasks.Task]::WaitAll($stdout, $stderr)
        [IO.File]::WriteAllText((Join-Path $runRoot "$Name.stdout.log"), $stdout.Result, [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $runRoot "$Name.stderr.log"), $stderr.Result, [Text.UTF8Encoding]::new($false))
        $current = Capture-HostState
        $equal = (Hash-Text (ConvertTo-Json -InputObject $current -Depth 8 -Compress)) -ceq $baselineIdentity
        $timer.Stop()
        $checks.Add([ordered]@{ name = $Name; arguments = $Arguments; exitCode = $process.ExitCode; timedOut = $timedOut; hostStateEqual = $equal; elapsedMilliseconds = $timer.ElapsedMilliseconds; peakWorkingSetBytes = $peak })
        Save-Json $resultsPath ([ordered]@{ runId = $runId; configuration = $Configuration; networkRestoreEnabled = $false; childDotnetAddGlobalToolsToPath = '0'; checks = $checks.ToArray(); hostState = $current })
        if (-not $Name.StartsWith('performance-', [StringComparison]::Ordinal)) { Write-Output $stdout.Result }
        if ($stderr.Result) { Write-Output $stderr.Result }
        Write-Output ("$Name exit=$($process.ExitCode) host-state-equal=$equal")
        if (-not $equal) { throw 'Host environment/Profile drift detected; evidence retained. No repair performed.' }
        if ($timedOut -or $process.ExitCode -ne 0) { throw "$Name failed; review the retained development logs." }
    }
    finally { $process.Dispose() }
}

try {
    Invoke-CheckedDotnet 'sdk' @('--version')
    $restore = @('restore', 'ArchSift.slnx', '--configfile', 'NuGet.Config', '--source', $localFeedPath, '--packages', $packageCache, '--disable-parallel', '--disable-build-servers', '-p:NuGetAudit=false', '--verbosity', 'minimal')
    if (-not $InitializeLocks) { $restore += '--locked-mode' } else { $restore += '--force-evaluate' }
    Invoke-CheckedDotnet 'restore' $restore
    if ($InitializeLocks) { Invoke-CheckedDotnet 'restore-locked' ($restore + '--locked-mode') }
    Invoke-CheckedDotnet 'build' @('build', 'ArchSift.slnx', '--no-restore', '--disable-build-servers', '-p:UseSharedCompilation=false', '-c', $Configuration, '--verbosity', 'minimal')
    Invoke-CheckedDotnet 'test' @('test', 'ArchSift.slnx', '--no-build', '--no-restore', '--disable-build-servers', '-c', $Configuration, '--logger', 'trx', '--results-directory', (Join-Path $runRoot 'test-results'))
    if ($VerifyContracts) {
        $cli = Join-Path $repositoryRoot "src/ArchSift.Cli/bin/$Configuration/net10.0/archsift.dll"
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'templates/rules') -Filter '*.json' -File) {
            if (-not (Test-Json -LiteralPath $file.FullName -SchemaFile (Join-Path $repositoryRoot 'schemas/ruleset.schema.json'))) {
                throw 'Independent PowerShell schema validation failed.'
            }
            Invoke-CheckedDotnet ("validate-" + $file.BaseName) @($cli, 'rules', 'validate', '--file', $file.FullName)
            Invoke-CheckedDotnet ("render-" + $file.BaseName) @($cli, 'rules', 'render', '--file', $file.FullName, '--output', (Join-Path $runRoot ($file.BaseName + '.md')))
        }
    }
    if ($MeasurePerformance) {
        $fixture = Join-Path $runRoot 'performance-fixture'
        [void][IO.Directory]::CreateDirectory($fixture)
        for ($index = 0; $index -lt 100; $index++) {
            $name = 'P' + $index.ToString('D3')
            $directory = Join-Path $fixture $name
            [void][IO.Directory]::CreateDirectory($directory)
            $reference = if ($index -gt 0) { '<ItemGroup><ProjectReference Include="../P' + ($index - 1).ToString('D3') + '/P' + ($index - 1).ToString('D3') + '.csproj" /></ItemGroup>' } else { '' }
            [IO.File]::WriteAllText((Join-Path $directory ($name + '.csproj')), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>' + $reference + '</Project>')
            [IO.File]::WriteAllText((Join-Path $directory ($name + '.cs')), 'namespace ' + $name + '; public class Marker {}')
        }
        $cli = Join-Path $repositoryRoot "src/ArchSift.Cli/bin/$Configuration/net10.0/archsift.dll"
        $measurements = @()
        for ($index = 0; $index -lt 5; $index++) {
            $name = 'performance-' + $index
            Invoke-CheckedDotnet $name @($cli, 'analyze', '--target', $fixture, '--output', (Join-Path $runRoot 'performance-reports'))
            $report = Get-Content -LiteralPath (Join-Path $runRoot ($name + '.stdout.log')) -Raw | ConvertFrom-Json
            $timing = $checks[$checks.Count - 1]
            $measurements += [ordered]@{ iteration = $index; cache = $(if ($index -eq 0) {'first-process/os-cache-unspecified'} else {'warm-os-cache/new-process'}); projectCount = $report.coverage.projectCount; analysisMilliseconds = $report.runMetadata.elapsedMilliseconds; processWallMilliseconds = $timing.elapsedMilliseconds; sampledPeakWorkingSetBytes = $timing.peakWorkingSetBytes; inputSha256 = $report.inputIdentity.sha256 }
        }
        Save-Json (Join-Path $runRoot 'performance.json') ([ordered]@{ fixture = '100-project-chain'; configuration = $Configuration; tfm = 'net10.0'; cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name); logicalProcessors = [Environment]::ProcessorCount; sdk = '10.0.303'; measurements = $measurements })
    }
}
finally {
    $final = Capture-HostState
    $equal = (Hash-Text (ConvertTo-Json -InputObject $final -Depth 8 -Compress)) -ceq $baselineIdentity
    Save-Json (Join-Path $runRoot 'host-final.json') ([ordered]@{ equal = $equal; hashes = $final })
    Write-Output ("development-run=$runId final-host-state-equal=$equal")
    if (-not $equal) { throw 'Final host state drift detected; evidence retained.' }
}
