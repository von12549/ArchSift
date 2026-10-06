[CmdletBinding()]
param([string]$LabRoot='D:\ArchSift-lab')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$run=Join-Path ([IO.Path]::GetFullPath($LabRoot)) ('runs/w09-ui-'+[Guid]::NewGuid().ToString('N'))
if($run.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'UI fixture must be outside source.'}
$source=Join-Path $run 'source'
foreach($name in @('Application','Domain')){
    $folder=Join-Path $source $name;[void][IO.Directory]::CreateDirectory($folder)
    [IO.File]::WriteAllText((Join-Path $folder ($name+'.csproj')),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
}
$policy=Join-Path $source 'policy.json'
[IO.File]::WriteAllText($policy,'{"schemaVersion":1,"id":"changes-ui","version":"1","description":"比较示例","rules":[{"id":"application-domain","type":"project-reference","enabled":true,"scope":{"kind":"project","match":"glob","value":"**"},"parameters":{"source":{"kind":"project","match":"exact","value":"Application/Application.csproj"},"target":{"kind":"project","match":"exact","value":"Domain/Domain.csproj"}},"severity":"warning","reason":"演示新增依赖违规"}],"exceptions":[]}')
& git -C $source init | Out-Null
if($LASTEXITCODE-ne0){throw 'Fixture Git init failed.'}
& git -C $source add .
& git -C $source -c user.name='ArchSift Fixture' -c user.email='fixture@archsift.invalid' commit -m "Synthetic UI baseline`n`nCo-Authored-By: Codex <noreply@openai.com>" | Out-Null
if($LASTEXITCODE-ne0){throw 'Fixture commit failed.'}
[IO.File]::WriteAllText((Join-Path $source 'Application/Application.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include="../Domain/Domain.csproj" /></ItemGroup></Project>')
$config=Join-Path $run 'config.json'
[IO.File]::WriteAllText($config,([ordered]@{schemaVersion=1;target=@{root=$source};rulesets=@($policy);output=@{directory=(Join-Path $run 'reports');formats=@('json','html')};rulesDirectory=(Join-Path $run 'rules')}|ConvertTo-Json -Depth 8))
Write-Output ('comparison-preview-run='+$run)
& (Join-Path $PSScriptRoot 'Preview-Workbench.ps1') -Config $config
