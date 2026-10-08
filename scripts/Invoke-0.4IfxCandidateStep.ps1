# Operator-only 0.4-F step 2. One native legacy verify; no build/restore/UI/policy adoption.
[CmdletBinding()]
param(
    [string]$DiscoveryRunRoot='D:\ArchSift-lab\runs\ifx-discovery-d0a6f85954dd49719a3c63c2a9b7a9f5',
    [string]$PackageRoot='D:\ArchSift-lab\packages\smoke-963a5161af604fb8902fa1415b6aac00\unpacked',
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$RulesPath='D:\IFX-10-Root\archsift-lab\runs\ifx-030-d46915bff2c74f17bacde44f8d232289\rules\production-candidate\v1-bdc137efc83748fb97f1e68637600447\ifx-protection.json',
    [string]$LabRoot='D:\ArchSift-lab',
    [switch]$ValidateOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if(-not$IsWindows-or$PSVersionTable.PSVersion.Major-lt7){throw 'Windows PowerShell 7 with -NoProfile is required.'}
. (Join-Path $PSScriptRoot 'HostState.ps1')
$target=[IO.Path]::GetFullPath($TargetRoot)
$package=[IO.Path]::GetFullPath($PackageRoot)
$policy=[IO.Path]::GetFullPath($RulesPath)
$prior=[IO.Path]::GetFullPath($DiscoveryRunRoot)
$lab=[IO.Path]::GetFullPath($LabRoot)
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$run=Join-Path $lab ('runs/ifx-0.4-candidate-'+[guid]::NewGuid().ToString('N'))
$expectedHead='64ef2674c57e6cf9031481d6d4410cca55b0dad5'
$expectedRulesHash='48495a266a1d8444ca0cd8cdeb6e5f3023f294ca70b30e020bf5c4a5131fbda2'
$expectedManifestHash='1a997d3b26de2ae64de5def4b5a9075f970b19b2ab2643e2e669683dd612bb5f'
$git=(Get-Command git -ErrorAction Stop).Source

function Is-Under([string]$Path,[string]$Root){$relative=[IO.Path]::GetRelativePath($Root,$Path);return $relative-eq'.'-or(-not[IO.Path]::IsPathRooted($relative)-and$relative-ne'..'-and-not$relative.StartsWith('..'+[IO.Path]::DirectorySeparatorChar,[StringComparison]::Ordinal))}
function No-Links([string]$Path){$cursor=[IO.Path]::GetFullPath($Path);while($cursor){if([IO.File]::Exists($cursor)-or[IO.Directory]::Exists($cursor)){if(([IO.File]::GetAttributes($cursor)-band[IO.FileAttributes]::ReparsePoint)-ne0){throw 'Link/reparse path rejected.'}};$cursor=[IO.Path]::GetDirectoryName($cursor)}}
function Sha([string]$Path){No-Links $Path;(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Save-Json([string]$Path,$Value){[IO.File]::WriteAllText($Path,(ConvertTo-Json -InputObject $Value -Depth 30)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))}
function Git-Text([string[]]$Arguments){$value=@(& $git -c core.fsmonitor=false -C $target @Arguments);if($LASTEXITCODE-ne0){throw 'Read-only target Git metadata failed.'};return ($value-join"`n").TrimEnd()}
function Source-Path([string]$Relative){if([IO.Path]::IsPathRooted($Relative)-or$Relative.Replace('\','/').Split('/')-contains'..'-or$Relative.StartsWith('@')){throw 'Invalid source record path.'};$path=[IO.Path]::GetFullPath((Join-Path $target $Relative));if(-not(Is-Under $path $target)){throw 'Source record escaped target.'};No-Links $path;return $path}
function Check-Source($Files){foreach($file in $Files){$path=Source-Path $file.path;if((Sha $path)-cne$file.sha256-or(Get-Item -LiteralPath $path).Length-ne$file.length){throw 'Accepted discovery source bytes changed.'}}}
function Check-Package($Manifest){$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase);foreach($file in $Manifest.entries){$path=[IO.Path]::GetFullPath((Join-Path $package $file.path));if(-not(Is-Under $path $package)-or$path-eq$package-or-not$seen.Add($file.path)-or(Sha $path)-cne$file.sha256-or(Get-Item -LiteralPath $path).Length-ne$file.bytes){throw 'Candidate payload identity/path changed.'}};if(@(Get-ChildItem -LiteralPath $package -File -Recurse).Count-ne$Manifest.entries.Count+1){throw 'Candidate payload file set changed.'}}

foreach($path in @($target,$package,$policy,$prior,$lab)){No-Links $path}
if((Is-Under $lab $target)-or(Is-Under $target $lab)-or(Is-Under $lab $repo)-or(Is-Under $repo $lab)-or(Is-Under $policy $target)-or(Is-Under $package $target)){throw 'Lab/package/policy overlaps a source root.'}
$priorResultPath=Join-Path $prior 'operator-result.json'
if((Sha $priorResultPath)-cne'bedbf98f5fa966c63be8bd5c9e748122255c608e207daf821f4b4ece113ed0e0'){throw 'Reviewed discovery result changed.'}
$priorResult=Get-Content -LiteralPath $priorResultPath -Raw|ConvertFrom-Json
if(-not$priorResult.hostStateEqual-or-not$priorResult.targetHeadEqual-or-not$priorResult.ordinaryStatusEqual-or$priorResult.exitCode-ne4-or$priorResult.targetHead-cne$expectedHead-or$priorResult.targetBuildInvoked-or$priorResult.restoreInvoked){throw 'Discovery gate is not accepted.'}
$discoveryPath=Join-Path $prior 'reports/ee6381379c1c43abbb7b95f3b53d720c/report.json'
if((Sha $discoveryPath)-cne'bd4bc01586263f339213ef9a28f23c22b622a8d0da6b13319fe725abf808053c'){throw 'Reviewed discovery report changed.'}
$discovery=Get-Content -LiteralPath $discoveryPath -Raw|ConvertFrom-Json -Depth 80
if($discovery.toolVersion-cne'0.4.0'-or$discovery.operation-cne'analyze'-or$discovery.coverage.projectCount-ne105-or$discovery.inputIdentity.files.Count-ne563){throw 'Discovery identity/scope changed.'}
$manifestPath=Join-Path $package 'package-manifest.json'
if((Sha $manifestPath)-cne$expectedManifestHash){throw 'Independently downloaded candidate manifest changed.'}
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json -Depth 20
if($manifest.version-cne'0.4.0'-or$manifest.sourceCommit-cne'061a53dfbbe5f67a3c339b4530288c4ca79c3adf'-or$manifest.productSourceDirty){throw 'Candidate source/version changed.'}
Check-Package $manifest
if((Sha $policy)-cne$expectedRulesHash){throw 'Original candidate rules changed.'}
$rules=Get-Content -LiteralPath $policy -Raw|ConvertFrom-Json -Depth 80
if($rules.rules.Count-ne182-or@($rules.rules|Where-Object enabled).Count-ne182-or$rules.exceptions.Count-ne0){throw 'Reviewed 182-rule candidate scope changed.'}
$headBefore=Git-Text @('rev-parse','HEAD')
$statusBefore=Git-Text @('status','--porcelain=v1','--untracked-files=all')
if($headBefore-cne$expectedHead-or$statusBefore){throw 'IFX checkout differs from the reviewed clean identity; preserve changes and stop.'}
Check-Source $discovery.inputIdentity.files
$hostBefore=Get-ArchSiftHostHash
if($ValidateOnly){[ordered]@{step='ifx-0.4-candidate-preflight';status='metadata-pass';nativeProcessStarted=$false;targetHead=$headBefore;sourceRecordsChecked=$discovery.inputIdentity.files.Count;ruleCount=$rules.rules.Count;packageEntriesChecked=$manifest.entries.Count;hostSha256=$hostBefore}|ConvertTo-Json;return}
[void][IO.Directory]::CreateDirectory($run)
$configuration=[ordered]@{schemaVersion=1;target=@{root=$target;entry='IFX.sln'};rulesets=@($policy);build=@{mode='existing';targetFramework='net10.0';configuration='Debug';allowNetwork=$false;assemblyPaths=@()};output=@{directory=(Join-Path $run 'reports');formats=@('json','html','sarif')}}
$configPath=Join-Path $run 'config.json';Save-Json $configPath $configuration
$configHash=Sha $configPath
Save-Json (Join-Path $run 'before.json') ([ordered]@{hostSha256=$hostBefore;targetHead=$headBefore;targetStatusSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($statusBefore))).ToLowerInvariant();rulesSha256=$expectedRulesHash;manifestSha256=$expectedManifestHash;discoverySourceInputSha256=$discovery.inputIdentity.sha256;discoveryReportSha256=(Sha $discoveryPath);scriptSha256=(Sha $PSCommandPath)})
$start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=Join-Path $package 'archsift.exe';$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
foreach($argument in @('verify','--config',$configPath)){$start.ArgumentList.Add($argument)}
$start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1';$start.Environment['DOTNET_CLI_UI_LANGUAGE']='en-US'
$process=$null;$operationError=$null;$exitCode=$null;$report=$null;$checksPassed=$false;$sourceRecordsEqual=$false;$packageEqual=$false;$rulesEqual=$false;$configEqual=$false;$processExited=$false
try{
    if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift before native verify; no repair.'}
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift after native launch; no repair.'}
    if(-not$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw 'Native candidate verification timed out.'}
    [Threading.Tasks.Task]::WaitAll($stdout,$stderr);$exitCode=$process.ExitCode;$processExited=$true
    [IO.File]::WriteAllText((Join-Path $run 'stdout.json'),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $run 'stderr.log'),$stderr.Result,[Text.UTF8Encoding]::new($false))
    $report=$stdout.Result|ConvertFrom-Json -Depth 80
    $reportRoot=Join-Path (Join-Path $run 'reports') $report.runMetadata.runId
    $reportPath=Join-Path $reportRoot 'report.json'
    if(-not(Test-Json -LiteralPath $reportPath -SchemaFile (Join-Path $package 'schemas/report.schema.json'))){throw 'Candidate report schema failed.'}
    if((Get-Content -LiteralPath $reportPath -Raw).Trim()-cne$stdout.Result.Trim()){throw 'Current stdout/saved JSON differ.'}
    if(-not(Test-Json -LiteralPath (Join-Path $reportRoot 'report.sarif') -SchemaFile 'D:\ArchSift-lab\evidence\sarif-2.1.0\sarif-schema-2.1.0.json')){throw 'Official SARIF schema failed.'}
    if($exitCode-ne4-or$report.toolVersion-cne'0.4.0'-or$report.operation-cne'verify'-or$report.execution-cne'partial'-or$report.compliance-cne'compliant'-or$report.coverage.projectCount-ne105-or$report.coverage.assemblyCount-ne0-or$report.coverage.sourceBound-or$report.buildContext.binding-cne'none'-or$report.findings.Count-ne0-or$report.executionErrors.Count-ne0-or$report.limitations.Count-ne105-or$report.ruleResults.Count-ne182-or@($report.ruleResults|Where-Object status -ne 'pass').Count){throw 'Candidate baseline differs from the reviewed declaration-level expectation; retain evidence and review.'}
    if($report.rulesetIdentities.Count-ne1-or$report.rulesetIdentities[0].sha256-cne$expectedRulesHash){throw 'Executed ruleset identity differs.'}
    $expectedIds=@($rules.rules.id|Sort-Object -CaseSensitive);$actualIds=@($report.ruleResults.ruleId|Sort-Object -CaseSensitive)
    if(($expectedIds-join"`n")-cne($actualIds-join"`n")){throw 'Executed rule IDs differ.'}
    $sourceFiles=@($report.inputIdentity.files|Where-Object {-not$_.path.StartsWith('@')})
    if($sourceFiles.Count-ne$discovery.inputIdentity.files.Count){throw 'Source record count changed (policy records are separate).'}
    foreach($file in $sourceFiles){$old=@($discovery.inputIdentity.files|Where-Object path -CEQ $file.path);if($old.Count-ne1-or$old[0].sha256-cne$file.sha256-or$old[0].length-ne$file.length-or$old[0].kind-cne$file.kind){throw 'Source records differ from discovery.'}}
    $policyRecords=@($report.inputIdentity.files|Where-Object {$_.path.StartsWith('@rules/')})
    if($policyRecords.Count-ne1-or$policyRecords[0].sha256-cne$expectedRulesHash-or@($report.inputIdentity.files|Where-Object {$_.path.StartsWith('@')-and-not$_.path.StartsWith('@rules/')}).Count){throw 'Unexpected policy/assembly evidence records.'}
    $sourceRecordsEqual=$true;Check-Source $sourceFiles
    Check-Package $manifest;$packageEqual=(Sha $manifestPath)-ceq$expectedManifestHash;$rulesEqual=(Sha $policy)-ceq$expectedRulesHash;$configEqual=(Sha $configPath)-ceq$configHash
    $checksPassed=$packageEqual-and$rulesEqual-and$configEqual
}catch{$operationError=$_.Exception.Message}
finally{
    if($process){if(-not$process.HasExited){$process.Kill($true);$process.WaitForExit()};$processExited=$process.HasExited;$process.Dispose()}
    $hostAfter=Get-ArchSiftHostHash;$headAfter=Git-Text @('rev-parse','HEAD');$statusAfter=Git-Text @('status','--porcelain=v1','--untracked-files=all')
    $equalHost=$hostBefore-ceq$hostAfter;$equalHead=$headBefore-ceq$headAfter;$equalStatus=$statusBefore-ceq$statusAfter
    $result=[ordered]@{step='ifx-0.4-candidate-baseline';status=$(if($checksPassed-and$equalHost-and$equalHead-and$equalStatus-and-not$operationError){'pass-with-limitations'}else{'stop'});targetHead=$headBefore;targetHeadEqual=$equalHead;ordinaryStatusEqual=$equalStatus;hostHashBefore=$hostBefore;hostHashAfter=$hostAfter;hostStateEqual=$equalHost;exitCode=$exitCode;runRoot=$run;sourceRecordsEqualToDiscovery=$sourceRecordsEqual;rulesSha256=$expectedRulesHash;rulesUnchanged=$rulesEqual;packageUnchanged=$packageEqual;configUnchanged=$configEqual;processExited=$processExited;reportSummary=$(if($report){[ordered]@{execution=$report.execution;compliance=$report.compliance;projectCount=$report.coverage.projectCount;assemblyCount=$report.coverage.assemblyCount;sourceBound=$report.coverage.sourceBound;ruleCount=$report.ruleResults.Count;findingCount=$report.findings.Count;limitationCount=$report.limitations.Count}}else{$null});targetBuildInvoked=$false;restoreInvoked=$false;policyAdopted=$false;testProjectAllowlistsClosed=$false;error=$operationError;nextAction='RETURN OPERATOR RESULT BEFORE NEXT COMMAND'}
    Save-Json (Join-Path $run 'operator-result.json') $result;$result|ConvertTo-Json -Depth 12
    if($result.status-eq'stop'){throw 'Operator candidate step stopped; evidence retained. Do not repair or start a later step.'}
}
