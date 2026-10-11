# Operator-only IFX block 02. Do not run this against the original installation.
[CmdletBinding()]
param(
    [string]$ReviewedPlanRoot='D:\ArchSift-lab\evidence\ifx-0.6-plan-dd1f086cc3ca45018657df9192d71c0a',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$NewInstallRoot='D:\IFX-10-Root\ArchSift-0.6-review',
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
$NewInstallRoot=Safe-Path $NewInstallRoot;$LabRoot=Safe-Path $LabRoot;$ReviewedPlanRoot=Safe-Path $ReviewedPlanRoot
$sourceRoot=Safe-Path (Join-Path $PSScriptRoot '../..')
foreach($protected in @($sourceRoot,$TargetRoot,$ExistingInstallRoot)){
    if($NewInstallRoot-eq$protected-or$NewInstallRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'New installation/source roots overlap.'}
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/source roots overlap.'}
}
if($NewInstallRoot-eq$LabRoot-or$NewInstallRoot.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$LabRoot.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'New installation/evidence roots overlap.'}
if(-not$ReviewedPlanRoot.StartsWith((Join-Path $LabRoot 'evidence')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Reviewed plan is outside the evidence root.'}
function Original-State {
    $selection=Get-Content -LiteralPath (Safe-Path (Join-Path $ExistingInstallRoot 'install.json')) -Raw|ConvertFrom-Json
    if($selection.root-cne$ExistingInstallRoot-or$selection.selectedVersion-cne'0.5.0'){throw 'Original installation selection changed.'}
    $chosen=@($selection.versions|Where-Object version -eq '0.5.0')
    if($chosen.Count-ne1-or$chosen[0].manifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'){throw 'Original manifest identity changed.'}
    $records=[Collections.Generic.List[object]]::new()
    function Walk-Original([string]$Directory){
        foreach($path in [IO.Directory]::EnumerateFileSystemEntries($Directory)){
            [void](Safe-Path $path)
            if([IO.Directory]::Exists($path)){Walk-Original $path}else{
                $records.Add(@{path=[IO.Path]::GetRelativePath($ExistingInstallRoot,$path);sha256=(Hash-File $path)})
                if($records.Count-gt10000){throw 'Original installation file-count limit.'}
            }
        }
    }
    Walk-Original $ExistingInstallRoot
    return @{selectedVersion=$selection.selectedVersion;configPath=$selection.configPath;filesSha256=(Hash-Json @($records|Sort-Object path));fileCount=$records.Count}
}
function Child-Run([string]$Executable,[string[]]$Arguments,[string]$Prefix,[int]$TimeoutMs=180000){
    $start=[Diagnostics.ProcessStartInfo]::new((Safe-Path $Executable));$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(-not$process.WaitForExit($TimeoutMs)){$process.Kill($true);$process.WaitForExit();throw "$Prefix timed out; only its owned process tree was stopped."}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run "$Prefix.stdout.json"),$stdout.Result,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $run "$Prefix.stderr.log"),$stderr.Result,[Text.UTF8Encoding]::new($false))
        if($process.ExitCode-ne0){throw "$Prefix failed with exit code $($process.ExitCode); preserve its logs."}
        return $stdout.Result|ConvertFrom-Json
    }finally{$process.Dispose()}
}
function Api([string]$Address,[string]$Route,[string]$Method='GET',$Body=$null){
    $uri=[Uri]$Address
    if($uri.Scheme-cne'http'-or$uri.Host-cne'127.0.0.1'-or$uri.Fragment-notmatch '^#session=[a-f0-9]{64}$'){throw 'Unexpected private loopback address.'}
    $base=$uri.GetLeftPart([UriPartial]::Authority)
    $client=[Net.Http.HttpClient]::new();$client.Timeout=[TimeSpan]::FromSeconds(15)
    $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method),$base+$Route)
    try{
        $request.Headers.Add('X-ArchSift-Token',$uri.Fragment.Substring(9))
        if($Method-ceq'POST'){$request.Content=[Net.Http.StringContent]::new((ConvertTo-Json -InputObject $Body -Depth 16 -Compress),[Text.Encoding]::UTF8,'application/json')}
        $response=$client.SendAsync($request).GetAwaiter().GetResult()
        try{
            $bodyText=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if(-not$response.IsSuccessStatusCode){throw "Private API $Route returned $([int]$response.StatusCode)."}
            if([string]::IsNullOrWhiteSpace($bodyText)){return $null}
            return $bodyText|ConvertFrom-Json
        }finally{$response.Dispose()}
    }finally{$request.Dispose();$client.Dispose()}
}
function No-Listener([int]$ProcessId){
    $live=Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if($live){$live.Dispose();return $false}
    return @((Get-NetTCPConnection -State Listen -OwningProcess $ProcessId -ErrorAction SilentlyContinue)).Count-eq0
}
function Stop-Workbench([string]$Address,[int]$ProcessId){
    [void](Api $Address '/api/launcher/stop' 'POST' @{discardChanges=$false})
    $deadline=[DateTime]::UtcNow.AddSeconds(20)
    do{
        $session=Api $Address '/api/launcher/session'
        if(-not$session.busy-and-not$session.workbenchUrl){break}
        Start-Sleep -Milliseconds 200
    }while([DateTime]::UtcNow-lt$deadline)
    if($session.busy-or$session.workbenchUrl-or-not(No-Listener $ProcessId)){throw 'Owned workbench PID/listener did not stop safely.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$originalBefore=$null;$launcher=$null;$launcherAddress=$null;$complete=$false
$run=Join-Path $LabRoot ('evidence/ifx-0.6-install-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($run)
try{
    $prior=Get-Content -LiteralPath (Safe-Path (Join-Path $ReviewedPlanRoot 'result.json')) -Raw|ConvertFrom-Json
    $before=Get-Content -LiteralPath (Safe-Path (Join-Path $ReviewedPlanRoot 'before.json')) -Raw|ConvertFrom-Json
    $planPath=Safe-Path (Join-Path $ReviewedPlanRoot 'plan.json')
    $plan=Get-Content -LiteralPath $planPath -Raw|ConvertFrom-Json
    $audit=Get-Content -LiteralPath (Safe-Path (Join-Path $ReviewedPlanRoot 'audit-errors.json')) -Raw|ConvertFrom-Json
    if($prior.status-cne'plan-ready-for-review'-or$prior.applied-or$prior.analysisOrBuildPerformed-or-not$prior.hostStateEqual-or-not$prior.targetStateEqual-or-not$prior.originalInstallationStateEqual-or@($audit).Count-ne0){throw 'Reviewed plan audit is not clean.'}
    if($plan.schemaVersion-ne1-or$plan.planId-cne'9cd21e4fa820b5d262cbf6571f169764647cd7c2f0399266ab3440317a481a89'-or$plan.operation-cne'install'-or$plan.root-cne$NewInstallRoot-or$plan.configPath-cne(Join-Path $NewInstallRoot 'config/default.json')-or$plan.config.target.root-cne$TargetRoot-or$plan.config.target.entry-cne'IFX.sln'-or$plan.config.build.mode-cne'existing'-or$plan.config.build.allowNetwork-or$plan.config.rulesets.Count-ne0-or-not$plan.smoke){throw 'Plan differs from reviewed scope.'}
    if([DateTimeOffset]::Parse($plan.expiresUtc)-lt[DateTimeOffset]::UtcNow){throw 'Reviewed plan expired; create and review a new plan.'}
    if($plan.package.sha256-cne'9f311ced6d1610761d6f00ef6f380a608d14859b1adc809b7031c42e029661fb'-or$plan.package.sourceCommit-cne'a05c44ea5a1e544f7d3e4ffb5d4a411291180a31'-or$plan.package.version-cne'0.6.0'-or(Hash-File $plan.package.path)-cne$plan.package.sha256){throw 'Downloaded package identity changed.'}
    if(Test-Path -LiteralPath $NewInstallRoot){throw 'Fresh installation root already exists; preserve it for review.'}
    foreach($owned in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        try{$image=$owned.Path;if(-not$image){throw 'Cannot inspect live ArchSift process.'};if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original installation has an active CLI/UI.'}}finally{$owned.Dispose()}
    }
    $targetBefore=Target-State;$originalBefore=Original-State
    if($targetBefore.head-cne$before.target.head-or$targetBefore.ordinaryStatusSha256-cne$before.target.ordinaryStatusSha256-or$targetBefore.filesSha256-cne$before.target.filesSha256-or$targetBefore.fileCount-ne$before.target.fileCount-or
        $originalBefore.selectedVersion-cne$before.original.selectedVersion-or$originalBefore.configPath-cne$before.original.configPath-or$originalBefore.filesSha256-cne$before.original.filesSha256-or$originalBefore.fileCount-ne$before.original.fileCount-or
        $hostBefore-cne$before.hostSha256){throw 'State changed since reviewed plan.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;reviewedPlanId=$plan.planId}|ConvertTo-Json -Depth 10))
    $setup=Safe-Path (Join-Path ([IO.Path]::GetDirectoryName($plan.package.path)) 'ArchSift.Setup.exe')
    if((Hash-File $setup)-cne'49fd9f6072c3f4a945bdaef6bf0ab6d940cd8a9e6d7ab9fce33778759cb62b9c'){throw 'Reviewed Setup identity changed.'}
    $receipt=Child-Run $setup @('apply','--plan',$planPath,'--plan-id',$plan.planId) 'apply'
    if($receipt.operation-cne'install'-or$receipt.planId-cne$plan.planId-or$receipt.outcome-cne'success'-or-not$receipt.hostStateEqual-or$receipt.after.root-cne$NewInstallRoot-or$receipt.after.selectedVersion-cne'0.6.0'){throw 'Setup apply receipt differs from reviewed install.'}
    $inspect=Child-Run $setup @('inspect','--root',$NewInstallRoot) 'inspect'
    if($inspect.status-cne'verified'-or$inspect.installation.selectedVersion-cne'0.6.0'-or$inspect.installation.configPath-cne$plan.configPath-or$inspect.installation.targetRoot-cne$TargetRoot){throw 'New installation inspect failed.'}
    $web=Safe-Path (Join-Path $NewInstallRoot 'versions/0.6.0/web/ArchSift.Web.exe')
    $start=[Diagnostics.ProcessStartInfo]::new($web);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'launcher-cli-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $start.Environment['ARCHSIFT_UI_PARENT_CONTROL']='stdin-v1'
    $start.ArgumentList.Add('--launcher');$start.ArgumentList.Add($NewInstallRoot)
    $launcher=[Diagnostics.Process]::Start($start)
    $launcherError=$launcher.StandardError.ReadToEndAsync()
    $deadline=[DateTime]::UtcNow.AddSeconds(30)
    while(-not$launcherAddress-and[DateTime]::UtcNow-lt$deadline){
        $line=$launcher.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(30)).GetAwaiter().GetResult()
        if($null-eq$line){break}
        if($line.StartsWith('ARCHSIFT_UI=',[StringComparison]::Ordinal)){$launcherAddress=$line.Substring(12)}
    }
    if(-not$launcherAddress){throw 'Native launcher exited before private readiness.'}
    $state=Api $launcherAddress '/api/launcher/state'
    if($state.PSObject.Properties.Name-contains'error'-or$state.toolVersion-cne'0.6.0'-or$state.profiles.Count-ne0-or$state.managedCandidate.status-cne'valid'){throw 'Fresh launcher initial state differs.'}
    $managed=Api $launcherAddress '/api/launcher/register' 'POST' @{name='default.json';configPath=$plan.configPath;catalogSha256=$state.catalogSha256}
    $external=Safe-Path (Join-Path $NewInstallRoot 'config/external-copy.json')
    if(Test-Path -LiteralPath $external){throw 'External profile destination already exists.'}
    Copy-Item -LiteralPath $plan.configPath -Destination $external
    $state=Api $launcherAddress '/api/launcher/state'
    $other=Api $launcherAddress '/api/launcher/register' 'POST' @{name='default.json';configPath=$external;catalogSha256=$state.catalogSha256}
    $state=Api $launcherAddress '/api/launcher/state'
    if($state.profiles.Count-ne2-or$managed.profileId-ceq$other.profileId){throw 'Two independent profile registrations were not retained.'}
    $profileResults=@()
    foreach($entry in @(@{id=$managed.profileId;path=$plan.configPath;protected=$true},@{id=$other.profileId;path=$external;protected=$false})){
        $health=@($state.profiles|Where-Object {$_.entry.profileId-ceq$entry.id})
        if($health.Count-ne1-or$health[0].status-cne'valid'-or$health[0].session.configProtected-ne$entry.protected){throw 'Profile health/protection label differs.'}
        $started=Api $launcherAddress '/api/launcher/start' 'POST' @{profileId=$entry.id;catalogSha256=$state.catalogSha256;configSha256=$health[0].sha256;selectionSha256=$state.selectionSha256}
        $workbench=Api $started.workbenchUrl '/api/config'
        $session=Api $started.workbenchUrl '/api/session'
        if($workbench.toolVersion-cne'0.6.0'-or$workbench.profile.configPath-cne$entry.path-or$workbench.profile.configProtected-ne$entry.protected-or$session.running-or$session.dirty){throw 'Workbench profile identity or idle state differs.'}
        $profileResults+=@{configPath=$entry.path;configProtected=$entry.protected;libraryProtected=$workbench.profile.libraryProtected;processId=$session.processId;running=$session.running}
        Stop-Workbench $launcherAddress ([int]$session.processId)
        $state=Api $launcherAddress '/api/launcher/state'
    }
    [void](Api $launcherAddress '/api/launcher/shutdown' 'POST' @{})
    if(-not$launcher.WaitForExit(20000)){throw 'Owned launcher did not exit after safe shutdown.'}
    if(-not(No-Listener $launcher.Id)){throw 'Owned launcher PID/listener remains.'}
    $launcherAddress=$null
    $post=Child-Run $setup @('inspect','--root',$NewInstallRoot) 'post-inspect'
    if($post.status-cne'verified'-or$post.installation.selectedVersion-cne'0.6.0'){throw 'Post-launch installation identity changed.'}
    [IO.File]::WriteAllText((Join-Path $run 'profile-results.json'),(@{status='pass';profiles=$profileResults;managedCandidate=$plan.configPath;newInstallRoot=$NewInstallRoot;analysisOrBuildPerformed=$false}|ConvertTo-Json -Depth 8))
    $complete=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message}|ConvertTo-Json));throw}
finally{
    $launcherStopped=$true
    if($null-ne$launcher){
        if(-not$launcher.HasExited){
            try{$launcher.StandardInput.WriteLine('shutdown');$launcher.StandardInput.Flush();$launcher.StandardInput.Close()}catch{}
            if(-not$launcher.WaitForExit(12000)){$launcherStopped=$false}
        }
        if($launcherStopped){$launcherStopped=No-Listener $launcher.Id}
        $launcher.Dispose()
    }
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$auditErrors=@()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=(Hash-Json (Original-State))-ceq(Hash-Json $originalBefore)}catch{$originalEqual=$false;$auditErrors+=$_.Exception.Message}}
    $result=@{status=$(if($complete-and$launcherStopped-and$hostEqual-and$targetEqual-and$originalEqual){'install-ready-for-review'}else{'stop'});runRoot=$run;newInstallRoot=$NewInstallRoot;reviewedPlanId='9cd21e4fa820b5d262cbf6571f169764647cd7c2f0399266ab3440317a481a89';analysisOrBuildPerformed=$false;originalInstallationStateEqual=$originalEqual;targetStateEqual=$targetEqual;hostStateEqual=$hostEqual;ownedLauncherStopped=$launcherStopped}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors));$result|ConvertTo-Json -Depth 6
    if(-not$launcherStopped-or-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false){throw 'Safety or owned-process audit failed; preserve evidence without repair.'}
}
