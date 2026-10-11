[CmdletBinding()]
param([string] $EvidenceRoot = 'D:\ArchSift-lab\evidence')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before = Get-ArchSiftHostHash
$installRoot = 'D:\IFX-10-Root\ArchSift'
$payloadRoot = Join-Path $installRoot 'versions/0.6.0'
$manifestPath = Join-Path $payloadRoot 'package-manifest.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$acceptance = Get-Content -Raw -LiteralPath (Join-Path $repository 'docs/releases/0.6.0/acceptance.json') | ConvertFrom-Json
$errors = [Collections.Generic.List[string]]::new()
function Hash-File([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
if ((Hash-File $manifestPath) -cne $acceptance.manifestSha256) { $errors.Add('Manifest hash differs.') }
if ($manifest.sourceCommit -cne $acceptance.sourceCommit -or $manifest.version -cne '0.6.0') { $errors.Add('Product identity differs.') }
$payload = @()
foreach ($entry in $manifest.entries) {
    $path = [IO.Path]::GetFullPath((Join-Path $payloadRoot $entry.path))
    if (-not $path.StartsWith($payloadRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Payload path escapes root.' }
    $item = Get-Item -LiteralPath $path
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked payload rejected.' }
    $hash = Hash-File $path
    $equal = $hash -ceq $entry.sha256 -and $item.Length -eq $entry.bytes
    if (-not $equal) { $errors.Add('Payload differs: ' + $entry.path) }
    $payload += [ordered]@{path=$entry.path;sha256=$hash;bytes=$item.Length;equal=$equal}
}
if ($payload.Count -ne $acceptance.payloadEntryCount) { $errors.Add('Payload count differs.') }
$frozen = @()
$paths = @(Get-ChildItem -LiteralPath (Join-Path $repository 'schemas') -Filter '*.json' -File | ForEach-Object FullName)
$paths += @(Get-ChildItem -LiteralPath (Join-Path $installRoot 'config'),(Join-Path $installRoot 'rules') -Recurse -Force -File | Where-Object { $_.Extension -eq '.json' } | ForEach-Object FullName)
$paths += (Join-Path $installRoot 'install.json')
$reportPath = Join-Path $installRoot 'reports/0e33886572de4309808cbbec9bceb671/2c6569663f4046d598366ef6c27265d5/b6beed746bf14411b23dbbcfcfa19449/report.json'
$paths += $reportPath,(Join-Path $installRoot 'reports/0e33886572de4309808cbbec9bceb671/chain-summary.json')
foreach ($path in $paths | Sort-Object -Unique) { $frozen += [ordered]@{path=$path;sha256=(Hash-File $path);bytes=(Get-Item -LiteralPath $path).Length} }
$report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
if ($report.toolVersion -cne '0.6.0' -or $report.coverage.assemblyCount -ne 0 -or $report.coverage.sourceBound -or
    @($report.ruleResults).Count -ne 406 -or @($report.ruleResults | Where-Object status -ne 'inconclusive').Count -ne 0) { $errors.Add('B report baseline differs.') }
$after = Get-ArchSiftHostHash
if ($before -cne $after) { $errors.Add('Host hash drift.') }
$result = [ordered]@{schemaVersion=1;kind='0.7-baseline';sourceCommit=$manifest.sourceCommit;baseCommit='4ca2a5fc485b28e3aaf4fadd2223bed378d56b03';payload=$payload;frozenFiles=$frozen;report=[ordered]@{execution=$report.execution;compliance=$report.compliance;coverage=$report.coverage;ruleCount=@($report.ruleResults).Count};hostBeforeSha256=$before;hostAfterSha256=$after;hostStateEqual=($before -ceq $after);errors=$errors.ToArray()}
$output = [IO.Path]::GetFullPath((Join-Path $EvidenceRoot ('0.7-baseline-' + [Guid]::NewGuid().ToString('N'))))
if ($output.StartsWith($repository + '\', [StringComparison]::OrdinalIgnoreCase) -or $output.StartsWith('D:\IFX-10-Root\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must be outside source and IFX installation.' }
[void][IO.Directory]::CreateDirectory($output)
$resultPath = Join-Path $output 'result.json'
[IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
[ordered]@{evidence=$resultPath;sha256=(Hash-File $resultPath);payloadCount=$payload.Count;frozenFileCount=$frozen.Count;hostStateEqual=$result.hostStateEqual;errors=$errors.ToArray()} | ConvertTo-Json -Depth 3
if ($errors.Count -gt 0) { throw 'Baseline failed; evidence preserved.' }
