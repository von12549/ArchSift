[CmdletBinding()]
param([switch]$ReadRepositorySwitch)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted' -or
   $env:GITHUB_REPOSITORY -cne 'von12549/ArchSift' -or $env:GITHUB_RUN_ATTEMPT -cne '1' -or
   $env:GITHUB_RUN_ID -cnotmatch '^\d+$' -or $env:GITHUB_SHA -cnotmatch '^[a-f0-9]{40}$' -or
   ($env:GITHUB_EVENT_NAME -cne 'pull_request' -and $env:GITHUB_REF -cne 'refs/heads/main')){throw 'NR06 requires its approved hosted repository/context; no local SDK entry.'}
if($env:NUGET_API_KEY -or $env:ACTIONS_ID_TOKEN_REQUEST_TOKEN -or $env:ACTIONS_ID_TOKEN_REQUEST_URL){throw 'NR06 must not receive publishing credentials or OIDC capability.'}
if(-not $env:RUNNER_TEMP -or -not $env:GITHUB_WORKSPACE){throw 'Explicit hosted roots required.'}
$root=[IO.Path]::GetFullPath($env:RUNNER_TEMP)
$source=[IO.Path]::GetFullPath($env:GITHUB_WORKSPACE)
if($root -eq $source -or $root.StartsWith($source+[IO.Path]::DirectorySeparatorChar) -or $source.StartsWith($root+[IO.Path]::DirectorySeparatorChar)){throw 'Runner evidence/source roots overlap.'}
if($ReadRepositorySwitch){
 # The workflow binds this directly from GitHub's repository-variable context.
 # No extra token permission or remote settings mutation is required.
 if($env:NR06_RELEASE_SWITCH){throw 'Release switch must remain absent; no repair performed.'}
}
Write-Output 'NR06 hosted/credential boundaries verified; no publishing operation.'
