[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CandidatePayload,
    [Parameter(Mandatory)][string]$ConfigCopy,
    [Parameter(Mandatory)][string]$JobDirectory,
    [Parameter(Mandatory)][string]$PreflightResult,
    [Parameter(Mandatory)][string]$ScopeResult,
    [Parameter(Mandatory)][string]$ParityResult,
    [Parameter(Mandatory)][string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
$baseline = Get-ArchSiftHostHash
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function SaveNew([string]$path, $value) {
    if (Test-Path -LiteralPath $path) { throw "Existing output: $path" }
    [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
}
try {
    $jobRoot = [IO.Path]::GetFullPath($JobDirectory)
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    if (-not $jobRoot.StartsWith('D:\ArchSift-lab\evidence\ifx-0.8\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $output.StartsWith('D:\ArchSift-lab\runs\ifx-0.8-review-', [StringComparison]::OrdinalIgnoreCase) -or
        (Test-Path -LiteralPath $output)) { throw 'Review paths must be the recorded external run and a new external review directory.' }
    $preflight = Get-Content -LiteralPath $PreflightResult -Raw | ConvertFrom-Json
    $target = $preflight.target.root
    if ($preflight.errors.Count -ne 0 -or (& git -C $target rev-parse HEAD).Trim() -cne $preflight.target.commit -or
        @(& git -C $target status --porcelain).Count -ne 0) { throw 'IFX target identity changed.' }
    foreach ($file in $preflight.original.frozenFiles) { if ((Hash $file.path) -cne $file.sha256) { throw 'Original installation changed.' } }
    $payload = [IO.Path]::GetFullPath($CandidatePayload)
    $manifest = Get-Content -LiteralPath (Join-Path $payload 'package-manifest.json') -Raw | ConvertFrom-Json
    $cli = Join-Path $payload 'archsift.exe'
    $entry = @($manifest.entries | Where-Object path -CEQ 'archsift.exe')
    if ($manifest.version -cne '0.8.0' -or $manifest.sourceDirty -or $manifest.productSourceDirty -or $entry.Count -ne 1 -or
        (Hash $cli) -cne $entry[0].sha256) { throw 'Candidate CLI identity changed.' }
    $job = Get-Content -LiteralPath (Join-Path $jobRoot 'job.json') -Raw | ConvertFrom-Json
    $scope = Get-Content -LiteralPath $ScopeResult -Raw | ConvertFrom-Json
    $parity = Get-Content -LiteralPath $ParityResult -Raw | ConvertFrom-Json
    if ($job.stage -cne 'review-required' -or $job.unknowns.Count -ne 0 -or $job.completedProjects -ne 105 -or
        $job.evaluatedProjects -ne 105 -or $job.executions.Count -ne 316 -or
        @($job.executions | Where-Object { $_.exitCode -ne 0 -or -not $_.ownedTreeStopped -or -not $_.hostStateEqual }).Count -ne 0 -or
        $scope.scopeSha256 -cne '198d3d4a7a47c67035c3ae2a91c36e540b052672eb625eef15513b2d8dfce4ee' -or
        $scope.materialCount -ne 13671 -or $scope.projectCount -ne 105 -or
        (Hash (Join-Path $jobRoot 'capture.json')) -cne $scope.captureSha256 -or
        (Hash (Join-Path $jobRoot 'review-template-v2.json')) -cne $scope.templateSha256 -or
        -not $parity.normalizedArgumentsAndOrderEqual -or -not $parity.sourceIdentityEqual -or
        $parity.plannedCommands -ne 316 -or $parity.actualCommands -ne 316 -or $parity.correctedRunDirectoryCreated) {
        throw 'Recorded build, material scope or corrected preview parity changed.'
    }
    $audit = Get-Content -LiteralPath (Join-Path $jobRoot 'compiler-input-audit.json') -Raw | ConvertFrom-Json
    $inputs = @($audit.projects | ForEach-Object { $_.inputs })
    $kinds = $inputs | Group-Object kind
    function CountKind([string]$kind) { [int](@($kinds | Where-Object Name -CEQ $kind | ForEach-Object Count)[0]) }
    if ($audit.projects.Count -ne 105 -or $inputs.Count -ne 29817 -or
        (CountKind 'captured-external') -ne 27463 -or (CountKind 'captured-source') -ne 545 -or
        (CountKind 'generated-input') -ne 1809 -or
        @($inputs | Where-Object { $_.kind -eq 'generated-input' -and $_.path -notmatch '\\(obj|bin)\\' }).Count -ne 0) {
        throw 'Actual compiler input relationships changed.'
    }
    $template = Get-Content -LiteralPath (Join-Path $jobRoot 'review-template-v2.json') -Raw | ConvertFrom-Json
    if ($template.imports.Count -ne 106 -or $template.generatorsAndTasks.Count -ne 36 -or
        $template.projectContexts.Count -ne 105 -or $template.assemblies.Count -ne 105 -or
        @($template.imports + $template.generatorsAndTasks | Where-Object {
            $_ -notlike 'C:\Program Files\dotnet\*' -and $_ -notlike "$jobRoot\snapshot\*" -and
            $_ -notlike "$jobRoot\packages\*"
        }).Count -ne 0) { throw 'Import/task/project context boundary changed.' }
    [void][IO.Directory]::CreateDirectory($output)
    $request = [ordered]@{
        jobDirectory = $jobRoot
        reviewScopeSha256 = $scope.scopeSha256
        reviewer = 'Codex'
        reviewerKind = 'delegated-agent'
        authorizationReference = 'User authorized this IFX 0.8 run for preview SHA-256 098b6bc5dd26063e2b612fe589aa697c28cf1e899e6f35f9d0d2b2b6406f88ee and continuation of machine verification on 2026-10-13; independent human UI remains reserved.'
        reason = 'Actual 105-project Debug/net10.0 external snapshot build completed 316/316 reviewed offline SDK, restore, build and ResolveReferences commands with exit 0, stopped process trees and equal host state. Corrected 0.8 preview has 316/316 order/argument parity after only run-root substitution. Source identity matches the clean target. The captured 105 compiled assemblies, evaluations and 13,671 review materials are fixed by the reviewed scope hash. Csc audit covers 29,817 reads with no missing input: 27,463 captured external, 545 captured source and 1,809 generated reads. Package build logic comparison found 197 props/targets and 2,031 DLLs byte-identical to the previously reviewed 0.7 package. No target-source custom Target, UsingTask or Exec was found.'
        generatedInputRelationships = @(
            'The 586 captured source files map by input identity to the clean IFX target; the build operates on the external snapshot, never the target checkout.',
            'The 13,355 captured external files cover SDK/reference/package inputs; each actual Csc external input is recorded with its hash.',
            'All 1,809 generated compiler reads resolve under this run snapshot obj/bin; generated editorconfig, GlobalUsings, AssemblyInfo and project reference assemblies are materialized by this run restore/build stages before Csc reads them.',
            'Each of the 105 evaluated project contexts binds its built assembly to its actual ResolveReferences results; imports and generator/task paths are inside captured SDK, external snapshot or the run-owned package copy.'
        )
        importTaskAssessment = 'Reviewed current source/root props and actual package task scope. No source custom Target, UsingTask or Exec. The 106 recorded imports and 36 generator/task paths are within captured SDK, external snapshot or the run-owned package copy; package props/targets and DLL bytes match the earlier reviewed build package. Restore uses only the explicit local source with NuGetAudit=false; build disables project-reference rebuild, build servers and shared compilation; evaluate invokes ResolveReferences. xUnit and Microsoft.Testing.Platform test entry targets are not invoked. MSBuildAllProjects/import enumeration is not exhaustive, so the command traces, package comparison and compiler-input audit bound this approval rather than a claim of general sandboxing.'
        boundaries = @(
            'Approval covers this one 105-project SDK 10.0.303, net10.0, Debug external snapshot and recorded input scope only.',
            'MSBuild is not an OS sandbox; conditional/dynamic build behavior and uncaptured runtime behavior remain outside this review.',
            'No application or test entry was run, and this approval does not certify deployment, runtime, architecture policy adoption or human UI usability.',
            'The original IFX checkout and 0.7 installation remain read-only; any current-source conclusion still requires independent seal/validation.'
        )
        approve = $true
    }
    $requestPath = Join-Path $output 'request.json'
    SaveNew $requestPath $request
    $start = [Diagnostics.ProcessStartInfo]::new($cli)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $output
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in @('evidence','approve','--config',([IO.Path]::GetFullPath($ConfigCopy)),'--request',$requestPath)) { $start.ArgumentList.Add($argument) }
    $start.Environment['DOTNET_CLI_HOME'] = Join-Path $output 'cli-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] = '0'
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(900000)) { $process.Kill($true); throw 'Review/seal/validation timed out; preserve evidence.' }
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        SaveNew (Join-Path $output 'execution.json') ([ordered]@{exitCode=$process.ExitCode;stdout=$stdout.Result;stderr=$stderr.Result})
        if ($process.ExitCode -ne 0) { throw "Review/seal/validation exited $($process.ExitCode); preserve evidence." }
        $result = $stdout.Result | ConvertFrom-Json
        if ($result.stage -cne 'validated' -or -not $result.validation.sourceBound -or
            -not $result.validation.coverageComplete -or $result.validation.contextCount -ne 105) {
            throw 'Independent validation did not bind all 105 project contexts.'
        }
        SaveNew (Join-Path $output 'result.json') ([ordered]@{kind='ifx-0.8-delegated-material-review';candidateCommit=$manifest.sourceCommit;requestSha256=(Hash $requestPath);scopeSha256=$scope.scopeSha256;stage=$result.stage;sourceBound=$result.validation.sourceBound;contexts=$result.validation.contextCount;manifestPath=$result.manifestPath;manifestSha256=(Hash $result.manifestPath);reviewPath=$result.reviewPath;reviewSha256=(Hash $result.reviewPath);hostStateEqual=((Get-ArchSiftHostHash) -ceq $baseline)})
        if ((Get-ArchSiftHostHash) -cne $baseline) { throw 'Host drift; preserve evidence.' }
        Get-Content -LiteralPath (Join-Path $output 'result.json') -Raw
    } finally { $process.Dispose() }
} finally {
    $equal = (Get-ArchSiftHostHash) -ceq $baseline
    Write-Output "ifx-0.8-approve-final-host-state-equal=$equal"
    if (-not $equal) { throw 'Host drift; preserve evidence without repair.' }
}
