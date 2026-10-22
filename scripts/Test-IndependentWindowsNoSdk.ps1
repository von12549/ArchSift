# Windows PowerShell 5.1 compatible: runs inside an unmodified Server Core image.
param([string]$InputRoot='C:\input',[string]$WorkRoot='C:\work')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Need([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Save([string]$Path,$Value){[IO.File]::WriteAllText($Path,(($Value|ConvertTo-Json -Depth 30)+"`n"),[Text.UTF8Encoding]::new($false))}
function HostHash{
    $values=[ordered]@{}
    foreach($scope in @('User','Machine','Process')){
        $items=[Environment]::GetEnvironmentVariables($scope)
        foreach($key in @($items.Keys)|Sort-Object){$values["$scope/$key"]=[string]$items[$key]}
    }
    foreach($path in @($PROFILE.AllUsersAllHosts,$PROFILE.AllUsersCurrentHost,$PROFILE.CurrentUserAllHosts,$PROFILE.CurrentUserCurrentHost)){$values["profile/$path"]=if([IO.File]::Exists($path)){Hash $path}else{'absent'}}
    $algorithm=[Security.Cryptography.SHA256]::Create()
    try{([BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes(($values|ConvertTo-Json -Compress))))).Replace('-','').ToLowerInvariant()}finally{$algorithm.Dispose()}
}
Need ($InputRoot -ceq 'C:\input' -and $WorkRoot -ceq 'C:\work') 'Only fixed container-owned paths are permitted.'
Need (-not(Test-Path $WorkRoot)) 'Fresh guest work root required.'
[void][IO.Directory]::CreateDirectory($WorkRoot)
$before=HostHash;$checks=New-Object 'Collections.Generic.List[object]';$web=$null
function NoSdk {
    $commands=@(Get-Command dotnet -ErrorAction SilentlyContinue)
    $paths=@('C:\Program Files\dotnet','C:\Program Files (x86)\dotnet',(Join-Path $env:USERPROFILE '.dotnet'))
    Need ($commands.Count -eq 0 -and @($paths|Where-Object{Test-Path -LiteralPath $_}).Count -eq 0) 'Container has an installed .NET SDK/runtime.'
    @{dotnetCommandAbsent=$true;knownCoreInstallDirectoriesAbsent=$true;framework4MayBePresent=$true}
}
function ChildInfo([string]$Exe,[string[]]$Arguments){
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$Exe;$start.WorkingDirectory=$WorkRoot;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.Arguments=(@($Arguments|ForEach-Object{Need (-not $_.Contains('"') -and -not $_.EndsWith('\')) 'Controlled argument contains unsupported quote/trailing slash.';'"'+$_+'"'}) -join ' ')
    $start.StandardOutputEncoding=New-Object Text.UTF8Encoding($false,$true);$start.StandardErrorEncoding=New-Object Text.UTF8Encoding($false,$true)
    $start.EnvironmentVariables['DOTNET_CLI_HOME']=Join-Path $WorkRoot 'cli-home';$start.EnvironmentVariables['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$start.EnvironmentVariables['DOTNET_BUNDLE_EXTRACT_BASE_DIR']=Join-Path $WorkRoot 'bundle-extract'
    $start
}
function Run([string]$Name,[string]$Exe,[string[]]$Arguments,[int]$Expected=0){
    Need ((HostHash)-ceq$before) 'Guest drift before native command.'
    $process=[Diagnostics.Process]::Start((ChildInfo $Exe $Arguments))
    try{
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(120000)){$process.Kill();$process.WaitForExit();throw 'Owned native command timed out.'}
        [Threading.Tasks.Task]::WaitAll(@($stdout,$stderr))
        [IO.File]::WriteAllText((Join-Path $WorkRoot ($Name+'.stdout.log')),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $WorkRoot ($Name+'.stderr.log')),$stderr.Result,[Text.UTF8Encoding]::new($false))
        $check=@{name=$Name;exitCode=$process.ExitCode;expectedExit=$Expected;hostEqual=((HostHash)-ceq$before)};$checks.Add($check);Save (Join-Path $WorkRoot 'command-checks.json') $checks.ToArray()
        Need ($check.hostEqual -and $process.ExitCode -eq $Expected) ('Native command failed: '+$Name)
        $stdout.Result
    }finally{$process.Dispose()}
}
try{
    $initial=NoSdk;Save (Join-Path $WorkRoot 'no-sdk-before.json') $initial
    $transfer=Join-Path $InputRoot 'transfer';$assets=Join-Path $transfer 'assets'
    $build=Get-Content (Join-Path $transfer 'build-result.json') -Raw|ConvertFrom-Json
    $zip=Join-Path $assets ('archsift-workbench-'+$build.version+'-win-x64.zip');$setup=Join-Path $assets 'ArchSift.Setup.exe'
    $payload=Join-Path $WorkRoot 'payload';Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($zip,$payload)
    $manifest=Get-Content (Join-Path $payload 'package-manifest.json') -Raw|ConvertFrom-Json
    Need ($manifest.sourceCommit -ceq $build.sourceCommit -and $manifest.version -ceq $build.version -and $manifest.selfContained -and $manifest.rid -ceq 'win-x64') 'Guest manifest differs from source-bound build.'
    foreach($entry in $manifest.files){$path=Join-Path $payload $entry.path;Need ((Hash $path)-ceq$entry.sha256 -and (Get-Item $path).Length -eq $entry.bytes) 'Guest extracted payload differs.'}
    $cli=Join-Path $payload 'archsift.exe';$webExe=Join-Path $payload 'web/ArchSift.Web.exe'
    Need ((Run 'cli-version' $cli @('--version')).Trim() -ceq ('archsift '+$build.version)) 'Guest CLI version differs.'
    Need ((Run 'web-version' $webExe @('--version')).Trim() -ceq ('archsift-web '+$build.version)) 'Guest Web version differs.'
    Need ((Run 'setup-version' $setup @('--version')).Trim() -ceq ('archsift-setup '+$build.version)) 'Guest Setup version differs.'
    $notices=Run 'setup-notices' $setup @('--notices')
    foreach($path in @((Join-Path $payload 'LICENSE'),(Join-Path $payload 'licenses/microsoft.netcore.app.runtime.win-x64-10.0.13-LICENSE.TXT'),(Join-Path $payload 'licenses/microsoft.netcore.app.runtime.win-x64-10.0.13-THIRD-PARTY-NOTICES.TXT'))){Need ($notices.Contains(([IO.File]::ReadAllText($path)).Trim())) 'Guest Setup original notices differ.'}
    $target=Join-Path $WorkRoot 'source';[void][IO.Directory]::CreateDirectory((Join-Path $target 'P000'))
    [IO.File]::WriteAllText((Join-Path $target 'P000/P000.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $target 'P000/Marker.cs'),'namespace P000; public class Marker {}',[Text.UTF8Encoding]::new($false))
    $rules=Get-Content (Join-Path $payload 'templates/rules/naming.json') -Raw|ConvertFrom-Json;$rules.rules[0].parameters.requiredName.value='P*'
    $rulePath=Join-Path $WorkRoot 'rules.json';Save $rulePath $rules
    $config=@{schemaVersion=1;target=@{root=$target};rulesets=@($rulePath);build=@{mode='existing';configuration='Debug';allowNetwork=$false};output=@{directory=(Join-Path $WorkRoot 'reports');formats=@('json','html')};rulesDirectory=(Join-Path $WorkRoot 'library')}
    $configPath=Join-Path $WorkRoot 'config.json';Save $configPath $config
    [void](Run 'analyze-without-sdk' $cli @('analyze','--config',$configPath))
    $web=[Diagnostics.Process]::Start((ChildInfo $webExe @('--config',$configPath)))
    $errors=$web.StandardError.ReadToEndAsync();$address=$null;$deadline=[DateTime]::UtcNow.AddSeconds(30)
    while([DateTime]::UtcNow -lt $deadline){
        $lineTask=$web.StandardOutput.ReadLineAsync();Need ($lineTask.Wait(30000)) 'Guest Web startup timed out.';$line=$lineTask.Result
        Need ($null -ne $line) 'Guest Web exited before startup.'
        if($line.StartsWith('ARCHSIFT_UI=')){$address=[Uri]$line.Substring(12);break}
    }
    Need ($null -ne $address -and $address.Host -ceq '127.0.0.1') 'Guest Web must be loopback only.'
    $rest=$web.StandardOutput.ReadToEndAsync();$base=$address.GetLeftPart([UriPartial]::Authority);$headers=@{'X-ArchSift-Token'=$address.Fragment.Substring(9)}
    Need ((Invoke-WebRequest ($base+'/') -UseBasicParsing).StatusCode -eq 200) 'Guest Web root unavailable.'
    $job=Invoke-RestMethod ($base+'/api/run/analyze') -Method Post -Headers $headers -ContentType 'application/json' -Body '{"targetKind":"fixture"}'
    $deadline=[DateTime]::UtcNow.AddSeconds(30)
    do{$view=Invoke-RestMethod ($base+'/api/jobs/'+$job.jobId) -Headers $headers;if($view.state -ne 'running'){break};Start-Sleep -Milliseconds 50}while([DateTime]::UtcNow -lt $deadline)
    Need ($view.exitCode -eq 0) 'Guest packaged Web analysis failed without SDK.'
    $report=Invoke-WebRequest ($base+'/api/jobs/'+$job.jobId+'/report/json') -UseBasicParsing -Headers $headers
    [IO.File]::WriteAllText((Join-Path $WorkRoot 'web-analysis-report.json'),$report.Content,[Text.UTF8Encoding]::new($false))
    $shutdown=Invoke-WebRequest ($base+'/api/shutdown') -UseBasicParsing -Method Post -Headers $headers -ContentType 'application/json' -Body '{}'
    Need ($shutdown.StatusCode -eq 202 -and $web.WaitForExit(30000) -and $web.ExitCode -eq 0) 'Guest Web shutdown failed.'
    [Threading.Tasks.Task]::WaitAll(@($errors,$rest));$web.Dispose();$web=$null
    $socket=New-Object Net.Sockets.TcpClient
    try{$listenerOpen=$false;try{$socket.Connect('127.0.0.1',$address.Port);$listenerOpen=$true}catch{};Need (-not $listenerOpen) 'Guest owned listener remained.'}finally{$socket.Dispose()}
    $install=Join-Path $WorkRoot 'fresh-install'
    $planText=Run 'setup-plan-install' $setup @('plan','install','--root',$install,'--package',$zip,'--sha256',(Hash $zip),'--target',$target,'--entry','P000/P000.csproj','--tfm','net10.0','--smoke','true')
    Need (-not(Test-Path $install)) 'Guest read-only plan created install root.'
    $plan=$planText|ConvertFrom-Json;$planPath=Join-Path $WorkRoot 'install-plan.json';[IO.File]::WriteAllText($planPath,$planText,[Text.UTF8Encoding]::new($false))
    $receipt=(Run 'setup-apply-private-ui' $setup @('apply','--plan',$planPath,'--plan-id',$plan.planId))|ConvertFrom-Json
    Need ($receipt.outcome -ceq 'success' -and $receipt.after.selectedVersion -ceq $build.version) 'Guest installed selection differs.'
    [void](Run 'setup-inspect' $setup @('inspect','--root',$install))
    $final=NoSdk;Need ((HostHash)-ceq$before) 'Guest host drift.'
    Save (Join-Path $WorkRoot 'verification.json') @{status='passed';sourceCommit=$build.sourceCommit;version=$build.version;zipSha256=(Hash $zip);setupSha256=(Hash $setup);operatingSystem=[Environment]::OSVersion.VersionString;architecture=$env:PROCESSOR_ARCHITECTURE;noSdkBefore=$initial;noSdkAfter=$final;manifestFilesVerified=$manifest.files.Count;checks=$checks.ToArray();loopbackWebAnalysisAndShutdown=$true;ownedListenerClosed=$true;standaloneSetupInstallAndPrivateUi=$true;hostBefore=$before;hostAfter=(HostHash);hostEqual=$true;scope='Independent GitHub-hosted Windows VM, official Server Core container with no installed .NET Core SDK/runtime. No desktop visual or user hands-on acceptance.'}
    Write-Output 'independent-windows-servercore-no-sdk=passed'
}catch{Save (Join-Path $WorkRoot 'failure.json') @{status='stopped';message=$_.Exception.Message;scriptStackTrace=$_.ScriptStackTrace;hostEqual=((HostHash)-ceq$before);completedChecks=$checks.Count};throw}
finally{if($null -ne $web){if(-not $web.HasExited){$web.Kill();$web.WaitForExit()};$web.Dispose()};if((HostHash)-cne$before){throw 'Guest host drift; no repair.'}}
