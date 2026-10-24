import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { checkReference, checkContext, checkEnvironment, checkCiRun, checkRequest, checkPackages, checkArtifact, checkOwnerPage, readZip, publishSequential, requireThat, sha256 } from './model.mjs';
import { GitHubReader, boundedResponse } from './github.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const policy = JSON.parse(fs.readFileSync(path.join(root, 'docs/releases/0.11.0/publishing-activation.json')));
requireThat(policy.originalReference === 'docs/releases/0.11.0/trusted-publishing-reference.json', 'Original reference path differs');
const originalReferenceBytes = fs.readFileSync(path.join(root, policy.originalReference));
requireThat(sha256(originalReferenceBytes) === policy.originalReferenceSha256, 'Immutable original reference changed');
const reference = JSON.parse(originalReferenceBytes);
const e = process.env;
const base = 'repos/von12549/ArchSift/';
const gh = new GitHubReader(e.GH_TOKEN);
const c = { repository: e.GITHUB_REPOSITORY, repositoryId: e.GITHUB_REPOSITORY_ID, ownerId: e.GITHUB_REPOSITORY_OWNER_ID, ref: e.GITHUB_REF, sha: e.GITHUB_SHA, runId: e.GITHUB_RUN_ID, runAttempt: e.GITHUB_RUN_ATTEMPT, event: e.GITHUB_EVENT_NAME, workflowSha: e.RELEASE_WORKFLOW_SHA ?? '', dataCommit: e.RELEASE_DATA_COMMIT ?? '', requestSha256: e.RELEASE_REQUEST_SHA256 ?? '', releaseSwitch: e.RELEASE_SWITCH ?? '' };
const mode = e.RELEASE_MODE || 'preflight';
function output(key, value) {
  requireThat(/^[a-z_]+$/.test(key) && /^[a-zA-Z0-9:_./-]*$/.test(String(value)), 'Unsafe workflow output');
  if (e.GITHUB_OUTPUT) fs.appendFileSync(e.GITHUB_OUTPUT, `${key}=${value}\n`);
}
function summary(value) { if (e.GITHUB_STEP_SUMMARY) fs.appendFileSync(e.GITHUB_STEP_SUMMARY, `${JSON.stringify(value, null, 2)}\n`); }
function ownedScratch(name) {
  requireThat(e.RUNNER_TEMP && path.isAbsolute(e.RUNNER_TEMP) && /^[a-z0-9-]+$/.test(name), 'Runner scratch required');
  const destination = path.join(e.RUNNER_TEMP, name);
  fs.mkdirSync(destination); return destination;
}
async function validateContext() {
  requireThat(e.GITHUB_ACTIONS === 'true' && e.RUNNER_ENVIRONMENT === 'github-hosted' && e.GITHUB_WORKFLOW_REF === `${policy.repository}/${policy.workflow}@refs/heads/main`, 'Hosted main workflow only');
  checkReference(reference);
  c.mainSha = (await gh.json(`${base}commits/main`)).sha;
  checkContext(c, policy, mode);
  const source = await gh.json(`${base}git/commits/${reference.sourceCommit}`);
  requireThat(source.sha === reference.sourceCommit && source.tree.sha === reference.sourceTree, 'Package source tree differs');
  for (const run of reference.sourceCi) {
    checkCiRun(await gh.json(`${base}actions/runs/${run.runId}`), { path: '.github/workflows/verify.yml', sha: run.headSha, event: run.event });
  }
  const merged = (await gh.json(`${base}commits/${c.sha}/pulls?per_page=100`)).filter(p => p.state === 'closed' && p.merged_at && p.merge_commit_sha === c.sha && p.base?.ref === 'main' && p.base?.repo?.full_name === policy.repository && p.head?.repo?.full_name === policy.repository);
  requireThat(merged.length === 1 && /^[a-f0-9]{40}$/.test(merged[0].head.sha), 'Exact reviewed merged PR identity missing');
  for (const workflow of ['verify.yml', 'trusted-publishing-checks.yml', 'session-baseline-checks.yml']) {
    for (const [sha, event] of [[c.sha, 'push'], [merged[0].head.sha, 'pull_request']]) {
      const runs = await gh.json(`${base}actions/workflows/${workflow}/runs?head_sha=${sha}&event=${event}&per_page=100`);
      const candidates = runs.workflow_runs.filter(r => r.head_sha === sha && r.event === event);
      requireThat(candidates.length > 0, 'Exact PR-head/actual-main CI missing');
      candidates.sort((a, b) => b.id - a.id);
      checkCiRun(candidates[0], { path: `.github/workflows/${workflow}`, sha, event });
    }
  }
  checkEnvironment(await gh.json(`${base}environments/${policy.environment}`), await gh.json(`${base}environments/${policy.environment}/deployment-branch-policies`), policy);
}
async function stagedInputs() {
  const tree = await gh.json(`${base}git/trees/${c.dataCommit}?recursive=1`);
  const names = [...reference.packages.map(p => p.file), 'release-request.json', 'package-payload-manifest.json'].sort();
  requireThat(tree.truncated === false && tree.tree.length === names.length && tree.tree.every(f => f.type === 'blob' && f.mode === '100644') && JSON.stringify(tree.tree.map(f => f.path).sort()) === JSON.stringify(names), 'Data-only tree allowlist differs');
  const files = new Map();
  for (const f of tree.tree) files.set(f.path, await gh.blob(f.sha));
  checkRequest(files.get('release-request.json'), c.requestSha256, reference, policy, c);
  requireThat(sha256(files.get('package-payload-manifest.json')) === policy.payloadManifestSha256, 'Payload manifest hash differs');
  await checkPackages(files, reference, JSON.parse(files.get('package-payload-manifest.json')));
  return files;
}
async function artifactInputs() {
  const id = Number(e.RELEASE_ARTIFACT_ID);
  const metadata = await gh.json(`${base}actions/artifacts/${id}`);
  checkArtifact(metadata, c);
  requireThat(metadata.digest === `sha256:${e.RELEASE_ARTIFACT_SHA256}`, 'Approval artifact digest differs');
  const raw = await gh.artifact(id);
  requireThat(sha256(raw) === e.RELEASE_ARTIFACT_SHA256, 'Raw artifact ZIP digest differs');
  const files = await readZip(raw, { maxTotal: 4_000_000, maxEntries: 7 });
  const allowed = [...reference.packages.map(p => p.file), 'release-request.json', 'package-payload-manifest.json'].sort();
  requireThat(JSON.stringify([...files.keys()].sort()) === JSON.stringify(allowed), 'Artifact file allowlist differs');
  checkRequest(files.get('release-request.json'), c.requestSha256, reference, policy, c);
  requireThat(sha256(files.get('package-payload-manifest.json')) === policy.payloadManifestSha256, 'Payload manifest differs');
  await checkPackages(files, reference, JSON.parse(files.get('package-payload-manifest.json')));
  return files;
}
async function registryMissing() {
  for (const p of reference.packages) {
    const response = await fetch(`https://api.nuget.org/v3-flatcontainer/${p.id.toLowerCase()}/0.11.0/${p.file.toLowerCase()}`, { method: 'HEAD', redirect: 'error', signal: AbortSignal.timeout(30_000) });
    requireThat(response.status === 404, 'Existing/uncertain server version: stop before login');
  }
}
function invokeDotnet(operation, file, key = '') {
  const child = { ...e }; delete child.GH_TOKEN; delete child.NUGET_API_KEY; delete child.ACTIONS_ID_TOKEN_REQUEST_TOKEN; delete child.ACTIONS_ID_TOKEN_REQUEST_URL;
  if (key) child.NUGET_API_KEY = key;
  const result = spawnSync('pwsh', ['-NoProfile', '-NonInteractive', '-File', path.join(root, 'scripts/Invoke-HostedNuGet.ps1'), '-Operation', operation, '-PackagePath', file], { cwd: root, env: child, encoding: 'utf8', timeout: 180_000, maxBuffer: 2_000_000, windowsHide: true });
  requireThat(result.status === 0 && !result.error, 'Hosted NuGet operation failed; output withheld for credential safety');
  return result.stdout;
}
async function servedProof(p, originalFiles, scratch) {
  const url = `https://api.nuget.org/v3-flatcontainer/${p.id.toLowerCase()}/0.11.0/${p.file.toLowerCase()}`;
  let bytes;
  for (let attempt = 0; attempt < 30; attempt++) {
    const response = await fetch(url, { redirect: 'error', signal: AbortSignal.timeout(30_000) });
    if (response.ok) { bytes = await boundedResponse(response); break; }
    requireThat(response.status === 404, 'Unexpected registry read response');
    if (attempt < 29) await new Promise(resolve => setTimeout(resolve, 30_000));
  }
  requireThat(bytes, 'Registry validation/indexing timed out');
  // The other four originals stay frozen; this verifies every entry of this served package.
  const manifest = JSON.parse(originalFiles.get('package-payload-manifest.json'));
  const entries = await readZip(bytes);
  const payload = manifest.packages.find(x => x.id === p.id).payload;
  requireThat(entries.size === payload.length + 1 && entries.has('.signature.p7s'), 'Unexpected signed archive entries');
  for (const entry of payload) requireThat(entries.has(entry.path) && entries.get(entry.path).length === entry.length && sha256(entries.get(entry.path)) === entry.sha256, 'Served nonsignature bytes differ');
  const file = path.join(scratch, `served-${p.file}`); fs.writeFileSync(file, bytes, { flag: 'wx' });
  const signature = invokeDotnet('Verify', file);
  requireThat(/Signature type:\s*Repository/i.test(signature) && /https:\/\/api\.nuget\.org\/v3\/index\.json/.test(signature), 'Repository signature identity not proven');
  const page = await boundedResponse(await fetch(`https://www.nuget.org/packages/${p.id}/0.11.0`, { redirect: 'error', signal: AbortSignal.timeout(30_000) }), 2_000_000);
  checkOwnerPage(page.toString('utf8'), policy.nugetUser);
  const contentHash = signature.match(/(?:Package content hash|Content hash):\s*(\S+)/i)?.[1];
  requireThat(contentHash && /^[a-zA-Z0-9+/=]+$/.test(contentHash), 'NuGet content hash missing');
  return { submissionSha256: p.sha256, servedSha256: sha256(bytes), contentHash, signatureValid: true, ownerVerified: true, payloadVerified: true };
}
async function main() {
  const command = process.argv[2];
  requireThat(['prepare', 'validate-artifact', 'publish', 'verify-public'].includes(command), 'Unknown release command');
  // This check precedes all archive transfer, credential use and child execution.
  await validateContext();
  const identity = { version: reference.version, packageSource: reference.sourceCommit, workflowCommit: c.sha, mode, publicationEnabled: policy.publicationEnabled, localGuardDotnet: 'stopped' };
  if (command === 'prepare') {
    output('publish_ready', 'false'); output('has_inputs', 'false');
    if (mode === 'preflight') {
      summary({ ...identity, status: 'passed-closed-preflight', packageBytesVerified: false, credentialsAcquired: false, publicPublication: false });
      console.log('Closed preflight passed: original source/main CI and protected environment verified; no package transfer or credentials.'); return;
    }
    const files = await stagedInputs(); const directory = ownedScratch('archsift-release-inputs');
    for (const [name, bytes] of files) fs.writeFileSync(path.join(directory, name), bytes, { flag: 'wx' });
    output('has_inputs', 'true'); output('publish_ready', mode === 'publish' ? 'true' : 'false');
    summary({ ...identity, requestSha256: c.requestSha256, dataCommit: c.dataCommit, packages: reference.packages, packageBytesVerified: true, credentialsAcquired: false });
    return;
  }
  requireThat(mode === 'publish', 'Only an approved publishing transaction may enter this job');
  const files = await artifactInputs();
  if (command === 'verify-public') {
    requireThat(!e.NUGET_API_KEY && !e.ACTIONS_ID_TOKEN_REQUEST_TOKEN, 'Public verification must not receive publishing credentials/OIDC');
    const scratch = ownedScratch('archsift-public-verification'); const proofs = [];
    for (const p of reference.packages) proofs.push({ id: p.id, ...await servedProof(p, files, scratch) });
    summary({ ...identity, status: 'public-signature-owner-payload-verified', proofs, consumerAcceptance: 'separate-human-operated-NR06-checks-required' }); return;
  }
  await registryMissing();
  if (command === 'validate-artifact') {
    summary({ ...identity, artifactId: Number(e.RELEASE_ARTIFACT_ID), artifactSha256: e.RELEASE_ARTIFACT_SHA256, requestSha256: c.requestSha256, packages: reference.packages, status: 'reverified-before-login', credentialsAcquired: false }); return;
  }
  requireThat(e.NUGET_API_KEY && e.NUGET_API_KEY.length > 10, 'Temporary publishing credential missing');
  const key = e.NUGET_API_KEY; delete e.NUGET_API_KEY;
  const scratch = ownedScratch('archsift-publisher');
  for (const p of reference.packages) fs.writeFileSync(path.join(scratch, p.file), files.get(p.file), { flag: 'wx' });
  await publishSequential({ reference,
    push: async p => { invokeDotnet('Push', path.join(scratch, p.file), key); },
    verifyServed: async p => servedProof(p, files, scratch),
    record: async rows => { fs.writeFileSync(path.join(scratch, 'transaction.json'), `${JSON.stringify({ source: reference.sourceCommit, workflow: c.sha, runId: c.runId, artifactId: Number(e.RELEASE_ARTIFACT_ID), rows }, null, 2)}\n`); summary({ transaction: rows }); }
  });
}
main().catch(() => { console.error('Release safety gate stopped. No automatic retry or repair; preserve the nonsecret summary and reconcile the transaction.'); process.exitCode = 1; });
