[CmdletBinding()]
param([string]$LabRoot = 'D:\ArchSift-lab')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/HostState.ps1')
$before = Get-ArchSiftHostHash
$target = 'D:\IFX-10-Root\IFX-New'
$original = 'D:\IFX-10-Root\ArchSift'
$packageRoot = 'D:\ArchSift-lab\packages\release-d0ad9b1cde1c45ecac21e95d0950d64c'
$jobRoot = 'D:\ArchSift-lab\evidence\ifx-0.8\ifx-cc06528a\2df22ba7e50e408b8094accc070aff28'
$previous = 'D:\ArchSift-lab\evidence\ifx-0.8-preflight-4021f5dce82d44de9380ed239dedde63\result.json'
$run = [IO.Path]::GetFullPath((Join-Path $LabRoot ('evidence/0.8-closure-post-human-' + [Guid]::NewGuid().ToString('N'))))
if ($run.StartsWith($repository + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $run.StartsWith('D:\IFX-10-Root\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Audit output must remain outside source and original installation.'
}
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
try {
    $prior = Get-Content -Raw -LiteralPath $previous | ConvertFrom-Json
    $head = (& git -C $target rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Target Git identity unavailable.' }
    $status = @(& git -C $target status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Target Git status unavailable.' }
    $selected = (Get-Content -Raw -LiteralPath (Join-Path $original 'install.json') | ConvertFrom-Json).selectedVersion
    $frozenChanged = @($prior.original.frozenFiles | Where-Object {
        -not [IO.File]::Exists($_.path) -or (Hash $_.path) -cne $_.sha256
    })
    $zipHash = Hash (Join-Path $packageRoot 'archsift-0.8.0-win-x64.zip')
    $setupHash = Hash (Join-Path $packageRoot 'setup/ArchSift.Setup.exe')
    $configHash = Hash (Join-Path $jobRoot 'config-bound.json')
    $manifestHash = Hash (Join-Path $jobRoot 'assembly-manifest-v3.json')
    $payload = [IO.Path]::GetFullPath((Join-Path $packageRoot 'payload'))
    $owned = @(Get-CimInstance Win32_Process | Where-Object {
        $_.ExecutablePath -and $_.ExecutablePath.StartsWith($payload + '\', [StringComparison]::OrdinalIgnoreCase)
    })
    $after = Get-ArchSiftHostHash
    $pass = $head -ceq 'cc06528ab17052baafa496df2a89f6cde9207384' -and $status.Count -eq 0 -and
        $selected -ceq '0.7.0' -and $frozenChanged.Count -eq 0 -and
        $zipHash -ceq 'a39de3098a966d9eb0222bd2a2dabc1f4c269e56fd9b4b0c373fe5ea9f0c42d2' -and
        $setupHash -ceq '1c45497b3b6338573af6866d40874b15ab5944c891a17c70d48e1b303d4d0790' -and
        $configHash -ceq '69199ccffc692d892f71456b8e7c31f09a23ced3be6c8b51a0ff44dd166324d5' -and
        $manifestHash -ceq 'c8ca2f9dbc44952c0b908c67f064bb04aea231a6e3523bf6386ed79ffc6b3b8b' -and
        $owned.Count -eq 0 -and $before -ceq $after
    $result = [ordered]@{
        kind = 'archsift-0.8-post-human-read-only-audit'; status = $(if ($pass) { 'pass' } else { 'fail' })
        targetCommit = $head; targetClean = ($status.Count -eq 0)
        originalSelectedVersion = $selected; originalFrozenFileCount = @($prior.original.frozenFiles).Count
        originalFrozenFilesEqual = ($frozenChanged.Count -eq 0)
        candidateZipSha256 = $zipHash; setupSha256 = $setupHash
        boundConfigSha256 = $configHash; manifestSha256 = $manifestHash
        candidateOwnedProcessCount = $owned.Count
        hostBeforeSha256 = $before; hostAfterSha256 = $after; hostStateEqual = ($before -ceq $after)
        analysisPerformed = $false; targetBuildPerformed = $false; installationChanged = $false
        humanSessionExitCodeVerified = $false; humanSessionListenerVerified = $false
    }
    [void][IO.Directory]::CreateDirectory($run)
    $path = Join-Path $run 'result.json'
    [IO.File]::WriteAllText($path, ($result | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    [ordered]@{ path = $path; sha256 = (Hash $path); status = $result.status; hostStateEqual = $result.hostStateEqual } | ConvertTo-Json
    if (-not $pass) { throw 'Post-human read-only audit failed; preserve evidence and stop.' }
} finally {
    if ((Get-ArchSiftHostHash) -cne $before) { throw 'Host drift; preserve evidence and stop without repair.' }
}
