# Operator-only 0.4-F step 3: authenticated native API/CLI matrix against read-only IFX.
[CmdletBinding()]
param([switch]$PreflightOnly)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Reuse the reviewed read-only guards/functions; this never runs native verify.
. (Join-Path $PSScriptRoot 'Invoke-0.4IfxCandidateStep.ps1') -ValidateOnly | Out-Null
$baselineRoot='D:\ArchSift-lab\runs\ifx-0.4-candidate-b79287d255874a90bf9c3ff5262fa015'
$baselineResultPath=Join-Path $baselineRoot 'operator-result.json'
if((Sha $baselineResultPath)-cne'e9cf1f33bc2771ee9501f72a053dcf595a2a1c748b809493c2aff8ee6ca78c2b'){throw 'Reviewed candidate result changed.'}
$baselineResult=Get-Content -LiteralPath $baselineResultPath -Raw|ConvertFrom-Json -Depth 30
if($baselineResult.status-cne'pass-with-limitations'-or-not$baselineResult.sourceRecordsEqualToDiscovery-or-not$baselineResult.hostStateEqual-or-not$baselineResult.packageUnchanged-or-not$baselineResult.rulesUnchanged){throw 'Step 02 is not accepted.'}
$baselineReportPath=Join-Path $baselineRoot 'reports/fa59bf3e59614d3fa940af4cf18d31a6/report.json'
if((Sha $baselineReportPath)-cne'36cedcb2f812a26d77c926abaf6f3fcdf8954c83163ae2828b2ad098a921f5d3'){throw 'Reviewed candidate report changed.'}
$baseline=Get-Content -LiteralPath $baselineReportPath -Raw|ConvertFrom-Json -Depth 80
$subsetRules=@($rules.rules|Where-Object {$_.type-in@('graph-integrity','target-framework')})
if($subsetRules.Count-ne3){throw 'Reviewed three-rule declaration subset changed.'}
if($PreflightOnly){[ordered]@{step='ifx-0.4-library-chain-preflight';status='metadata-pass';nativeProcessStarted=$false;baselineRules=182;subsetRules=3;sourceRecordsChecked=563;packageEntriesChecked=621}|ConvertTo-Json;return}
$run=Join-Path $lab ('runs/ifx-0.4-library-chain-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
$library=Join-Path $run 'library';[void][IO.Directory]::CreateDirectory($library)
$state=Join-Path $run 'state';[void][IO.Directory]::CreateDirectory($state)
$configPath=Join-Path $run 'config.json'
$invalidExternal=Join-Path $state 'invalid-external.json';[IO.File]::WriteAllText($invalidExternal,'{}',[Text.UTF8Encoding]::new($false))
$configuration=[ordered]@{schemaVersion=1;target=@{root=$target;entry='IFX.sln'};rulesDirectory=$library;rulesets=@($policy,$invalidExternal);build=@{mode='existing';targetFramework='net10.0';configuration='Debug';allowNetwork=$false;assemblyPaths=@()};output=@{directory=(Join-Path $run 'reports');formats=@('json','html','sarif')}}
Save-Json $configPath $configuration
$configurationHash=Sha $configPath
$hostBefore=Get-ArchSiftHostHash;$headBefore=Git-Text @('rev-parse','HEAD');$statusBefore=Git-Text @('status','--porcelain=v1','--untracked-files=all')
$checks=[Collections.Generic.List[object]]::new();$http=$null;$server=$null;$serverTail=$null;$serverError=$null;$errorMessage=$null;$processExited=$false;$completed=$false;$baseUri=$null
function Require([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Checkpoint {Require ((Get-ArchSiftHostHash)-ceq$hostBefore) 'Host environment/Profile/PATH drift; stop without repair.';Require ((Git-Text @('rev-parse','HEAD'))-ceq$headBefore) 'IFX HEAD changed.';Require ((Git-Text @('status','--porcelain=v1','--untracked-files=all'))-ceq$statusBefore) 'IFX status changed.';Check-Source $discovery.inputIdentity.files;Require ((Sha $policy)-ceq$expectedRulesHash) 'Original policy changed.'}
function Response([string]$Method,[string]$Route,[byte[]]$Bytes,[int]$Expected=200){Checkpoint;$request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method),($baseUri+'/api/'+$Route));if($null-ne$Bytes){$request.Content=[Net.Http.ByteArrayContent]::new($Bytes);$request.Content.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new('application/json')};try{$reply=$http.SendAsync($request).GetAwaiter().GetResult();$data=$reply.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();if([int]$reply.StatusCode-ne$Expected){$detail='';try{$errorBody=[Text.Encoding]::UTF8.GetString($data)|ConvertFrom-Json -Depth 8;if($errorBody.error -is [string]){$detail='; '+$errorBody.error.Substring(0,[Math]::Min(400,$errorBody.error.Length))}}catch{};throw ('Unexpected API status for '+$Route+': '+[int]$reply.StatusCode+$detail)};Checkpoint;return ,$data}finally{$request.Dispose()}}
function Get-Json([string]$Route){$data=Response 'GET' $Route $null;[Text.Encoding]::UTF8.GetString($data)|ConvertFrom-Json -Depth 100}
function Post([string]$Route,$Body,[int]$Expected=200){$bytes=[Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Body -Depth 90));$data=Response 'POST' $Route $bytes $Expected;if($data.Length){return [Text.Encoding]::UTF8.GetString($data)|ConvertFrom-Json -Depth 100}}
function Api-Job([string]$Operation,$Body){$queued=Post ('run/'+$Operation) $Body;$watch=[Diagnostics.Stopwatch]::StartNew();do{$job=Get-Json ('jobs/'+$queued.jobId);if($job.state-ne'running'){Require ($null-eq$job.error) 'Native API job failed.';Save-Json (Join-Path $state ($job.id+'.json')) $job;return $job};Start-Sleep -Milliseconds 100}while($watch.Elapsed.TotalSeconds-lt120);[void](Post ('jobs/'+$queued.jobId+'/cancel') @{});throw 'Native API job timeout; cancel requested.'}
function Check-Report($Report,[int]$ExpectedRules){Require ($Report.execution-eq'partial'-and$Report.compliance-eq'compliant'-and$Report.toolVersion-eq'0.4.0'-and$Report.coverage.projectCount-eq105-and$Report.coverage.assemblyCount-eq0-and-not$Report.coverage.sourceBound-and$Report.buildContext.binding-eq'none') 'Analysis status/coverage differs.';Require ($Report.ruleResults.Count-eq$ExpectedRules-and@($Report.ruleResults|Where-Object status -ne 'pass').Count-eq0-and$Report.findings.Count-eq0-and$Report.executionErrors.Count-eq0-and$Report.limitations.Count-eq105) 'Rule/coverage results differ.';$source=@($Report.inputIdentity.files|Where-Object {-not$_.path.StartsWith('@')});Require ($source.Count-eq563) 'Source record count differs.';foreach($file in $source){$expected=@($discovery.inputIdentity.files|Where-Object path -CEQ $file.path);Require ($expected.Count-eq1-and$expected[0].sha256-ceq$file.sha256-and$expected[0].length-eq$file.length-and$expected[0].kind-ceq$file.kind) 'Source record differs.'}}
function Child-Report($Summary,$Child){Require ($Child.diagnostic-eq$null-and$Child.reportDirectory) 'Analysis child report missing.';$path=Join-Path $Summary.snapshot.outputDirectory ($Child.reportDirectory+'/report.json');Require (Test-Json -LiteralPath $path -SchemaFile (Join-Path $package 'schemas/report.schema.json')) 'Child JSON schema failed.';Get-Content -LiteralPath $path -Raw|ConvertFrom-Json -Depth 100}
function Check-Chain($Summary,[string[]]$Order){Require ($Summary.execution-eq'partial'-and$Summary.compliance-eq'compliant'-and$Summary.exitCode-eq4-and$Summary.projectCount-eq105-and$Summary.entries.Count-eq2) 'Chain aggregate differs.';Require (($Summary.entries.entryId-join'|')-ceq($Order-join'|')) 'Chain order differs.';Require ($Summary.snapshot.inputIdentity.files.Count-eq563) 'Shared target identity missing.';foreach($child in $Summary.entries){Check-Report (Child-Report $Summary $child) $(if($child.entryId-eq$fullId){182}else{3})}}
function Download-Reports($Job){foreach($format in @('json','html','sarif')){$data=Response 'GET' ('jobs/'+$Job.id+'/report/'+$format) $null;[IO.File]::WriteAllBytes((Join-Path $state ($Job.id+'.'+$format)),$data)}}
try{
    Checkpoint;Check-Package $manifest
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=Join-Path $package 'web/ArchSift.Web.exe';$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in @('--config',$configPath)){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'web-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
    $server=[Diagnostics.Process]::Start($start);$serverError=$server.StandardError.ReadToEndAsync();Checkpoint
    for($index=0;$index-lt30;$index++){$read=$server.StandardOutput.ReadLineAsync();Require ($read.Wait(10000)) 'Native UI readiness timeout.';$line=$read.Result;Require ($null-ne$line) 'Native UI exited before readiness.';if($line.StartsWith('ARCHSIFT_UI=')){$address=[uri]$line.Substring(12);$baseUri=$address.GetLeftPart([UriPartial]::Authority);$sessionToken=$address.Fragment.Substring(9);break}}
    Require ($null-ne$baseUri) 'No native UI readiness address.';$serverTail=$server.StandardOutput.ReadToEndAsync()
    $http=[Net.Http.HttpClient]::new();$http.Timeout=[TimeSpan]::FromSeconds(30);$http.DefaultRequestHeaders.Add('X-ArchSift-Token',$sessionToken)
    $initial=Get-Json 'library';Require ($initial.entries.Count-eq0-and$initial.external.Count-eq2) 'External config files were silently enrolled.'
    $originalBytes=[IO.File]::ReadAllBytes($policy)
    $import=Response 'POST' 'library/import/ifx-protection.json' $originalBytes
    $full=[Text.Encoding]::UTF8.GetString($import)|ConvertFrom-Json -Depth 100;$fullId=$full.entryId
    Require ($full.identity.sha256-ceq$expectedRulesHash) 'Imported original content hash differs.'
    $export=Response 'GET' ('library/'+$fullId+'/json') $null;Require ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($export)).ToLowerInvariant()-ceq$expectedRulesHash) 'Exported original bytes differ.'
    $registry=Join-Path $library '.archsift-library.json';$registryHash=Sha $registry
    [void](Response 'POST' 'library/import/ifx-protection.json' $originalBytes 400)
    [void](Response 'POST' 'library/import/conflicting-id.json' $originalBytes 400)
    [void](Response 'POST' 'library/import/invalid.json' ([Text.Encoding]::UTF8.GetBytes('{}')) 400)
    Require ((Sha $registry)-ceq$registryHash) 'Rejected import mutated registry.'
    $checks.Add(@{name='explicit-import-export-and-atomic-conflicts';passed=$true})
    $subset=[ordered]@{schemaVersion=1;id='ifx-0.4-declaration-subset';version='1.0.0';description='Audit subset of reviewed graph/TFM rules; no new policy approval.';rules=$subsetRules;exceptions=@()}
    $subsetBytes=[Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $subset -Depth 80));$subsetHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($subsetBytes)).ToLowerInvariant()
    $import=Response 'POST' 'library/import/declaration-subset.json' $subsetBytes;$subsetCard=[Text.Encoding]::UTF8.GetString($import)|ConvertFrom-Json -Depth 100;$subsetId=$subsetCard.entryId
    $cardJob=Api-Job 'ruleset' @{entryId=$fullId;targetKind='real'};Require ($cardJob.exitCode-eq4-and$cardJob.context.rulesets.Count-eq1) 'Single card loaded startup composition.';Check-Report $cardJob.report 182;Require ($cardJob.report.rulesetIdentities[0].sha256-ceq$expectedRulesHash) 'Single card saved hash differs.';Download-Reports $cardJob
    $checks.Add(@{name='single-saved-card-despite-invalid-external-config';passed=$true})
    $chainId='ifx-04-audit-chain';Require ($chainId -match '^[a-zA-Z0-9_-]{1,100}$') 'Chain ID must meet the saved-chain contract.';$chain=@{schemaVersion=1;id=$chainId;version='1.0.0';description='Independent audit entries on one IFX source identity.';entries=@(@{entryId=$fullId},@{entryId=$subsetId})}
    [void](Post ('chains/'+$chainId) $chain);$forward=Api-Job 'chain' @{chainId=$chainId;targetKind='real'};Check-Chain $forward.chain @($fullId,$subsetId);Download-Reports $forward
    $chain.entries=@(@{entryId=$subsetId},@{entryId=$fullId});[void](Post ('chains/'+$chainId) $chain);$reverse=Api-Job 'chain' @{chainId=$chainId;targetKind='real'};Check-Chain $reverse.chain @($subsetId,$fullId);Download-Reports $reverse
    foreach($entry in $forward.chain.entries){$same=@($reverse.chain.entries|Where-Object entryId -CEQ $entry.entryId);Require ((ConvertTo-Json -InputObject $entry.ruleResults -Depth 30 -Compress)-ceq(ConvertTo-Json -InputObject $same[0].ruleResults -Depth 30 -Compress)) 'Reorder changed rule results.'}
    $checks.Add(@{name='ordered-independent-chain-duplicate-rule-ids-and-reorder';passed=$true})
    $cliStart=[Diagnostics.ProcessStartInfo]::new();$cliStart.FileName=Join-Path $package 'archsift.exe';$cliStart.UseShellExecute=$false;$cliStart.CreateNoWindow=$true;$cliStart.RedirectStandardOutput=$true;$cliStart.RedirectStandardError=$true
    foreach($argument in @('chain','verify','--config',$configPath,'--chain',(Join-Path $library ('chains/'+$chainId+'.json')),'--target-kind','real')){$cliStart.ArgumentList.Add($argument)}
    $cliStart.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home';$cliStart.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';Checkpoint
    $cli=[Diagnostics.Process]::Start($cliStart);try{$out=$cli.StandardOutput.ReadToEndAsync();$err=$cli.StandardError.ReadToEndAsync();if(-not$cli.WaitForExit(120000)){$cli.Kill($true);$cli.WaitForExit();throw 'Native chain CLI timeout.'};[Threading.Tasks.Task]::WaitAll($out,$err);Require ($cli.ExitCode-eq4) 'Native chain CLI exit differs.';[IO.File]::WriteAllText((Join-Path $state 'cli-chain.json'),$out.Result);[IO.File]::WriteAllText((Join-Path $state 'cli-chain.stderr.log'),$err.Result);$cliSummary=$out.Result|ConvertFrom-Json -Depth 100;Check-Chain $cliSummary @($subsetId,$fullId)}finally{if(-not$cli.HasExited){$cli.Kill($true);$cli.WaitForExit()};$cli.Dispose()};Checkpoint
    $checks.Add(@{name='native-cli-ui-chain-parity';passed=$true})
    [void](Post ('library/'+$subsetId+'/delete') @{});$sameName=Response 'POST' 'library/import/declaration-subset.json' $subsetBytes;$newCard=[Text.Encoding]::UTF8.GetString($sameName)|ConvertFrom-Json -Depth 100
    Require ($newCard.entryId-cne$subsetId) 'Same-name import rebound a deleted identity.'
    $savedChain=Get-Json ('chains/'+$chainId);Require ($savedChain.entries[0].entryId-ceq$subsetId) 'Deleted chain reference was removed/rebound.'
    $missing=Api-Job 'chain' @{chainId=$chainId;targetKind='real'};Require ($missing.exitCode-eq4-and$missing.chain.execution-eq'partial'-and$missing.chain.compliance-eq'inconclusive'-and$missing.chain.entries[0].diagnostic.code-eq'missing-ruleset'-and$missing.chain.entries[0].ruleResults.Count-eq0-and$missing.chain.entries[0].findings.Count-eq0-and$missing.chain.snapshot.entries[0].rulesetIdentity-eq$null) 'Missing-entry diagnostic differs.';Check-Report (Child-Report $missing.chain $missing.chain.entries[1]) 182;Download-Reports $missing
    $checks.Add(@{name='deleted-reference-diagnostic-and-later-child-continuation';passed=$true})
    $chain.entries=@(@{entryId=$newCard.entryId},@{entryId=$fullId});[void](Post ('chains/'+$chainId) $chain)
    $testFile=Join-Path $library 'declaration-subset.json';Require (Is-Under $testFile $library) 'Fixture write containment failed.';No-Links $testFile;[IO.File]::WriteAllText($testFile,'{}',[Text.UTF8Encoding]::new($false))
    $invalid=Api-Job 'chain' @{chainId=$chainId;targetKind='real'};Require ($invalid.chain.entries[0].diagnostic.code-eq'invalid-ruleset'-and$invalid.chain.entries[0].ruleResults.Count-eq0-and$invalid.chain.entries[0].findings.Count-eq0) 'Invalid entry invented results.';Check-Report (Child-Report $invalid.chain $invalid.chain.entries[1]) 182;Download-Reports $invalid
    [void](Response 'POST' 'rules/declaration-subset.json' $subsetBytes);Require ((Sha $testFile)-ceq$subsetHash) 'Saved test-fixture recovery differs.'
    $checks.Add(@{name='invalid-child-diagnostic-and-later-child-continuation';passed=$true})
    $bad=[ordered]@{};foreach($key in $configuration.Keys){$bad[$key]=$configuration[$key]};$bad.target=@{root=$target;entry='__archsift_missing_0_4.sln'};[void](Post 'config' $bad 400)
    $checks.Add(@{name='invalid-global-target-rejected-before-job';passed=$true})
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $run 'reports') -File -Recurse){if($file.Name-eq'report.json'){Require (Test-Json -LiteralPath $file.FullName -SchemaFile (Join-Path $package 'schemas/report.schema.json')) 'Analysis JSON schema failed.'}elseif($file.Name-eq'chain-summary.json'){Require (Test-Json -LiteralPath $file.FullName -SchemaFile (Join-Path $package 'schemas/chain-summary.schema.json')) 'Chain JSON schema failed.'}elseif($file.Name-eq'diagnostic.json'){Require (Test-Json -LiteralPath $file.FullName -SchemaFile (Join-Path $package 'schemas/chain-diagnostic.schema.json')) 'Diagnostic JSON schema failed.'}elseif($file.Extension-eq'.sarif'){Require (Test-Json -LiteralPath $file.FullName -SchemaFile 'D:\ArchSift-lab\evidence\sarif-2.1.0\sarif-schema-2.1.0.json') 'Official SARIF schema failed.'}}
    $checks.Add(@{name='all-current-analysis-chain-diagnostic-schemas';passed=$true})
    [void](Response 'POST' 'shutdown' ([Text.Encoding]::UTF8.GetBytes('{}')) 202);Require ($server.WaitForExit(10000)) 'Native safe shutdown did not exit.';Checkpoint;Check-Package $manifest;Require ((Sha $manifestPath)-ceq$expectedManifestHash) 'Candidate manifest changed.';Require ((Sha $configPath)-ceq$configurationHash) 'Saved run config changed.';$completed=$true
}catch{$errorMessage=$_.Exception.Message}
finally{
    if($http){$http.Dispose()};if($server){if(-not$server.HasExited){$server.Kill($true);$server.WaitForExit()};$processExited=$server.HasExited;if($serverTail){[IO.File]::WriteAllText((Join-Path $state 'web.stdout.log'),$serverTail.GetAwaiter().GetResult())};if($serverError){[IO.File]::WriteAllText((Join-Path $state 'web.stderr.log'),$serverError.GetAwaiter().GetResult())};$server.Dispose()}
    $hostAfter=Get-ArchSiftHostHash;$headAfter=Git-Text @('rev-parse','HEAD');$statusAfter=Git-Text @('status','--porcelain=v1','--untracked-files=all')
    $result=[ordered]@{step='ifx-0.4-library-chain-matrix';status=$(if($completed-and$hostBefore-ceq$hostAfter-and$headBefore-ceq$headAfter-and$statusBefore-ceq$statusAfter){'pass-with-limitations'}else{'stop'});runRoot=$run;checks=$checks.ToArray();checksPassed=$checks.Count;targetHead=$headBefore;targetHeadEqual=($headBefore-ceq$headAfter);ordinaryStatusEqual=($statusBefore-ceq$statusAfter);hostHashBefore=$hostBefore;hostHashAfter=$hostAfter;hostStateEqual=($hostBefore-ceq$hostAfter);rulesUnchanged=((Sha $policy)-ceq$expectedRulesHash);packageManifestUnchanged=((Sha $manifestPath)-ceq$expectedManifestHash);configUnchanged=((Sha $configPath)-ceq$configurationHash);processExited=$processExited;targetBuildInvoked=$false;restoreInvoked=$false;policyAdopted=$false;testProjectAllowlistsClosed=$false;browserVisualReviewPerformed=$false;error=$errorMessage;nextAction='RETURN OPERATOR RESULT BEFORE NEXT COMMAND'}
    Save-Json (Join-Path $run 'operator-result.json') $result;$result|ConvertTo-Json -Depth 15;if($result.status-eq'stop'){throw 'Operator matrix stopped; preserve evidence and review before another step.'}
}
