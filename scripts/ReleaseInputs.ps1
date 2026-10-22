# Shared release inputs. This module reads source metadata and never changes the host environment.
function Get-ArchSiftReleaseInputs([string]$RepositoryRoot) {
    $root=[IO.Path]::GetFullPath($RepositoryRoot)
    [xml]$props=[IO.File]::ReadAllText((Join-Path $root 'Directory.Build.props'))
    $node=$props.SelectSingleNode('/Project/PropertyGroup/ArchSiftReleaseVersion')
    $version=if($null -eq $node){''}else{$node.InnerText.Trim()}
    if($version -notmatch '^0\.10\.0(?:-rc\.[1-9][0-9]*)?$'){throw 'The reviewed central 0.10.0 release version is required.'}
    $profile=Get-Content -LiteralPath (Join-Path $root 'docs/releases/0.10.0/build-profile.json') -Raw|ConvertFrom-Json
    if($profile.schemaVersion -ne 1 -or $profile.sdk -cne '10.0.303' -or $profile.runtime -cne '10.0.11' -or
        $profile.tfm -cne 'net10.0' -or $profile.rid -cne 'win-x64' -or $profile.configuration -cne 'Release'){
        throw 'Unknown release build profile; review it before execution.'
    }
    [pscustomobject]@{repositoryRoot=$root;version=$version;profile=$profile;assemblyVersion=($version.Split('-')[0]+'.0')}
}

function Assert-ArchSiftExternalPath([string]$RepositoryRoot,[string]$ExternalPath) {
    $root=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($RepositoryRoot))
    $path=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($ExternalPath))
    $comparison=if($IsWindows){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
    if($path.Equals($root,$comparison) -or $path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,$comparison) -or
        $root.StartsWith($path+[IO.Path]::DirectorySeparatorChar,$comparison)){
        throw 'Release state/output and source must be disjoint in both directions.'
    }
    Assert-ArchSiftReleaseNoLinks $path
    $path
}

function Assert-ArchSiftReleaseNoLinks([string]$Path) {
    for($cursor=[IO.Path]::GetFullPath($Path);$cursor;$cursor=[IO.Path]::GetDirectoryName($cursor)){
        if(Test-Path -LiteralPath $cursor){
            $item=Get-Item -LiteralPath $cursor -Force
            if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Release paths must not cross links/reparse points.'}
        }
    }
}

function Get-ArchSiftReleaseArchiveName([string]$Version) {
    if($Version -notmatch '^0\.10\.0(?:-rc\.[1-9][0-9]*)?$'){throw 'Unknown Workbench release identity.'}
    'archsift-workbench-'+$Version+'-win-x64.zip'
}
