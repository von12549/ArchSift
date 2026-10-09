# Operator-only step 01. The agent must not execute this script for real IFX.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SetupPath,
    [Parameter(Mandatory)][string]$ZipPath,
    [Parameter(Mandatory)][string]$ExpectedSetupSha256,
    [Parameter(Mandatory)][string]$ExpectedZipSha256,
    [Parameter(Mandatory)][string]$ExpectedSourceCommit,
    [string]$TargetRoot='D:\IFX-10-Root\IFX-New',
    [string]$ExistingInstallRoot='D:\IFX-10-Root\ArchSift',
    [string]$NewInstallRoot='D:\IFX-10-Root\ArchSift-0.5-review',
    [string]$LabRoot='D:\ArchSift-lab'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../scripts/HostState.ps1')
function Safe-Path([string]$Path){
    $full=[IO.Path]::GetFullPath($Path);$cursor=$full
    if($full.StartsWith('\\')){throw 'UNC/device path unsupported.'}
    while($cursor){
        try{if(([IO.File]::GetAttributes($cursor)-band[IO.FileAttributes]::ReparsePoint)-ne0){throw 'Link/reparse input rejected.'}}catch [IO.FileNotFoundException]{}catch [IO.DirectoryNotFoundException]{}
        $cursor=[IO.Path]::GetDirectoryName($cursor)
    };return $full
}
function Hash-Json($Object){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Object -Depth 10 -Compress)))).ToLowerInvariant()}
function Hash-File([string]$Path){(Get-FileHash -LiteralPath (Safe-Path $Path) -Algorithm SHA256).Hash.ToLowerInvariant()}
function Git-Read([string[]]$Arguments){
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=(Get-Command git).Source;$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in (@('-C',$TargetRoot)+$Arguments)){$start.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{if(-not$process.WaitForExit(30000)){$process.Kill($true);$process.WaitForExit();throw 'Read-only Git timeout.'};[Threading.Tasks.Task]::WaitAll($stdout,$stderr);if($process.ExitCode-ne0){throw 'Read-only Git metadata failed.'};return $stdout.Result}finally{$process.Dispose()}
}
function Target-State {
    $head=(Git-Read @('rev-parse','HEAD')).Trim();$status=Git-Read @('status','--porcelain=v1','--untracked-files=normal')
    $paths=(Git-Read @('ls-files','-z','--cached','--others','--exclude-standard')).Split([char]0,[StringSplitOptions]::RemoveEmptyEntries)
    if($paths.Count-gt10000){throw 'Target metadata file-count limit.'}
    $files=@($paths|Sort-Object -Unique|ForEach-Object{
        $path=Safe-Path (Join-Path $TargetRoot $_)
        if(-not$path.StartsWith($TargetRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Target input escapes root.'}
        if([IO.File]::Exists($path)){[ordered]@{path=$_;sha256=(Hash-File $path)}}
    })
    return [ordered]@{head=$head;ordinaryStatusSha256=(Hash-Json $status);filesSha256=(Hash-Json $files);fileCount=$files.Count}
}
function Existing-State {
    $manifestPath=Join-Path $ExistingInstallRoot 'package-manifest.json'
    $manifest=Get-Content -LiteralPath (Safe-Path $manifestPath) -Raw|ConvertFrom-Json
    if($manifest.version-cne'0.4.0'-or$manifest.sourceCommit-cne'bb599bd87aaa3322229c049442e135f3c091b72a'-or(Hash-File $manifestPath)-cne'd35fea42475633eb290c858e4df4aec4387dad639d2d03480d20e9edd532849c'){throw 'Existing 0.4.0 manifest differs; stop for review.'}
    foreach($file in $manifest.entries){$path=Safe-Path (Join-Path $ExistingInstallRoot $file.path);if(-not$path.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $path)-cne$file.sha256){throw 'Existing package identity differs.'}}
    $configPath=Join-Path $ExistingInstallRoot 'config/ifx.json';$config=Get-Content -LiteralPath (Safe-Path $configPath) -Raw|ConvertFrom-Json
    $base=[IO.Path]::GetDirectoryName($configPath)
    $library=if($config.PSObject.Properties.Name-contains'rulesDirectory'){[IO.Path]::GetFullPath([string]$config.rulesDirectory,$base)}else{Join-Path ([IO.Path]::GetFullPath([string]$config.output.directory,$base)) 'rules'}
    $library=Safe-Path $library;$records=[Collections.Generic.List[object]]::new()
    function Walk-Library([string]$Directory){
        foreach($path in [IO.Directory]::EnumerateFileSystemEntries($Directory)){
            [void](Safe-Path $path)
            if([IO.Directory]::Exists($path)){Walk-Library $path}else{$records.Add([ordered]@{path=[IO.Path]::GetRelativePath($library,$path);sha256=(Hash-File $path)});if($records.Count-gt10000){throw 'Library metadata limit.'}}
        }
    }
    if([IO.Directory]::Exists($library)){Walk-Library $library}
    return [ordered]@{manifestSha256=(Hash-File $manifestPath);payloadCount=$manifest.entries.Count;configSha256=(Hash-File $configPath);librarySha256=(Hash-Json @($records|Sort-Object path));libraryFileCount=$records.Count}
}
$TargetRoot=Safe-Path $TargetRoot;$ExistingInstallRoot=Safe-Path $ExistingInstallRoot;$NewInstallRoot=Safe-Path $NewInstallRoot;$LabRoot=Safe-Path $LabRoot
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach($protected in @($sourceRoot,$TargetRoot,$ExistingInstallRoot)){
    if($LabRoot-eq$protected-or$LabRoot.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or$protected.StartsWith($LabRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence/protected roots overlap.'}
}
if([IO.Directory]::Exists($NewInstallRoot)-or[IO.File]::Exists($NewInstallRoot)){throw 'New review root must be absent; preserve existing directories.'}
$hostBefore=Get-ArchSiftHostHash;$targetBefore=Target-State;$existingBefore=Existing-State
$run=Join-Path $LabRoot ('evidence/ifx-0.5-plan-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
[IO.File]::WriteAllText((Join-Path $run 'before.json'),(@{hostSha256=$hostBefore;target=$targetBefore;existing=$existingBefore}|ConvertTo-Json -Depth 8))
$completed=$false
try{
    $SetupPath=Safe-Path $SetupPath;$ZipPath=Safe-Path $ZipPath
    if((Hash-File $SetupPath)-cne$ExpectedSetupSha256-or(Hash-File $ZipPath)-cne$ExpectedZipSha256){throw 'Reviewed candidate asset hash mismatch.'}
    $archive=[IO.Compression.ZipFile]::OpenRead($ZipPath)
    try{$entry=$archive.GetEntry('package-manifest.json');if($null-eq$entry-or$entry.Length-gt4194304){throw 'Candidate manifest missing/oversize.'};$reader=[IO.StreamReader]::new($entry.Open());try{$candidate=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}}finally{$archive.Dispose()}
    if($candidate.version-cne'0.5.0'-or$candidate.sourceCommit-cne$ExpectedSourceCommit-or$candidate.productSourceDirty){throw 'Candidate source identity mismatch.'}
    if((Get-ArchSiftHostHash)-cne$hostBefore){throw 'Host drift after asset check; no repair.'}
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$SetupPath;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in @('plan','install','--root',$NewInstallRoot,'--package',$ZipPath,'--sha256',$ExpectedZipSha256,'--target',$TargetRoot,'--entry','IFX.sln','--tfm','net10.0','--smoke','true')){$start.ArgumentList.Add($arg)}
    $start.Environment['DOTNET_CLI_HOME']=(Join-Path $run 'cli-home');$start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
    $process=[Diagnostics.Process]::Start($start);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    try{
        if(-not$process.WaitForExit(120000)){$process.Kill($true);$process.WaitForExit();throw 'Read-only setup planning timeout.'}
        [Threading.Tasks.Task]::WaitAll($stdout,$stderr)
        [IO.File]::WriteAllText((Join-Path $run 'plan.json'),$stdout.Result,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $run 'plan.stderr.log'),$stderr.Result)
        if($process.ExitCode-ne0){throw ('Setup planning rejected; retain evidence. Exit '+$process.ExitCode)}
        $plan=$stdout.Result|ConvertFrom-Json
        if($plan.operation-cne'install'-or$plan.config.target.root-cne$TargetRoot-or$plan.config.build.mode-cne'existing'-or$plan.config.build.allowNetwork-or$plan.config.rulesets.Count-ne0){throw 'Reviewed installation defaults differ.'}
        if([IO.Directory]::Exists($NewInstallRoot)){throw 'Read-only plan unexpectedly created install root.'}
        $completed=$true
    }finally{$process.Dispose()}
}finally{
    $hostEqual=(Get-ArchSiftHostHash)-ceq$hostBefore;$targetEqual=(Hash-Json (Target-State))-ceq(Hash-Json $targetBefore);$existingEqual=(Hash-Json (Existing-State))-ceq(Hash-Json $existingBefore)
    $result=[ordered]@{status=$(if($completed-and$hostEqual-and$targetEqual-and$existingEqual){'plan-ready-for-review'}else{'stop'});runRoot=$run;hostStateEqual=$hostEqual;targetStateEqual=$targetEqual;existing040StateEqual=$existingEqual;applied=$false;analysisOrBuildPerformed=$false}
    [IO.File]::WriteAllText((Join-Path $run 'result.json'),($result|ConvertTo-Json -Depth 5));$result|ConvertTo-Json -Depth 5
    if(-not$hostEqual-or-not$targetEqual-or-not$existingEqual){throw 'Safety drift; preserve evidence and stop. No repair.'}
}
