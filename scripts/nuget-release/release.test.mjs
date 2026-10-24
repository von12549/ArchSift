import { assertPreviewOnly, createPreviewData } from './stage-preview.mjs';
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { spawnSync } from 'node:child_process';
import { checkContext, checkEnvironment, checkRequest, checkArtifact, checkPackages, checkOwnerPage, checkCiRun, checkPreviewArtifact, publishSequential, sha256, readZip, safeEntry } from './model.mjs';
import { GitHubReader } from './github.mjs';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const reference = JSON.parse(fs.readFileSync(path.join(root, 'docs/releases/0.11.0/trusted-publishing-reference.json')));
const policy = JSON.parse(fs.readFileSync(path.join(root, 'docs/releases/0.11.0/publishing-activation.json')));
const context = { repository: policy.repository, repositoryId: String(policy.repositoryId), ownerId: String(policy.ownerId), ref: 'refs/heads/main', sha: 'a'.repeat(40), mainSha: 'a'.repeat(40), runAttempt: '1', event: 'workflow_dispatch', workflowSha: '', dataCommit: '', requestSha256: '', releaseSwitch: '' };
const active = { ...policy, realAssetTransferEnabled: true, publicationEnabled: true };
const publishing = { ...context, workflowSha: context.sha, dataCommit: 'b'.repeat(40), requestSha256: 'c'.repeat(64), releaseSwitch: 'true' };
test('default preflight works with publication locked', () => checkContext(context, policy, 'preflight'));
test('even a true repository switch cannot open committed publication lock', () => assert.throws(() => checkContext(publishing, policy, 'publish')));
test('TA04 permits exact preview inputs while publication stays locked', () => checkContext(publishing, policy, 'preview'));
test('disabled transfer still rejects preview', () => assert.throws(() => checkContext(publishing, {...policy,realAssetTransferEnabled:false}, 'preview')));
test('separately enabled synthetic publication model accepts exact context', () => checkContext(publishing, active, 'publish'));
for (const [name, change, mode] of [
  ['wrong repo', {repository: 'other/ArchSift'}, 'preflight'],
  ['wrong repository ID', {repositoryId: '1'}, 'preflight'],
  ['wrong owner ID', {ownerId: '1'}, 'preflight'],
  ['tag deployment', {ref: 'refs/tags/v0.11.0'}, 'preflight'],
  ['main moved', {mainSha: 'd'.repeat(40)}, 'preflight'],
  ['workflow rerun', {runAttempt: '2'}, 'publish'],
  ['automatic push', {event: 'push'}, 'publish'],
  ['switch missing', {releaseSwitch: ''}, 'publish'],
  ['switch false', {releaseSwitch: 'false'}, 'publish'],
  ['switch nonexact', {releaseSwitch: 'True'}, 'publish'],
  ['unapproved workflow commit', {workflowSha: 'b'.repeat(40)}, 'publish'],
  ['invalid data commit', {dataCommit: '../escape'}, 'publish'],
  ['invalid request hash', {requestSha256: 'z'.repeat(64)}, 'publish'],
  ['unknown mode', {}, 'other'],
  ['staged input on default mode', {dataCommit: 'b'.repeat(40)}, 'preflight']
]) test(`reject ${name}`, () => assert.throws(() => checkContext({ ...(mode === 'preflight' ? context : publishing), ...change }, active, mode)));
const environment = { name: 'nuget-release', can_admins_bypass: false, deployment_branch_policy: { protected_branches: false, custom_branch_policies: true }, protection_rules: [{ type: 'required_reviewers', prevent_self_review: false, reviewers: [{ type: 'User', reviewer: { login: 'von12549', id: 2306937 } }] }, { type: 'branch_policy' }] };
const branches = { total_count: 1, branch_policies: [{ type: 'branch', name: 'main' }] };
test('retain exact existing operator approval rules', () => checkEnvironment(environment, branches, policy));
for (const [name, mutate] of [
  ['administrator bypass', e => { e.can_admins_bypass = true; }],
  ['missing reviewer', e => { e.protection_rules[0].reviewers = []; }],
  ['different reviewer', e => { e.protection_rules[0].reviewers[0].reviewer.id = 1; }],
  ['missing environment', e => { e.name = ''; }],
  ['unprotected branch wildcard', e => { e.deployment_branch_policy.custom_branch_policies = false; }]
]) test(`reject ${name}`, () => { const e = structuredClone(environment); mutate(e); assert.throws(() => checkEnvironment(e, branches, policy)); });
test('reject tag rule added to environment', () => assert.throws(() => checkEnvironment(environment, { total_count: 2, branch_policies: [...branches.branch_policies, {type:'tag',name:'*'}] }, policy)));
const run = { repository: {full_name:policy.repository}, head_repository: {full_name:policy.repository}, path: '.github/workflows/verify.yml', head_sha: context.sha, event:'push', status:'completed', conclusion:'success' };
const expectedRun = {path:run.path,sha:context.sha,event:'push'};
test('accept pinned successful same-repository CI', () => checkCiRun(run, expectedRun));
for (const [key,value] of [['head_sha','d'.repeat(40)],['event','pull_request'],['conclusion','failure'],['status','in_progress'],['path','.github/workflows/other.yml'],['head_repository',{full_name:'fork/ArchSift'}]]) test(`reject CI ${key}`, () => assert.throws(() => checkCiRun({...run,[key]:value},expectedRun)));
const request = { schemaVersion:1,version:reference.version,sourceCommit:reference.sourceCommit,sourceTree:reference.sourceTree,workflowCommit:context.sha,repositoryId:policy.repositoryId,ownerId:policy.ownerId,packages:reference.packages,payloadManifestSha256:policy.payloadManifestSha256,acceptanceSha256:reference.acceptedPreview.acceptanceSha256 };
test('request is bound to exact bytes/source/workflow/acceptance', () => {const bytes=Buffer.from(JSON.stringify(request));checkRequest(bytes,sha256(bytes),reference,policy,context);});
for (const [name, mutate] of [
 ['extra field',r=>{r.command='evil';}],['wrong source',r=>{r.sourceCommit='b'.repeat(40);}],['wrong workflow',r=>{r.workflowCommit='b'.repeat(40);}],['wrong manifest',r=>{r.payloadManifestSha256='0'.repeat(64);}],['wrong acceptance',r=>{r.acceptanceSha256='0'.repeat(64);}],['changed packages',r=>{r.packages.reverse();}]
]) test(`reject request ${name}`,()=>{const r=structuredClone(request);mutate(r);const b=Buffer.from(JSON.stringify(r));assert.throws(()=>checkRequest(b,sha256(b),reference,policy,context));});
test('reject altered request bytes',()=>assert.throws(()=>checkRequest(Buffer.from('{}'),'0'.repeat(64),reference,policy,context)));
// Minimal stored-ZIP builder is a test fixture, not a release packer.
function zip(entries) {
  const blocks=[],central=[];let offset=0;
  const crc32=bytes=>{let c=0xffffffff;for(const b of bytes){c^=b;for(let i=0;i<8;i++)c=(c>>>1)^((c&1)?0xedb88320:0);}return (c^0xffffffff)>>>0;};
  for(const [name,value,attributes=0] of entries){const n=Buffer.from(name),b=Buffer.from(value),crc=crc32(b);const h=Buffer.alloc(30);h.writeUInt32LE(0x04034b50);h.writeUInt16LE(20,4);h.writeUInt32LE(crc,14);h.writeUInt32LE(b.length,18);h.writeUInt32LE(b.length,22);h.writeUInt16LE(n.length,26);blocks.push(h,n,b);const c=Buffer.alloc(46);c.writeUInt32LE(0x02014b50);c.writeUInt16LE(0x0314,4);c.writeUInt16LE(20,6);c.writeUInt32LE(crc,16);c.writeUInt32LE(b.length,20);c.writeUInt32LE(b.length,24);c.writeUInt16LE(n.length,28);c.writeUInt32LE(attributes>>>0,38);c.writeUInt32LE(offset,42);central.push(c,n);offset+=h.length+n.length+b.length;}
  const c=Buffer.concat(central),end=Buffer.alloc(22);end.writeUInt32LE(0x06054b50);end.writeUInt16LE(entries.length,8);end.writeUInt16LE(entries.length,10);end.writeUInt32LE(c.length,12);end.writeUInt32LE(offset,16);return Buffer.concat([...blocks,c,end]);
}
test('bounded ZIP reader returns files without extraction',async()=>assert.equal((await readZip(zip([['a','hello']]))).get('a').toString(),'hello'));
for (const name of ['../escape','/absolute','a\\b','C:drive','a//b','a/./b','a/../b','trail.','trail ','folder/']) test(`reject unsafe archive name ${JSON.stringify(name)}`,()=>assert.throws(()=>safeEntry(name)));
test('reject duplicate case-colliding ZIP entries',async()=>assert.rejects(readZip(zip([['a','x'],['A','y']]))));
test('reject symlink ZIP entries',async()=>assert.rejects(readZip(zip([['link','target',0xa1ff0000]]))));
test('reject archive expansion over budget',async()=>assert.rejects(readZip(zip([['a','hello']]),{maxTotal:4})));
test('reject archive input over budget',async()=>assert.rejects(readZip(zip([['a','hello']]),{maxInput:4})));
test('reject non-ZIP bytes',async()=>assert.rejects(readZip(Buffer.from('not zip'))));
const artifact={id:42,name:'archsift-0.11.0-reviewed-submissions',expired:false,size_in_bytes:123,digest:`sha256:${'a'.repeat(64)}`,workflow_run:{id:100,head_sha:context.sha,head_repository_id:policy.repositoryId}};
test('artifact must be same-run, same-repository and digest-bearing',()=>checkArtifact(artifact,{...context,runId:'100'}));
for(const [key,value] of [['expired',true],['digest',''],['size_in_bytes',20_000_000],['name','other'],['workflow_run',{...artifact.workflow_run,id:101}],['workflow_run',{...artifact.workflow_run,head_repository_id:1}]])test(`reject artifact ${key} ${JSON.stringify(value)}`,()=>assert.throws(()=>checkArtifact({...artifact,[key]:value},{...context,runId:'100'})));
function syntheticPackages({signed=false,tamper=false,extra=false}={}){
 const r=structuredClone(reference), files=new Map();const m={schemaVersion:1,version:r.version,sourceCommit:r.sourceCommit,packages:[]};
 for(const p of r.packages){const nuspec=Buffer.from(`<package><metadata><id>${p.id}</id><version>0.11.0</version><repository commit="${r.sourceCommit}" /></metadata></package>`);const payload=[[`${p.id}.nuspec`,nuspec],['file.dll',Buffer.from('synthetic payload')]];const original=zip(payload);p.bytes=original.length;p.sha256=sha256(original);m.packages.push({id:p.id,submittedSha256:p.sha256,payload:payload.map(([path,b])=>({path,length:b.length,sha256:sha256(b)}))});const actual=payload.map(([n,b])=>[n,Buffer.from(b)]);if(tamper)actual[1][1][0]^=1;if(signed)actual.push(['.signature.p7s','synthetic signature is not a real certificate']);if(extra)actual.push(['unapproved.txt','bad']);files.set(p.file,zip(actual));}
 return {r,files,m};
}
test('verify all synthetic unsigned package bytes and payload entries',async()=>{const {r,files,m}=syntheticPackages();await checkPackages(files,r,m);});
test('signed archive may differ only by signature/ZIP representation',async()=>{const {r,files,m}=syntheticPackages({signed:true});await checkPackages(files,r,m,{served:true});assert.notEqual(sha256(files.get(r.packages[0].file)),r.packages[0].sha256);});
test('reject changed nonsignature signed payload',async()=>{const {r,files,m}=syntheticPackages({signed:true,tamper:true});await assert.rejects(checkPackages(files,r,m,{served:true}));});
test('reject extra signed payload entry',async()=>{const {r,files,m}=syntheticPackages({signed:true,extra:true});await assert.rejects(checkPackages(files,r,m,{served:true}));});
test('reject missing repository signature',async()=>{const {r,files,m}=syntheticPackages();await assert.rejects(checkPackages(files,r,m,{served:true}));});
test('Authors/profile links outside owner list are not ownership proof',()=>assert.throws(()=>checkOwnerPage('<div>Authors <a href="/profiles/XiaolongFeng">Author</a></div>','XiaolongFeng')));
test('gallery owner-list profile binding',()=>checkOwnerPage('<ul class="list-unstyled owner-list"><li><a class="username" href="/profiles/XiaolongFeng" title="owner">owner</a></li></ul>','XiaolongFeng'));
const proof={submissionSha256:'a'.repeat(64),servedSha256:'b'.repeat(64),contentHash:'abc=',signatureValid:true,ownerVerified:true,payloadVerified:true};
test('publisher order and nonsecret receipts are sequential',async()=>{const order=[],states=[];await publishSequential({reference,push:async p=>order.push(p.id),verifyServed:async()=>proof,record:async rows=>states.push(structuredClone(rows))});assert.deepEqual(order,reference.packages.map(p=>p.id));assert.ok(states.at(-1).every(p=>p.state==='verified-served'));});
for(const index of [0,2,4])test(`partial push failure at ${index} stops later packages and masks fake key`,async()=>{let count=0;const states=[];const key='FAKE-SENSITIVE-KEY';await assert.rejects(publishSequential({reference,push:async()=>{if(count++===index)throw new Error(key);},verifyServed:async()=>proof,record:async r=>states.push(structuredClone(r))}),e=>!e.message.includes(key));assert.equal(count,index+1);assert.equal(states.at(-1)[index].state,'outcome-unknown');assert.ok(states.at(-1).slice(index+1).every(r=>r.state==='not-attempted'));assert.ok(!JSON.stringify(states).includes(key));});
for(const field of ['signatureValid','ownerVerified','payloadVerified'])test(`failed served ${field} stops next push`,async()=>{let count=0;await assert.rejects(publishSequential({reference,push:async()=>count++,verifyServed:async()=>({...proof,[field]:false}),record:async()=>{}}));assert.equal(count,1);});
test('expired credential window does not refresh or start a push',async()=>{let count=0,t=0;await assert.rejects(publishSequential({reference,push:async()=>count++,verifyServed:async()=>proof,record:async()=>{},now:()=>t++?41*60_000:0}));assert.equal(count,0);});
for(const reason of ['HTTP 401','HTTP 403','HTTP 409','HTTP 500','transport timeout','cancellation','indexing validation timeout'])test(`stop on mocked ${reason} without a retry`,async()=>{let count=0;await assert.rejects(publishSequential({reference,push:async()=>{count++;throw new Error(reason);},verifyServed:async()=>proof,record:async()=>{}}));assert.equal(count,1);});
test('ambiguous duplicate gallery owner regions fail closed',()=>assert.throws(()=>checkOwnerPage('<ul class="owner-list"><a href="/profiles/XiaolongFeng">x</a></ul>'.repeat(2),'XiaolongFeng')));
test('artifact redirect does not leak GitHub token to storage',async()=>{const calls=[];const reader=new GitHubReader('FAKE-GH-TOKEN',async(url,options)=>{calls.push({url:String(url),options});if(calls.length===1)return new Response(null,{status:302,headers:{location:'https://example.blob.core.windows.net/x?sig=redacted'}});return new Response('zip');});assert.equal((await reader.artifact(1)).toString(),'zip');assert.ok(calls[0].options.headers.Authorization);assert.equal(calls[1].options.headers,undefined);});
test('artifact redirect to arbitrary host is rejected',async()=>{const reader=new GitHubReader('fake',async()=>new Response(null,{status:302,headers:{location:'https://evil.example/x'}}));await assert.rejects(reader.artifact(1));});
test('GitHub reader rejects mutation and arbitrary endpoint paths',async()=>{const reader=new GitHubReader('fake',async()=>{throw new Error('must not call');});await assert.rejects(reader.json('repos/other/repo/actions'));});
for(const mode of ['preflight','preview','publish'])test(`real driver ${mode} with publication locked and no staged identity never accesses token/push/asset endpoints`,()=>{
 const child={...process.env,GITHUB_ACTIONS:'true',RUNNER_ENVIRONMENT:'github-hosted',GITHUB_REPOSITORY:policy.repository,GITHUB_REPOSITORY_ID:String(policy.repositoryId),GITHUB_REPOSITORY_OWNER_ID:String(policy.ownerId),GITHUB_REF:'refs/heads/main',GITHUB_SHA:context.sha,GITHUB_RUN_ID:'100',GITHUB_RUN_ATTEMPT:'1',GITHUB_EVENT_NAME:'workflow_dispatch',GITHUB_WORKFLOW_REF:`${policy.repository}/${policy.workflow}@refs/heads/main`,RELEASE_MODE:mode,RELEASE_WORKFLOW_SHA:'',RELEASE_DATA_COMMIT:'',RELEASE_REQUEST_SHA256:'',RELEASE_SWITCH:'true',GH_TOKEN:'FAKE-GH-TOKEN',NUGET_API_KEY:'FAKE-SECRET-MUST-NOT-LEAK'};
 delete child.GITHUB_OUTPUT;delete child.GITHUB_STEP_SUMMARY;
 const result=spawnSync(process.execPath,['--import',pathToFileURL(path.join(root,'scripts/nuget-release/test-fetch.mjs')).href,path.join(root,'scripts/nuget-release/driver.mjs'),'prepare'],{cwd:root,env:child,encoding:'utf8',timeout:20000});
 assert.equal(result.status,mode==='preflight'?0:1,result.stderr);assert.ok(result.stdout.includes('SYNTHETIC-TRANSPORT-LOADED'));assert.ok(!`${result.stdout}${result.stderr}`.includes(child.NUGET_API_KEY));assert.ok(!`${result.stdout}${result.stderr}`.includes('FORBIDDEN-TRANSPORT'));
});

// TA04 fixtures exercise full preview bytes without a network/upload transport.
test('TA04 builds exactly seven public files from five originals and pinned metadata', async()=>{
 const {r,files,m}=syntheticPackages();const bytes=Buffer.from(JSON.stringify(m));files.set('package-payload-manifest.json',bytes);
 const p={...policy,payloadManifestSha256:sha256(bytes)};
 const result=await createPreviewData(files,r,p,context.sha);
 assert.equal(result.files.size,7);assert.equal(result.requestSha256,sha256(result.files.get('release-request.json')));
 assert.equal(result.files.get(r.packages[0].file),files.get(r.packages[0].file));
});
test('TA04 staging rejects a publication-enabled policy',()=>assert.throws(()=>assertPreviewOnly({...policy,publicationEnabled:true},'')));
test('TA04 staging rejects a true repository publication switch',()=>assert.throws(()=>assertPreviewOnly(policy,'true')));
test('TA04 staging rejects extra private evidence',async()=>{
 const {r,files,m}=syntheticPackages();const b=Buffer.from(JSON.stringify(m));files.set('package-payload-manifest.json',b);files.set('host-baseline.json',Buffer.from('private'));
 await assert.rejects(createPreviewData(files,r,{...policy,payloadManifestSha256:sha256(b)},context.sha));
});
test('TA04 staging rejects a changed original payload manifest',async()=>{
 const {r,files,m}=syntheticPackages();files.set('package-payload-manifest.json',Buffer.from(JSON.stringify(m)));
 await assert.rejects(createPreviewData(files,r,policy,context.sha));
});
async function previewFixture(){
 const {r,files,m}=syntheticPackages();const b=Buffer.from(JSON.stringify(m));files.set('package-payload-manifest.json',b);
 const p={...policy,payloadManifestSha256:sha256(b)};const prepared=await createPreviewData(files,r,p,context.sha);
 const raw=zip([...prepared.files]);const c={...publishing,runId:'100',requestSha256:prepared.requestSha256};
 const a={...artifact,size_in_bytes:raw.length,digest:`sha256:${sha256(raw)}`};
 return {r,p,c,a,raw,prepared};
}
test('complete same-run preview artifact is independently verified',async()=>{
 const f=await previewFixture();const files=await checkPreviewArtifact(f.raw,f.a,f.r,f.p,f.c,sha256(f.raw));assert.equal(files.size,7);
});
test('preview rejects a mismatched raw ZIP digest',async()=>{const f=await previewFixture();await assert.rejects(checkPreviewArtifact(f.raw,f.a,f.r,f.p,f.c,'0'.repeat(64)));});
test('preview rejects a mismatched API ZIP size',async()=>{const f=await previewFixture();await assert.rejects(checkPreviewArtifact(f.raw,{...f.a,size_in_bytes:f.raw.length+1},f.r,f.p,f.c,sha256(f.raw)));});
test('preview rejects another run even when file hashes match',async()=>{const f=await previewFixture();await assert.rejects(checkPreviewArtifact(f.raw,f.a,f.r,f.p,{...f.c,runId:'101'},sha256(f.raw)));});
test('preview rejects a changed approved request digest',async()=>{const f=await previewFixture();await assert.rejects(checkPreviewArtifact(f.raw,f.a,f.r,f.p,{...f.c,requestSha256:'0'.repeat(64)},sha256(f.raw)));});
test('preview rejects missing original package with a freshly hashed outer ZIP',async()=>{
 const f=await previewFixture();f.prepared.files.delete(f.r.packages[0].file);const raw=zip([...f.prepared.files]);
 await assert.rejects(checkPreviewArtifact(raw,{...f.a,size_in_bytes:raw.length,digest:`sha256:${sha256(raw)}`},f.r,f.p,f.c,sha256(raw)));
});
test('preview rejects tampered package despite a freshly hashed outer ZIP',async()=>{
 const f=await previewFixture();const b=Buffer.from(f.prepared.files.get(f.r.packages[0].file));b[0]^=1;f.prepared.files.set(f.r.packages[0].file,b);const raw=zip([...f.prepared.files]);
 await assert.rejects(checkPreviewArtifact(raw,{...f.a,size_in_bytes:raw.length,digest:`sha256:${sha256(raw)}`},f.r,f.p,f.c,sha256(raw)));
});
