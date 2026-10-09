[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath,[string]$LabRoot='D:\ArchSift-lab',[string]$BudgetFile=(Join-Path $PSScriptRoot '../docs/performance/w11-budget.json'),[string]$SarifSchema)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('packages/smoke-'+[Guid]::NewGuid().ToString('N'))
$package=Join-Path $run 'unpacked'
$archive=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ZipPath))
try{
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $archive.Entries){
        $path=[IO.Path]::GetFullPath((Join-Path $package $entry.FullName))
        if(-not$path.StartsWith($package+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$entry.FullName.Contains(':')-or$entry.FullName.Replace('\','/').Split('/').Contains('..')-or-not$seen.Add($entry.FullName)-or(($entry.ExternalAttributes-shr16)-band0xf000)-eq0xa000){throw 'Unsafe or duplicate ZIP path.'}
    }
    [IO.Compression.ZipFileExtensions]::ExtractToDirectory($archive,$package)
}finally{$archive.Dispose()}
$manifest=Get-Content -LiteralPath (Join-Path $package 'package-manifest.json') -Raw | ConvertFrom-Json
$manifestPaths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($file in $manifest.entries){$path=[IO.Path]::GetFullPath((Join-Path $package $file.path));if(-not$path.StartsWith($package+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or-not$manifestPaths.Add($file.path)-or(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()-cne$file.sha256-or(Get-Item -LiteralPath $path).Length-ne$file.bytes){throw ('Package bytes/path mismatch: '+$file.path)}}
if(@(Get-ChildItem -LiteralPath $package -Recurse -File).Count-ne$manifest.entries.Count+1){throw 'Package file set mismatch.'}
if((Get-FileHash -LiteralPath (Join-Path $package 'LICENSE') -Algorithm SHA256).Hash-cne(Get-FileHash -LiteralPath (Join-Path $root 'LICENSE') -Algorithm SHA256).Hash){throw 'Product license mismatch.'}
function State-Hash {
    $values=[ordered]@{}
    foreach($scope in @('User','Machine')){$items=[Environment]::GetEnvironmentVariables($scope);foreach($key in @($items.Keys)|Sort-Object){$values["$scope/$key"]=[string]$items[$key]}}
    $values['process-path']=[Environment]::GetEnvironmentVariable('Path')
    foreach($name in @('AllUsersAllHosts','AllUsersCurrentHost','CurrentUserAllHosts','CurrentUserCurrentHost')){$p=[string]$PROFILE.$name;$values[$name]=if([IO.File]::Exists($p)){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}else{'absent'}}
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($values|ConvertTo-Json -Compress)))).ToLowerInvariant()
}
$before=State-Hash
$checks=[Collections.Generic.List[object]]::new()
function Run-Package([string]$Name,[string]$Executable,[string[]]$Arguments,[int]$Expected){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$Executable;$start.WorkingDirectory=$run;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_ROOT']=(Join-Path $run 'no-dotnet');$start.Environment['DOTNET_HOST_PATH']=(Join-Path $run 'no-dotnet/dotnet.exe');$start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $timer=[Diagnostics.Stopwatch]::StartNew();$p=[Diagnostics.Process]::Start($start);$out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync();$peak=0L
    try{while(-not$p.WaitForExit(50)-and$timer.Elapsed.TotalSeconds-lt180){try{$peak=[Math]::Max($peak,$p.PeakWorkingSet64)}catch{}};if(-not$p.HasExited){$p.Kill($true);throw 'Package command timeout'};[Threading.Tasks.Task]::WaitAll($out,$err);$timer.Stop();[IO.File]::WriteAllText((Join-Path $run ($Name+'.stdout.log')),$out.Result);[IO.File]::WriteAllText((Join-Path $run ($Name+'.stderr.log')),$err.Result);$equal=(State-Hash)-ceq$before;$checks.Add([ordered]@{name=$Name;exitCode=$p.ExitCode;expected=$Expected;hostStateEqual=$equal;wallMilliseconds=$timer.ElapsedMilliseconds;sampledPeakBytes=$peak});if($p.ExitCode-ne$Expected-or-not$equal){Write-Output $err.Result;throw ('Package check failed: '+$Name)};Write-Host ($Name+' PASS');return $out.Result}finally{$p.Dispose()}
}
try {
    $cli=Join-Path $package 'archsift.exe';$web=Join-Path $package 'web/ArchSift.Web.exe'
    $version=Run-Package 'cli-version' $cli @('--version') 0
    $webVersion=Run-Package 'web-version' $web @('--version') 0
    if($version.Trim()-cne('archsift '+$manifest.version)-or$webVersion.Trim()-cne('archsift-web '+$manifest.version)){throw 'Package versions differ'}
    $fixture=Join-Path $run 'fixture'
    for($index=0;$index-lt100;$index++){
        $name='P'+$index.ToString('D3');$directory=Join-Path $fixture $name;[void][IO.Directory]::CreateDirectory($directory)
        $reference=if($index-gt0){'<ItemGroup><ProjectReference Include="../P'+($index-1).ToString('D3')+'/P'+($index-1).ToString('D3')+'.csproj" /></ItemGroup>'}else{''}
        [IO.File]::WriteAllText((Join-Path $directory ($name+'.csproj')),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>'+$reference+'</Project>')
        [IO.File]::WriteAllText((Join-Path $directory ($name+'.cs')),'namespace '+$name+'; public class Marker {}')
    }
    $reportText=Run-Package 'analyze-performance' $cli @('analyze','--target',$fixture,'--output',(Join-Path $run 'reports')) 0
    $report=$reportText.Substring($reportText.IndexOf('{'))|ConvertFrom-Json
    $budget=Get-Content -LiteralPath ([IO.Path]::GetFullPath($BudgetFile)) -Raw|ConvertFrom-Json
    $timing=$checks[$checks.Count-1]
    $performancePassed=$report.inputIdentity.sha256-ceq$budget.inputSha256-and$report.coverage.projectCount-eq100-and$report.runMetadata.elapsedMilliseconds-le$budget.analysisMillisecondsLimit-and$timing.wallMilliseconds-le$budget.processWallMillisecondsLimit-and$timing.sampledPeakBytes-le$budget.sampledPeakWorkingSetBytesLimit
    if(-not$performancePassed){throw 'Package exceeds frozen reference budget'}
    $performance=@([ordered]@{iteration=0;analysisMilliseconds=$report.runMetadata.elapsedMilliseconds;processWallMilliseconds=$timing.wallMilliseconds;sampledPeakBytes=$timing.sampledPeakBytes})
    for($index=1;$index-lt5;$index++){
        $text=Run-Package ('performance-'+$index) $cli @('analyze','--target',$fixture,'--output',(Join-Path $run 'reports')) 0
        $sample=$text.Substring($text.IndexOf('{'))|ConvertFrom-Json;$record=$checks[$checks.Count-1]
        if($sample.inputIdentity.sha256-cne$budget.inputSha256-or$sample.runMetadata.elapsedMilliseconds-gt$budget.analysisMillisecondsLimit-or$record.wallMilliseconds-gt$budget.processWallMillisecondsLimit-or$record.sampledPeakBytes-gt$budget.sampledPeakWorkingSetBytesLimit){throw 'Representative package run exceeds budget'}
        $performance += [ordered]@{iteration=$index;analysisMilliseconds=$sample.runMetadata.elapsedMilliseconds;processWallMilliseconds=$record.wallMilliseconds;sampledPeakBytes=$record.sampledPeakBytes}
    }
    $rules=Join-Path $run 'rules.json'
    [IO.File]::WriteAllText($rules,'{"schemaVersion":1,"id":"smoke","version":"1","description":"smoke","rules":[{"id":"name","type":"naming","enabled":true,"scope":{"kind":"project","match":"glob","value":"**"},"parameters":{"subjectKind":"project","requiredName":{"match":"exact","value":"Wrong"}},"severity":"warning","reason":"smoke"}],"exceptions":[]}')
    $verified=Run-Package 'nonblocking-violation' $cli @('verify','--target',$fixture,'--rules',$rules,'--output',(Join-Path $run 'reports')) 0
    $verifiedReport=$verified.Substring($verified.IndexOf('{'))|ConvertFrom-Json
    if($verifiedReport.compliance-cne'noncompliant'){throw 'Violation disappeared'}
    $sarifConfig=Join-Path $run 'sarif-config.json'
    $sarifOutput=Join-Path $run 'sarif-reports'
    [IO.File]::WriteAllText($sarifConfig,([ordered]@{schemaVersion=1;target=@{root=$fixture};rulesets=@($rules);output=@{directory=$sarifOutput;formats=@('json','html','sarif')}}|ConvertTo-Json -Depth 8))
    $sarifRun=Run-Package 'sarif-nonblocking-violation' $cli @('verify','--config',$sarifConfig) 0
    $sarifFile=Get-ChildItem -LiteralPath $sarifOutput -Filter 'report.sarif' -Recurse -File|Select-Object -ExpandProperty FullName
    $sarifSchemaVerified=$false
    if($SarifSchema){if(-not(Test-Json -LiteralPath $sarifFile -SchemaFile ([IO.Path]::GetFullPath($SarifSchema)))){throw 'Official SARIF schema failed.'};$sarifSchemaVerified=$true}
    # Git is a documented prerequisite only for changes; no SDK path is added.
    $git=(Get-Command git -ErrorAction Stop).Source
    & $git -C $fixture init | Out-Null
    & $git -C $fixture add .
    & $git -C $fixture -c user.name='ArchSift Fixture' -c user.email='fixture@archsift.invalid' commit -m "Synthetic packaged comparison`n`nCo-Authored-By: Codex <noreply@openai.com>" | Out-Null
    if($LASTEXITCODE-ne0){throw 'Packaged comparison fixture commit failed.'}
    [IO.File]::WriteAllText((Join-Path $fixture 'New.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
    $invalidRef=Run-Package 'changes-invalid-local-ref' $cli @('changes','--config',$sarifConfig,'--base','archsift-missing-local-commit','--head','HEAD') 2
    $compared=Run-Package 'native-changes-no-sdk' $cli @('changes','--config',$sarifConfig) 0
    $comparison=$compared.Substring($compared.IndexOf('{'))|ConvertFrom-Json
    if($comparison.added.Count-ne1-or$comparison.existing.Count-ne100-or$comparison.status-cne'completed'){throw 'Packaged comparison evidence mismatch.'}
    if($SarifSchema){$comparisonSarif=Get-ChildItem -LiteralPath $sarifOutput -Filter 'comparison.sarif' -Recurse -File|Select-Object -ExpandProperty FullName;if(-not(Test-Json -LiteralPath $comparisonSarif -SchemaFile ([IO.Path]::GetFullPath($SarifSchema)))){throw 'Official comparison SARIF schema failed.'}}
    $invalid=Run-Package 'configuration-error' $cli @('verify','--bogus','x') 2
    $typeRules=Join-Path $run 'types.json'
    [IO.File]::WriteAllText($typeRules,'{"schemaVersion":1,"id":"types","version":"1","description":"worker without SDK","rules":[{"id":"types","type":"type-dependency","enabled":true,"scope":{"kind":"namespace","match":"exact","value":"ArchSift.Cli"},"parameters":{"source":{"kind":"namespace","match":"exact","value":"ArchSift.Cli"},"forbiddenTarget":{"kind":"namespace","match":"exact","value":"ArchSift.Core"}},"severity":"warning","reason":"smoke"}],"exceptions":[]}')
    $assemblyPaths=@('archsift.dll','ArchSift.Contracts.dll','ArchSift.Core.dll','ArchSift.ArchUnit.dll','ArchUnitNET.dll','Mono.Cecil.dll','Mono.Cecil.Rocks.dll','Mono.Cecil.Pdb.dll','Mono.Cecil.Mdb.dll','StronglyConnectedComponents.dll','JetBrains.Annotations.dll','Newtonsoft.Json.dll')|ForEach-Object{Join-Path $package $_}
    $config=Join-Path $run 'worker-config.json'
    [IO.File]::WriteAllText($config,([ordered]@{schemaVersion=1;target=[ordered]@{root=$fixture};rulesets=@($typeRules);build=[ordered]@{mode='existing';configuration='Debug';allowNetwork=$false;assemblyPaths=@($assemblyPaths)};output=[ordered]@{directory=(Join-Path $run 'worker-reports');formats=@('json','html')}}|ConvertTo-Json -Depth 7))
    $worker=Run-Package 'worker-without-sdk' $cli @('verify','--config',$config) 4
    $workerReport=$worker.Substring($worker.IndexOf('{'))|ConvertFrom-Json
    if($workerReport.engineVersions.archunitnet-cne'0.13.4'-or$workerReport.executionErrors.Count-ne0){throw 'Self-contained worker did not execute correctly'}
    $library=Join-Path $run 'chain-library';[void][IO.Directory]::CreateDirectory($library)
    $firstPolicy=Join-Path $library 'first.json';$secondPolicy=Join-Path $library 'second.json'
    $policyText=Get-Content -LiteralPath (Join-Path $package 'templates/rules/naming.json') -Raw
    [IO.File]::WriteAllText($firstPolicy,$policyText,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($secondPolicy,$policyText.Replace('template-naming','second-policy'),[Text.UTF8Encoding]::new($false))
    $chainConfig=Join-Path $run 'chain-config.json'
    $chainSettings=[ordered]@{schemaVersion=1;target=@{root=$fixture};rulesDirectory=$library;rulesets=@($firstPolicy,$secondPolicy);build=@{mode='existing';configuration='Debug'};output=@{directory=(Join-Path $run 'chain-reports');formats=@('json','html','sarif')}}
    [IO.File]::WriteAllText($chainConfig,($chainSettings|ConvertTo-Json -Depth 8))
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$web;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.ArgumentList.Add('--config');$start.ArgumentList.Add($chainConfig);$start.Environment['DOTNET_ROOT']=(Join-Path $run 'no-dotnet');$start.Environment['DOTNET_HOST_PATH']=(Join-Path $run 'no-dotnet/dotnet.exe');$start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'web-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $server=[Diagnostics.Process]::Start($start);$serverError=$server.StandardError.ReadToEndAsync();$url=$null
    try{
        for($lineIndex=0;$lineIndex-lt25;$lineIndex++){$read=$server.StandardOutput.ReadLineAsync();if(-not$read.Wait(10000)){throw 'UI startup timeout'};$line=$read.Result;if($null-eq$line){throw 'UI exited before readiness'};if($line.StartsWith('ARCHSIFT_UI=')){$url=$line.Substring(12);break}}
        if($null-eq$url){throw 'UI has no readiness address'};$serverOutput=$server.StandardOutput.ReadToEndAsync();$address=[Uri]$url;$http=[Net.Http.HttpClient]::new();$http.Timeout=[TimeSpan]::FromSeconds(30);$baseUri=$address.GetLeftPart([UriPartial]::Authority);$http.DefaultRequestHeaders.Add('X-ArchSift-Token',$address.Fragment.Substring(9));$response=$http.GetAsync($baseUri+'/api/config').GetAwaiter().GetResult();if([int]$response.StatusCode-ne200){throw 'Packaged UI API failed'}
        function Native-Post([string]$Path,$Body){$content=[Net.Http.StringContent]::new(($Body|ConvertTo-Json -Depth 20),[Text.Encoding]::UTF8,'application/json');try{$reply=$http.PostAsync($baseUri+'/api/'+$Path,$content).GetAwaiter().GetResult();[void]$reply.EnsureSuccessStatusCode();$text=$reply.Content.ReadAsStringAsync().GetAwaiter().GetResult();if($text){return $text|ConvertFrom-Json}}finally{$content.Dispose()}}
        function Native-Job([string]$Operation,$Body){$queued=Native-Post ('run/'+$Operation) $Body;$timer=[Diagnostics.Stopwatch]::StartNew();do{$job=$http.GetStringAsync($baseUri+'/api/jobs/'+$queued.jobId).GetAwaiter().GetResult()|ConvertFrom-Json;if($job.state-ne'running'){return $job};Start-Sleep -Milliseconds 25}while($timer.Elapsed.TotalSeconds-lt30);throw 'Native UI job timeout'}
        $cards=$http.GetStringAsync($baseUri+'/api/library').GetAwaiter().GetResult()|ConvertFrom-Json
        if($cards.entries.Count-ne2){throw 'Native library initialization mismatch'}
        $firstCard=$cards.entries|Where-Object fileName -eq 'first.json';$secondCard=$cards.entries|Where-Object fileName -eq 'second.json'
        $exported=$http.GetByteArrayAsync($baseUri+'/api/library/'+$secondCard.entryId+'/json').GetAwaiter().GetResult()
        if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($exported)).ToLowerInvariant()-cne(Get-FileHash -LiteralPath $secondPolicy -Algorithm SHA256).Hash.ToLowerInvariant()){throw 'Native export byte identity mismatch'}
        $cardJob=Native-Job 'ruleset' @{entryId=$firstCard.entryId;targetKind='fixture'}
        if($cardJob.exitCode-ne0-or$cardJob.report.rulesetIdentities.Count-ne1){throw 'Native single-card isolation failed'}
        $chainBody=@{schemaVersion=1;id='smoke-chain';version='1';description='Native independent policies';entries=@(@{entryId=$firstCard.entryId},@{entryId=$secondCard.entryId})}
        [void](Native-Post 'chains/smoke-chain' $chainBody)
        $chainJob=Native-Job 'chain' @{chainId='smoke-chain';targetKind='fixture'}
        # The preceding changes scenario added New.csproj: one shared target now has 101 projects, not 100 or 202.
        if($chainJob.exitCode-ne0-or$chainJob.chain.entries.Count-ne2-or$chainJob.chain.projectCount-ne101){throw 'Native chain summary failed'}
        if(@($chainJob.chain.entries|Where-Object {$_.coverage.projectCount-ne101}).Count){throw 'Native child project coverage differs from the shared target'}
        $chainPath=Join-Path $library 'chains/smoke-chain.json'
        [void](Run-Package 'chain-without-sdk' $cli @('chain','verify','--config',$chainConfig,'--chain',$chainPath,'--target-kind','fixture') 0)
        [void](Run-Package 'legacy-duplicate-rule-ids' $cli @('verify','--config',$chainConfig) 2)
        [void](Native-Post ('library/'+$firstCard.entryId+'/delete') @{})
        $missingJob=Native-Job 'chain' @{chainId='smoke-chain';targetKind='fixture'}
        if($missingJob.exitCode-ne4-or$missingJob.chain.entries[0].diagnostic.code-cne'missing-ruleset'-or$missingJob.chain.entries[1].execution-cne'completed'){throw 'Native missing-child continuation failed'}
        [void](Run-Package 'chain-missing-then-success' $cli @('chain','verify','--config',$chainConfig,'--chain',$chainPath) 4)
        if($SarifSchema){foreach($file in Get-ChildItem -LiteralPath $chainSettings.output.directory -Recurse -File -Filter '*.sarif'){if(-not(Test-Json -LiteralPath $file.FullName -SchemaFile $SarifSchema)){throw 'Native chain/diagnostic SARIF failed official validation'}}}
        [void](Native-Post 'shutdown' @{});if(-not$server.WaitForExit(10000)){throw 'Native UI safe shutdown failed'};$http.Dispose();$checks.Add([ordered]@{name='offline-native-ui-cards-chains-export';exitCode=0;expected=0;hostStateEqual=((State-Hash)-ceq$before)})
    }finally{if(-not$server.HasExited){$server.Kill($true);$server.WaitForExit()};$server.Dispose()}
    $result=[ordered]@{packageRoot=$package;zipSha256=(Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant();checks=$checks.ToArray();performancePassed=$performancePassed;performance=$performance;noSdkPathUsed=$false;invalidDotnetRootAndHostUsed=$true;processPathPreserved=$true;hostStateEqual=((State-Hash)-ceq$before);version=$manifest.version;sourceCommit=$manifest.sourceCommit;sarifSchemaVerified=$sarifSchemaVerified}
    [IO.File]::WriteAllText((Join-Path $run 'smoke-result.json'),($result|ConvertTo-Json -Depth 8));$result|ConvertTo-Json -Depth 8
} finally {Write-Output ('smoke-final-host-state-equal='+((State-Hash)-ceq$before))}
