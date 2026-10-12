[CmdletBinding()]
param(
    [ValidateSet('Baseline','Final')][string]$Stage='Baseline',
    [Parameter(Mandatory)][string]$AuditRoot,
    [string]$CandidateRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
$audit=[IO.Path]::GetFullPath($AuditRoot)
if(!$audit.StartsWith('D:\ArchSift-lab\evidence\0.7-closure-',[StringComparison]::OrdinalIgnoreCase)) { throw 'External closure audit directory required.' }
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Save([string]$name,$value) { [IO.File]::WriteAllText((Join-Path $audit $name),($value|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false)) }
$before=Get-ArchSiftHostHash
$target='D:\IFX-10-Root\IFX-New'
$preflight=Get-Content -Raw 'D:\ArchSift-lab\evidence\ifx-0.7-preflight-527f4137255440f3b682467a9fbbf0ec\result.json'|ConvertFrom-Json
if((& git -C $target rev-parse HEAD).Trim() -cne $preflight.target.commit -or (& git -C $target status --porcelain)) { throw 'Original IFX commit/worktree changed.' }
foreach($file in $preflight.original.frozenFiles) { if((Hash $file.path) -cne $file.sha256) { throw 'Original IFX state changed.' } }
$tracked=@(& git -C $target -c core.quotepath=false ls-files)
if($LASTEXITCODE -ne 0) { throw 'Cannot enumerate target tracked inputs.' }
$records=@($tracked|Sort-Object|ForEach-Object{[ordered]@{path=$_;sha256=(Hash (Join-Path $target $_))}})
$sourceHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($records|ConvertTo-Json -Compress)))).ToLowerInvariant()
if($Stage -eq 'Baseline') {
    if(Test-Path -LiteralPath $audit) { throw 'Baseline directory must be new.' }
    [void][IO.Directory]::CreateDirectory($audit)
    Save 'baseline.json' ([ordered]@{targetCommit=$preflight.target.commit;trackedFiles=$records;trackedSha256=$sourceHash;hostSha256=$before;originalFrozenFiles=$preflight.original.frozenFiles.Count})
    [ordered]@{auditRoot=$audit;trackedFiles=$records.Count;hostSha256=$before;targetSha256=$sourceHash}|ConvertTo-Json -Compress
} else {
    $baseline=Get-Content -Raw (Join-Path $audit 'baseline.json')|ConvertFrom-Json
    if($before -cne $baseline.hostSha256 -or $sourceHash -cne $baseline.trackedSha256) { throw 'Baseline host/source drift; preserve evidence.' }
    $candidate=[IO.Path]::GetFullPath($CandidateRoot)
    if(!$candidate.StartsWith('D:\ArchSift-lab\packages\release-',[StringComparison]::OrdinalIgnoreCase)) { throw 'Recorded external candidate required.' }
    $package=Get-Content -Raw (Join-Path $candidate 'package-result.json')|ConvertFrom-Json
    if((Hash $package.zipPath) -cne $package.zipSha256 -or (Hash $package.setupPath) -cne $package.setupSha256 -or !$package.hostStateEqual -or $package.productSourceDirty) { throw 'Frozen package identity changed.' }
    $manifest=Get-Content -Raw (Join-Path $candidate 'payload/package-manifest.json')|ConvertFrom-Json
    foreach($entry in $manifest.entries) {
        $p=[IO.Path]::GetFullPath((Join-Path (Join-Path $candidate 'payload') $entry.path))
        if(!$p.StartsWith((Join-Path $candidate 'payload')+'\',[StringComparison]::OrdinalIgnoreCase) -or (Hash $p) -cne $entry.sha256 -or (Get-Item -LiteralPath $p).Length -ne $entry.bytes) { throw 'Candidate payload changed.' }
    }
    $originalManifestPath='D:\IFX-10-Root\ArchSift\versions\0.6.0\package-manifest.json'
    $originalManifest=Get-Content -Raw $originalManifestPath|ConvertFrom-Json
    foreach($entry in $originalManifest.entries) {
        $p=[IO.Path]::GetFullPath((Join-Path 'D:\IFX-10-Root\ArchSift\versions\0.6.0' $entry.path))
        if(!$p.StartsWith('D:\IFX-10-Root\ArchSift\versions\0.6.0\',[StringComparison]::OrdinalIgnoreCase) -or (Hash $p) -cne $entry.sha256 -or (Get-Item -LiteralPath $p).Length -ne $entry.bytes) { throw 'Original installation payload changed.' }
    }
    $processes=@(Get-CimInstance Win32_Process|Where-Object {$_.Name -in 'dotnet.exe','archsift.exe','ArchSift.Web.exe','ArchSift.Setup.exe' -and ($_.CommandLine -like '*D:\ArchSift-lab\*' -or $_.CommandLine -like '*D:\ArchSift\src\*')})
    $ids=@($processes|ForEach-Object ProcessId)
    $listeners=@(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue|Where-Object {$_.OwningProcess -in $ids}|Select-Object LocalPort,OwningProcess)
    $after=Get-ArchSiftHostHash
    Save 'result.json' ([ordered]@{status=$(if($processes.Count -or $listeners.Count -or $after -cne $before){'fail'}else{'pass'});sourceCommit=$package.sourceCommit;candidateRoot=$candidate;zipSha256=$package.zipSha256;setupSha256=$package.setupSha256;payloadEntryCount=$manifest.entries.Count;originalPayloadEntryCount=$originalManifest.entries.Count;originalFrozenFileCount=$preflight.original.frozenFiles.Count;targetTrackedFileCount=$records.Count;targetTrackedSha256=$sourceHash;originalUnchanged=$true;hostBeforeSha256=$before;hostAfterSha256=$after;hostStateEqual=($before -ceq $after);residualProcesses=@($processes|Select-Object ProcessId,Name);residualListeners=$listeners;humanUiAcceptance='pending'})
    if($processes.Count -or $listeners.Count -or $after -cne $before) { throw 'Final machine safety audit failed; preserve evidence.' }
    [ordered]@{auditRoot=$audit;status='pass';hostStateEqual=$true;originalUnchanged=$true;noOwnedProcess=$true;payloadCount=$manifest.entries.Count}|ConvertTo-Json -Compress
}
if((Get-ArchSiftHostHash) -cne $before) { throw 'Host drift during audit.' }
