param([Parameter(Mandatory)][string]$RunId,[Parameter(Mandatory)][string]$SourceCommit,[Parameter(Mandatory)][string]$ExpectedArchiveSha256,[Parameter(Mandatory)][string]$OutputRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($RunId -cnotmatch '^\d+$' -or $SourceCommit -cnotmatch '^[a-f0-9]{40}$' -or $ExpectedArchiveSha256 -cnotmatch '^[a-f0-9]{64}$' -or (Test-Path $OutputRoot)){throw 'Pinned original artifact and new owned output required.'}
if(-not $env:GH_TOKEN){throw 'Read-only Actions token is missing.'}
[void][IO.Directory]::CreateDirectory($OutputRoot)
function Save([string]$Path,$Value){[IO.File]::WriteAllText($Path,(($Value|ConvertTo-Json -Depth 40)+"`n"),[Text.UTF8Encoding]::new($false))}
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
$client=[Net.Http.HttpClient]::new();$client.Timeout=[TimeSpan]::FromMinutes(10)
$client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$env:GH_TOKEN)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('ArchSift-Release-Verification/1.0')
function Api([string]$Suffix){$client.GetStringAsync('https://api.github.com/repos/von12549/ArchSift/'+$Suffix).GetAwaiter().GetResult()|ConvertFrom-Json}
try{
    $run=Api ('actions/runs/'+$RunId);$jobs=Api ('actions/runs/'+$RunId+'/jobs?per_page=100');$list=Api ('actions/runs/'+$RunId+'/artifacts?per_page=100')
    if($run.status -cne 'completed' -or $run.event -cne 'workflow_dispatch' -or $run.head_branch -cne 'codex/workbench-independent-driver' -or $jobs.jobs.Count -ne 1){throw 'Original run identity differs.'}
    $buildSteps=@($jobs.jobs[0].steps|Where-Object name -CEQ 'Build exact pinned source with private verified tools and explicit frozen packages')
    if($buildSteps.Count -ne 1 -or $buildSteps[0].conclusion -cne 'success'){throw 'Original product build did not pass.'}
    $artifacts=@($list.artifacts|Where-Object name -CEQ ('independent-workbench-'+$SourceCommit))
    if($artifacts.Count -ne 1 -or $artifacts[0].expired -or $artifacts[0].digest -cne ('sha256:'+$ExpectedArchiveSha256)){throw 'Original official artifact identity differs.'}
    $a=$artifacts[0];$archive=Join-Path $OutputRoot 'reused-original-artifact.zip'
    [IO.File]::WriteAllBytes($archive,$client.GetByteArrayAsync($a.archive_download_url).GetAwaiter().GetResult())
    if((Hash $archive) -cne $ExpectedArchiveSha256 -or (Get-Item $archive).Length -ne $a.size_in_bytes){throw 'Original artifact download differs.'}
    $raw=Join-Path $OutputRoot 'reused-original-raw';[IO.Compression.ZipFile]::ExtractToDirectory($archive,$raw)
    $build=Get-Content (Join-Path $raw 'transfer/build-result.json') -Raw|ConvertFrom-Json
    $proof=Get-Content (Join-Path $raw 'build-transfer.json') -Raw|ConvertFrom-Json
    if($build.status -cne 'candidate-build-and-generic-consumption-passed' -or $build.sourceCommit -cne $SourceCommit -or $proof.sourceCommit -cne $SourceCommit -or -not $build.hostEqual -or -not $proof.parentHostEqual){throw 'Original source/build proof differs.'}
    foreach($entry in $proof.assets){$path=Join-Path $raw ('transfer/assets/'+$entry.file);if((Hash $path) -cne $entry.sha256 -or (Get-Item $path).Length -ne $entry.bytes){throw 'Original frozen asset bytes differ.'}}
    Copy-Item -LiteralPath (Join-Path $raw 'transfer') -Destination $OutputRoot -Recurse
    foreach($file in Get-ChildItem -LiteralPath $raw -File|Where-Object {$_.Extension -in @('.json','.log')}){Copy-Item -LiteralPath $file.FullName -Destination $OutputRoot}
    Save (Join-Path $OutputRoot 'reused-build-origin.json') @{status='exact-original-build-assets-reused-no-rebuild';originalRun=$RunId;originalDriverCommit=$run.head_sha;sourceCommit=$SourceCommit;version=$build.version;officialArtifactId=$a.id;officialArchiveSha256=$ExpectedArchiveSha256;officialArchiveBytes=$a.size_in_bytes;originalBuildStepPassed=$true;originalOverallConclusion=$run.conclusion;originalGuestAcceptance=$false;allAssetsByteIdentical=$true;newGuestAcceptance='separate current run';originalMetadata=@{run=$run;jobs=$jobs;artifact=$a}}
    Write-Output ('exact-original-build-reused source='+$SourceCommit+' original-run='+$RunId+' no-rebuild=true')
}catch{Save (Join-Path $OutputRoot 'reuse-failure.json') @{status='stopped';message=$_.Exception.Message};throw}
finally{$client.Dispose()}
