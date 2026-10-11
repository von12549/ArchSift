param(
    [string]$PayloadRoot='D:\ArchSift-lab\packages\release-6b1174cde4544a699d5f689944694763\payload',
    [string]$ConfigPath='D:\ArchSift-lab\runs\ifx-external-0.7-6fb8ea0cfb1f4608920d57d4563b6ea4\config-unbound.json',
    [string]$WebExecutable
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
$qaRoot='D:\ArchSift-lab\runs\ifx-browser-0.7-'+[Guid]::NewGuid().ToString('N')
$qaBefore=Get-ArchSiftHostHash
$preflight=Get-Content -Raw 'D:\ArchSift-lab\evidence\ifx-0.7-preflight-527f4137255440f3b682467a9fbbf0ec\result.json' | ConvertFrom-Json
function CheckOriginal {
    foreach($file in $preflight.original.frozenFiles) {
        if((Get-FileHash -LiteralPath $file.path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw 'Original IFX state drift.' }
    }
    if((& git -C $preflight.target.root status --porcelain) -or (& git -C $preflight.target.root rev-parse HEAD).Trim() -ne $preflight.target.commit) { throw 'Original target drift.' }
}
CheckOriginal
$arguments=@((Join-Path $PSScriptRoot '0.7-ifx-browser.mjs'),$PayloadRoot,$ConfigPath,$qaRoot,'C:\Users\von12\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules')
if($WebExecutable) { $arguments+=$WebExecutable }
& 'C:\Program Files\nodejs\node.exe' @arguments
$qaExit=$LASTEXITCODE
$qaAfter=Get-ArchSiftHostHash
CheckOriginal
[IO.File]::WriteAllText((Join-Path $qaRoot 'host-state.json'),([ordered]@{beforeSha256=$qaBefore;afterSha256=$qaAfter;equal=($qaBefore -ceq $qaAfter);exitCode=$qaExit;originalUnchanged=$true;developmentOverride=[bool]$WebExecutable}|ConvertTo-Json))
Write-Output "qa-root=$qaRoot host-state-equal=$($qaBefore -ceq $qaAfter)"
if($qaBefore -cne $qaAfter){throw 'Host drift; preserve evidence.'}
exit $qaExit
