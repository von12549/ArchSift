import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { checkReleaseProfile } from './profile.mjs';
import { checkReference, checkQualification, checkContext, sha256 } from './model.mjs';
import { checkWorkflowForVersion } from './workflow-policy.mjs';
import { checkCandidateProvenance } from './candidate-inputs.mjs';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const actual = JSON.parse(fs.readFileSync(path.join(root, 'docs/releases/0.11.4/publishing-activation.json')));
const draft = {...actual,state:'AWAITING_MAIN_SOURCE_AND_FINAL_IDENTITIES',realAssetTransferEnabled:false,publicationEnabled:false,candidateArtifact:null,originalReferenceSha256:null,payloadManifestSha256:null};
const frozen = {...draft, state:'FINAL_IDENTITIES_FROZEN', originalReferenceSha256:'a'.repeat(64), payloadManifestSha256:'b'.repeat(64), candidateArtifact:{id:1,runId:2,sourceCommit:'c'.repeat(40),sha256:'d'.repeat(64),files:['packages/synthetic.nupkg']}};
test('0114 draft fixture remains closed before reading absent final identities', () => {
  assert.equal(draft.realAssetTransferEnabled, false); assert.equal(draft.publicationEnabled, false);
  assert.equal(draft.candidateArtifact, null); assert.equal(draft.originalReferenceSha256, null);
  assert.throws(() => checkReleaseProfile(draft, '0.11.4'), /identities not frozen/);
});
test('0114 fully typed synthetic freeze can be validated without opening capabilities', () => checkReleaseProfile(frozen, '0.11.4'));
for (const [name, change] of [
  ['prior version switch', p => {p.versionSwitch='ARCHSIFT_NUGET_PUBLISH_0113';}],
  ['wrong reference', p => {p.originalReference='docs/releases/0.11.3/trusted-publishing-reference.json';}],
  ['string publication flag', p => {p.publicationEnabled='false';}],
  ['string transfer flag', p => {p.realAssetTransferEnabled='false';}],
  ['missing source hash', p => {p.originalReferenceSha256=null;}],
  ['missing payload hash', p => {p.payloadManifestSha256=null;}],
  ['missing hosted candidate', p => {p.candidateArtifact=null;}],
]) test(`0114 frozen profile rejects ${name}`, () => {const p=structuredClone(frozen);change(p);assert.throws(()=>checkReleaseProfile(p,'0.11.4'));});
test('0114 unknown next version is rejected',()=>assert.throws(()=>checkReleaseProfile(frozen,'0.11.5')));
for(const mode of ['preview','publish']) test(`0114 operational true cannot open ${mode} on a closed frozen profile`,()=>{
  assert.throws(()=>checkContext({version:'0.11.4',repository:draft.repository,repositoryId:String(draft.repositoryId),ownerId:String(draft.ownerId),ref:'refs/heads/main',sha:'a'.repeat(40),mainSha:'a'.repeat(40),runAttempt:'1',event:'workflow_dispatch',workflowSha:'a'.repeat(40),dataCommit:'b'.repeat(40),requestSha256:'c'.repeat(64),releaseSwitch:'true'},frozen,mode),/transfer is locked/);
});
for(const mode of ['preflight','preview','publish']) test(`0114 unhosted ${mode} driver stops before network access`,()=>{
  const denyNetwork='data:text/javascript,'+encodeURIComponent('let calls=0;globalThis.fetch=()=>{calls++;throw new Error("UNEXPECTED_NETWORK")};process.on("exit",()=>{if(calls)process.exitCode=97;});');
  const r=spawnSync(process.execPath,['--import',denyNetwork,path.join(root,'scripts/nuget-release/driver.mjs'),'prepare'],{cwd:root,env:{...process.env,NODE_OPTIONS:'',GITHUB_ACTIONS:'false',RUNNER_ENVIRONMENT:'local-fixture',NUGET_API_KEY:'',ACTIONS_ID_TOKEN_REQUEST_TOKEN:'',RELEASE_VERSION:'0.11.4',RELEASE_MODE:mode,RELEASE_SWITCH:'true',GH_TOKEN:'synthetic-never-use'},encoding:'utf8',timeout:10000});
  assert.equal(r.status,1);assert.match(r.stderr,actual.state==='FINAL_IDENTITIES_FROZEN'?/Release safety gate stopped/:/identities not frozen/);assert.doesNotMatch(r.stderr,/UNEXPECTED_NETWORK|ENOENT/);
});
function fixture(){
  const r=JSON.parse(fs.readFileSync(path.join(root,'docs/releases/0.11.3/trusted-publishing-reference.json')));
  r.version='0.11.4';r.sourceDirty=false;r.packages=r.packages.map(p=>({...p,file:p.file.replace('0.11.3','0.11.4')}));
  delete r.agentUiAcceptance;r.humanAcceptance={status:'passed-user-confirmed',finalIdentityConfirmed:true};
  const q={schemaVersion:1,version:r.version,sourceCommit:r.sourceCommit,sourceTree:r.sourceTree,sourceDirty:false,packages:r.packages,status:'ACCEPTED_0114_FINAL_SOURCE_AND_PACKAGE_SCOPE',finalPackageIdentityConfirmed:true,humanAcceptance:{status:'passed-user-confirmed',finalIdentityConfirmed:true,evidenceSha256:'1'.repeat(64)},evidence:['release-regression','package-integrity','consumer-lifecycle','ifx-sdk-matrix','ifx-independent-audit','secondary-machine'].map(lane=>({lane,status:'passed',reviewSha256:'2'.repeat(64),sourceCommit:r.sourceCommit,packageSetSha256:sha256(Buffer.from(JSON.stringify(r.packages)))}))};
  return {r,q};
}
function verify(f){const bytes=Buffer.from(JSON.stringify(f.q));f.r.acceptedPreview.acceptanceSha256=sha256(bytes);return checkQualification(bytes,f.r);}
test('0114 complete synthetic final-source/package qualification passes',()=>verify(fixture()));
for(const lane of ['release-regression','package-integrity','consumer-lifecycle','ifx-sdk-matrix','ifx-independent-audit','secondary-machine']){
  test(`0114 qualification rejects missing ${lane}`,()=>{const f=fixture();f.q.evidence=f.q.evidence.filter(e=>e.lane!==lane);assert.throws(()=>verify(f));});
  test(`0114 qualification rejects old package binding in ${lane}`,()=>{const f=fixture();f.q.evidence.find(e=>e.lane===lane).packageSetSha256='f'.repeat(64);assert.throws(()=>verify(f));});
  test(`0114 qualification rejects different source in ${lane}`,()=>{const f=fixture();f.q.evidence.find(e=>e.lane===lane).sourceCommit='f'.repeat(40);assert.throws(()=>verify(f));});
}
for(const [name,change] of [
  ['dirty source',f=>{f.q.sourceDirty=true;}],
  ['PR replacing main CI',f=>{f.r.sourceCi[1].event='pull_request';}],
  ['duplicate CI identity',f=>{f.r.sourceCi[1].runId=f.r.sourceCi[0].runId;}],
  ['unconfirmed bytes',f=>{f.q.finalPackageIdentityConfirmed=false;}],
  ['prior acceptance status',f=>{f.q.status='ACCEPTED_0113_FINAL_SOURCE_AND_PACKAGE_SCOPE';}],
  ['prior release waiver',f=>{f.q.repeatWaiver={};}],
  ['duplicated review lane',f=>{f.q.evidence.push({...f.q.evidence[0]});}],
  ['failed review',f=>{f.q.evidence[0].status='failed';}],
]) test(`0114 qualification rejects ${name}`,()=>{const f=fixture();change(f);assert.throws(()=>verify(f));});
function delegatedFixture(){const f=fixture();const h={status:'skipped-explicit-user-delegation',performed:false,finalIdentityConfirmed:false};const a={status:'passed-agent-delegated',performedBy:'Codex',finalIdentityConfirmed:true,sourceCommit:f.r.sourceCommit,sourceTree:f.r.sourceTree,packageSetSha256:sha256(Buffer.from(JSON.stringify(f.r.packages))),authorizationSha256:'3'.repeat(64),evidenceSha256:'4'.repeat(64)};for(const x of [f.r,f.q]){x.humanAcceptance=structuredClone(h);x.agentUiAcceptance=structuredClone(a);}return f;}
test('0114 explicit Agent UI delegation remains separate from human execution',()=>verify(delegatedFixture()));
for(const [name,change] of [
  ['missing authorization',f=>{delete f.r.agentUiAcceptance.authorizationSha256;}],
  ['different final bytes',f=>{f.r.agentUiAcceptance.packageSetSha256='f'.repeat(64);}],
  ['human execution claim',f=>{f.r.humanAcceptance.performed=true;}],
  ['mismatched evidence',f=>{f.q.agentUiAcceptance.evidenceSha256='f'.repeat(64);}],
]) test(`0114 delegated qualification rejects ${name}`,()=>{const f=delegatedFixture();change(f);assert.throws(()=>verify(f));});
const proposed=fs.readFileSync(path.join(root,'docs/releases/0.11.4/publish-nuget.proposed.yml'),'utf8');

test('0114 frozen evidence graph preserves reviewed bytes across checkout platforms',()=>{
  if(actual.state!=='FINAL_IDENTITIES_FROZEN')return;
  const read=p=>fs.readFileSync(path.join(root,p));
  const r=JSON.parse(read(actual.originalReference));
  const q=JSON.parse(read('docs/releases/0.11.4/qualification.json'));
  for(const e of q.evidence){
    const bytes=read(e.review);assert.equal(sha256(bytes),e.reviewSha256);
    const review=JSON.parse(bytes);assert.equal(review.sourceCommit,r.sourceCommit);
    assert.equal(review.packageSetSha256,sha256(Buffer.from(JSON.stringify(r.packages))));
  }
  const base='docs/releases/0.11.4/evidence/';
  assert.equal(sha256(read(base+'agent-ui-authorization.json')),r.agentUiAcceptance.authorizationSha256);
  assert.equal(sha256(read(base+'agent-ui-review.json')),r.agentUiAcceptance.evidenceSha256);
  const retained=JSON.parse(read(base+'secondary-machine-equivalence-review.json'));
  assert.equal(retained.freshSecondaryMachineExecutionClaimed,false);
  assert.equal(retained.canonicalArchivesExecutedOnSecondaryMachine,false);
  assert.equal(sha256(read(retained.originalReview)),retained.originalReviewSha256);
  const managedBytes=read(base+'managed-equivalence.json'),fullBytes=read(base+'full-package-equivalence.json');
  assert.equal(sha256(managedBytes),retained.managedReviewSha256);
  assert.equal(sha256(fullBytes),retained.equivalenceReviewSha256);
  const managed=JSON.parse(managedBytes),full=JSON.parse(fullBytes);
  assert.equal(full.managedProofSha256,sha256(managedBytes));
  assert.equal(managed.verifierSha256,sha256(read('scripts/Compare-ManagedReleaseImages.ps1')));
  assert.equal(full.verifierSha256,sha256(read('scripts/Compare-ReleasePackageContent.py')));
  assert.equal(managed.modules.length,13);
  assert.ok(managed.modules.every(m=>m.equal&&m.nativeResourcesEqual&&m.portableSymbolsMatch));
  assert.equal(managed.negativeIlControl,true);assert.equal(managed.negativeMetadataControl,true);
  assert.equal(full.negativeControls.length,4);
});
test('0114 proposed workflow retains the exact protected transaction',()=>checkWorkflowForVersion(proposed,'0.11.4'));
test('0114 active workflow matches the reviewed preparation state',()=>checkWorkflowForVersion(fs.readFileSync(path.join(root,'.github/workflows/publish-nuget.yml'),'utf8'),actual.state==='FINAL_IDENTITIES_FROZEN'?'0.11.4':'0.11.3'));
test('0114 actual profile binds final identities and requires the exact operational switch',()=>{
  assert.equal(typeof actual.publicationEnabled,'boolean');
  if(actual.state!=='FINAL_IDENTITIES_FROZEN'){assert.equal(actual.realAssetTransferEnabled,false);return;}
  checkReleaseProfile(actual,'0.11.4');assert.equal(actual.realAssetTransferEnabled,true);
  const bytes=fs.readFileSync(path.join(root,actual.originalReference));assert.equal(sha256(bytes),actual.originalReferenceSha256);
  const r=JSON.parse(bytes);checkQualification(fs.readFileSync(path.join(root,'docs/releases/0.11.4/qualification.json')),r);
  assert.equal(sha256(fs.readFileSync(path.join(root,'docs/releases/0.11.4/package-payload-manifest.json'))),actual.payloadManifestSha256);
  const context={version:'0.11.4',repository:actual.repository,repositoryId:String(actual.repositoryId),ownerId:String(actual.ownerId),ref:'refs/heads/main',sha:'a'.repeat(40),mainSha:'a'.repeat(40),runAttempt:'1',event:'workflow_dispatch',workflowSha:'a'.repeat(40),dataCommit:r.sourceCommit,requestSha256:'b'.repeat(64),releaseSwitch:'true'};
  assert.throws(()=>checkContext(context,{...actual,publicationEnabled:false},'publish'),/Publication remains closed/);
  for(const releaseSwitch of ['', 'false','True'])assert.throws(()=>checkContext({...context,releaseSwitch},actual,'publish'),/Publication remains closed/);
  if(actual.publicationEnabled)assert.doesNotThrow(()=>checkContext(context,actual,'publish'));
});

test('0114 activated profile requires the explicit original-package publication authorization',()=>{
  if(!actual.publicationEnabled)return;
  const r=JSON.parse(fs.readFileSync(path.join(root,actual.originalReference)));
  const a=JSON.parse(fs.readFileSync(path.join(root,'docs/releases/0.11.4/publication-authorization.json')));
  assert.equal(a.status,'USER_AUTHORIZED_ORIGINAL_0114_PUBLICATION');assert.equal(a.version,'0.11.4');
  assert.equal(a.sourceCommit,r.sourceCommit);assert.equal(a.sourceTree,r.sourceTree);assert.deepEqual(a.packages,r.packages);
  assert.equal(a.originalReferenceSha256,actual.originalReferenceSha256);
  assert.equal(a.qualificationSha256,r.acceptedPreview.acceptanceSha256);assert.equal(a.payloadManifestSha256,actual.payloadManifestSha256);
  assert.equal(a.requiredHumanEnvironmentApproval,true);
  const trustBytes=fs.readFileSync(path.join(root,a.trustReview));assert.equal(sha256(trustBytes),a.trustReviewSha256);
  const trust=JSON.parse(trustBytes);assert.equal(trust.nugetAccountTrustReadback.status,'passed-fresh-authenticated-readback');
  assert.deepEqual(trust.packages,r.packages);assert.equal(trust.nugetAccountTrustReadback.policyModified,false);
});
for(const [name,change] of [
  ['old switch',s=>s.replaceAll('PUBLISH_0114','PUBLISH_0113')],
  ['old artifact',s=>s.replace('archsift-0.11.4-reviewed-submissions','archsift-0.11.3-reviewed-submissions')],
  ['other approval environment',s=>s.replace('environment: nuget-release','environment: other')],
  ['skipped audit',s=>s.replace(" && needs.audit.result == 'success'",'')],
]) test(`0114 proposed workflow rejects ${name}`,()=>assert.throws(()=>checkWorkflowForVersion(change(proposed),'0.11.4')));
function provenanceFixture(){const source='a'.repeat(40),digest='b'.repeat(64);const binding={id:3,runId:4,sourceCommit:source,sha256:digest};const meta={id:3,expired:false,name:`unpublished-recovery-candidate-${source}`,digest:`sha256:${digest}`,workflow_run:{id:4,head_sha:source,head_repository_id:1407069697}};const run={id:4,repository:{full_name:'von12549/ArchSift'},head_repository:{full_name:'von12549/ArchSift'},head_sha:source,head_branch:'main',path:'.github/workflows/recovery-candidate.yml',event:'push',run_attempt:1,status:'completed',conclusion:'success'};return{binding,meta,run,context:{dataCommit:source,repositoryId:'1407069697'}};}
test('0114 synthetic fresh main candidate provenance passes',()=>{const f=provenanceFixture();checkCandidateProvenance(f.meta,f.run,f.binding,f.context);});
test('0114 PR candidate cannot substitute for a fresh main artifact',()=>{const f=provenanceFixture();f.run.event='pull_request';f.run.head_branch='codex/archsift-0114-daily-portability';assert.throws(()=>checkCandidateProvenance(f.meta,f.run,f.binding,f.context));});
