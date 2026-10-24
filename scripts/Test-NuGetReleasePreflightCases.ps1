param([Parameter(Mandatory)][string]$OutputRoot)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before = Get-ArchSiftHostHash
if (Test-Path -LiteralPath $OutputRoot) { throw 'Use a fresh owned test output directory; preserve earlier evidence.' }
[void][IO.Directory]::CreateDirectory($OutputRoot)
$reference = Join-Path $PSScriptRoot '../docs/releases/0.11.0/trusted-publishing-reference.json'
$driver = Join-Path $PSScriptRoot 'Test-NuGetReleasePreflight.ps1'
$original = Get-Content -LiteralPath $reference -Raw
$results = [Collections.Generic.List[object]]::new()
function Case([string]$name,[scriptblock]$body,[bool]$shouldPass) {
    $failed = $false
    try { & $body | Out-Null } catch { $failed = $true }
    if ($failed -eq $shouldPass) { throw ('Unexpected preflight result: '+$name) }
    $results.Add(@{name=$name;expected=$(if($shouldPass){'pass'}else{'reject'});status='passed'})
}
function Mutate([string]$name,[scriptblock]$change) {
    $r = $original | ConvertFrom-Json -AsHashtable
    & $change $r
    $path = Join-Path $OutputRoot ($name+'.json')
    $r | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $path -Encoding utf8NoBOM
    & $driver -ReferencePath $path
}
try {
    Case 'accepted-reference' { & $driver } $true
    Case 'publication-enabled' { Mutate 'publication' {param($r) $r.authority.publicPublication=$true} } $false
    Case 'credential-enabled' { Mutate 'credential' {param($r) $r.authority.credentialAcquisition=$true} } $false
    Case 'string-false-is-not-authority' { Mutate 'string-false' {param($r) $r.authority.publicPublication='false'} } $false
    Case 'unaccepted-human-result' { Mutate 'human' {param($r) $r.humanAcceptance.status='pending'} } $false
    Case 'wrong-username' { Mutate 'username' {param($r) $r.trustedPublisher.nugetUser='other'} } $false
    Case 'wrong-environment' { Mutate 'environment' {param($r) $r.trustedPublisher.environment=''} } $false
    Case 'wrong-workflow' { Mutate 'workflow' {param($r) $r.trustedPublisher.workflowFile='verify.yml'} } $false
    Case 'missing-package' { Mutate 'missing' {param($r) $r.packages=@($r.packages|Select-Object -First 4)} } $false
    Case 'duplicate-package' { Mutate 'duplicate' {param($r) $r.packages[4]=$r.packages[0]} } $false
    Case 'path-traversal-package' { Mutate 'path' {param($r) $r.packages[0].file='../escape.nupkg'} } $false
    Case 'wrong-source-ci' { Mutate 'ci' {param($r) $r.sourceCi[1].headSha=('0'*40)} } $false
    Case 'missing-source-field' { Mutate 'field' {param($r) $r.Remove('sourceTree')} } $false
    Case 'empty-explicit-directory' { & $driver -PackageDirectory '' } $false
    Case 'nonexistent-directory' { & $driver -PackageDirectory (Join-Path $OutputRoot 'absent') } $false
    $synthetic = $original | ConvertFrom-Json -AsHashtable
    $packages = Join-Path $OutputRoot 'synthetic-packages'
    [void][IO.Directory]::CreateDirectory($packages)
    foreach ($p in $synthetic.packages) {
        $path = Join-Path $packages $p.file
        [IO.File]::WriteAllText($path,('Synthetic hash fixture: '+$p.id))
        $p.bytes=(Get-Item -LiteralPath $path).Length
        $p.sha256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
    }
    $syntheticPath=Join-Path $OutputRoot 'synthetic-reference.json'
    $synthetic | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $syntheticPath -Encoding utf8NoBOM
    Case 'synthetic-byte-identity' { & $driver -ReferencePath $syntheticPath -PackageDirectory $packages } $true
    $tamper=Join-Path $packages $synthetic.packages[0].file
    $bytes=[IO.File]::ReadAllBytes($tamper);$bytes[0]=$bytes[0] -bxor 1;[IO.File]::WriteAllBytes($tamper,$bytes)
    Case 'same-length-tampered-package' { & $driver -ReferencePath $syntheticPath -PackageDirectory $packages } $false
    [IO.File]::WriteAllText((Join-Path $packages 'unexpected.nupkg'),'extra')
    Case 'unexpected-sixth-package' { & $driver -ReferencePath $syntheticPath -PackageDirectory $packages } $false
    if ((Get-ArchSiftHostHash) -cne $before) { throw 'Host drift after cases; stop without repair.' }
    @{status='passed';cases=$results;count=$results.Count;hostEqual=$true;syntheticPackagesAreNotReleaseArchives=$true;networkOrCredentialTest=$false} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot 'result.json') -Encoding utf8NoBOM
    Write-Output ('Passed '+$results.Count+' preflight contract/byte-boundary cases; no credentials or publishing.')
} finally {
    if ((Get-ArchSiftHostHash) -cne $before) { throw 'Host environment/Profile drift; stop without repair.' }
}
