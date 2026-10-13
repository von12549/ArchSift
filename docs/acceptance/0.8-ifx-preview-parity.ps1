[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CorrectedPreview,
    [Parameter(Mandatory)][string]$ExecutedJob,
    [Parameter(Mandatory)][string]$OutputPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$previewPath=[IO.Path]::GetFullPath($CorrectedPreview)
$jobPath=[IO.Path]::GetFullPath($ExecutedJob)
$output=[IO.Path]::GetFullPath($OutputPath)
if(-not$previewPath.StartsWith('D:\ArchSift-lab\runs\ifx-0.8-preview-',[StringComparison]::OrdinalIgnoreCase)-or
    -not$jobPath.StartsWith('D:\ArchSift-lab\evidence\ifx-0.8\',[StringComparison]::OrdinalIgnoreCase)-or
    -not$output.StartsWith('D:\ArchSift-lab\runs\ifx-0.8-preview-',[StringComparison]::OrdinalIgnoreCase)-or
    (Test-Path -LiteralPath $output)){throw 'Parity inputs/output must be recorded external paths and output create-new.'}
$preview=Get-Content -LiteralPath $previewPath -Raw|ConvertFrom-Json
$job=Get-Content -LiteralPath $jobPath -Raw|ConvertFrom-Json
$actualRoot=$job.preview.storage.runDirectory
$plannedRoot=$preview.storage.runDirectory
if($preview.projectCount-ne105-or$job.preview.projectCount-ne105-or$preview.commands.Count-ne316-or$job.executions.Count-ne316-or
    $job.stage-cne'review-required'-or$job.unknowns.Count-ne0){throw 'Project, execution or review state differs.'}
$different=[Collections.Generic.List[object]]::new()
for($index=0;$index-lt$preview.commands.Count;$index++){
    $planned=$preview.commands[$index];$actual=$job.executions[$index]
    $stage=switch($actual.stage){'inspecting-sdk'{'sdk'}'restoring'{'restore'}'building'{'build'}'evaluating'{'evaluate'}default{$actual.stage}}
    $arguments=@($actual.arguments|ForEach-Object{[string]$_.Replace($actualRoot,$plannedRoot,[StringComparison]::OrdinalIgnoreCase)})
    if($stage-cne$planned.stage-or$planned.executable-cne$job.request.dotnetPath-or
        ($arguments|ConvertTo-Json -Compress)-cne($planned.arguments|ConvertTo-Json -Compress)){
        $different.Add([ordered]@{index=$index;plannedStage=$planned.stage;actualStage=$stage})
    }
}
$result=[ordered]@{kind='ifx-0.8-cross-candidate-command-parity';correctedPreviewSha256=(Get-FileHash -LiteralPath $previewPath -Algorithm SHA256).Hash.ToLowerInvariant();executedJobSha256=(Get-FileHash -LiteralPath $jobPath -Algorithm SHA256).Hash.ToLowerInvariant();sourceIdentityEqual=($preview.sourceIdentity-ceq$job.preview.sourceIdentity);plannedCommands=$preview.commands.Count;actualCommands=$job.executions.Count;normalizedArgumentsAndOrderEqual=($different.Count-eq0);differences=$different.ToArray();correctedRunDirectoryCreated=(Test-Path -LiteralPath $plannedRoot);note='Corrected candidate preview is compared with the earlier delegated build after only replacing the create-new run directory; this does not claim a second real build.'}
[IO.File]::WriteAllText($output,($result|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$result|ConvertTo-Json -Depth 8
if(-not$result.sourceIdentityEqual-or-not$result.normalizedArgumentsAndOrderEqual-or$result.correctedRunDirectoryCreated){throw 'Corrected preview did not match the recorded real execution.'}
