[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CandidatePayload,
    [Parameter(Mandatory)][string]$ConfigCopy,
    [Parameter(Mandatory)][string]$JobDirectory,
    [Parameter(Mandatory)][string]$PreflightResult,
    [Parameter(Mandatory)][string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
$baseline = Get-ArchSiftHostHash
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
try {
    $jobRoot = [IO.Path]::GetFullPath($JobDirectory)
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    if (-not $jobRoot.StartsWith('D:\ArchSift-lab\evidence\ifx-0.8\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $output.StartsWith('D:\ArchSift-lab\runs\ifx-0.8-config-', [StringComparison]::OrdinalIgnoreCase) -or
        (Test-Path -LiteralPath $output)) { throw 'External create-new paths required.' }
    $preflight = Get-Content -LiteralPath $PreflightResult -Raw | ConvertFrom-Json
    if ($preflight.errors.Count -ne 0 -or (& git -C $preflight.target.root rev-parse HEAD).Trim() -cne $preflight.target.commit -or
        @(& git -C $preflight.target.root status --porcelain).Count -ne 0) { throw 'Target source changed.' }
    foreach ($file in $preflight.original.frozenFiles) { if ((Hash $file.path) -cne $file.sha256) { throw 'Original installation changed.' } }
    $payload = [IO.Path]::GetFullPath($CandidatePayload)
    $manifest = Get-Content -LiteralPath (Join-Path $payload 'package-manifest.json') -Raw | ConvertFrom-Json
    $cli = Join-Path $payload 'archsift.exe'
    $entry = @($manifest.entries | Where-Object path -CEQ 'archsift.exe')
    if ($manifest.version -cne '0.8.0' -or $manifest.sourceDirty -or $entry.Count -ne 1 -or (Hash $cli) -cne $entry[0].sha256) { throw 'Candidate CLI changed.' }
    $job = Get-Content -LiteralPath (Join-Path $jobRoot 'job.json') -Raw | ConvertFrom-Json
    if ($job.stage -cne 'validated' -or -not (Test-Path -LiteralPath (Join-Path $jobRoot 'assembly-manifest-v3.json'))) { throw 'IFX evidence is not sealed and validated.' }
    [void][IO.Directory]::CreateDirectory($output)
    $start = [Diagnostics.ProcessStartInfo]::new($cli)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $output
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in @('evidence','export-config','--config',([IO.Path]::GetFullPath($ConfigCopy)),'--job',$jobRoot)) { $start.ArgumentList.Add($argument) }
    $start.Environment['DOTNET_CLI_HOME'] = Join-Path $output 'cli-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] = '0'
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(900000)) { $process.Kill($true); throw 'Configuration export timed out.' }
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $output 'stdout.json'),$stdout.Result,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $output 'stderr.log'),$stderr.Result,[Text.UTF8Encoding]::new($false))
        if ($process.ExitCode -ne 0) { throw "Export exited $($process.ExitCode); inspect preserved output." }
        $result = $stdout.Result | ConvertFrom-Json
        $copy = [IO.Path]::GetFullPath($result.configurationCopyPath)
        if ($result.stage -cne 'configured' -or -not $result.validation.sourceBound -or
            -not $copy.StartsWith($jobRoot+'\', [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $copy)) { throw 'Configuration copy/validation differs.' }
        $config = Get-Content -LiteralPath $copy -Raw | ConvertFrom-Json
        if ($config.build.mode -cne 'existing' -or $config.build.assemblyManifest -cne (Join-Path $jobRoot 'assembly-manifest-v3.json') -or
            -not $config.rulesDirectory.StartsWith($jobRoot+'\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $config.output.directory.StartsWith($jobRoot+'\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Exported configuration escaped the run or lost manifest binding.' }
        $record = [ordered]@{kind='ifx-0.8-config-copy';candidateCommit=$manifest.sourceCommit;configCopyPath=$copy;configCopySha256=(Hash $copy);sourceBound=$result.validation.sourceBound;contextCount=$result.validation.contextCount;hostStateEqual=((Get-ArchSiftHostHash) -ceq $baseline)}
        [IO.File]::WriteAllText((Join-Path $output 'result.json'),($record | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
        if (-not $record.hostStateEqual) { throw 'Host drift; preserve evidence.' }
        $record | ConvertTo-Json -Depth 6
    } finally { $process.Dispose() }
} finally {
    $equal = (Get-ArchSiftHostHash) -ceq $baseline
    Write-Output "ifx-0.8-export-final-host-state-equal=$equal"
    if (-not $equal) { throw 'Host drift; preserve evidence without repair.' }
}
