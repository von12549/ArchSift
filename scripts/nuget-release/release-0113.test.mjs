import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { spawnSync } from 'node:child_process';
import { checkReleaseProfile } from './profile.mjs';
import { checkReference, checkQualification, checkContext, sha256 } from './model.mjs';
import { checkWorkflowForVersion } from './workflow-policy.mjs';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const actualProfile = JSON.parse(fs.readFileSync(path.join(root, 'docs/releases/0.11.3/publishing-activation.json')));
const draft = {...actualProfile,state:'AWAITING_FINAL_IDENTITIES',realAssetTransferEnabled:false,publicationEnabled:false,originalReferenceSha256:null,payloadManifestSha256:null};
const frozen = { ...draft, state: 'FINAL_IDENTITIES_FROZEN', originalReferenceSha256: 'a'.repeat(64), payloadManifestSha256: 'b'.repeat(64) };

test('0113 unbound draft rejects before reference or network access', () => assert.throws(() => checkReleaseProfile(draft, '0.11.3'), /identities not frozen/));
test('0113 actual profile binds reviewed evidence and still requires an exact operational switch',()=>{checkReleaseProfile(actualProfile,'0.11.3');assert.equal(typeof actualProfile.publicationEnabled,'boolean');const ref=JSON.parse(fs.readFileSync(path.join(root,actualProfile.originalReference)));checkQualification(fs.readFileSync(path.join(root,'docs/releases/0.11.3/qualification.json')),ref);for(const releaseSwitch of ['', 'false', 'True']) assert.throws(()=>checkContext({version:'0.11.3',repository:actualProfile.repository,repositoryId:String(actualProfile.repositoryId),ownerId:String(actualProfile.ownerId),ref:'refs/heads/main',sha:'a'.repeat(40),mainSha:'a'.repeat(40),runAttempt:'1',event:'workflow_dispatch',workflowSha:'a'.repeat(40),dataCommit:ref.sourceCommit,requestSha256:'b'.repeat(64),releaseSwitch},actualProfile,'publish'));});

test('0113 typed closed frozen profile can be checked without enabling publication', () => checkReleaseProfile(frozen, '0.11.3'));
for (const [name, change] of [
  ['wrong reference', p => { p.originalReference = 'docs/releases/0.11.2/trusted-publishing-reference.json'; }],
  ['old switch', p => { p.versionSwitch = 'ARCHSIFT_NUGET_PUBLISH_0112'; }],
  ['string transfer flag', p => { p.realAssetTransferEnabled = 'false'; }],
  ['string publication flag', p => { p.publicationEnabled = 'true'; }],
  ['missing source hash', p => { p.originalReferenceSha256 = null; }],
  ['missing payload hash', p => { p.payloadManifestSha256 = null; }],
]) test(`0113 rejects ${name}`, () => { const p = structuredClone(frozen); change(p); assert.throws(() => checkReleaseProfile(p, '0.11.3')); });
test('0113 code capabilities remain closed even with a true operational switch', () => assert.throws(() => checkContext({version:'0.11.3',repository:draft.repository,repositoryId:String(draft.repositoryId),ownerId:String(draft.ownerId),ref:'refs/heads/main',sha:'a'.repeat(40),mainSha:'a'.repeat(40),runAttempt:'1',event:'workflow_dispatch',workflowSha:'a'.repeat(40),dataCommit:'b'.repeat(40),requestSha256:'c'.repeat(64),releaseSwitch:'true'}, frozen, 'publish'), /transfer is locked/));

function fixture() {
  const reference = JSON.parse(fs.readFileSync(path.join(root, 'docs/releases/0.11.2/trusted-publishing-reference.json')));
  reference.version = '0.11.3'; reference.sourceDirty = false;
  reference.packages = reference.packages.map(p => ({...p, file:p.file.replace('0.11.2','0.11.3')}));
  reference.humanAcceptance = {status:'passed-user-confirmed', finalIdentityConfirmed:true};
  const qualification = {schemaVersion:1, version:'0.11.3', sourceCommit:reference.sourceCommit, sourceTree:reference.sourceTree, sourceDirty:false,
    packages:reference.packages, status:'ACCEPTED_0113_FINAL_SOURCE_AND_PACKAGE_SCOPE', finalPackageIdentityConfirmed:true,
    humanAcceptance:{status:'passed-user-confirmed', finalIdentityConfirmed:true, evidenceSha256:'1'.repeat(64)},
    evidence:['release-regression','package-integrity','consumer-lifecycle'].map(lane => ({lane,status:'passed',reviewSha256:'2'.repeat(64)}))};
  return {reference,qualification};
}
function verify(f) {
  const bytes = Buffer.from(JSON.stringify(f.qualification));
  f.reference.acceptedPreview.acceptanceSha256 = sha256(bytes);
  checkQualification(bytes, f.reference);
}
test('0113 accepts only fully bound synthetic final-scope qualification', () => verify(fixture()));
for (const [name, mutate] of [
  ['dirty reference', f => { f.reference.sourceDirty = true; }],
  ['missing clean reference', f => { delete f.reference.sourceDirty; }],
  ['prior release waiver', f => { f.reference.humanAcceptance.status = 'explicit-user-approved-repeat-waiver'; }],
  ['dirty qualified source', f => { f.qualification.sourceDirty = true; }],
  ['unconfirmed package bytes', f => { f.qualification.finalPackageIdentityConfirmed = false; }],
  ['missing human evidence', f => { delete f.qualification.humanAcceptance.evidenceSha256; }],
  ['old waiver object', f => { f.qualification.repeatWaiver = {}; }],
  ['missing lifecycle', f => { f.qualification.evidence.pop(); }],
  ['duplicate lane', f => { f.qualification.evidence.push(f.qualification.evidence[0]); }],
  ['failed regression', f => { f.qualification.evidence[0].status = 'failed'; }],
  ['unknown evidence hash', f => { f.qualification.evidence[1].reviewSha256 = null; }],
  ['wrong source', f => { f.qualification.sourceCommit = 'f'.repeat(40); }],
  ['wrong version', f => { f.qualification.version = '0.11.2'; }],
]) test(`0113 qualification rejects ${name}`, () => { const f=fixture(); mutate(f); assert.throws(() => verify(f)); });
test('0113 reference cannot omit final-byte human confirmation', () => { const {reference}=fixture(); reference.humanAcceptance.finalIdentityConfirmed=false; assert.throws(() => checkReference(reference)); });

function delegatedFixture() {
  const f=fixture();
  const h={status:'skipped-explicit-user-delegation',performed:false,finalIdentityConfirmed:false};
  const a={status:'passed-agent-delegated',performedBy:'Codex',finalIdentityConfirmed:true,
    sourceCommit:f.reference.sourceCommit,sourceTree:f.reference.sourceTree,
    packageSetSha256:sha256(Buffer.from(JSON.stringify(f.reference.packages))),
    authorizationSha256:'3'.repeat(64),evidenceSha256:'4'.repeat(64)};
  for(const x of [f.reference,f.qualification]) {x.humanAcceptance=structuredClone(h);x.agentUiAcceptance=structuredClone(a);}
  return f;
}
test('0113 accepts explicit source/package-bound agent UI delegation without claiming human execution',()=>verify(delegatedFixture()));
for(const [name,mutate] of [
  ['missing authorization',f=>{delete f.reference.agentUiAcceptance.authorizationSha256;}],
  ['missing UI evidence',f=>{delete f.qualification.agentUiAcceptance.evidenceSha256;}],
  ['different evidence',f=>{f.qualification.agentUiAcceptance.evidenceSha256='5'.repeat(64);}],
  ['different authorization',f=>{f.qualification.agentUiAcceptance.authorizationSha256='5'.repeat(64);}],
  ['prior source',f=>{f.reference.agentUiAcceptance.sourceCommit='f'.repeat(40);}],
  ['prior tree',f=>{f.reference.agentUiAcceptance.sourceTree='f'.repeat(40);}],
  ['different package set',f=>{f.reference.agentUiAcceptance.packageSetSha256='f'.repeat(64);}],
  ['unconfirmed final bytes',f=>{f.reference.agentUiAcceptance.finalIdentityConfirmed=false;}],
  ['human execution claim',f=>{f.reference.humanAcceptance.performed=true;}],
  ['human confirmation claim',f=>{f.reference.humanAcceptance.finalIdentityConfirmed=true;}],
  ['human pass disguise',f=>{f.reference.humanAcceptance.status='passed-user-confirmed';f.reference.humanAcceptance.finalIdentityConfirmed=true;}],
  ['failed agent review',f=>{f.reference.agentUiAcceptance.status='failed';}],
  ['unknown reviewer',f=>{f.reference.agentUiAcceptance.performedBy='unknown';}],
  ['missing agent evidence object',f=>{delete f.reference.agentUiAcceptance;}],
  ['earlier release',f=>{f.reference.version='0.11.2';}],
]) test(`0113 delegated review rejects ${name}`,()=>{const f=delegatedFixture();mutate(f);assert.throws(()=>verify(f));});

const proposed=fs.readFileSync(path.join(root,'docs/releases/0.11.3/publish-nuget.proposed.yml'),'utf8');
test('0113 proposed workflow retains strict approval/audit/integrity structure', () => checkWorkflowForVersion(proposed,'0.11.3'));
test('active workflow selects a reviewed version without changing the historical 0113 proposal', () => {const text=fs.readFileSync(path.join(root,'.github/workflows/publish-nuget.yml'),'utf8');checkWorkflowForVersion(text,text.includes("RELEASE_VERSION: '0.11.4'")?'0.11.4':'0.11.3');});
for (const [name, change] of [
  ['old switch', s=>s.replace('PUBLISH_0113','PUBLISH_0112')],
  ['old artifact', s=>s.replace('archsift-0.11.3-reviewed-submissions','archsift-0.11.2-reviewed-submissions')],
  ['different approval environment', s=>s.replace('environment: nuget-release','environment: other')],
  ['cancel in progress', s=>s.replace('cancel-in-progress: false','cancel-in-progress: true')],
]) test(`0113 proposed workflow rejects ${name}`, () => assert.throws(() => checkWorkflowForVersion(change(proposed),'0.11.3')));
