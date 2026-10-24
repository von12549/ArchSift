# Release-harness tooling only. Dot-sourcing defines functions; it captures or approves nothing.
function Get-ArchSiftBaselineHash([string]$Text) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::Unicode.GetBytes($Text))).ToLowerInvariant()
}
function Get-ArchSiftBaselineFileHash([byte[]]$Bytes) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}
function Read-ArchSiftBaselineDocument([string]$Path) {
    $bytes=[IO.File]::ReadAllBytes($Path)
    @{value=([Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xfeff)|ConvertFrom-Json -AsHashtable);sha256=(Get-ArchSiftBaselineFileHash $bytes)}
}
function Write-ArchSiftNewBaselineDocument([string]$Path,$Value) {
    if(-not [IO.Path]::IsPathFullyQualified($Path)){throw 'Evidence output must be an explicit absolute path.'}
    $repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))+[IO.Path]::DirectorySeparatorChar
    if([IO.Path]::GetFullPath($Path).StartsWith($repo,[StringComparison]::OrdinalIgnoreCase)){throw 'Session evidence belongs outside the source repository.'}
    $parent=[IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path)))
    while($null -ne $parent){if(-not $parent.Exists -or ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Evidence parents must exist and must not be links/junctions.'};$parent=$parent.Parent}
    $bytes=[Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Value -Depth 40)+"`n")
    $stream=[IO.File]::Open($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    try{$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
}
function Get-ArchSiftPersistentBaselineState {
    $state=[ordered]@{}
    foreach($scope in @('User','Machine')) {
        $map=[ordered]@{};$items=[Environment]::GetEnvironmentVariables($scope)
        foreach($key in @($items.Keys)|Sort-Object){$map[$key]=[string]$items[$key]}
        $json=$map|ConvertTo-Json -Compress
        $state[$scope]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json))).ToLowerInvariant()
        $state["$scope/PATH"]=Get-ArchSiftBaselineHash ([string][Environment]::GetEnvironmentVariable('PATH',$scope))
    }
    foreach($p in @($PROFILE.AllUsersAllHosts,$PROFILE.AllUsersCurrentHost,$PROFILE.CurrentUserAllHosts,$PROFILE.CurrentUserCurrentHost)) {
        $state["profile/$p"]=if([IO.File]::Exists($p)){(Get-FileHash -LiteralPath $p).Hash.ToLowerInvariant()}else{'absent'}
    }
    return $state
}
function Get-ArchSiftSessionContextKeys {
    @('ownerPid','ownerStartedUtc','ownerExecutableSha256','ownerExecutablePathSha256','shellExecutableSha256','shellExecutablePathSha256','shellVersion','noProfile','workingDirectorySha256','launchPolicySha256')
}
function Test-ArchSiftSchemaOne($Value) { ($Value -is [int] -or $Value -is [long]) -and $Value -eq 1 }
function Assert-ArchSiftPersistentShape($State) {
    foreach($key in @('User','Machine','User/PATH','Machine/PATH')){if($State[$key] -isnot [string] -or $State[$key] -cnotmatch '^[a-f0-9]{64}$'){throw 'Invalid persisted baseline hash.'}}
    $profiles=@($State.Keys|Where-Object {$_ -clike 'profile/*'})
    if($profiles.Count -ne 4){throw 'Exactly four profile identities are required.'}
    foreach($key in $profiles){if($State[$key] -cne 'absent' -and $State[$key] -cnotmatch '^[a-f0-9]{64}$'){throw 'Invalid profile hash.'}}
}
function Get-ArchSiftPersistentDifference($Original,$Current) {
    Assert-ArchSiftPersistentShape $Original
    Assert-ArchSiftPersistentShape $Current
    $keys=@('User','Machine','User/PATH','Machine/PATH')+@($Original.Keys|Where-Object {$_ -clike 'profile/*'})
    foreach($key in $keys){if(-not $Current.Contains($key) -or $Current[$key] -cne $Original[$key]){return $true}}
    foreach($key in @($Current.Keys|Where-Object {$_ -clike 'profile/*'})){if(-not $Original.Contains($key)){return $true}}
    return $false
}
function Get-ArchSiftProcessStartTicks($Value) {
    if($Value -is [DateTimeOffset]){return $Value.UtcTicks}
    if($Value -is [DateTime]){if($Value.Kind -eq [DateTimeKind]::Unspecified){throw 'Process time requires a timezone.'};return $Value.ToUniversalTime().Ticks}
    if($Value -isnot [string] -or $Value -cnotmatch '^\d{4}-\d{2}-\d{2}T.+(?:Z|[+-]\d{2}:\d{2})$'){throw 'Process time must use an explicit ISO timezone.'}
    return [DateTimeOffset]::Parse($Value,[Globalization.CultureInfo]::InvariantCulture).UtcTicks
}
function Assert-ArchSiftSessionSnapshotShape($Snapshot) {
    if(-not(Test-ArchSiftSchemaOne $Snapshot.schemaVersion)){throw 'Unsupported session snapshot.'}
    Assert-ArchSiftPersistentShape $Snapshot.persistent
    $keys=@(Get-ArchSiftSessionContextKeys)
    if($Snapshot.context.Count -ne $keys.Count){throw 'Unexpected execution-context fields.'}
    foreach($key in $keys){if(-not $Snapshot.context.Contains($key)){throw 'Incomplete execution context.'}}
    foreach($key in @($keys|Where-Object {$_ -clike '*Sha256'})){if($Snapshot.context[$key] -cnotmatch '^[a-f0-9]{64}$'){throw 'Invalid execution-context hash.'}}
    foreach($identity in @(@{pid=$Snapshot.context.ownerPid;started=$Snapshot.context.ownerStartedUtc},@{pid=$Snapshot.operation.pid;started=$Snapshot.operation.startedUtc})) {
        if(($identity.pid -isnot [int] -and $identity.pid -isnot [long]) -or $identity.pid -le 0){throw 'Invalid process identity.'}
        [void](Get-ArchSiftProcessStartTicks $identity.started)
    }
    if($Snapshot.context.noProfile -isnot [bool] -or -not $Snapshot.context.noProfile -or [string]::IsNullOrWhiteSpace($Snapshot.context.shellVersion)){throw 'NoProfile and shell version are required.'}
    $path=$Snapshot.processPath
    if($path.sha256 -cnotmatch '^[a-f0-9]{64}$' -or ($path.characters -isnot [long] -and $path.characters -isnot [int]) -or $path.characters -lt 0 -or $path.entryCount -ne @($path.entrySha256).Count -or $path.entryCount -lt 1){throw 'Invalid exact PATH summary.'}
    foreach($sha in $path.entrySha256){if($sha -cnotmatch '^[a-f0-9]{64}$'){throw 'Invalid ordered PATH-entry hash.'}}
}
function Test-ArchSiftSameSessionContext($Before,$After) {
    foreach($key in (Get-ArchSiftSessionContextKeys)){if($key -ceq 'ownerStartedUtc'){if((Get-ArchSiftProcessStartTicks $Before[$key]) -ne (Get-ArchSiftProcessStartTicks $After[$key])){return $false}}elseif($Before[$key] -cne $After[$key]){return $false}}
    return $true
}
function Test-ArchSiftSameExactPath($Before,$After) {
    if($Before.sha256 -cne $After.sha256 -or $Before.characters -ne $After.characters -or $Before.entryCount -ne $After.entryCount){return $false}
    for($i=0;$i -lt $Before.entryCount;$i++){if($Before.entrySha256[$i] -cne $After.entrySha256[$i]){return $false}}
    return $true
}
function Compare-ArchSiftSessionBaseline {
    param($Original,$Snapshot,$Candidate,$Approval,[string]$OriginalFileSha256,[string]$ExpectedOriginalFileSha256,[string]$CandidateFileSha256)
    $result=@{status='invalid-input';baselineMatches=$false;operationAuthorized=$false;automaticRepair=$false}
    try {
        Assert-ArchSiftSessionSnapshotShape $Snapshot
        if($OriginalFileSha256 -cnotmatch '^[a-f0-9]{64}$' -or $OriginalFileSha256 -cne $ExpectedOriginalFileSha256){$result.status='persistent-baseline-file-changed';return $result}
        if(Get-ArchSiftPersistentDifference $Original $Snapshot.persistent){$result.status='persistent-drift';return $result}
        if($null -eq $Candidate -or $null -eq $Approval){$result.status='session-confirmation-required';return $result}
        Assert-ArchSiftSessionSnapshotShape $Candidate.snapshot
        $sessionId=[Guid]::Empty
        if(-not(Test-ArchSiftSchemaOne $Candidate.schemaVersion) -or $Candidate.kind -cne 'archsift-session-candidate' -or -not [Guid]::TryParse([string]$Candidate.sessionId,[ref]$sessionId) -or $sessionId -eq [Guid]::Empty -or $Candidate.persistentBaselineSha256 -cne $OriginalFileSha256 -or (Get-ArchSiftPersistentDifference $Original $Candidate.snapshot.persistent)){return $result}
        $result.status='invalid-confirmation'
        $confirmationId=[Guid]::Empty
        if(-not(Test-ArchSiftSchemaOne $Approval.schemaVersion) -or $Approval.kind -cne 'archsift-session-confirmation' -or $Approval.scope -cne 'baseline-only-no-operation-authorization' -or $Approval.sessionId -cne $Candidate.sessionId -or $CandidateFileSha256 -cnotmatch '^[a-f0-9]{64}$' -or $Approval.candidateSha256 -cne $CandidateFileSha256 -or [string]::IsNullOrWhiteSpace($Approval.confirmedBy) -or -not [Guid]::TryParse([string]$Approval.confirmationId,[ref]$confirmationId) -or $confirmationId -eq [Guid]::Empty -or (Get-ArchSiftProcessStartTicks $Approval.confirmedUtc) -le 0){ $result.status='invalid-confirmation';return $result }
        if(-not(Test-ArchSiftSameSessionContext $Candidate.snapshot.context $Snapshot.context)){$result.status='execution-context-changed';return $result}
        if(-not(Test-ArchSiftSameExactPath $Candidate.snapshot.processPath $Snapshot.processPath)){$result.status='process-path-drift';return $result}
        $result.status='matched';$result.baselineMatches=$true;return $result
    } catch {return $result}
}
function Compare-ArchSiftHostOperation($Before,$After) {
    $result=@{status='invalid-input';unchanged=$false;operationAuthorized=$false}
    try {
        Assert-ArchSiftSessionSnapshotShape $Before;Assert-ArchSiftSessionSnapshotShape $After
        if(Get-ArchSiftPersistentDifference $Before.persistent $After.persistent){$result.status='persistent-drift';return $result}
        if($Before.operation.pid -ne $After.operation.pid -or (Get-ArchSiftProcessStartTicks $Before.operation.startedUtc) -ne (Get-ArchSiftProcessStartTicks $After.operation.startedUtc) -or -not(Test-ArchSiftSameSessionContext $Before.context $After.context)){$result.status='operation-context-changed';return $result}
        if(-not(Test-ArchSiftSameExactPath $Before.processPath $After.processPath)){$result.status='process-path-drift';return $result}
        $result.status='matched';$result.unchanged=$true;return $result
    } catch {return $result}
}
function Test-ArchSiftNoProfileLaunch([string[]]$Arguments) {
    foreach($argument in @($Arguments|Select-Object -Skip 1)) {
        if($argument -ieq '-NoProfile'){return $true}
        if($argument -imatch '^-(?:NoLogo|NonInteractive|STA|MTA)$'){continue}
        return $false
    }
    return $false
}
function Get-ArchSiftSessionSnapshot([string]$LaunchPolicyPath) {
    $policy=Read-ArchSiftBaselineDocument $LaunchPolicyPath
    if(-not(Test-ArchSiftSchemaOne $policy.value.schemaVersion) -or [string]::IsNullOrWhiteSpace($policy.value.label) -or [string]::IsNullOrWhiteSpace($policy.value.isolationPolicy)){throw 'A reviewed, nonsecret launch-policy document is required.'}
    if(-not(Test-ArchSiftNoProfileLaunch ([Environment]::GetCommandLineArgs()))){throw 'Invoke PowerShell with explicit -NoProfile.'}
    if($IsWindows){$parentId=[int](Get-CimInstance Win32_Process -Filter "ProcessId=$PID").ParentProcessId}
    elseif($IsLinux){$line=@([IO.File]::ReadAllLines("/proc/$PID/status")|Where-Object {$_ -match '^PPid:'})[0];$parentId=[int]([regex]::Match($line,'\d+').Value)}
    else{throw 'Session identity capture supports Windows and Linux only; no silent fallback.'}
    $owner=[Diagnostics.Process]::GetProcessById($parentId);$self=[Diagnostics.Process]::GetCurrentProcess()
    try {
        $ownerPath=$owner.MainModule.FileName;$shellPath=$self.MainModule.FileName
        $context=[ordered]@{ownerPid=$parentId;ownerStartedUtc=$owner.StartTime.ToUniversalTime().ToString('O');ownerExecutableSha256=(Get-FileHash -LiteralPath $ownerPath).Hash.ToLowerInvariant();ownerExecutablePathSha256=(Get-ArchSiftBaselineHash $ownerPath);shellExecutableSha256=(Get-FileHash -LiteralPath $shellPath).Hash.ToLowerInvariant();shellExecutablePathSha256=(Get-ArchSiftBaselineHash $shellPath);shellVersion=$PSVersionTable.PSVersion.ToString();noProfile=$true;workingDirectorySha256=(Get-ArchSiftBaselineHash ([IO.Path]::GetFullPath((Get-Location).ProviderPath)));launchPolicySha256=$policy.sha256}
        $value=[string][Environment]::GetEnvironmentVariable('PATH','Process');$entries=$value.Split([IO.Path]::PathSeparator)
        $snapshot=@{schemaVersion=1;persistent=(Get-ArchSiftPersistentBaselineState);context=$context;operation=@{pid=$PID;startedUtc=$self.StartTime.ToUniversalTime().ToString('O')};processPath=@{sha256=(Get-ArchSiftBaselineHash $value);characters=$value.Length;entryCount=$entries.Count;entrySha256=@($entries|ForEach-Object {Get-ArchSiftBaselineHash $_})}}
        Assert-ArchSiftSessionSnapshotShape $snapshot
        return $snapshot
    } finally {$owner.Dispose();$self.Dispose()}
}
