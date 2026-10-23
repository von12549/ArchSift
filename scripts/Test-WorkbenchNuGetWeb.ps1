[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputRoot,[Parameter(Mandatory)][string]$OfflineFeed,
    [Parameter(Mandatory)][string]$Dotnet,[string]$ExpectedHostHash='')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
. (Join-Path $PSScriptRoot 'HostState.ps1')
$release=Get-ArchSiftReleaseInputs $repo
$root=Assert-ArchSiftExternalPath $repo $OutputRoot
$feed=[IO.Path]::GetFullPath($OfflineFeed)
Assert-ArchSiftReleaseNoLinks $feed
$dotnetPath=[IO.Path]::GetFullPath($Dotnet)
if(-not(Test-Path $dotnetPath -PathType Leaf) -or -not(Test-Path $feed -PathType Container)){throw 'Explicit dotnet host and offline feed are required.'}
if(Test-Path $root){throw 'Keep prior cold-tool attempts; select a fresh output root.'}
$before=Get-ArchSiftHostHash
if($ExpectedHostHash -and $before -cne $ExpectedHostHash){throw 'Host differs; stop without repair.'}
[void][IO.Directory]::CreateDirectory($root)
$checks=[Collections.Generic.List[object]]::new()
$tokens=[Collections.Generic.List[string]]::new()
function Save([string]$Path,$Value){[IO.File]::WriteAllText($Path,(($Value|ConvertTo-Json -Depth 18)+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))}
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant()}
function Check-Host {
    if((Get-ArchSiftHostHash) -cne $before){throw 'Host drift; retain evidence and stop without repair.'}
}
function Start-Info([string]$File,[string[]]$Arguments,[string]$ModeRoot){
    $start=[Diagnostics.ProcessStartInfo]::new($File)
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$ModeRoot
    $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.StandardOutputEncoding=[Text.Encoding]::UTF8;$start.StandardErrorEncoding=[Text.Encoding]::UTF8
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $ModeRoot 'home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $start.Environment['DOTNET_ROOT']=[IO.Path]::GetDirectoryName($dotnetPath)
    $start.Environment['DOTNET_HOST_PATH']=$dotnetPath
    $start.Environment['PATH']=[IO.Path]::GetDirectoryName($dotnetPath)+[IO.Path]::PathSeparator+$start.Environment['PATH']
    $start.Environment['NUGET_PACKAGES']=Join-Path $ModeRoot 'cache'
    $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
    $start.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE']='false'
    $start.Environment['DOTNET_NOLOGO']='1'
    return $start
}
function Run([string]$Name,[string]$File,[string[]]$Arguments,[string]$ModeRoot){
    $process=[Diagnostics.Process]::Start((Start-Info $File $Arguments $ModeRoot))
    try{
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not$process.WaitForExit(120000)){$process.Kill($true);$process.WaitForExit();throw 'Cold-tool command timed out.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $root ($Name+'.stdout.log')),$stdout.Result)
        [IO.File]::WriteAllText((Join-Path $root ($Name+'.stderr.log')),$stderr.Result)
        Check-Host
        $checks.Add(@{name=$Name;arguments=$Arguments;exitCode=$process.ExitCode})
        if($process.ExitCode -ne 0){throw ('Cold-tool command failed: '+$Name)}
        return $stdout.Result
    }finally{$process.Dispose()}
}
function Open-Session([string]$File,[string[]]$Arguments,[string]$ModeRoot){
    $process=[Diagnostics.Process]::Start((Start-Info $File $Arguments $ModeRoot))
    $stderr=$process.StandardError.ReadToEndAsync()
    try{
        $address=$null;$ready=$false;$childPid=$null
        $deadline=[DateTime]::UtcNow.AddSeconds(45)
        while(-not$ready){
            $remaining=$deadline-[DateTime]::UtcNow
            if($remaining.TotalMilliseconds -le 0){throw 'Cold-tool UI readiness timed out.'}
            $line=$process.StandardOutput.ReadLineAsync().WaitAsync($remaining).GetAwaiter().GetResult()
            if($null -eq $line){throw 'Cold-tool UI exited before readiness; inspect the retained failure.'}
            if($line.StartsWith('ARCHSIFT_UI=')){$address=[uri]$line.Substring(12)}
            if($line.StartsWith('ARCHSIFT_PID=')){$childPid=[int]$line.Substring(13)}
            if($line.StartsWith('ARCHSIFT_STATE=waiting')){$ready=$true}
        }
        if($null -eq $address -or $address.Scheme -cne 'http' -or $address.Host -cne '127.0.0.1' -or $null -eq $childPid){throw 'Unexpected UI readiness protocol.'}
        $token=$address.Fragment.Substring(9);$tokens.Add($token)
        $client=[Net.Http.HttpClient]::new()
        $client.BaseAddress=[uri]$address.GetLeftPart([UriPartial]::Authority)
        $client.Timeout=[TimeSpan]::FromSeconds(30)
        $client.DefaultRequestHeaders.Add('X-ArchSift-Token',$token)
        return @{process=$process;stderr=$stderr;stdout=$process.StandardOutput.ReadToEndAsync();client=$client;port=$address.Port;childPid=$childPid}
    }catch{
        if(-not$process.HasExited){$process.Kill($true);$process.WaitForExit()}
        # Readiness URLs/tokens are never persisted.
        $errorText=$stderr.GetAwaiter().GetResult()
        foreach($token in $tokens){$errorText=$errorText.Replace($token,'[redacted]')}
        [IO.File]::WriteAllText((Join-Path $root 'startup-failure.stderr.log'),$errorText)
        $process.Dispose();throw
    }
}
function Request($Session,[string]$Path,$Body=$null,[int]$ExpectedStatus=200){
    if($null -eq $Body){$response=$Session.client.GetAsync($Path).GetAwaiter().GetResult()}
    else{$content=[Net.Http.StringContent]::new(($Body|ConvertTo-Json -Depth 12 -Compress),[Text.Encoding]::UTF8,'application/json');try{$response=$Session.client.PostAsync($Path,$content).GetAwaiter().GetResult()}finally{$content.Dispose()}}
    try{
        $text=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if([int]$response.StatusCode -ne $ExpectedStatus){throw ('Unexpected HTTP status for '+$Path+': '+[int]$response.StatusCode)}
        if($text){return ($text|ConvertFrom-Json)}
    }finally{$response.Dispose()}
}
function Finish($Session,[bool]$Killed=$false){
    try{
        if(-not$Session.process.WaitForExit(20000)){throw 'Owned CLI did not close.'}
        if(-not$Killed -and $Session.process.ExitCode -ne 0){throw 'Owned CLI did not exit successfully.'}
        [void]$Session.stdout.GetAwaiter().GetResult();[void]$Session.stderr.GetAwaiter().GetResult()
        if(@([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()|Where-Object Port -eq $Session.port).Count){throw 'Owned listener remains.'}
        try{$child=[Diagnostics.Process]::GetProcessById($Session.childPid);try{if(-not$child.WaitForExit(15000)){throw 'Owned Web remains.'}}finally{$child.Dispose()}}catch [ArgumentException]{}
        Check-Host
    }finally{
        if(-not$Session.process.HasExited){$Session.process.Kill($true);$Session.process.WaitForExit()}
        $Session.client.Dispose();$Session.process.Dispose()
    }
}
$passed=$false
try{
    foreach($mode in @('local-restore','tool-path')){
        $modeRoot=Join-Path $root ($mode+' spaces')
        [void][IO.Directory]::CreateDirectory($modeRoot)
        $config=Join-Path $modeRoot 'NuGet.Config'
        [IO.File]::WriteAllText($config,('<configuration><packageSources><clear/><add key="approved" value="'+[Security.SecurityElement]::Escape($feed)+'"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>'))
        if($mode -ceq 'local-restore'){
            $manifest=Join-Path $modeRoot '.config/dotnet-tools.json'
            [void][IO.Directory]::CreateDirectory((Split-Path $manifest))
            Save $manifest @{version=1;isRoot=$true;tools=@{'archsift.cli'=@{version=$release.version;commands=@('archsift');rollForward=$false}}}
            [void](Run ($mode+'-restore') $dotnetPath @('tool','restore','--tool-manifest',$manifest,'--configfile',$config,'--add-source',$feed,'--no-cache') $modeRoot)
            $file=$dotnetPath;$prefix=@('tool','run','archsift','--')
        }else{
            $tools=Join-Path $modeRoot 'tools'
            [void](Run ($mode+'-install') $dotnetPath @('tool','install','ArchSift.Cli','--version',$release.version,'--tool-path',$tools,'--configfile',$config,'--add-source',$feed,'--no-cache') $modeRoot)
            $file=Join-Path $tools $(if($IsWindows){'archsift.exe'}else{'archsift'});$prefix=@()
        }
        $actual=(Run ($mode+'-version') $file ($prefix+@('--version')) $modeRoot).Trim()
        if($actual -cne ('archsift '+$release.version)){throw 'Installed tool version differs.'}
        $target=Join-Path $modeRoot 'target';[void][IO.Directory]::CreateDirectory($target)
        $project=Join-Path $target 'Fixture.csproj'
        [IO.File]::WriteAllText($project,'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
        $targetHash=Hash $project
        $userConfig=Join-Path $modeRoot 'external-config.json'
        Save $userConfig @{schemaVersion=1;target=@{root=$target;entry='Fixture.csproj'};build=@{mode='existing';configuration='Release';targetFramework='net10.0'};output=@{directory=(Join-Path $modeRoot 'reports');formats=@('json','html')};rulesDirectory=(Join-Path $modeRoot 'rules');rulesets=@()}
        $configHash=Hash $userConfig
        $ui=Open-Session $file ($prefix+@('ui','--config',$userConfig)) $modeRoot
        try{
            $view=Request $ui '/api/config'
            [void](Request $ui '/api/shutdown' @{} 202)
        }finally{Finish $ui}
        $stateRoot=Join-Path $modeRoot 'portable-state'
        $launcher=Open-Session $file ($prefix+@('launch','--root',$stateRoot,'--config',$userConfig,'--no-browser')) $modeRoot
        try{
            $state=Request $launcher '/api/launcher/state'
            if($state.mode -cne 'portable' -or $state.toolVersion -cne $release.version -or $state.profiles.Count -ne 1){throw 'Portable Launcher identity/profile differs.'}
            $health=$state.profiles[0]
            if($health.session.configProtected -or $health.session.libraryProtected){throw 'Portable config incorrectly claims Setup protection.'}
            $started=Request $launcher '/api/launcher/start' @{profileId=$health.entry.profileId;catalogSha256=$state.catalogSha256;configSha256=$health.sha256;selectionSha256=$state.selectionSha256}
            $childAddress=[uri]$started.workbenchUrl;$tokens.Add($childAddress.Fragment.Substring(9))
            $childClient=[Net.Http.HttpClient]::new();$childClient.BaseAddress=[uri]$childAddress.GetLeftPart([UriPartial]::Authority);$childClient.DefaultRequestHeaders.Add('X-ArchSift-Token',$childAddress.Fragment.Substring(9))
            try{
                $child=@{client=$childClient};[void](Request $child '/api/config')
                [void](Request $child '/api/session/dirty' @{dirty=$true})
                [void](Request $launcher '/api/launcher/stop' @{discardChanges=$false} 400)
                [void](Request $child '/api/session')
                [void](Request $launcher '/api/launcher/stop' @{discardChanges=$true})
            }finally{$childClient.Dispose()}
            [void](Request $launcher '/api/launcher/shutdown' @{} 202)
        }finally{Finish $launcher}
        if(Test-Path (Join-Path $stateRoot 'install.json')){throw 'Portable state counterfeited managed ownership.'}
        foreach($json in Get-ChildItem $stateRoot -Recurse -File -Filter '*.json'){
            $bytes=[IO.File]::ReadAllText($json.FullName);foreach($token in $tokens){if($bytes.Contains($token)){throw 'Session token persisted into portable state.'}}
        }
        if((Hash $project)-cne$targetHash -or (Hash $userConfig)-cne$configHash){throw 'Cold Web operation changed input bytes.'}
        $checks.Add(@{name=$mode+'-ui-and-portable-launcher';status='passed';version=$release.version;directUi=$true;portableLauncher=$true;dirtyRejection=$true;ownedShutdown=$true;targetConfigUnchanged=$true;separateWebSourceOrSetup=$false})
    }
    $passed=$true
}finally{
    $equal=(Get-ArchSiftHostHash)-ceq$before
    Save (Join-Path $root 'qualification.json') @{schemaVersion=1;status=$(if($passed -and $equal){'passed'}else{'failed'});version=$release.version;checks=$checks.ToArray();hostEqual=$equal;humanAcceptance='pending-user-only';limitations=@('Synthetic target; no target build or entry point executed.','Negative/runtime/real IFX/lifecycle rollback and exact stable gates remain separate.')}
    Write-Output ('cold-tool-web passed='+$passed+' host-equal='+$equal)
    if(-not$equal){throw 'Host drift; retain evidence and stop without repair.'}
}
