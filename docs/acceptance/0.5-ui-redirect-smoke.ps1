# Called only by the reviewed operator wrapper (or on a synthetic configuration).
# Successful UI URL/token is held in memory and never written to the evidence directory.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$Config,
    [Parameter(Mandatory)][string]$RunRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$Executable=[IO.Path]::GetFullPath($Executable);$Config=[IO.Path]::GetFullPath($Config);$RunRoot=[IO.Path]::GetFullPath($RunRoot)
function Assert-WebGone([int]$WebId){
    $gone=$false
    try{$web=[Diagnostics.Process]::GetProcessById($WebId);try{$gone=$web.HasExited}finally{$web.Dispose()}}catch [ArgumentException]{$gone=$true}
    $listening=@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object OwningProcess -eq $WebId).Count -gt 0
    if(-not$gone-or$listening){throw 'Redirected UI Web PID/listener remains; preserve owner evidence.'}
    return $true
}
$pipe=[Diagnostics.ProcessStartInfo]::new();$pipe.FileName=$Executable;$pipe.UseShellExecute=$false;$pipe.CreateNoWindow=$true
$pipe.RedirectStandardOutput=$true;$pipe.RedirectStandardError=$true
foreach($arg in @('ui','--config',$Config)){$pipe.ArgumentList.Add($arg)}
$pipe.Environment['DOTNET_CLI_HOME']=(Join-Path $RunRoot 'pipe-home');$pipe.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
$pipeProcess=[Diagnostics.Process]::Start($pipe)
$pipeExit=$null;$webId=$null;$pipeReady=$false;$pipeGone=$false
[IO.File]::WriteAllText((Join-Path $RunRoot 'pipe-owner.json'),(@{cliPid=$pipeProcess.Id;executable=$Executable;configPath=$Config}|ConvertTo-Json))
try{
    $stderrTask=$pipeProcess.StandardError.ReadToEndAsync()
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds(30);$url=$null;$waiting=$false
    while(-not($url-and$webId-and$waiting)){
        $remaining=[int][Math]::Max(1,($deadline-[DateTimeOffset]::UtcNow).TotalMilliseconds)
        if($remaining-le1){throw 'Redirected UI readiness timed out; preserve owner evidence.'}
        $read=$pipeProcess.StandardOutput.ReadLineAsync()
        if(-not$read.Wait($remaining)){throw 'Redirected UI readiness timed out; preserve owner evidence.'}
        $line=$read.Result
        if($null-eq$line){throw 'Redirected UI exited before readiness; preserve owner evidence.'}
        if($line.StartsWith('ARCHSIFT_UI=',[StringComparison]::Ordinal)){$url=$line.Substring(12)}
        elseif($line.StartsWith('ARCHSIFT_PID=',[StringComparison]::Ordinal)){
            $pidText=$line.Substring(13)
            if($pidText-notmatch'^[1-9][0-9]{0,9}$'){throw 'Redirected UI PID is invalid.'}
            $webId=[int]$pidText
        }
        elseif($line.StartsWith('ARCHSIFT_STATE=waiting',[StringComparison]::Ordinal)){$waiting=$true}
    }
    $uri=[Uri]::new($url)
    if($uri.Scheme-cne'http'-or$uri.Host-cne'127.0.0.1'-or$uri.Fragment-notmatch'^#session=[0-9a-f]{64}$'){throw 'Redirected UI address/token contract failed.'}
    $client=[Net.Http.HttpClient]::new()
    try{
        $client.Timeout=[TimeSpan]::FromSeconds(15)
        $client.BaseAddress=[Uri]::new($uri.GetLeftPart([UriPartial]::Authority))
        $client.DefaultRequestHeaders.Add('X-ArchSift-Token',$uri.Fragment.Substring(9))
        $content=[Net.Http.StringContent]::new('{}',[Text.Encoding]::UTF8,'application/json')
        try{$shutdown=$client.PostAsync('/api/shutdown',$content);if(-not$shutdown.Wait(20000)){throw 'Redirected UI shutdown timed out.'};$response=$shutdown.Result;try{if(-not$response.IsSuccessStatusCode){throw 'Redirected UI shutdown was rejected.'}}finally{$response.Dispose()}}finally{$content.Dispose()}
    }finally{$client.Dispose()}
    $tail=$pipeProcess.StandardOutput.ReadToEndAsync()
    if(-not$pipeProcess.WaitForExit(30000)){throw 'Redirected CLI did not exit after Safe shutdown; preserve owner evidence.'}
    if(-not$tail.Wait(5000)-or-not$stderrTask.Wait(5000)){throw 'Redirected CLI output streams did not drain.'}
    if(($tail.Result+$stderrTask.Result).Contains('#session=')){throw 'Redirected CLI wrote a session token outside the URL line.'}
    $pipeExit=$pipeProcess.ExitCode
    if($pipeExit-ne0){throw ('Redirected CLI exit code '+$pipeExit+'.')}
    $pipeReady=$true;$pipeGone=Assert-WebGone $webId
}finally{$pipeProcess.Dispose()}

# Direct stdout/stderr-to-file checks use commands that cannot generate a session URL.
# The successful UI start above is tested through an OS pipe and shut down in memory.
$versionOut=Join-Path $RunRoot 'file-version.stdout.txt';$versionErr=Join-Path $RunRoot 'file-version.stderr.txt'
$version=Start-Process -FilePath $Executable -ArgumentList @('--version') -RedirectStandardOutput $versionOut -RedirectStandardError $versionErr -WindowStyle Hidden -PassThru
try{if(-not$version.WaitForExit(30000)){throw 'File stdout version command timed out.'};if($version.ExitCode-ne0){throw 'File stdout version command failed.'}}finally{$version.Dispose()}
$versionText=[IO.File]::ReadAllText($versionOut);$versionError=[IO.File]::ReadAllText($versionErr)
if($versionText.Trim()-cne'archsift 0.5.0'-or$versionError.Length-ne0){throw 'Direct file stdout did not contain the expected 0.5.0 version.'}
$invalid=Join-Path $RunRoot 'invalid-config.json';[IO.File]::WriteAllText($invalid,'{}')
$errorOut=Join-Path $RunRoot 'file-invalid.stdout.txt';$errorErr=Join-Path $RunRoot 'file-invalid.stderr.txt'
$invalidArg='"'+$invalid+'"'
$bad=Start-Process -FilePath $Executable -ArgumentList @('ui','--config',$invalidArg) -RedirectStandardOutput $errorOut -RedirectStandardError $errorErr -WindowStyle Hidden -PassThru
try{if(-not$bad.WaitForExit(30000)){throw 'File stderr invalid-config command timed out.'};if($bad.ExitCode-ne2){throw ('File stderr invalid-config exit code '+$bad.ExitCode+'.')}}finally{$bad.Dispose()}
$badOutput=[IO.File]::ReadAllText($errorOut);$badError=[IO.File]::ReadAllText($errorErr)
if($badOutput.Length-ne0-or$badError.Length-eq0){throw 'Direct file stderr did not contain a configuration failure.'}
foreach($value in @($versionText,$versionError,$badOutput,$badError)){if($value.Contains('ARCHSIFT_UI=')-or$value.Contains('#session=')){throw 'Session URL/token appeared in direct file output.'}}
return [ordered]@{pipeReady=$pipeReady;pipeCliExitCode=$pipeExit;pipeWebPid=$webId;pipeWebProcessExited=$pipeGone;pipeWebPidListening=$false;fileStdoutVersionPassed=$true;fileStderrInvalidConfigPassed=$true;fileSessionTokenAbsent=$true;analysisOrBuildPerformed=$false}
