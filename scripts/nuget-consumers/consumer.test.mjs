import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertHosted, contained, readJson, sha256, validatePublication, validateWorkflow, verifyGate, validateConsumerReceipt, verifyPublicInputs, writeJson } from './model.mjs';
import { generateFixtures, nugetXml } from './fixtures.mjs';
const source=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const pub=readJson(path.join(source,'docs/releases/0.11.0/nr06-publication.json'));
const reference=readJson(path.join(source,'docs/releases/0.11.0/trusted-publishing-reference.json'));
const payload=readJson(path.join(source,'docs/releases/0.11.0/nr06-payload-manifest.json'));
const workflow=fs.readFileSync(path.join(source,'.github/workflows/verify-nuget-consumers.yml'),'utf8');
const copy=x=>structuredClone(x);
test('NR05 immutable public identities and original payload manifest bind the same submissions',()=>{
 validatePublication(pub,reference); assert.equal(payload.sourceCommit,pub.sourceCommit);
 for(const p of pub.packages)assert.equal(payload.packages.find(x=>x.id===p.id).submittedSha256,p.submissionSha256);
});
for(const [name,mutate] of [
 ['owner',x=>x.owner='attacker'],['source',x=>x.sourceCommit='a'.repeat(40)],['run',x=>x.publicationRun++],['version',x=>x.version='0.11.1'],
 ['missing-package',x=>x.packages.pop()],['duplicate',x=>x.packages[1]=x.packages[0]],['server-hash',x=>x.packages[0].servedSha256='a'.repeat(64)],['server-bytes',x=>x.packages[0].servedBytes++],['submission-hash',x=>x.packages[0].submissionSha256='a'.repeat(64)],['origin',x=>x.packages[0].url=x.packages[0].url.replace('api.nuget.org','example.com')]
])test(`Reject changed publication ${name}`,()=>{const p=copy(pub);mutate(p);assert.throws(()=>validatePublication(p,reference));});
test('Published archive corruption rejects before package use',async()=>{
 const files=new Map(pub.packages.map(x=>[`${x.id}.0.11.0.nupkg`,Buffer.from('corrupt')]));
 await assert.rejects(verifyPublicInputs(files,new Map(),pub,reference,payload));
});
test('Dedicated workflow is pinned, read only and separates safety from five real consumer cells',()=>validateWorkflow(workflow));
test('Release switch binding cannot be replaced by a constant',()=>assert.throws(()=>validateWorkflow(workflow.replace('${{ vars.ARCHSIFT_NUGET_PUBLISH_0110 }}',"''"))));
for(const [name,from,to] of [
 ['OIDC','contents: read','contents: read\n  id-token: write'],['write-token','actions: read','actions: write'],['untrusted-action','actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9','actions/setup-dotnet@main'],['persisted-token','persist-credentials: false','persist-credentials: true'],['self-hosted','runs-on: ubuntu-latest','runs-on: self-hosted'],['publish-environment','timeout-minutes: 45','timeout-minutes: 45\n    environment: nuget-release'],['push','run: ./scripts/Invoke-NR06HostedConsumers.ps1','run: dotnet nuget push'],['missing-boundary','Assert-NR06HostedContext.ps1','UnreviewedContext.ps1'],['matrix','os: windows-latest, sdk: 10.0.100','os: windows-latest, sdk: 9.0.314']
])test(`Reject unsafe workflow ${name}`,()=>{assert.notEqual(workflow.replace(from,to),workflow);assert.throws(()=>validateWorkflow(workflow.replace(from,to)));});
const hosted={GITHUB_ACTIONS:'true',RUNNER_ENVIRONMENT:'github-hosted',GITHUB_REPOSITORY:'von12549/ArchSift',GITHUB_RUN_ATTEMPT:'1',GITHUB_RUN_ID:'123',GITHUB_SHA:'a'.repeat(40),GITHUB_EVENT_NAME:'workflow_dispatch',GITHUB_REF:'refs/heads/main'};
test('Hosted main and PR contexts accepted without credentials',()=>{assertHosted(hosted);assertHosted({...hosted,GITHUB_EVENT_NAME:'pull_request',GITHUB_REF:'refs/pull/36/merge'});});
for(const [name,override] of [['local',{}],['self-hosted',{...hosted,RUNNER_ENVIRONMENT:'self-hosted'}],['wrong-repo',{...hosted,GITHUB_REPOSITORY:'other/repo'}],['retry',{...hosted,GITHUB_RUN_ATTEMPT:'2'}],['branch',{...hosted,GITHUB_REF:'refs/heads/other'}],['key',{...hosted,NUGET_API_KEY:'synthetic'}],['oidc',{...hosted,ACTIONS_ID_TOKEN_REQUEST_TOKEN:'synthetic'}]])test(`Reject execution context ${name}`,()=>assert.throws(()=>assertHosted(override)));
const temporary=fs.mkdtempSync(path.join(os.tmpdir(),'nr06-synthetic-'));
test('Contained roots reject equal/ancestor/traversal and links',()=>{
 assert.throws(()=>contained(temporary,temporary));assert.throws(()=>contained(temporary,path.dirname(temporary)));assert.throws(()=>contained(temporary,path.join(temporary,'..','escape')));
 assert.equal(contained(temporary,path.join(temporary,'owned')),path.join(temporary,'owned'));
});
test('Generated consumer topology has exact standard files, package references, pins and unexecuted targets',()=>{
 const f=generateFixtures(path.join(temporary,'fixtures'),source,'10.0.100');
 assert.equal(f.libraries.length,4);assert.equal(f.targets.length,3);
 for(const p of f.libraries){const project=fs.readFileSync(path.join(p.directory,'Consumer.csproj'),'utf8');assert.ok(project.includes(`Include="ArchSift.${p.kind}" Version="[0.11.0]"`));assert.ok(!project.includes('ProjectReference'));assert.equal(readJson(path.join(p.directory,'global.json')).sdk.rollForward,'disable');}
 const manifest=readJson(path.join(f.tool,'.config/dotnet-tools.json'));assert.equal(manifest.isRoot,true);assert.equal(manifest.tools['archsift.cli'].rollForward,false);
 const gate=readJson(f.gate);assert.equal(gate.auditMode,'offline-architecture');assert.equal(readJson(path.join(f.mtp,'required-cases.json')).cases.length,3);
 assert.ok(f.targets.every(t=>fs.readFileSync(path.join(t.directory,'NeverExecute.cs'),'utf8').includes('must never run')));
});
test('Explicit NuGet configurations clear inherited sources, fallbacks and audit sources',()=>{
 const config=nugetXml();for(const s of ['packageSources','fallbackPackageFolders','auditSources'])assert.ok(config.includes(`<${s}><clear/>`));assert.ok(!config.includes('ignore-failed'));
 assert.ok(nugetXml({poison:'/owned/poison'}).includes('pattern="ArchSift.*"'));
});
const receiptRoot=path.join(temporary,'receipt');fs.mkdirSync(receiptRoot);
const config={targetRoot:receiptRoot,requiredCases:'required-cases.json'};writeJson(path.join(receiptRoot,config.requiredCases),{synthetic:'frozen'});config.requiredCasesSha256=sha256(fs.readFileSync(path.join(receiptRoot,config.requiredCases)));
const report={outcome:{runGate:'allow'},testScope:{coverageComplete:true},process:{audit:{status:'not-run'}}};
const ctrf={results:{summary:{tests:4,passed:4,failed:0,skipped:0},tests:['OriginalRows(first)','OriginalRows(second)','ProductPackagesMatchApprovedClosure','JsonRule(rule)'].map(name=>({name,status:'passed'}))}};
test('Gate accepts only full explicit four-case receipt',()=>verifyGate(report,ctrf,config));
for(const [name,mutate] of [['empty',x=>x.results.summary.tests=0],['skipped',x=>x.results.summary.skipped=1],['failed',x=>x.results.summary.failed=1],['duplicated',x=>x.results.tests[1]=x.results.tests[0]],['missing-family',x=>x.results.tests[3].name='UnrelatedTest'],['old-row',x=>x.results.tests[2].status='failed']])test(`Reject incomplete gate ${name}`,()=>{const x=copy(ctrf);mutate(x);assert.throws(()=>verifyGate(report,x,config));});
test('Wrong coverage, audit, required-case hashes never pass',()=>{
 assert.throws(()=>verifyGate({...report,testScope:{coverageComplete:false}},ctrf,config));
 assert.throws(()=>verifyGate({...report,process:{audit:{status:'passed'}}},ctrf,config));
 assert.throws(()=>verifyGate(report,ctrf,{...config,requiredCasesSha256:'a'.repeat(64)}));
});
test('A source-built or stale copied assembly cannot satisfy public package provenance',()=>{
 const dll=path.join(receiptRoot,'ArchSift.Contracts.dll');fs.writeFileSync(dll,'unpublished assembly');
 const receipt={kind:'Contracts',version:'0.11.0',checks:'passed',assemblies:[{name:'ArchSift.Contracts',version:'0.11.0',location:dll}]};
 assert.throws(()=>validateConsumerReceipt(receipt,'Contracts',receiptRoot,payload));
});
test('Hosted PowerShell entry keeps host APIs read only and never falls through to installer/push',()=>{
 const script=fs.readFileSync(path.join(source,'scripts/Invoke-NR06HostedConsumers.ps1'),'utf8');
 assert.ok(script.indexOf('Assert-NR06HostedContext.ps1')<script.indexOf('ProcessStartInfo'));
 for(const forbidden of [/SetEnvironmentVariable\s*\(/,/\bsetx\b/i,/\bnuget[\s'",]+push\b/i,/ReleaseInputs\.ps1/])assert.ok(!forbidden.test(script));
 assert.ok(script.includes("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'"));
 assert.ok(script.includes('ReadToEndAsync')&&script.includes('Check-Host')&&script.includes('Get-ArchSiftHostHash'));
});
// Keep small synthetic evidence under the OS temp root until the normal ephemeral job ends.
