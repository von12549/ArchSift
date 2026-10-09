# Shared read-only identities for operator-only 0.5.0 steps.
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
    foreach($arg in (@('--no-optional-locks','--no-replace-objects','-c','core.fsmonitor=false','-c','core.untrackedCache=false','-C',$TargetRoot)+$Arguments)){$start.ArgumentList.Add($arg)}
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
    if($ExistingVersionDirectory){
        $versionRoot=Safe-Path $ExistingVersionDirectory
        if($versionRoot-cne$ExistingInstallRoot-and-not$versionRoot.StartsWith($ExistingInstallRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Existing version directory escapes installation root.'}
    }else{
        $candidates=@($ExistingInstallRoot,(Join-Path $ExistingInstallRoot 'versions/0.4.0'))
        $matches=@($candidates|Where-Object {[IO.File]::Exists((Safe-Path (Join-Path $_ 'package-manifest.json')))})
        if($matches.Count-ne1){throw 'Expected exactly one existing 0.4.0 package root; specify -ExistingVersionDirectory after review.'}
        $versionRoot=Safe-Path $matches[0]
    }
    $manifestPath=Join-Path $versionRoot 'package-manifest.json'
    $manifest=Get-Content -LiteralPath (Safe-Path $manifestPath) -Raw|ConvertFrom-Json
    if($manifest.version-cne'0.4.0'-or$manifest.sourceCommit-cne'bb599bd87aaa3322229c049442e135f3c091b72a'-or(Hash-File $manifestPath)-cne'd35fea42475633eb290c858e4df4aec4387dad639d2d03480d20e9edd532849c'){throw 'Existing 0.4.0 manifest differs; stop for review.'}
    foreach($file in $manifest.entries){$path=Safe-Path (Join-Path $versionRoot $file.path);if(-not$path.StartsWith($versionRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or(Hash-File $path)-cne$file.sha256){throw 'Existing package identity differs.'}}
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
    return [ordered]@{versionDirectory=$versionRoot;manifestSha256=(Hash-File $manifestPath);payloadCount=$manifest.entries.Count;configSha256=(Hash-File $configPath);librarySha256=(Hash-Json @($records|Sort-Object path));libraryFileCount=$records.Count}
}
