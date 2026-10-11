# Operator-only IFX block 03b: one serial CLI canary against the reviewed 03a policy bytes.
[CmdletBinding()]
param(
    [string]$ReviewedFreezeRoot='D:\ArchSift-lab\evidence\ifx-0.6-policy-freeze-c69cc6d46f8b4675a6dfab5225ff956a',
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
$NewInstallRoot=Safe-Path $NewInstallRoot;$LabRoot=Safe-Path $LabRoot;$ReviewedFreezeRoot=Safe-Path $ReviewedFreezeRoot
$sourceRoot=Safe-Path (Join-Path $PSScriptRoot '../..')
foreach($protected in @($sourceRoot,$TargetRoot,$ExistingInstallRoot)){
    if($NewInstallRoot-eq$protected-or$NewInstallRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'New installation overlaps a protected root.'}
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence root overlaps a protected root.'}
}
if($NewInstallRoot-eq$LabRoot-or$NewInstallRoot.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$LabRoot.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Installation and evidence roots overlap.'}
if(-not$ReviewedFreezeRoot.StartsWith((Join-Path $LabRoot 'evidence')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Frozen input is outside evidence root.'}
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

$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$originalBefore=$null;$freeze=$null;$policyEqual=$null;$configEqual=$null;$configBefore=$null
$completed=$false;$attempted=$false;$ownedStopped=$true;$reportRoot=$null;$exitCode=$null;$elapsedMs=$null;$peakBytes=$null;$cpuMs=$null
$run=Join-Path $LabRoot ('evidence/ifx-0.6-cli-canary-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($run)
try{
    $freezeResult=Get-Content -LiteralPath (Owned-Path $ReviewedFreezeRoot 'result.json') -Raw|ConvertFrom-Json
    $freezeAudit=Get-Content -LiteralPath (Owned-Path $ReviewedFreezeRoot 'audit-errors.json') -Raw|ConvertFrom-Json
    $freeze=Get-Content -LiteralPath (Owned-Path $ReviewedFreezeRoot 'frozen-input.json') -Raw|ConvertFrom-Json
    if($freezeResult.status-cne'policy-frozen-for-review'-or$freezeResult.copiedFileCount-ne6-or-not$freezeResult.sourcePolicyStateEqual-or-not$freezeResult.targetStateEqual-or-not$freezeResult.originalInstallationStateEqual-or-not$freezeResult.hostStateEqual-or$freezeResult.analysisOrBuildPerformed-or@($freezeAudit).Count-ne0){throw '03a reviewed result is not clean.'}
    if((Hash-File (Owned-Path $ReviewedFreezeRoot 'frozen-input.json'))-cne'26eca9a9f63d99b0da5badcb1ba3207220f4854b8333b64d2f6c7251a4f5cf08'-or$freeze.status-cne'policy-input-frozen-for-review'-or$freeze.sourceCommit-cne'a05c44ea5a1e544f7d3e4ffb5d4a411291180a31'-or$freeze.packageSha256-cne'9f311ced6d1610761d6f00ef6f380a608d14859b1adc809b7031c42e029661fb'-or$freeze.installRoot-cne$NewInstallRoot-or$freeze.chainId-cne'ifx-04-class-A-test'-or$freeze.rulesetCount-ne4-or$freeze.buildMode-cne'existing'-or$freeze.targetFramework-cne'net10.0'-or$freeze.configuration-cne'Debug'-or$freeze.allowNetwork){throw 'Frozen input identity changed.'}
    foreach($owned in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        try{$image=$owned.Path;if(-not$image){throw 'Cannot inspect live ArchSift process.'};if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$image.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'An original or new installation process is active.'}}finally{$owned.Dispose()}
    }
    $targetBefore=Target-State;$originalBefore=Original-State
    if(-not(Same-Target $targetBefore $freeze.target)-or-not(Same-Original $originalBefore $freeze.original)){throw 'Target or original installation changed since policy freeze.'}
    $installPath=Owned-Path $NewInstallRoot 'install.json';$configPath=Owned-Path $NewInstallRoot 'config/default.json'
    $catalogPath=Owned-Path $NewInstallRoot 'config/profiles.json';$selectionPath=Owned-Path $NewInstallRoot 'config/selection.json'
    $manifestPath=Owned-Path $NewInstallRoot 'versions/0.6.0/package-manifest.json';$exePath=Owned-Path $NewInstallRoot 'versions/0.6.0/archsift.exe'
    $rulesRoot=Owned-Path $NewInstallRoot 'rules';$chainPath=Owned-Path $rulesRoot 'chains/ifx-04-class-A-test.json'
    $install=Get-Content -LiteralPath $installPath -Raw|ConvertFrom-Json
    if((Hash-File $installPath)-cne'0b8701997ba6f4686b90cc0fe935fc6e12b867840b0f342f13bff2ded7784fc4'-or$install.selectedVersion-cne'0.6.0'-or$install.configPath-cne$configPath-or$install.libraryPath-cne$rulesRoot-or$install.targetRoot-cne$TargetRoot){throw 'New installation selection changed.'}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
    $exeRecord=@($manifest.entries|Where-Object path -eq 'archsift.exe')
    if((Hash-File $manifestPath)-cne'39501df8e2b6e875b9e9c377e7bdf75544281b4ab769a377912c248f01fa3f2f'-or$manifest.version-cne'0.6.0'-or$manifest.sourceCommit-cne$freeze.sourceCommit-or$exeRecord.Count-ne1-or(Hash-File $exePath)-cne$exeRecord[0].sha256){throw 'Selected native executable differs from reviewed package.'}
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
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;config=$configBefore;policyFiles=$freeze.policyFiles;executableSha256=(Hash-File $exePath);reportBase=$reportBase}|ConvertTo-Json -Depth 12))
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$exePath;$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.CreateNoWindow=$true
    foreach($arg in @('chain','verify','--config',$configPath,'--chain',$chainPath,'--target-kind','real','--max-concurrency','1')){[void]$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'dotnet-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    [void][IO.Directory]::CreateDirectory($start.Environment['DOTNET_CLI_HOME'])
    $attempted=$true;$process=[Diagnostics.Process]::Start($start)
    try{
        $stdoutTask=$process.StandardOutput.ReadToEndAsync();$stderrTask=$process.StandardError.ReadToEndAsync();$clock=[Diagnostics.Stopwatch]::StartNew();$timedOut=$false;$peakBytes=0
        while(-not$process.WaitForExit(50)){
            try{$process.Refresh();$peakBytes=[Math]::Max($peakBytes,$process.WorkingSet64)}catch{}
            if($clock.Elapsed.TotalSeconds-gt$TimeoutSeconds){$timedOut=$true;$process.Kill($true);break}
        }
        $process.WaitForExit();$clock.Stop();[Threading.Tasks.Task]::WaitAll($stdoutTask,$stderrTask)
        $elapsedMs=$clock.ElapsedMilliseconds;$exitCode=$process.ExitCode
        try{$process.Refresh();$peakBytes=[Math]::Max($peakBytes,$process.PeakWorkingSet64);$cpuMs=[long]$process.TotalProcessorTime.TotalMilliseconds}catch{}
        [IO.File]::WriteAllText((Join-Path $run 'cli.stdout.json'),$stdoutTask.Result)
        [IO.File]::WriteAllText((Join-Path $run 'cli.stderr.txt'),$stderrTask.Result)
        $ownedStopped=$process.HasExited
        if($timedOut){throw 'Owned CLI exceeded reviewed timeout; exact process tree was stopped.'}
    }finally{$process.Dispose()}
    $summary=Get-Content -LiteralPath (Join-Path $run 'cli.stdout.json') -Raw|ConvertFrom-Json
    if($summary.schemaVersion-ne2-or$summary.kind-cne'chain-summary'-or$summary.toolVersion-cne'0.6.0'-or$summary.exitCode-ne$exitCode-or$summary.snapshot.chainId-cne$freeze.chainId-or$summary.snapshot.targetKind-cne'real'-or$summary.snapshot.executionOptions.maxConcurrency-ne1-or$summary.snapshot.target.root-cne$TargetRoot-or$summary.snapshot.build.mode-cne'existing'-or$summary.snapshot.build.allowNetwork-or$summary.projectCount-lt1-or$summary.entries.Count-ne4-or($summary.entries.entryId -join ',')-cne($expectedEntries -join ',')){throw 'CLI summary identity, order or process exit differs.'}
    $reportRoot=Safe-Path $summary.snapshot.outputDirectory
    if(-not$reportRoot.StartsWith($reportBase+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Report directory escaped the new installation reports root.'}
    foreach($name in @('snapshot.json','chain-summary.json','chain-summary.html','chain-summary.sarif')){[void](Hash-File (Owned-Path $reportRoot $name))}
    if(([IO.File]::ReadAllText((Owned-Path $reportRoot 'chain-summary.json'))).TrimEnd()-cne([IO.File]::ReadAllText((Join-Path $run 'cli.stdout.json'))).TrimEnd()){throw 'CLI stdout differs from its current saved summary.'}
    $children=@()
    foreach($entry in $summary.entries){
        $child=@{entryId=$entry.entryId;execution=$entry.execution;compliance=$entry.compliance;exitCode=$entry.exitCode;coverage=$entry.coverage;binding=$entry.binding;findingCount=@($entry.findings).Count;ruleResultCount=@($entry.ruleResults).Count;limitations=@($entry.limitations);reportDirectory=$entry.reportDirectory}
        if($entry.reportDirectory){
            $childRoot=Owned-Path $reportRoot $entry.reportDirectory
            foreach($name in @('report.json','report.html','report.sarif')){[void](Hash-File (Owned-Path $childRoot $name))}
        }
        $children+=$child
    }
    [IO.File]::WriteAllText((Join-Path $run 'measurement.json'),(@{schemaVersion=1;sourceCommit=$freeze.sourceCommit;packageSha256=$freeze.packageSha256;inputIdentitySha256=$summary.snapshot.inputIdentity.sha256;chainId=$summary.snapshot.chainId;execution=$summary.execution;compliance=$summary.compliance;exitCode=$exitCode;projectCount=$summary.projectCount;elapsedMilliseconds=$elapsedMs;cpuMilliseconds=$cpuMs;peakWorkingSetBytes=$peakBytes;reportDirectory=$reportRoot;entries=$children;limitations=@($summary.limitations)}|ConvertTo-Json -Depth 16))
    $completed=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$policyAfterEqual=$null;$configEqual=$null;$auditErrors=@()
    if($null-ne$targetBefore){try{$targetEqual=Same-Target (Target-State) $targetBefore}catch{$targetEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=Same-Original (Original-State) $originalBefore}catch{$originalEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$freeze){try{$policyAfterEqual=@($freeze.policyFiles|Where-Object {(Hash-File (Owned-Path (Owned-Path $NewInstallRoot 'rules') $_.path))-cne$_.sha256}).Count-eq0}catch{$policyAfterEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$configBefore){try{$configEqual=(Hash-File (Owned-Path $NewInstallRoot 'config/default.json'))-ceq$configBefore.managedSha256-and(Hash-File (Owned-Path $NewInstallRoot 'config/profiles.json'))-ceq$configBefore.catalogSha256-and(Hash-File (Owned-Path $NewInstallRoot 'config/selection.json'))-ceq$configBefore.selectionSha256-and(Hash-File (Owned-Path $NewInstallRoot 'install.json'))-ceq$configBefore.installSha256}catch{$configEqual=$false;$auditErrors+=$_.Exception.Message}}
    $result=@{status=$(if($completed-and$hostEqual-and$targetEqual-and$originalEqual-and$policyAfterEqual-and$configEqual-and$ownedStopped){'serial-canary-for-review'}else{'stop'});runRoot=$run;reportDirectory=$reportRoot;executionAttempted=$attempted;exitCode=$exitCode;elapsedMilliseconds=$elapsedMs;peakWorkingSetBytes=$peakBytes;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;hostStateEqual=$hostEqual;policyStateEqual=$policyAfterEqual;configStateEqual=$configEqual;ownedProcessStopped=$ownedStopped}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false-or$policyAfterEqual-eq$false-or$configEqual-eq$false-or-not$ownedStopped){throw 'Safety audit failed; preserve evidence without repair.'}
}
