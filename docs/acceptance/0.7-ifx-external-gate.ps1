[CmdletBinding()]
param(
    [ValidateSet('PrepareRestore','Capture','Build')][string]$Stage='PrepareRestore',
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
function RunDotnet([string]$name,[string[]]$arguments,[int]$timeoutSeconds=600) {
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
        if ($timeout -or $p.ExitCode -ne 0) { throw "$name failed; inspect external logs before continuing." }
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
        foreach($dir in @('C:\Program Files\dotnet\sdk\10.0.303','C:\Program Files\dotnet\packs','C:\Program Files\dotnet\shared')) {
            foreach($file in Get-ChildItem -LiteralPath $dir -File -Recurse) { [void]$inputs.Add($file.FullName) }
        }
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $run 'packages') -File -Recurse) {
            if ($file.Extension -in '.dll','.exe','.props','.targets','.nuspec','.cs','.json','.xml','.config','.txt','.editorconfig','.globalconfig') { [void]$inputs.Add($file.FullName) }
        }
        foreach($file in Get-ChildItem -LiteralPath $snapshot -File -Recurse) {
            if ($file.FullName.Contains('\obj\')) { [void]$inputs.Add($file.FullName) }
        }
        [void]$inputs.Add((Join-Path $run 'NuGet.Config'))
        Save (Join-Path $run 'external-input-paths.json') @($inputs | Sort-Object)
        $config=Get-Content -Raw 'D:\IFX-10-Root\ArchSift\config\ifx.json' | ConvertFrom-Json
        $config.output.directory=Join-Path $run 'reports'
        $config.rulesDirectory=Join-Path $run 'rules'
        Save (Join-Path $run 'config.json') $config
        RunDotnet 'capture' @('D:\ArchSift\src\ArchSift.Cli\bin\Release\net10.0\archsift.dll','evidence','capture','--config',(Join-Path $run 'config.json'),'--inputs',(Join-Path $run 'external-input-paths.json'),'--output',(Join-Path $run 'capture.json'))
        Write-Output ('capture-bytes='+(Get-Item -LiteralPath (Join-Path $run 'capture.json')).Length)
    } else {
        $sourceFiles=@(Get-Content -Raw (Join-Path $run 'source-files.json') | ConvertFrom-Json)
        RunDotnet 'build' @('build','IFX.sln','--no-restore','--disable-build-servers','-p:UseSharedCompilation=false','-p:NuGetAudit=false','-c','Debug','--verbosity','normal',('-bl:'+(Join-Path $run 'build.binlog')))
    }
} finally {
    if ([IO.Directory]::Exists($run)) { Save (Join-Path $run ($Stage+'-host-final.json')) ([ordered]@{hostBeforeSha256=$baseline;hostAfterSha256=(Get-ArchSiftHostHash);equal=((Get-ArchSiftHostHash) -ceq $baseline)}) }
    Write-Output "run-root=$run"
    if ((Get-ArchSiftHostHash) -cne $baseline) { throw 'Final host drift; preserve evidence.' }
}
