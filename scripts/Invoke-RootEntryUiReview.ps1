#Requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$RepositoryRoot,[Parameter(Mandatory)][string]$ExpectedCommit,
      [Parameter(Mandatory)][string]$PackageSource,[Parameter(Mandatory)][string]$DotnetPath,
      [Parameter(Mandatory)][string]$StorageRoot,[Parameter(Mandatory)][string]$EvidenceRoot,
      [Parameter(Mandatory)][string]$NativeQualificationReport,[Parameter(Mandatory)][int]$ExpectedRulesets,
      [Parameter(Mandatory)][int]$ExpectedCsharpCases,[Parameter(Mandatory)][int]$ExpectedJsonCases)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$repo=[IO.Path]::GetFullPath($RepositoryRoot)
Import-Module (Join-Path $repo 'eng/ArchSift.Companion.psm1') -Force
Import-Module (Join-Path $repo 'eng/ArchSift.CloneAcceptance.psm1') -Force
Import-Module (Join-Path $repo 'eng/ArchSift.Host.psm1') -Force
. (Join-Path $repo 'eng/HostSafety.ps1');$baseline=Get-PortableHostHash
$storage=Get-CompanionPath $StorageRoot;$evidence=Get-CompanionPath $EvidenceRoot;$state=$evidence+'.state'
foreach($path in @($repo,$storage,$PackageSource,$DotnetPath)){Assert-CompanionDisjoint $evidence $path;Assert-CompanionDisjoint $state $path}
Assert-CompanionDisjoint $storage $repo;Assert-CompanionDisjoint $storage $PackageSource
foreach($path in @($storage,$evidence,$state)){Assert-Companion (-not(Test-Path -LiteralPath $path)) 'Use fresh owned storage/evidence; preserve all prior attempts.'}
$native=Read-CompanionJson $NativeQualificationReport
Assert-Companion ($native.status -ceq 'PASSED' -and $native.hostEqual) 'Complete sequential native qualification is required before UI review.'
$source=Read-CompanionJson (Join-Path (Split-Path $NativeQualificationReport) 'source-inputs.json')
Assert-Companion ($source.baseCommit -ceq $ExpectedCommit -and -not $source.dirty) 'Native qualification source differs.'
foreach($path in @($evidence,$state)){[void][IO.Directory]::CreateDirectory($path)}
$nonce=[Guid]::NewGuid().ToString('N');$stages=[Collections.Generic.List[object]]::new();$status='FAIL';$failure=$null
$approvalHash=Get-CloneAcceptanceHash (Join-Path $repo 'architecture/package-approval.json')
$plan=Get-IfxHostPlan $repo $storage $PackageSource $DotnetPath
function Save([string]$Name,$Value){$Value|ConvertTo-Json -Depth 40|Set-Content (Join-Path $evidence $Name) -Encoding utf8NoBOM}
function Check {Assert-Companion ((Get-PortableHostHash) -ceq $baseline) 'Host drift; stop without repair.';& (Join-Path $repo 'eng/Test-ArchSiftCloneInputs.ps1') -ExpectedCommit $ExpectedCommit -RequireClean|Out-Null}
try{
    foreach($mode in @('Launcher','Workbench','Launcher','Workbench')){
        Check;$id='startup-'+($stages.Count+1)+'-'+$mode.ToLowerInvariant();$ui=Join-Path $evidence ('ui/'+$id);[void][IO.Directory]::CreateDirectory($ui)
        $request=@{schemaVersion=1;kind='ifx-archsift-ui-request';runNonce=$nonce;stage=$id;candidateCommit=$ExpectedCommit;packageApprovalSha256=$approvalHash;checkout=$repo;mode=$mode;actor='agent';humanPerformed=$false;requiredObservations=@('project-and-rules','daily-inventory','no-automatic-test-execution','safe-close');expected=@{rulesets=$ExpectedRulesets;csharp=$ExpectedCsharpCases;json=$ExpectedJsonCases};storageScenario='explicit-owned-root-created-by-root-entry';privateLogs=(Join-Path $state ($id+'.stdout.private.log'));receiptPath=(Join-Path $ui 'receipt.json')}
        Save ('ui/'+$id+'/request.json') $request;$request.requestSha256=Get-CloneAcceptanceHash (Join-Path $ui 'request.json');Save 'active-ui-request.json' $request
        Write-Output ('AGENT UI REQUEST: '+(Join-Path $evidence 'active-ui-request.json'))
        $arguments=@('-Action','Start','-Mode',$mode,'-StorageRoot',$storage,'-CandidateSource',$PackageSource,'-DotnetPath',$DotnetPath,'-PowerShellPath',(Join-Path $PSHOME 'pwsh.exe'))
        $row=Invoke-CloneAcceptanceChild -Name $id -Executable (Join-Path $repo 'archsift.cmd') -Arguments $arguments -WorkingDirectory $repo -LogRoot $state -TimeoutSeconds 900 -CmdEntry
        $row|Add-Member hostEqual ((Get-PortableHostHash) -ceq $baseline);$stages.Add($row);Save 'stages.json' $stages.ToArray()
        Assert-Companion ($row.hostEqual -and -not $row.timedOut -and $row.exitCode -eq 0) 'Root-entry UI child failed; preserve private logs.'
        $deadline=[DateTimeOffset]::UtcNow.AddSeconds(60)
        while(-not(Test-Path -LiteralPath $request.receiptPath) -and [DateTimeOffset]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 250}
        Assert-Companion (Test-Path -LiteralPath $request.receiptPath) 'Actual Agent observation is required; child exit alone is not PASS.'
        $null=Test-CloneUiReceipt $request (Read-CompanionJson $request.receiptPath) $evidence
        $markers=@(Get-CompanionFiles $plan.Revision|Where-Object Name -CEQ 'workspace-state.json');Assert-Companion ($markers.Count -eq 1) 'Generated workspace count differs.'
        $generated=Join-Path $markers[0].DirectoryName 'config/workbench.json';$configuration=Read-CompanionJson $generated
        $policy=Read-CompanionJson (Join-Path $repo 'architecture/archsift-policy.json');$expected=@($policy.rulesets|ForEach-Object {[IO.Path]::GetFullPath((Join-Path $repo $_.path))})
        Assert-Companion ($expected.Count -eq $ExpectedRulesets -and ($expected|ConvertTo-Json -Compress) -ceq (@($configuration.rulesets)|ConvertTo-Json -Compress)) 'Generated startup rules differ.'
        Copy-Item -LiteralPath $generated -Destination (Join-Path $ui 'generated-workbench.json')
    }
    Check;$status='PASS'
}catch{$failure=$_.Exception.Message;Write-Warning $failure}
finally{
    try{
        Check;$outer=0;$product=0;[void][IO.Directory]::CreateDirectory((Join-Path $evidence 'host-closeouts'))
        $closeouts=@(Get-CompanionFiles $plan.Revision|Where-Object Name -CEQ 'closeout.json')
        for($i=0;$i -lt $closeouts.Count;$i++){$row=Read-CompanionJson $closeouts[$i].FullName;Copy-Item $closeouts[$i].FullName (Join-Path $evidence ('host-closeouts/'+$i+'.json'));if($row.PSObject.Properties.Name -contains 'action'){Assert-Companion ($row.success -and $row.hostEqual -and -not $row.activationPerformed) 'Root closeout differs.';$outer++}else{Assert-Companion ($row.exitCode -eq 0 -and $row.hostEqual) 'Product closeout differs.';$product++}}
        if($status -ceq 'PASS'){Assert-Companion ($outer -eq 4 -and $product -eq 4) 'Four paired closeouts are required.'}
        foreach($lock in @(Get-CompanionFiles $plan.Revision|Where-Object Extension -CEQ '.lock')){$lease=[IO.File]::Open($lock.FullName,'Open','ReadWrite','None');$lease.Dispose()}
    }catch{$status='FAIL';$failure=(@($failure,$_.Exception.Message)|Where-Object {$_}) -join '; closeout: '}
    Save 'summary.json' @{schemaVersion=1;kind='archsift-root-entry-ui-review';status=$status;actor='agent';agent='Codex';humanPerformed=$false;checkout=$repo;candidateCommit=$ExpectedCommit;packageApprovalSha256=$approvalHash;runNonce=$nonce;stages=$stages.ToArray();hostEqual=((Get-PortableHostHash) -ceq $baseline);storageRoot=$storage;storageScenario='explicit-owned-root-initially-absent';defaultStorageTestedHere=$false;nativeQualificationSha256=(Get-CloneAcceptanceHash $NativeQualificationReport);failure=$failure;publicationPerformed=$false}
    Save 'manifest.json' @{schemaVersion=1;files=@(Get-CompanionFiles $evidence|ForEach-Object {@{path=[IO.Path]::GetRelativePath($evidence,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-CloneAcceptanceHash $_.FullName)}})}
    [IO.Compression.ZipFile]::CreateFromDirectory($evidence,$evidence+'.zip')
    [IO.File]::WriteAllText($evidence+'.zip.sha256',(Get-CloneAcceptanceHash ($evidence+'.zip'))+'  '+[IO.Path]::GetFileName($evidence)+".zip`n")
    Write-Output ('Root UI review '+$status+': '+$evidence+'.zip')
}
if($status -cne 'PASS'){exit 1}
