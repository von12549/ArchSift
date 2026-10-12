[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CliPath,
    [Parameter(Mandatory)][string]$CliSha256,
    [Parameter(Mandatory)][string]$EvidenceRunRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
$before=Get-ArchSiftHostHash
$cli=[IO.Path]::GetFullPath($CliPath)
$evidence=[IO.Path]::GetFullPath($EvidenceRunRoot)
if(!$evidence.StartsWith('D:\ArchSift-lab\runs\ifx-external-0.7-',[StringComparison]::OrdinalIgnoreCase)) { throw 'Reviewed external evidence root required.' }
$run='D:\ArchSift-lab\runs\ifx-bound-0.7-'+[Guid]::NewGuid().ToString('N')
[void][IO.Directory]::CreateDirectory($run)
$preflight=Get-Content -Raw 'D:\ArchSift-lab\evidence\ifx-0.7-preflight-527f4137255440f3b682467a9fbbf0ec\result.json'|ConvertFrom-Json
$sources=Get-Content -Raw (Join-Path $evidence 'source-files.json')|ConvertFrom-Json
$checks=[Collections.Generic.List[object]]::new()
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Save([string]$name,$value) { [IO.File]::WriteAllText((Join-Path $run $name),($value|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false)) }
function CheckOriginal {
    if($CliSha256 -notmatch '^[a-f0-9]{64}$' -or (Hash $cli) -cne $CliSha256) { throw 'Frozen CLI bytes changed.' }
    foreach($file in $preflight.original.frozenFiles) { if((Hash $file.path) -cne $file.sha256) { throw 'Original IFX installation/state changed.' } }
    if((& git -C $preflight.target.root rev-parse HEAD).Trim() -cne $preflight.target.commit -or (& git -C $preflight.target.root status --porcelain)) { throw 'Original IFX source changed.' }
    foreach($file in $sources) { if((Hash (Join-Path $preflight.target.root $file.path)) -cne $file.sha256) { throw 'Original IFX input bytes changed.' } }
}
function RunCli([string]$name,[string[]]$arguments) {
    CheckOriginal
    $exe=if([IO.Path]::GetExtension($cli) -eq '.dll'){'C:\Program Files\dotnet\dotnet.exe'}else{$cli}
    $psi=[Diagnostics.ProcessStartInfo]::new($exe);$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.WorkingDirectory=$run;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
    if($exe -ne $cli) { $psi.ArgumentList.Add($cli) }
    foreach($arg in $arguments) { $psi.ArgumentList.Add($arg) }
    $psi.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home';$psi.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $p=[Diagnostics.Process]::Start($psi);$timer=[Diagnostics.Stopwatch]::StartNew()
    try {
        $out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync()
        if(!$p.WaitForExit(600000)) { $p.Kill($true);$p.WaitForExit();throw 'Owned CLI timed out; preserve evidence.' }
        [Threading.Tasks.Task]::WaitAll($out,$err)
        [IO.File]::WriteAllText((Join-Path $run ($name+'.stdout.json')),$out.Result,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $run ($name+'.stderr.log')),$err.Result,[Text.UTF8Encoding]::new($false))
        $equal=(Get-ArchSiftHostHash) -ceq $before
        $checks.Add([ordered]@{name=$name;arguments=$arguments;exitCode=$p.ExitCode;elapsedMilliseconds=$timer.ElapsedMilliseconds;hostStateEqual=$equal})
        Save 'checks.json' $checks.ToArray()
        CheckOriginal
        if(!$equal -or $p.ExitCode -ne $(if($name -eq 'validate'){0}else{4})) { throw 'Bound IFX gate failed; inspect retained logs.' }
        Write-Host "$name exit=$($p.ExitCode) host-state-equal=$equal"
        return ($out.Result|ConvertFrom-Json)
    } finally { $p.Dispose() }
}
try {
    CheckOriginal
    Copy-Item -LiteralPath 'D:\IFX-10-Root\ArchSift\rules' -Destination (Join-Path $run 'rules') -Recurse
    $config=Get-Content -Raw (Join-Path $evidence 'config-bound.json')|ConvertFrom-Json
    $config.rulesDirectory=Join-Path $run 'rules';$config.output.directory=Join-Path $run 'reports'
    Save 'config.json' $config
    $configPath=Join-Path $run 'config.json'
    $validated=RunCli 'validate' @('evidence','validate','--config',$configPath)
    if(!$validated.sourceBound -or !$validated.coverageComplete -or $validated.contexts.Count -ne 105 -or $validated.limitations.Count) { throw 'Incomplete current-source binding.' }
    $b=RunCli 'class-B' @('chain','verify','--config',$configPath,'--chain',(Join-Path $run 'rules/chains/ifx-06-class-B-test.json'),'--target-kind','real')
    $a=RunCli 'class-A' @('chain','verify','--config',$configPath,'--chain',(Join-Path $run 'rules/chains/ifx-04-class-A-test.json'),'--target-kind','real')
    if($b.entries.Count -ne 1 -or $a.entries.Count -ne 4) { throw 'Real chain membership changed.' }
    foreach($summary in @($b,$a)) {
        if($summary.execution -cne 'partial' -or $summary.compliance -cne 'compliant' -or $summary.exitCode -ne 4) { throw 'Declaration-model limitation semantics changed.' }
        foreach($entry in $summary.entries) {
            if(!$entry.coverage.sourceBound -or $entry.coverage.assemblyCount -ne 105 -or $entry.coverage.projectCount -ne 105 -or @($entry.ruleResults|Where-Object {$_.status -cne 'pass'}).Count) { throw 'Unexpected rule result/coverage.' }
        }
    }
    if($b.entries[0].ruleResults.Count -ne 406 -or @($b.entries[0].ruleResults|Where-Object {$_.matched -lt 1}).Count -or @($a.entries|ForEach-Object ruleResults).Count -ne 182) { throw 'Real rule count/matches changed.' }
    $children=@(Get-ChildItem -LiteralPath (Join-Path $run 'reports') -Recurse -File -Filter report.json)
    if($children.Count -ne 5) { throw 'Expected five fresh child reports.' }
    foreach($file in $children) {
        $report=Get-Content -Raw $file.FullName|ConvertFrom-Json
        if($report.executionErrors.Count -or $report.findings.Count -or $report.buildContext.assemblyBindingMode -cne 'project-contexts' -or $report.buildContext.evidenceOrigin -cne 'external-reviewed') { throw 'Child report errors, findings or evidence identity changed.' }
    }
    $schemas=Join-Path $PSScriptRoot '../../schemas'
    foreach($file in $children) {
        if(!(Test-Json -Json (Get-Content -Raw $file.FullName) -SchemaFile (Join-Path $schemas 'report.schema.json'))) { throw 'Real report contract failed.' }
    }
    $summaries=@(Get-ChildItem -LiteralPath (Join-Path $run 'reports') -Recurse -File -Filter chain-summary.json)
    foreach($file in $summaries) {
        if(!(Test-Json -Json (Get-Content -Raw $file.FullName) -SchemaFile (Join-Path $schemas 'chain-summary-v2.schema.json'))) { throw 'Real chain contract failed.' }
    }
    $sarif=@(Get-ChildItem -LiteralPath (Join-Path $run 'reports') -Recurse -File -Filter '*.sarif')
    if($sarif.Count -ne 7) { throw 'Expected five child and two chain SARIF outputs.' }
    foreach($file in $sarif) {
        if(!(Test-Json -Json (Get-Content -Raw $file.FullName) -SchemaFile 'D:\ArchSift-lab\evidence\sarif-2.1.0\sarif-schema-2.1.0.json')) { throw 'Official SARIF contract failed.' }
    }
    Save 'result.json' ([ordered]@{status='pass';cliPath=$cli;cliSha256=$CliSha256;externalEvidenceRoot=$evidence;sourceBound=$true;contexts=105;bPass=406;aPass=182;execution='partial';exitCode=4;reportContractCount=$children.Count;chainContractCount=$summaries.Count;sarifContractCount=$sarif.Count;checks=$checks.ToArray();reports=@($children|ForEach-Object{[ordered]@{path=$_.FullName;sha256=(Hash $_.FullName)}});originalUnchanged=$true;hostStateEqual=((Get-ArchSiftHostHash) -ceq $before)})
} finally {
    $after=Get-ArchSiftHostHash
    Save 'host-final.json' ([ordered]@{beforeSha256=$before;afterSha256=$after;equal=($before -ceq $after)})
    Write-Output "bound-reports-root=$run host-state-equal=$($before -ceq $after)"
    if($before -cne $after) { throw 'Host drift; no repair.' }
}
