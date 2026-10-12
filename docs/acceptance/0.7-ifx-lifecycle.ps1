param(
    [string]$CandidateRoot='D:\ArchSift-lab\packages\release-9404fe66144347649c5a35fe3a16d7d1',
    [string]$CandidateSha256='bbddb2f46a05447909293818648a96b254cc7d352cffb91b942d95e65303e8bf'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
$before=Get-ArchSiftHostHash
$run='D:\ArchSift-lab\runs\ifx-lifecycle-0.7-'+[Guid]::NewGuid().ToString('N')
$installation=Join-Path $run 'review-install'
$oldPayload=Join-Path $installation 'old-payload'
$setup=Join-Path $CandidateRoot 'setup\ArchSift.Setup.exe'
$zip=Join-Path $CandidateRoot 'archsift-0.7.0-win-x64.zip'
$oldZip='D:\ArchSift-lab\packages\published-0.6.0-verify-a3b40851fa864c04a594cabeac2cd3a0\archsift-0.6.0-win-x64.zip'
$checks=[Collections.Generic.List[object]]::new()
$preflight=Get-Content -Raw 'D:\ArchSift-lab\evidence\ifx-0.7-preflight-527f4137255440f3b682467a9fbbf0ec\result.json' | ConvertFrom-Json
function Hash([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant() }
function Save([string]$name,$value) { [IO.File]::WriteAllText((Join-Path $run $name),($value|ConvertTo-Json -Depth 25),[Text.UTF8Encoding]::new($false)) }
function CheckOriginal {
    foreach($file in $preflight.original.frozenFiles) { if((Hash $file.path) -ne $file.sha256) { throw 'Original installation state drift.' } }
    if((& git -C $preflight.target.root status --porcelain) -or (& git -C $preflight.target.root rev-parse HEAD).Trim() -ne $preflight.target.commit) { throw 'Original source drift.' }
}
function UserState {
    $items=@(Get-ChildItem -LiteralPath (Join-Path $installation 'config'),(Join-Path $installation 'rules') -File -Recurse | Sort-Object FullName | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($installation,$_.FullName);sha256=(Hash $_.FullName)} })
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($items|ConvertTo-Json -Depth 8 -Compress)))).ToLowerInvariant()
}
function InvokeSetup([string]$name,[string[]]$arguments) {
    CheckOriginal
    $psi=[Diagnostics.ProcessStartInfo]::new($setup);$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
    foreach($arg in $arguments) { $psi.ArgumentList.Add($arg) }
    $psi.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
    $psi.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $p=[Diagnostics.Process]::Start($psi)
    try {
        $out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync()
        if(!$p.WaitForExit(180000)) { $p.Kill($true);$p.WaitForExit();throw 'Owned Setup timed out; preserve evidence.' }
        [Threading.Tasks.Task]::WaitAll($out,$err)
        [IO.File]::WriteAllText((Join-Path $run ($name+'.stdout.json')),$out.Result,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $run ($name+'.stderr.log')),$err.Result,[Text.UTF8Encoding]::new($false))
        $equal=(Get-ArchSiftHostHash) -ceq $before
        $checks.Add([ordered]@{name=$name;arguments=$arguments;exitCode=$p.ExitCode;hostStateEqual=$equal})
        Save 'checks.json' $checks.ToArray()
        CheckOriginal
        Write-Host "$name exit=$($p.ExitCode) host-state-equal=$equal"
        if(!$equal -or $p.ExitCode -ne 0) { throw 'Lifecycle gate failed; inspect retained result.' }
        return ($out.Result|ConvertFrom-Json)
    } finally { $p.Dispose() }
}
function ApplyPlan([string]$name,$plan) {
    $file=Join-Path $run ($name+'-plan.json')
    # Preserve the exact machine plan; PowerShell date reserialization changes its reviewed identity.
    [IO.File]::Copy((Join-Path $run ($name+'-plan.stdout.json')),$file,$false)
    InvokeSetup ($name+'-apply') @('apply','--plan',$file,'--plan-id',$plan.planId)
}
try {
    CheckOriginal
    if($CandidateSha256 -notmatch '^[a-f0-9]{64}$' -or (Hash $zip) -ne $CandidateSha256 -or (Hash $oldZip) -ne '64af0fb70ea8548b99373151c7005c15b0365784d8faedd3890989d3d040a560') { throw 'Frozen package mismatch.' }
    [void][IO.Directory]::CreateDirectory($oldPayload)
    [IO.Compression.ZipFile]::ExtractToDirectory($oldZip,$oldPayload)
    $library=Join-Path $installation 'rules'
    Copy-Item -LiteralPath 'D:\IFX-10-Root\ArchSift\rules' -Destination $library -Recurse
    [void][IO.Directory]::CreateDirectory((Join-Path $installation 'config'))
    $config=Get-Content -Raw 'D:\IFX-10-Root\ArchSift\config\ifx.json'|ConvertFrom-Json
    $config.output.directory=Join-Path $installation 'reports';$config.rulesDirectory=$library
    $configPath=Join-Path $installation 'config\ifx.json';Save 'review-install\config\ifx.json' $config
    $adoptPlan=InvokeSetup 'adopt-plan' @('plan','adopt','--root',$installation,'--version-directory',$oldPayload,'--config',$configPath)
    $adopt=ApplyPlan 'adopt' $adoptPlan
    if($adopt.after.selectedVersion -ne '0.6.0') { throw 'Adopted version mismatch.' }
    $stateBefore=UserState
    $upgradePlan=InvokeSetup 'upgrade-plan' @('plan','upgrade','--root',$installation,'--package',$zip,'--sha256',(Hash $zip),'--smoke','true')
    $upgrade=ApplyPlan 'upgrade' $upgradePlan
    if($upgrade.after.selectedVersion -ne '0.7.0' -or (UserState) -ne $stateBefore) { throw 'Real copied state changed on upgrade.' }
    $noopPlan=InvokeSetup 'noop-plan' @('plan','upgrade','--root',$installation,'--package',$zip,'--sha256',(Hash $zip))
    $noop=ApplyPlan 'noop' $noopPlan
    if($noop.outcome -ne 'no-op' -or (UserState) -ne $stateBefore) { throw 'No-op changed state.' }
    $rollback=InvokeSetup 'rollback' @('rollback','--root',$installation,'--receipt',$upgrade.id,'--selection-sha256',(Hash (Join-Path $installation 'install.json')))
    if($rollback.after.selectedVersion -ne '0.6.0' -or (UserState) -ne $stateBefore) { throw 'Rollback changed real copied state.' }
    $selectPlan=InvokeSetup 'reselect-plan' @('plan','upgrade','--root',$installation,'--package',$zip,'--sha256',(Hash $zip),'--smoke','true')
    $selected=ApplyPlan 'reselect' $selectPlan
    if($selected.after.selectedVersion -ne '0.7.0' -or (UserState) -ne $stateBefore) { throw 'Reselection changed state.' }
    [void](InvokeSetup 'inspect' @('inspect','--root',$installation))
    if(Test-Path -LiteralPath (Join-Path $installation 'reports')) { throw 'Setup ran analysis.' }
    $residual=@(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue | Where-Object {$_.Path -and $_.Path.StartsWith($run+'\',[StringComparison]::OrdinalIgnoreCase)})
    if($residual.Count) { throw 'Owned process remains.' }
    Save 'result.json' ([ordered]@{status='pass';scope='Real IFX metadata and rule/config copies in an external review installation; original not upgraded';targetCommit=$preflight.target.commit;selectedVersion='0.7.0';stateSha256=$stateBefore;statePreserved=$true;originalUnchanged=$true;noAnalysisPerformed=$true;noOwnedProcess=$true;hostStateEqual=((Get-ArchSiftHostHash) -ceq $before);checks=$checks.ToArray()})
} finally {
    if(Test-Path -LiteralPath $run) { Save 'host-final.json' ([ordered]@{beforeSha256=$before;afterSha256=(Get-ArchSiftHostHash);equal=((Get-ArchSiftHostHash) -ceq $before)}) }
    Write-Output "lifecycle-root=$run"
    if((Get-ArchSiftHostHash) -cne $before) { throw 'Host drift; retain evidence.' }
}
