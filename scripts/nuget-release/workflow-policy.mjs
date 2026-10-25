import assert from 'node:assert/strict';
import { parseDocument } from 'yaml';
export { stringify } from 'yaml';
import { exactKeys, requireThat } from './model.mjs';
export const pins = {
  checkout: 'actions/checkout@11d5960a326750d5838078e36cf38b85af677262',
  upload: 'actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02',
  sdk: 'actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9',
  login: 'NuGet/login@8d196754b4036150537f80ac539e15c2f1028841'
};
const npm = 'npm ci --prefix scripts/nuget-release --ignore-scripts --no-audit --no-fund';
const gate = "github.repository == 'von12549/ArchSift' && github.ref == 'refs/heads/main'";
export const publishGate = `${gate} && github.run_attempt == 1 && inputs.mode == 'publish' && needs.preflight.outputs.publish_ready == 'true' && vars.ARCHSIFT_NUGET_PUBLISH_0110 == 'true'`;
export function parseWorkflow(text) {
  const doc = parseDocument(text, { uniqueKeys: true });
  requireThat(doc.errors.length === 0, 'Malformed/duplicate YAML');
  // No aliases/merge keys/custom tags, including within otherwise ignored metadata.
  requireThat(!/[&*][A-Za-z_]|!!|!<|^\s*<<:/m.test(text), 'YAML indirection is prohibited');
  return doc.toJS({ maxAliasCount: 0 });
}
export function checkWorkflow(text) {
  const w = parseWorkflow(text);
  exactKeys(w, ['name','on','permissions','concurrency','env','jobs']);
  exactKeys(w.on, ['workflow_dispatch']); exactKeys(w.on.workflow_dispatch, ['inputs']);
  const inputs = w.on.workflow_dispatch.inputs;
  exactKeys(inputs, ['mode','workflow_sha','data_commit','request_sha256']);
  assert.equal(inputs.mode.type, 'choice'); assert.equal(inputs.mode.default, 'preflight');
  assert.deepEqual(inputs.mode.options, ['preflight','preview','publish']);
  for (const [key, value] of Object.entries(inputs)) {
    exactKeys(value, key === 'mode' ? ['description','type','options','default'] : ['description','type','default']);
    if (key !== 'mode') { assert.equal(value.type, 'string'); assert.equal(value.default, ''); }
  }
  assert.deepEqual(w.permissions, { contents: 'read', actions: 'read', 'pull-requests': 'read' });
  assert.deepEqual(w.concurrency, { group: 'archsift-nuget-0.11.0', 'cancel-in-progress': false });
  assert.deepEqual(w.env, { RELEASE_MODE: '${{ inputs.mode }}', RELEASE_WORKFLOW_SHA: '${{ inputs.workflow_sha }}', RELEASE_DATA_COMMIT: '${{ inputs.data_commit }}', RELEASE_REQUEST_SHA256: '${{ inputs.request_sha256 }}' });
  exactKeys(w.jobs, ['preflight','audit','publish','public-verification']);
  const a = w.jobs.preflight, b = w.jobs.publish;
  exactKeys(a, ['if','runs-on','timeout-minutes','outputs','steps']);
  assert.equal(a.if, gate); assert.equal(a['runs-on'], 'ubuntu-latest'); assert.equal(a['timeout-minutes'], 10);
  assert.deepEqual(a.outputs, { publish_ready: '${{ steps.prepare.outputs.publish_ready }}', has_inputs: '${{ steps.prepare.outputs.has_inputs }}', artifact_id: '${{ steps.upload.outputs.artifact-id }}', artifact_sha256: '${{ steps.upload.outputs.artifact-digest }}' });
  assert.equal(a.steps.length, 6);
  assert.deepEqual(a.steps[0], { uses: pins.checkout, with: { 'persist-credentials': false } });
  assert.deepEqual(a.steps[1], { name: 'Install locked release parsers without lifecycle scripts', run: npm });
  const prepare = a.steps[2]; exactKeys(prepare, ['name','id','shell','env','run']);
  assert.equal(prepare.id, 'prepare'); assert.equal(prepare.shell, 'pwsh -NoProfile -NonInteractive -File {0}');
  assert.deepEqual(prepare.env, { GH_TOKEN: '${{ github.token }}', RELEASE_SWITCH: '${{ vars.ARCHSIFT_NUGET_PUBLISH_0110 }}' });
  assert.equal(prepare.run.trim(), ["$ErrorActionPreference = 'Stop'", '. ./scripts/HostState.ps1', '$before = Get-ArchSiftHostHash', 'try {', '  & node ./scripts/nuget-release/driver.mjs prepare', "  if ($LASTEXITCODE -ne 0) { throw 'Release preparation gate failed' }", '} finally {', "  if ((Get-ArchSiftHostHash) -cne $before) { throw 'Host drift; no repair performed' }", '}'].join('\n'));
  assert.deepEqual(a.steps[3], { uses: pins.upload, id: 'upload', if: "steps.prepare.outputs.has_inputs == 'true'", with: { name: 'archsift-0.11.0-reviewed-submissions', path: '${{ runner.temp }}/archsift-release-inputs', 'if-no-files-found': 'error', 'retention-days': 7 } });
  assert.deepEqual(a.steps[4], { name: 'Record immutable artifact identity for operator review', if: "steps.prepare.outputs.has_inputs == 'true'", env: { ARTIFACT_ID: '${{ steps.upload.outputs.artifact-id }}', ARTIFACT_SHA256: '${{ steps.upload.outputs.artifact-digest }}' }, run: 'printf \'%s\\n\' "Artifact ID=$ARTIFACT_ID SHA256=$ARTIFACT_SHA256" >> "$GITHUB_STEP_SUMMARY"' });
  assert.deepEqual(a.steps[5], { name: 'Independently verify reviewed input ZIP before any approval', if: "steps.prepare.outputs.has_inputs == 'true'", shell: 'pwsh -NoProfile -NonInteractive -File {0}', env: { GH_TOKEN: '${{ github.token }}', RELEASE_ARTIFACT_ID: '${{ steps.upload.outputs.artifact-id }}', RELEASE_ARTIFACT_SHA256: '${{ steps.upload.outputs.artifact-digest }}', RELEASE_SWITCH: '${{ vars.ARCHSIFT_NUGET_PUBLISH_0110 }}' }, run: ["$ErrorActionPreference = 'Stop'", '. ./scripts/HostState.ps1', '$before = Get-ArchSiftHostHash', 'try {', '  & node ./scripts/nuget-release/driver.mjs verify-preview', "  if ($LASTEXITCODE -ne 0) { throw 'Preview artifact verification failed' }", '} finally {', "  if ((Get-ArchSiftHostHash) -cne $before) { throw 'Host drift; no repair performed' }", '}', ''].join('\n') });
  exactKeys(b, ['needs','if','environment','runs-on','timeout-minutes','permissions','env','steps']);
  assert.deepEqual(b.needs, ['preflight','audit']); assert.equal(b.if, publishGate + " && needs.audit.result == 'success'"); assert.equal(b.environment, 'nuget-release');
  assert.equal(b['runs-on'], 'ubuntu-latest'); assert.equal(b['timeout-minutes'], 45);
  assert.deepEqual(b.permissions, { contents: 'read', actions: 'read', 'pull-requests': 'read', 'id-token': 'write' });
  assert.deepEqual(b.env, { RELEASE_ARTIFACT_ID: '${{ needs.preflight.outputs.artifact_id }}', RELEASE_ARTIFACT_SHA256: '${{ needs.preflight.outputs.artifact_sha256 }}', RELEASE_SWITCH: '${{ vars.ARCHSIFT_NUGET_PUBLISH_0110 }}', RELEASE_AUDIT_ARTIFACT_ID: '${{ needs.audit.outputs.artifact_id }}', RELEASE_AUDIT_ARTIFACT_SHA256: '${{ needs.audit.outputs.artifact_sha256 }}' });
  assert.equal(b.steps.length, 6); assert.deepEqual(b.steps[0], a.steps[0]); assert.deepEqual(b.steps[1], a.steps[1]);
  assert.deepEqual(b.steps[2], { name: 'Revalidate exact artifact, main, rules and version absence before login', env: { GH_TOKEN: '${{ github.token }}' }, run: 'node ./scripts/nuget-release/driver.mjs validate-artifact' });
  assert.deepEqual(b.steps[3], { uses: pins.sdk, with: { 'dotnet-version': '10.0.303' } });
  assert.deepEqual(b.steps[4], { uses: pins.login, id: 'login', with: { user: 'XiaolongFeng', audience: 'https://www.nuget.org', 'token-service-url': 'https://www.nuget.org/api/v2/token' } });
  assert.deepEqual(b.steps[5], { name: 'Publish original submissions sequentially; stop on uncertain outcome', env: { GH_TOKEN: '${{ github.token }}', NUGET_API_KEY: '${{ steps.login.outputs.NUGET_API_KEY }}' }, run: 'node ./scripts/nuget-release/driver.mjs publish' });
  const d=w.jobs.audit;
  exactKeys(d,['needs','if','runs-on','timeout-minutes','outputs','env','steps']);
  assert.equal(d.needs,'preflight'); assert.equal(d.if,"needs.preflight.outputs.has_inputs == 'true'"); assert.equal(d['runs-on'],'ubuntu-latest'); assert.equal(d['timeout-minutes'],20);
  assert.deepEqual(d.outputs,{artifact_id:'${{ steps.audit-upload.outputs.artifact-id }}',artifact_sha256:'${{ steps.audit-upload.outputs.artifact-digest }}'});
  assert.deepEqual(d.env,{RELEASE_ARTIFACT_ID:'${{ needs.preflight.outputs.artifact_id }}',RELEASE_ARTIFACT_SHA256:'${{ needs.preflight.outputs.artifact_sha256 }}',RELEASE_SWITCH:'${{ vars.ARCHSIFT_NUGET_PUBLISH_0110 }}'});
  assert.equal(d.steps.length,6); assert.deepEqual(d.steps[0],a.steps[0]);assert.deepEqual(d.steps[1],a.steps[1]);
  assert.deepEqual(d.steps[2],{name:'Bind verified original inputs to credential-free audit',env:{GH_TOKEN:'${{ github.token }}'},run:'node ./scripts/nuget-release/driver.mjs prepare-audit'});
  assert.deepEqual(d.steps[3],{uses:pins.sdk,with:{'dotnet-version':'10.0.303\n10.0.402\n9.0.314\n8.0.x\n'}});
  assert.deepEqual(d.steps[4],{name:'Audit original package source with fresh official advisory data',shell:'pwsh -NoProfile -NonInteractive -File {0}',run:'& ./scripts/Invoke-HostedReleaseAudit.ps1'});
  assert.deepEqual(d.steps[5],{uses:pins.upload,id:'audit-upload',if:'always()',with:{name:'archsift-0.11.0-prepublication-audit',path:'${{ runner.temp }}/archsift-audit-report','if-no-files-found':'warn','retention-days':7}});
  const v = w.jobs['public-verification'];
  exactKeys(v, ['needs','if','runs-on','timeout-minutes','env','steps']);
  assert.deepEqual(v.needs, ['preflight','publish']); assert.equal(v.if, "needs.publish.result == 'success' && inputs.mode == 'publish'");
  assert.equal(v['runs-on'], 'ubuntu-latest'); assert.equal(v['timeout-minutes'], 30); assert.deepEqual(v.env, { RELEASE_ARTIFACT_ID: '${{ needs.preflight.outputs.artifact_id }}', RELEASE_ARTIFACT_SHA256: '${{ needs.preflight.outputs.artifact_sha256 }}', RELEASE_SWITCH: '${{ vars.ARCHSIFT_NUGET_PUBLISH_0110 }}' });
  assert.equal(v.steps.length, 4); assert.deepEqual(v.steps[0], a.steps[0]); assert.deepEqual(v.steps[1], a.steps[1]); assert.deepEqual(v.steps[2], b.steps[3]);
  assert.deepEqual(v.steps[3], { name: 'Verify anonymous served signatures, owners and all payload bytes', env: { GH_TOKEN: '${{ github.token }}' }, run: 'node ./scripts/nuget-release/driver.mjs verify-public' });
  requireThat(!/\$\{\{\s*(?:secrets\.|inputs\.)[^}]*\}\}/.test(a.steps.map(s => s.run ?? '').join('\n') + b.steps.map(s => s.run ?? '').join('\n')), 'Inputs/secrets must never be interpolated into executable text');
  return w;
}
