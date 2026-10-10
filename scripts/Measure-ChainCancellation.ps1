[CmdletBinding()]
param([Parameter(Mandatory)][string]$FixtureRoot,[string]$LabRoot='D:\ArchSift-lab',[string]$Configuration='Release',[ValidateSet('declaration','assembly')][string]$Scenario='declaration')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HostState.ps1')
$baseline=Get-ArchSiftHostHash
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('runs/chain-cancellation-'+[Guid]::NewGuid().ToString('N'))
if($run.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must be outside source.'}
[void][IO.Directory]::CreateDirectory($run)
$fixture=[IO.Path]::GetFullPath($FixtureRoot);$config=Join-Path $run 'config.json'
$build=@{mode='existing';targetFramework='net10.0';configuration='Debug';allowNetwork=$false}
if($Scenario-eq'assembly'){$build.mode='isolated';$build.localFeed=Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'}
[IO.File]::WriteAllText($config,(@{schemaVersion=1;target=@{root=(Join-Path $fixture 'source')};rulesDirectory=(Join-Path $fixture 'rules');rulesets=@();build=$build;output=@{directory=(Join-Path $run 'reports');formats=@('json','html','sarif')}}|ConvertTo-Json -Depth 8))
$rows=@()
try{
    foreach($count in @(1,2,4)){
        for($iteration=0;$iteration-lt5;$iteration++){
            $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$repo
            $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
            foreach($arg in @((Join-Path $repo "src/ArchSift.Web/bin/$Configuration/net10.0/ArchSift.Web.dll"),'--config',$config)){$start.ArgumentList.Add($arg)}
            $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home';$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$start.Environment['ARCHSIFT_UI_PARENT_CONTROL']='stdin-v1'
            $process=[Diagnostics.Process]::Start($start);$errors=$process.StandardError.ReadToEndAsync();$client=[Net.Http.HttpClient]::new()
            try{
                $address=$null
                while($null-eq$address){$line=$process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(30)).GetAwaiter().GetResult();if($null-eq$line){throw 'Web exited before ready.'};if($line.StartsWith('ARCHSIFT_UI=')){$address=[Uri]::new($line.Substring(12))}}
                $output=$process.StandardOutput.ReadToEndAsync();$client.BaseAddress=[Uri]::new($address.GetLeftPart([UriPartial]::Authority));$client.DefaultRequestHeaders.Add('X-ArchSift-Token',$address.Fragment.Substring(9))
                $body=[Net.Http.StringContent]::new((@{chainId='measure';targetKind='fixture';maxConcurrency=$count}|ConvertTo-Json -Compress),[Text.Encoding]::UTF8,'application/json')
                $response=$client.PostAsync('/api/run/chain',$body).GetAwaiter().GetResult();$response.EnsureSuccessStatusCode()|Out-Null;$id=($response.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json).jobId
                if($Scenario-eq'assembly'){
                    $waiting=[Diagnostics.Stopwatch]::StartNew()
                    do{$before=$client.GetStringAsync('/api/jobs/'+$id).GetAwaiter().GetResult()|ConvertFrom-Json;if($waiting.Elapsed.TotalSeconds-gt30-or$before.state-ne'running'){throw 'Could not observe current worker phase.'};if($null-eq$before.progress-or$before.progress.stage-ne'evaluating'-or$before.progress.runningCount-eq0){Start-Sleep -Milliseconds 10}}while($null-eq$before.progress-or$before.progress.stage-ne'evaluating'-or$before.progress.runningCount-eq0)
                    $workerPids=@(Get-CimInstance Win32_Process -Filter ("ParentProcessId="+$process.Id)|Where-Object {$_.Name-in@('dotnet.exe','archsift.exe','ArchSift.Web.exe')}|Select-Object -ExpandProperty ProcessId)
                    if($workerPids.Count-eq0){throw 'No owned worker PID observed; this run cannot prove worker cancellation latency.'}
                }
                $timer=[Diagnostics.Stopwatch]::StartNew();$response=$client.PostAsync('/api/jobs/'+$id+'/cancel',[Net.Http.StringContent]::new('{}',[Text.Encoding]::UTF8,'application/json')).GetAwaiter().GetResult();$response.EnsureSuccessStatusCode()|Out-Null
                do{if($timer.Elapsed.TotalSeconds-gt10){throw 'Cancellation did not finish within measurement safety limit.'};$job=$client.GetStringAsync('/api/jobs/'+$id).GetAwaiter().GetResult()|ConvertFrom-Json;if($job.state-eq'running'){Start-Sleep -Milliseconds 10}}while($job.state-eq'running')
                if($job.state-ne'cancelled'-or$job.exitCode-ne130-or-not$job.chain){throw 'Cancellation did not produce a current audit.'}
                if($Scenario-eq'assembly'){foreach($pidValue in $workerPids){try{$owned=[Diagnostics.Process]::GetProcessById($pidValue);try{if(-not$owned.HasExited){throw 'Owned worker remains after cancelled audit.'}}finally{$owned.Dispose()}}catch [ArgumentException]{}}}
                $rows+=@{maxConcurrency=$count;iteration=$iteration;cancellationMilliseconds=$timer.ElapsedMilliseconds;exitCode=$job.exitCode;runId=$job.chain.runMetadata.runId;endedCount=$job.progress.endedCount;skippedCount=$job.progress.skippedCount;observedWorkerCount=$(if($Scenario-eq'assembly'){$workerPids.Count}else{0})}
            }finally{if(-not$process.HasExited){$process.StandardInput.WriteLine('shutdown');$process.StandardInput.Flush();if(-not$process.WaitForExit(10000)){$process.Kill($true);$process.WaitForExit()}};$client.Dispose();$process.Dispose();if((Get-ArchSiftHostHash)-cne$baseline){throw 'Host drift; stop without repair.'}}
        }
    }
    [IO.File]::WriteAllText((Join-Path $run 'measurements.json'),(@{sourceCommit=(& git -C $repo rev-parse HEAD);fixtureRoot=$fixture;scenario=$Scenario;scope=$(if($Scenario-eq'assembly'){'real synthetic worker-phase cancellation; includes owned process drain'}else{'declaration preparation cancellation'});rows=$rows;hostStateEqual=$true}|ConvertTo-Json -Depth 8))
    Write-Output ('cancellation-measurement-root='+$run)
}finally{Write-Output ('host-state-sha256='+(Get-ArchSiftHostHash)+' host-state-equal='+((Get-ArchSiftHostHash)-ceq$baseline));if((Get-ArchSiftHostHash)-cne$baseline){throw 'Host drift; preserve evidence.'}}
