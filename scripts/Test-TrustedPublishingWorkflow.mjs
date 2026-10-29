import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkWorkflow, checkWorkflowForVersion, parseWorkflow, stringify } from './nuget-release/workflow-policy.mjs';
import { sha256 } from './nuget-release/model.mjs';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const activeWorkflow = fs.readFileSync(path.join(root, '.github/workflows/publish-nuget.yml'), 'utf8');
checkWorkflowForVersion(activeWorkflow,'0.11.3');
// Reuse the full mutation suite against the canonical version-normalized structure.
const workflow=activeWorkflow.replaceAll('0.11.3','0.11.2').replaceAll('PUBLISH_0113','PUBLISH_0112');
checkWorkflow(workflow);
const mutations = [
 w => { delete w.jobs.preflight.steps[5].env.RELEASE_SWITCH; },
 w => { w.jobs.preflight.steps[5].env.RELEASE_SWITCH = 'true'; },
 w => { w.jobs.preflight.steps[5].env.RELEASE_SWITCH = 'false'; },
 w => { w.jobs.preflight.steps[5].env.RELEASE_SWITCH = '${{ vars.OTHER_RELEASE_SWITCH }}'; },
 w => { delete w.jobs.preflight.steps[5].env.RELEASE_SWITCH; w.jobs.preflight.steps[2].env.RELEASE_SWITCH = '${{ vars.ARCHSIFT_NUGET_PUBLISH_0110 }}'; },
 w => { w.permissions.contents = 'write'; },
 w => { w.permissions['id-token'] = 'write'; },
 w => { w.on.push = {}; },
 w => { w.on.workflow_call = {}; },
 w => { w.on.workflow_dispatch.inputs.mode.default = 'publish'; },
 w => { w.on.workflow_dispatch.inputs.extra = { type: 'string' }; },
 w => { w.jobs.preflight.environment = 'nuget-release'; },
 w => { w.jobs.preflight.permissions = { 'id-token': 'write' }; },
 w => { w.jobs.preflight.if = 'true'; },
 w => { w.jobs.preflight['runs-on'] = 'self-hosted'; },
 w => { w.jobs.preflight.steps[0].with['persist-credentials'] = true; },
 w => { w.jobs.preflight.steps[1].run += '\nnode evil.mjs'; },
 w => { w.jobs.preflight.steps[2].run += '\nGet-Credential'; },
 w => { w.jobs.preflight.steps[2].env.KEY = '${{ secrets.NUGET_API_KEY }}'; },
 w => { w.jobs.preflight.steps[3].if = 'always()'; },
 w => { w.jobs.preflight.steps[3].with.path = '${{ github.workspace }}'; },
 w => { w.jobs.preflight.steps[3].uses = 'actions/upload-artifact@v4'; },
 w => { w.jobs.preflight.steps[4].run += '\nsetx X Y'; },
 w => { w.jobs.publish.if = "inputs.mode == 'publish'"; },
 w => { w.jobs.publish.environment = 'other'; },
 w => { delete w.jobs.publish.environment; },
 w => { w.jobs.publish.permissions.contents = 'write'; },
 w => { w.jobs.publish.steps[2].run = 'echo skip'; },
 w => { [w.jobs.publish.steps[2],w.jobs.publish.steps[4]] = [w.jobs.publish.steps[4],w.jobs.publish.steps[2]]; },
 w => { w.jobs.publish.steps[4].uses = 'NuGet/login@v1'; },
 w => { w.jobs.publish.steps[4].with.user = 'other'; },
 w => { w.jobs.publish.steps[4].with.audience = 'https://evil.example'; },
 w => { w.jobs.publish.steps[5].run += ' --skip-duplicate'; },
 w => { w.jobs.release = { 'runs-on': 'ubuntu-latest' }; },
 w => { w.concurrency['cancel-in-progress'] = true; },
 w => { w.jobs['public-verification'].permissions = { 'id-token': 'write' }; },
 w => { w.jobs['public-verification'].env.NUGET_API_KEY = '${{ steps.login.outputs.NUGET_API_KEY }}'; },
 w => { w.jobs['public-verification'].steps[3].run += '\ngh release create v0.11.0'; },
 w => { w.jobs.preflight.steps[5].if = 'false'; },
 w => { w.jobs.preflight.steps[5].run = 'echo unverified'; },
 w => { w.jobs.preflight.steps[5].env.NUGET_API_KEY = '${{ secrets.NUGET_API_KEY }}'; },
 w => { w.jobs.preflight.steps[5].run += '\nNuGet/login'; },
 w => { w.jobs.audit.permissions = { 'id-token': 'write' }; },
 w => { w.jobs.audit.env.NUGET_API_KEY = 'secret'; },
 w => { w.jobs.audit.steps[4].run += ' -SkipAudit'; },
 w => { w.jobs.audit.if = 'false'; },
 w => { w.jobs.audit.steps[5].with.path = '${{ github.workspace }}'; },
 w => { w.jobs.publish.needs = 'preflight'; },
 w => { w.jobs.publish.if = w.jobs.publish.if.replace(" && needs.audit.result == 'success'", ''); },
 w => { w.jobs.preflight.steps[5].if += " && inputs.mode == 'preview'"; }
];
for (const [i, mutate] of mutations.entries()) { const w = parseWorkflow(workflow); mutate(w); assert.throws(() => checkWorkflow(stringify(w)), `Unsafe workflow mutation ${i}`); }
assert.throws(() => checkWorkflow(workflow + '\npermissions:\n  contents: write\n'));
const policy = JSON.parse(fs.readFileSync(path.join(root, 'docs/releases/0.11.0/publishing-activation.json')));
assert.equal(policy.realAssetTransferEnabled, true); assert.equal(policy.publicationEnabled, true); assert.equal(policy.auditMaxAgeHours, 24);
assert.equal(policy.localGuardDotnet, 'stopped'); assert.equal(policy.newInstaller, false);
assert.equal(sha256(fs.readFileSync(path.join(root, policy.originalReference))), policy.originalReferenceSha256);
const preflight = fs.readFileSync(path.join(root, 'scripts/Test-NuGetReleasePreflight.ps1'), 'utf8');
assert.doesNotMatch(preflight, /(?:Invoke-Expression|Start-Process|SetEnvironmentVariable|setx\b|ACTIONS_ID_TOKEN|nuget\s+push|NuGet\/login|NUGET_API_KEY)/i);
const hosted = fs.readFileSync(path.join(root, 'scripts/Invoke-HostedNuGet.ps1'), 'utf8');
assert.doesNotMatch(hosted, /(?:SetEnvironmentVariable|setx\b|Set-ItemProperty|--api-key|--skip-duplicate)/i);
assert.match(hosted, /Environment\['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'\]='0'/);
assert.match(hosted, /Environment\['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE'\]='true'/);
assert.match(hosted, /Get-ArchSiftHostHash/);
console.log(`Passed structural YAML release boundary, ${mutations.length + 1} rejected mutations and the closed operational release gates.`);

const licenses = JSON.parse(fs.readFileSync(path.join(root, 'scripts/nuget-release/licenses/manifest.json')));
for (const license of licenses.packages) {
  assert.equal(sha256(fs.readFileSync(path.join(root, 'scripts/nuget-release/licenses', license.file))), license.sha256);
  assert.equal(sha256(fs.readFileSync(path.join(root, 'scripts/nuget-release/node_modules', license.name, 'LICENSE'))), license.sha256);
}

function checkHostedHttpBoundary(text) {
  assert.ok(text.includes("Environment['NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT']='1'"));
  assert.ok(text.includes("Environment['NUGET_EXE_NO_PROMPT']='true'"));
}
checkHostedHttpBoundary(hosted);
assert.throws(() => checkHostedHttpBoundary(hosted.replace("Environment['NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT']='1'", "Environment['NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT']='6'")));
assert.throws(() => checkHostedHttpBoundary(hosted.replace("Environment['NUGET_EXE_NO_PROMPT']='true'", "Environment['NUGET_EXE_NO_PROMPT']='false'")));
console.log('Passed child HTTP attempt/prompt boundary and 2 rejected mutations.');

const auditScript=fs.readFileSync(path.join(root,'scripts/Invoke-HostedReleaseAudit.ps1'),'utf8');
assert.doesNotMatch(auditScript, /SetEnvironmentVariable|setx\b|Set-ItemProperty/i);
assert.ok(auditScript.includes("Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH']='0'"));
assert.ok(auditScript.includes("'-p:NuGetAudit=true'"));
assert.ok(auditScript.includes("'-p:NuGetAuditMode=all'"));
assert.ok(auditScript.includes("'-p:NuGetAuditLevel=moderate'"));
assert.ok(auditScript.includes('Get-ArchSiftHostHash'));
