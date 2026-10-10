# Operator-only first IFX block. The coding agent must not execute this script for real IFX.
[CmdletBinding()]
param(
    [string]$CandidateRoot='D:\ArchSift-lab\packages\draft-0.6-download-e23ad7b9c033471eb2b254b4e50f333f',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$NewInstallRoot='D:\IFX-10-Root\ArchSift-0.6-review',
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$NewInstallRoot=Safe-Path $NewInstallRoot;$CandidateRoot=Safe-Path $CandidateRoot;$LabRoot=Safe-Path $LabRoot
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach($protected in @($sourceRoot,$TargetRoot,$ExistingInstallRoot)){
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/source roots overlap.'}
    if($NewInstallRoot-eq$protected-or$NewInstallRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($NewInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'New installation/source roots overlap.'}
}
if(Test-Path -LiteralPath $NewInstallRoot){throw 'New review root must be absent; preserve it if present.'}
function Original-State {
    $selection=Get-Content -LiteralPath (Safe-Path (Join-Path $ExistingInstallRoot 'install.json')) -Raw|ConvertFrom-Json
    if($selection.root-cne$ExistingInstallRoot-or$selection.selectedVersion-cne'0.5.0'){throw 'Original selection differs from reviewed 0.5.0 identity; stop for review.'}
    $chosen=@($selection.versions|Where-Object version -eq '0.5.0')
    if($chosen.Count-ne1-or$chosen[0].manifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'){throw 'Original manifest identity changed.'}
    $records=[Collections.Generic.List[object]]::new()
    function Walk-Original([string]$Directory){
        foreach($path in [IO.Directory]::EnumerateFileSystemEntries($Directory)){
            [void](Safe-Path $path)
            if([IO.Directory]::Exists($path)){Walk-Original $path}else{$records.Add(@{path=[IO.Path]::GetRelativePath($ExistingInstallRoot,$path);sha256=(Hash-File $path)});if($records.Count-gt10000){throw 'Original installation file-count limit.'}}
        }
    }
    Walk-Original $ExistingInstallRoot
    return @{selectedVersion=$selection.selectedVersion;configPath=$selection.configPath;filesSha256=(Hash-Json @($records|Sort-Object path));fileCount=$records.Count}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$originalBefore=$null;$completed=$false
$run=Join-Path $LabRoot ('evidence/ifx-0.6-plan-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($run)
try{
    foreach($owned in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        try{$image=$owned.Path;if(-not$image){throw 'Cannot inspect a live ArchSift process; operator review required.'};if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Close the original installation UI/CLI safely before this block.'}}finally{$owned.Dispose()}
    }
    $targetBefore=Target-State;$originalBefore=Original-State
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore}|ConvertTo-Json -Depth 10))
    $setup=Safe-Path (Join-Path $CandidateRoot 'ArchSift.Setup.exe');$zip=Safe-Path (Join-Path $CandidateRoot 'archsift-0.6.0-win-x64.zip')
    $zipHash='9f311ced6d1610761d6f00ef6f380a608d14859b1adc809b7031c42e029661fb'
    if((Hash-File $zip)-cne$zipHash-or(Hash-File $setup)-cne'49fd9f6072c3f4a945bdaef6bf0ab6d940cd8a9e6d7ab9fce33778759cb62b9c'){throw 'Reviewed downloaded assets differ.'}
    $archive=[IO.Compression.ZipFile]::OpenRead($zip)
    try{$entry=$archive.GetEntry('package-manifest.json');if($null-eq$entry-or$entry.Length-gt4194304){throw 'Missing/oversized candidate manifest.'};$reader=[IO.StreamReader]::new($entry.Open());try{$manifest=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}}finally{$archive.Dispose()}
    if($manifest.version-cne'0.6.0'-or$manifest.sourceCommit-cne'a05c44ea5a1e544f7d3e4ffb5d4a411291180a31'-or$manifest.productSourceDirty){throw 'Frozen candidate identity differs.'}
    if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift before plan; no repair.'}
    $start=[Diagnostics.ProcessStartInfo]::new($setup);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in @('plan','install','--root',$NewInstallRoot,'--package',$zip,'--sha256',$zipHash,'--target',$TargetRoot,'--entry','IFX.sln','--tfm','net10.0','--smoke','true')){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(-not$process.WaitForExit(120000)){$process.Kill($true);$process.WaitForExit();throw 'Read-only planning timed out.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr);[IO.File]::WriteAllText((Join-Path $run 'plan.json'),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $run 'plan.stderr.log'),$stderr.Result)
        if($process.ExitCode-ne0){throw 'Read-only plan rejected; review retained logs.'}
        $plan=$stdout.Result|ConvertFrom-Json
        if($plan.operation-cne'install'-or$plan.configPath-cne(Join-Path $NewInstallRoot 'config/default.json')-or$plan.config.target.root-cne$TargetRoot-or$plan.config.build.mode-cne'existing'-or$plan.config.build.allowNetwork-or$plan.config.rulesets.Count-ne0-or-not$plan.smoke){throw 'Plan defaults or scope differ.'}
        if(Test-Path -LiteralPath $NewInstallRoot){throw 'Read-only plan created an installation root.'};$completed=$true
    }finally{$process.Dispose()}
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$audit=@()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$audit+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=(Hash-Json (Original-State))-ceq(Hash-Json $originalBefore)}catch{$originalEqual=$false;$audit+=$_.Exception.Message}}
    $result=@{status=$(if($completed-and$hostEqual-and$targetEqual-and$originalEqual){'plan-ready-for-review'}else{'stop'});runRoot=$run;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;applied=$false;analysisOrBuildPerformed=$false}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $audit));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false){throw 'Safety drift/audit failure; preserve evidence without repair.'}
}
