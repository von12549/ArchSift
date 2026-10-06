[CmdletBinding()]
param()
& (Join-Path $PSScriptRoot 'Invoke-DevelopmentChecks.ps1')
