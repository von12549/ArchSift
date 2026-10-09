# Final metadata-only audit. Does not start ArchSift, execute IFX, build, restore, or clean artifacts.
[CmdletBinding()]
param([string]$LabRoot='D:\ArchSift-lab')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$lab=[IO.Path]::GetFullPath($LabRoot)
$target='D:\IFX-10-Root\IFX-New'
$expectedHead='64ef2674c57e6cf9031481d6d4410cca55b0dad5'
$policy='D:\IFX-10-Root\archsift-lab\runs\ifx-030-d46915bff2c74f17bacde44f8d232289\rules\production-candidate\v1-bdc137efc83748fb97f1e68637600447\ifx-protection.json'
$package='D:\ArchSift-lab\packages\smoke-cda7d9c09d9f4b3fa899fb55a481001d\unpacked'
$zip='D:\ArchSift-lab\packages\download-0.4-editor-20261009-bb599bd\archsift-0.4.0-win-x64.zip'
$browser='D:\ArchSift-lab\runs\ifx-0.4-browser-8564bbf6a23c4390b18203739d03189d'
$discovery='D:\ArchSift-lab\runs\ifx-discovery-d0a6f85954dd49719a3c63c2a9b7a9f5\reports\ee6381379c1c43abbb7b95f3b53d720c\report.json'
. (Join-Path $repo 'scripts/HostState.ps1')
function Sha([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Under([string]$Path,[string]$Root){$relative=[IO.Path]::GetRelativePath($Root,$Path);return $relative-eq'.'-or(-not[IO.Path]::IsPathRooted($relative)-and$relative-ne'..'-and-not$relative.StartsWith('..'+[IO.Path]::DirectorySeparatorChar,[StringComparison]::Ordinal))}
function NoLinks([string]$Path){$cursor=[IO.Path]::GetFullPath($Path);while($cursor){if([IO.File]::Exists($cursor)-or[IO.Directory]::Exists($cursor)){if(([IO.File]::GetAttributes($cursor)-band[IO.FileAttributes]::ReparsePoint)-ne0){throw 'Link/reparse path rejected.'}};$cursor=[IO.Path]::GetDirectoryName($cursor)}}
if(-not$IsWindows-or-not(Under $package $lab)-or-not(Under $zip $lab)-or-not(Under $browser $lab)-or(Under $target $lab)-or(Under $repo $lab)){throw 'Audit path boundaries differ.'}
foreach($path in @($repo,$lab,$target,$policy,$package,$zip,$browser,$discovery)){NoLinks $path}
$hostBefore=Get-ArchSiftHostHash
$checks=[ordered]@{}
$errorText=$null
$sourceCount=0
$manifest=$null
$recordedPids=@()
try{
    $git=(Get-Command git -ErrorAction Stop).Source
    $head=(& $git -c core.fsmonitor=false -C $target rev-parse HEAD).Trim()
    $status=@(& $git -c core.fsmonitor=false -C $target status --porcelain=v1 --untracked-files=all)
    $checks['targetHeadEqual']=$head-ceq$expectedHead
    $checks['targetOrdinaryStatusClean']=$status.Count-eq0
    $checks['policyUnchanged']=(Sha $policy)-ceq'48495a266a1d8444ca0cd8cdeb6e5f3023f294ca70b30e020bf5c4a5131fbda2'
    $report=Get-Content -LiteralPath $discovery -Raw|ConvertFrom-Json -Depth 80
    foreach($file in $report.inputIdentity.files){
        if([IO.Path]::IsPathRooted($file.path)-or$file.path.Replace('\','/').Split('/')-contains'..'-or$file.path.StartsWith('@')){throw 'Invalid discovery source path.'}
        $source=[IO.Path]::GetFullPath((Join-Path $target $file.path))
        if(-not(Under $source $target)){throw 'Discovery source escaped target.'}
        NoLinks $source
        if((Sha $source)-cne$file.sha256-or(Get-Item -LiteralPath $source).Length-ne$file.length){throw 'Discovery source bytes changed.'}
        $sourceCount++
    }
    $checks['sourceRecordsEqual']=$sourceCount-eq563
    $manifestPath=Join-Path $package 'package-manifest.json'
    $checks['manifestUnchanged']=(Sha $manifestPath)-ceq'd35fea42475633eb290c858e4df4aec4387dad639d2d03480d20e9edd532849c'
    $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json -Depth 20
    $checks['packageSourceEqual']=$manifest.sourceCommit-ceq'bb599bd87aaa3322229c049442e135f3c091b72a'-and-not$manifest.productSourceDirty
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $manifest.entries){
        $path=[IO.Path]::GetFullPath((Join-Path $package $entry.path))
        if(-not(Under $path $package)-or-not$seen.Add($entry.path)){throw 'Invalid package path.'}
        NoLinks $path
        if((Sha $path)-cne$entry.sha256-or(Get-Item -LiteralPath $path).Length-ne$entry.bytes){throw 'Package payload changed.'}
    }
    $checks['packagePayloadEqual']=$seen.Count-eq621-and@(Get-ChildItem -LiteralPath $package -Recurse -File).Count-eq622
    $checks['downloadZipUnchanged']=(Sha $zip)-ceq'4f35dfdb0f9a14966b0d0d0c375e2c6d505d09c36f18ff673c4f686b72bba97c'
    $resultPath=Join-Path $browser 'operator-result.json'
    $checks['browserResultUnchanged']=(Sha $resultPath)-ceq'be3b084bdbc95aa902f6c0708b9a0e21a17d6f0c2ef74f65745e013e04a64494'
    $operator=Get-Content -LiteralPath $resultPath -Raw|ConvertFrom-Json -Depth 20
    $checks['browserOperatorGate']=$operator.status-ceq'pass-with-limitations'-and$operator.humanChecksPassed-and$operator.usabilityRating-eq5-and@($operator.review|Where-Object answer -ne 'yes').Count-eq0-and$operator.hostStateEqual-and$operator.targetHeadEqual-and$operator.ordinaryStatusEqual-and$operator.sourceRecordsEqual-and$operator.packageUnchanged-and$operator.rulesUnchanged-and$operator.configUnchanged-and$operator.processExited-and$operator.recordedListenerGone-and-not$operator.targetBuildInvoked-and-not$operator.restoreInvoked-and-not$operator.policyAdopted
    foreach($root in @('ifx-0.4-browser-ca19f9bf0f624bf588fb0d488ca921a6','ifx-0.4-browser-13b6d73ec3b64765be043ca0fa7431c3','ifx-0.4-browser-8564bbf6a23c4390b18203739d03189d')){
        $log=Join-Path (Join-Path $lab 'runs') (Join-Path $root 'web.stdout.log')
        $match=[regex]::Match([IO.File]::ReadAllText($log),'ARCHSIFT_PID=(\d+)')
        if(-not$match.Success){throw 'Recorded browser PID missing.'}
        $recordedPids+=[int]$match.Groups[1].Value
    }
    $live=@(Get-CimInstance Win32_Process -Filter "Name = 'ArchSift.Web.exe' OR Name = 'archsift.exe'" -ErrorAction Stop)
    $checks['noArchSiftNativeProcesses']=$live.Count-eq0
    $checks['recordedBrowserPidsGone']=@($live|Where-Object {$_.ProcessId-in$recordedPids}).Count-eq0
    $listeners=@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object {$_.OwningProcess-in$recordedPids})
    $checks['recordedBrowserListenersGone']=$listeners.Count-eq0
}catch{$errorText=$_.Exception.Message}
$hostAfter=Get-ArchSiftHostHash
$checks['auditHostStateEqual']=$hostBefore-ceq$hostAfter
$passed=$null -eq $errorText -and @($checks.Values|Where-Object {$_ -ne $true}).Count -eq 0
$result=[ordered]@{step='ifx-0.4-final-read-only-audit';status=$(if($passed){'pass-with-limitations'}else{'stop'});checks=$checks;sourceRecordCount=$sourceCount;packageEntryCount=$(if($manifest){$manifest.entries.Count}else{$null});recordedBrowserPidCount=$(if($recordedPids){$recordedPids.Count}else{0});hostHashBefore=$hostBefore;hostHashAfter=$hostAfter;targetBuildInvoked=$false;restoreInvoked=$false;policyAdopted=$false;cleanupInvoked=$false;error=$errorText}
$root=Join-Path $lab ('evidence/ifx-0.4-final-audit-'+[guid]::NewGuid().ToString('N'))
if(-not(Under $root $lab)-or(Under $root $target)-or(Under $root $repo)){throw 'Audit output escaped lab.'}
[void][IO.Directory]::CreateDirectory($root)
$path=Join-Path $root 'audit.json'
[IO.File]::WriteAllText($path,($result|ConvertTo-Json -Depth 12)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output ('auditPath='+$path)
$result|ConvertTo-Json -Depth 12
if(-not$passed){throw 'Final read-only audit stopped; preserve evidence.'}
