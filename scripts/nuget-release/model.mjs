import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import yauzl from 'yauzl';

export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
export const ids = ['ArchSift.Contracts', 'ArchSift.Core', 'ArchSift.ArchUnit', 'ArchSift.Testing', 'ArchSift.Cli'];
export const requireThat = (condition, message) => { if (!condition) throw new Error(message); };
const sha = (value, length) => typeof value === 'string' && new RegExp(`^[a-f0-9]{${length}}$`).test(value);
export function exactKeys(value, keys) {
  requireThat(value && Object.getPrototypeOf(value) === Object.prototype, 'Expected a plain object');
  assert.deepEqual(Object.keys(value).sort(), [...keys].sort(), 'Unexpected or missing fields');
}
export function checkReference(r) {
  requireThat(r.schemaVersion === 1 && ['0.11.0','0.11.2'].includes(r.version) && sha(r.sourceCommit, 40) && sha(r.sourceTree, 40), 'Invalid original source reference');
  requireThat(r.humanAcceptance.status === 'passed-user-confirmed' && (r.version === '0.11.0' ? r.humanAcceptance.usabilityScore === 4 : r.humanAcceptance.finalIdentityConfirmed === true), 'Human acceptance missing');
  requireThat(r.packages.length === 5, 'Exactly five packages required');
  r.packages.forEach((p, i) => requireThat(p.id === ids[i] && p.file === `${ids[i]}.${r.version}.nupkg` && Number.isSafeInteger(p.bytes) && p.bytes > 0 && sha(p.sha256, 64), 'Invalid package/order'));
  requireThat(r.sourceCi.length === 2 && r.sourceCi[1].headSha === r.sourceCommit, 'Source CI binding missing');
}
export function checkContext(c, policy, mode) {
  requireThat(['preflight', 'preview', 'publish'].includes(mode), 'Unknown mode');
  requireThat(c.repository === policy.repository && c.repositoryId === String(policy.repositoryId) && c.ownerId === String(policy.ownerId) && c.ref === 'refs/heads/main', 'Wrong repository or branch');
  requireThat(sha(c.sha, 40) && c.mainSha === c.sha, 'Approved workflow commit is no longer main');
  requireThat(c.runAttempt === '1' && c.event === 'workflow_dispatch', 'Only a fresh manual dispatch is accepted');
  if (mode !== 'preflight') {
    requireThat(policy.realAssetTransferEnabled === true, 'Real asset transfer is locked by the reviewed preparation scope');
    requireThat(c.workflowSha === c.sha && sha(c.dataCommit, 40) && sha(c.requestSha256, 64), 'Exact workflow/data/request identities required');
  } else {
    requireThat(!c.workflowSha && !c.dataCommit && !c.requestSha256, 'Default preflight does not accept staged inputs');
  }
  if (mode === 'publish') {
    requireThat(policy.publicationEnabled === true && c.releaseSwitch === 'true', 'Publication remains closed');
  }
}
export function checkEnvironment(environment, branches, policy) {
  requireThat(environment.name === policy.environment && environment.can_admins_bypass === false, 'Environment missing or bypass enabled');
  assert.deepEqual(environment.deployment_branch_policy, { protected_branches: false, custom_branch_policies: true });
  const reviews = environment.protection_rules.filter(r => r.type === 'required_reviewers');
  requireThat(reviews.length === 1 && reviews[0].prevent_self_review === false, 'Unexpected reviewer rule');
  const reviewers = reviews[0].reviewers;
  requireThat(reviewers.length === 1 && reviewers[0].type === 'User' && reviewers[0].reviewer.login === policy.reviewer && reviewers[0].reviewer.id === policy.ownerId, 'Wrong reviewer identity');
  requireThat(environment.protection_rules.length === 2 && environment.protection_rules.some(r => r.type === 'branch_policy'), 'Unexpected protection rules');
  requireThat(branches.total_count === 1 && branches.branch_policies.length === 1 && branches.branch_policies[0].type === 'branch' && branches.branch_policies[0].name === 'main', 'Exactly Branch/main is required');
}
export function checkCiRun(run, expected) {
  requireThat(run.repository?.full_name === 'von12549/ArchSift' && run.path === expected.path && run.head_sha === expected.sha && run.event === expected.event && run.status === 'completed' && run.conclusion === 'success', 'Pinned CI identity did not pass');
  requireThat(run.head_repository?.full_name === 'von12549/ArchSift', 'Fork CI is not release proof');
}
export function checkRequest(bytes, expectedHash, reference, policy, context) {
  requireThat(bytes.length <= 16384 && sha256(bytes) === expectedHash, 'Request hash/size differs');
  const r = JSON.parse(bytes.toString('utf8'));
  exactKeys(r, ['schemaVersion', 'version', 'sourceCommit', 'sourceTree', 'workflowCommit', 'repositoryId', 'ownerId', 'packages', 'payloadManifestSha256', 'acceptanceSha256']);
  requireThat(r.schemaVersion === 1 && r.version === reference.version && r.sourceCommit === reference.sourceCommit && r.sourceTree === reference.sourceTree && r.workflowCommit === context.sha && r.repositoryId === policy.repositoryId && r.ownerId === policy.ownerId, 'Request source/workflow/repository differs');
  requireThat(r.payloadManifestSha256 === policy.payloadManifestSha256 && r.acceptanceSha256 === reference.acceptedPreview.acceptanceSha256, 'Acceptance/payload binding differs');
  assert.deepEqual(r.packages, reference.packages, 'Request package set/order differs');
  return r;
}
export function safeEntry(name) {
  requireThat(typeof name === 'string' && name.length > 0 && name.length < 512 && !/[\\\x00-\x1f:]/.test(name) && !name.startsWith('/') && !name.endsWith('/') && name.split('/').every(p => p && p !== '.' && p !== '..' && !/[ .]$/.test(p)), 'Unsafe ZIP entry');
}
// Bounded, in-memory read; never extracts paths or executes package content.
export async function readZip(bytes, { maxInput = 10_000_000, maxTotal = 80_000_000, maxEntries = 1500 } = {}) {
  requireThat(Buffer.isBuffer(bytes) && bytes.length <= maxInput, 'ZIP input exceeds limit');
  return new Promise((resolve, reject) => {
    yauzl.fromBuffer(bytes, { lazyEntries: true, strictFileNames: true, validateEntrySizes: true }, (err, zip) => {
      if (err) { reject(new Error('Invalid ZIP')); return; }
      const files = new Map(), names = new Set(); let total = 0;
      const fail = e => { zip.close(); reject(e); };
      zip.on('error', () => fail(new Error('Invalid ZIP stream')));
      zip.on('end', () => resolve(files));
      zip.on('entry', entry => {
        try {
          safeEntry(entry.fileName);
          requireThat(!names.has(entry.fileName.toLowerCase()) && names.size < maxEntries, 'Duplicate/excess ZIP entries');
          requireThat((entry.generalPurposeBitFlag & 1) === 0 && ((entry.externalFileAttributes >>> 16) & 0xf000) !== 0xa000, 'Encrypted/symlink ZIP entry');
          total += entry.uncompressedSize;
          requireThat(Number.isSafeInteger(total) && total <= maxTotal, 'Expanded ZIP exceeds limit');
          names.add(entry.fileName.toLowerCase());
          zip.openReadStream(entry, (error, stream) => {
            if (error) { fail(new Error('ZIP entry cannot be read')); return; }
            const chunks = []; let size = 0;
            stream.on('data', chunk => { size += chunk.length; if (size > entry.uncompressedSize || size > maxTotal) { stream.destroy(); fail(new Error('ZIP stream exceeds limit')); } else chunks.push(chunk); });
            stream.on('error', () => fail(new Error('ZIP stream failed')));
            stream.on('end', () => { if (size !== entry.uncompressedSize) { fail(new Error('ZIP size differs')); return; } files.set(entry.fileName, Buffer.concat(chunks)); zip.readEntry(); });
          });
        } catch (e) { fail(e); }
      });
      zip.readEntry();
    });
  });
}
export async function checkPackages(files, reference, payloadManifest, { served = false } = {}) {
  checkReference(reference);
  requireThat(payloadManifest.schemaVersion === 1 && payloadManifest.version === reference.version && payloadManifest.sourceCommit === reference.sourceCommit && payloadManifest.packages.length === 5, 'Invalid payload manifest');
  for (const p of reference.packages) {
    const bytes = files.get(p.file);
    requireThat(bytes && (served || (bytes.length === p.bytes && sha256(bytes) === p.sha256)), 'Submission bytes differ');
    const entries = await readZip(bytes);
    const manifest = payloadManifest.packages.filter(x => x.id === p.id);
    requireThat(manifest.length === 1 && manifest[0].submittedSha256 === p.sha256, 'Payload identity differs');
    const expected = manifest[0].payload;
    requireThat(entries.size === expected.length + (served ? 1 : 0), 'Unexpected payload entries');
    if (served) requireThat(entries.has('.signature.p7s') && entries.get('.signature.p7s').length > 0, 'Repository signature missing');
    for (const e of expected) {
      safeEntry(e.path); const b = entries.get(e.path);
      requireThat(b && b.length === e.length && sha256(b) === e.sha256, 'Nonsignature payload differs');
    }
    const nuspec = entries.get(`${p.id}.nuspec`)?.toString('utf8');
    requireThat(nuspec && nuspec.includes(`<id>${p.id}</id>`) && nuspec.includes(`<version>${reference.version}</version>`) && nuspec.includes(reference.sourceCommit), 'Nuspec identity differs');
  }
}
export function checkArtifact(a, context) {
  requireThat(a.expired === false && a.name === `archsift-${context.version || '0.11.0'}-reviewed-submissions` && Number.isSafeInteger(a.id) && a.id > 0 && a.size_in_bytes > 0 && a.size_in_bytes <= 10_000_000, 'Artifact unavailable/oversized');
  requireThat(a.workflow_run?.id === Number(context.runId) && a.workflow_run?.head_sha === context.sha && a.workflow_run?.head_repository_id === Number(context.repositoryId) && /^sha256:[a-f0-9]{64}$/.test(a.digest), 'Artifact provenance/digest differs');
}
export function checkOwnerPage(html, user) {
  // Scoped gallery owner list; Authors or unrelated profile links cannot satisfy this.
  const sections = [...html.matchAll(/<ul\s+class="[^"]*\bowner-list\b[^"]*"[^>]*>[\s\S]*?<\/ul>/gi)];
  requireThat(sections.length === 1 && new RegExp(`href="/profiles/${user}"(?:[\\s>])`, 'i').test(sections[0][0]), 'Gallery owner proof unavailable, ambiguous or owner differs');
}
export async function publishSequential({ reference, push, verifyServed, record, now = Date.now }) {
  const started = now(); const rows = reference.packages.map(p => ({ id: p.id, state: 'not-attempted' }));
  await record(rows);
  for (let i = 0; i < rows.length; i++) {
    requireThat(now() - started < 40 * 60_000, 'Credential window exceeded; no automatic refresh');
    rows[i].state = 'in-flight'; await record(rows);
    try {
      await push(reference.packages[i]);
      rows[i].state = 'accepted-awaiting-validation'; await record(rows);
      const proof = await verifyServed(reference.packages[i]);
      requireThat(proof.signatureValid === true && proof.ownerVerified === true && proof.payloadVerified === true, 'Served verification incomplete');
      rows[i] = { ...rows[i], state: 'verified-served', proof }; await record(rows);
    } catch {
      rows[i].state = 'outcome-unknown'; await record(rows);
      throw new Error('Publication stopped; reconcile exact server state before a separately approved new run');
    }
  }
  return rows;
}

export async function checkPreviewArtifact(raw, metadata, reference, policy, context, expectedDigest) {
  checkArtifact(metadata, context);
  requireThat(/^[a-f0-9]{64}$/.test(expectedDigest) && metadata.digest === `sha256:${expectedDigest}` && sha256(raw) === expectedDigest && raw.length === metadata.size_in_bytes, 'Raw artifact ZIP digest/size differs');
  const files = await readZip(raw, { maxTotal: 4_000_000, maxEntries: 7 });
  const allowed = [...reference.packages.map(p => p.file), 'release-request.json', 'package-payload-manifest.json'].sort();
  requireThat(JSON.stringify([...files.keys()].sort()) === JSON.stringify(allowed), 'Artifact file allowlist differs');
  checkRequest(files.get('release-request.json'), context.requestSha256, reference, policy, context);
  requireThat(sha256(files.get('package-payload-manifest.json')) === policy.payloadManifestSha256, 'Payload manifest differs');
  await checkPackages(files, reference, JSON.parse(files.get('package-payload-manifest.json')));
  return files;
}

export function checkAuditReport(report, context, reference, policy, input, now = Date.now()) {
  requireThat(report.status==='passed-fresh-source-audit' && report.schemaVersion===1 && report.binding.sourceCommit===reference.sourceCommit && report.binding.sourceTree===reference.sourceTree && report.binding.workflowCommit===context.sha && report.binding.runId===Number(context.runId) && report.binding.dataCommit===context.dataCommit && report.binding.requestSha256===context.requestSha256 && report.binding.inputArtifactId===input.id && report.binding.inputArtifactSha256===input.sha256, 'Audit source/request/artifact binding differs');
  requireThat(report.audit.enabled===true && report.audit.mode==='all' && report.audit.level==='moderate' && report.audit.warningsAsErrors===true && report.audit.source==='https://data.nuget.org/v3/index.json' && report.credentialAcquisition===false && report.hostEqual===true && report.targetApplicationExecuted===false && report.projects.length>=5 && report.projects.every(x=>typeof x.project==='string' && /^[a-f0-9]{64}$/.test(x.assetsSha256)) && report.packages.length>0 && report.dataFiles.length>=3, 'Audit policy or coverage missing');
  const time=Date.parse(report.completedUtc);
  requireThat(Number.isFinite(time) && time<=now+60_000 && now-time<=policy.auditMaxAgeHours*3_600_000 && policy.auditMaxAgeHours===24,'Audit is stale or has an invalid timestamp');
  requireThat(report.dataFiles.some(x=>x.name==='vuln_index.dat') && report.dataFiles.some(x=>x.name==='vuln_data_base.dat') && report.dataFiles.some(x=>x.name==='vuln_data_update.dat') && report.dataFiles.every(x=>/^[a-f0-9]{64}$/.test(x.sha256)),'Fresh official advisory data proof missing');
}
