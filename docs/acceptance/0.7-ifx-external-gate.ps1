[CmdletBinding()]
param(
    [ValidateSet('PrepareRestore','Capture','Build','Evaluate','EvaluateRuntime','Reports','Bind','BoundReports')][string]$Stage='PrepareRestore',
    [ValidateSet('single','project')][string]$ContextModel='single',
    [string]$ReviewFile='review-project-contexts.json',
    [string]$ReportTag='bound',
    [string]$RunRoot,
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
$baseline=Get-ArchSiftHostHash
$target=[IO.Path]::GetFullPath($TargetRoot)
if (!$RunRoot) { $RunRoot='D:\ArchSift-lab\runs\ifx-external-0.7-'+[Guid]::NewGuid().ToString('N') }
$run=[IO.Path]::GetFullPath($RunRoot)
if (!$run.StartsWith('D:\ArchSift-lab\runs\ifx-external-0.7-',[StringComparison]::OrdinalIgnoreCase)) { throw 'Run must be a recorded external IFX run.' }
$snapshot=Join-Path $run 'snapshot'
$feed='C:\Users\von12\.nuget\packages'
$records=[Collections.Generic.List[object]]::new()
function Save([string]$path,$value) { [IO.File]::WriteAllText($path,($value|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false)) }
function CheckTarget {
    $head=(& git -C $target rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne 'cc06528ab17052baafa496df2a89f6cde9207384' -or (& git -C $target status --porcelain)) { throw 'Target commit or clean status changed.' }
    foreach($item in $sourceFiles) {
        if ((Get-FileHash -LiteralPath (Join-Path $target $item.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.sha256) { throw ('Source drift: '+$item.path) }
    }
}
function RunDotnet([string]$name,[string[]]$arguments,[int]$timeoutSeconds=600,[int]$expectedExit=0) {
    CheckTarget
    $psi=[Diagnostics.ProcessStartInfo]::new('C:\Program Files\dotnet\dotnet.exe')
    $psi.WorkingDirectory=$snapshot; $psi.UseShellExecute=$false; $psi.RedirectStandardOutput=$true; $psi.RedirectStandardError=$true
    foreach($arg in $arguments) { $psi.ArgumentList.Add($arg) }
    $psi.Environment['DOTNET_CLI_HOME']=Join-Path $run 'cli-home'
    $psi.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $psi.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    $psi.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
    $psi.Environment['DOTNET_NOLOGO']='1'
    $psi.Environment['DOTNET_CLI_UI_LANGUAGE']='en-US'
    $psi.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
    $psi.Environment['MSBUILDDISABLENODEREUSE']='1'
    $psi.Environment['NUGET_PACKAGES']=Join-Path $run 'packages'
    $p=[Diagnostics.Process]::Start($psi)
    try {
        $out=$p.StandardOutput.ReadToEndAsync(); $err=$p.StandardError.ReadToEndAsync()
        $timer=[Diagnostics.Stopwatch]::StartNew()
        while (!$p.WaitForExit(100) -and $timer.Elapsed.TotalSeconds -lt $timeoutSeconds) {}
        $timeout=!$p.HasExited
        if ($timeout) { $p.Kill($true); $p.WaitForExit() }
        [Threading.Tasks.Task]::WaitAll($out,$err)
        [IO.File]::WriteAllText((Join-Path $run ($name+'.stdout.log')),$out.Result,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $run ($name+'.stderr.log')),$err.Result,[Text.UTF8Encoding]::new($false))
        $equal=(Get-ArchSiftHostHash) -ceq $baseline
        $records.Add([ordered]@{name=$name;command=@($psi.FileName)+$arguments;exitCode=$p.ExitCode;timedOut=$timeout;hostStateEqual=$equal;elapsedMilliseconds=$timer.ElapsedMilliseconds})
        Save (Join-Path $run ($Stage+'-result.json')) ([ordered]@{stage=$Stage;targetRoot=$target;runRoot=$run;networkRestoreEnabled=$false;configuration='Debug';tfm='net10.0';nuGetAuditOverride=$false;checks=$records.ToArray();hostBeforeSha256=$baseline;hostStateEqual=$equal})
        Write-Output "$name exit=$($p.ExitCode) host-state-equal=$equal"
        CheckTarget
        if (!$equal) { throw 'Host drift; evidence retained, no repair.' }
        if ($timeout -or $p.ExitCode -ne $expectedExit) { throw "$name unexpected exit; inspect external logs before continuing." }
    } finally { $p.Dispose() }
}
try {
    if ($Stage -eq 'PrepareRestore') {
        if ([IO.Directory]::Exists($run)) { throw 'Prepare requires a new output directory.' }
        [void][IO.Directory]::CreateDirectory($snapshot)
        $tracked=@(& git -C $target -c core.quotepath=false ls-files)
        if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate tracked source.' }
        $sourceFiles=@(foreach($relative in $tracked) {
            $original=[IO.Path]::GetFullPath((Join-Path $target $relative))
            if (!$original.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe source path.' }
            $file=Get-Item -LiteralPath $original
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked source input rejected.' }
            $destination=Join-Path $snapshot $relative
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
            [IO.File]::Copy($original,$destination,$false)
            [ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash.ToLowerInvariant()}
        })
        Save (Join-Path $run 'source-files.json') $sourceFiles
        [IO.File]::WriteAllText((Join-Path $run 'NuGet.Config'),'<configuration><packageSources><clear /></packageSources><auditSources><clear /></auditSources></configuration>')
        RunDotnet 'sdk' @('--version') 30
        RunDotnet 'restore' @('restore','IFX.sln','--configfile',(Join-Path $run 'NuGet.Config'),'--source',$feed,'--packages',(Join-Path $run 'packages'),'--disable-parallel','--disable-build-servers','-p:NuGetAudit=false','--verbosity','minimal',('-bl:'+(Join-Path $run 'restore.binlog')))
    } elseif ($Stage -eq 'Capture') {
        $sourceFiles=@(Get-Content -Raw (Join-Path $run 'source-files.json') | ConvertFrom-Json)
        $inputs=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach($dir in @('C:\Program Files\dotnet\sdk\10.0.303','C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.11','C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App\10.0.11','C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\10.0.11','C:\Program Files\dotnet\packs\Microsoft.AspNetCore.App.Ref\10.0.11','C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Host.win-x64\10.0.11')) {
            foreach($file in Get-ChildItem -LiteralPath $dir -File -Recurse) { [void]$inputs.Add($file.FullName) }
        }
        foreach($pack in @('Microsoft.NETCore.App.Ref','Microsoft.AspNetCore.App.Ref','NETStandard.Library.Ref')) {
            foreach($file in Get-ChildItem -LiteralPath ('C:\Program Files\dotnet\packs\'+$pack) -File -Recurse -Filter '*.dll') { [void]$inputs.Add($file.FullName) }
        }
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $run 'packages') -File -Recurse) {
            if ($file.Extension -in '.dll','.exe','.props','.targets','.nuspec','.cs','.json','.xml','.config','.txt','.editorconfig','.globalconfig') { [void]$inputs.Add($file.FullName) }
        }
        foreach($file in Get-ChildItem -LiteralPath $snapshot -File -Recurse) {
            if ($file.FullName.Contains('\obj\')) { [void]$inputs.Add($file.FullName) }
        }
        [void]$inputs.Add((Join-Path $run 'NuGet.Config'))
        [void]$inputs.Add('C:\Program Files\dotnet\dotnet.exe')
        foreach($file in Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\host\fxr\10.0.11' -File) { [void]$inputs.Add($file.FullName) }
        Save (Join-Path $run 'external-input-paths.json') @($inputs | Sort-Object)
        $config=Get-Content -Raw 'D:\IFX-10-Root\ArchSift\config\ifx.json' | ConvertFrom-Json
        $config.output.directory=Join-Path $run 'reports'
        $config.rulesDirectory=Join-Path $run 'rules'
        Save (Join-Path $run 'config.json') $config
        RunDotnet 'capture-focused' @('D:\ArchSift\src\ArchSift.Cli\bin\Release\net10.0\archsift.dll','evidence','capture','--context-model',$ContextModel,'--config',(Join-Path $run 'config.json'),'--inputs',(Join-Path $run 'external-input-paths.json'),'--output',(Join-Path $run 'capture-focused.json'))
        $capture=Get-Content -Raw (Join-Path $run 'capture-focused.json') | ConvertFrom-Json
        foreach($file in $capture.inputIdentity.files) {
            $original=[IO.Path]::GetFullPath((Join-Path $target $file.path))
            $destination=[IO.Path]::GetFullPath((Join-Path $snapshot $file.path))
            if(!$original.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase) -or !$destination.StartsWith($snapshot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe captured snapshot path.' }
            if(!(Test-Path -LiteralPath $destination)) {
                [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
                [IO.File]::Copy($original,$destination,$false)
            }
            if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw 'Captured snapshot byte mismatch.' }
            if(!@($sourceFiles|Where-Object path -EQ $file.path).Count) { $sourceFiles+=[pscustomobject]@{path=$file.path;sha256=$file.sha256} }
        }
        Save (Join-Path $run 'source-files.json') $sourceFiles
        [IO.File]::WriteAllText((Join-Path $run 'capture-compact.json'),($capture|ConvertTo-Json -Depth 30 -Compress),[Text.UTF8Encoding]::new($false))
        Write-Output ('capture-bytes='+(Get-Item -LiteralPath (Join-Path $run 'capture-compact.json')).Length)
    } elseif ($Stage -in 'Evaluate','EvaluateRuntime') {
        $sourceFiles=@(Get-Content -Raw (Join-Path $run 'source-files.json') | ConvertFrom-Json)
        $capture=Get-Content -Raw (Join-Path $run 'capture-compact.json') | ConvertFrom-Json
        $properties=@($capture.propertyNames)+@('MSBuildProjectFullPath','AssemblyName','TargetPath','TargetFramework','MSBuildBinPath','MSBuildAllProjects','RepositoryRoot','Configuration','NuGetAudit','ContinuousIntegrationBuild','UseMicrosoftTestingPlatformRunner','IsTestProject','TargetFrameworkRootPath')
        $properties=@($properties | Sort-Object -Unique)
        $index=0
        foreach($project in @($sourceFiles | Where-Object {$_.path.EndsWith('.csproj',[StringComparison]::OrdinalIgnoreCase)})) {
            $prefix=if($Stage -eq 'EvaluateRuntime'){'resolve-runtime-'}else{'evaluate-'}
            RunDotnet ($prefix+$index.ToString('D3')) @('msbuild',$project.path,'-nologo','-t:ResolveReferences','-p:Configuration=Debug','-p:NuGetAudit=false','-p:BuildProjectReferences=false','-p:UseSharedCompilation=false',('-getProperty:'+($properties -join ',')),'-getItem:Compile,Analyzer,AdditionalFiles,EditorConfigFiles,ReferencePath,ReferenceCopyLocalPaths') 180
            $index++
        }
    } elseif ($Stage -eq 'Bind') {
        if($ContextModel -ne 'project') { throw 'Partition binding requires project context mode.' }
        $sourceFiles=@(Get-Content -Raw (Join-Path $run 'source-files.json') | ConvertFrom-Json)
        $cli='D:\ArchSift\src\ArchSift.Cli\bin\Release\net10.0\archsift.dll'
        RunDotnet 'context-seal' @($cli,'evidence','seal','--config',(Join-Path $run 'config.json'),'--capture',(Join-Path $run 'capture-compact.json'),'--review',(Join-Path $run $ReviewFile),'--output',(Join-Path $run 'assembly-manifest-v3.json')) 600
        $config=Get-Content -Raw (Join-Path $run 'config.json')|ConvertFrom-Json
        $config.build|Add-Member -NotePropertyName assemblyManifest -NotePropertyValue (Join-Path $run 'assembly-manifest-v3.json')
        Save (Join-Path $run 'config-bound.json') $config
        RunDotnet 'context-validate' @($cli,'evidence','validate','--config',(Join-Path $run 'config-bound.json')) 600
        $validated=Get-Content -Raw (Join-Path $run 'context-validate.stdout.log')|ConvertFrom-Json
        if(!$validated.sourceBound -or $validated.contexts.Count -ne 105) { throw 'All 105 current-source contexts must bind.' }
    } elseif ($Stage -eq 'BoundReports') {
        $sourceFiles=@(Get-Content -Raw (Join-Path $run 'source-files.json') | ConvertFrom-Json)
        $cli='D:\ArchSift\src\ArchSift.Cli\bin\Release\net10.0\archsift.dll'
        $rulesRoot=Join-Path $run 'rules'
        if(!(Test-Path -LiteralPath $rulesRoot)) { Copy-Item -LiteralPath 'D:\IFX-10-Root\ArchSift\rules' -Destination $rulesRoot -Recurse }
        foreach($file in Get-ChildItem -LiteralPath 'D:\IFX-10-Root\ArchSift\rules' -File -Recurse) {
            $destination=Join-Path $rulesRoot ([IO.Path]::GetRelativePath('D:\IFX-10-Root\ArchSift\rules',$file.FullName))
            if((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) { throw 'Copied real library changed; do not reuse.' }
        }
        RunDotnet ('class-B-'+$ReportTag) @($cli,'chain','verify','--config',(Join-Path $run 'config-bound.json'),'--chain',(Join-Path $rulesRoot 'chains\ifx-06-class-B-test.json'),'--target-kind','real') 600 4
        RunDotnet ('class-A-'+$ReportTag) @($cli,'chain','verify','--config',(Join-Path $run 'config-bound.json'),'--chain',(Join-Path $rulesRoot 'chains\ifx-04-class-A-test.json'),'--target-kind','real') 600 4
        $summary=Get-Content -Raw (Join-Path $run ('class-B-'+$ReportTag+'.stdout.log'))|ConvertFrom-Json
        if($summary.entries.Count -ne 1 -or !$summary.entries[0].coverage.sourceBound -or $summary.entries[0].ruleResults.Count -ne 406 -or @($summary.entries[0].ruleResults|Where-Object {$_.status -notin 'pass','violation','not-applicable'}).Count) { throw 'B conformance must be current-source and fully decidable.' }
    } elseif ($Stage -eq 'Reports') {
        $sourceFiles=@(Get-Content -Raw (Join-Path $run 'source-files.json') | ConvertFrom-Json)
        $cli='D:\ArchSift\src\ArchSift.Cli\bin\Release\net10.0\archsift.dll'
        RunDotnet 'seal-rejected' @($cli,'evidence','seal','--config',(Join-Path $run 'config.json'),'--capture',(Join-Path $run 'capture-compact.json'),'--review',(Join-Path $run 'review-unapproved.json'),'--output',(Join-Path $run 'must-not-exist-manifest.json')) 180 2
        if (Test-Path -LiteralPath (Join-Path $run 'must-not-exist-manifest.json')) { throw 'Rejected review produced a manifest.' }
        $rulesRoot=Join-Path $run 'rules'
        if (Test-Path -LiteralPath $rulesRoot) { throw 'Real copied library must be new.' }
        [void][IO.Directory]::CreateDirectory($rulesRoot)
        foreach($file in Get-ChildItem -LiteralPath 'D:\IFX-10-Root\ArchSift\rules' -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath('D:\IFX-10-Root\ArchSift\rules',$file.FullName)
            $destination=Join-Path $rulesRoot $relative
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
            [IO.File]::Copy($file.FullName,$destination,$false)
        }
        $config=Get-Content -Raw (Join-Path $run 'config.json') | ConvertFrom-Json
        $paths=@(Get-Content -Raw (Join-Path $run 'assembly-paths-unbound.json') | ConvertFrom-Json)
        $config.build | Add-Member -NotePropertyName assemblyPaths -NotePropertyValue $paths
        Save (Join-Path $run 'config-unbound.json') $config
        RunDotnet 'class-B-unbound' @($cli,'chain','verify','--config',(Join-Path $run 'config-unbound.json'),'--chain',(Join-Path $rulesRoot 'chains\ifx-06-class-B-test.json'),'--target-kind','real') 600 4
        RunDotnet 'class-A-regression' @($cli,'chain','verify','--config',(Join-Path $run 'config.json'),'--chain',(Join-Path $rulesRoot 'chains\ifx-04-class-A-test.json'),'--target-kind','real') 600 4
    } else {
        $sourceFiles=@(Get-Content -Raw (Join-Path $run 'source-files.json') | ConvertFrom-Json)
        RunDotnet 'build' @('build','IFX.sln','--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-p:NuGetAudit=false','-c','Debug','--verbosity','normal',('-bl:'+(Join-Path $run 'build.binlog')))
    }
} finally {
    if ([IO.Directory]::Exists($run)) { Save (Join-Path $run ($Stage+'-host-final.json')) ([ordered]@{hostBeforeSha256=$baseline;hostAfterSha256=(Get-ArchSiftHostHash);equal=((Get-ArchSiftHostHash) -ceq $baseline)}) }
    Write-Output "run-root=$run"
    if ((Get-ArchSiftHostHash) -cne $baseline) { throw 'Final host drift; preserve evidence.' }
}
