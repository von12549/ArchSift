# Agent-authorized block 05a: copy the reviewed original 0.5.0 state externally and plan adoption.
[CmdletBinding()]
param(
    [string]$FreezeRoot='D:\ArchSift-lab\evidence\ifx-0.6-r2-policy-freeze-6432925c039843caa1b881c5372427eb',
    [string]$UiRoot='D:\ArchSift-lab\evidence\ifx-0.6-r2-ui-measure-28288d02baa848fc868abec6bb607f4e',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$SetupPath='D:\ArchSift-lab\packages\draft-0.6-replacement-independent-20261011\ArchSift.Setup.exe',
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$FreezeRoot=Safe-Path $FreezeRoot;$UiRoot=Safe-Path $UiRoot;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$SetupPath=Safe-Path $SetupPath;$LabRoot=Safe-Path $LabRoot
$repoRoot=Safe-Path (Join-Path $PSScriptRoot '../..')
foreach($protected in @($repoRoot,$TargetRoot,$ExistingInstallRoot)){
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Lab root overlaps protected root.'}
}
function Original-State {
    $selection=Get-Content -LiteralPath (Safe-Path (Join-Path $ExistingInstallRoot 'install.json')) -Raw|ConvertFrom-Json
    if($selection.root-cne$ExistingInstallRoot-or$selection.selectedVersion-cne'0.5.0'){throw 'Original selection changed.'}
    $chosen=@($selection.versions|Where-Object version -eq '0.5.0')
    if($chosen.Count-ne1-or$chosen[0].manifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'){throw 'Original package identity changed.'}
    $records=[Collections.Generic.List[object]]::new()
    function Walk-Original([string]$Directory){
        foreach($path in [IO.Directory]::EnumerateFileSystemEntries($Directory)){
            [void](Safe-Path $path)
            if([IO.Directory]::Exists($path)){Walk-Original $path}else{
                $records.Add([pscustomobject][ordered]@{path=[IO.Path]::GetRelativePath($ExistingInstallRoot,$path);sha256=(Hash-File $path)})
                if($records.Count-gt10000){throw 'Original file-count limit.'}
            }
        }
    }
    Walk-Original $ExistingInstallRoot
    return @{selectedVersion=$selection.selectedVersion;configPath=$selection.configPath;filesSha256=(Hash-Json @($records|Sort-Object path));fileCount=$records.Count}
}
function Same-Target($A,$B){return $A.head-ceq$B.head-and$A.ordinaryStatusSha256-ceq$B.ordinaryStatusSha256-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
function Same-Original($A,$B){return $A.selectedVersion-ceq$B.selectedVersion-and$A.configPath-ceq$B.configPath-and$A.filesSha256-ceq$B.filesSha256-and$A.fileCount-eq$B.fileCount}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$originalBefore=$null;$completed=$false;$copy=$null;$plan=$null;$copyRoot=$null
$run=Safe-Path (Join-Path $LabRoot ('evidence/ifx-0.6-r2-lifecycle-'+[Guid]::NewGuid().ToString('N')))
[void][IO.Directory]::CreateDirectory($run)
function Run-Setup([string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.CreateNoWindow=$true
    foreach($arg in $Arguments){[void]$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'dotnet-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    [void][IO.Directory]::CreateDirectory($start.Environment['DOTNET_CLI_HOME'])
    $process=[Diagnostics.Process]::Start($start)
    try{
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw 'Setup planning timed out.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run 'adopt-plan.json'),$stdout.Result)
        [IO.File]::WriteAllText((Join-Path $run 'adopt-plan.stderr.txt'),$stderr.Result)
        if($process.ExitCode-ne0-or($stdout.Result+$stderr.Result).Contains('#session=')){throw 'Setup planning failed or leaked token.'}
        if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift after Setup plan.'}
        return $stdout.Result|ConvertFrom-Json
    }finally{$process.Dispose()}
}
try{
    $freezeResult=Get-Content -LiteralPath (Safe-Path (Join-Path $FreezeRoot 'result.json')) -Raw|ConvertFrom-Json
    $freeze=Get-Content -LiteralPath (Safe-Path (Join-Path $FreezeRoot 'frozen-input.json')) -Raw|ConvertFrom-Json
    $ui=Get-Content -LiteralPath (Safe-Path (Join-Path $UiRoot 'result.json')) -Raw|ConvertFrom-Json
    $uiAudit=Get-Content -LiteralPath (Safe-Path (Join-Path $UiRoot 'audit-errors.json')) -Raw|ConvertFrom-Json
    if((Hash-File (Join-Path $FreezeRoot 'frozen-input.json'))-cne'0ff6038c85d162506853d441b025e6cb4cb9a5b91e445123fe04ac46c407d1ab'-or(Hash-File (Join-Path $UiRoot 'result.json'))-cne'2a5eeeb888f65595de0ac695e5d6ceeb963b751e042503f5fd23a30242c1767a'-or(Hash-File (Join-Path $UiRoot 'measurements.json'))-cne'888dedaa3d9ec8ee89edef2dcf39a43ee23b82a6a76a52b3bbde901e1508f890'-or$freezeResult.status-cne'policy-frozen-for-review'-or$ui.status-cne'ui-measurements-for-review'-or$ui.completedCount-ne18-or-not$ui.ownedProcessStopped-or-not$ui.ownedListenersClosed-or-not$ui.hostStateEqual-or-not$ui.targetStateEqual-or-not$ui.originalInstallationStateEqual-or@($uiAudit).Count-ne0){throw 'Reviewed IFX gates are not clean.'}
    if((Hash-File $SetupPath)-cne'6327fcf10700aeec00f0b84da4201d4c3ad8b10d51d3f894766db4a2c55debab'){throw 'Setup asset changed.'}
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web','ArchSift.Setup' -ErrorAction SilentlyContinue)){
        try{$path=$running.Path;if(-not$path){throw 'Cannot inspect running ArchSift process.'};if($path.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original installation is running.'}}finally{$running.Dispose()}
    }
    $targetBefore=Target-State;$originalBefore=Original-State
    if(-not(Same-Target $targetBefore $freeze.target)-or-not(Same-Original $originalBefore $freeze.original)){throw 'Target or original changed since freeze.'}
    $copyRoot=Safe-Path (Join-Path $run 'installation')
    if(Test-Path -LiteralPath $copyRoot){throw 'Copy root already exists.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;original=$originalBefore;setupSha256=(Hash-File $SetupPath)}|ConvertTo-Json -Depth 10))
    $copy=& (Join-Path $PSScriptRoot '0.6-copy-original-state.ps1') -SourceInstallRoot $ExistingInstallRoot -SourceVersionDirectory (Join-Path $ExistingInstallRoot 'versions/0.5.0') -SourceConfigPath (Join-Path $ExistingInstallRoot 'config/ifx.json') -DestinationRoot $copyRoot
    [IO.File]::WriteAllText((Join-Path $run 'copy-summary.json'),($copy|ConvertTo-Json -Depth 10))
    if($copy.destinationRoot-cne$copyRoot-or$copy.manifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'-or$copy.payloadCount-ne626-or-not$copy.sourceStateEqual-or-not$copy.copyStateEqual-or$copy.targetEntry-cne'IFX.sln'){throw 'External copy identity differs.'}
    if(Test-Path -LiteralPath (Join-Path $copyRoot 'install.json')){throw 'Copy unexpectedly acquired Setup ownership.'}
    $plan=Run-Setup @('plan','adopt','--root',$copyRoot,'--version-directory',$copy.versionDirectory,'--config',$copy.configPath)
    if($plan.schemaVersion-ne1-or$plan.operation-cne'adopt'-or$plan.root-cne$copyRoot-or$plan.adoptedVersion.version-cne'0.5.0'-or$plan.adoptedVersion.manifestSha256-cne$copy.manifestSha256-or$plan.configPath-cne$copy.configPath-or$plan.config.target.entry-cne'IFX.sln'-or(Safe-Path $plan.config.target.root)-cne$TargetRoot-or$plan.config.build.mode-cne'existing'-or$plan.config.build.allowNetwork-or$plan.config.output.directory-cne(Join-Path $copyRoot 'reports')-or$plan.config.rulesDirectory-cne(Join-Path $copyRoot 'rules')-or[DateTimeOffset]::Parse($plan.expiresUtc)-le[DateTimeOffset]::UtcNow){throw 'Adopt plan differs from reviewed scope.'}
    if(-not(Same-Target (Target-State) $targetBefore)-or-not(Same-Original (Original-State) $originalBefore)){throw 'Protected state changed during copy/plan.'}
    $completed=$true
}catch{[IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;copyRoot=$copyRoot}|ConvertTo-Json));throw}
finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$originalEqual=$null;$audit=@()
    if($null-ne$targetBefore){try{$targetEqual=Same-Target (Target-State) $targetBefore}catch{$targetEqual=$false;$audit+=$_.Exception.Message}}
    if($null-ne$originalBefore){try{$originalEqual=Same-Original (Original-State) $originalBefore}catch{$originalEqual=$false;$audit+=$_.Exception.Message}}
    $result=@{status=$(if($completed-and$hostEqual-and$targetEqual-and$originalEqual){'external-copy-adopt-plan-ready'}else{'stop'});runRoot=$run;copyRoot=$copyRoot;planId=$plan.planId;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;originalInstallationStateEqual=$originalEqual;analysisOrBuildPerformed=$false;adoptApplied=$false;upgradeApplied=$false}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $audit));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or$targetEqual-eq$false-or$originalEqual-eq$false){throw 'Safety audit failed; preserve evidence.'}
}
