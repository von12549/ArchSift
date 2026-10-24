import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { checkReference, checkRequest, checkPackages, checkEnvironment, checkCiRun, requireThat, sha256 } from './model.mjs';

export function checkStagingRepository(repo, policy) {
  requireThat(repo.id === policy.repositoryId && repo.owner?.id === policy.ownerId && repo.full_name === policy.repository, 'Public staging repository identity differs');
}
export function assertPreviewOnly(policy, releaseSwitch) {
  requireThat(policy.realAssetTransferEnabled === true && policy.publicationEnabled === false, 'Only separately authorized transfer with publication locked is allowed');
  requireThat(releaseSwitch === '' || releaseSwitch === 'false', 'Repository publication switch must remain closed');
}
export async function createPreviewData(files, reference, policy, workflowCommit) {
  assertPreviewOnly(policy, ''); checkReference(reference);
  requireThat(/^[a-f0-9]{40}$/.test(workflowCommit), 'Exact reviewed workflow commit required');
  const names = [...reference.packages.map(p => p.file), 'package-payload-manifest.json'].sort();
  requireThat(JSON.stringify([...files.keys()].sort()) === JSON.stringify(names), 'Only five originals and the pinned payload manifest may be input');
  const manifest = files.get('package-payload-manifest.json');
  requireThat(sha256(manifest) === policy.payloadManifestSha256, 'Original payload manifest differs');
  await checkPackages(files, reference, JSON.parse(manifest));
  const request = { schemaVersion: 1, version: reference.version, sourceCommit: reference.sourceCommit, sourceTree: reference.sourceTree, workflowCommit, repositoryId: policy.repositoryId, ownerId: policy.ownerId, packages: reference.packages, payloadManifestSha256: policy.payloadManifestSha256, acceptanceSha256: reference.acceptedPreview.acceptanceSha256 };
  const bytes = Buffer.from(JSON.stringify(request, null, 2) + '\n');
  const digest = sha256(bytes); checkRequest(bytes, digest, reference, policy, { sha: workflowCommit });
  return { files: new Map([...files, ['release-request.json', bytes]]), requestSha256: digest };
}
const gitBlobSha = bytes => createHash('sha1').update(Buffer.from(`blob ${bytes.length}\0`)).update(bytes).digest('hex');
async function main() {
  const { values: args } = parseArgs({ options: { assets: { type: 'string' }, evidence: { type: 'string' }, workflow: { type: 'string' }, branch: { type: 'string' }, 'authorize-public-data-upload': { type: 'boolean', default: false } } });
  requireThat(args['authorize-public-data-upload'] === true && process.env.GITHUB_ACTIONS !== 'true', 'Explicit authorized local Git-data upload only; never run from Actions');
  requireThat(path.isAbsolute(args.assets ?? '') && path.isAbsolute(args.evidence ?? '') && /^release-inputs\/archsift-0\.11\.0\/[a-z0-9-]+$/.test(args.branch ?? ''), 'Explicit external assets/evidence and unique data branch required');
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
  const evidence = path.resolve(args.evidence);
  for (const forbidden of [root, 'D:/IFX-10-Root/IFX-New', 'D:/ArchSift']) requireThat(!evidence.toLowerCase().startsWith(path.resolve(forbidden).toLowerCase() + path.sep) && evidence.toLowerCase() !== path.resolve(forbidden).toLowerCase(), 'Evidence belongs outside source roots');
  requireThat(!fs.existsSync(evidence), 'Use a new owned evidence directory; preserve previous attempts');
  const policy = JSON.parse(fs.readFileSync(path.join(root, 'docs/releases/0.11.0/publishing-activation.json')));
  const original = fs.readFileSync(path.join(root, policy.originalReference));
  requireThat(sha256(original) === policy.originalReferenceSha256, 'Immutable original reference differs');
  const reference = JSON.parse(original);
  assertPreviewOnly(policy, '');
  const git = (list) => execFileSync('git', ['-C', root, ...list], { encoding: 'utf8', windowsHide: true }).trim();
  requireThat(git(['rev-parse', 'HEAD']) === args.workflow && git(['status', '--porcelain']) === '', 'Expected clean, reviewed workflow checkout');
  const name = git(['config', '--get', 'user.name']), email = git(['config', '--get', 'user.email']);
  requireThat(name === 'Xiaolong Feng' && email === 'xiaolong@cxisoftware.com.au', 'Human primary author differs');
  const api = (endpoint, input) => {
    requireThat(endpoint === 'repos/von12549/ArchSift' || endpoint.startsWith('repos/von12549/ArchSift/'), 'Fixed repository only');
    const command = ['api', '--hostname', 'github.com', endpoint];
    if (input) command.push('--method', 'POST', '--input', input);
    try { return JSON.parse(execFileSync('gh', command, { encoding: 'utf8', timeout: 120_000, maxBuffer: 16_000_000, windowsHide: true })); }
    catch { throw new Error('GitHub operation failed; preserve ledger and reconcile before retry'); }
  };
  const base = 'repos/von12549/ArchSift/';
  checkStagingRepository(api(base.slice(0, -1)), policy);
  requireThat(api(base + 'commits/main').sha === args.workflow, 'Main moved before data transfer');
  const source = api(base + 'git/commits/' + reference.sourceCommit);
  requireThat(source.sha === reference.sourceCommit && source.tree.sha === reference.sourceTree, 'Original source tree differs');
  for (const ci of reference.sourceCi) checkCiRun(api(base + 'actions/runs/' + ci.runId), { path: '.github/workflows/verify.yml', sha: ci.headSha, event: ci.event });
  const variable = api(base + 'actions/variables').variables.find(v => v.name === policy.versionSwitch);
  assertPreviewOnly(policy, variable?.value ?? '');
  checkEnvironment(api(base + 'environments/nuget-release'), api(base + 'environments/nuget-release/deployment-branch-policies'), policy);
  const merged = api(base + `commits/${args.workflow}/pulls?per_page=100`).filter(p => p.state === 'closed' && p.merged_at && p.merge_commit_sha === args.workflow && p.base?.ref === 'main' && p.base?.repo?.full_name === policy.repository && p.head?.repo?.full_name === policy.repository);
  requireThat(merged.length === 1 && /^[a-f0-9]{40}$/.test(merged[0].head.sha), 'Exact reviewed merged PR missing');
  for (const workflow of ['verify.yml', 'trusted-publishing-checks.yml', 'session-baseline-checks.yml']) for (const [sha, event] of [[args.workflow, 'push'], [merged[0].head.sha, 'pull_request']]) {
    const candidates = api(base + `actions/workflows/${workflow}/runs?head_sha=${sha}&event=${event}&per_page=100`).workflow_runs.filter(r => r.head_sha === sha && r.event === event).sort((a,b) => b.id-a.id);
    requireThat(candidates.length > 0, 'PR/main CI missing'); checkCiRun(candidates[0], { path: `.github/workflows/${workflow}`, sha, event });
  }
  requireThat(api(base + `git/matching-refs/heads/${args.branch}`).length === 0, 'Data branch already exists; do not overwrite');
  const inputs = new Map();
  for (const file of [...reference.packages.map(p => p.file), 'package-payload-manifest.json']) {
    const actual = path.join(args.assets, file); requireThat(fs.lstatSync(actual).isFile() && !fs.lstatSync(actual).isSymbolicLink(), 'Regular original file required');
    inputs.set(file, fs.readFileSync(actual));
  }
  const prepared = await createPreviewData(inputs, reference, policy, args.workflow);
  fs.mkdirSync(evidence); const payload = path.join(evidence, 'public-payload'); fs.mkdirSync(payload);
  for (const [file, bytes] of prepared.files) fs.writeFileSync(path.join(payload, file), bytes, { flag: 'wx' });
  const ledger = { schemaVersion: 1, status: 'validated-not-yet-uploaded', branch: args.branch, workflowCommit: args.workflow, sourceCommit: reference.sourceCommit, requestSha256: prepared.requestSha256, publicFiles: [...prepared.files].map(([file, bytes]) => ({ file, bytes: bytes.length, sha256: sha256(bytes), gitBlob: gitBlobSha(bytes) })), createdBlobs: [], credentialAcquisition: false, nugetPush: false, tagOrRelease: false, localGuardDotnet: 'stopped' };
  const save = () => fs.writeFileSync(path.join(evidence, 'operation-ledger.json'), JSON.stringify(ledger, null, 2) + '\n');
  let operation = 0;
  const post = (endpoint, body) => {
    const file = path.join(evidence, `request-${++operation}.json`); fs.writeFileSync(file, JSON.stringify(body) + '\n', { flag: 'wx' });
    ledger.status = 'write-in-flight-reconcile-if-interrupted'; ledger.pendingEndpoint = endpoint; save();
    const result = api(base + endpoint, file); delete ledger.pendingEndpoint; return result;
  };
  save();
  const tree = [];
  for (const [file, bytes] of prepared.files) {
    const blob = post('git/blobs', { content: bytes.toString('base64'), encoding: 'base64' });
    requireThat(blob.sha === gitBlobSha(bytes), 'Created blob identity differs');
    ledger.createdBlobs.push(blob.sha); save(); tree.push({ path: file, mode: '100644', type: 'blob', sha: blob.sha });
    console.log(`Verified public data blob ${tree.length}/7: ${file}`);
  }
  const createdTree = post('git/trees', { tree }); ledger.tree = createdTree.sha; save();
  const message = `Stage accepted ArchSift 0.11.0 originals for preview\n\nData only: five original packages and two hash-bound metadata files.\nPackage source ${reference.sourceCommit}; workflow ${args.workflow}.\nNo NuGet push, credential acquisition, tag, Release or installer.\n\nCo-Authored-By: Codex <noreply@openai.com>`;
  const commit = post('git/commits', { message, tree: createdTree.sha, parents: [], author: { name, email } });
  ledger.dataCommit = commit.sha; save();
  const ref = post('git/refs', { ref: `refs/heads/${args.branch}`, sha: commit.sha });
  requireThat(ref.object.sha === commit.sha, 'Data ref identity differs');
  requireThat(api(base + `git/ref/heads/${args.branch}`).object.sha === commit.sha, 'Data ref readback differs');
  const actualTree = api(base + `git/trees/${createdTree.sha}`);
  requireThat(actualTree.tree.length === 7, 'Unexpected public tree');
  for (const row of actualTree.tree) {
    requireThat(row.mode === '100644' && row.type === 'blob' && prepared.files.has(row.path), 'Unexpected public entry');
    const blob = api(base + `git/blobs/${row.sha}`);
    requireThat(blob.encoding === 'base64' && sha256(Buffer.from(blob.content, 'base64')) === sha256(prepared.files.get(row.path)), 'Public blob bytes differ');
  }
  ledger.status = 'public-data-branch-verified-preview-not-yet-dispatched'; save();
  console.log(JSON.stringify({ status: ledger.status, branch: ledger.branch, dataCommit: ledger.dataCommit, workflowCommit: ledger.workflowCommit, requestSha256: ledger.requestSha256, publicFiles: 7, originalPackages: 5, publicationEnabled: false }));
}
if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) main().catch(() => { console.error('Public preview staging stopped; preserve the ledger, do not overwrite refs or retry blindly.'); process.exitCode = 1; });
