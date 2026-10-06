[CmdletBinding()]
param([string]$LabRoot='D:\ArchSift-lab',[string]$LocalFeed=(Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'),[string]$Image='mcr.microsoft.com/dotnet/sdk:10.0')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('runs/linux-'+[Guid]::NewGuid().ToString('N'))
if($run.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Linux snapshot must be outside source.'}
$source=Join-Path $run 'source';[void][IO.Directory]::CreateDirectory($source)
$fixture=Join-Path $run 'fixture'
foreach($project in @('A','B')){
    $folder=Join-Path $fixture $project;[void][IO.Directory]::CreateDirectory($folder)
    $reference=if($project-eq'A'){'<ItemGroup><ProjectReference Include="../B/B.csproj" /></ItemGroup>'}else{''}
    [IO.File]::WriteAllText((Join-Path $folder ($project+'.csproj')),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>'+$reference+'</Project>')
}
$rules=Join-Path $run 'rules.json'
[IO.File]::WriteAllText($rules,'{"schemaVersion":1,"id":"parity","version":"1","description":"跨平台","rules":[{"id":"edge","type":"project-reference","enabled":true,"scope":{"kind":"project","match":"glob","value":"**"},"parameters":{"source":{"kind":"project","match":"exact","value":"A/A.csproj"},"target":{"kind":"project","match":"exact","value":"B/B.csproj"}},"severity":"warning","reason":"parity"}],"exceptions":[]}')
$linuxConfig=@{schemaVersion=1;target=@{root='/work/fixture'};rulesets=@('/work/rules.json');output=@{directory='/work/linux-reports';formats=@('json','html','sarif')}}
[IO.File]::WriteAllText((Join-Path $run 'linux-config.json'),($linuxConfig|ConvertTo-Json -Depth 8))
$windowsConfig=@{schemaVersion=1;target=@{root=$fixture};rulesets=@($rules);output=@{directory=(Join-Path $run 'windows-reports');formats=@('json','html','sarif')}}
[IO.File]::WriteAllText((Join-Path $run 'windows-config.json'),($windowsConfig|ConvertTo-Json -Depth 8))
$files=(& git -C $repository -c core.fsmonitor=false ls-files --cached --others --exclude-standard -z) -join "`n"
if($LASTEXITCODE-ne0){throw 'Cannot identify current source.'}
$inputs=@()
foreach($relative in $files.Split([char]0,[StringSplitOptions]::RemoveEmptyEntries)){
    $relative=$relative.TrimStart("`n");$original=[IO.Path]::GetFullPath((Join-Path $repository $relative))
    if(-not$original.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Source path containment failed.'}
    if(-not[IO.File]::Exists($original)){continue}
    if((Get-Item -LiteralPath $original).Attributes-band[IO.FileAttributes]::ReparsePoint){throw 'Linked source refused.'}
    $destination=Join-Path $source $relative;[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination));Copy-Item -LiteralPath $original -Destination $destination
    $inputs+=@{path=$relative;sha256=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$imageIdentity=(& docker image inspect --format '{{.Id}}' $Image)
if($LASTEXITCODE-ne0){throw 'Linux SDK image unavailable; do not pull implicitly.'}
$name='archsift-linux-'+[Guid]::NewGuid().ToString('N')
$arguments=@('run','--rm','--name',$name,'--pull','never','--network','none','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges','--tmpfs','/tmp:rw',
    '--mount',('type=bind,source='+$run+',target=/work'), '--mount',('type=bind,source='+[IO.Path]::GetFullPath($LocalFeed)+',target=/feed,readonly'),
    '-e','DOTNET_CLI_HOME=/work/home','-e','DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0','-e','DOTNET_CLI_TELEMETRY_OPTOUT=1','-e','DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1','-e','NUGET_PACKAGES=/work/cache',
    '-w','/work/source',$imageIdentity.Trim(),'bash','scripts/verify-linux.sh')
$start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command docker).Source;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
foreach($arg in $arguments){$start.ArgumentList.Add($arg)}
$process=$null;$code=$null;$parity=$false
try {
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    if(-not$process.WaitForExit(180000)){& docker stop --time 2 $name | Out-Null;throw 'Linux verification timeout.'}
    [Threading.Tasks.Task]::WaitAll($stdout,$stderr);$code=$process.ExitCode
    [IO.File]::WriteAllText((Join-Path $run 'stdout.log'),$stdout.Result);[IO.File]::WriteAllText((Join-Path $run 'stderr.log'),$stderr.Result)
    Write-Output $stdout.Result;Write-Output $stderr.Result
    foreach($file in $inputs){if((Get-FileHash -LiteralPath (Join-Path $repository $file.path) -Algorithm SHA256).Hash.ToLowerInvariant()-cne$file.sha256){throw 'Current source changed during Linux verification.'}}
    if($code-ne0){throw 'Linux verification failed; logs retained.'}
    $windows=[Diagnostics.ProcessStartInfo]::new();$windows.FileName=(Get-Command dotnet).Source;$windows.UseShellExecute=$false;$windows.CreateNoWindow=$true;$windows.RedirectStandardOutput=$true;$windows.RedirectStandardError=$true
    foreach($arg in @((Join-Path $repository 'src/ArchSift.Cli/bin/Release/net10.0/archsift.dll'),'verify','--config',(Join-Path $run 'windows-config.json'))){$windows.ArgumentList.Add($arg)}
    $windows.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'windows-home');$windows.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $win=[Diagnostics.Process]::Start($windows);$winOut=$win.StandardOutput.ReadToEndAsync();$winErr=$win.StandardError.ReadToEndAsync()
    try{if(-not$win.WaitForExit(180000)){$win.Kill($true);throw 'Windows parity timeout.'};[Threading.Tasks.Task]::WaitAll($winOut,$winErr);if($win.ExitCode-ne0){throw 'Windows parity run failed.'};[IO.File]::WriteAllText((Join-Path $run 'windows-report.json'),$winOut.Result)}finally{$win.Dispose()}
    $a=Get-Content -LiteralPath (Join-Path $run 'windows-report.json') -Raw|ConvertFrom-Json
    $b=Get-Content -LiteralPath (Join-Path $run 'linux-report.json') -Raw|ConvertFrom-Json
    $fields=@('schemaVersion','operation','toolVersion','inputIdentity','scope','projects','buildContext','engineVersions','execution','compliance','ruleResults','findings','coverage','limitations','executionErrors')
    $parity=($a|Select-Object -Property $fields|ConvertTo-Json -Depth 64 -Compress)-ceq($b|Select-Object -Property $fields|ConvertTo-Json -Depth 64 -Compress)
    if(-not$parity){throw 'Cross-platform core report mismatch.'}
    Write-Output 'cross-platform-core-parity=True'
} finally {
    if($process){$process.Dispose()}
    $equal=(Get-ArchSiftHostHash)-ceq$before
    $result=@{runRoot=$run;image=$Image;imageIdentity=$imageIdentity.Trim();exitCode=$code;networkEnabled=$false;hostStateEqual=$equal;coreParity=$parity;sourceInputs=$inputs}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 8))
    Write-Output ('linux-run='+$run+' host-state-equal='+$equal)
    if(-not$equal){throw 'Host drift detected; evidence preserved, no repair.'}
}
