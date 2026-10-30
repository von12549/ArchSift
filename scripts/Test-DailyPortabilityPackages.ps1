[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageBuildRoot,
      [Parameter(Mandatory)][string]$LocalFeed,
      [Parameter(Mandatory)][string]$OutputRoot,
      [Parameter(Mandatory)][string[]]$DotnetPaths,[string]$ReuseMatrixRoot='')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'HostState.ps1')
$baseline=Get-ArchSiftHostHash
function Need([bool]$value,[string]$message){if(-not $value){throw $message}}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Host-Check {Need ((Get-ArchSiftHostHash) -ceq $baseline) 'Host drift; stop and preserve evidence without repair.'}
$output=[IO.Path]::GetFullPath($OutputRoot)
Need (-not(Test-Path -LiteralPath $output) -and -not $output.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)) 'Use fresh external package-test evidence.'
[IO.Directory]::CreateDirectory($output)|Out-Null
function Save([string]$name,$value){$value|ConvertTo-Json -Depth 35|Set-Content -LiteralPath (Join-Path $output $name) -Encoding utf8NoBOM}
$build=Get-Content -LiteralPath (Join-Path $PackageBuildRoot 'package-build.json') -Raw|ConvertFrom-Json
Need ($build.version -ceq '0.11.4' -and $build.packages.Count -eq 5 -and $build.hostEqual) 'Package build identity differs.'
$feed=Join-Path $output 'feed';[IO.Directory]::CreateDirectory($feed)|Out-Null
foreach($package in Get-ChildItem -LiteralPath $LocalFeed -File -Filter '*.nupkg'|Where-Object Name -notlike 'ArchSift.*.nupkg'){
    Copy-Item -LiteralPath $package.FullName -Destination $feed
}
foreach($package in $build.packages){
    $path=Join-Path $PackageBuildRoot ('packages/'+$package.file)
    Need ((Hash $path) -ceq $package.sha256) 'Package bytes changed since build.'
    $zip=[IO.Compression.ZipFile]::OpenRead($path)
    try{
        $nuspec=@($zip.Entries|Where-Object FullName -like '*.nuspec');Need ($nuspec.Count -eq 1) 'Missing/duplicate nuspec.'
        $reader=[IO.StreamReader]::new($nuspec[0].Open());try{[xml]$xml=$reader.ReadToEnd()}finally{$reader.Dispose()}
        Need ($xml.package.metadata.version -ceq $build.version) 'Mixed package version.'
        foreach($dependency in $xml.SelectNodes('//*[local-name()="dependency"]')|Where-Object id -like 'ArchSift.*'){
            Need ($dependency.version -ceq ('['+$build.version+']')) 'Unpinned or mixed internal package edge.'
        }
    }finally{$zip.Dispose()}
    Copy-Item -LiteralPath $path -Destination $feed
}
$unpacked=Join-Path $output 'cli-unpacked'
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $feed 'ArchSift.Cli.0.11.4.nupkg'),$unpacked)
$cli=Join-Path $unpacked 'tools/net10.0/any/archsift.dll'
$cache=Join-Path $output 'packages'
$checks=[Collections.Generic.List[object]]::new()
function Start-Info([string]$dotnet,[string[]]$arguments,[string]$workdir){
    Host-Check
    $start=[Diagnostics.ProcessStartInfo]::new($dotnet);$start.WorkingDirectory=$workdir;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
    foreach($pair in @(@('DOTNET_CLI_HOME',(Join-Path $output 'cli-home')),@('DOTNET_ADD_GLOBAL_TOOLS_TO_PATH','0'),@('NUGET_PACKAGES',$cache),@('DOTNET_HOST_PATH',$dotnet),@('DOTNET_ROOT',(Split-Path $dotnet)),@('DOTNET_ROOT_X64',(Split-Path $dotnet)),@('DOTNET_MULTILEVEL_LOOKUP','0'),@('DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE','true'),@('DOTNET_GENERATE_ASPNET_CERTIFICATE','false'),@('DOTNET_ROLL_FORWARD','LatestPatch'))){$start.Environment[$pair[0]]=$pair[1]}
    $start.Environment['PATH']=(Split-Path $dotnet)+[IO.Path]::PathSeparator+$start.Environment['PATH']
    $start.Environment['ARCHSIFT_PROFILE_PATHS']=ConvertTo-Json -Compress -InputObject @($PROFILE.AllUsersAllHosts,$PROFILE.AllUsersCurrentHost,$PROFILE.CurrentUserAllHosts,$PROFILE.CurrentUserCurrentHost)
    return $start
}
function Child([string]$name,[string]$dotnet,[string[]]$arguments,[string]$workdir,[int]$expected=0){
    $p=[Diagnostics.Process]::Start((Start-Info $dotnet $arguments $workdir))
    try{
        $stdout=$p.StandardOutput.ReadToEndAsync();$stderr=$p.StandardError.ReadToEndAsync()
        if(-not $p.WaitForExit(240000)){$p.Kill($true);$p.WaitForExit();throw ('Package test timed out: '+$name)}
        $text=$stdout.GetAwaiter().GetResult();[IO.File]::WriteAllText((Join-Path $output ($name+'.stdout.log')),$text)
        [IO.File]::WriteAllText((Join-Path $output ($name+'.stderr.log')),$stderr.GetAwaiter().GetResult())
        Host-Check;$checks.Add(@{name=$name;exitCode=$p.ExitCode;expected=$expected;hostEqual=$true})
        Need ($p.ExitCode -eq $expected) ('Package check failed: '+$name)
        return $text.Trim()
    }finally{if(-not $p.HasExited){$p.Kill($true);$p.WaitForExit()};$p.Dispose();Host-Check}
}
function Api($session,[string]$path,$body=$null,[int]$expected=200){
    if($null -eq $body){$response=$session.client.GetAsync($path).GetAwaiter().GetResult()}
    else{$content=[Net.Http.StringContent]::new(($body|ConvertTo-Json -Depth 15 -Compress),[Text.Encoding]::UTF8,'application/json');try{$response=$session.client.PostAsync($path,$content).GetAwaiter().GetResult()}finally{$content.Dispose()}}
    try{Need ([int]$response.StatusCode -eq $expected) ('Unexpected API status: '+$path);$text=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult();if($text){$text|ConvertFrom-Json}}finally{$response.Dispose()}
}
function Client([uri]$address){
    Need ($address.Host -ceq '127.0.0.1' -and $address.Fragment.StartsWith('#session=')) 'Unexpected local UI identity.'
    $client=[Net.Http.HttpClient]::new();$client.BaseAddress=[uri]$address.GetLeftPart([UriPartial]::Authority);$client.Timeout=[TimeSpan]::FromSeconds(20)
    $client.DefaultRequestHeaders.Add('X-ArchSift-Token',$address.Fragment.Substring(9));return $client
}
$passed=$false;$matrix=[Collections.Generic.List[object]]::new()
try{
    foreach($dotnet in $DotnetPaths){
        $id='matrix-'+($matrix.Count+1);$fixture=Join-Path $(if($ReuseMatrixRoot){$ReuseMatrixRoot}else{$output}) $id
        if($ReuseMatrixRoot){foreach($package in $build.packages){Need ((Hash (Join-Path $ReuseMatrixRoot ('feed/'+$package.file))) -ceq $package.sha256) 'Reused matrix package identity differs.'}}
        else{& (Join-Path $PSScriptRoot 'Prepare-PortableVerificationFixture.ps1') -FixtureRoot $fixture -LocalFeed $feed -PackageCache $cache -Configuration Release -CliPath $cli -DotnetPath $dotnet -Packaged -CompatibleSdk -ConfiguredRestore | Out-Null}
        $receipt=Get-Content -LiteralPath (Join-Path $fixture 'fixture.json') -Raw|ConvertFrom-Json
        Need ($receipt.passed -eq 3 -and $receipt.failed -eq 0 -and $receipt.skipped -eq 0 -and $receipt.kind -ceq 'archsift-owned-packaged-consumer') 'Packaged synthetic coverage differs.'
        Need ($receipt.dotnet -ceq $dotnet -and (Hash $receipt.report.path) -ceq $receipt.report.sha256) 'Reused matrix report or selected host differs.'
        $matrix.Add($receipt);Write-Host ('Packaged compatible gate passed: '+$receipt.sdk)
    }
    Need (($matrix.sdk -contains '10.0.300') -and ($matrix.sdk -contains '10.0.303') -and @($matrix|Where-Object {[version]$_.sdk -ge [version]'10.0.400'}).Count -gt 0) 'Actual required SDK matrix is incomplete.'
    $baselineDotnet=($matrix|Where-Object sdk -CEQ '10.0.303'|Select-Object -First 1).dotnet
    if($ReuseMatrixRoot){$exact=Get-Content -LiteralPath (Join-Path $ReuseMatrixRoot 'exact-baseline/fixture.json') -Raw|ConvertFrom-Json;Need ($exact.passed -eq 3 -and $exact.sdk -ceq '10.0.303' -and (Hash $exact.report.path) -ceq $exact.report.sha256) 'Reused exact baseline differs.'}
    else{& (Join-Path $PSScriptRoot 'Prepare-PortableVerificationFixture.ps1') -FixtureRoot (Join-Path $output 'exact-baseline') -LocalFeed $feed -PackageCache $cache -Configuration Release -CliPath $cli -DotnetPath $baselineDotnet -Packaged | Out-Null}
    $last=Join-Path $(if($ReuseMatrixRoot){$ReuseMatrixRoot}else{$output}) ('matrix-'+$matrix.Count);$source=Join-Path $last 'source';$dotnet=$matrix[$matrix.Count-1].dotnet
    $config=Get-Content -LiteralPath (Join-Path $last 'state/config/full.json') -Raw|ConvertFrom-Json -AsHashtable
    foreach($scenario in @('exact-mismatch','missing-host','unknown-field')){
        $negative=$config.Clone();$negative.runDirectory=Join-Path $output ('negative-'+$scenario);$negative.dotnetCliHome=Join-Path $negative.runDirectory 'cli-home'
        if($scenario -ceq 'exact-mismatch'){$negative.Remove('minimumSdkVersion');$negative.Remove('maximumSdkVersionExclusive');$expected=4}
        elseif($scenario -ceq 'missing-host'){$negative.dotnetPath=Join-Path $output 'missing-dotnet.exe';$expected=5}
        else{$negative.unrecognizedSdkPolicy='must-fail';$expected=5}
        $path=Join-Path $output ($scenario+'.json');$negative|ConvertTo-Json -Depth 30|Set-Content -LiteralPath $path -Encoding utf8NoBOM
        [void](Child $scenario $dotnet @($cli,'test-gate','--config',$path) $source $expected)
        if($scenario -ceq 'exact-mismatch'){$blocked=Get-Content -LiteralPath (Join-Path $negative.runDirectory 'architecture-test-run.json') -Raw|ConvertFrom-Json;Need ($blocked.process.restore.status -ceq 'not-started') 'Exact mismatch reached restore.'}
    }
    [IO.Directory]::CreateDirectory((Join-Path $source '.config'))|Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $source '.archsift'))|Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $source 'architecture'))|Out-Null
    @{version=1;isRoot=$true;tools=@{'archsift.cli'=@{version=$build.version;commands=@('archsift');rollForward=$false}}}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $source '.config/dotnet-tools.json') -Encoding utf8NoBOM
    [void](Child 'tool-restore' $dotnet @('tool','restore','--tool-manifest',(Join-Path $source '.config/dotnet-tools.json'),'--configfile',(Join-Path $source 'NuGet.Config')) $source)
    $installed=Join-Path $cache ('archsift.cli/'+$build.version+'/tools/net10.0/any/archsift.dll')
    Need ((Hash (Join-Path $cache ('archsift.cli/'+$build.version+'/archsift.cli.'+$build.version+'.nupkg'))) -ceq (Hash (Join-Path $feed 'ArchSift.Cli.0.11.4.nupkg'))) 'Restored CLI package differs.'
    @{schemaVersion=1;version=$build.version;cliPackageSha256=(Hash (Join-Path $feed 'ArchSift.Cli.0.11.4.nupkg'));policyFile='policy.json';policySha256=(Hash (Join-Path $source 'policy.json'));requiredCases='required-cases.json';requiredCasesSha256=(Hash (Join-Path $source 'required-cases.json'));testProject='Portable.Architecture.Tests.csproj';testExecutableName='Portable.Architecture.Tests';rulesets=@('rules.json')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $source 'architecture/portable-gate.json') -Encoding utf8NoBOM
    $workspace=@{kind='archsift-workspace';schemaVersion=1;project=@{id=('owned-0114-'+[guid]::NewGuid().ToString('N'));root='..';entry='Portable.Architecture.Tests.csproj'};state=@{};runtime=@{mode='host';dotnetPath=$dotnet;minimumSdkVersion='10.0.300';maximumSdkVersionExclusive='10.1.0';allowPrerelease=$false};nuget=@{mode='configured';allowNetwork=$true;configFile='../NuGet.Config';cacheDirectory=$cache};gate=@{kind='ifx';metadataFile='../architecture/portable-gate.json'};workbench=@{policyFile='../policy.json';rulesets=@();build=@{mode='existing'}}}
    $workspacePath=Join-Path $source '.archsift/workspace.json';$workspace|ConvertTo-Json -Depth 15|Set-Content -LiteralPath $workspacePath -Encoding utf8NoBOM
    $doctor=(Child 'doctor-before-init' $dotnet @($installed,'workspace','doctor','--config',$workspacePath) $source)|ConvertFrom-Json
    Need (-not $doctor.hostProbed -and -not(Test-Path -LiteralPath $doctor.stateRoot)) 'Doctor probed a host or initialized state.'
    $init=(Child 'default-init' $dotnet @($installed,'workspace','init','--config',$workspacePath) $source)|ConvertFrom-Json
    Need ($init.stateRoot -ceq $doctor.stateRoot -and (Test-Path -LiteralPath $init.dailyTestConfig)) 'Default storage/daily configuration differs.'
    Save 'workspace-initialization.json' $init
    foreach($mode in @('workbench','launcher','workbench','launcher')){
        $id='ui-'+($checks.Count+1)+'-'+$mode
        $start=Start-Info $dotnet @($installed,'workspace','start','--config',$workspacePath,'--mode',$mode,'--no-browser') $source
        $start.RedirectStandardInput=$true;$start.Environment['ARCHSIFT_WORKSPACE_PARENT_CONTROL']='stdin-v1'
        $p=[Diagnostics.Process]::Start($start);$p.StandardInput.WriteLine('parent-owned');$p.StandardInput.Flush();$stderr=$p.StandardError.ReadToEndAsync();$client=$null;$remaining=$null
        try{
            $deadline=[DateTime]::UtcNow.AddSeconds(90);$address=$null
            while($true){$span=$deadline-[DateTime]::UtcNow;Need ($span.TotalSeconds -gt 0) 'UI readiness timed out.';$line=$p.StandardOutput.ReadLineAsync().WaitAsync($span).GetAwaiter().GetResult();Need ($null -ne $line) 'UI exited before readiness.';if($line.StartsWith('ARCHSIFT_UI=')){$address=[uri]$line.Substring(12)};if($line.StartsWith('ARCHSIFT_STATE=waiting')){break}}
            Need ($null -ne $address) 'Missing UI address.';$remaining=$p.StandardOutput.ReadToEndAsync();$client=Client $address;$session=@{client=$client}
            $workbench=$session;$childClient=$null
            try{
                if($mode -ceq 'launcher'){
                    $state=Api $session '/api/launcher/state';Need ($state.profiles.Count -eq 1) 'Launcher profile inventory differs.';$selectedProfile=$state.profiles[0]
                    $child=Api $session '/api/launcher/start' @{profileId=$selectedProfile.entry.profileId;catalogSha256=$state.catalogSha256;configSha256=$selectedProfile.sha256;selectionSha256=$state.selectionSha256}
                    $childClient=Client ([uri]$child.workbenchUrl);$workbench=@{client=$childClient}
                }
                $view=Api $workbench '/api/test-workbench'
                Need ($view.configured -and $view.logicalCaseKeys.Count -eq 2 -and $view.rules.Count -eq 1) 'Automatic packaged daily inventory differs.'
                [void](Api $workbench '/api/session/dirty' @{dirty=$true})
                [void](Api $workbench '/api/shutdown' @{} 409)
                [void](Api $workbench '/api/session/dirty' @{dirty=$false})
                if($mode -ceq 'launcher'){[void](Api $session '/api/launcher/stop' @{discardChanges=$false});[void](Api $session '/api/launcher/shutdown' @{} 202)}
                else{[void](Api $session '/api/shutdown' @{} 202)}
            }finally{if($null -ne $childClient){$childClient.Dispose()}}
            Need ($p.WaitForExit(30000) -and $p.ExitCode -eq 0) 'Owned UI did not close successfully.'
            [void]$remaining.GetAwaiter().GetResult();$errorText=$stderr.GetAwaiter().GetResult();[IO.File]::WriteAllText((Join-Path $output ($id+'.stderr.log')),$errorText)
            Need (@([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()|Where-Object Port -eq $address.Port).Count -eq 0) 'Owned UI listener remains.'
            Host-Check;$checks.Add(@{name=$id;exitCode=$p.ExitCode;hostEqual=$true;automaticInventory=$true;listenerClosed=$true;mode=$mode})
        }finally{if(-not $p.HasExited){$p.Kill($true);$p.WaitForExit()};if($null -ne $client){$client.Dispose()};$p.Dispose();Host-Check}
    }
    $lease=[IO.File]::Open((Join-Path $init.stateRoot '.workspace.lock'),'Open','ReadWrite','None');$lease.Dispose()
    $passed=$true
}finally{
    $equal=(Get-ArchSiftHostHash) -ceq $baseline
    Save 'results.json' @{schemaVersion=1;status=$(if($passed -and $equal){'passed'}else{'failed'});version=$build.version;packageBuildRoot=$PackageBuildRoot;packageSourceIdentity=$build.sourceInputsSha256;verificationScriptSha256=(Hash $PSCommandPath);reusedMatrixRoot=$ReuseMatrixRoot;packages=$build.packages;matrix=@($matrix.ToArray());checks=@($checks.ToArray());hostEqual=$equal;realIfxExecuted=$false;humanPerformed=$false;visualReview='separate-agent-review';published=$false}
    if(-not $equal){throw 'Host drift; evidence preserved without repair.'}
}
