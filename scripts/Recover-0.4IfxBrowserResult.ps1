# Operator-only recovery for the 2026-10-09 step-04 result-writer failure. No native launch.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Invoke-0.4IfxCandidateStep.ps1') -ValidateOnly | Out-Null
$browserRoot='D:\ArchSift-lab\runs\ifx-0.4-browser-ca19f9bf0f624bf588fb0d488ca921a6'
$configPath=Join-Path $browserRoot 'config.json'
$expectedConfigHash='c78de692f1bcfdea1245913e8d586b3172bbe3700a794066e1452584721ef200'
$subsetPath=Join-Path $browserRoot 'inputs/declaration-subset.json'
$expectedSubsetHash='4f1db37bc6ab94406578c702293fc4fefd3d01b0f5f1cde9f7e84c90f88ccb9f'
$expectedOperatorHostHash='9bb5548523dbb22f6dbacf9903a3b67c4f6b1ee266bf0445e39a3769cbe0531d'
$stdoutPath=Join-Path $browserRoot 'web.stdout.log'
foreach($path in @($browserRoot,$configPath,$subsetPath,$stdoutPath)){No-Links $path}
if(-not(Is-Under $browserRoot $lab)-or(Sha $configPath)-cne$expectedConfigHash-or(Sha $subsetPath)-cne$expectedSubsetHash-or(Sha (Join-Path $browserRoot 'inputs/ifx-protection.json'))-cne$expectedRulesHash){throw 'Browser session inputs changed; stop recovery.'}
$stdout=[IO.File]::ReadAllText($stdoutPath)
$match=[regex]::Match($stdout,'ARCHSIFT_PID=(\d+)')
if(-not$match.Success){throw 'Recorded native Web PID is missing.'}
$webPid=[int]$match.Groups[1].Value
$webAlive=$null-ne(Get-Process -Id $webPid -ErrorAction SilentlyContinue)
$listenerCount=@(Get-NetTCPConnection -State Listen -LocalPort 7935 -ErrorAction SilentlyContinue).Count
$head=Git-Text @('rev-parse','HEAD')
$status=Git-Text @('status','--porcelain=v1','--untracked-files=all')
$hostNow=Get-ArchSiftHostHash
Check-Source $discovery.inputIdentity.files
Check-Package $manifest
$reports=Join-Path $browserRoot 'reports'
$analysisFiles=@(Get-ChildItem -LiteralPath $reports -Filter report.json -Recurse -File)
$chainFiles=@(Get-ChildItem -LiteralPath $reports -Filter chain-summary.json -Recurse -File)
$diagnosticFiles=@(Get-ChildItem -LiteralPath $reports -Filter diagnostic.json -Recurse -File)
foreach($file in $analysisFiles){if(-not(Test-Json -LiteralPath $file.FullName -SchemaFile (Join-Path $package 'schemas/report.schema.json'))){throw 'Saved analysis report schema failed.'}}
foreach($file in $chainFiles){if(-not(Test-Json -LiteralPath $file.FullName -SchemaFile (Join-Path $package 'schemas/chain-summary.schema.json'))){throw 'Saved chain summary schema failed.'}}
foreach($file in $diagnosticFiles){if(-not(Test-Json -LiteralPath $file.FullName -SchemaFile (Join-Path $package 'schemas/chain-diagnostic.schema.json'))){throw 'Saved diagnostic schema failed.'}}
$safe=(-not$webAlive)-and$listenerCount-eq0-and$head-ceq'64ef2674c57e6cf9031481d6d4410cca55b0dad5'-and-not$status-and$hostNow-ceq$expectedOperatorHostHash-and(Sha $policy)-ceq$expectedRulesHash-and(Sha $manifestPath)-ceq$expectedManifestHash
$result=[ordered]@{
    step='ifx-0.4-browser-result-recovery';status=$(if($safe){'needs-review'}else{'stop'});runRoot=$browserRoot
    originalResultWriterError='Where-Object shorthand lacked a separated comparison operator; the launcher did not save operator-result.json.'
    originalResultExists=(Test-Path -LiteralPath (Join-Path $browserRoot 'operator-result.json'))
    operatorResponses='six yes responses and overall usability rating 4, as returned by the human operator; two screenshot findings override visual acceptance'
    visualFindings=@('Language control and Safe shutdown button are not aligned.','Expanded ruleset card is too long; the editor is too prominent.')
    browserVisualAccepted=$false;humanRating=4
    analysisReportCount=$analysisFiles.Count;chainSummaryCount=$chainFiles.Count;diagnosticCount=$diagnosticFiles.Count
    nativeWebPid=$webPid;nativeWebAlive=$webAlive;recordedListenerCount=$listenerCount
    targetHead=$head;ordinaryStatusClean=(-not$status);hostHashExpected=$expectedOperatorHostHash;hostHashCurrent=$hostNow;hostStateEqual=($hostNow-ceq$expectedOperatorHostHash)
    sourceRecordsEqual=$true;packageUnchanged=$true;rulesUnchanged=((Sha $policy)-ceq$expectedRulesHash);configUnchanged=((Sha $configPath)-ceq$expectedConfigHash)
    targetBuildInvoked=$false;restoreInvoked=$false;policyAdopted=$false;nextAction='RETURN RECOVERY RESULT; RETEST UI AFTER PRODUCT FIX'
}
Save-Json (Join-Path $browserRoot 'recovered-result.json') $result
$result|ConvertTo-Json -Depth 12
if($result.status-eq'stop'){throw 'Browser recovery detected safety drift; preserve evidence and stop.'}
