# Compare distributed runtime code with reviewed official package bytes, not labels alone.
function Get-ArchSiftRuntimeCodeProof([string]$PayloadRoot,[string]$PackageCache,$Release) {
    $records=[Collections.Generic.List[object]]::new()
    foreach($application in @(@{name='Cli';directory=$PayloadRoot;config='archsift.runtimeconfig.json'},@{name='Web';directory=(Join-Path $PayloadRoot 'web');config='ArchSift.Web.runtimeconfig.json'})){
        $runtime=Get-Content -LiteralPath (Join-Path $application.directory $application.config) -Raw|ConvertFrom-Json
        foreach($framework in $runtime.runtimeOptions.includedFrameworks){
            $approved=@($Release.profile.runtimePackages|Where-Object framework -CEQ $framework.name)
            if($approved.Count -ne 1 -or $framework.version -cne $approved[0].version -or $framework.version -cne $Release.profile.runtime){throw 'Unapproved bundled framework identity.'}
            $id=$approved[0].id.ToLowerInvariant()
            $archivePath=Join-Path (Join-Path (Join-Path $PackageCache $id) $framework.version) ($id+'.'+$framework.version+'.nupkg')
            if((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $approved[0].sha256){throw 'Runtime package archive differs from reviewed official bytes.'}
            $archive=[IO.Compression.ZipFile]::OpenRead($archivePath)
            $matched=[Collections.Generic.List[object]]::new()
            try{
                foreach($entry in $archive.Entries){
                    $prefix=@('runtimes/win-x64/lib/net10.0/','runtimes/win-x64/native/')|Where-Object{$entry.FullName.StartsWith($_,[StringComparison]::Ordinal)}|Select-Object -First 1
                    if(-not $prefix -or $entry.FullName -notmatch '(?i)\.(dll|exe)$'){continue}
                    $relative=$entry.FullName.Substring($prefix.Length)
                    $target=[IO.Path]::GetFullPath((Join-Path $application.directory $relative))
                    if(-not $target.StartsWith($application.directory+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Runtime code path escapes payload.'}
                    if(-not(Test-Path -LiteralPath $target -PathType Leaf)){continue}
                    $stream=$entry.Open()
                    try{$expected=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}finally{$stream.Dispose()}
                    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expected){throw ('Distributed runtime code differs: '+$relative)}
                    $matched.Add(@{path=$relative;sha256=$expected})
                }
            }finally{$archive.Dispose()}
            if($matched.Count -eq 0){throw 'No actual runtime code verified.'}
            if($framework.name -ceq 'Microsoft.NETCore.App'){
                foreach($required in @('coreclr.dll','clrjit.dll','hostfxr.dll','hostpolicy.dll','System.Private.CoreLib.dll')){if($required -notin @($matched|ForEach-Object{$_.path})){throw 'Essential CLR code was not verified.'}}
            }
            $records.Add(@{application=$application.name;framework=$framework.name;version=$framework.version;packageSha256=$approved[0].sha256;matchedCodeFiles=$matched.Count;files=$matched.ToArray();scope='Every actually present DLL/EXE from the approved runtime archive matched; absent optional archive files are not claimed as distributed.'})
        }
    }
    $records.ToArray()
}
