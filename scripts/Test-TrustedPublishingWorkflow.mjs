import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
// Deliberately narrow policy for the preparation-only workflow, not a general YAML parser.
function check(text) {
  const yaml = text.replaceAll('\r\n', '\n');
  assert.match(yaml, /^on:\n  workflow_dispatch:\npermissions:/m);
  assert.doesNotMatch(yaml, /\t|[&*][A-Za-z_]|^\s*<<:/m, 'No YAML aliases or merge keys');
  assert.doesNotMatch(yaml, /^\s*(?:environment|id-token|secrets|uses:\s*NuGet\/login|workflow_call|pull_request_target|inputs)\s*:/mi);
  assert.doesNotMatch(yaml, /(?:secrets\.|NuGet\/login@|ACTIONS_ID_TOKEN|nuget\s+push|gh\s+(?:release|api.*(?:POST|PUT|PATCH|DELETE))|contents:\s*write|actions:\s*write)/i);
  assert.match(yaml, /^permissions:\n  contents: read\n  actions: read\nconcurrency:/m);
  assert.match(yaml, /^    if: github\.repository == 'von12549\/ArchSift' && github\.ref == 'refs\/heads\/main'$/m);
  assert.match(yaml, /^    runs-on: ubuntu-latest$/m);
  assert.equal((yaml.match(/^  [\w-]+:\n/gm) ?? []).length, 2, 'Only workflow_dispatch and preflight blocks');
  assert.equal((yaml.match(/^      - /gm) ?? []).length, 2, 'Only checkout and preflight steps');
  assert.equal((yaml.match(/uses:/g) ?? []).length, 1);
  assert.match(yaml, /uses: actions\/checkout@11d5960a326750d5838078e36cf38b85af677262/);
  assert.match(yaml, /^          persist-credentials: false$/m);
  assert.match(yaml, /^        shell: pwsh -NoProfile -NonInteractive -File \{0\}$/m);
  const run = yaml.split('        run: |\n');
  assert.equal(run.length, 2);
  assert.equal(run[1].trimEnd(), [
    "          $ErrorActionPreference = 'Stop'",
    '          & ./scripts/Test-NuGetReleasePreflight.ps1 -CheckSourceCi',
    "          'Preflight only: no environment job, OIDC permission, NuGet login, push, tag or release.' >> $env:GITHUB_STEP_SUMMARY"
  ].join('\n'));
}
const workflow = fs.readFileSync(path.join(root, '.github/workflows/publish-nuget.yml'), 'utf8');
check(workflow);
const negatives = [
  workflow.replace('contents: read', 'contents: write'),
  workflow.replace('  actions: read', '  actions: read\n  id-token: write'),
  workflow.replace('    runs-on:', '    environment: nuget-release\n    runs-on:'),
  workflow.replace('  workflow_dispatch:', '  push:'),
  workflow.replace("refs/heads/main", 'refs/heads/other'),
  workflow.replace('persist-credentials: false', 'persist-credentials: true'),
  workflow.replace('-CheckSourceCi', '-CheckSourceCi\n          dotnet nuget push anything'),
  workflow.replace('          GH_TOKEN:', '          TOKEN: ${{ secrets.NUGET_API_KEY }}\n          GH_TOKEN:'),
  workflow.replace('ubuntu-latest', 'self-hosted'),
  workflow + '\n  publish:\n    runs-on: ubuntu-latest\n'
];
for (const [i, candidate] of negatives.entries()) assert.throws(() => check(candidate), `Unsafe workflow mutation ${i} must fail`);
const script = fs.readFileSync(path.join(root, 'scripts/Test-NuGetReleasePreflight.ps1'), 'utf8');
assert.doesNotMatch(script, /(?:Invoke-Expression|Start-Process|SetEnvironmentVariable|setx\b|ACTIONS_ID_TOKEN|nuget\s+push|NuGet\/login|NUGET_API_KEY)/i);
assert.match(script, /& \$gh api --hostname github\.com \$endpoint/);
console.log(`Passed preparation workflow boundary and ${negatives.length} rejected privilege/trigger/command mutations.`);
