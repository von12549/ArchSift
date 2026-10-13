[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CandidatePayload,
    [Parameter(Mandatory)][string]$ConfigCopy,
    [Parameter(Mandatory)][string]$JobDirectory,
    [Parameter(Mandatory)][string]$PreflightResult,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$RetestRecord
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
        -not $output.StartsWith('D:\ArchSift-lab\runs\ifx-0.8-bound-', [StringComparison]::OrdinalIgnoreCase) -or
        (Test-Path -LiteralPath $output)) { throw 'Recorded external evidence and new external report audit directory required.' }
    $preflight = Get-Content -LiteralPath $PreflightResult -Raw | ConvertFrom-Json
    function CheckOriginal {
        if ($preflight.errors.Count -ne 0 -or (& git -C $preflight.target.root rev-parse HEAD).Trim() -cne $preflight.target.commit -or
            @(& git -C $preflight.target.root status --porcelain).Count -ne 0) { throw 'IFX target source changed.' }
        foreach ($file in $preflight.original.frozenFiles) { if ((Hash $file.path) -cne $file.sha256) { throw 'Original installation changed.' } }
    }
    CheckOriginal
    $payload = [IO.Path]::GetFullPath($CandidatePayload)
    $manifest = Get-Content -LiteralPath (Join-Path $payload 'package-manifest.json') -Raw | ConvertFrom-Json
    $cli = Join-Path $payload 'archsift.exe'
    $entry = @($manifest.entries | Where-Object path -CEQ 'archsift.exe')
    if ($manifest.version -cne '0.8.0' -or $manifest.sourceDirty -or $entry.Count -ne 1 -or (Hash $cli) -cne $entry[0].sha256) { throw 'Candidate CLI changed.' }
    $configPath = [IO.Path]::GetFullPath($ConfigCopy)
    $retest = $null
    if ($RetestRecord) {
        $recordPath = [IO.Path]::GetFullPath($RetestRecord)
        if (-not $recordPath.StartsWith('D:\ArchSift-lab\evidence\ifx-0.8-retest-', [StringComparison]::OrdinalIgnoreCase)) { throw 'Retest record must be external.' }
        $retest = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
        if ($retest.kind -cne 'ifx-0.8-retest-config-copy' -or -not $retest.hostStateEqual -or
            $retest.jobDirectory -cne $jobRoot -or $retest.newConfig -cne $configPath -or
            $retest.originalConfig -cne (Join-Path $jobRoot 'config-bound.json') -or
            (Hash $retest.originalConfig) -cne $retest.originalSha256 -or
            (Hash $configPath) -cne $retest.newSha256 -or
            $retest.preflightResult -cne ([IO.Path]::GetFullPath($PreflightResult)) -or
            (Hash $PreflightResult) -cne $retest.preflightSha256 -or
            -not $configPath.StartsWith((Split-Path $recordPath -Parent)+'\', [StringComparison]::OrdinalIgnoreCase)) { throw 'External retest copy identity differs.' }
    }
    elseif (-not $configPath.StartsWith($jobRoot+'\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use only the validated run configuration copy.' }
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    if ($config.build.mode -cne 'existing' -or $config.build.assemblyManifest -cne (Join-Path $jobRoot 'assembly-manifest-v3.json') -or
        -not $config.rulesDirectory.StartsWith($jobRoot+'\', [StringComparison]::OrdinalIgnoreCase) -or
        ($retest -and ($config.output.directory -cne $retest.newReports -or
            -not $config.output.directory.StartsWith($output+'\', [StringComparison]::OrdinalIgnoreCase))) -or
        (-not $retest -and -not $config.output.directory.StartsWith($jobRoot+'\', [StringComparison]::OrdinalIgnoreCase)) -or
        (Test-Path -LiteralPath $config.output.directory)) { throw 'Expected unused, run-owned report output.' }
    $configSha = Hash $configPath
    $checks = [Collections.Generic.List[object]]::new()
    [void][IO.Directory]::CreateDirectory($output)
    function RunCli([string]$name, [string[]]$arguments, [int]$expectedExit) {
        CheckOriginal
        if ((Hash $configPath) -cne $configSha -or $retest -and (Hash $retest.originalConfig) -cne $retest.originalSha256) { throw 'Configuration copy changed.' }
        $start = [Diagnostics.ProcessStartInfo]::new($cli)
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $output
        $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
        foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
        $start.Environment['DOTNET_CLI_HOME'] = Join-Path $output 'cli-home'
        $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] = '0'
        $process = [Diagnostics.Process]::Start($start)
        try {
            $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit(900000)) { $process.Kill($true); throw "$name timed out; preserve evidence." }
            [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
            [IO.File]::WriteAllText((Join-Path $output "$name.stdout.json"),$stdout.Result,[Text.UTF8Encoding]::new($false))
            [IO.File]::WriteAllText((Join-Path $output "$name.stderr.log"),$stderr.Result,[Text.UTF8Encoding]::new($false))
            $equal = (Get-ArchSiftHostHash) -ceq $baseline
            $checks.Add([ordered]@{name=$name;exitCode=$process.ExitCode;hostStateEqual=$equal})
            [IO.File]::WriteAllText((Join-Path $output 'checks.json'),($checks.ToArray() | ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
            CheckOriginal
            if (-not $equal -or $process.ExitCode -ne $expectedExit) { throw "$name returned unexpected exit or host drift; inspect retained output." }
            Write-Host "$name exit=$($process.ExitCode) host-state-equal=$equal"
            return ($stdout.Result | ConvertFrom-Json)
        } finally { $process.Dispose() }
    }
    $validated = RunCli 'validate' @('evidence','validate','--config',$configPath) 0
    if (-not $validated.sourceBound -or -not $validated.coverageComplete -or $validated.contexts.Count -ne 105 -or $validated.limitations.Count -ne 0) {
        throw 'Current-source validation is incomplete; no chain gate.'
    }
    $b = RunCli 'class-B' @('chain','verify','--config',$configPath,'--chain',(Join-Path $config.rulesDirectory 'chains/ifx-07-class-B-test.json'),'--target-kind','real') 4
    if ($b.entries.Count -ne 1 -or $b.entries[0].ruleResults.Count -ne 406 -or
        $b.execution -cne 'partial' -or $b.compliance -cne 'compliant' -or $b.exitCode -ne 4 -or
        @($b.entries[0].ruleResults | Where-Object { $_.status -cne 'pass' -or $_.matched -lt 1 }).Count -ne 0) {
        throw 'Class B real gate differs; no Class A gate.'
    }
    $a = RunCli 'class-A' @('chain','verify','--config',$configPath,'--chain',(Join-Path $config.rulesDirectory 'chains/ifx-07-class-A-test.json'),'--target-kind','real') 4
    if ($a.entries.Count -ne 4 -or @($a.entries | ForEach-Object ruleResults).Count -ne 182 -or
        $a.execution -cne 'partial' -or $a.compliance -cne 'compliant' -or $a.exitCode -ne 4 -or
        @($a.entries | ForEach-Object ruleResults | Where-Object { $_.status -cne 'pass' -or $_.matched -lt 1 }).Count -ne 0) {
        throw 'Class A real gate differs.'
    }
    foreach ($summary in @($b,$a)) {
        foreach ($entry in $summary.entries) {
            if (-not $entry.coverage.sourceBound -or $entry.coverage.assemblyCount -ne 105 -or $entry.coverage.projectCount -ne 105) {
                throw 'Real report coverage changed.'
            }
        }
    }
    $reports = @(Get-ChildItem -LiteralPath $config.output.directory -Recurse -File -Filter report.json)
    $summaries = @(Get-ChildItem -LiteralPath $config.output.directory -Recurse -File -Filter chain-summary.json)
    $sarif = @(Get-ChildItem -LiteralPath $config.output.directory -Recurse -File -Filter '*.sarif')
    if ($reports.Count -ne 5 -or $summaries.Count -ne 2 -or $sarif.Count -ne 7) { throw 'Report artifact counts changed.' }
    foreach ($file in $reports) {
        $report = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        if ($report.executionErrors.Count -ne 0 -or $report.findings.Count -ne 0 -or
            $report.buildContext.assemblyBindingMode -cne 'project-contexts' -or $report.buildContext.evidenceOrigin -cne 'external-reviewed' -or
            -not (Test-Json -Json (Get-Content -LiteralPath $file.FullName -Raw) -SchemaFile (Join-Path $PSScriptRoot '../../schemas/report.schema.json'))) {
            throw 'Real child report failed contract or result check.'
        }
    }
    foreach ($file in $summaries) {
        if (-not (Test-Json -Json (Get-Content -LiteralPath $file.FullName -Raw) -SchemaFile (Join-Path $PSScriptRoot '../../schemas/chain-summary-v2.schema.json'))) { throw 'Chain summary schema failed.' }
    }
    foreach ($file in $sarif) {
        if (-not (Test-Json -Json (Get-Content -LiteralPath $file.FullName -Raw) -SchemaFile 'D:\ArchSift-lab\evidence\sarif-2.1.0\sarif-schema-2.1.0.json')) { throw 'Official SARIF schema failed.' }
    }
    $record = [ordered]@{kind='ifx-0.8-bound-reports';status='pass';candidateCommit=$manifest.sourceCommit;configSha256=$configSha;originalConfigSha256=$(if($retest){$retest.originalSha256}else{$configSha});retestRecord=$(if($retest){[IO.Path]::GetFullPath($RetestRecord)}else{$null});sourceBound=$true;contexts=105;classBPass=406;classAPass=182;execution='partial';compliance='compliant';exitCode=4;reports=$reports.Count;chainSummaries=$summaries.Count;sarif=$sarif.Count;checks=$checks.ToArray();originalsUnchanged=$true;hostStateEqual=((Get-ArchSiftHostHash) -ceq $baseline)}
    [IO.File]::WriteAllText((Join-Path $output 'result.json'),($record | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    if (-not $record.hostStateEqual) { throw 'Host drift; preserve reports.' }
    $record | ConvertTo-Json -Depth 8
} finally {
    $equal = (Get-ArchSiftHostHash) -ceq $baseline
    Write-Output "ifx-0.8-bound-final-host-state-equal=$equal"
    if (-not $equal) { throw 'Host drift; preserve reports without repair.' }
}
