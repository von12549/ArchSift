import fs from 'node:fs';
import path from 'node:path';
import { writeJson, sha256 } from './model.mjs';
export function nugetXml({ cache, poison, poisonOnly = false, unavailable = false, localFeed } = {}) {
  const escape = value => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');
  const source = localFeed ? `<add key="local" value="${escape(localFeed)}" />` : `<add key="nuget.org" value="${unavailable ? 'https://127.0.0.1:1/v3/index.json' : 'https://api.nuget.org/v3/index.json'}" />`;
  const mapping = poison ? `<packageSourceMapping><clear/><packageSource key="nuget.org"><package pattern="${poisonOnly ? '*' : 'ArchSift.*'}"/>${poisonOnly ? '' : '<package pattern="*"/>'}</packageSource><packageSource key="poison"><package pattern="${poisonOnly ? 'ArchSift.*' : '*'}"/></packageSource></packageSourceMapping>` : '';
  return `<configuration><packageSources><clear/>${source}${poison ? `<add key="poison" value="${escape(poison)}"/>` : ''}</packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders><disabledPackageSources><clear/></disabledPackageSources><auditSources><clear/></auditSources>${cache ? `<config><add key="globalPackagesFolder" value="${escape(cache)}"/></config>` : ''}${mapping}</configuration>`;
}
const text = (file, bytes) => { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, bytes); };
export function project(root, sdk, id, version = '0.11.0', tfm = 'net10.0') {
  writeJson(path.join(root, 'global.json'), { sdk: { version: sdk, rollForward: 'disable', allowPrerelease: false } });
  text(path.join(root, 'NuGet.Config'), nugetXml());
  text(path.join(root, 'Consumer.csproj'), `<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>${tfm}</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile><NuGetAudit>false</NuGetAudit><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup>${id ? `<ItemGroup><PackageReference Include="${id}" Version="[${version}]"/></ItemGroup>` : ''}</Project>`);
}
export const typeRule = forbidden => ({ schemaVersion: 1, id: 'nr06-synthetic', version: '1.0.0', description: 'Public synthetic consumer, no IFX adoption', rules: [{ id: 'synthetic-no-transactions', type: 'type-dependency', enabled: true, scope: { kind: 'type', match: 'exact', value: 'Synthetic.AllowedSource' }, parameters: { source: { kind: 'type', match: 'exact', value: 'Synthetic.AllowedSource' }, forbiddenTarget: { kind: 'type', match: 'exact', value: forbidden } }, severity: 'warning', reason: 'Public package consumer acceptance' }], exceptions: [] });
const frameworkRule = allow => ({ schemaVersion: 1, id: 'nr06-frameworks', version: '1.0.0', description: 'Controlled target framework selection', rules: [{ id: 'framework-contract', type: 'target-framework', enabled: true, scope: { kind: 'project', match: 'glob', value: '**' }, parameters: { allowedFrameworks: allow }, severity: 'warning', reason: 'Synthetic declared-target rule' }], exceptions: [] });
export function consumerProgram(kind) {
  const common = `using System.Text.Json;\nusing System.Reflection;\nusing ArchSift.Contracts;\n`;
  const check = {
    Contracts: 'if (ToolIdentity.Name != "archsift" || ToolIdentity.Version != "0.11.0") throw new Exception("Contracts API/version");',
    Core: 'if (ArchSift.Core.ContentHash.Text("abc") != "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad") throw new Exception("Core hash API"); var snapshot = ArchSift.Core.ProjectDiscovery.Discover(Environment.CurrentDirectory, "Consumer.csproj", "net10.0"); if(snapshot.Projects.Length != 1 || snapshot.Projects[0].Frameworks.Single() != "net10.0") throw new Exception("Core real discovery");',
    ArchUnit: 'var bundle = ArchSift.Core.RuleLoader.Compose(new[]{ArchSift.Core.RuleLoader.Load("rules.json")}); var result = ArchSift.ArchUnit.TypeDependencyTestEngine.Evaluate(bundle, new[]{typeof(Synthetic.AllowedSource).Assembly.Location}); if(result.Results.Single().Status != "pass" || result.Results.Single().Matched != 1) throw new Exception("Real ArchUnit API"); var bad = ArchSift.ArchUnit.TypeDependencyTestEngine.Evaluate(ArchSift.Core.RuleLoader.Compose(new[]{ArchSift.Core.RuleLoader.Load("violation.json")}), new[]{typeof(Synthetic.AllowedSource).Assembly.Location}); if(bad.Results.Single().Status != "violation" || bad.Findings.Length == 0) throw new Exception("Expected dependency violation");',
    Testing: 'var (bundle,cases) = ArchSift.Testing.ArchitectureTestAdapter.LoadCases(new[]{"rules.json"}); if(cases.Length != 1) throw new Exception("Testing case identity"); ArchSift.Testing.ArchitectureTestAdapter.AssertPassed(ArchSift.Testing.ArchitectureTestAdapter.EvaluateCase(bundle,cases[0],new[]{typeof(Synthetic.AllowedSource).Assembly.Location})); var (badBundle,badCases)=ArchSift.Testing.ArchitectureTestAdapter.LoadCases(new[]{"violation.json"}); var rejected=false; try { ArchSift.Testing.ArchitectureTestAdapter.AssertPassed(ArchSift.Testing.ArchitectureTestAdapter.EvaluateCase(badBundle,badCases[0],new[]{typeof(Synthetic.AllowedSource).Assembly.Location})); } catch(Exception error) when(error.Message.Contains("violation",StringComparison.OrdinalIgnoreCase)) { rejected=true; } if(!rejected) throw new Exception("Testing did not reject real violation");'
  }[kind];
  return common + check + `\nvar assemblies=AppDomain.CurrentDomain.GetAssemblies().Where(a => (a.GetName().Name ?? "").StartsWith("ArchSift.",StringComparison.Ordinal)).Select(a=>new{ name=a.GetName().Name, location=a.Location, version=a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion }).ToArray();\nConsole.WriteLine(JsonSerializer.Serialize(new{kind="${kind}",version=ToolIdentity.Version,checks="passed",assemblies}));\nnamespace Synthetic { public sealed class AllowedSource { public string Name => "synthetic"; } }\n`;
}
export function generateFixtures(root, source, sdk) {
  const tool = path.join(root, 'tool target'); project(tool, sdk);
  fs.rmSync(path.join(tool, 'Consumer.csproj')); // Exact newly created file, not a source/unknown file.
  writeJson(path.join(tool, '.config/dotnet-tools.json'), {version:1,isRoot:true,tools:{'archsift.cli':{version:'0.11.0',commands:['archsift'],rollForward:false}}});
  const libraries = ['Contracts','Core','ArchUnit','Testing'].map(kind => {
    const directory = path.join(root, 'libraries', kind); project(directory, sdk, `ArchSift.${kind}`);
    text(path.join(directory, 'Program.cs'), consumerProgram(kind));
    writeJson(path.join(directory, 'rules.json'), typeRule('System.Transactions.TransactionScope'));
    writeJson(path.join(directory, 'violation.json'), typeRule('System.String'));
    return { kind, directory };
  });
  const targets = ['net8.0','net9.0','net10.0'].map(tfm => {
    const directory = path.join(root, 'analyzed targets', tfm); project(directory, sdk);
    text(path.join(directory, 'Consumer.csproj'), `<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>${tfm}</TargetFramework></PropertyGroup></Project>`);
    text(path.join(directory, 'NeverExecute.cs'), 'public static class NeverExecute { public static void Main() => throw new System.Exception("Analyzed business entry must never run"); }');
    const rules = path.join(root, 'analysis inputs', tfm, 'pass.json'); writeJson(rules, frameworkRule([tfm]));
    const badRules = path.join(root, 'analysis inputs', tfm, 'violation.json'); writeJson(badRules, frameworkRule(['net7.0']));
    const config = path.join(root, 'analysis inputs', tfm, 'config.json');
    writeJson(config, {schemaVersion:1,target:{root:directory,entry:'Consumer.csproj'},rulesets:[rules],build:{mode:'existing',targetFramework:tfm},output:{directory:path.join(root,'analysis results',tfm),formats:['json']}});
    return { tfm, directory, config, rules, badRules };
  });
  const unsupported = ['net8.0','net9.0'].map(tfm => { const directory=path.join(root,'unsupported consumer',tfm); project(directory,sdk,'ArchSift.Contracts','0.11.0',tfm); text(path.join(directory,'Program.cs'),'System.Console.WriteLine(ArchSift.Contracts.ToolIdentity.Version);'); return directory; });
  const mtp = path.join(root,'MTP target'); project(mtp,sdk);
  fs.rmSync(path.join(mtp,'Consumer.csproj'));
  text(path.join(mtp,'Portable.Architecture.Tests.csproj'), `<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile><UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner><NuGetAudit>false</NuGetAudit><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup><ItemGroup><PackageReference Include="ArchSift.Testing" Version="[0.11.0]"/><PackageReference Include="xunit.v3" Version="[4.0.1]"/></ItemGroup></Project>`);
  text(path.join(mtp,'PortableTests.cs'),fs.readFileSync(path.join(source,'samples/portable-verification/PortableTests.cs')));
  text(path.join(mtp,'ArchSiftCaseIdentityAttribute.cs'),fs.readFileSync(path.join(source,'samples/ifx-formal/ArchSiftCaseIdentityAttribute.cs')));
  text(path.join(mtp,'PackageContractTests.cs'),`using System.Text.Json; using ArchSift.Testing; using Xunit; namespace Synthetic; public sealed class PackageContractTests { [Fact] public void ProductPackagesMatchApprovedClosure() { using var config=JsonDocument.Parse(File.ReadAllBytes(Environment.GetEnvironmentVariable("ARCHSIFT_TEST_GATE_CONFIG")!)); PackageToolApproval.Verify(config.RootElement.GetProperty("targetRoot").GetString()!,"Portable.Architecture.Tests.csproj","package-approval.json"); } }`);
  writeJson(path.join(mtp,'rules.json'),typeRule('System.Transactions.TransactionScope'));
  const textType='System.String, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e';
  const cases=['first','second'].map(value=>({group:'synthetic-original',identity:{assembly:'Portable.Architecture.Tests',type:'Synthetic.PortableTests',method:'OriginalRows',parameterTypes:[textType],arguments:[{declaredType:textType,runtimeType:textType,kind:'string',value}]}}));
  cases.push({group:'synthetic-package-contract',identity:{assembly:'Portable.Architecture.Tests',type:'Synthetic.PackageContractTests',method:'ProductPackagesMatchApprovedClosure',parameterTypes:[],arguments:[]}});
  writeJson(path.join(mtp,'required-cases.json'),{schemaVersion:1,kind:'archsift-required-cases',sdk,runner:'mtp-ctrf/xunit-v3-4.0.1',targetFramework:'net10.0',packageVersion:'0.11.0',cases});
  writeJson(path.join(mtp,'policy.json'),{schemaVersion:1,version:'nr06-synthetic-1',rulesets:[{path:'rules.json',sha256:sha256(fs.readFileSync(path.join(mtp,'rules.json')))}],sources:[]});
  const gate=path.join(root,'gate-config.json'), feed=path.join(root,'same-network-restore-feed');
  fs.mkdirSync(feed);
  writeJson(gate,{schemaVersion:2,targetRoot:mtp,testProject:'Portable.Architecture.Tests.csproj',testExecutableName:'Portable.Architecture.Tests',targetFramework:'net10.0',configuration:'Release',nuGetConfig:'NuGet.Gate.Config',localFeed:feed,packageCache:path.join(root,'packages'),rulesets:['rules.json'],runDirectory:path.join(root,'gate-results'),dotnetCliHome:path.join(root,'gate-results','home'),runnerFormat:'mtp-ctrf',auditMode:'offline-architecture',toolIdentity:'0.11.0',policyFile:'policy.json',policySha256:sha256(fs.readFileSync(path.join(mtp,'policy.json'))),requiredCases:'required-cases.json',requiredCasesSha256:sha256(fs.readFileSync(path.join(mtp,'required-cases.json')))});
  text(path.join(mtp,'NuGet.Gate.Config'),nugetXml({localFeed:feed}));
  const shim=path.join(root,'probe shim'); project(shim,sdk);
  text(path.join(shim,'Program.cs'),`using System.Text.Json; using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"probe.json"))); var key=args.Single() switch { "--version"=>"sdk", "--info"=>"info", "--list-runtimes"=>"runtimes", _=>throw new Exception("Probes only; never a replacement application host") }; Console.WriteLine(doc.RootElement.GetProperty(key).GetString());`);
  const result={schemaVersion:1,sdk,root,tool,libraries,targets,unsupported,mtp,gate,feed,shim};
  writeJson(path.join(root,'fixtures.json'),result); return result;
}
export function workspace(configPath, target, state, dotnet, cache = null, nugetConfig = null) {
  writeJson(configPath,{kind:'archsift-workspace',schemaVersion:1,project:{id:'nr06-consumer',root:target,entry:'Consumer.csproj'},state:{root:state},nuget:{mode:'configured',allowNetwork:true,configFile:nugetConfig,cacheDirectory:cache},runtime:{mode:'host',dotnetPath:dotnet,minimumSdkVersion:'10.0.0',allowPrerelease:false},workbench:{rulesets:[],build:{mode:'existing'}}});
}
