[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreviewRun,
    [Parameter(Mandatory)][string]$PreflightResult,
    [Parameter(Mandatory)][string]$CandidatePayload,
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/HostState.ps1')
$baseline=Get-ArchSiftHostHash
$lab=[IO.Path]::GetFullPath($LabRoot)
$review=[IO.Path]::GetFullPath($PreviewRun)
if(-not$review.StartsWith((Join-Path $lab 'runs')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Preview review directory must be in the external lab.'}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
try {
    $record=Get-Content -LiteralPath (Join-Path $review 'result.json') -Raw|ConvertFrom-Json
    $preflightPath=[IO.Path]::GetFullPath($PreflightResult)
    $preflight=Get-Content -LiteralPath $preflightPath -Raw|ConvertFrom-Json
    $payload=[IO.Path]::GetFullPath($CandidatePayload)
    $manifest=Get-Content -LiteralPath (Join-Path $payload 'package-manifest.json') -Raw|ConvertFrom-Json
    $cli=Join-Path $payload 'archsift.exe'
    $configPath=Join-Path $review 'config-copy.json'
    $requestPath=Join-Path $review 'request-preview.json'
    $previewPath=Join-Path $review 'preview.json'
    $preview=Get-Content -LiteralPath $previewPath -Raw|ConvertFrom-Json
    if($record.kind-cne'ifx-0.8-read-only-preview'-or-not$record.hostStateEqual-or$record.runDirectoryCreated-or$record.targetBuildPerformed-or
        $preflight.kind-cne'ifx-0.8-preflight'-or$preflight.errors.Count-ne0-or
        (Hash $preflightPath)-cne$record.preflightSha256-or(Hash $cli)-cne$record.candidateCliSha256-or
        (Hash $configPath)-cne$record.configCopySha256-or(Hash $requestPath)-cne$record.requestSha256-or(Hash $previewPath)-cne$record.previewSha256-or
        $preview.previewSha256-cne$record.reviewedPreviewSha256-or$manifest.sourceCommit-cne$record.candidateSourceCommit){throw 'Reviewed preview/candidate identity changed.'}
    $target=$preflight.target.root
    function CheckOriginals {
        $head=(& git -C $target rev-parse HEAD).Trim()
        if($LASTEXITCODE-ne0-or$head-cne$preflight.target.commit-or@(& git -C $target status --porcelain).Count-ne0){throw 'IFX source identity changed; stop.'}
        foreach($file in $preflight.original.frozenFiles){if((Hash $file.path)-cne$file.sha256){throw 'Original ArchSift state changed; stop.'}}
        if((Get-Content -LiteralPath (Join-Path $preflight.original.root 'install.json') -Raw|ConvertFrom-Json).selectedVersion-cne'0.7.0'){throw 'Original installation selection changed; stop.'}
    }
    CheckOriginals
    $runDirectory=$preview.storage.runDirectory
    if($preview.projectCount-ne105-or$preview.commands.Count-ne316-or$runDirectory-cne$record.runDirectory-or(Test-Path -LiteralPath $runDirectory)){throw 'Reviewed project/command count or create-new run changed.'}
    if(-not$runDirectory.StartsWith((Join-Path $lab 'evidence/ifx-0.8')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence run escapes reviewed external root.'}
    $counts=$preview.commands|Group-Object stage
    foreach($expected in @(@('sdk',1),@('restore',105),@('build',105),@('evaluate',105))){
        $found=@($counts|Where-Object Name -CEQ $expected[0]);if($found.Count-ne1-or$found[0].Count-ne$expected[1]){throw 'Reviewed command stage count changed.'}
    }
    foreach($command in $preview.commands){
        if($command.executable-cne'C:\Program Files\dotnet\dotnet.exe'-or-not$command.workingDirectory.StartsWith((Join-Path $runDirectory 'snapshot'),[StringComparison]::OrdinalIgnoreCase)){throw 'Reviewed command executable/working directory changed.'}
        if($command.stage-eq'restore'-and(@($command.arguments|Where-Object{$_-eq'--source'}).Count-ne1-or-not($command.arguments-contains'-p:NuGetAudit=false'))){throw 'Restore is not the reviewed offline form.'}
        if($command.stage-eq'build'-and(-not($command.arguments-contains'--no-restore')-or-not($command.arguments-contains'-p:BuildProjectReferences=false'))){throw 'Build isolation flags changed.'}
        if($command.stage-eq'evaluate'-and-not($command.arguments-contains'-t:ResolveReferences')){throw 'Evaluation target changed.'}
        if($command.arguments[0]-in@('run','test','vstest','publish')){throw 'Application/test entry point command was requested.'}
    }
    $request=Get-Content -LiteralPath $requestPath -Raw|ConvertFrom-Json
    if($request.reviewedBuildLogic-or$request.storage.runId-cne$preview.storage.runId-or$request.storage.evidenceRoot-cne$preview.storage.evidenceRoot){throw 'Preview request changed.'}
    $request.reviewedBuildLogic=$true
    $request.executionReviewer='Codex'
    $request.executionReason='User explicitly delegated this one IFX 0.8 run after read-only preview SHA-256 '+$preview.previewSha256+'; 105 SDK-style projects, external snapshot/offline restore/compile/ResolveReferences, no application or test entry points.'
    $buildRequest=Join-Path $review 'request-build.json'
    if(Test-Path -LiteralPath $buildRequest){throw 'Build request already exists; this reviewed run is single-use.'}
    [IO.File]::WriteAllText($buildRequest,($request|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $start=[Diagnostics.ProcessStartInfo]::new($cli)
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$review
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in @('evidence','build','--config',$configPath,'--request',$buildRequest,'--preview-sha256',$preview.previewSha256)){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $review 'cli-home-build'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        $started=[DateTimeOffset]::UtcNow;$lastStage='';$lastCheck=[DateTimeOffset]::MinValue
        while(-not$process.WaitForExit(1000)){
            if(([DateTimeOffset]::UtcNow-$started).TotalHours-gt4){$process.Kill($true);throw 'Overall reviewed build time cap reached; evidence retained.'}
            if(([DateTimeOffset]::UtcNow-$lastCheck).TotalSeconds-ge30){
                try {
                    CheckOriginals
                    if((Get-ArchSiftHostHash)-cne$baseline){throw 'Host drift; evidence retained, no repair.'}
                } catch {$process.Kill($true);throw}
                $jobPath=Join-Path $runDirectory 'job.json'
                if(Test-Path -LiteralPath $jobPath){
                    try{$job=Get-Content -LiteralPath $jobPath -Raw|ConvertFrom-Json;$stage=$job.stage+'/'+$job.completedProjects+'/'+$job.evaluatedProjects
                        if($stage-cne$lastStage){Write-Output ('ifx-0.8-stage='+$stage);$lastStage=$stage}}catch{Write-Output 'ifx-0.8-stage=updating'}
                }
                $lastCheck=[DateTimeOffset]::UtcNow
            }
        }
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $review 'build.stdout.json'),$stdout.Result,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $review 'build.stderr.log'),$stderr.Result,[Text.UTF8Encoding]::new($false))
        CheckOriginals
        $equal=(Get-ArchSiftHostHash)-ceq$baseline
        $jobFile=Join-Path $runDirectory 'job.json'
        $job=if(Test-Path -LiteralPath $jobFile){Get-Content -LiteralPath $jobFile -Raw|ConvertFrom-Json}else{$null}
        $result=[ordered]@{kind='ifx-0.8-delegated-build';previewSha256=$preview.previewSha256;buildRequestSha256=(Hash $buildRequest);candidateSourceCommit=$manifest.sourceCommit;exitCode=$process.ExitCode;stage=$job.stage;completedProjects=$job.completedProjects;evaluatedProjects=$job.evaluatedProjects;runDirectory=$runDirectory;jobSha256=$(if($job){Hash $jobFile}else{$null});hostStateEqual=$equal;originalsUnchanged=$true}
        [IO.File]::WriteAllText((Join-Path $review 'build-result.json'),($result|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        $result|ConvertTo-Json -Depth 10
        if(-not$equal){throw 'Host drift; stop and preserve evidence.'}
        if($process.ExitCode-ne4-or$job.stage-notin@('review-required','inputs-need-recapture')){throw 'Build did not reach a reviewable stage; inspect current attempt before another gate.'}
    }finally{$process.Dispose()}
}finally{$equal=(Get-ArchSiftHostHash)-ceq$baseline;Write-Output ('ifx-0.8-build-final-host-state-equal='+$equal);if(-not$equal){throw 'Host drift; preserve evidence, no repair.'}}
