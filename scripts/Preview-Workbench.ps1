[CmdletBinding()]
param([Parameter(Mandatory)][string] $Config)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function State-Hash {
    $values=[ordered]@{}
    foreach($scope in @('User','Machine')){
        $environment=[Environment]::GetEnvironmentVariables($scope)
        foreach($key in @($environment.Keys)|Sort-Object){$values["$scope/$key"]=[string]$environment[$key]}
    }
    $values['process-path']=[Environment]::GetEnvironmentVariable('Path')
    foreach($name in @('AllUsersAllHosts','AllUsersCurrentHost','CurrentUserAllHosts','CurrentUserCurrentHost')){
        $file=[string]$PROFILE.$name
        $values[$name]=if([IO.File]::Exists($file)){(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}else{'absent'}
    }
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($values|ConvertTo-Json -Compress)))).ToLowerInvariant()
}
$before=State-Hash
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName=(Get-Command dotnet).Source
$start.UseShellExecute=$false
$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true
$start.RedirectStandardError=$true
$start.ArgumentList.Add((Join-Path $root 'src/ArchSift.Web/bin/Debug/net10.0/ArchSift.Web.dll'))
$start.ArgumentList.Add('--config')
$start.ArgumentList.Add([IO.Path]::GetFullPath($Config))
$start.Environment['DOTNET_CLI_HOME']=(Join-Path $root 'artifacts/ui-preview/cli-home')
$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
$process=[Diagnostics.Process]::Start($start)
$errors=$process.StandardError.ReadToEndAsync()
try {
    while(-not$process.HasExited){
        $line=$process.StandardOutput.ReadLine()
        if($null-ne$line){Write-Output $line}
    }
} finally {
    if(-not$process.HasExited){$process.Kill($true);$process.WaitForExit()}
    Write-Output ('preview-host-state-equal='+((State-Hash)-ceq$before))
    $process.Dispose()
}
