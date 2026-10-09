# Operator-only 0.4-F step 4. The operator performs the browser review and safe shutdown.
[CmdletBinding()]
param(
    [switch]$PreflightOnly,
    [string]$ReplacementPackageRoot,
    [string]$ReplacementManifestSha256,
    [string]$ReplacementSourceCommit
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$replacementValues=@($ReplacementPackageRoot,$ReplacementManifestSha256,$ReplacementSourceCommit)
if(@($replacementValues|Where-Object { -not[string]::IsNullOrWhiteSpace($_) }).Count -notin @(0,3)){throw 'Replacement package root, manifest SHA-256 and source commit must be supplied together.'}
. (Join-Path $PSScriptRoot 'Invoke-0.4IfxCandidateStep.ps1') -ValidateOnly | Out-Null
if($ReplacementPackageRoot){
    if($ReplacementManifestSha256-notmatch'^[0-9a-f]{64}$'-or$ReplacementSourceCommit-notmatch'^[0-9a-f]{40}$'){throw 'Replacement manifest hash or source commit is invalid.'}
    $package=[IO.Path]::GetFullPath($ReplacementPackageRoot)
    if(-not(Is-Under $package $lab)-or(Is-Under $package $target)){throw 'Replacement package must be inside the lab and outside IFX.'}
    No-Links $package
    $manifestPath=Join-Path $package 'package-manifest.json'
    if((Sha $manifestPath)-cne$ReplacementManifestSha256){throw 'Replacement package manifest hash changed.'}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json -Depth 20
    if($manifest.version-cne'0.4.0'-or$manifest.sourceCommit-cne$ReplacementSourceCommit-or$manifest.productSourceDirty){throw 'Replacement candidate source/version differs.'}
    Check-Package $manifest
    $expectedManifestHash=$ReplacementManifestSha256
}
$matrixRoot='D:\ArchSift-lab\runs\ifx-0.4-library-chain-2352c85bc7ed49449d985eabbd1d7981'
$matrixResultPath=Join-Path $matrixRoot 'operator-result.json'
if((Sha $matrixResultPath)-cne'c39ea0f58c79a4b6ac79ef59ed2ced08e3222b09af7b535d1bbcab2ba301d99f'){throw 'Accepted matrix result changed.'}
$matrixResult=Get-Content -LiteralPath $matrixResultPath -Raw|ConvertFrom-Json -Depth 30
if($matrixResult.status-cne'pass-with-limitations'-or$matrixResult.checksPassed-ne8-or-not$matrixResult.hostStateEqual-or-not$matrixResult.targetHeadEqual-or-not$matrixResult.ordinaryStatusEqual-or-not$matrixResult.processExited){throw 'IFX API/CLI matrix is not accepted.'}
$subsetSource=Join-Path $matrixRoot 'library/declaration-subset.json'
$expectedSubsetHash='4f1db37bc6ab94406578c702293fc4fefd3d01b0f5f1cde9f7e84c90f88ccb9f'
if((Sha $subsetSource)-cne$expectedSubsetHash){throw 'Reviewed subset bytes changed.'}
if($PreflightOnly){[ordered]@{step='ifx-0.4-browser-preflight';status='metadata-pass';nativeProcessStarted=$false;sourceRecordsChecked=563;candidateRulesChecked=182;subsetRulesChecked=3;packageEntriesChecked=$manifest.entries.Count;packageSourceCommit=$manifest.sourceCommit;packageManifestSha256=$expectedManifestHash}|ConvertTo-Json;return}

$sessionRoot=Join-Path $lab ('runs/ifx-0.4-browser-'+[guid]::NewGuid().ToString('N'))
if(-not(Is-Under $sessionRoot $lab)-or(Is-Under $sessionRoot $target)-or(Is-Under $sessionRoot $repo)){throw 'Browser session output escaped the lab.'}
No-Links $sessionRoot
[void][IO.Directory]::CreateDirectory($sessionRoot)
$inputRoot=Join-Path $sessionRoot 'inputs';$libraryRoot=Join-Path $sessionRoot 'library';$reportRoot=Join-Path $sessionRoot 'reports'
foreach($path in @($inputRoot,$libraryRoot,$reportRoot)){[void][IO.Directory]::CreateDirectory($path)}
$originalCopy=Join-Path $inputRoot 'ifx-protection.json';$subsetCopy=Join-Path $inputRoot 'declaration-subset.json'
[IO.File]::WriteAllBytes($originalCopy,[IO.File]::ReadAllBytes($policy))
[IO.File]::WriteAllBytes($subsetCopy,[IO.File]::ReadAllBytes($subsetSource))
if((Sha $originalCopy)-cne$expectedRulesHash-or(Sha $subsetCopy)-cne$expectedSubsetHash){throw 'Browser input copies differ.'}
$configPath=Join-Path $sessionRoot 'config.json'
$configuration=[ordered]@{schemaVersion=1;target=@{root=$target;entry='IFX.sln'};rulesDirectory=$libraryRoot;rulesets=@($policy);build=@{mode='existing';targetFramework='net10.0';configuration='Debug';allowNetwork=$false;assemblyPaths=@()};output=@{directory=$reportRoot;formats=@('json','html','sarif')}}
Save-Json $configPath $configuration
$configHash=Sha $configPath
$hostBaseline=Get-ArchSiftHostHash;$targetHead=Git-Text @('rev-parse','HEAD');$targetStatus=Git-Text @('status','--porcelain=v1','--untracked-files=all')
$process=$null;$stdoutTail=$null;$stderrTail=$null;$port=$null;$review=@();$usabilityRating=$null;$problem=$null;$exitObserved=$false;$listenerGone=$false
try{
    if($targetHead-cne'64ef2674c57e6cf9031481d6d4410cca55b0dad5'-or$targetStatus){throw 'IFX HEAD/status changed before browser launch.'}
    Check-Source $discovery.inputIdentity.files
    if((Get-ArchSiftHostHash)-cne$hostBaseline){throw 'Host drift before browser launch.'}
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName=Join-Path $package 'web/ArchSift.Web.exe';$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in @('--config',$configPath)){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $sessionRoot 'web-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
    $start.Environment['DOTNET_CLI_UI_LANGUAGE']='en-US'
    $process=[Diagnostics.Process]::Start($start);$stderrTail=$process.StandardError.ReadToEndAsync()
    $ready=$null
    for($index=0;$index-lt30;$index++){
        $read=$process.StandardOutput.ReadLineAsync()
        if(-not$read.Wait(10000)){throw 'Native Web readiness timed out.'}
        $line=$read.Result
        if($null-eq$line){throw 'Native Web exited before readiness.'}
        if($line.StartsWith('ARCHSIFT_UI=')){$ready=$line.Substring(12);break}
    }
    if($null-eq$ready){throw 'Native Web readiness URL missing.'}
    $uri=[uri]$ready;$port=$uri.Port
    if($uri.Host-cne'127.0.0.1'-or$uri.Fragment-notmatch'^#session=[0-9a-f]{64}$'){throw 'Native Web URL is not an authenticated loopback address.'}
    $stdoutTail=$process.StandardOutput.ReadToEndAsync()
    $listeners=@(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction Stop|Where-Object OwningProcess -eq $process.Id)
    if($listeners.Count-ne1){throw 'Browser session listener ownership differs.'}
    if((Get-ArchSiftHostHash)-cne$hostBaseline){throw 'Host drift after Web launch.'}
    Write-Host ('Run root: '+$sessionRoot)
    Write-Host ('Import file 1: '+$originalCopy)
    Write-Host ('Import file 2: '+$subsetCopy)
    Write-Host ('Open this local browser URL; keep it private: '+$ready)
    Write-Host 'Complete the reviewed browser checklist, then click Safe shutdown in the UI. This command waits for the service to exit.'
    $process.WaitForExit()
    $exitObserved=$process.HasExited
    if($process.ExitCode-ne0){throw 'Native Web exited with an error.'}
    $listenerGone=@(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue|Where-Object OwningProcess -eq $process.Id).Count-eq0
    $questions=@(
        'Desktop and narrow-screen layouts in English and Chinese: clear sections, concise summary/details, distinct color plus text labels; Language and Safe shutdown aligned',
        'External rules read-only; both JSON files imported explicitly; 182-rule card bounded and editor hidden until New/Edit; editor rules default collapsed and ID/type search locates one rule; full saved card verified with partial/compliant, 105 limits, zero assemblies and no source binding visible',
        'Two-entry saved chain verified; entry order, per-child result/downloads and unique 105-project scope understandable',
        'After deleting the subset, chain Verify stayed available and the missing child was shown while the later full child ran',
        'After an invalid target entry submission, current project count/list/title/downloads cleared; prior result remained only in History',
        'Keyboard navigation, history, report downloads and Safe shutdown worked without a blocking dialog or horizontal overflow'
    )
    foreach($question in $questions){$answer=(Read-Host ($question+' [yes/no/not-observed]')).Trim().ToLowerInvariant();$review+=@{question=$question;answer=$(if($answer-in@('yes','no','not-observed')){$answer}else{'not-observed'})}}
    $ratingText=(Read-Host 'Independent overall usability rating [1-5, or not-observed]').Trim()
    if($ratingText-match'^[1-5]$'){$usabilityRating=[int]$ratingText}
}catch{$problem=$_.Exception.Message}
finally{
    if($process){if(-not$process.HasExited){$process.Kill($true);$process.WaitForExit()};$exitObserved=$process.HasExited;if($stdoutTail){[IO.File]::WriteAllText((Join-Path $sessionRoot 'web.stdout.log'),$stdoutTail.GetAwaiter().GetResult())};if($stderrTail){[IO.File]::WriteAllText((Join-Path $sessionRoot 'web.stderr.log'),$stderrTail.GetAwaiter().GetResult())};$process.Dispose()}
    $hostAfter=Get-ArchSiftHostHash;$headAfter=Git-Text @('rev-parse','HEAD');$statusAfter=Git-Text @('status','--porcelain=v1','--untracked-files=all')
    $sourceEqual=$true;try{Check-Source $discovery.inputIdentity.files}catch{$sourceEqual=$false}
    $packageEqual=$true;try{Check-Package $manifest}catch{$packageEqual=$false}
    $safe=$exitObserved-and$listenerGone-and$hostBaseline-ceq$hostAfter-and$targetHead-ceq$headAfter-and$targetStatus-ceq$statusAfter-and$sourceEqual-and$packageEqual-and(Sha $policy)-ceq$expectedRulesHash-and(Sha $manifestPath)-ceq$expectedManifestHash-and(Sha $configPath)-ceq$configHash
    $humanPass=$review.Count-eq6-and@($review|Where-Object { $_.answer -ne 'yes' }).Count-eq0-and$null-ne$usabilityRating-and$usabilityRating-ge4
    $result=[ordered]@{step='ifx-0.4-browser-manual-session';status=$(if($safe-and$humanPass-and$null-eq$problem){'pass-with-limitations'}elseif($safe){'needs-review'}else{'stop'});runRoot=$sessionRoot;review=$review;humanChecksPassed=$humanPass;usabilityRating=$usabilityRating;packageSourceCommit=$manifest.sourceCommit;packageManifestSha256=$expectedManifestHash;targetHeadEqual=($targetHead-ceq$headAfter);ordinaryStatusEqual=($targetStatus-ceq$statusAfter);sourceRecordsEqual=$sourceEqual;hostHashBefore=$hostBaseline;hostHashAfter=$hostAfter;hostStateEqual=($hostBaseline-ceq$hostAfter);packageUnchanged=$packageEqual;rulesUnchanged=((Sha $policy)-ceq$expectedRulesHash);configUnchanged=((Sha $configPath)-ceq$configHash);processExited=$exitObserved;recordedListenerGone=$listenerGone;targetBuildInvoked=$false;restoreInvoked=$false;policyAdopted=$false;browserVisualReviewPerformed=($review.Count-eq6);error=$problem;nextAction='RETURN RESULT AND ANY HUMAN OBSERVATIONS BEFORE NEXT COMMAND'}
    Save-Json (Join-Path $sessionRoot 'operator-result.json') $result
    $result|ConvertTo-Json -Depth 12
    if($result.status-eq'stop'){throw 'Browser operator session stopped; preserve evidence and review before another step.'}
}
