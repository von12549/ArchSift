[CmdletBinding()]
param([Parameter(Mandatory)][string]$SetupPath,[Parameter(Mandatory)][string]$ZipPath,[Parameter(Mandatory)][string]$OldZipPath,[string]$LabRoot='D:\ArchSift-lab')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('packages/setup-smoke-'+[Guid]::NewGuid().ToString('N'))
if($run.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Smoke artifacts must be external.'}
[void][IO.Directory]::CreateDirectory($run)
$checks=[Collections.Generic.List[object]]::new()
$setup=[IO.Path]::GetFullPath($SetupPath)
$zip=[IO.Path]::GetFullPath($ZipPath)
$zipHash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$oldZip=[IO.Path]::GetFullPath($OldZipPath)
if((Get-FileHash -LiteralPath $oldZip -Algorithm SHA256).Hash.ToLowerInvariant()-cne'4f35dfdb0f9a14966b0d0d0c375e2c6d505d09c36f18ff673c4f686b72bba97c'){throw 'Old fixture ZIP differs from frozen official 0.4.0.'}
function Invoke-Setup([string]$Name,[string[]]$Arguments,[int]$Expected=0){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$setup;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    # Preserve inherited PATH. Invalid explicit dotnet roots/host detect fallback without environment repair.
    $start.Environment['DOTNET_ROOT']=(Join-Path $run 'no-dotnet');$start.Environment['DOTNET_HOST_PATH']=(Join-Path $run 'no-dotnet/dotnet.exe');$start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(-not$process.WaitForExit(120000)){$process.Kill($true);$process.WaitForExit();throw 'Setup smoke timed out.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run ($Name+'.stdout.log')),$stdout.Result);[IO.File]::WriteAllText((Join-Path $run ($Name+'.stderr.log')),$stderr.Result)
        $equal=(Get-ArchSiftHostHash)-ceq$before
        $checks.Add(@{name=$Name;exitCode=$process.ExitCode;expected=$Expected;hostStateEqual=$equal})
        if(-not$equal){throw 'Host drift; preserve evidence.'}
        if($process.ExitCode-ne$Expected){Write-Output $stderr.Result;throw ('Setup smoke failed: '+$Name)}
        if(($stdout.Result+$stderr.Result).Contains('#session=')){throw 'Setup leaked a UI session token.'}
        Write-Host ($Name+' PASS');return $stdout.Result
    }finally{$process.Dispose()}
}
function Save-Plan([string]$Name,[string]$Text){$path=Join-Path $run ($Name+'.json');[IO.File]::WriteAllText($path,$Text,[Text.UTF8Encoding]::new($false));return $path}
function User-StateHash([string]$Config,[string]$Library){
    $records=@([ordered]@{path='config.json';sha256=(Get-FileHash -LiteralPath $Config -Algorithm SHA256).Hash.ToLowerInvariant()})
    if(Test-Path -LiteralPath $Library){$records+=@(Get-ChildItem -LiteralPath $Library -Recurse -File|Sort-Object FullName|ForEach-Object{[ordered]@{path=[IO.Path]::GetRelativePath($Library,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})}
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($records|ConvertTo-Json -Depth 5 -Compress)))).ToLowerInvariant()
}
try{
    if((Invoke-Setup 'setup-version' @('--version')).Trim()-cne'archsift-setup 0.5.0'){throw 'Setup version mismatch.'}
    $notices=Invoke-Setup 'embedded-notices' @('--notices')
    if(-not$notices.Contains('THIRD-PARTY-NOTICES')-or-not$notices.Contains('ArchSift-LICENSE')){throw 'Standalone Setup notices incomplete.'}
    $target=Join-Path $run 'synthetic-source';[void][IO.Directory]::CreateDirectory($target)
    [IO.File]::WriteAllText((Join-Path $target 'Synthetic.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
    $targetHash=(Get-FileHash -LiteralPath (Join-Path $target 'Synthetic.csproj') -Algorithm SHA256).Hash
    $newRoot=Join-Path $run 'fresh-install'
    $text=Invoke-Setup 'plan-install' @('plan','install','--root',$newRoot,'--package',$zip,'--sha256',$zipHash,'--target',$target,'--entry','Synthetic.csproj','--tfm','net10.0','--smoke','true')
    if(Test-Path -LiteralPath $newRoot){throw 'Read-only plan created root.'}
    $plan=$text|ConvertFrom-Json;$path=Save-Plan 'install-plan' $text
    $install=(Invoke-Setup 'apply-install-and-private-ui-smoke' @('apply','--plan',$path,'--plan-id',$plan.planId))|ConvertFrom-Json
    if($install.after.selectedVersion-cne'0.5.0'-or$install.outcome-cne'success'){throw 'Install selection mismatch.'}
    [void](Invoke-Setup 'inspect-install' @('inspect','--root',$newRoot))
    [void](Invoke-Setup 'stale-install-rejected' @('apply','--plan',$path,'--plan-id',$plan.planId) 2)
    if(Test-Path -LiteralPath (Join-Path $newRoot 'reports')){throw 'Setup performed analysis.'}
    $legacy=Join-Path $run 'legacy-install';$oldVersion=Join-Path $legacy 'old-payload'
    [void][IO.Directory]::CreateDirectory($oldVersion);[IO.Compression.ZipFile]::ExtractToDirectory($oldZip,$oldVersion)
    $library=Join-Path $legacy 'rules';[void][IO.Directory]::CreateDirectory((Join-Path $library 'chains'))
    Copy-Item -LiteralPath (Join-Path $oldVersion 'templates/rules/naming.json') -Destination (Join-Path $library 'policy.json')
    $active='11111111111111111111111111111111';$deleted='22222222222222222222222222222222'
    [IO.File]::WriteAllText((Join-Path $library '.archsift-library.json'),(@{schemaVersion=1;entries=@(@{entryId=$active;fileName='policy.json';rulesetId='template-naming';deleted=$false},@{entryId=$deleted;fileName='deleted.json';rulesetId='deleted-policy';deleted=$true})}|ConvertTo-Json -Depth 6))
    [IO.File]::WriteAllText((Join-Path $library '.archsift-library.lock'),'')
    [IO.File]::WriteAllText((Join-Path $library 'chains/retained.json'),(@{schemaVersion=1;id='retained';version='1';description='Synthetic dangling reference';entries=@(@{entryId=$active},@{entryId=$deleted})}|ConvertTo-Json -Depth 6))
    $configPath=Join-Path $legacy 'config.json'
    [IO.File]::WriteAllText($configPath,(@{schemaVersion=1;target=@{root=$target;entry='Synthetic.csproj'};rulesets=@();build=@{mode='existing';targetFramework='net10.0';configuration='Debug';allowNetwork=$false};output=@{directory=(Join-Path $legacy 'reports');formats=@('json','html','sarif')};rulesDirectory=$library}|ConvertTo-Json -Depth 8))
    $stateBefore=User-StateHash $configPath $library
    $text=Invoke-Setup 'plan-adopt' @('plan','adopt','--root',$legacy,'--version-directory',$oldVersion,'--config',$configPath)
    $plan=$text|ConvertFrom-Json;$path=Save-Plan 'adopt-plan' $text
    [void](Invoke-Setup 'apply-adopt' @('apply','--plan',$path,'--plan-id',$plan.planId))
    $text=Invoke-Setup 'plan-upgrade' @('plan','upgrade','--root',$legacy,'--package',$zip,'--sha256',$zipHash,'--smoke','true')
    $plan=$text|ConvertFrom-Json;$path=Save-Plan 'upgrade-plan' $text
    $upgrade=(Invoke-Setup 'apply-upgrade-and-private-ui-smoke' @('apply','--plan',$path,'--plan-id',$plan.planId))|ConvertFrom-Json
    if((User-StateHash $configPath $library)-cne$stateBefore){throw 'Upgrade changed user state bytes/entry IDs/tombstones/chains.'}
    $text=Invoke-Setup 'plan-current-version' @('plan','upgrade','--root',$legacy,'--package',$zip,'--sha256',$zipHash)
    $plan=$text|ConvertFrom-Json;$path=Save-Plan 'noop-plan' $text
    if(((Invoke-Setup 'checked-no-op' @('apply','--plan',$path,'--plan-id',$plan.planId))|ConvertFrom-Json).outcome-cne'no-op'){throw 'Repeat version not a checked no-op.'}
    $selection=Join-Path $legacy 'install.json';$selectionHash=(Get-FileHash -LiteralPath $selection -Algorithm SHA256).Hash.ToLowerInvariant()
    $rollback=(Invoke-Setup 'receipt-rollback' @('rollback','--root',$legacy,'--receipt',$upgrade.id,'--selection-sha256',$selectionHash))|ConvertFrom-Json
    if($rollback.after.selectedVersion-cne'0.4.0'-or(User-StateHash $configPath $library)-cne$stateBefore){throw 'Rollback failed version/state preservation.'}
    if((Get-FileHash -LiteralPath (Join-Path $target 'Synthetic.csproj') -Algorithm SHA256).Hash-cne$targetHash){throw 'Synthetic target changed.'}
    if(Test-Path -LiteralPath (Join-Path $legacy 'reports')){throw 'Setup performed analysis.'}
    $residual=@(Get-Process -Name 'archsift','ArchSift.Web' -ErrorAction SilentlyContinue|Where-Object {$_.Path-and$_.Path.StartsWith($run+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)})
    if($residual.Count){throw 'Setup owned process remains.'}
    $result=@{status='pass';version='0.5.0';checks=$checks.ToArray();zipSha256=$zipHash;setupSha256=(Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant();stateSha256=$stateBefore;targetUnchanged=$true;noOwnedProcess=$true;hostStateEqual=((Get-ArchSiftHostHash)-ceq$before);invalidDotnetRootsUsed=$true;processPathPreserved=$true;limits=@('Synthetic targets only; no real IFX installation.', 'Host has SDKs; unavailable explicit dotnet paths test self-contained fallback, not a physically SDK-free host.')}
    [IO.File]::WriteAllText((Join-Path $run 'setup-smoke-result.json'),($result|ConvertTo-Json -Depth 8));$result|ConvertTo-Json -Depth 8
}finally{$equal=(Get-ArchSiftHostHash)-ceq$before;Write-Output ('setup-smoke-run='+$run+' final-host-state-equal='+$equal);if(-not$equal){throw 'Host drift; preserve evidence.'}}
