import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { assertHosted, contained, readJson, writeJson, sha256, readZip, verifyPublicInputs, validateConsumerReceipt, verifyGate, verifyTargetReport } from './model.mjs';
import { generateFixtures, nugetXml, workspace } from './fixtures.mjs';
const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const publication = readJson(path.join(source,'docs/releases/0.11.0/nr06-publication.json'));
const reference = readJson(path.join(source,'docs/releases/0.11.0/trusted-publishing-reference.json'));
const payload = readJson(path.join(source,'docs/releases/0.11.0/nr06-payload-manifest.json'));
assert.equal(sha256(fs.readFileSync(path.join(source,'docs/releases/0.11.0/nr06-payload-manifest.json'))),publication.payloadManifestSha256,'Original payload manifest bytes changed');
assertHosted(process.env);
const [operation, rootArg, sdk] = process.argv.slice(2);
const root = contained(process.env.RUNNER_TEMP, rootArg);
contained(root, path.join(root,'fixtures.json'));
if (operation === 'prepare') {
  assert.ok(['10.0.100','10.0.303','10.0.402'].includes(sdk));
  assert.ok(!fs.existsSync(root), 'Keep prior evidence; fresh root required'); fs.mkdirSync(root);
  const packages = new Map(), pages = new Map();
  for (const item of publication.packages) {
    const response = await fetch(item.url,{redirect:'error',signal:AbortSignal.timeout(60000)}); assert.equal(response.status,200);
    const bytes = Buffer.from(await response.arrayBuffer()); packages.set(`${item.id}.0.11.0.nupkg`,bytes);
    const gallery = await fetch(`https://www.nuget.org/packages/${item.id}/0.11.0`,{redirect:'error',signal:AbortSignal.timeout(30000)}); assert.equal(gallery.status,200); pages.set(item.id,await gallery.text());
  }
  await verifyPublicInputs(packages,pages,publication,reference,payload);
  fs.mkdirSync(path.join(root,'signed-public-inputs'));
  for (const [name,bytes] of packages) fs.writeFileSync(path.join(root,'signed-public-inputs',name),bytes);
  const cli = await readZip(packages.get('ArchSift.Cli.0.11.0.nupkg'));
  const template = cli.get('tools/net10.0/any/templates/project/.archsift/start.ps1.template'); assert.ok(template,'Package must initialize without external startup scripts');
  fs.writeFileSync(path.join(root,'package-start.ps1'),template);
  const fixtures=generateFixtures(root,source,sdk);
  fs.mkdirSync(path.join(root,'poison feed'));
  fs.writeFileSync(path.join(root,'poison feed','ArchSift.Cli.0.11.0.nupkg'),'owned deliberately corrupted package');
  fs.writeFileSync(path.join(fixtures.tool,'NuGet.Mapping.Config'),nugetXml({poison:path.join(root,'poison feed')}));
  fs.writeFileSync(path.join(fixtures.tool,'NuGet.Poison.Config'),nugetXml({poison:path.join(root,'poison feed'),poisonOnly:true}));
  fs.writeFileSync(path.join(fixtures.tool,'NuGet.Unavailable.Config'),nugetXml({unavailable:true}));
  writeJson(path.join(root,'download-verification.json'),{schemaVersion:1,status:'passed-public-signed-byte-owner-payload-binding',publicationRun:publication.publicationRun,sourceCommit:publication.sourceCommit,packages:publication.packages,signatureCryptographicVerification:'pending-native-hosted-verifier',harnessCommit:process.env.GITHUB_SHA,runId:process.env.GITHUB_RUN_ID,runAttempt:1,sdk,offlineClaim:false,humanAcceptance:false});
} else if (operation === 'workspace') {
  const [target,state,dotnet,cache,config] = process.argv.slice(4);
  contained(root,target); contained(root,state); if(cache && cache!=='null') contained(root,cache);
  workspace(path.join(target,'.archsift/workspace.json'),target,state,dotnet==='null'?null:dotnet,cache==='null'?null:cache,config==='null'?null:config);
} else if (operation === 'finish') {
  const fixtures=readJson(path.join(root,'fixtures.json'));
  for(const item of fixtures.libraries){
    const receipt=readJson(path.join(root,`api-${item.kind}.stdout.log`)); validateConsumerReceipt(receipt,item.kind,item.directory,payload);
    writeJson(path.join(root,`provenance-${item.kind}.json`),{...receipt,assemblies:receipt.assemblies.map(a=>({...a,sha256:sha256(fs.readFileSync(a.location))}))});
    const assets=readJson(path.join(item.directory,'obj/project.assets.json'));
    assert.ok(Object.keys(assets.packageFolders).every(x => path.resolve(x)===path.join(root,'packages')),'Only owned cache folders');
    assert.equal(assets.libraries[`ArchSift.${item.kind}/0.11.0`].type,'package');
    assert.ok(!Object.values(assets.libraries).some(x=>x.type==='project'));
  }
  const config=readJson(fixtures.gate);
  if(fixtures.sdk==='10.0.303'){
    const report=readJson(path.join(config.runDirectory,'architecture-test-run.json'));
    verifyGate(report,readJson(path.join(config.runDirectory,'test-results.ctrf.json')),config);
  }else{
    assert.ok(fs.readFileSync(path.join(root,'full-gate-sdk-contract-rejection.stderr.log'),'utf8').includes('Unsupported or incomplete required-case manifest.'));
  }
  for(const item of fixtures.targets){
    for(const outcome of ['pass','violation']){
      const result=readJson(path.join(root,`target-${item.tfm}-${outcome}.stdout.log`));
      verifyTargetReport(result,item.tfm,outcome,fixtures.sdk);
    }
  }
  const checks=readJson(path.join(root,'checks.json')); assert.ok(checks.every(x=>x.hostEqual && x.expectationMet));
  const prefix=['sdk','frameworks','tool-restore','tool-version','tool-path-install','tool-path-version','mapped-source-restore','poison-source-rejection','missing-version-rejection','unavailable-source-rejection','mtp-seed-lock',fixtures.sdk==='10.0.303'?'full-gate':'full-gate-sdk-contract-rejection','host-shim-build'];
  for(const name of prefix) assert.equal(checks.filter(x=>x.name===name).length,1,`Missing or duplicate required step ${name}`);
  const mtp=fixtures.sdk==='10.0.303'?{status:'passed-exact-approved-SDK',sdk:'10.0.303',cSharp:3,json:1,total:4,passed:4,failed:0,skipped:0,audit:'not-run-functional-phase'}:{status:'expected-rejection-existing-exact-SDK-contract',requiredSdk:'10.0.303',attemptedSdk:fixtures.sdk,executedCases:0};
  const gateStatus={schemaVersion:1,status:'passed-NR06.00-through-NR06.03-machine-cell',harnessCommit:process.env.GITHUB_SHA,runId:process.env.GITHUB_RUN_ID,runAttempt:1,selectedSdk:fixtures.sdk,runtime:'10.0.13',publicPackageVersion:'0.11.0',packageSourceCommit:publication.sourceCommit,checks:checks.length,libraryConsumers:4,mtp,analyzedFrameworks:['net8.0','net9.0','net10.0'],analyzedAssemblyBinding:'source-bound-isolated-allow-and-real-type-violation',windowsEntryScope:process.platform==='win32'?'project entry/cache/init/doctor and embedded UI smoke':'not-applicable-Windows-only',strictOffline:'not-executed-awaiting-user-environment-confirmation',fullLifecycle:'not-executed-NR06.05',freshCompleteClosureAudit:'not-executed-NR06.06',humanAcceptance:'pending-user-only',publicationSwitch:'absent-at-independent-preflight',localAuthorRuntimeUsed:false,hostEqual:true};
  writeJson(path.join(root,'qualification.json'),gateStatus); console.log(JSON.stringify(gateStatus));
} else throw new Error('Unknown consumer preparation operation');
