#Requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OldCliRoot,[Parameter(Mandatory)][string]$NewCliRoot,
      [Parameter(Mandatory)][string]$OutputRoot,[string]$RuntimeReferenceDirectory=$PSHOME)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'HostState.ps1')
$baseline=Get-ArchSiftHostHash
$old=[IO.Path]::GetFullPath($OldCliRoot);$new=[IO.Path]::GetFullPath($NewCliRoot);$output=[IO.Path]::GetFullPath($OutputRoot)
foreach($root in @($old,$new)){
    if($output -ieq $root -or $output.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Comparison output overlaps an input.'}
    if((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked input root.'}
}
if(Test-Path -LiteralPath $output){throw 'Use fresh comparison output.'}
[void][IO.Directory]::CreateDirectory($output)
$parser=Join-Path $new 'Mono.Cecil.dll'
if((Get-FileHash $parser).Hash -cne (Get-FileHash (Join-Path $old 'Mono.Cecil.dll')).Hash -or [Reflection.AssemblyName]::GetAssemblyName($parser).Version.ToString() -cne '0.11.6.0'){throw 'Unreviewed or changed metadata parser.'}
Add-Type -LiteralPath $parser
function HashBytes([byte[]]$Bytes){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()}
function NativeResources([string]$Path){
    $stream=[IO.File]::OpenRead($Path);$pe=[Reflection.PortableExecutable.PEReader]::new($stream)
    try{
        $dir=$pe.PEHeaders.PEHeader.ResourceTableDirectory
        if($dir.Size -eq 0){return 'absent'}
        $bytes=[byte[]]@($pe.GetSectionData($dir.RelativeVirtualAddress).GetContent(0,$dir.Size))
        return HashBytes $bytes
    }finally{$pe.Dispose();$stream.Dispose()}
}
function CanonicalModule([string]$Path,[ValidateSet('none','il','metadata')][string]$Mutation='none'){
    $resolver=[Mono.Cecil.DefaultAssemblyResolver]::new();$resolver.AddSearchDirectory((Split-Path $Path));$resolver.AddSearchDirectory($RuntimeReferenceDirectory)
    $reader=[Mono.Cecil.ReaderParameters]::new();$reader.InMemory=$true;$reader.ReadSymbols=$true;$reader.ThrowIfSymbolsAreNotMatching=$true;$reader.AssemblyResolver=$resolver
    $assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path,$reader)
    try{
        $module=$assembly.MainModule
        if(-not $module.HasSymbols){throw 'Expected matching portable debugging symbols.'}
        $module.Mvid=[Guid]::Empty
        if($Mutation -ceq 'il'){$method=@($module.Types|ForEach-Object Methods|Where-Object HasBody)[0];$method.Body.Instructions.Insert(0,[Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Nop))}
        if($Mutation -ceq 'metadata'){$assembly.Name.Name+='__verification_negative_control'}
        $writer=[Mono.Cecil.WriterParameters]::new();$writer.WriteSymbols=$false;$writer.DeterministicMvid=$false;$writer.Timestamp=[uint32]0
        $stream=[IO.MemoryStream]::new()
        try{$assembly.Write($stream,$writer);$bytes=$stream.ToArray();if($bytes.Length -lt 1024){throw 'Empty normalized module.'};return HashBytes $bytes}finally{$stream.Dispose()}
    }finally{$assembly.Dispose();$resolver.Dispose()}
}
$rows=@(foreach($file in Get-ChildItem -LiteralPath $old -Filter '*.dll' -File -Recurse|Where-Object {$_.Name -like 'ArchSift.*.dll' -or $_.Name -ceq 'archsift.dll'}){
    if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked module input.'}
    $relative=[IO.Path]::GetRelativePath($old,$file.FullName);$target=Join-Path $new $relative
    $a=CanonicalModule $file.FullName;$b=CanonicalModule $target;$ra=NativeResources $file.FullName;$rb=NativeResources $target
    @{path=$relative.Replace('\','/');equal=($a -ceq $b -and $ra -ceq $rb);oldNormalizedSha256=$a;mainNormalizedSha256=$b;oldRawSha256=(Get-FileHash $file.FullName).Hash.ToLowerInvariant();mainRawSha256=(Get-FileHash $target).Hash.ToLowerInvariant();nativeResourcesEqual=($ra -ceq $rb);portableSymbolsMatch=$true}
})
$control=Join-Path $old 'ArchSift.Core.dll';$plain=CanonicalModule $control
$negativeIl=$plain -cne (CanonicalModule $control -Mutation il);$negativeMetadata=$plain -cne (CanonicalModule $control -Mutation metadata)
$equal=(Get-ArchSiftHostHash) -ceq $baseline
$passed=$rows.Count -gt 0 -and @($rows|Where-Object {-not $_.equal}).Count -eq 0 -and $negativeIl -and $negativeMetadata -and $equal
@{schemaVersion=1;status=$(if($passed){'passed-normalized-managed-image-equivalence'}else{'differences-require-review'});method='Read complete managed modules with matching portable PDB validation; reserialize without debugging symbols with only MVID and PE timestamp normalized. Independently compare raw native resources. Original code is never executed or written. IL and metadata mutation controls must change the normalized digest.';normalizer='Mono.Cecil/0.11.6';parserSha256=(Get-FileHash $parser).Hash.ToLowerInvariant();verifierSha256=(Get-FileHash $PSCommandPath).Hash.ToLowerInvariant();modules=$rows;negativeIlControl=$negativeIl;negativeMetadataControl=$negativeMetadata;hostEqual=$equal;rawPayloadEquality=$false;humanPerformed=$false;publicationPerformed=$false}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $output 'managed-equivalence.json') -Encoding utf8NoBOM
Write-Output ('Managed image comparison: '+$rows.Count+' modules; passed='+$passed+'; hostEqual='+$equal)
if(-not $passed){throw 'Managed image equivalence is not proven; inspect preserved evidence.'}
