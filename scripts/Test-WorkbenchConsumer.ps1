[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$FixtureRoot,
    [Parameter(Mandatory)][string[]]$OfflineSources,
    [Parameter(Mandatory)][string]$PackageCache,
    [string]$ExpectedHostHash=''
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'HostState.ps1')
. (Join-Path $PSScriptRoot 'ReleaseInputs.ps1')
$release=Get-ArchSiftReleaseInputs $repo
$before=Get-ArchSiftHostHash
if($ExpectedHostHash -and $before -cne $ExpectedHostHash){throw 'Host differs from the reviewed baseline; no repair.'}
$root=Assert-ArchSiftExternalPath $repo $FixtureRoot
if(Test-Path -LiteralPath $root){throw 'Keep the existing consumer attempt; use a fresh root.'}
$null=& (Join-Path $PSScriptRoot 'New-WorkbenchConsumerFixture.ps1') -FixtureRoot $root -OfflineSources $OfflineSources -PackageCache $PackageCache -Version $release.version
$fixture=Get-Content (Join-Path $root 'fixture-inputs.json') -Raw|ConvertFrom-Json
$checks=[Collections.Generic.List[object]]::new()
function Save([string]$Path,$Value){[IO.File]::WriteAllText($Path,((ConvertTo-Json -InputObject $Value -Depth 35)+"`n"),[Text.UTF8Encoding]::new($false))}
function Invoke-ConsumerDotnet([string]$Name,[string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -ErrorAction Stop).Source)
    $start.WorkingDirectory=$fixture.source;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $root 'tool-home'
    $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='1'
    $start.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE']='false'
    $start.Environment['DOTNET_NOLOGO']='1'
    $start.Environment['NUGET_CERT_REVOCATION_MODE']='offline'
    $start.Environment['NUGET_PACKAGES']=$fixture.packageCache
    $start.Environment['ARCHSIFT_PROFILE_PATHS']=ConvertTo-Json -InputObject @($PROFILE.AllUsersAllHosts,$PROFILE.AllUsersCurrentHost,$PROFILE.CurrentUserAllHosts,$PROFILE.CurrentUserCurrentHost) -Compress
    foreach($key in @('ARCHSIFT_SP02_REPORT_FIXTURE','ARCHSIFT_TEST_GATE_CONFIG','ARCHSIFT_LT01_NEGATIVE','IFX_UPDATE_SNAPSHOTS')){[void]$start.Environment.Remove($key)}
    $process=[Diagnostics.Process]::Start($start)
    try{
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(600000)){$process.Kill($true);$process.WaitForExit();throw 'Owned synthetic consumer process timed out.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $root ($Name+'.stdout.log')),$stdout.Result,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $root ($Name+'.stderr.log')),$stderr.Result,[Text.UTF8Encoding]::new($false))
        $equal=(Get-ArchSiftHostHash) -ceq $before
        $checks.Add(@{name=$Name;arguments=$Arguments;exitCode=$process.ExitCode;hostEqual=$equal})
        Save (Join-Path $root 'checks.json') $checks.ToArray()
        Write-Output ("consumer-$Name exit="+$process.ExitCode+" host-equal=$equal")
        if(-not $equal){throw 'Host drift; preserve evidence without repair.'}
        if($process.ExitCode -ne 0){throw ("Consumer $Name failed; inspect retained stdout/stderr.")}
    }finally{$process.Dispose()}
}
$completed=$false
try{
    Invoke-ConsumerDotnet 'sdk' @('--version')
    if(([IO.File]::ReadAllText((Join-Path $root 'sdk.stdout.log'))).Trim() -cne $release.profile.sdk){throw 'Consumer SDK differs from approval.'}
    $nuget=Join-Path $fixture.source 'NuGet.Config'
    Invoke-ConsumerDotnet 'tool-restore' @('tool','restore','--tool-manifest',(Join-Path $fixture.source '.config/dotnet-tools.json'),'--configfile',$nuget,'--disable-parallel')
    Invoke-ConsumerDotnet 'tool-version' @('tool','run','archsift','--','--version')
    if(([IO.File]::ReadAllText((Join-Path $root 'tool-version.stdout.log'))).Trim() -cne ('archsift '+$release.version)){throw 'Normal restored CLI has the wrong version.'}
    # The synthetic project is new: create its lock once, then require it unchanged in the real gate.
    Invoke-ConsumerDotnet 'seed-lock' @('restore',$fixture.project,'--configfile',$nuget,'--packages',$fixture.packageCache,'--disable-parallel','--disable-build-servers','--force-evaluate','-p:NuGetAudit=false')
    $locked=Get-Content (Join-Path $fixture.source 'packages.lock.json') -Raw|ConvertFrom-Json -AsHashtable
    $graph=$locked.dependencies['net10.0']
    $closure=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    function Visit([string]$Id){
        if(-not $closure.Add($Id)){return}
        $keys=@($graph.Keys|Where-Object{[string]::Equals($_,$Id,[StringComparison]::OrdinalIgnoreCase)})
        if($keys.Count -ne 1){throw 'Ambiguous synthetic tool dependency.'}
        $item=$graph[$keys[0]]
        if($item.type -eq 'Project' -or -not $item.resolved -or -not $item.contentHash){throw 'The synthetic fixture must consume actual pinned packages.'}
        if($item.ContainsKey('dependencies')){foreach($child in $item.dependencies.Keys){Visit $child}}
    }
    Visit 'ArchSift.Testing'
    $approval=@{schemaVersion=1;testingVersion=$release.version;cliVersion=$release.version;packages=@(foreach($id in ($closure|Sort-Object)){
        $key=@($graph.Keys|Where-Object{[string]::Equals($_,$id,[StringComparison]::OrdinalIgnoreCase)})[0]
        @{id=$key;version=$graph[$key].resolved;contentHash=$graph[$key].contentHash}
    })}
    Save (Join-Path $fixture.source 'package-approval.json') $approval
    Invoke-ConsumerDotnet 'full-gate' @('tool','run','archsift','--','test-gate','--config',$fixture.config)
    $config=Get-Content $fixture.config -Raw|ConvertFrom-Json
    $report=Get-Content (Join-Path $config.runDirectory 'architecture-test-run.json') -Raw|ConvertFrom-Json
    $ctrf=Get-Content (Join-Path $config.runDirectory 'test-results.ctrf.json') -Raw|ConvertFrom-Json
    if($report.outcome.runGate -cne 'allow' -or -not $report.testScope.coverageComplete -or
        $ctrf.results.summary.tests -ne 4 -or $ctrf.results.summary.passed -ne 4 -or $ctrf.results.summary.failed -ne 0 -or $ctrf.results.summary.skipped -ne 0 -or
        $report.process.audit.status -cne 'not-run'){
        throw 'Actual synthetic package coverage/audit differs from its reviewed contract.'
    }
    $result=@{schemaVersion=1;status='passed';scope='owned synthetic packaged consumer; not IFX or formal policy adoption';version=$release.version;sdk=$release.profile.sdk;configuration='Release';tfm='net10.0';actualCases=4;originalCSharpCases=3;jsonCases=1;failed=0;skipped=0;coverageComplete=$true;gate='allow';audit='not-run';normalToolRestore=$true;testingProjectReferences=$false;packageClosureCount=$closure.Count;reportPath=(Join-Path $config.runDirectory 'architecture-test-run.json');reportSha256=(Get-FileHash (Join-Path $config.runDirectory 'architecture-test-run.json') -Algorithm SHA256).Hash.ToLowerInvariant();requiredCasesSha256=$config.requiredCasesSha256;policySha256=$config.policySha256;hostHash=$before;hostEqual=((Get-ArchSiftHostHash)-ceq$before);checks=$checks.ToArray()}
    Save (Join-Path $root 'qualification.json') $result
    $completed=$true
    Write-Output 'consumer-qualification=passed cases=4 failed=0 skipped=0 audit=not-run'
}finally{
    $after=Get-ArchSiftHostHash
    Save (Join-Path $root 'host-final.json') @{before=$before;after=$after;equal=($before -ceq $after);completed=$completed}
    if($before -cne $after){throw 'Final host drift; preserve evidence and do not repair.'}
}
