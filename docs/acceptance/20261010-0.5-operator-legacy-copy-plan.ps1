# Operator-only step 04a: preserve real 0.4.0/IFX as read-only, copy saved state externally and plan adoption.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreviousRedirectRun,
    [string]$SetupPath='D:\ArchSift-lab\packages\draft-0.5-accepted-candidate-f8be63c2c1bb4ce9ac44eb7b80bc24b5\ArchSift.Setup.exe',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory='D:\IFX-10-Root\ArchSift\versions\0.4.0'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$PreviousRedirectRun=Safe-Path $PreviousRedirectRun;$ApplyRun=Safe-Path ([IO.Path]::GetDirectoryName($PreviousRedirectRun))
$SetupPath=Safe-Path $SetupPath;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$ExistingVersionDirectory=Safe-Path $ExistingVersionDirectory
foreach($protected in @($TargetRoot,$ExistingInstallRoot,[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))){
    if($ApplyRun-eq$protected-or$ApplyRun.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($ApplyRun+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/protected roots overlap.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null;$reportsBefore=$null;$planHash=$null;$completed=$false
$copy=[ordered]@{copyConfigSha256=$null;librarySha256=$null;reportsSha256=$null}
$plan=[ordered]@{planId=$null}
$run=Join-Path $ApplyRun ('legacy-copy-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory((Safe-Path $run))
$copyRoot=Safe-Path (Join-Path $run 'installation')
function Reports-State {
    $config=Get-Content -LiteralPath (Safe-Path (Join-Path $ExistingInstallRoot 'config/ifx.json')) -Raw|ConvertFrom-Json
    $reports=Safe-Path ([string]$config.output.directory)
    if(-not$reports.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original reports path escapes old installation.'}
    $records=@(Get-ChildItem -LiteralPath $reports -Recurse -File -Force|Sort-Object FullName|ForEach-Object{
        $file=Safe-Path $_.FullName
        if(-not$file.StartsWith($reports+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original report escapes root.'}
        [ordered]@{path=[IO.Path]::GetRelativePath($reports,$file);length=$_.Length;sha256=(Hash-File $file)}
    })
    return [ordered]@{sha256=(Hash-Json $records);fileCount=$records.Count}
}
function Run-Setup([string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'setup-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    [IO.File]::WriteAllText((Join-Path $run 'setup-owner.json'),(@{pid=$process.Id;executable=$SetupPath;operation='plan adopt'}|ConvertTo-Json))
    try{
        if(-not$process.WaitForExit(120000)){throw 'Setup plan timed out; preserve operator evidence.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run 'adopt-plan.json'),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $run 'adopt-plan.stderr.log'),$stderr.Result)
        if(($stdout.Result+$stderr.Result).Contains('#session=')){throw 'Setup plan leaked a session token.'}
        if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift after Setup plan; preserve evidence.'}
        if($process.ExitCode-ne0){throw ('Setup adopt plan stopped with exit '+$process.ExitCode+'.')}
        return $stdout.Result|ConvertFrom-Json
    }finally{$process.Dispose()}
}
try{
    if((Hash-File (Join-Path $PreviousRedirectRun 'result.json'))-cne'7792f5fc8b6a7b05f7a28aa606cb354f9721eab69835f6e151c179d20ac48611'){throw 'Reviewed pipe/file evidence changed.'}
    $previous=Get-Content -LiteralPath (Safe-Path (Join-Path $PreviousRedirectRun 'result.json')) -Raw|ConvertFrom-Json
    $previousBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $PreviousRedirectRun 'before.json')) -Raw|ConvertFrom-Json
    if($previous.runRoot-cne$PreviousRedirectRun-or$previous.status-cne'native-cli-pipe-file-passed'-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.existing040StateEqual-or-not$previous.newConfigEqual-or-not$previous.newPayloadEqual-or$previous.nextGate-cne'operator-external-copy-upgrade-rollback'){throw 'Previous operator gate is not ready.'}
    if((Hash-File $SetupPath)-cne'f3eb9e4d166efc7c3ddb9bb738571016807f795b0cc68aeb6cb4f2668c5fad07'){throw 'Setup asset changed.'}
    $targetBefore=Target-State;$existingBefore=Existing-State;$reportsBefore=Reports-State
    if((Hash-Json $targetBefore)-cne(Hash-Json $previousBefore.target)-or(Hash-Json $existingBefore)-cne(Hash-Json $previousBefore.existing)){throw 'IFX target/original installation changed since pipe/file check.'}
    if($existingBefore.libraryFileCount-ne2-or$reportsBefore.fileCount-ne6){throw 'Original saved-state file counts changed; review before copying.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore;reports=$reportsBefore;previousResultSha256='7792f5fc8b6a7b05f7a28aa606cb354f9721eab69835f6e151c179d20ac48611'}|ConvertTo-Json -Depth 9))
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        $image=$running.Path
        if(-not$image){throw ('Cannot inspect an ArchSift process before state copy; review PID '+$running.Id)}
        if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('Original installation is running; close it before state copy. PID '+$running.Id)}
    }
    $copy=& (Join-Path $PSScriptRoot '0.5-copy-legacy-state.ps1') -SourceInstallRoot $ExistingInstallRoot -SourceVersionDirectory $ExistingVersionDirectory -SourceConfigPath (Join-Path $ExistingInstallRoot 'config/ifx.json') -DestinationRoot $copyRoot
    [IO.File]::WriteAllText((Join-Path $run 'copy-summary.json'),($copy|ConvertTo-Json -Depth 8))
    if($copy.destinationRoot-cne$copyRoot-or$copy.sourceConfigSha256-cne$existingBefore.configSha256-or$copy.manifestSha256-cne$existingBefore.manifestSha256-or$copy.payloadCount-ne621-or$copy.libraryFileCount-ne2-or$copy.reportFileCount-ne6-or$copy.reportsSha256-cne$reportsBefore.sha256-or-not$copy.sourceStateEqual-or-not$copy.copyStateEqual){throw 'External copy identity differs from original baseline.'}
    if(Test-Path -LiteralPath (Join-Path $copyRoot 'install.json')){throw 'Copy unexpectedly acquired setup ownership.'}
    $plan=Run-Setup @('plan','adopt','--root',$copyRoot,'--version-directory',$copy.versionDirectory,'--config',$copy.configPath)
    $planHash=Hash-File (Join-Path $run 'adopt-plan.json')
    if($plan.schemaVersion-ne1-or$plan.operation-cne'adopt'-or$plan.root-cne$copyRoot-or$plan.adoptedVersion.version-cne'0.4.0'-or$plan.adoptedVersion.directory-cne$copy.versionDirectory-or$plan.adoptedVersion.manifestSha256-cne$copy.manifestSha256-or$plan.configPath-cne$copy.configPath-or$plan.config.target.entry-cne'IFX.sln'-or(Safe-Path $plan.config.target.root)-cne$TargetRoot-or$plan.config.build.mode-cne'existing'-or$plan.config.build.targetFramework-cne'net10.0'-or$plan.config.build.configuration-cne'Debug'-or$plan.config.build.allowNetwork-or$plan.config.rulesets.Count-ne0-or$plan.config.output.directory-cne(Join-Path $copyRoot 'reports')-or$plan.config.rulesDirectory-cne(Join-Path $copyRoot 'rules')){throw 'Adopt plan differs from reviewed external-copy scope.'}
    if([DateTimeOffset]::Parse($plan.expiresUtc)-le[DateTimeOffset]::UtcNow-or-not$plan.planId){throw 'Adopt plan is expired/invalid.'}
    if(Test-Path -LiteralPath (Join-Path $copyRoot 'install.json')){throw 'Read-only adopt plan created ownership.'}
    if((Hash-Json (Target-State))-cne(Hash-Json $targetBefore)-or(Hash-Json (Existing-State))-cne(Hash-Json $existingBefore)-or(Hash-Json (Reports-State))-cne(Hash-Json $reportsBefore)){throw 'Original target/install/reports changed during copy/plan.'}
    $completed=$true
}catch{
    [IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;copyRoot=$copyRoot}|ConvertTo-Json));throw
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$existingEqual=$null;$reportsEqual=$null;$auditErrors=[Collections.Generic.List[string]]::new()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$existingBefore){try{$existingEqual=(Hash-Json (Existing-State))-ceq(Hash-Json $existingBefore)}catch{$existingEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$reportsBefore){try{$reportsEqual=(Hash-Json (Reports-State))-ceq(Hash-Json $reportsBefore)}catch{$reportsEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    $result=[ordered]@{status=$(if($completed-and$hostEqual-and$targetEqual-and$existingEqual-and$reportsEqual){'external-copy-adopt-plan-ready'}else{'stop'});runRoot=$run;copyRoot=$copyRoot;planId=$plan.planId;planFileSha256=$planHash;copyConfigSha256=$copy.copyConfigSha256;sourceLibrarySha256=$copy.librarySha256;sourceReportsSha256=$copy.reportsSha256;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;existingReportsEqual=$reportsEqual;adoptApplied=$false;upgradeApplied=$false;analysisOrBuildPerformed=$false;nextGate='operator-external-copy-adopt-apply'}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors.ToArray()));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or($null-ne$targetEqual-and-not$targetEqual)-or($null-ne$existingEqual-and-not$existingEqual)-or($null-ne$reportsEqual-and-not$reportsEqual)){throw 'Safety drift/audit failure; preserve evidence and stop. No repair.'}
}
