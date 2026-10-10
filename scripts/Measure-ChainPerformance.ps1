[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string]$LabRoot='D:\ArchSift-lab',
    [string]$FixtureRoot,
    [ValidateRange(1,4)][int]$MaxConcurrency=1,
    [ValidateRange(6,20)][int]$Iterations=6,
    [switch]$SkipUi
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HostState.ps1')
$baseline=Get-ArchSiftHostHash
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lab=[IO.Path]::GetFullPath($LabRoot)
if($lab.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Measurements must be outside source.'}
$run=Join-Path $lab ('runs/chain-measure-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
$dotnet=(Get-Command dotnet -ErrorAction Stop).Source
$cli=Join-Path $repo "src/ArchSift.Cli/bin/$Configuration/net10.0/archsift.dll"
$web=Join-Path $repo "src/ArchSift.Web/bin/$Configuration/net10.0/ArchSift.Web.dll"
$records=[Collections.Generic.List[object]]::new()
function Save-Json([string]$Path,$Value){[IO.File]::WriteAllText($Path,($Value|ConvertTo-Json -Depth 60)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))}
function Check-Host {
    $current=Get-ArchSiftHostHash
    Write-Output ("host-state-sha256=$current host-state-equal="+($current-ceq$baseline))
    if($current-cne$baseline){throw 'Host drift; stop and preserve evidence. No repair permitted.'}
}
function New-Start([string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.WorkingDirectory=$repo
    $start.RedirectStandardInput=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    foreach($arg in $Arguments){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    $start.Environment['DOTNET_NOLOGO']='1'
    $start.Environment['MSBUILDDISABLENODEREUSE']='1'
    $start.Environment['ARCHSIFT_UI_PARENT_CONTROL']='stdin-v1'
    return $start
}
function Invoke-Tool([string[]]$Arguments){
    $timer=[Diagnostics.Stopwatch]::StartNew(); $process=[Diagnostics.Process]::Start((New-Start $Arguments))
    try{
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync(); $peak=0L
        while(-not$process.WaitForExit(10)){
            if($timer.Elapsed.TotalSeconds-gt60){$process.Kill($true);$process.WaitForExit();throw 'Measurement timed out.'}
            try{$process.Refresh();$peak=[Math]::Max($peak,$process.PeakWorkingSet64)}catch{}
        }
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        if($process.ExitCode-notin@(0,4)){throw ('Measurement failed: '+$stderr.Result)}
        return @{stdout=$stdout.Result;stderr=$stderr.Result;exitCode=$process.ExitCode;wall=$timer.ElapsedMilliseconds;peak=$peak}
    }finally{if(-not$process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
}
try{
    if(-not$FixtureRoot){
        $FixtureRoot=Join-Path $run 'fixture'; $source=Join-Path $FixtureRoot 'source'; $library=Join-Path $FixtureRoot 'rules'
        [void][IO.Directory]::CreateDirectory($source); [void][IO.Directory]::CreateDirectory($library)
        for($i=0;$i-lt100;$i++){
            $name='P'+$i.ToString('D3'); $directory=Join-Path $source $name; [void][IO.Directory]::CreateDirectory($directory)
            [IO.File]::WriteAllText((Join-Path $directory "$name.csproj"),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            [IO.File]::WriteAllText((Join-Path $directory "$name.cs"),"namespace $name; public class Marker {}")
        }
        $entries=@(); $registry=@(); $counts=@(24,33,35,90)
        for($group=0;$group-lt4;$group++){
            $id=('a'+($group+1)).PadRight(32,'0'); $name='group-'+($group+1); $rules=@()
            for($i=0;$i-lt$counts[$group];$i++){
                $project='P'+$i.ToString('D3')
                $rules+=@{id='rule-'+$i;type='naming';enabled=$true;severity='warning';reason='Synthetic independent project naming';scope=@{kind='project';match='exact';value="$project/$project.csproj"};parameters=@{subjectKind='project';requiredName=@{match='exact';value=$project}}}
            }
            Save-Json (Join-Path $library "$name.json") @{schemaVersion=1;id=$name;version='1';description='Synthetic declaration benchmark';rules=$rules;exceptions=@()}
            $entries+=@{entryId=$id}; $registry+=@{entryId=$id;fileName="$name.json";rulesetId=$name;deleted=$false}
        }
        Save-Json (Join-Path $library '.archsift-library.json') @{schemaVersion=1;entries=$registry}
        [void][IO.Directory]::CreateDirectory((Join-Path $library 'chains'))
        Save-Json (Join-Path $library 'chains/measure.json') @{schemaVersion=1;id='measure';version='1';description='Four independent synthetic groups';entries=$entries}
    }
    $FixtureRoot=[IO.Path]::GetFullPath($FixtureRoot)
    if(-not$FixtureRoot.StartsWith($lab+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Only recorded external lab fixtures are accepted.'}
    $source=Join-Path $FixtureRoot 'source'; $library=Join-Path $FixtureRoot 'rules'; $chain=Join-Path $library 'chains/measure.json'
    $config=Join-Path $run 'config.json'
    Save-Json $config @{schemaVersion=1;target=@{root=$source};rulesDirectory=$library;rulesets=@();build=@{mode='existing';targetFramework='net10.0';configuration='Debug';allowNetwork=$false};output=@{directory=(Join-Path $run 'reports');formats=@('json','html','sarif')}}
    $sdk=(Invoke-Tool @('--version')).stdout.Trim(); Check-Host
    $arguments=@($cli,'chain','verify','--config',$config,'--chain',$chain,'--target-kind','fixture')
    if($MaxConcurrency-ne1){$arguments+=@('--max-concurrency',[string]$MaxConcurrency)}
    for($i=0;$i-lt$Iterations;$i++){
        $result=Invoke-Tool $arguments; Check-Host
        [IO.File]::WriteAllText((Join-Path $run "cli-$i.json"),$result.stdout,[Text.UTF8Encoding]::new($false))
        $summary=$result.stdout|ConvertFrom-Json
        if($summary.projectCount-ne100-or$summary.entries.Count-ne4-or$summary.execution-ne'completed'-or$summary.compliance-ne'compliant'){throw 'Unexpected synthetic result; preserve evidence.'}
        $records.Add(@{surface='cli';iteration=$i;cache=$(if($i-eq0){'first-process/os-cache-unspecified'}else{'warm-os-cache/new-process'});maxConcurrency=$MaxConcurrency;wallMilliseconds=$result.wall;sampledPeakWorkingSetBytes=$result.peak;inputSha256=$summary.snapshot.inputIdentity.sha256;timings=$summary.timings;entryMilliseconds=@($summary.entries|ForEach-Object{(Get-Content -LiteralPath (Join-Path $summary.snapshot.outputDirectory ($_.reportDirectory+'/report.json')) -Raw|ConvertFrom-Json).runMetadata.elapsedMilliseconds})})
    }
    if(-not$SkipUi){
        for($i=0;$i-lt$Iterations;$i++){
            $timer=[Diagnostics.Stopwatch]::StartNew(); $process=[Diagnostics.Process]::Start((New-Start @($web,'--config',$config)))
            $client=[Net.Http.HttpClient]::new(); $stderr=$process.StandardError.ReadToEndAsync()
            try{
                $address=$null
                while($null-eq$address){
                    $line=$process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(30)).GetAwaiter().GetResult()
                    if($null-eq$line){throw 'Web exited before readiness.'}
                    if($line.StartsWith('ARCHSIFT_UI=')){$address=[Uri]::new($line.Substring(12))}
                }
                $stdout=$process.StandardOutput.ReadToEndAsync()
                $client.BaseAddress=[Uri]::new($address.GetLeftPart([UriPartial]::Authority)); $client.DefaultRequestHeaders.Add('X-ArchSift-Token',$address.Fragment.Substring(9))
                $startup=$timer.ElapsedMilliseconds; $jobTimer=[Diagnostics.Stopwatch]::StartNew()
                $body=[Net.Http.StringContent]::new((@{chainId='measure';targetKind='fixture';maxConcurrency=$MaxConcurrency}|ConvertTo-Json -Compress),[Text.Encoding]::UTF8,'application/json')
                if($MaxConcurrency-eq1){$body.Dispose();$body=[Net.Http.StringContent]::new('{"chainId":"measure","targetKind":"fixture"}',[Text.Encoding]::UTF8,'application/json')}
                $response=$client.PostAsync('/api/run/chain',$body).GetAwaiter().GetResult(); $response.EnsureSuccessStatusCode()|Out-Null
                $jobId=($response.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json).jobId
                do{
                    if($jobTimer.Elapsed.TotalSeconds-gt60){throw 'UI job timeout.'}
                    $job=$client.GetStringAsync('/api/jobs/'+$jobId).GetAwaiter().GetResult()|ConvertFrom-Json
                    $peak=$process.PeakWorkingSet64
                    if($job.state-eq'running'){Start-Sleep -Milliseconds 10}
                }while($job.state-eq'running')
                if($job.exitCode-ne0-or$job.chain.projectCount-ne100-or$job.chain.compliance-ne'compliant'){throw 'Unexpected UI result.'}
                $jobTimer.Stop(); Save-Json (Join-Path $run "ui-$i.json") $job.chain
                $records.Add(@{surface='ui';iteration=$i;cache=$(if($i-eq0){'first-process/os-cache-unspecified'}else{'warm-os-cache/new-process'});maxConcurrency=$MaxConcurrency;startupMilliseconds=$startup;wallMilliseconds=$jobTimer.ElapsedMilliseconds;sampledPeakWorkingSetBytes=$peak;inputSha256=$job.chain.snapshot.inputIdentity.sha256;timings=$job.chain.timings})
            }finally{
                if(-not$process.HasExited){$process.StandardInput.WriteLine('shutdown');$process.StandardInput.Flush();if(-not$process.WaitForExit(10000)){$process.Kill($true);$process.WaitForExit()}}
                $client.Dispose(); $process.Dispose(); Check-Host
            }
        }
    }
    $productInputs=@(& git -C $repo ls-files src schemas Directory.Build.props|Sort-Object|ForEach-Object{@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $repo $_)).Hash.ToLowerInvariant()}})
    $productIdentity=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($productInputs|ConvertTo-Json -Depth 4 -Compress)))).ToLowerInvariant()
    Save-Json (Join-Path $run 'measurements.json') @{fixtureRoot=$FixtureRoot;sourceCommit=(& git -C $repo rev-parse HEAD);sourceDirty=([bool](& git -C $repo status --porcelain));productInputs=$productInputs;productInputsSha256=$productIdentity;scriptSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant();sdk=$sdk;tfm='net10.0';productConfiguration=$Configuration;targetConfiguration='Debug';cpu=(Get-CimInstance Win32_Processor|Select-Object -First 1 -ExpandProperty Name);logicalProcessors=[Environment]::ProcessorCount;cliSha256=(Get-FileHash -LiteralPath $cli).Hash.ToLowerInvariant();webSha256=(Get-FileHash -LiteralPath $web).Hash.ToLowerInvariant();scope='100 projects / 200 inputs / 182 naming rules / four declaration groups; not IFX or assembly performance';records=$records.ToArray()}
    Write-Output ('chain-measurement-root='+$run)
}finally{Save-Json (Join-Path $run 'host-final.json') @{baselineSha256=$baseline;finalSha256=(Get-ArchSiftHostHash);equal=((Get-ArchSiftHostHash)-ceq$baseline)};Check-Host}
