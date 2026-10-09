# Operator-only step 04b: apply reviewed adoption to the external copy, then plan (do not apply) 0.5.0 upgrade.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CopyPlanRun,
    [string]$SetupPath='D:\ArchSift-lab\packages\draft-0.5-accepted-candidate-f8be63c2c1bb4ce9ac44eb7b80bc24b5\ArchSift.Setup.exe',
    [string]$ZipPath='D:\ArchSift-lab\packages\draft-0.5-accepted-candidate-f8be63c2c1bb4ce9ac44eb7b80bc24b5\archsift-0.5.0-win-x64.zip',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory='D:\IFX-10-Root\ArchSift\versions\0.4.0'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$CopyPlanRun=Safe-Path $CopyPlanRun;$SetupPath=Safe-Path $SetupPath;$ZipPath=Safe-Path $ZipPath
$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$ExistingVersionDirectory=Safe-Path $ExistingVersionDirectory
if($CopyPlanRun-cne'D:\ArchSift-lab\evidence\ifx-0.5-plan-acc5c1fb295a4aac9530e67c66577d3c\apply-05d9f6e06ee74ac8b231634aff54acda\legacy-copy-5435ee29964b4516aefb29f1f32d4e9c'){throw 'Only the reviewed external-copy evidence root is allowed.'}
$copyRoot=Safe-Path (Join-Path $CopyPlanRun 'installation')
foreach($protected in @($TargetRoot,$ExistingInstallRoot,[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))){
    if($CopyPlanRun-eq$protected-or$CopyPlanRun.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($CopyPlanRun+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Copy evidence/protected roots overlap.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null;$reportsBefore=$null;$copyBefore=$null;$receiptId=$null;$selectionHash=$null;$upgradePlanHash=$null;$adopted=$false;$completed=$false
$upgradePlan=[ordered]@{planId=$null}
$run=Join-Path $CopyPlanRun ('adopt-apply-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory((Safe-Path $run))
function Tree-State([string]$Root){
    $Root=Safe-Path $Root
    if(-not[IO.Directory]::Exists($Root)){throw 'Expected state directory missing.'}
    $records=[Collections.Generic.List[object]]::new()
    foreach($item in @(Get-ChildItem -LiteralPath $Root -Recurse -Force)){
        $path=Safe-Path $item.FullName
        if(-not$path.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'State item escapes root.'}
        if($item.PSIsContainer){continue}
        if(-not($item -is [IO.FileInfo])){throw 'Unsupported state item.'}
        $records.Add([ordered]@{path=[IO.Path]::GetRelativePath($Root,$path);length=$item.Length;sha256=(Hash-File $path)})
        if($records.Count-gt10000){throw 'State file-count limit.'}
    }
    $ordered=@($records|Sort-Object path)
    return [ordered]@{sha256=(Hash-Json $ordered);fileCount=$ordered.Count}
}
function Original-Reports {
    $config=Get-Content -LiteralPath (Safe-Path (Join-Path $ExistingInstallRoot 'config/ifx.json')) -Raw|ConvertFrom-Json
    $path=Safe-Path ([string]$config.output.directory)
    if(-not$path.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Original reports path escapes installation.'}
    return Tree-State $path
}
function Copy-State {
    $config=Safe-Path (Join-Path $copyRoot 'config/ifx.json');$library=Safe-Path (Join-Path $copyRoot 'rules');$reports=Safe-Path (Join-Path $copyRoot 'reports')
    $manifest=Safe-Path (Join-Path $copyRoot 'versions/0.4.0/package-manifest.json')
    return [ordered]@{configSha256=(Hash-File $config);library=(Tree-State $library);reports=(Tree-State $reports);manifestSha256=(Hash-File $manifest)}
}
function Run-Setup([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'setup-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    [IO.File]::WriteAllText((Join-Path $run ($Name+'-owner.json')),(@{pid=$process.Id;executable=$SetupPath;operation=$Name}|ConvertTo-Json))
    try{
        if(-not$process.WaitForExit(180000)){throw ('Setup '+$Name+' timed out; preserve owner/journal evidence.')}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run ($Name+'.json')),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $run ($Name+'.stderr.log')),$stderr.Result)
        if(($stdout.Result+$stderr.Result).Contains('#session=')){throw 'Setup leaked a UI session token.'}
        if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift after Setup; preserve evidence.'}
        if($process.ExitCode-ne0){throw ('Setup '+$Name+' stopped with exit '+$process.ExitCode+'.')}
        return $stdout.Result|ConvertFrom-Json
    }finally{$process.Dispose()}
}
try{
    if((Hash-File (Join-Path $CopyPlanRun 'result.json'))-cne'9f9d3f77431d073800a25f7b1ca0c9756b5681a0f1289322f0d5b6a06ff53511'-or(Hash-File (Join-Path $CopyPlanRun 'adopt-plan.json'))-cne'9921ac788fe415556b3bf3919d70517365d2dc47391b6fc6ec009824801ef7e5'-or(Hash-File (Join-Path $CopyPlanRun 'copy-summary.json'))-cne'2748682337dfc21d7c3073541626d85f1afb6fc0d8c95c86f77a40dd67d1dd42'){throw 'Reviewed copy/plan evidence changed.'}
    if((Hash-File $SetupPath)-cne'f3eb9e4d166efc7c3ddb9bb738571016807f795b0cc68aeb6cb4f2668c5fad07'-or(Hash-File $ZipPath)-cne'9071b35036ea8d7b0a52e00bbd078b4de70ccc5b4a661a3f0daf10f5e6f9de85'){throw 'Setup/ZIP asset changed.'}
    $previous=Get-Content -LiteralPath (Safe-Path (Join-Path $CopyPlanRun 'result.json')) -Raw|ConvertFrom-Json
    $previousBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $CopyPlanRun 'before.json')) -Raw|ConvertFrom-Json
    $copySummary=Get-Content -LiteralPath (Safe-Path (Join-Path $CopyPlanRun 'copy-summary.json')) -Raw|ConvertFrom-Json
    $planPath=Safe-Path (Join-Path $CopyPlanRun 'adopt-plan.json');$plan=Get-Content -LiteralPath $planPath -Raw|ConvertFrom-Json
    if($previous.status-cne'external-copy-adopt-plan-ready'-or-not$previous.hostStateEqual-or-not$previous.targetStateEqual-or-not$previous.existing040StateEqual-or-not$previous.existingReportsEqual-or$previous.adoptApplied-or$previous.upgradeApplied-or$previous.nextGate-cne'operator-external-copy-adopt-apply'-or$previous.copyRoot-cne$copyRoot){throw 'Previous external-copy gate is not ready.'}
    if($plan.schemaVersion-ne1-or$plan.planId-cne'09ab270957afd8f66908da4132df1f293677580cdb5b2b077dd6946ba4d9b47a'-or$plan.operation-cne'adopt'-or$plan.root-cne$copyRoot-or$plan.package-or$plan.selectionSha256-or$plan.smoke-or$plan.adoptedVersion.version-cne'0.4.0'-or$plan.adoptedVersion.directory-cne(Join-Path $copyRoot 'versions/0.4.0')-or$plan.adoptedVersion.manifestSha256-cne'd35fea42475633eb290c858e4df4aec4387dad639d2d03480d20e9edd532849c'-or$plan.configPath-cne(Join-Path $copyRoot 'config/ifx.json')-or$plan.config.target.entry-cne'IFX.sln'-or(Safe-Path $plan.config.target.root)-cne$TargetRoot-or$plan.config.build.mode-cne'existing'-or$plan.config.build.targetFramework-cne'net10.0'-or$plan.config.build.configuration-cne'Debug'-or$plan.config.build.allowNetwork-or$plan.config.rulesets.Count-ne0-or$plan.config.output.directory-cne(Join-Path $copyRoot 'reports')-or$plan.config.rulesDirectory-cne(Join-Path $copyRoot 'rules')){throw 'Adopt plan differs from reviewed copy scope.'}
    if([DateTimeOffset]::Parse($plan.expiresUtc)-le[DateTimeOffset]::UtcNow){throw 'Reviewed adopt plan expired; regenerate/review before applying.'}
    if(Test-Path -LiteralPath (Join-Path $copyRoot 'install.json')){throw 'External copy was already adopted; preserve it for review.'}
    $targetBefore=Target-State;$existingBefore=Existing-State;$reportsBefore=Original-Reports;$copyBefore=Copy-State
    if((Hash-Json $targetBefore)-cne(Hash-Json $previousBefore.target)-or(Hash-Json $existingBefore)-cne(Hash-Json $previousBefore.existing)-or(Hash-Json $reportsBefore)-cne(Hash-Json $previousBefore.reports)){throw 'IFX/original installation/reports changed since copy plan.'}
    if($copyBefore.configSha256-cne$copySummary.copyConfigSha256-or$copyBefore.library.sha256-cne$copySummary.librarySha256-or$copyBefore.library.fileCount-ne2-or$copyBefore.reports.sha256-cne$copySummary.reportsSha256-or$copyBefore.reports.fileCount-ne6-or$copyBefore.manifestSha256-cne$copySummary.manifestSha256-or$plan.state.sha256-cne'd5f2d02476a88821d8a9e623164a5cc88fdc0f5beffeb5e8f6a0eb62d823c858'){throw 'External copy state differs from reviewed plan.'}
    [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore;originalReports=$reportsBefore;copy=$copyBefore;reviewedPlanId=$plan.planId;reviewedPlanSha256='9921ac788fe415556b3bf3919d70517365d2dc47391b6fc6ec009824801ef7e5'}|ConvertTo-Json -Depth 10))
    foreach($running in @(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue)){
        $image=$running.Path
        if(-not$image){throw ('Cannot inspect an ArchSift process; review PID '+$running.Id)}
        if($image.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$image.StartsWith($copyRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('Original/copy installation is running; review PID '+$running.Id)}
    }
    $receipt=Run-Setup 'adopt-receipt' @('apply','--plan',$planPath,'--plan-id',$plan.planId)
    if($receipt.schemaVersion-ne1-or$receipt.operation-cne'adopt'-or$receipt.planId-cne$plan.planId-or$receipt.outcome-cne'success'-or-not$receipt.hostStateEqual-or$receipt.before-or$receipt.after.root-cne$copyRoot-or$receipt.after.selectedVersion-cne'0.4.0'-or$receipt.afterState.sha256-cne$plan.state.sha256){throw 'Adopt receipt differs from reviewed plan.'}
    $adopted=$true;$receiptId=$receipt.id
    $inspect=Run-Setup 'adopt-inspect' @('inspect','--root',$copyRoot)
    if($inspect.status-cne'verified'-or$inspect.installation.lastOperation-cne$receiptId-or$inspect.installation.selectedVersion-cne'0.4.0'-or$inspect.state.sha256-cne$plan.state.sha256-or$inspect.pendingOperations.Count-ne0){throw 'Adopted copy inspection differs from receipt.'}
    $selectionHash=Hash-File (Join-Path $copyRoot 'install.json')
    if($inspect.selectionSha256-cne$selectionHash){throw 'Adopted copy selection differs from inspect.'}
    if((Hash-Json (Copy-State))-cne(Hash-Json $copyBefore)){throw 'Adoption changed copied user state/package.'}
    $upgradePlan=Run-Setup 'upgrade-plan' @('plan','upgrade','--root',$copyRoot,'--package',$ZipPath,'--sha256','9071b35036ea8d7b0a52e00bbd078b4de70ccc5b4a661a3f0daf10f5e6f9de85','--smoke','true')
    $upgradePlanHash=Hash-File (Join-Path $run 'upgrade-plan.json')
    if($upgradePlan.schemaVersion-ne1-or$upgradePlan.operation-cne'upgrade'-or$upgradePlan.root-cne$copyRoot-or$upgradePlan.selectionSha256-cne$selectionHash-or$upgradePlan.configPath-cne$plan.configPath-or(Hash-Json $upgradePlan.config)-cne(Hash-Json $plan.config)-or$upgradePlan.state.sha256-cne$plan.state.sha256-or-not$upgradePlan.smoke-or$upgradePlan.package.version-cne'0.5.0'-or$upgradePlan.package.sourceCommit-cne'c4010d8c45eecadb3c51272334ceb4169e27b6ce'-or$upgradePlan.package.sha256-cne'9071b35036ea8d7b0a52e00bbd078b4de70ccc5b4a661a3f0daf10f5e6f9de85'-or$upgradePlan.package.manifestSha256-cne'b7a7b7508ba887eaf3b1a3208cf924c47266148bbb41c5c4bb593da4a791b225'-or$upgradePlan.package.path-cne$ZipPath-or$upgradePlan.config.build.allowNetwork-or$upgradePlan.config.rulesets.Count-ne0){throw 'Upgrade plan differs from reviewed copy and candidate identity.'}
    if([DateTimeOffset]::Parse($upgradePlan.expiresUtc)-le[DateTimeOffset]::UtcNow-or-not$upgradePlan.planId){throw 'Upgrade plan expired/invalid.'}
    if(Test-Path -LiteralPath (Join-Path $copyRoot 'versions/0.5.0')){throw 'Read-only upgrade plan created a new version.'}
    if((Hash-Json (Copy-State))-cne(Hash-Json $copyBefore)-or(Hash-File (Join-Path $copyRoot 'install.json'))-cne$selectionHash){throw 'Copy selection/user state changed during upgrade planning.'}
    $completed=$true
}catch{
    [IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;copyRoot=$copyRoot;adopted=$adopted}|ConvertTo-Json));throw
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$existingEqual=$null;$reportsEqual=$null;$copyEqual=$null;$auditErrors=[Collections.Generic.List[string]]::new()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$existingBefore){try{$existingEqual=(Hash-Json (Existing-State))-ceq(Hash-Json $existingBefore)}catch{$existingEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$reportsBefore){try{$reportsEqual=(Hash-Json (Original-Reports))-ceq(Hash-Json $reportsBefore)}catch{$reportsEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    if($null-ne$copyBefore){try{$copyEqual=(Hash-Json (Copy-State))-ceq(Hash-Json $copyBefore)}catch{$copyEqual=$false;$auditErrors.Add($_.Exception.Message)}}
    $passed=$completed-and$hostEqual-and$targetEqual-and$existingEqual-and$reportsEqual-and$copyEqual
    $result=[ordered]@{status=$(if($passed){'external-copy-adopted-upgrade-plan-ready'}else{'stop'});runRoot=$run;copyRoot=$copyRoot;adoptReceiptId=$receiptId;selectionSha256=$selectionHash;upgradePlanId=$upgradePlan.planId;upgradePlanFileSha256=$upgradePlanHash;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;existingReportsEqual=$reportsEqual;copyUserStateEqual=$copyEqual;adoptApplied=$adopted;upgradeApplied=$false;analysisOrBuildPerformed=$false;nextGate=$(if($passed){'operator-external-copy-upgrade-apply'}else{$null})}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $auditErrors.ToArray()));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or($null-ne$targetEqual-and-not$targetEqual)-or($null-ne$existingEqual-and-not$existingEqual)-or($null-ne$reportsEqual-and-not$reportsEqual)-or($null-ne$copyEqual-and-not$copyEqual)){throw 'Safety drift/audit failure; preserve evidence and stop. No repair.'}
}
