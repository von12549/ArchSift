[CmdletBinding()]
param([Parameter(Mandatory)][string]$CandidateZip,[Parameter(Mandatory)][string]$CandidateSha256,[string]$LabRoot='D:\ArchSift-lab')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/HostState.ps1')
$before=Get-ArchSiftHostHash
$target='D:\IFX-10-Root\IFX-New';$original='D:\IFX-10-Root\ArchSift'
$run=[IO.Path]::GetFullPath((Join-Path $LabRoot ('evidence/ifx-0.7-preflight-'+[Guid]::NewGuid().ToString('N'))))
if($run.StartsWith($repository+'\',[StringComparison]::OrdinalIgnoreCase)-or$run.StartsWith('D:\IFX-10-Root\',[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must be outside source and original installation.'}
[void][IO.Directory]::CreateDirectory($run)
$errors=[Collections.Generic.List[string]]::new()
function File-Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
try{
    $zip=[IO.Path]::GetFullPath($CandidateZip)
    if($zip.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)-or$zip.StartsWith($original+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Candidate must be external.'}
    if($CandidateSha256-notmatch'^[a-f0-9]{64}$'-or(File-Hash $zip)-cne$CandidateSha256){throw 'Candidate identity differs.'}
    $head=(& git -C $target rev-parse HEAD).Trim();if($LASTEXITCODE-ne0){throw 'Target metadata unavailable.'}
    $status=@(& git -C $target status --porcelain);if($LASTEXITCODE-ne0){throw 'Target status unavailable.'}
    if($head-cne'cc06528ab17052baafa496df2a89f6cde9207384'-or$status.Count){$errors.Add('IFX source differs from reviewed baseline; stop before build.')}
    $projectPaths=@(& git -C $target ls-files -- '*.csproj');if($LASTEXITCODE-ne0){throw 'Project inventory unavailable.'}
    $projects=@();$settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null
    foreach($relative in $projectPaths){
        $path=[IO.Path]::GetFullPath((Join-Path $target $relative));if(-not$path.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Project escapes target.'}
        $reader=[Xml.XmlReader]::Create($path,$settings)
        try{$xml=[Xml.Linq.XDocument]::Load($reader);$reviewItems=@($xml.Descendants()|Where-Object {$_.Name.LocalName-in@('Target','UsingTask','Import','Analyzer','AdditionalFiles')}|ForEach-Object{$_.ToString()})}finally{$reader.Dispose()}
        $projects+=@{path=$relative;sha256=(File-Hash $path);reviewItems=$reviewItems}
    }
    if($projects.Count-ne105){$errors.Add('Project count differs from 105; review current scope.')}
    $installation=Get-Content -Raw -LiteralPath (Join-Path $original 'install.json')|ConvertFrom-Json
    if($installation.selectedVersion-cne'0.6.0'){$errors.Add('Original selected version differs from 0.6.0.')}
    $frozen=@();foreach($file in Get-ChildItem -LiteralPath (Join-Path $original 'config'),(Join-Path $original 'rules') -Recurse -Force -File|Where-Object Extension -eq '.json'){$frozen+=@{path=$file.FullName;sha256=(File-Hash $file.FullName)}}
    $props=@();foreach($name in @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json')){$path=Join-Path $target $name;if(Test-Path -LiteralPath $path){$props+=@{path=$name;sha256=(File-Hash $path)}}}
    $after=Get-ArchSiftHostHash;if($after-cne$before){$errors.Add('Host drift; preserve evidence and stop.')}
    $result=@{schemaVersion=1;kind='ifx-0.7-preflight';candidate=@{path=$zip;sha256=$CandidateSha256};target=@{root=$target;commit=$head;clean=($status.Count-eq0);projects=$projects;contextFiles=$props};original=@{root=$original;selectedVersion=$installation.selectedVersion;frozenFiles=$frozen};hostBeforeSha256=$before;hostAfterSha256=$after;hostStateEqual=($after-ceq$before);errors=$errors.ToArray();analysisPerformed=$false;targetBuildPerformed=$false;installationChanged=$false}
    $resultPath=Join-Path $run 'result.json';[IO.File]::WriteAllText($resultPath,($result|ConvertTo-Json -Depth 14),[Text.UTF8Encoding]::new($false))
    @{evidence=$resultPath;sha256=(File-Hash $resultPath);projectCount=$projects.Count;hostStateEqual=($after-ceq$before);errors=$errors.ToArray()}|ConvertTo-Json
    if($errors.Count){throw 'Preflight requires review; no build/install performed.'}
}finally{if((Get-ArchSiftHostHash)-cne$before){throw 'Host drift; no repair.'}}
