$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
if($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted' -or $env:GITHUB_REPOSITORY -cne 'von12549/ArchSift' -or $env:GITHUB_REF -cne 'refs/heads/main' -or $env:GITHUB_RUN_ATTEMPT -cne '1' -or $env:NUGET_API_KEY -or $env:ACTIONS_ID_TOKEN_REQUEST_TOKEN){throw 'Credential-free hosted audit only.'}
. (Join-Path $PSScriptRoot 'HostState.ps1')
$before=Get-ArchSiftHostHash
$binding=Get-Content -LiteralPath (Join-Path $env:RUNNER_TEMP 'archsift-audit-inputs/binding.json') -Raw|ConvertFrom-Json -AsHashtable
if($binding.sourceCommit -cne '8b721f44e90a41285fa74a6f72576c7f6996ef7d' -or $binding.sourceTree -cne '2c18d18145beb52a917a3884ddd7f442ec486967' -or $binding.workflowCommit -cne $env:GITHUB_SHA -or $binding.runId -ne [long]$env:GITHUB_RUN_ID){throw 'Audit binding differs.'}
$root=Join-Path $env:RUNNER_TEMP 'archsift-fresh-audit'
if(Test-Path -LiteralPath $root){throw 'Fresh owned audit directory required.'}
[void][IO.Directory]::CreateDirectory($root)
$source=Join-Path $root 'source';$hooks=Join-Path $root 'empty-hooks';[void][IO.Directory]::CreateDirectory($hooks)
$output=Join-Path $env:RUNNER_TEMP 'archsift-audit-report';[void][IO.Directory]::CreateDirectory($output)
$git=(Get-Command git -CommandType Application|Select-Object -First 1).Source
try{
 & $git -c "core.hooksPath=$hooks" clone --no-checkout --filter=blob:none --single-branch --branch main https://github.com/von12549/ArchSift.git $source
 if($LASTEXITCODE){throw 'Read-only package-source clone failed.'}
 & $git -C $source -c "core.hooksPath=$hooks" checkout --detach $binding.sourceCommit
 if($LASTEXITCODE){throw 'Original source checkout failed.'}
 if(((& $git -C $source rev-parse HEAD).Trim() -cne $binding.sourceCommit) -or ((& $git -C $source rev-parse 'HEAD^{tree}').Trim() -cne $binding.sourceTree)){throw 'Original source/tree differs.'}
 $config=Join-Path $root 'NuGet.Config'
 [IO.File]::WriteAllText($config,'<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders><disabledPackageSources><clear/></disabledPackageSources><auditSources><clear/><add key="official" value="https://data.nuget.org/v3/index.json"/></auditSources></configuration>')
 $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application|Select-Object -First 1).Source)
 $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$source;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 foreach($arg in @('restore',(Join-Path $source 'ArchSift.slnx'),'--locked-mode','--disable-build-servers','--configfile',$config,'--source','https://api.nuget.org/v3/index.json','--packages',(Join-Path $root 'packages'),'-p:NuGetAudit=true','-p:NuGetAuditMode=all','-p:NuGetAuditLevel=moderate','-p:TreatWarningsAsErrors=true')){$start.ArgumentList.Add($arg)}
 $start.Environment['DOTNET_CLI_HOME']=Join-Path $root 'home'
 $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'
 $start.Environment['NUGET_PACKAGES']=Join-Path $root 'packages'
 $start.Environment['NUGET_HTTP_CACHE_PATH']=Join-Path $root 'http-cache'
 $start.Environment['NUGET_SCRATCH']=Join-Path $root 'scratch'
 $start.Environment['NUGET_EXE_NO_PROMPT']='true'
 $start.Environment['DOTNET_CLI_UI_LANGUAGE']='en-US'
 $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
 foreach($key in @('GH_TOKEN','GITHUB_TOKEN','NUGET_API_KEY','ACTIONS_ID_TOKEN_REQUEST_TOKEN','ACTIONS_ID_TOKEN_REQUEST_URL')){[void]$start.Environment.Remove($key)}
 $process=[Diagnostics.Process]::new();$process.StartInfo=$start
 try{
  [void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
  if(-not $process.WaitForExit(600000)){$process.Kill($true);throw 'Official audit timed out; no pass.'}
  $text=$stdout.GetAwaiter().GetResult()+"`n"+$stderr.GetAwaiter().GetResult()
  [IO.File]::WriteAllText((Join-Path $output 'restore.log'),$text,[Text.UTF8Encoding]::new($false))
  if($process.ExitCode -ne 0){Write-Output $text;throw 'Fresh official audit failed; retain evidence.'}
 }finally{$process.Dispose()}
 if((Get-ArchSiftHostHash) -cne $before){throw 'Host drift after audited restore.'}
 if(@(& $git -C $source status --porcelain).Count){throw 'Audit modified original tracked source/locks.'}
 $projects=@();$packages=[Collections.Generic.HashSet[string]]::new()
 foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File -Filter project.assets.json){
  $assets=Get-Content -LiteralPath $file.FullName -Raw|ConvertFrom-Json -AsHashtable
  $audit=$assets.project.restore.restoreAuditProperties
  if($audit.enableAudit -cne 'true' -or $audit.auditMode -cne 'all' -or $audit.auditLevel -cne 'moderate' -or -not $assets.project.restore.warningProperties.allWarningsAsErrors){throw 'Audit settings not effective in every restored project.'}
  foreach($warning in @($assets.project.restore.warningProperties['noWarn'])){if($warning -match '^NU190[0-5]$'){throw 'Advisory warning suppression is prohibited.'}}
  if(@($audit['suppressedAdvisories']).Where({$null -ne $_}).Count){throw 'Audit advisory suppression is prohibited.'}
  foreach($name in $assets.libraries.Keys){if($assets.libraries[$name].type -ceq 'package'){[void]$packages.Add($name)}}
  $projects+=@{project=[IO.Path]::GetRelativePath($source,$assets.project.restore.projectPath).Replace('\','/');assetsSha256=(Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant()}
 }
 if($projects.Count -lt 5 -or $packages.Count -lt 1){throw 'Incomplete source audit graph.'}
 $data=@(Get-ChildItem -LiteralPath (Join-Path $root 'http-cache') -Recurse -File|Where-Object Name -in @('vuln_index.dat','vuln_data_base.dat','vuln_data_update.dat')|ForEach-Object {@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
 foreach($name in @('vuln_index.dat','vuln_data_base.dat','vuln_data_update.dat')){if(@($data|Where-Object name -ceq $name).Count -lt 1){throw 'Fresh official vulnerability data not evidenced.'}}
 if((Get-ArchSiftHostHash) -cne $before){throw 'Host drift at audit completion.'}
 @{schemaVersion=1;status='passed-fresh-source-audit';binding=$binding;completedUtc=[DateTimeOffset]::UtcNow.ToString('O');audit=@{enabled=$true;mode='all';level='moderate';warningsAsErrors=$true;source='https://data.nuget.org/v3/index.json'};projects=$projects;packages=@($packages|Sort-Object);dataFiles=$data;hostEqual=$true;credentialAcquisition=$false;targetApplicationExecuted=$false;scope='Original package-source full locked restore audit; public consumer/UI verification remains separate.'}|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $output 'audit-report.json') -Encoding utf8NoBOM
 Write-Output ('Fresh official source audit passed: '+$projects.Count+' projects, '+$packages.Count+' package identities; no credentials or target execution.')
}finally{if((Get-ArchSiftHostHash) -cne $before){throw 'Host drift; no repair performed.'}}
