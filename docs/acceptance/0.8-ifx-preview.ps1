[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreflightResult,
    [Parameter(Mandatory)][string]$CandidatePayload,
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/HostState.ps1')
$before=Get-ArchSiftHostHash
$lab=[IO.Path]::GetFullPath($LabRoot)
$run=Join-Path $lab ('runs/ifx-0.8-preview-'+[Guid]::NewGuid().ToString('N'))
if($run.StartsWith($repository+'\',[StringComparison]::OrdinalIgnoreCase)-or$run.StartsWith('D:\IFX-10-Root\',[StringComparison]::OrdinalIgnoreCase)){throw 'Preview output must be external.'}
[void][IO.Directory]::CreateDirectory($run)
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
try {
    $preflightPath=[IO.Path]::GetFullPath($PreflightResult)
    $preflight=Get-Content -LiteralPath $preflightPath -Raw|ConvertFrom-Json
    if($preflight.kind-cne'ifx-0.8-preflight'-or$preflight.errors.Count-ne0-or-not$preflight.hostStateEqual-or-not$preflight.target.clean-or$preflight.target.projects.Count-ne105){throw 'Preflight result is not a clean reviewed input.'}
    if($preflight.target.commit-cne'cc06528ab17052baafa496df2a89f6cde9207384'){throw 'IFX commit differs.'}
    $head=(& git -C $preflight.target.root rev-parse HEAD).Trim()
    if($LASTEXITCODE-ne0-or$head-cne$preflight.target.commit-or@(& git -C $preflight.target.root status --porcelain).Count-ne0){throw 'IFX source changed after preflight.'}
    $payload=[IO.Path]::GetFullPath($CandidatePayload)
    $manifest=Get-Content -LiteralPath (Join-Path $payload 'package-manifest.json') -Raw|ConvertFrom-Json
    if($manifest.version-cne'0.8.0'-or$manifest.sourceCommit-cne$preflight.candidate.sourceCommit-or$manifest.sourceDirty-or$manifest.productSourceDirty){throw 'Candidate payload identity differs.'}
    $cli=Join-Path $payload 'archsift.exe'
    $entry=@($manifest.entries|Where-Object path -CEQ 'archsift.exe')
    if($entry.Count-ne1-or(Hash $cli)-cne$entry[0].sha256){throw 'Candidate CLI bytes differ from package manifest.'}
    $originalConfig=[IO.Path]::GetFullPath($preflight.original.root+'\config\ifx.json')
    $frozen=@($preflight.original.frozenFiles|Where-Object path -CEQ $originalConfig)
    if($frozen.Count-ne1-or(Hash $originalConfig)-cne$frozen[0].sha256){throw 'Original config differs from preflight.'}
    $config=Get-Content -LiteralPath $originalConfig -Raw|ConvertFrom-Json
    $config.output.directory=Join-Path $run 'reports'
    $configPath=Join-Path $run 'config-copy.json'
    [IO.File]::WriteAllText($configPath,($config|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
    $runId=[Guid]::NewGuid().ToString('N')
    $request=[ordered]@{
        storage=[ordered]@{dataRoot=$null;evidenceRoot=(Join-Path $lab 'evidence/ifx-0.8');projectId='ifx-cc06528a';runId=$runId;protectedRoots=@($repository,$preflight.original.root)}
        dotnetPath='C:\Program Files\dotnet\dotnet.exe'
        localFeed='C:\Users\von12\.nuget\packages'
        sdkDirectory='C:\Program Files\dotnet\sdk\10.0.303'
        externalInputRoots=@('C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App\10.0.11')
        environmentInputNames=@()
        reviewedBuildLogic=$false
        executionReviewer=''
        executionReason=''
        timeoutSeconds=1800
    }
    $requestPath=Join-Path $run 'request-preview.json'
    [IO.File]::WriteAllText($requestPath,($request|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    $start=[Diagnostics.ProcessStartInfo]::new($cli)
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$run
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in @('evidence','preview','--config',$configPath,'--request',$requestPath)){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not$process.WaitForExit(120000)){$process.Kill($true);throw 'Read-only preview timed out.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run 'preview.stderr.log'),$stderr.Result,[Text.UTF8Encoding]::new($false))
        if($process.ExitCode-ne0){throw ('Read-only preview failed with exit '+$process.ExitCode+'.')}
        $preview=$stdout.Result|ConvertFrom-Json
        if($preview.projectCount-ne105-or$preview.sourceIdentity.Length-ne64-or$preview.previewSha256.Length-ne64-or
            $preview.storage.runId-cne$runId-or(Test-Path -LiteralPath $preview.storage.runDirectory)){
            throw 'Preview identity or no-write invariant differs.'
        }
        [IO.File]::WriteAllText((Join-Path $run 'preview.json'),$stdout.Result,[Text.UTF8Encoding]::new($false))
        $after=Get-ArchSiftHostHash
        $result=[ordered]@{kind='ifx-0.8-read-only-preview';preflightSha256=(Hash $preflightPath);candidateSourceCommit=$manifest.sourceCommit;candidateCliSha256=(Hash $cli);configCopySha256=(Hash $configPath);requestSha256=(Hash $requestPath);previewSha256=(Hash (Join-Path $run 'preview.json'));reviewedPreviewSha256=$preview.previewSha256;projectCount=$preview.projectCount;commandCount=$preview.commands.Count;sourceIdentity=$preview.sourceIdentity;runDirectory=$preview.storage.runDirectory;runDirectoryCreated=$false;targetBuildPerformed=$false;hostStateEqual=($after-ceq$before)}
        [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        $result|ConvertTo-Json -Depth 10
    }finally{$process.Dispose()}
}finally{if((Get-ArchSiftHostHash)-cne$before){throw 'Host drift; preserve preview evidence, no repair.'};Write-Output ('ifx-0.8-preview-run='+$run+' final-host-state-equal=True')}
