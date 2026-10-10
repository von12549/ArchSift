# Operator-only IFX block 03a: copy six reviewed policy files into the new install root, then freeze inputs.
[CmdletBinding()]
param(
    [string]$ReviewedInstallRoot='D:\ArchSift-lab\evidence\ifx-0.6-install-641393a2052a4318a3b248d9b8c3d54d',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$NewInstallRoot='D:\IFX-10-Root\ArchSift-0.6-review',
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
$NewInstallRoot=Safe-Path $NewInstallRoot;$LabRoot=Safe-Path $LabRoot;$ReviewedInstallRoot=Safe-Path $ReviewedInstallRoot
$sourceRoot=Safe-Path (Join-Path $PSScriptRoot '../..')
foreach($protected in @($sourceRoot,$TargetRoot,$ExistingInstallRoot)){
    if($NewInstallRoot-eq$protected-or$NewInstallRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'New installation/source roots overlap.'}
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/source roots overlap.'}
}
if($NewInstallRoot-eq$LabRoot-or$NewInstallRoot.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$LabRoot.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'New installation/evidence roots overlap.'}
if(-not$ReviewedInstallRoot.StartsWith((Join-Path $LabRoot 'evidence')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Install result is outside the evidence root.'}
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
function Stable-Path([string]$Root,[string]$Relative){
    $path=Safe-Path (Join-Path $Root $Relative)
    if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Policy path escapes its reviewed root.'}
    return $path
}
$policyNames=@('.archsift-library.json','ifx-foundation-v1.json','ifx-hub-boundaries-v1.json','ifx-direct-packages-v1.json','ifx-tenant-boundaries-v1.json','chains/ifx-04-class-A-test.json')
$expected=@{
    '.archsift-library.json'='2ed9cf357092d0557d974b165cf2c1543b67f8b547601499b755143c77f3b135'
    'ifx-foundation-v1.json'='2a4b8c052a3bff05bda7afe2a0b7821d53e058b6627926762fec640c69fdbc2f'
    'ifx-hub-boundaries-v1.json'='e5cd1f5254774b4b11c905bce93884fa7942ab6264f51b7d080e24c8ace53da5'
    'ifx-direct-packages-v1.json'='524502bc940d7e5e16b9444df1ed36f73381ce8cf46d37065e20b6b484eaa90c'
    'ifx-tenant-boundaries-v1.json'='4919b07e941d2f1ff26a78c9fe724a7464771a98e07dd63548726913b659e980'
    'chains/ifx-04-class-A-test.json'='b29552ab34da4f29d219e9da17693599cae653547465e46cb4840fdab179ad64'
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$originalBefore=$null;$completed=$false;$sourceRecords=@()
$run=Join-Path $LabRoot ('evidence/ifx-0.6-policy-freeze-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($run)
try{
    $installResult=Get-Content -LiteralPath (Safe-Path (Join-Path $ReviewedInstallRoot 'result.json')) -Raw|ConvertFrom-Json
    $installAudit=Get-Content -LiteralPath (Safe-Path (Join-Path $ReviewedInstallRoot 'audit-errors.json')) -Raw|ConvertFrom-Json
    $receipt=Get-Content -LiteralPath (Safe-Path (Join-Path $ReviewedInstallRoot 'apply.stdout.json')) -Raw|ConvertFrom-Json
    $inspection=Get-Content -LiteralPath (Safe-Path (Join-Path $ReviewedInstallRoot 'post-inspect.stdout.json')) -Raw|ConvertFrom-Json
    $installBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $ReviewedInstallRoot 'before.json')) -Raw|ConvertFrom-Json
    if($installResult.status-cne'install-ready-for-review'-or-not$installResult.targetStateEqual-or-not$installResult.originalInstallationStateEqual-or-not$installResult.hostStateEqual-or-not$installResult.ownedLauncherStopped-or$installResult.analysisOrBuildPerformed-or@($installAudit).Count-ne0){throw 'Reviewed installation result is not clean.'}
    if($installResult.reviewedPlanId-cne'9cd21e4fa820b5d262cbf6571f169764647cd7c2f0399266ab3440317a481a89'-or$installResult.newInstallRoot-cne$NewInstallRoot-or$receipt.planId-cne$installResult.reviewedPlanId-or$receipt.outcome-cne'success'-or$receipt.after.selectedVersion-cne'0.6.0'-or$receipt.after.root-cne$NewInstallRoot-or$inspection.status-cne'verified'-or$inspection.pendingOperations.Count-ne0-or$inspection.installation.selectedVersion-cne'0.6.0'){throw 'Reviewed installation identity differs.'}
    foreach($owned in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        try{$image=$owned.Path;if(-not$image){throw 'Cannot inspect live ArchSift process.'};if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$image.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original or new installation has an active CLI/UI.'}}finally{$owned.Dispose()}
    }
    $targetBefore=Target-State;$originalBefore=Original-State
    if($targetBefore.head-cne$installBefore.target.head-or$targetBefore.ordinaryStatusSha256-cne$installBefore.target.ordinaryStatusSha256-or$targetBefore.filesSha256-cne$installBefore.target.filesSha256-or$targetBefore.fileCount-ne$installBefore.target.fileCount-or
        $originalBefore.selectedVersion-cne$installBefore.original.selectedVersion-or$originalBefore.configPath-cne$installBefore.original.configPath-or$originalBefore.filesSha256-cne$installBefore.original.filesSha256-or$originalBefore.fileCount-ne$installBefore.original.fileCount-or
        $hostBefore-cne$installBefore.hostSha256){throw 'Source, original installation or host changed since install review.'}
    $install=Get-Content -LiteralPath (Stable-Path $NewInstallRoot 'install.json') -Raw|ConvertFrom-Json
    if($install.selectedVersion-cne'0.6.0'-or$install.configPath-cne(Join-Path $NewInstallRoot 'config/default.json')-or$install.libraryPath-cne(Join-Path $NewInstallRoot 'rules')-or$install.targetRoot-cne$TargetRoot-or(Hash-File (Stable-Path $NewInstallRoot 'install.json'))-cne$inspection.selectionSha256){throw 'New installation changed after inspect.'}
    $newConfig=Stable-Path $NewInstallRoot 'config/default.json'
    $catalog=Stable-Path $NewInstallRoot 'config/profiles.json';$selection=Stable-Path $NewInstallRoot 'config/selection.json'
    $configHash=Hash-File $newConfig;$catalogHash=Hash-File $catalog;$selectionHash=Hash-File $selection
    if($configHash-cne'c0bcde53d142122e2fc5050e74fcb770c617d4028f0eaba18cf9c26a073e79ad'){throw 'New managed config changed since Setup receipt.'}
    $sourceRules=Stable-Path $ExistingInstallRoot 'rules';$destinationRules=Stable-Path $NewInstallRoot 'rules'
    if(Test-Path -LiteralPath $destinationRules){throw 'Destination rules directory already exists; preserve it for review.'}
    foreach($name in $policyNames){
        $source=Stable-Path $sourceRules $name
        $hash=Hash-File $source
        if($hash-cne$expected[$name]){throw "Source policy byte identity changed: $name"}
        $sourceRecords+=@{path=$name;sha256=$hash;bytes=([IO.FileInfo]::new($source)).Length}
    }
    $registry=Get-Content -LiteralPath (Stable-Path $sourceRules '.archsift-library.json') -Raw|ConvertFrom-Json
    $chain=Get-Content -LiteralPath (Stable-Path $sourceRules 'chains/ifx-04-class-A-test.json') -Raw|ConvertFrom-Json
    $active=@($registry.entries|Where-Object {-not$_.deleted})
    if($registry.schemaVersion-ne1-or$chain.schemaVersion-ne1-or$chain.id-cne'ifx-04-class-A-test'-or$active.Count-ne4-or$chain.entries.Count-ne4-or@($registry.entries|Where-Object {$_.deleted}).Count-ne1){throw 'Policy registry/chain topology changed.'}
    foreach($entry in $chain.entries){if(@($active|Where-Object {$_.entryId-ceq$entry.entryId}).Count-ne1){throw 'Chain entry no longer resolves to one active library entry.'}}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;newInstallSelectionSha256=$inspection.selectionSha256;newConfigSha256=$configHash;catalogSha256=$catalogHash;profileSelectionSha256=$selectionHash;sourcePolicyFiles=$sourceRecords}|ConvertTo-Json -Depth 12))
    [void][IO.Directory]::CreateDirectory($destinationRules)
    [void][IO.Directory]::CreateDirectory((Stable-Path $destinationRules 'chains'))
    foreach($name in $policyNames){
        $from=Stable-Path $sourceRules $name;$to=Stable-Path $destinationRules $name
        [IO.File]::Copy($from,$to,$false)
        if((Hash-File $to)-cne$expected[$name]){throw "Copied policy bytes differ: $name"}
    }
    foreach($name in $policyNames){if((Hash-File (Stable-Path $sourceRules $name))-cne$expected[$name]){throw "Source policy changed during copy: $name"}}
    if((Hash-File (Stable-Path $NewInstallRoot 'install.json'))-cne$inspection.selectionSha256-or(Hash-File $newConfig)-cne$configHash-or(Hash-File $catalog)-cne$catalogHash-or(Hash-File $selection)-cne$selectionHash){throw 'New installation metadata changed during policy copy.'}
    $freeze=@{schemaVersion=1;status='policy-input-frozen-for-review';sourceCommit='a05c44ea5a1e544f7d3e4ffb5d4a411291180a31';packageSha256='9f311ced6d1610761d6f00ef6f380a608d14859b1adc809b7031c42e029661fb';target=$targetBefore;original=$originalBefore;installRoot=$NewInstallRoot;configPath=$newConfig;configSha256=$configHash;libraryPath=$destinationRules;policyFiles=$sourceRecords;chainId=$chain.id;chainEntryIds=@($chain.entries|ForEach-Object {$_.entryId});rulesetCount=4;buildMode='existing';targetFramework='net10.0';configuration='Debug';allowNetwork=$false;analysisOrBuildPerformed=$false}
    [IO.File]::WriteAllText((Join-Path $run 'frozen-input.json'),($freeze|ConvertTo-Json -Depth 16))
    $completed=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$sourceEqual=$null;$auditErrors=@()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=(Hash-Json (Original-State))-ceq(Hash-Json $originalBefore)}catch{$originalEqual=$false;$auditErrors+=$_.Exception.Message}}
    if($sourceRecords.Count-eq$policyNames.Count){try{$sourceEqual=@($sourceRecords|Where-Object {(Hash-File (Stable-Path (Stable-Path $ExistingInstallRoot 'rules') $_.path))-cne$_.sha256}).Count-eq0}catch{$sourceEqual=$false;$auditErrors+=$_.Exception.Message}}
    $result=@{status=$(if($completed-and$hostEqual-and$targetEqual-and$originalEqual-and$sourceEqual){'policy-frozen-for-review'}else{'stop'});runRoot=$run;newInstallRoot=$NewInstallRoot;copiedFileCount=$policyNames.Count;sourcePolicyStateEqual=$sourceEqual;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;hostStateEqual=$hostEqual;analysisOrBuildPerformed=$false}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false-or$sourceEqual-eq$false){throw 'Safety or source audit failed; preserve evidence without repair.'}
}
