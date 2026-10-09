# Synthetic-only regression for the operator wrapper. No real IFX paths are used.
[CmdletBinding()]
param([string]$LabRoot='D:\ArchSift-lab')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/HostState.ps1')
$before=Get-ArchSiftHostHash
$id=[Guid]::NewGuid().ToString('N')
$fixtures=Join-Path $LabRoot ('fixtures/operator-layout-'+$id)
$evidence=Join-Path $LabRoot ('runs/operator-layout-'+$id)
$candidate=Join-Path $LabRoot 'packages/draft-0.5-accepted-candidate-f8be63c2c1bb4ce9ac44eb7b80bc24b5'
$oldZip=Join-Path $LabRoot 'packages/published-0.4.0-verify-20261009/archsift-0.4.0-win-x64.zip'
if((Get-FileHash -LiteralPath $oldZip -Algorithm SHA256).Hash.ToLowerInvariant()-cne'4f35dfdb0f9a14966b0d0d0c375e2c6d505d09c36f18ff673c4f686b72bba97c'){throw 'Old fixture package differs.'}
[void][IO.Directory]::CreateDirectory($evidence)
$checks=[Collections.Generic.List[object]]::new()
try{
    foreach($layout in @('flat','versioned','explicit-nested','missing','ambiguous')){
        $case=Join-Path $fixtures $layout;$target=Join-Path $case 'synthetic-target';$old=Join-Path $case 'synthetic-old';$new=Join-Path $case 'new-review';$caseEvidence=Join-Path $evidence $layout
        [void][IO.Directory]::CreateDirectory($target);[void][IO.Directory]::CreateDirectory((Join-Path $old 'config'));[void][IO.Directory]::CreateDirectory((Join-Path $old 'rules'))
        [IO.File]::WriteAllText((Join-Path $target 'IFX.sln'),'Microsoft Visual Studio Solution File, Format Version 12.00')
        & git -C $target init | Out-Null
        & git -C $target add .
        & git -C $target commit -m 'Synthetic operator layout fixture' -m 'Co-Authored-By: Codex <noreply@openai.com>' | Out-Null
        if($LASTEXITCODE-ne0){throw 'Synthetic fixture commit failed.'}
        $version=if($layout-eq'flat'){$old}elseif($layout-eq'explicit-nested'){Join-Path $old 'versions/0.4.0/extracted-package'}else{Join-Path $old 'versions/0.4.0'}
        if($layout-ne'missing'){[IO.Compression.ZipFile]::ExtractToDirectory($oldZip,$version)}
        if($layout-eq'ambiguous'){[IO.Compression.ZipFile]::ExtractToDirectory($oldZip,$old)}
        $config=@{schemaVersion=1;target=@{root=$target;entry='IFX.sln'};rulesets=@();build=@{mode='existing';targetFramework='net10.0';configuration='Debug';allowNetwork=$false};output=@{directory=(Join-Path $old 'reports');formats=@('json','html','sarif')};rulesDirectory=(Join-Path $old 'rules')}
        [IO.File]::WriteAllText((Join-Path $old 'config/ifx.json'),($config|ConvertTo-Json -Depth 8))
        $configHash=(Get-FileHash -LiteralPath (Join-Path $old 'config/ifx.json') -Algorithm SHA256).Hash
        $options=@{SetupPath=(Join-Path $candidate 'ArchSift.Setup.exe');ZipPath=(Join-Path $candidate 'archsift-0.5.0-win-x64.zip');ExpectedSetupSha256='f3eb9e4d166efc7c3ddb9bb738571016807f795b0cc68aeb6cb4f2668c5fad07';ExpectedZipSha256='9071b35036ea8d7b0a52e00bbd078b4de70ccc5b4a661a3f0daf10f5e6f9de85';ExpectedSourceCommit='c4010d8c45eecadb3c51272334ceb4169e27b6ce';TargetRoot=$target;ExistingInstallRoot=$old;NewInstallRoot=$new;LabRoot=$caseEvidence}
        if($layout-eq'explicit-nested'){$options.ExistingVersionDirectory=$version}
        $rejected=$false
        try{& (Join-Path $PSScriptRoot '20261010-0.5-operator-plan.ps1') @options | Out-Null}catch{$rejected=$true}
        $shouldReject=$layout-in@('missing','ambiguous')
        if($rejected-ne$shouldReject){throw ('Unexpected operator outcome: '+$layout)}
        $results=@(Get-ChildItem -LiteralPath $caseEvidence -Recurse -File -Filter 'result.json')
        if($results.Count-ne1){throw 'Missing initial-stop evidence.'}
        $result=Get-Content -LiteralPath $results[0].FullName -Raw|ConvertFrom-Json
        if(-not$result.hostStateEqual-or-not$result.targetStateEqual-or$result.applied-or$result.analysisOrBuildPerformed){throw 'Operator safety result mismatch.'}
        if($shouldReject){
            if($result.status-cne'stop'-or$null-ne$result.existing040StateEqual){throw 'Missing old-state baseline incorrectly reported equal.'}
            $stop=Get-Content -LiteralPath (Join-Path $results[0].DirectoryName 'stop.json') -Raw|ConvertFrom-Json
            if($stop.candidateStarted){throw 'Candidate started before old-state identity succeeded.'}
        }else{
            if($result.status-cne'plan-ready-for-review'-or-not$result.existing040StateEqual){throw 'Plan identity check failed.'}
            $baseline=Get-Content -LiteralPath (Join-Path $results[0].DirectoryName 'before.json') -Raw|ConvertFrom-Json
            if($baseline.existing.versionDirectory-cne[IO.Path]::GetFullPath($version)){throw 'Wrong binary root captured.'}
        }
        if((Test-Path -LiteralPath $new)-or(Get-FileHash -LiteralPath (Join-Path $old 'config/ifx.json') -Algorithm SHA256).Hash-cne$configHash){throw 'Fixture state changed.'}
        $checks.Add(@{layout=$layout;expectedReject=$shouldReject;pass=$true;runRoot=$result.runRoot})
        Write-Output ('operator-layout='+$layout+' PASS')
    }
}finally{
    $equal=(Get-ArchSiftHostHash)-ceq$before
    [IO.File]::WriteAllText((Join-Path $evidence 'checks.json'),(@{checks=$checks.ToArray();hostStateEqual=$equal;realIfxExecuted=$false}|ConvertTo-Json -Depth 7))
    Write-Output ('operator-layout-evidence='+$evidence+' host-state-equal='+$equal)
    if(-not$equal){throw 'Host drift; preserve evidence.'}
}
