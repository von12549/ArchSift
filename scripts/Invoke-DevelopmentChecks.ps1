[CmdletBinding()]
param(
    [string] $LocalFeed = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'),
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Debug',
    [switch] $InitializeLocks
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runId = [Guid]::NewGuid().ToString('N')
$runRoot = Join-Path $repositoryRoot "artifacts/development/$runId"
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
    $start.Environment['NUGET_CERT_REVOCATION_MODE'] = 'offline'
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit(180000)
        if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
        [Threading.Tasks.Task]::WaitAll($stdout, $stderr)
        [IO.File]::WriteAllText((Join-Path $runRoot "$Name.stdout.log"), $stdout.Result, [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $runRoot "$Name.stderr.log"), $stderr.Result, [Text.UTF8Encoding]::new($false))
        $current = Capture-HostState
        $equal = (Hash-Text (ConvertTo-Json -InputObject $current -Depth 8 -Compress)) -ceq $baselineIdentity
        $checks.Add([ordered]@{ name = $Name; arguments = $Arguments; exitCode = $process.ExitCode; timedOut = $timedOut; hostStateEqual = $equal })
        Save-Json $resultsPath ([ordered]@{ runId = $runId; configuration = $Configuration; networkRestoreEnabled = $false; childDotnetAddGlobalToolsToPath = '0'; checks = $checks.ToArray(); hostState = $current })
        Write-Output $stdout.Result
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
    if (-not $InitializeLocks) { $restore += '--locked-mode' }
    Invoke-CheckedDotnet 'restore' $restore
    if ($InitializeLocks) { Invoke-CheckedDotnet 'restore-locked' ($restore + '--locked-mode') }
    Invoke-CheckedDotnet 'build' @('build', 'ArchSift.slnx', '--no-restore', '--disable-build-servers', '-p:UseSharedCompilation=false', '-c', $Configuration, '--verbosity', 'minimal')
    Invoke-CheckedDotnet 'test' @('test', 'ArchSift.slnx', '--no-build', '--no-restore', '--disable-build-servers', '-c', $Configuration, '--logger', 'trx', '--results-directory', (Join-Path $runRoot 'test-results'))
}
finally {
    $final = Capture-HostState
    $equal = (Hash-Text (ConvertTo-Json -InputObject $final -Depth 8 -Compress)) -ceq $baselineIdentity
    Save-Json (Join-Path $runRoot 'host-final.json') ([ordered]@{ equal = $equal; hashes = $final })
    Write-Output ("development-run=$runId final-host-state-equal=$equal")
    if (-not $equal) { throw 'Final host state drift detected; evidence retained.' }
}
