[CmdletBinding()]
param([string] $LocalFeed = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'))

& (Join-Path $PSScriptRoot 'Invoke-DevelopmentChecks.ps1') -LocalFeed $LocalFeed -VerifyContracts
