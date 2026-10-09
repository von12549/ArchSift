[CmdletBinding()]
param([string]$LabRoot='D:\ArchSift-lab',[string]$LocalFeed=(Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'))
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('runs/package-locks-'+[Guid]::NewGuid().ToString('N'))
if($run.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must be outside source.'}
[void][IO.Directory]::CreateDirectory($run)
try {
    foreach($project in @('Cli','Web','Setup')){
        $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command dotnet).Source;$start.UseShellExecute=$false;$start.WorkingDirectory=$root;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        foreach($arg in @('restore',('src/ArchSift.'+$project+'/ArchSift.'+$project+'.csproj'),'--force-evaluate','-r','win-x64','-p:RuntimeIdentifier=win-x64','-p:SelfContained=true','--configfile','NuGet.Config','--source',[IO.Path]::GetFullPath($LocalFeed),'--packages',(Join-Path $root 'artifacts/development/packages'),'--disable-build-servers','-p:NuGetAudit=false')){$start.ArgumentList.Add($arg)}
        $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0';$start.Environment['NUGET_CERT_REVOCATION_MODE']='offline';$start.Environment['MSBUILDDISABLENODEREUSE']='1'
        $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        try{
            if(-not$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw 'Lock initialization timeout.'}
            [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
            [IO.File]::WriteAllText((Join-Path $run ($project+'.stdout.log')),$stdout.Result);[IO.File]::WriteAllText((Join-Path $run ($project+'.stderr.log')),$stderr.Result)
            $equal=(Get-ArchSiftHostHash)-ceq$before
            Write-Output ($project+' exit='+$process.ExitCode+' host-state-equal='+$equal)
            if(-not$equal){throw 'Host drift; no repair.'}
            if($process.ExitCode-ne0){Write-Output $stderr.Result;throw 'Explicit offline lock initialization failed.'}
        }finally{$process.Dispose()}
    }
}finally{$equal=(Get-ArchSiftHostHash)-ceq$before;[IO.File]::WriteAllText((Join-Path $run 'host-result.json'),(@{baselineSha256=$before;finalSha256=(Get-ArchSiftHostHash);equal=$equal}|ConvertTo-Json));Write-Output ('package-lock-run='+$run+' final-host-state-equal='+$equal);if(-not$equal){throw 'Host drift; preserve evidence.'}}
