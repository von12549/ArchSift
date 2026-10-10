# Operator-only IFX block 03d: direct Web UI 1/2/4, one first process and five warm new processes each.
[CmdletBinding()]
param(
    [string]$ReviewedFreezeRoot='D:\ArchSift-lab\evidence\ifx-0.6-policy-freeze-c69cc6d46f8b4675a6dfab5225ff956a',
    [string]$ReviewedCanaryRoot='D:\ArchSift-lab\evidence\ifx-0.6-cli-canary-f9fb357a192544d3b2213e292216696e',
    [string]$ReviewedCliRoot='D:\ArchSift-lab\evidence\ifx-0.6-cli-measure-6a51446e41f448f9a0ac0f711fb9d030',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$NewInstallRoot='D:\IFX-10-Root\ArchSift-0.6-review',
    [string]$LabRoot='D:\ArchSift-lab',
    [ValidateRange(30,600)][int]$TimeoutSeconds=300
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
$NewInstallRoot=Safe-Path $NewInstallRoot;$LabRoot=Safe-Path $LabRoot;$ReviewedFreezeRoot=Safe-Path $ReviewedFreezeRoot;$ReviewedCanaryRoot=Safe-Path $ReviewedCanaryRoot;$ReviewedCliRoot=Safe-Path $ReviewedCliRoot
$sourceRoot=Safe-Path (Join-Path $PSScriptRoot '../..')
foreach($protected in @($sourceRoot,$TargetRoot,$ExistingInstallRoot)){
    if($NewInstallRoot-eq$protected-or$NewInstallRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'New installation overlaps a protected root.'}
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence root overlaps a protected root.'}
}
if($NewInstallRoot-eq$LabRoot-or$NewInstallRoot.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$LabRoot.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Installation and evidence roots overlap.'}
if(-not$ReviewedFreezeRoot.StartsWith((Join-Path $LabRoot 'evidence')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Frozen input is outside evidence root.'}
if(-not$ReviewedCanaryRoot.StartsWith((Join-Path $LabRoot 'evidence')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Reviewed canary is outside evidence root.'}
if(-not$ReviewedCliRoot.StartsWith((Join-Path $LabRoot 'evidence')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Reviewed CLI measurements are outside evidence root.'}
function Owned-Path([string]$Root,[string]$Relative){
    $path=Safe-Path (Join-Path $Root $Relative)
    if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escapes reviewed root.'}
    return $path
}
function Original-State {
    $selection=Get-Content -LiteralPath (Owned-Path $ExistingInstallRoot 'install.json') -Raw|ConvertFrom-Json
    if($selection.root-cne$ExistingInstallRoot-or$selection.selectedVersion-cne'0.5.0'){throw 'Original installation selection changed.'}
    $chosen=@($selection.versions|Where-Object version -eq '0.5.0')
    if($chosen.Count-ne1-or$chosen[0].manifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'){throw 'Original manifest identity changed.'}
    $records=[Collections.Generic.List[object]]::new()
    function Walk-Original([string]$Directory){
        foreach($path in [IO.Directory]::EnumerateFileSystemEntries($Directory)){
            [void](Safe-Path $path)
            if([IO.Directory]::Exists($path)){Walk-Original $path}else{
                $records.Add([pscustomobject][ordered]@{path=[IO.Path]::GetRelativePath($ExistingInstallRoot,$path);sha256=(Hash-File $path)})
                if($records.Count-gt10000){throw 'Original installation file-count limit.'}
            }
        }
    }
    Walk-Original $ExistingInstallRoot
    $sorted=@($records|Sort-Object path)
    return @{selectedVersion=$selection.selectedVersion;configPath=$selection.configPath;filesSha256=(Hash-Json $sorted);fileCount=$records.Count}
}
function Same-Target($A,$B){return $A.head-ceq$B.head-and$A.ordinaryStatusSha256-ceq$B.ordinaryStatusSha256-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
function Same-Original($A,$B){return $A.selectedVersion-ceq$B.selectedVersion-and$A.configPath-ceq$B.configPath-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
function Semantic-Sha($Summary,[string]$Root){
    $entries=@(foreach($entry in $Summary.entries){
        if(-not$entry.reportDirectory){throw 'A measured entry has no child report.'}
        $childRoot=Owned-Path $Root $entry.reportDirectory
        $report=Get-Content -LiteralPath (Owned-Path $childRoot 'report.json') -Raw|ConvertFrom-Json
        [ordered]@{
            entryId=$entry.entryId;execution=$entry.execution;compliance=$entry.compliance;exitCode=$entry.exitCode
            coverage=$entry.coverage;binding=$entry.binding
            ruleResultsSha256=(Hash-Json $entry.ruleResults);findingsSha256=(Hash-Json $entry.findings);limitationsSha256=(Hash-Json $entry.limitations)
            reportInputSha256=$report.inputIdentity.sha256;reportExecution=$report.execution;reportCompliance=$report.compliance
            reportRuleResultsSha256=(Hash-Json $report.ruleResults);reportFindingsSha256=(Hash-Json $report.findings)
        }
    })
    return Hash-Json ([ordered]@{schemaVersion=$Summary.schemaVersion;toolVersion=$Summary.toolVersion;chainId=$Summary.snapshot.chainId;targetKind=$Summary.snapshot.targetKind;targetRoot=$Summary.snapshot.target.root;inputSha256=$Summary.snapshot.inputIdentity.sha256;buildInputSha256=$Summary.snapshot.buildInputIdentity.sha256;chainEntriesSha256=(Hash-Json $Summary.snapshot.entries);execution=$Summary.execution;compliance=$Summary.compliance;exitCode=$Summary.exitCode;projectCount=$Summary.projectCount;entries=$entries;limitationsSha256=(Hash-Json $Summary.limitations)})
}
function Port-Closed([int]$Port){
    $socket=[Net.Sockets.TcpClient]::new()
    try{
        try{$task=$socket.ConnectAsync([Net.IPAddress]::Loopback,$Port);if(-not$task.Wait(1000)){return $false};return -not$socket.Connected}
        catch{
            $error=$_.Exception
            while($null-ne$error.InnerException){$error=$error.InnerException}
            return $error -is [Net.Sockets.SocketException] -and $error.SocketErrorCode -eq [Net.Sockets.SocketError]::ConnectionRefused
        }
    }finally{$socket.Dispose()}
}

$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$originalBefore=$null;$freeze=$null;$policyEqual=$null;$configEqual=$null;$configBefore=$null
$completed=$false;$attemptedCount=0;$completedCount=0;$ownedStopped=$true;$allPortsClosed=$true;$semanticSha=$null;$records=[Collections.Generic.List[object]]::new()
$run=Join-Path $LabRoot ('evidence/ifx-0.6-ui-measure-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($run)
try{
    $freezeResult=Get-Content -LiteralPath (Owned-Path $ReviewedFreezeRoot 'result.json') -Raw|ConvertFrom-Json
    $freezeAudit=Get-Content -LiteralPath (Owned-Path $ReviewedFreezeRoot 'audit-errors.json') -Raw|ConvertFrom-Json
    $freeze=Get-Content -LiteralPath (Owned-Path $ReviewedFreezeRoot 'frozen-input.json') -Raw|ConvertFrom-Json
    if($freezeResult.status-cne'policy-frozen-for-review'-or$freezeResult.copiedFileCount-ne6-or-not$freezeResult.sourcePolicyStateEqual-or-not$freezeResult.targetStateEqual-or-not$freezeResult.originalInstallationStateEqual-or-not$freezeResult.hostStateEqual-or$freezeResult.analysisOrBuildPerformed-or@($freezeAudit).Count-ne0){throw '03a reviewed result is not clean.'}
    if((Hash-File (Owned-Path $ReviewedFreezeRoot 'frozen-input.json'))-cne'26eca9a9f63d99b0da5badcb1ba3207220f4854b8333b64d2f6c7251a4f5cf08'-or$freeze.status-cne'policy-input-frozen-for-review'-or$freeze.sourceCommit-cne'a05c44ea5a1e544f7d3e4ffb5d4a411291180a31'-or$freeze.packageSha256-cne'9f311ced6d1610761d6f00ef6f380a608d14859b1adc809b7031c42e029661fb'-or$freeze.installRoot-cne$NewInstallRoot-or$freeze.chainId-cne'ifx-04-class-A-test'-or$freeze.rulesetCount-ne4-or$freeze.buildMode-cne'existing'-or$freeze.targetFramework-cne'net10.0'-or$freeze.configuration-cne'Debug'-or$freeze.allowNetwork){throw 'Frozen input identity changed.'}
    $canaryResult=Get-Content -LiteralPath (Owned-Path $ReviewedCanaryRoot 'result.json') -Raw|ConvertFrom-Json
    $canaryAudit=Get-Content -LiteralPath (Owned-Path $ReviewedCanaryRoot 'audit-errors.json') -Raw|ConvertFrom-Json
    if((Hash-File (Owned-Path $ReviewedCanaryRoot 'result.json'))-cne'fe98ce3848e471f4b4a0c73938c608751737b13680d6b8aa2645de3db606ab01'-or$canaryResult.status-cne'serial-canary-for-review'-or$canaryResult.exitCode-ne4-or-not$canaryResult.targetStateEqual-or-not$canaryResult.originalInstallationStateEqual-or-not$canaryResult.hostStateEqual-or-not$canaryResult.configStateEqual-or-not$canaryResult.policyStateEqual-or-not$canaryResult.ownedProcessStopped-or@($canaryAudit).Count-ne0){throw 'Reviewed serial canary is not clean.'}
    $canaryRoot=Safe-Path $canaryResult.reportDirectory
    if($canaryRoot-cne(Owned-Path (Owned-Path $NewInstallRoot 'reports') '40c6dce372d940cdb2217b7127028caa')-or(Hash-File (Owned-Path $canaryRoot 'chain-summary.json'))-cne'4c0aa4c58c5b4446ae51b55a01b0d06a1b0f222b8972cca9f93ddb5708b92687'){throw 'Reviewed canary report changed.'}
    $canarySummary=Get-Content -LiteralPath (Owned-Path $canaryRoot 'chain-summary.json') -Raw|ConvertFrom-Json
    $semanticSha=Semantic-Sha $canarySummary $canaryRoot
    $cliResult=Get-Content -LiteralPath (Owned-Path $ReviewedCliRoot 'result.json') -Raw|ConvertFrom-Json
    $cliAudit=Get-Content -LiteralPath (Owned-Path $ReviewedCliRoot 'audit-errors.json') -Raw|ConvertFrom-Json
    if((Hash-File (Owned-Path $ReviewedCliRoot 'result.json'))-cne'2c63b72226f46f3e0148ef35051634953fa80fede39a44b524183e079f94691e'-or(Hash-File (Owned-Path $ReviewedCliRoot 'measurements.json'))-cne'57915c23a489da421825619d276cdf7afc3c181fc12df378ff50cd79ec34aae7'-or$cliResult.status-cne'cli-measurements-for-review'-or$cliResult.completedCount-ne18-or$cliResult.attemptedCount-ne18-or$cliResult.semanticSha256-cne$semanticSha-or-not$cliResult.targetStateEqual-or-not$cliResult.originalInstallationStateEqual-or-not$cliResult.hostStateEqual-or-not$cliResult.configStateEqual-or-not$cliResult.policyStateEqual-or-not$cliResult.ownedProcessStopped-or@($cliAudit).Count-ne0){throw 'Reviewed CLI measurements differ or are incomplete.'}
    foreach($owned in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        try{$image=$owned.Path;if(-not$image){throw 'Cannot inspect live ArchSift process.'};if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$image.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'An original or new installation process is active.'}}finally{$owned.Dispose()}
    }
    $targetBefore=Target-State;$originalBefore=Original-State
    if(-not(Same-Target $targetBefore $freeze.target)-or-not(Same-Original $originalBefore $freeze.original)){throw 'Target or original installation changed since policy freeze.'}
    $installPath=Owned-Path $NewInstallRoot 'install.json';$configPath=Owned-Path $NewInstallRoot 'config/default.json'
    $catalogPath=Owned-Path $NewInstallRoot 'config/profiles.json';$selectionPath=Owned-Path $NewInstallRoot 'config/selection.json'
    $manifestPath=Owned-Path $NewInstallRoot 'versions/0.6.0/package-manifest.json';$exePath=Owned-Path $NewInstallRoot 'versions/0.6.0/archsift.exe';$webExePath=Owned-Path $NewInstallRoot 'versions/0.6.0/web/ArchSift.Web.exe'
    $rulesRoot=Owned-Path $NewInstallRoot 'rules';$chainPath=Owned-Path $rulesRoot 'chains/ifx-04-class-A-test.json'
    $install=Get-Content -LiteralPath $installPath -Raw|ConvertFrom-Json
    if((Hash-File $installPath)-cne'0b8701997ba6f4686b90cc0fe935fc6e12b867840b0f342f13bff2ded7784fc4'-or$install.selectedVersion-cne'0.6.0'-or$install.configPath-cne$configPath-or$install.libraryPath-cne$rulesRoot-or$install.targetRoot-cne$TargetRoot){throw 'New installation selection changed.'}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
    $exeRecord=@($manifest.entries|Where-Object path -eq 'archsift.exe');$webRecord=@($manifest.entries|Where-Object path -eq 'web/ArchSift.Web.exe')
    if((Hash-File $manifestPath)-cne'39501df8e2b6e875b9e9c377e7bdf75544281b4ab769a377912c248f01fa3f2f'-or$manifest.version-cne'0.6.0'-or$manifest.sourceCommit-cne$freeze.sourceCommit-or$exeRecord.Count-ne1-or(Hash-File $exePath)-cne$exeRecord[0].sha256-or$webRecord.Count-ne1-or(Hash-File $webExePath)-cne$webRecord[0].sha256){throw 'Selected native executable differs from reviewed package.'}
    $config=Get-Content -LiteralPath $configPath -Raw|ConvertFrom-Json
    $configBefore=@{managedSha256=(Hash-File $configPath);catalogSha256=(Hash-File $catalogPath);selectionSha256=(Hash-File $selectionPath);installSha256=(Hash-File $installPath)}
    if($configBefore.managedSha256-cne$freeze.configSha256-or$config.target.root-cne$TargetRoot-or$config.target.entry-cne'IFX.sln'-or$config.rulesDirectory-cne$rulesRoot-or$config.output.directory-cne(Owned-Path $NewInstallRoot 'reports')-or$config.build.mode-cne'existing'-or$config.build.targetFramework-cne'net10.0'-or$config.build.configuration-cne'Debug'-or$config.build.allowNetwork-or@($config.build.sources).Count-ne0-or@($config.build.assemblyPaths).Count-ne0-or($config.build.PSObject.Properties.Name-contains'assemblyManifest'-and$config.build.assemblyManifest)){throw 'Managed configuration no longer matches reviewed existing/offline target.'}
    $expectedEntries=@('4238199ffcc0405c85893430f01c582f','e628477df8d347adb5f7d880ec1ef0c5','d844bb174a4940a39617ff71a60dbe79','8b19f875ef6d4f7ba6db0c95754f25ca')
    if(($freeze.chainEntryIds -join ',')-cne($expectedEntries -join ',')){throw 'Frozen chain order differs.'}
    $chain=Get-Content -LiteralPath $chainPath -Raw|ConvertFrom-Json
    if(($chain.entries.entryId -join ',')-cne($expectedEntries -join ',')){throw 'Destination chain order differs.'}
    $policyEqual=$true
    foreach($file in $freeze.policyFiles){
        $path=Owned-Path $rulesRoot $file.path
        if((Hash-File $path)-cne$file.sha256-or([IO.FileInfo]::new($path)).Length-ne$file.bytes){$policyEqual=$false;break}
    }
    if(-not$policyEqual-or@($freeze.policyFiles).Count-ne6){throw 'Frozen destination policy bytes changed.'}
    foreach($name in @('ifx-foundation-v1.json','ifx-hub-boundaries-v1.json','ifx-direct-packages-v1.json','ifx-tenant-boundaries-v1.json')){
        $ruleset=Get-Content -LiteralPath (Owned-Path $rulesRoot $name) -Raw|ConvertFrom-Json
        foreach($rule in @($ruleset.rules|Where-Object enabled)){
            if($rule.type-ceq'type-dependency'-or($rule.type-ceq'naming'-and$rule.parameters.subjectKind-in @('type','assembly'))){throw 'This chain requires assembly evidence; stop before canary.'}
        }
    }
    $reportBase=Owned-Path $NewInstallRoot 'reports'
    $existingReports=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($path in [IO.Directory]::EnumerateDirectories($reportBase)){[void](Safe-Path $path);[void]$existingReports.Add($path)}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;config=$configBefore;policyFiles=$freeze.policyFiles;cliExecutableSha256=(Hash-File $exePath);webExecutableSha256=(Hash-File $webExePath);reportBase=$reportBase}|ConvertTo-Json -Depth 12))
    if($canarySummary.schemaVersion-ne2-or$canarySummary.toolVersion-cne'0.6.0'-or$canarySummary.snapshot.inputIdentity.sha256-cne'ea84db669bdfc609035585bfb9aba90cee9ee9b096115dbeabf30b957834d529'-or$canarySummary.projectCount-ne105-or$canarySummary.execution-cne'partial'-or$canarySummary.compliance-cne'compliant'-or$canarySummary.exitCode-ne4){throw 'Reviewed canary semantics differ.'}
    $records=[Collections.Generic.List[object]]::new()
    for($iteration=0;$iteration-lt6;$iteration++){
        foreach($concurrency in @(1,2,4)){
            $stem="ui-c${concurrency}-i${iteration}"
            $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$webExePath;$start.UseShellExecute=$false;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.CreateNoWindow=$true
            foreach($arg in @('--config',$configPath)){[void]$start.ArgumentList.Add($arg)}
            $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'dotnet-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$start.Environment['ARCHSIFT_UI_PARENT_CONTROL']='stdin-v1'
            [void]$start.Environment.Remove('ARCHSIFT_UI_PROFILE_CONTROL')
            [void][IO.Directory]::CreateDirectory($start.Environment['DOTNET_CLI_HOME'])
            $launchClock=[Diagnostics.Stopwatch]::StartNew();$process=[Diagnostics.Process]::Start($start);$attemptedCount++
            $client=$null;$address=$null;$webPid=$null;$job=$null;$stdoutRest=$null;$stderrTask=$process.StandardError.ReadToEndAsync();$startupLines=[Collections.Generic.List[string]]::new()
            $peakBytes=0L;$cpuMs=$null;$startupMs=$null;$jobMs=$null;$reportRoot=$null;$portClosed=$false;$shutdownMode='none'
            try{
                $ready=$false
                while(-not$ready){
                    if($launchClock.Elapsed.TotalSeconds-gt30){throw "Web UI startup timeout on $stem."}
                    $line=$process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(30)).GetAwaiter().GetResult()
                    if($null-eq$line){throw "Web UI exited before readiness on $stem."}
                    if($line.StartsWith('ARCHSIFT_UI=')){
                        $address=[Uri]::new($line.Substring(12));[void]$startupLines.Add('ARCHSIFT_UI=<redacted>')
                        if($address.Scheme-cne'http'-or$address.Host-cne'127.0.0.1'-or$address.Fragment-notmatch'^#session=[0-9a-f]{64}$'){throw "Unexpected local UI address on $stem."}
                    }elseif($line.StartsWith('ARCHSIFT_PID=')){
                        $webPid=[int]::Parse($line.Substring(13));[void]$startupLines.Add("ARCHSIFT_PID=$webPid")
                    }else{[void]$startupLines.Add($line);if($line.StartsWith('ARCHSIFT_STATE=waiting')){$ready=$true}}
                }
                $stdoutRest=$process.StandardOutput.ReadToEndAsync()
                if($null-eq$address-or$webPid-ne$process.Id){throw "UI address or owned PID identity differs on $stem."}
                $startupMs=$launchClock.ElapsedMilliseconds
                $client=[Net.Http.HttpClient]::new();$client.Timeout=[TimeSpan]::FromSeconds(10)
                $client.BaseAddress=[Uri]::new($address.GetLeftPart([UriPartial]::Authority))
                $client.DefaultRequestHeaders.Add('X-ArchSift-Token',$address.Fragment.Substring(9))
                $session=$client.GetStringAsync('/api/session').GetAwaiter().GetResult()|ConvertFrom-Json
                if($session.processId-ne$webPid-or$session.running-or$session.dirty){throw "Web UI is not idle on $stem."}
                $jobClock=[Diagnostics.Stopwatch]::StartNew()
                $request=if($concurrency-eq1){@{chainId=$freeze.chainId;targetKind='real'}}else{@{chainId=$freeze.chainId;targetKind='real';maxConcurrency=$concurrency}}
                $content=[Net.Http.StringContent]::new(($request|ConvertTo-Json -Compress),[Text.Encoding]::UTF8,'application/json')
                try{$response=$client.PostAsync('/api/run/chain',$content).GetAwaiter().GetResult();try{$response.EnsureSuccessStatusCode()|Out-Null;$jobId=($response.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json).jobId}finally{$response.Dispose()}}finally{$content.Dispose()}
                if($jobId-notmatch'^[0-9a-f]{32}$'){throw "Unexpected UI job ID on $stem."}
                do{
                    if($jobClock.Elapsed.TotalSeconds-gt$TimeoutSeconds){throw "UI job timeout on $stem."}
                    $jobText=$client.GetStringAsync('/api/jobs/'+$jobId).GetAwaiter().GetResult()
                    $job=$jobText|ConvertFrom-Json
                    try{$process.Refresh();$peakBytes=[Math]::Max($peakBytes,$process.PeakWorkingSet64)}catch{}
                    if($job.state-eq'running'){Start-Sleep -Milliseconds 10}
                }while($job.state-eq'running')
                $jobClock.Stop();$jobMs=$jobClock.ElapsedMilliseconds
                [IO.File]::WriteAllText((Join-Path $run "$stem.job.json"),$jobText)
                $summary=$job.chain
                if($job.state-cne'partial'-or$job.exitCode-ne4-or$job.error-or$null-eq$summary-or$summary.schemaVersion-ne2-or$summary.kind-cne'chain-summary'-or$summary.toolVersion-cne'0.6.0'-or$summary.exitCode-ne4-or$summary.snapshot.chainId-cne$freeze.chainId-or$summary.snapshot.targetKind-cne'real'-or$summary.snapshot.executionOptions.maxConcurrency-ne$concurrency-or$summary.snapshot.target.root-cne$TargetRoot-or$summary.snapshot.build.mode-cne'existing'-or$summary.snapshot.build.allowNetwork-or$summary.projectCount-ne105-or@($summary.snapshot.inputIdentity.files).Count-ne585-or$summary.entries.Count-ne4-or($summary.entries.entryId -join ',')-cne($expectedEntries -join ',')){throw "UI job identity, order or outcome differs on $stem."}
                if($job.progress.stage-cne'finished'-or$job.progress.endedCount-ne4-or$job.progress.executedCount-ne4-or$job.progress.skippedCount-ne0-or$job.progress.runningCount-ne0){throw "UI final progress differs on $stem."}
                $reportRoot=Safe-Path $summary.snapshot.outputDirectory
                if(-not$reportRoot.StartsWith($reportBase+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not$existingReports.Add($reportRoot)){throw "UI report directory is outside the new root or reused on $stem."}
                $summaryPath=Owned-Path $reportRoot 'chain-summary.json'
                foreach($name in @('snapshot.json','chain-summary.json','chain-summary.html','chain-summary.sarif')){[void](Hash-File (Owned-Path $reportRoot $name))}
                $savedSummary=Get-Content -LiteralPath $summaryPath -Raw|ConvertFrom-Json
                if((Semantic-Sha $summary $reportRoot)-cne$semanticSha-or(Semantic-Sha $savedSummary $reportRoot)-cne$semanticSha){throw "UI job and saved result semantics differ from canary on $stem."}
                foreach($format in @('json','html','sarif')){
                    $name=if($format-eq'json'){'chain-summary.json'}elseif($format-eq'html'){'chain-summary.html'}else{'chain-summary.sarif'}
                    $apiText=$client.GetStringAsync('/api/jobs/'+$jobId+'/report/'+$format).GetAwaiter().GetResult()
                    if($apiText.TrimEnd()-cne([IO.File]::ReadAllText((Owned-Path $reportRoot $name))).TrimEnd()){throw "Current UI download differs from saved $format on $stem."}
                }
                $chainHtml=[IO.File]::ReadAllText((Owned-Path $reportRoot 'chain-summary.html'))
                $chainSarif=Get-Content -LiteralPath (Owned-Path $reportRoot 'chain-summary.sarif') -Raw|ConvertFrom-Json
                if(-not$chainHtml.Contains('Execution: <strong>partial</strong>; policy: <strong>compliant</strong>; exit: 4')-or-not$chainHtml.Contains('This run is incomplete.')-or$chainSarif.version-cne'2.1.0'-or$chainSarif.runs.Count-ne5-or($chainSarif.runs[0..3].properties.chainEntryId -join ',')-cne($expectedEntries -join ',')-or$chainSarif.runs[4].properties.execution-cne'partial'-or$chainSarif.runs[4].properties.compliance-cne'compliant'){throw "UI HTML/SARIF projection differs on $stem."}
                foreach($entry in $summary.entries){
                    $childRoot=Owned-Path $reportRoot $entry.reportDirectory
                    foreach($name in @('report.json','report.html','report.sarif')){[void](Hash-File (Owned-Path $childRoot $name))}
                    $childJson=Get-Content -LiteralPath (Owned-Path $childRoot 'report.json') -Raw|ConvertFrom-Json
                    $childSarif=Get-Content -LiteralPath (Owned-Path $childRoot 'report.sarif') -Raw|ConvertFrom-Json
                    if($childJson.execution-cne$entry.execution-or$childJson.compliance-cne$entry.compliance-or@($childJson.ruleResults).Count-ne@($entry.ruleResults).Count-or@($childJson.findings).Count-ne@($entry.findings).Count-or$childSarif.version-cne'2.1.0'-or$childSarif.runs.Count-ne1-or@($childSarif.runs[0].results).Count-ne@($entry.findings).Count){throw "UI child projection differs on $stem."}
                }
                $reportFiles=@([IO.Directory]::EnumerateFiles($reportRoot,'*',[IO.SearchOption]::AllDirectories))
                $reportBytes=0L;foreach($path in $reportFiles){[void](Safe-Path $path);$reportBytes+=([IO.FileInfo]::new($path)).Length}
                $session=$client.GetStringAsync('/api/session').GetAwaiter().GetResult()|ConvertFrom-Json
                if($session.processId-ne$webPid-or$session.running-or$session.dirty){throw "UI did not return to idle on $stem."}
                $content=[Net.Http.StringContent]::new('{}',[Text.Encoding]::UTF8,'application/json')
                try{$response=$client.PostAsync('/api/shutdown',$content).GetAwaiter().GetResult();try{if([int]$response.StatusCode-ne202){throw "UI safe shutdown was not accepted on $stem."}}finally{$response.Dispose()}}finally{$content.Dispose()}
                $shutdownMode='api'
                if(-not$process.WaitForExit(10000)){throw "UI safe shutdown timeout on $stem."}
                $launchClock.Stop();$webExitCode=$process.ExitCode
                $portClosed=Port-Closed $address.Port
                $allPortsClosed=$allPortsClosed-and$portClosed
                if($webExitCode-ne0-or-not$portClosed){throw "UI process or exact loopback port remained on $stem."}
                try{$process.Refresh();$peakBytes=[Math]::Max($peakBytes,$process.PeakWorkingSet64);$cpuMs=[long]$process.TotalProcessorTime.TotalMilliseconds}catch{}
                $records.Add([ordered]@{surface='direct-native-web';iteration=$iteration;cache=$(if($iteration-eq0){'first-process/os-cache-unspecified'}else{'warm-os-cache/new-process'});maxConcurrency=$concurrency;startupMilliseconds=$startupMs;jobWallMilliseconds=$jobMs;totalWallMilliseconds=$launchClock.ElapsedMilliseconds;cpuMilliseconds=$cpuMs;peakWorkingSetBytes=$peakBytes;memoryScope='single-native-web-process/no-assembly-worker';reportBytes=$reportBytes;reportFileCount=$reportFiles.Count;reportDirectory=$reportRoot;summarySha256=(Hash-File $summaryPath);inputIdentitySha256=$summary.snapshot.inputIdentity.sha256;semanticSha256=$semanticSha;timings=$summary.timings;webPid=$webPid;loopbackPort=$address.Port;safeShutdown=$shutdownMode;portClosed=$portClosed})
                $completedCount++
                [IO.File]::WriteAllText((Join-Path $run 'progress.json'),(@{attemptedCount=$attemptedCount;completedCount=$completedCount;lastStem=$stem;lastReportDirectory=$reportRoot}|ConvertTo-Json))
            }finally{
                if(-not$process.HasExited){
                    try{$process.StandardInput.WriteLine('shutdown');$process.StandardInput.Flush()}catch{}
                    if(-not$process.WaitForExit(10000)){$process.Kill($true);$process.WaitForExit()}
                }
                $ownedStopped=$ownedStopped-and$process.HasExited
                try{[Threading.Tasks.Task]::WaitAll(@($stderrTask)+$(if($null-ne$stdoutRest){@($stdoutRest)}else{@()}))}catch{}
                $sanitizedError=if($stderrTask.IsCompletedSuccessfully){$stderrTask.Result -replace '#session=[0-9a-fA-F]+','#session=<redacted>'}else{'stderr unavailable'}
                [IO.File]::WriteAllText((Join-Path $run "$stem.stderr.txt"),$sanitizedError)
                $safeLines=@($startupLines)
                if($null-ne$stdoutRest-and$stdoutRest.IsCompletedSuccessfully){$safeLines+=@(($stdoutRest.Result -replace 'ARCHSIFT_UI=[^\r\n]+','ARCHSIFT_UI=<redacted>' -replace '#session=[0-9a-fA-F]+','#session=<redacted>'))}
                [IO.File]::WriteAllText((Join-Path $run "$stem.stdout-redacted.txt"),($safeLines -join [Environment]::NewLine))
                if($null-ne$client){$client.Dispose()}
                $process.Dispose()
            }
            if((Get-ArchSiftHostHash)-cne$hostBefore){throw "Host state drift after $stem; preserve evidence."}
        }
        if(-not(Same-Target (Target-State) $targetBefore)-or-not(Same-Original (Original-State) $originalBefore)){throw "Target or original installation drift after iteration $iteration."}
    }
    try{$cpu=Get-CimInstance Win32_Processor|Select-Object -First 1 -ExpandProperty Name}catch{$cpu='unavailable'}
    [IO.File]::WriteAllText((Join-Path $run 'measurements.json'),(@{schemaVersion=1;surface='direct-native-web';status='measurements-for-review';sourceCommit=$freeze.sourceCommit;packageSha256=$freeze.packageSha256;webExecutableSha256=(Hash-File $webExePath);configSha256=$configBefore.managedSha256;policyFiles=$freeze.policyFiles;chainId=$freeze.chainId;reviewedCliRoot=$ReviewedCliRoot;canaryReportDirectory=$canaryRoot;semanticSha256=$semanticSha;inputIdentitySha256=$canarySummary.snapshot.inputIdentity.sha256;target=$targetBefore;original=$originalBefore;hostSha256=$hostBefore;targetFramework='net10.0';targetConfiguration='Debug';buildMode='existing';allowNetwork=$false;sdk='not invoked (existing mode)';cpu=$cpu;logicalProcessors=[Environment]::ProcessorCount;osVersion=[Environment]::OSVersion.VersionString;cacheMethod='one first process per concurrency, then five warm new processes; OS cache not cleared';records=$records.ToArray()}|ConvertTo-Json -Depth 18))
    $completed=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=($_.Exception.Message -replace '#session=[0-9a-fA-F]+','#session=<redacted>')}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$policyAfterEqual=$null;$configEqual=$null;$auditErrors=@()
    if($null-ne$targetBefore){try{$targetEqual=Same-Target (Target-State) $targetBefore}catch{$targetEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=Same-Original (Original-State) $originalBefore}catch{$originalEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$freeze){try{$policyAfterEqual=@($freeze.policyFiles|Where-Object {(Hash-File (Owned-Path (Owned-Path $NewInstallRoot 'rules') $_.path))-cne$_.sha256}).Count-eq0}catch{$policyAfterEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$configBefore){try{$configEqual=(Hash-File (Owned-Path $NewInstallRoot 'config/default.json'))-ceq$configBefore.managedSha256-and(Hash-File (Owned-Path $NewInstallRoot 'config/profiles.json'))-ceq$configBefore.catalogSha256-and(Hash-File (Owned-Path $NewInstallRoot 'config/selection.json'))-ceq$configBefore.selectionSha256-and(Hash-File (Owned-Path $NewInstallRoot 'install.json'))-ceq$configBefore.installSha256}catch{$configEqual=$false;$auditErrors+=$_.Exception.Message}}
    $result=@{status=$(if($completed-and$hostEqual-and$targetEqual-and$originalEqual-and$policyAfterEqual-and$configEqual-and$ownedStopped-and$allPortsClosed-and$completedCount-eq18){'ui-measurements-for-review'}else{'stop'});runRoot=$run;attemptedCount=$attemptedCount;completedCount=$completedCount;semanticSha256=$semanticSha;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;hostStateEqual=$hostEqual;policyStateEqual=$policyAfterEqual;configStateEqual=$configEqual;ownedProcessStopped=$ownedStopped;ownedListenersClosed=$allPortsClosed}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false-or$policyAfterEqual-eq$false-or$configEqual-eq$false-or-not$ownedStopped-or-not$allPortsClosed){throw 'Safety audit failed; preserve evidence without repair.'}
}
