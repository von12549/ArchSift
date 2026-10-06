[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath,[string]$LabRoot='D:\ArchSift-lab')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('packages/smoke-'+[Guid]::NewGuid().ToString('N'))
$package=Join-Path $run 'unpacked'
[IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($ZipPath),$package)
$manifest=Get-Content -LiteralPath (Join-Path $package 'package-manifest.json') -Raw | ConvertFrom-Json
foreach($file in $manifest.entries){$path=Join-Path $package $file.path;if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()-cne$file.sha256){throw ('Package bytes mismatch: '+$file.path)}}
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
    $start.Environment['PATH']='';$start.Environment['DOTNET_ROOT']=(Join-Path $run 'no-dotnet');$start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $timer=[Diagnostics.Stopwatch]::StartNew();$p=[Diagnostics.Process]::Start($start);$out=$p.StandardOutput.ReadToEndAsync();$err=$p.StandardError.ReadToEndAsync();$peak=0L
    try{while(-not$p.WaitForExit(50)-and$timer.Elapsed.TotalSeconds-lt180){try{$peak=[Math]::Max($peak,$p.PeakWorkingSet64)}catch{}};if(-not$p.HasExited){$p.Kill($true);throw 'Package command timeout'};[Threading.Tasks.Task]::WaitAll($out,$err);$timer.Stop();[IO.File]::WriteAllText((Join-Path $run ($Name+'.stdout.log')),$out.Result);[IO.File]::WriteAllText((Join-Path $run ($Name+'.stderr.log')),$err.Result);$equal=(State-Hash)-ceq$before;$checks.Add([ordered]@{name=$Name;exitCode=$p.ExitCode;expected=$Expected;hostStateEqual=$equal;wallMilliseconds=$timer.ElapsedMilliseconds;sampledPeakBytes=$peak});if($p.ExitCode-ne$Expected-or-not$equal){Write-Output $err.Result;throw ('Package check failed: '+$Name)};Write-Host ($Name+' PASS');return $out.Result}finally{$p.Dispose()}
}
try {
    $cli=Join-Path $package 'archsift.exe';$web=Join-Path $package 'web/ArchSift.Web.exe'
    $version=Run-Package 'cli-version' $cli @('--version') 0
    $webVersion=Run-Package 'web-version' $web @('--version') 0
    if($version.Trim()-cne'archsift 0.1.0-dev'-or$webVersion.Trim()-cne'archsift-web 0.1.0-dev'){throw 'Package versions differ'}
    $fixture=Join-Path $run 'fixture'
    Copy-Item -LiteralPath (Join-Path $root 'artifacts/development/533dfa632ed84dcbbbd275fdcab14c9b/performance-fixture') -Destination $fixture -Recurse
    $reportText=Run-Package 'analyze-performance' $cli @('analyze','--target',$fixture,'--output',(Join-Path $run 'reports')) 0
    $report=$reportText.Substring($reportText.IndexOf('{'))|ConvertFrom-Json
    $budget=Get-Content -LiteralPath (Join-Path $root 'docs/performance/budget.json') -Raw|ConvertFrom-Json
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
    $invalid=Run-Package 'configuration-error' $cli @('verify','--bogus','x') 2
    $typeRules=Join-Path $run 'types.json'
    [IO.File]::WriteAllText($typeRules,'{"schemaVersion":1,"id":"types","version":"1","description":"worker without SDK","rules":[{"id":"types","type":"type-dependency","enabled":true,"scope":{"kind":"namespace","match":"exact","value":"ArchSift.Cli"},"parameters":{"source":{"kind":"namespace","match":"exact","value":"ArchSift.Cli"},"forbiddenTarget":{"kind":"namespace","match":"exact","value":"ArchSift.Core"}},"severity":"warning","reason":"smoke"}],"exceptions":[]}')
    $assemblyPaths=@('archsift.dll','ArchSift.Contracts.dll','ArchSift.Core.dll','ArchSift.ArchUnit.dll','ArchUnitNET.dll','Mono.Cecil.dll','Mono.Cecil.Rocks.dll','Mono.Cecil.Pdb.dll','Mono.Cecil.Mdb.dll','StronglyConnectedComponents.dll','JetBrains.Annotations.dll','Newtonsoft.Json.dll')|ForEach-Object{Join-Path $package $_}
    $config=Join-Path $run 'worker-config.json'
    [IO.File]::WriteAllText($config,([ordered]@{schemaVersion=1;target=[ordered]@{root=$fixture};rulesets=@($typeRules);build=[ordered]@{mode='existing';configuration='Debug';allowNetwork=$false;assemblyPaths=@($assemblyPaths)};output=[ordered]@{directory=(Join-Path $run 'worker-reports');formats=@('json','html')}}|ConvertTo-Json -Depth 7))
    $worker=Run-Package 'worker-without-sdk' $cli @('verify','--config',$config) 4
    $workerReport=$worker.Substring($worker.IndexOf('{'))|ConvertFrom-Json
    if($workerReport.engineVersions.archunitnet-cne'0.13.4'-or$workerReport.executionErrors.Count-ne0){throw 'Self-contained worker did not execute correctly'}
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$web;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.ArgumentList.Add('--config');$start.ArgumentList.Add($config);$start.Environment['PATH']='';$start.Environment['DOTNET_ROOT']=(Join-Path $run 'no-dotnet');$start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'web-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $server=[Diagnostics.Process]::Start($start);$serverError=$server.StandardError.ReadToEndAsync();$url=$null
    try{
        for($lineIndex=0;$lineIndex-lt25;$lineIndex++){$read=$server.StandardOutput.ReadLineAsync();if(-not$read.Wait(10000)){throw 'UI startup timeout'};$line=$read.Result;if($null-eq$line){throw 'UI exited before readiness'};if($line.StartsWith('ARCHSIFT_UI=')){$url=$line.Substring(12);break}}
        if($null-eq$url){throw 'UI has no readiness address'};$address=[Uri]$url;$http=[Net.Http.HttpClient]::new();$http.DefaultRequestHeaders.Add('X-ArchSift-Token',$address.Fragment.Substring(9));$response=$http.GetAsync($address.GetLeftPart([UriPartial]::Authority)+'/api/config').GetAwaiter().GetResult();if([int]$response.StatusCode-ne200){throw 'Packaged UI API failed'};$http.Dispose();$checks.Add([ordered]@{name='offline-native-ui';exitCode=0;expected=0;hostStateEqual=((State-Hash)-ceq$before)})
    }finally{if(-not$server.HasExited){$server.Kill($true);$server.WaitForExit()};$server.Dispose()}
    $result=[ordered]@{packageRoot=$package;zipSha256=(Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant();checks=$checks.ToArray();performancePassed=$performancePassed;performance=$performance;noSdkPathUsed=$true;hostStateEqual=((State-Hash)-ceq$before);w08Accepted=$false}
    [IO.File]::WriteAllText((Join-Path $run 'smoke-result.json'),($result|ConvertTo-Json -Depth 8));$result|ConvertTo-Json -Depth 8
} finally {Write-Output ('smoke-final-host-state-equal='+((State-Hash)-ceq$before))}
