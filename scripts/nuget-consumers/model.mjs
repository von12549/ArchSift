import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { checkPackages, checkOwnerPage, readZip, sha256, ids } from '../nuget-release/model.mjs';
const require = createRequire(new URL('../nuget-release/package.json', import.meta.url));
const { parseDocument } = require('yaml');
export { readZip, sha256 };
export const sourceCommit = '8b721f44e90a41285fa74a6f72576c7f6996ef7d';
const signed = [
 ['b96ad039d293f3451af160f91d56a7ec5c1d100f8e34037e88ccadd95a2b857c',107839],
 ['c97c8d24ddb2fe038263bd818ff5ba5b1682c476f7b84953ee4320289e0879d7',175710],
 ['e424f4b282cee4fcdc9500afbdc255127eb7c26a2ff3369f87d93213a7c3faf4',44643],
 ['345b35171e844aff751cf987c9ecec92201071979f2e23105c07559ae834ff6d',39929],
 ['dfe72c3ed5b0060e4bed57f2ca179b5edae930751d77dd6975b1b4150c1be60e',2920710]
];
export const readJson = p => JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/u, ''));
export function writeJson(file, value) { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n'); }
export function contained(root, candidate) {
  const relative = path.relative(path.resolve(root), path.resolve(candidate));
  assert.ok(relative && relative !== '..' && !relative.startsWith('..' + path.sep) && !path.isAbsolute(relative), 'Expected strictly owned descendant');
  for (let cursor = path.resolve(candidate); ; cursor = path.dirname(cursor)) {
    if (fs.existsSync(cursor)) assert.ok(!fs.lstatSync(cursor).isSymbolicLink(), 'No symlink roots');
    if (cursor === path.dirname(cursor)) break;
  }
  return path.resolve(candidate);
}
export function validatePublication(p, reference) {
  assert.equal(p.schemaVersion, 1); assert.equal(p.kind, 'archsift-nr06-published-inputs');
  assert.equal(p.publicationRun, 40158940042); assert.equal(p.version, '0.11.0');
  assert.equal(p.sourceCommit, sourceCommit); assert.equal(p.sourceTree, reference.sourceTree);
  assert.equal(p.owner, 'XiaolongFeng'); assert.equal(p.packages.length, 5);
  assert.deepEqual(p.packages.map(x => x.id), ids);
  for (const item of p.packages) {
    const original = reference.packages.find(x => x.id === item.id);
    assert.equal(item.submissionSha256, original.sha256);
    assert.match(item.servedSha256, /^[0-9a-f]{64}$/); assert.ok(Number.isSafeInteger(item.servedBytes) && item.servedBytes > 0);
    assert.deepEqual([item.servedSha256,item.servedBytes],signed[ids.indexOf(item.id)],'NR05 signed identity is immutable');
    assert.equal(item.url, `https://api.nuget.org/v3-flatcontainer/${item.id.toLowerCase()}/0.11.0/${item.id.toLowerCase()}.0.11.0.nupkg`);
  }
}
export async function verifyPublicInputs(files, pages, publication, reference, payload) {
  validatePublication(publication, reference);
  for (const item of publication.packages) {
    const bytes = files.get(`${item.id}.0.11.0.nupkg`);
    assert.ok(bytes); assert.equal(bytes.length, item.servedBytes); assert.equal(sha256(bytes), item.servedSha256, 'Served identity changed; stop for review');
    checkOwnerPage(pages.get(item.id), publication.owner);
  }
  await checkPackages(files, reference, payload, { served: true });
}
export function verifyGate(report, ctrf, config) {
  assert.equal(report.outcome.runGate, 'allow'); assert.equal(report.testScope.coverageComplete, true);
  assert.equal(report.process.audit.status, 'not-run');
  assert.deepEqual(Object.fromEntries(['tests','passed','failed','skipped'].map(k => [k, ctrf.results.summary[k]])), { tests: 4, passed: 4, failed: 0, skipped: 0 });
  const rows = ctrf.results.tests;
  assert.equal(rows.length, 4); assert.ok(rows.every(x => x.status === 'passed'));
  assert.equal(new Set(rows.map(x => x.name)).size, 4, 'Duplicate case receipt');
  assert.ok(rows.some(x => /OriginalRows/.test(x.name)) && rows.some(x => /ProductPackagesMatchApprovedClosure/.test(x.name)) && rows.some(x => /JsonRule/.test(x.name)), 'Missing explicit consumer case families');
  assert.equal(config.requiredCasesSha256, sha256(fs.readFileSync(path.join(config.targetRoot, config.requiredCases))));
}
export function validateConsumerReceipt(receipt, kind, fixture, payload) {
  assert.equal(receipt.kind, kind); assert.equal(receipt.version, '0.11.0'); assert.equal(receipt.checks, 'passed');
  assert.ok(receipt.assemblies.length > 0);
  for (const item of receipt.assemblies) {
    contained(fixture, item.location); assert.equal(item.version.split('+')[0], '0.11.0');
    const expected = payload.packages.flatMap(x => x.payload).filter(x => x.path === `lib/net10.0/${item.name}.dll`);
    assert.equal(expected.length, 1, 'Assembly must have a public package payload origin');
    assert.equal(sha256(fs.readFileSync(item.location)), expected[0].sha256, 'Runtime copy must equal the signed package library');
  }
}
const pinned = {
  'actions/checkout': '11d5960a326750d5838078e36cf38b85af677262',
  'actions/setup-dotnet': '67a3573c9a986a3f9c594539f4ab511d57bb3ce9',
  'actions/upload-artifact': 'ea165f8d65b6e75b540449e92b4886f43607fa02'
};
export function validateWorkflow(text) {
  const document = parseDocument(text, { uniqueKeys: true }); assert.equal(document.errors.length, 0);
  const w = document.toJS();
  assert.deepEqual(w.permissions, { contents: 'read', actions: 'read' });
  assert.deepEqual(Object.keys(w.on).sort(), ['pull_request','push','workflow_dispatch']);
  assert.deepEqual(w.on.push.branches, ['main']);
  assert.deepEqual(Object.keys(w.jobs).sort(), ['consumers','safety']);
  for (const job of Object.values(w.jobs)) {
    assert.ok(!job.environment && !job.permissions && !job.container && !job.services);
    assert.ok(!JSON.stringify(job).includes('self-hosted'));
    for (const step of job.steps) {
      if (step.uses) { const [action, commit] = step.uses.split('@'); assert.equal(commit, pinned[action], 'Only pinned approved actions'); }
      if (step.uses?.startsWith('actions/checkout')) assert.equal(step.with['persist-credentials'], false);
      if (step.run) assert.ok(!/id-token|nuget\s+push|NuGet\/login|publish-nuget|gh\s+(?:release|variable\s+(?:set|delete))|setx|SetEnvironmentVariable|secrets\./i.test(step.run));
    }
  }
  assert.equal(w.jobs.consumers.needs, 'safety');
  assert.equal(w.jobs.consumers.env.NR06_RELEASE_SWITCH, '${{ vars.ARCHSIFT_NUGET_PUBLISH_0110 }}');
  assert.deepEqual(w.jobs.consumers.strategy.matrix.include.map(x => `${x.os}/${x.sdk}`), ['windows-latest/10.0.100','windows-latest/10.0.303','windows-latest/10.0.402','ubuntu-latest/10.0.100','ubuntu-latest/10.0.303']);
  const commands = w.jobs.consumers.steps.filter(x => x.run).map(x => x.run).join('\n');
  assert.ok(commands.includes('Assert-NR06HostedContext.ps1') && commands.includes('Invoke-NR06HostedConsumers.ps1'));
  return w;
}
export function assertHosted(env) {
  assert.equal(env.GITHUB_ACTIONS, 'true'); assert.equal(env.RUNNER_ENVIRONMENT, 'github-hosted');
  assert.equal(env.GITHUB_REPOSITORY, 'von12549/ArchSift'); assert.equal(env.GITHUB_RUN_ATTEMPT, '1');
  assert.match(env.GITHUB_RUN_ID ?? '', /^\d+$/); assert.match(env.GITHUB_SHA ?? '', /^[0-9a-f]{40}$/);
  assert.ok(env.GITHUB_EVENT_NAME === 'pull_request' || env.GITHUB_REF === 'refs/heads/main');
  assert.ok(!env.NUGET_API_KEY && !env.ACTIONS_ID_TOKEN_REQUEST_TOKEN && !env.ACTIONS_ID_TOKEN_REQUEST_URL, 'No publishing credential/OIDC capability');
}
