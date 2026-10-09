# Operator-only step 02. The agent must not execute this script for real IFX.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlanPath,
    [Parameter(Mandatory)][string]$ReviewedPlanId,
    [Parameter(Mandatory)][string]$ExpectedPlanFileSha256,
    [Parameter(Mandatory)][string]$SetupPath,
    [string]$ExpectedSetupSha256='f3eb9e4d166efc7c3ddb9bb738571016807f795b0cc68aeb6cb4f2668c5fad07',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$ExistingVersionDirectory='D:\IFX-10-Root\ArchSift\versions\0.4.0',
    [string]$ExpectedNewInstallRoot='D:\IFX-10-Root\ArchSift-0.5-review'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '0.5-operator-common.ps1')
$PlanPath=Safe-Path $PlanPath;$SetupPath=Safe-Path $SetupPath;$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$ExpectedNewInstallRoot=Safe-Path $ExpectedNewInstallRoot
$planRun=[IO.Path]::GetDirectoryName($PlanPath)
foreach($protected in @($TargetRoot,$ExistingInstallRoot,$ExpectedNewInstallRoot,[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))){
    if($planRun-eq$protected-or$planRun.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($planRun+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence and protected/installation roots overlap.'}
}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=$null;$existingBefore=$null
$run=Join-Path $planRun ('apply-'+[Guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory((Safe-Path $run))
$started=$false;$applied=$false;$inspectPassed=$false;$receiptId=$null;$receipt=$null
function Save-Before { [IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore;reviewedPlanId=$ReviewedPlanId;expectedPlanFileSha256=$ExpectedPlanFileSha256}|ConvertTo-Json -Depth 10)) }
function Run-Setup([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$script:started=$true;$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(-not$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw ('Setup '+$Name+' timed out; preserve owned journal/artifacts.')}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run ($Name+'.json')),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $run ($Name+'.stderr.log')),$stderr.Result)
        if(($stdout.Result+$stderr.Result).Contains('#session=')){throw 'Setup output contains a session token; preserve private evidence and stop.'}
        if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift after setup; no repair.'}
        if($process.ExitCode-ne0){throw ('Setup '+$Name+' stopped; exit '+$process.ExitCode+'. Preserve evidence; do not retry or clean automatically.')}
        return $stdout.Result|ConvertFrom-Json
    }finally{$process.Dispose()}
}
Save-Before
try{
    if((Hash-File $PlanPath)-cne$ExpectedPlanFileSha256-or(Hash-File $SetupPath)-cne$ExpectedSetupSha256){throw 'Reviewed plan/setup bytes changed.'}
    $prior=Get-Content -LiteralPath (Safe-Path (Join-Path $planRun 'result.json')) -Raw|ConvertFrom-Json
    $priorBefore=Get-Content -LiteralPath (Safe-Path (Join-Path $planRun 'before.json')) -Raw|ConvertFrom-Json
    $plan=Get-Content -LiteralPath $PlanPath -Raw|ConvertFrom-Json
    if($prior.status-cne'plan-ready-for-review'-or-not$prior.hostStateEqual-or-not$prior.targetStateEqual-or-not$prior.existing040StateEqual-or$prior.applied){throw 'Prior operator plan gate is not ready.'}
    if($plan.schemaVersion-ne1-or$plan.planId-cne$ReviewedPlanId-or$plan.operation-cne'install'-or$plan.root-cne$ExpectedNewInstallRoot-or$plan.config.target.root-cne$TargetRoot-or$plan.config.target.entry-cne'IFX.sln'-or$plan.config.build.mode-cne'existing'-or$plan.config.build.targetFramework-cne'net10.0'-or$plan.config.build.configuration-cne'Debug'-or$plan.config.build.allowNetwork-or$plan.config.rulesets.Count-ne0-or-not$plan.smoke){throw 'Plan differs from reviewed installation scope.'}
    if($plan.configPath-cne(Join-Path $ExpectedNewInstallRoot 'config/project.json')-or$plan.config.rulesDirectory-cne(Join-Path $ExpectedNewInstallRoot 'rules')-or$plan.config.output.directory-cne(Join-Path $ExpectedNewInstallRoot 'reports')){throw 'Plan state/output paths differ from review.'}
    if([DateTimeOffset]::Parse($plan.expiresUtc)-le[DateTimeOffset]::UtcNow){throw 'Reviewed plan expired; generate/review a new plan first.'}
    if($plan.package.version-cne'0.5.0'-or$plan.package.sourceCommit-cne'c4010d8c45eecadb3c51272334ceb4169e27b6ce'-or$plan.package.sha256-cne'9071b35036ea8d7b0a52e00bbd078b4de70ccc5b4a661a3f0daf10f5e6f9de85'-or(Hash-File $plan.package.path)-cne$plan.package.sha256){throw 'Candidate package identity changed.'}
    if(Test-Path -LiteralPath $ExpectedNewInstallRoot){throw 'New review root appeared; preserve it and stop.'}
    $targetBefore=Target-State;Save-Before
    $existingBefore=Existing-State;Save-Before
    if((Hash-Json $targetBefore)-cne(Hash-Json $priorBefore.target)-or(Hash-Json $existingBefore)-cne(Hash-Json $priorBefore.existing)){throw 'Target or original 0.4.0 changed since reviewed plan; stop for review.'}
    $receipt=Run-Setup 'receipt' @('apply','--plan',$PlanPath,'--plan-id',$ReviewedPlanId)
    if($receipt.schemaVersion-ne1-or$receipt.operation-cne'install'-or$receipt.planId-cne$ReviewedPlanId-or$receipt.outcome-cne'success'-or-not$receipt.hostStateEqual-or$receipt.after.root-cne$ExpectedNewInstallRoot-or$receipt.after.selectedVersion-cne'0.5.0'){throw 'Successful reviewed install receipt was not returned.'}
    $applied=$true;$receiptId=$receipt.id
    $inspection=Run-Setup 'inspect' @('inspect','--root',$ExpectedNewInstallRoot)
    if($inspection.status-cne'verified'-or$inspection.installation.lastOperation-cne$receiptId-or$inspection.installation.selectedVersion-cne'0.5.0'-or$inspection.state.sha256-cne$receipt.afterState.sha256){throw 'Installed manifest/state differs from receipt.'}
    $inspectPassed=$true
}catch{
    [IO.File]::WriteAllText((Join-Path $run 'stop.json'),(@{status='stop';message=$_.Exception.Message;candidateStarted=$started;receiptVerified=$applied}|ConvertTo-Json -Depth 5))
    throw
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=$null;$existingEqual=$null;$errors=[Collections.Generic.List[string]]::new()
    if($null-ne$targetBefore){try{$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore)}catch{$targetEqual=$false;$errors.Add($_.Exception.Message)}}
    if($null-ne$existingBefore){try{$existingEqual=(Hash-Json (Existing-State))-ceq(Hash-Json $existingBefore)}catch{$existingEqual=$false;$errors.Add($_.Exception.Message)}}
    $result=[ordered]@{status=$(if($applied-and$inspectPassed-and$hostEqual-and$targetEqual-and$existingEqual){'installed-private-smoke-passed'}else{'stop'});runRoot=$run;receiptId=$receiptId;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;applied=$applied;installedStateVerified=$inspectPassed;privateUiSmokeRequested=$true;analysisOrBuildPerformed=$false;nextGate='operator-native-cli-terminal'}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 6));[IO.File]::WriteAllText((Join-Path $run 'audit-errors.json'),(ConvertTo-Json -InputObject $errors.ToArray()));$result|ConvertTo-Json -Depth 6
    if(-not$hostEqual-or($null-ne$targetEqual-and-not$targetEqual)-or($null-ne$existingEqual-and-not$existingEqual)){throw 'Safety drift/audit failure; preserve evidence and stop. No repair.'}
}
