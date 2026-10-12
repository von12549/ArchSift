param([Parameter(Mandatory)][string]$RunRoot,[switch]$ProjectContexts,[string]$EvaluationPrefix='evaluate-', [string]$ReviewOutput='review-project-contexts.json')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
$before=Get-ArchSiftHostHash
$run=[IO.Path]::GetFullPath($RunRoot)
if (!$run.StartsWith('D:\ArchSift-lab\runs\ifx-external-0.7-',[StringComparison]::OrdinalIgnoreCase)) { throw 'External recorded run required.' }
function Hash([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant() }
function Save([string]$p,$v) { [IO.File]::WriteAllText((Join-Path $run $p),($v|ConvertTo-Json -Depth 40 -Compress),[Text.UTF8Encoding]::new($false)) }
function Material([string]$p,[string]$role) { [ordered]@{path=$p;sha256=(Hash $p);length=(Get-Item -LiteralPath $p).Length;role=$role} }
try {
$capturePath=Join-Path $run 'capture-compact.json'
$capture=Get-Content -Raw $capturePath | ConvertFrom-Json
$sources=@(Get-Content -Raw (Join-Path $run 'source-files.json') | ConvertFrom-Json)
$projects=@($sources|Where-Object {$_.path.EndsWith('.csproj',[StringComparison]::OrdinalIgnoreCase)})
$inputs=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($f in $capture.externalInputs) { [void]$inputs.Add([IO.Path]::GetFullPath($f.path)) }
$changed=@(foreach($f in $capture.externalInputs) { if(!(Test-Path -LiteralPath $f.path) -or (Hash $f.path) -ne $f.sha256) { $f.path } })
$mapped=[Collections.Generic.List[object]]::new()
$contexts=[Collections.Generic.List[object]]::new()
$assemblies=[Collections.Generic.List[object]]::new()
$dependencies=[Collections.Generic.List[object]]::new()
$missing=[Collections.Generic.List[object]]::new()
$tasks=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$imports=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$framework=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
foreach($f in $capture.externalInputs | Where-Object { $_.path -match '\\ref\\net[^\\]+\\[^\\]+\.dll$|\\sdk\\10.0.303\\ref\\[^\\]+\.dll$' }) {
    try { $id=[Reflection.AssemblyName]::GetAssemblyName($f.path).FullName; if (!$framework.ContainsKey($id)) { $framework.Add($id,$f.path) } } catch [BadImageFormatException] {}
}
$index=0
foreach($project in $projects) {
    $evaluationPath=Join-Path $run ($EvaluationPrefix+$index.ToString('D3')+'.stdout.log')
    $evaluation=Get-Content -Raw $evaluationPath | ConvertFrom-Json
    $values=$evaluation.Properties
    $properties=[ordered]@{}
    foreach($name in $capture.propertyNames) { $properties[$name]=[string]$values.$name }
    $mapped.Add([ordered]@{sourceProject=$project.path;assemblyName=$values.AssemblyName;targetFramework=$values.TargetFramework;evaluatedProperties=$properties})
    if (!$values.TargetPath -or !(Test-Path -LiteralPath $values.TargetPath)) { throw ('Missing successful build output '+$project.path) }
    $assemblies.Add([ordered]@{sourceProject=$project.path;path=$values.TargetPath})
    if($ProjectContexts) {
        $id=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($project.path))).ToLowerInvariant().Substring(0,16)
        $contexts.Add([ordered]@{id=('project-'+$id);sourceProject=$project.path;assemblyPath=$values.TargetPath;evaluationPath=$evaluationPath;referencePaths=@($evaluation.Items.ReferencePath|ForEach-Object FullPath);runtimePaths=@($evaluation.Items.ReferenceCopyLocalPaths|ForEach-Object FullPath);resolutionPolicy='dotnet-reference-path'})
    }
    foreach($kind in @('Compile','Analyzer','AdditionalFiles','EditorConfigFiles','ReferencePath','ReferenceCopyLocalPaths')) {
        foreach($item in $evaluation.Items.$kind) {
            $path=[IO.Path]::GetFullPath($item.FullPath)
            $tracked=$inputs.Contains($path)
            if ($path.StartsWith((Join-Path $run 'snapshot')+'\',[StringComparison]::OrdinalIgnoreCase)) {
                $rel=[IO.Path]::GetRelativePath((Join-Path $run 'snapshot'),$path).Replace('\','/')
                $tracked=($capture.inputIdentity.files.path -contains $rel) -or $rel.Contains('/obj/') -or $rel.Contains('/bin/')
            }
            if (!$tracked) { $missing.Add([ordered]@{project=$project.path;kind=$kind;path=$path}) }
            if ($kind -eq 'Analyzer') { [void]$tasks.Add($path) }
            if ($kind -eq 'ReferencePath' -and !$path.StartsWith('C:\Program Files\dotnet\packs\',[StringComparison]::OrdinalIgnoreCase) -and !$path.StartsWith((Join-Path $run 'snapshot')+'\',[StringComparison]::OrdinalIgnoreCase)) {
                $identity=[Reflection.AssemblyName]::GetAssemblyName($path)
                $dependencies.Add([pscustomobject]@{project=$project.path;path=$path;name=$identity.Name;identity=$identity.FullName;sha256=(Hash $path)})
            }
        }
    }
    foreach($p in $values.MSBuildAllProjects.Split(';',[StringSplitOptions]::RemoveEmptyEntries)) { [void]$imports.Add([IO.Path]::GetFullPath($p)) }
    $index++
}
$conflicts=@($dependencies | Group-Object name | Where-Object { @($_.Group.identity | Sort-Object -Unique).Count -gt 1 } | ForEach-Object { [ordered]@{assemblyName=$_.Name;identities=@($_.Group | Group-Object identity | ForEach-Object { [ordered]@{identity=$_.Name;paths=@($_.Group.path|Sort-Object -Unique);projects=@($_.Group.project|Sort-Object -Unique)} })} })
$audit=[ordered]@{schemaVersion=1;projectCount=$mapped.Count;successfulBuild=$true;sourceBound=$false;changedExternalInputs=$changed;untrackedEvaluatedInputs=$missing.ToArray();dependencyIdentityConflicts=$conflicts;hostStateEqual=((Get-ArchSiftHostHash) -ceq $before)}
Save 'input-review-audit.json' $audit
$notes=Join-Path $run 'review-notes.md'
[IO.File]::WriteAllText($notes,"# IFX current-source external review`n`nSDK 10.0.303 / net10.0 / Debug; original clean commit cc06528ab17052baafa496df2a89f6cde9207384 copied read-only; local feed only, NuGetAudit=false, no application or test entry executed. Build succeeded with zero warnings/errors. All 105 outputs and post-build evaluated properties recorded. Root MSBuildThisFileDirectory is scoped to the identical external snapshot; repository-root mapping recorded in evaluation. SDK/pack/package inputs captured before build; obj/bin generated artifacts retained after build. Review is UNAPPROVED: the combined dependency closure contains same-name different-version assemblies; do not substitute the highest version, hide conflicts, or infer current-source pass. Refer to input-review-audit.json. No successful seal is claimed.`n",[Text.UTF8Encoding]::new($false))
$materials=[Collections.Generic.List[object]]::new()
foreach($p in @('build.stdout.log','build.stderr.log','restore.stdout.log')) { $full=Join-Path $run $p; if((Get-Item $full).Length -gt 0) { $materials.Add((Material $full 'build-log')) } }
foreach($p in @('build.binlog','restore.binlog','input-review-audit.json','source-files.json')) { $materials.Add((Material (Join-Path $run $p) 'evaluation-trace')) }
foreach($f in Get-ChildItem -LiteralPath $run -File -Filter ($EvaluationPrefix+'*.stdout.log')) { $materials.Add((Material $f.FullName 'evaluation-trace')) }
$materials.Add((Material $notes 'review-notes'))
foreach($f in Get-ChildItem -LiteralPath (Join-Path $run 'snapshot') -File -Recurse | Where-Object { $_.FullName.Contains('\obj\') -or ($_.FullName.Contains('\bin\') -and $_.Extension -eq '.xml') }) { $materials.Add((Material $f.FullName 'generated-input')) }
$unknowns=@('Combined exact non-framework closure contains same-name different-version dependencies; per-project binding contexts are outside the frozen 0.7 contract.')
if ($changed.Count) { $unknowns+='Captured external input changed.' }
if ($missing.Count) { $unknowns+='Untracked evaluated input exists; see audit.' }
$review=[ordered]@{schemaVersion=1;origin='external-reviewed';captureSha256=(Hash $capturePath);approved=$false;inputsComplete=$false;reviewer='Codex, delegated machine reviewer';sdk='10.0.303';sdkToolPath='C:\Program Files\dotnet\sdk\10.0.303\MSBuild.dll';targetFramework='net10.0';configuration='Debug';command=@(Get-Content -Raw (Join-Path $run 'Build-result.json')|ConvertFrom-Json).checks[0].command;exitCode=0;globalProperties=@{Configuration='Debug';NuGetAudit='false';UseSharedCompilation='false'};materials=$materials.ToArray();projects=$mapped.ToArray();assemblies=$assemblies.ToArray();frameworkPaths=@($framework.Values);imports=@($imports);generatorsAndTasks=@($tasks);unknowns=$unknowns}
Save 'review-unapproved.json' $review
if($ProjectContexts) {
    $buildResult=Get-Content -Raw (Join-Path $run 'Build-result.json')|ConvertFrom-Json
    $evaluationResult=if($EvaluationPrefix -eq 'resolve-runtime-'){'EvaluateRuntime-result.json'}else{'Evaluate-result.json'}
    $evaluations=Get-Content -Raw (Join-Path $run $evaluationResult)|ConvertFrom-Json
    if($changed.Count -or $missing.Count -or $contexts.Count -ne 105 -or @($buildResult.checks|Where-Object {$_.exitCode -ne 0 -or !$_.hostStateEqual}).Count -or $evaluations.checks.Count -ne 105 -or @($evaluations.checks|Where-Object {$_.exitCode -ne 0 -or !$_.hostStateEqual}).Count) { throw 'Cannot approve incomplete project review.' }
    foreach($file in $capture.inputIdentity.files) { if((Hash (Join-Path (Join-Path $run 'snapshot') $file.path)) -ne $file.sha256) { throw 'Snapshot/source input mismatch.' } }
    $projectNotes=Join-Path $run 'project-context-review-notes.md'
    [IO.File]::WriteAllText($projectNotes,"# Delegated IFX project-context review`n`n105 actual Debug/net10.0 project outputs and ResolveReferences selections; SDK 10.0.303, explicit offline feed, NuGetAudit=false. The complete SDK/pack/package/source inputs and generated obj/first-party XML materials are tracked and unchanged. Root props and all 105 project files contain no custom targets/UsingTask; actual package tasks reviewed (xunit source generation, MVC manifest generation, Shouldly child-process environment only), no target app or test entry ran. Cross-project Hangfire/EF/Npgsql versions remain recorded in the global audit. Each context selects exactly its actual evaluated ReferencePath, never a highest-version guess. SDK reference contracts terminate optional unused forwarders; all implementation closure identities, snapshots and contexts still require Core seal validation. This is an externally reviewed build-causality statement, not an ArchSift target rebuild or runtime-execution certification.`n",[Text.UTF8Encoding]::new($false))
    $review.schemaVersion=2;$review.approved=$true;$review.inputsComplete=$true;$review.unknowns=@();$review.frameworkPaths=@()
    $review.materials=@($review.materials)+@((Material $projectNotes 'review-notes'))
    $review.buildSnapshotRoot=Join-Path $run 'snapshot';$review.projectContexts=$contexts.ToArray()
    Save $ReviewOutput $review
}
Save 'assembly-paths-unbound.json' @($assemblies.path)
Save 'review-host-final.json' ([ordered]@{hostBeforeSha256=$before;hostAfterSha256=(Get-ArchSiftHostHash);equal=((Get-ArchSiftHostHash) -ceq $before)})
[ordered]@{projectCount=$mapped.Count;globalConflictCount=$conflicts.Count;changedInputCount=$changed.Count;untrackedInputCount=$missing.Count;globalReviewApproved=$false;projectReviewApproved=[bool]$ProjectContexts;hostStateEqual=((Get-ArchSiftHostHash) -ceq $before);runRoot=$run}|ConvertTo-Json
if ((Get-ArchSiftHostHash) -cne $before) { throw 'Host drift; preserve evidence.' }
} finally {
    Save 'review-final-check.json' ([ordered]@{beforeSha256=$before;afterSha256=(Get-ArchSiftHostHash);equal=((Get-ArchSiftHostHash) -ceq $before)})
    if((Get-ArchSiftHostHash) -cne $before) { throw 'Review host drift; preserve evidence.' }
}
