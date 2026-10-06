[CmdletBinding()]
param()
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bad=@()
foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'src') -Recurse -File -Filter '*.cs' | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'} ) {
    $text=Get-Content -LiteralPath $file.FullName -Raw
    if($text -match 'SetEnvironmentVariable\s*\(' -or $text -match '(?i)\bsetx(?:\.exe)?\b' -or $text -match 'Registry(?:Key)?\s*\.\s*(?:SetValue|CreateSubKey)'){$bad+=$file.FullName}
    if($text -match 'DOTNET_CLI_HOME' -and $text -notmatch 'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'){$bad+=$file.FullName}
}
if($bad.Count){throw ('Forbidden persistent mutation/unpaired dotnet home: '+($bad -join ','))}
& (Join-Path $PSScriptRoot 'Invoke-DevelopmentChecks.ps1')
