[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CandidatePayload,
    [Parameter(Mandatory)][string]$ConfigCopy,
    [Parameter(Mandatory)][string]$JobDirectory,
    [Parameter(Mandatory)][string]$PreflightResult,
    [Parameter(Mandatory)][string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/HostState.ps1')
$baseline=Get-ArchSiftHostHash
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
try {
    $preflight=Get-Content -LiteralPath $PreflightResult -Raw|ConvertFrom-Json
    $jobRoot=[IO.Path]::GetFullPath($JobDirectory)
    $output=[IO.Path]::GetFullPath($OutputDirectory)
    if(-not$jobRoot.StartsWith('D:\ArchSift-lab\evidence\ifx-0.8\',[StringComparison]::OrdinalIgnoreCase)-or
        -not$output.StartsWith('D:\ArchSift-lab\runs\ifx-0.8-preview-',[StringComparison]::OrdinalIgnoreCase)-or
        $preflight.kind-cne'ifx-0.8-preflight'-or$preflight.errors.Count-ne0){throw 'Review scope does not refer to the recorded external run.'}
    $target=$preflight.target.root
    $head=(& git -C $target rev-parse HEAD).Trim()
    if($LASTEXITCODE-ne0-or$head-cne$preflight.target.commit-or@(& git -C $target status --porcelain).Count-ne0){throw 'IFX source changed.'}
    foreach($file in $preflight.original.frozenFiles){if((Hash $file.path)-cne$file.sha256){throw 'Original installation state changed.'}}
    $payload=[IO.Path]::GetFullPath($CandidatePayload)
    $manifest=Get-Content -LiteralPath (Join-Path $payload 'package-manifest.json') -Raw|ConvertFrom-Json
    $cli=Join-Path $payload 'archsift.exe';$entry=@($manifest.entries|Where-Object path -CEQ 'archsift.exe')
    if($manifest.version-cne'0.8.0'-or$manifest.sourceDirty-or$manifest.productSourceDirty-or$entry.Count-ne1-or(Hash $cli)-cne$entry[0].sha256){throw 'Candidate CLI identity invalid.'}
    $job=Get-Content -LiteralPath (Join-Path $jobRoot 'job.json') -Raw|ConvertFrom-Json
    if($job.stage-cne'review-required'-or$job.unknowns.Count-ne0-or$job.completedProjects-ne105-or$job.evaluatedProjects-ne105-or$job.executions.Count-ne316){throw 'IFX run is not ready for actual material review.'}
    $start=[Diagnostics.ProcessStartInfo]::new($cli)
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$output
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in @('evidence','review-scope','--config',([IO.Path]::GetFullPath($ConfigCopy)),'--job',$jobRoot)){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $output 'cli-home-review'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not$process.WaitForExit(900000)){$process.Kill($true);throw 'Read-only material scope timed out.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        $scopePath=Join-Path $output 'review-scope.json'
        if(Test-Path -LiteralPath $scopePath){throw 'Review scope output already exists.'}
        [IO.File]::WriteAllText((Join-Path $output 'review-scope.stderr.log'),$stderr.Result,[Text.UTF8Encoding]::new($false))
        if($process.ExitCode-ne0){throw ('Review scope failed with exit '+$process.ExitCode+'.')}
        $scope=$stdout.Result|ConvertFrom-Json
        if($scope.stage-cne'review-required'-or$scope.projectCount-ne105-or$scope.materialCount-ne13671-or$scope.unknowns.Count-ne1){throw 'Material scope differs from the reviewed build record.'}
        [IO.File]::WriteAllText($scopePath,$stdout.Result,[Text.UTF8Encoding]::new($false))
        $after=Get-ArchSiftHostHash
        $result=[ordered]@{kind='ifx-0.8-material-scope';candidateSourceCommit=$manifest.sourceCommit;jobSha256=(Hash (Join-Path $jobRoot 'job.json'));scopeFileSha256=(Hash $scopePath);scopeSha256=$scope.scopeSha256;captureSha256=$scope.captureSha256;templateSha256=$scope.templateSha256;projectCount=$scope.projectCount;materialCount=$scope.materialCount;materialRoles=$scope.materialRoles;unknowns=$scope.unknowns;hostStateEqual=($after-ceq$baseline)}
        $result|ConvertTo-Json -Depth 8
        if(-not$result.hostStateEqual){throw 'Host drift; no approval or repair.'}
    }finally{$process.Dispose()}
}finally{$equal=(Get-ArchSiftHostHash)-ceq$baseline;Write-Output ('ifx-0.8-review-scope-final-host-state-equal='+$equal);if(-not$equal){throw 'Host drift; preserve material evidence.'}}
