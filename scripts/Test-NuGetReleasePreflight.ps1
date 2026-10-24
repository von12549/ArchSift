param(
    [string]$ReferencePath = (Join-Path $PSScriptRoot '../docs/releases/0.11.0/trusted-publishing-reference.json'),
    [string]$PackageDirectory,
    [switch]$CheckSourceCi
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before = Get-ArchSiftHostHash
try {
    $r = Get-Content -LiteralPath $ReferencePath -Raw | ConvertFrom-Json -AsHashtable
    if ($r.schemaVersion -ne 1 -or $r.version -cne '0.11.0') { throw 'Unsupported release reference.' }
    if ($r.sourceCommit -cnotmatch '^[a-f0-9]{40}$' -or $r.sourceTree -cnotmatch '^[a-f0-9]{40}$') { throw 'Invalid source identity.' }
    if ($r.authority.preparationOnly -isnot [bool] -or -not $r.authority.preparationOnly) { throw 'Preparation-only authority required.' }
    foreach ($key in @('credentialAcquisition','publicPublication','tagOrRelease','newInstaller')) {
        if ($r.authority[$key] -isnot [bool] -or $r.authority[$key]) { throw 'Publication capabilities are disabled in this preparation.' }
    }
    $publisher = $r.trustedPublisher
    $expected = @{nugetUser='XiaolongFeng';repository='von12549/ArchSift';workflowFile='publish-nuget.yml';environment='nuget-release';globPattern='ArchSift.*';scope='push-new-packages-and-versions';configurationStatus='user-operated-pending'}
    foreach ($key in $expected.Keys) { if ($publisher[$key] -cne $expected[$key]) { throw 'Unreviewed publisher binding.' } }
    if ($r.humanAcceptance.status -cne 'passed-user-confirmed' -or $r.humanAcceptance.usabilityScore -ne 4 -or $r.humanAcceptance.usabilityMaximum -ne 5 -or $r.humanAcceptance.defectsReported -ne 0) { throw 'Final human acceptance binding differs.' }
    foreach ($key in @('acceptanceSha256','manifestSha256','checksumsSha256')) { if ($r.acceptedPreview[$key] -cnotmatch '^[a-f0-9]{64}$') { throw 'Invalid accepted-preview hash.' } }
    $ids = @('ArchSift.Contracts','ArchSift.Core','ArchSift.ArchUnit','ArchSift.Testing','ArchSift.Cli')
    if ($r.packages.Count -ne $ids.Count) { throw 'Exactly five submission packages are required.' }
    for ($i = 0; $i -lt $ids.Count; $i++) {
        $p = $r.packages[$i]
        if ($p.id -cne $ids[$i] -or $p.file -cne ($ids[$i]+'.0.11.0.nupkg') -or $p.sha256 -cnotmatch '^[a-f0-9]{64}$' -or $p.bytes -isnot [long] -and $p.bytes -isnot [int] -or $p.bytes -le 0) { throw 'Invalid package identity or dependency order.' }
    }
    if ($r.sourceCi.Count -ne 2 -or $r.sourceCi[0].event -cne 'pull_request' -or $r.sourceCi[1].event -cne 'push' -or $r.sourceCi[1].headSha -cne $r.sourceCommit) { throw 'Original source CI binding differs.' }
    foreach ($ci in $r.sourceCi) { if ($ci.runId -le 0 -or $ci.headSha -cnotmatch '^[a-f0-9]{40}$') { throw 'Invalid CI identity.' } }
    $packagesVerified = $false
    if ($PSBoundParameters.ContainsKey('PackageDirectory')) {
        if ([string]::IsNullOrWhiteSpace($PackageDirectory)) { throw 'Explicit package directory cannot be empty.' }
        $directory = Get-Item -LiteralPath $PackageDirectory
        if (-not $directory.PSIsContainer -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Expected a real package directory.' }
        $actual = @(Get-ChildItem -LiteralPath $directory.FullName -Filter '*.nupkg' -File)
        if ($actual.Count -ne 5) { throw 'Submission directory must contain exactly five packages.' }
        foreach ($p in $r.packages) {
            $path = Join-Path $directory.FullName $p.file
            $file = Get-Item -LiteralPath $path
            if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $file.Length -ne $p.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $p.sha256) { throw 'Submission package bytes differ.' }
        }
        $packagesVerified = $true
    }
    if ($CheckSourceCi) {
        $gh = (Get-Command gh -CommandType Application | Select-Object -First 1).Source
        function Read-GitHub([string]$endpoint) {
            $raw = & $gh api --hostname github.com $endpoint
            if ($LASTEXITCODE -ne 0) { throw 'Read-only GitHub source verification failed.' }
            return ($raw | ConvertFrom-Json -AsHashtable)
        }
        $source = Read-GitHub ('repos/von12549/ArchSift/git/commits/'+$r.sourceCommit)
        if ($source.sha -cne $r.sourceCommit -or $source.tree.sha -cne $r.sourceTree) { throw 'Original package source tree differs.' }
        foreach ($ci in $r.sourceCi) {
            $run = Read-GitHub ('repos/von12549/ArchSift/actions/runs/'+$ci.runId)
            if ($run.repository.full_name -cne 'von12549/ArchSift' -or $run.path -cne '.github/workflows/verify.yml' -or $run.head_sha -cne $ci.headSha -or $run.event -cne $ci.event -or $run.status -cne 'completed' -or $run.conclusion -cne 'success') { throw 'Original package source CI is not successful for its pinned identity.' }
        }
    }
    [pscustomobject]@{
        status='preflight-passed-publication-disabled';sourceCommit=$r.sourceCommit;sourceTree=$r.sourceTree
        preparationWorkflowCommit=$env:GITHUB_SHA;sourceCiChecked=[bool]$CheckSourceCi;packageBytesVerified=$packagesVerified
        humanAcceptance=$r.humanAcceptance.status;credentialAcquisition=$false;publicPublication=$false
        environmentConfiguration='not-checked-by-preflight';nugetPolicyConfiguration='not-checked-by-preflight'
    } | ConvertTo-Json
} finally {
    if ((Get-ArchSiftHostHash) -cne $before) { throw 'Host environment/Profile drift; stop without repair.' }
}
