# Operator-only step 01. The agent must not execute this script for real IFX.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SetupPath,
    [Parameter(Mandatory)][string]$ZipPath,
    [Parameter(Mandatory)][string]$ExpectedSetupSha256,
    [Parameter(Mandatory)][string]$ExpectedZipSha256,
    [Parameter(Mandatory)][string]$ExpectedSourceCommit,
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory,
    [string]$NewInstallRoot='D:\IFX-10-Root\ArchSift-0.5-review',
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$NewInstallRoot=Safe-Path $NewInstallRoot;$LabRoot=Safe-Path $LabRoot
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach($protected in @($sourceRoot,$TargetRoot,$ExistingInstallRoot)){
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/protected roots overlap.'}
}
if([IO.Directory]::Exists($NewInstallRoot)-or[IO.File]::Exists($NewInstallRoot)){throw 'New review root must be absent; preserve existing directories.'}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null
$run=Join-Path $LabRoot ('evidence/ifx-0.5-plan-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
$beforePath=Join-Path $run 'before.json'
[IO.File]::WriteAllText($beforePath,(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore}|ConvertTo-Json -Depth 8))
$completed=$false
$candidateStarted=$false
try{
    $targetBefore=Target-State
    [IO.File]::WriteAllText($beforePath,(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore}|ConvertTo-Json -Depth 8))
    $existingBefore=Existing-State
    [IO.File]::WriteAllText($beforePath,(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore}|ConvertTo-Json -Depth 8))
    $SetupPath=Safe-Path $SetupPath;$ZipPath=Safe-Path $ZipPath
    if((Hash-File $SetupPath)-cne$ExpectedSetupSha256-or(Hash-File $ZipPath)-cne$ExpectedZipSha256){throw 'Reviewed candidate asset hash mismatch.'}
    $archive=[IO.Compression.ZipFile]::OpenRead($ZipPath)
    try{$entry=$archive.GetEntry('package-manifest.json');if($null-eq$entry-or$entry.Length-gt4194304){throw 'Candidate manifest missing/oversize.'};$reader=[IO.StreamReader]::new($entry.Open());try{$candidate=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}}finally{$archive.Dispose()}
    if($candidate.version-cne'0.5.0'-or$candidate.sourceCommit-cne$ExpectedSourceCommit-or$candidate.productSourceDirty){throw 'Candidate source identity mismatch.'}
    if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift after asset check; no repair.'}
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in @('plan','install','--root',$NewInstallRoot,'--package',$ZipPath,'--sha256',$ExpectedZipSha256,'--target',$TargetRoot,'--entry','IFX.sln','--tfm','net10.0','--smoke','true')){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$candidateStarted=$true;$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(-not$process.WaitForExit(120000)){$process.Kill($true);$process.WaitForExit();throw 'Read-only setup planning timeout.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run 'plan.json'),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $run 'plan.stderr.log'),$stderr.Result)
        if($process.ExitCode-ne0){throw ('Setup planning rejected; retain evidence. Exit '+$process.ExitCode)}
        $plan=$stdout.Result|ConvertFrom-Json
        if($plan.operation-cne'install'-or$plan.config.target.root-cne$TargetRoot-or$plan.config.build.mode-cne'existing'-or$plan.config.build.allowNetwork-or$plan.config.rulesets.Count-ne0){throw 'Reviewed installation defaults differ.'}
        if([IO.Directory]::Exists($NewInstallRoot)){throw 'Read-only plan unexpectedly created install root.'}
        $completed=$true
    }finally{$process.Dispose()}
}catch{
    [IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;candidateStarted=$candidateStarted}|ConvertTo-Json -Depth 4))
    throw
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$existingEqual=$null
    $auditErrors=[Collections.Generic.List[string]]::new()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$existingBefore){try{$existingEqual=(Hash-Json (Existing-State))-ceq(Hash-Json $existingBefore)}catch{$existingEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    $result=[ordered]@{status=$(if($completed-and$hostEqual-and$targetEqual-and$existingEqual){'plan-ready-for-review'}else{'stop'});runRoot=$run;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;applied=$false;analysisOrBuildPerformed=$false}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 5));$result|ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors.ToArray()))
    if(-not$hostEqual-or($null-ne$targetEqual-and-not$targetEqual)-or($null-ne$existingEqual-and-not$existingEqual)){throw 'Safety drift or audit failure; preserve evidence and stop. No repair.'}
}
