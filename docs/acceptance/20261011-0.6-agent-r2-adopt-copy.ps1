# Agent-authorized block 05b: re-plan and adopt only the reviewed external 0.5.0 copy.
[CmdletBinding()]
param(
    [string]$CopyRun='D:\ArchSift-lab\evidence\ifx-0.6-r2-lifecycle-91c27481fd5443f09eefb0a541c37e00',
    [string]$SetupPath='D:\ArchSift-lab\packages\draft-0.6-replacement-independent-20261011\ArchSift.Setup.exe',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$CopyRun=Safe-Path $CopyRun;$SetupPath=Safe-Path $SetupPath;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot
if($CopyRun-cne'D:\ArchSift-lab\evidence\ifx-0.6-r2-lifecycle-91c27481fd5443f09eefb0a541c37e00'){throw 'Only the reviewed copy root is allowed.'}
$copyRoot=Safe-Path (Join-Path $CopyRun 'installation');$hostBefore=Get-ArchSiftHostHash
$run=Safe-Path (Join-Path $CopyRun ('adopt-'+[Guid]::NewGuid().ToString('N')));[void][IO.Directory]::CreateDirectory($run)
function Original-State {
    $selection=Get-Content -LiteralPath (Safe-Path (Join-Path $ExistingInstallRoot 'install.json')) -Raw|ConvertFrom-Json
    if($selection.root-cne$ExistingInstallRoot-or$selection.selectedVersion-cne'0.5.0'){throw 'Original selection changed.'}
    $records=[Collections.Generic.List[object]]::new()
    function Walk([string]$Directory){foreach($path in [IO.Directory]::EnumerateFileSystemEntries($Directory)){[void](Safe-Path $path);if([IO.Directory]::Exists($path)){Walk $path}else{$records.Add([pscustomobject][ordered]@{path=[IO.Path]::GetRelativePath($ExistingInstallRoot,$path);sha256=(Hash-File $path)});if($records.Count-gt10000){throw 'Original file-count limit.'}}}}
    Walk $ExistingInstallRoot
    return @{selectedVersion=$selection.selectedVersion;configPath=$selection.configPath;filesSha256=(Hash-Json @($records|Sort-Object path));fileCount=$records.Count}
}
function Same-Target($A,$B){return $A.head-ceq$B.head-and$A.ordinaryStatusSha256-ceq$B.ordinaryStatusSha256-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
function Same-Original($A,$B){return $A.selectedVersion-ceq$B.selectedVersion-and$A.configPath-ceq$B.configPath-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
function Tree-Hash([string]$Root){
    $Root=Safe-Path $Root;$records=[Collections.Generic.List[object]]::new()
    foreach($item in @(Get-ChildItem -LiteralPath $Root -Recurse -Force)){
        $path=Safe-Path $item.FullName
        if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Copied user state escapes root.'}
        if($item.PSIsContainer){continue}
        if(-not($item -is [IO.FileInfo])){throw 'Unsupported user state item.'}
        $records.Add([ordered]@{path=[IO.Path]::GetRelativePath($Root,$path);length=$item.Length;sha256=(Hash-File $path)})
        if($records.Count-gt10000){throw 'User state file-count limit.'}
    }
    return Hash-Json @($records|Sort-Object path)
}
function Run-Setup([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){[void]$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'dotnet-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';[void][IO.Directory]::CreateDirectory($start.Environment['DOTNET_CLI_HOME'])
    $process=[Diagnostics.Process]::Start($start)
    try{
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw "Setup $Name timed out."}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run "$Name.json"),$stdout.Result);[IO.File]::WriteAllText((Join-Path $run "$Name.stderr.txt"),$stderr.Result)
        if($process.ExitCode-ne0-or($stdout.Result+$stderr.Result).Contains('#session=')){throw "Setup $Name failed or leaked token."}
        if((Get-ArchSiftHostHash)-cne$hostBefore){throw "Host drift after Setup $Name."}
        return $stdout.Result|ConvertFrom-Json
    }finally{$process.Dispose()}
}
$targetBefore=$null;$originalBefore=$null;$copyConfigHash=$null;$copyLibraryHash=$null;$copyReportsHash=$null;$completed=$false;$applied=$false;$receipt=$null;$plan=$null
try{
    if((Hash-File (Join-Path $CopyRun 'result.json'))-cne'78210975164e24c4afd581c65266d69e703288c738b62966333511e995bf8ad3'-or(Hash-File (Join-Path $CopyRun 'copy-summary.json'))-cne'52fd7e37bc38e93be4acb6cba8455374873a44e6d41cd88b6139b32a0e3c730f'-or(Hash-File $SetupPath)-cne'6327fcf10700aeec00f0b84da4201d4c3ad8b10d51d3f894766db4a2c55debab'){throw 'Reviewed evidence or Setup changed.'}
    $previous=Get-Content -LiteralPath (Join-Path $CopyRun 'result.json') -Raw|ConvertFrom-Json
    $summary=Get-Content -LiteralPath (Join-Path $CopyRun 'copy-summary.json') -Raw|ConvertFrom-Json
    $before=Get-Content -LiteralPath (Join-Path $CopyRun 'before.json') -Raw|ConvertFrom-Json
    if($previous.status-cne'external-copy-adopt-plan-ready'-or$previous.copyRoot-cne$copyRoot-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.originalInstallationStateEqual-or$previous.adoptApplied-or$previous.upgradeApplied){throw 'Copy gate is not ready.'}
    if(Test-Path -LiteralPath (Join-Path $copyRoot 'install.json')){throw 'Copy was already adopted.'}
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){try{$path=$running.Path;if(-not$path){throw 'Cannot inspect active ArchSift process.'};if($path.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$path.StartsWith($copyRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original or copy installation is active.'}}finally{$running.Dispose()}}
    $targetBefore=Target-State;$originalBefore=Original-State
    if(-not(Same-Target $targetBefore $before.target)-or-not(Same-Original $originalBefore $before.original)){throw 'Protected state changed since copy.'}
    $copyConfigHash=Hash-File (Join-Path $copyRoot 'config/ifx.json')
    $copyLibraryHash=Tree-Hash (Join-Path $copyRoot 'rules')
    $copyReportsHash=Tree-Hash (Join-Path $copyRoot 'reports')
    if($copyConfigHash-cne$summary.copyConfigSha256-or$copyLibraryHash-cne$summary.librarySha256-or$copyReportsHash-cne$summary.reportsSha256){throw 'Copied user state differs from reviewed bytes.'}
    $plan=Run-Setup 'adopt-plan' @('plan','adopt','--root',$copyRoot,'--version-directory',$summary.versionDirectory,'--config',$summary.configPath)
    $planPath=Join-Path $run 'adopt-plan.json'
    if($plan.schemaVersion-ne1-or$plan.operation-cne'adopt'-or$plan.root-cne$copyRoot-or$plan.adoptedVersion.version-cne'0.5.0'-or$plan.adoptedVersion.manifestSha256-cne$summary.manifestSha256-or$plan.configPath-cne$summary.configPath-or$plan.state.files.Count-ne8-or$plan.state.sha256-cne'ee57d687cbf3cf534084d92dc738c74ff2f0f038e29f717f2bcde83104e7ac1e'-or$plan.config.target.entry-cne'IFX.sln'-or(Safe-Path $plan.config.target.root)-cne$TargetRoot-or$plan.config.build.mode-cne'existing'-or$plan.config.build.allowNetwork-or[DateTimeOffset]::Parse($plan.expiresUtc)-le[DateTimeOffset]::UtcNow){throw 'Renewed adopt plan differs from reviewed scope.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;copyConfigSha256=$copyConfigHash;copyLibrarySha256=$copyLibraryHash;copyReportsSha256=$copyReportsHash;planId=$plan.planId;planSha256=(Hash-File $planPath)}|ConvertTo-Json -Depth 10))
    $receipt=Run-Setup 'adopt-receipt' @('apply','--plan',$planPath,'--plan-id',$plan.planId);$applied=$true
    if($receipt.schemaVersion-ne1-or$receipt.operation-cne'adopt'-or$receipt.outcome-cne'success'-or-not$receipt.hostStateEqual-or$receipt.planId-cne$plan.planId-or$receipt.after.root-cne$copyRoot-or$receipt.after.selectedVersion-cne'0.5.0'-or$receipt.afterState.sha256-cne$plan.state.sha256){throw 'Adoption receipt differs.'}
    $inspect=Run-Setup 'adopt-inspect' @('inspect','--root',$copyRoot)
    if($inspect.status-cne'verified'-or$inspect.installation.selectedVersion-cne'0.5.0'-or$inspect.installation.lastOperation-cne$receipt.id-or$inspect.pendingOperations.Count-ne0-or$inspect.state.sha256-cne$plan.state.sha256){throw 'Adopted copy inspection differs.'}
    if((Hash-File (Join-Path $copyRoot 'config/ifx.json'))-cne$copyConfigHash-or(Tree-Hash (Join-Path $copyRoot 'rules'))-cne$copyLibraryHash-or(Tree-Hash (Join-Path $copyRoot 'reports'))-cne$copyReportsHash){throw 'Adoption changed copied user state.'}
    $completed=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;applied=$applied}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$audit=@()
    if($null-ne$targetBefore){try{$targetEqual=Same-Target (Target-State) $targetBefore}catch{$targetEqual=$false;$audit+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=Same-Original (Original-State) $originalBefore}catch{$originalEqual=$false;$audit+=$_.Exception.Message}}
    $result=@{status=$(if($completed-and$hostEqual-and$targetEqual-and$originalEqual){'external-copy-adopted'}else{'stop'});runRoot=$run;copyRoot=$copyRoot;planId=$plan.planId;adoptReceiptId=$receipt.id;adoptApplied=$applied;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;analysisOrBuildPerformed=$false}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $audit));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false){throw 'Safety audit failed; preserve evidence.'}
}
