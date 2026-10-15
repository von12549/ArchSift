param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$tool = Join-Path $root 'runtime/archsift-inproject.exe'
if (-not [IO.File]::Exists($tool)) { throw 'Locked InProject runtime is missing.' }
& $tool @Arguments
exit $LASTEXITCODE
