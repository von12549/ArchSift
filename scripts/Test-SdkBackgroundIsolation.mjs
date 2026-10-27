import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
// SDK 10.0.303 uses bool.TryParse, which does not treat the string "1" as true.
const values = text => [...text.matchAll(/DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE["'\]]*\s*(?:=|:)\s*["']?([a-z0-9]+)/giu)].map(m => m[1]);
for (const source of [
  `start.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1";`,
  `$child=@{DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='false'}`,
  `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE: '1'`,
  `'-e','DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1'`
]) assert.ok(values(source).some(v => v !== 'true'), 'Reject ineffective SDK Boolean values in each supported launcher syntax.');

const files = execFileSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], { cwd: root, encoding: 'utf8' })
  .split('\0').filter(f => /^(src|templates|scripts|\.github)\//u.test(f) && /\.(cs|ps1|yml)$/u.test(f));
let assignments = 0;
for (const file of files) {
  for (const value of values(readFileSync(path.join(root, file), 'utf8'))) {
    assignments++;
    assert.equal(value, 'true', `${file}: use the literal SDK Boolean true in child settings.`);
  }
}
assert.ok(assignments >= 24, 'The product, project bootstrap, CI and package builders must remain covered.');
for (const file of ['src/ArchSift.Core/SafeProcess.cs', 'src/ArchSift.Cli/Workspace/WorkspaceHost.cs',
  'src/ArchSift.TestGate/TestGateService.cs', 'src/ArchSift.TestGate/TestRunReportWriter.cs', 'src/Shared/UiProcess.cs']) {
  const source = readFileSync(path.join(root, file), 'utf8');
  assert.deepEqual(values(source), ['true'], `${file}: one explicit background-refresh guard is required.`);
  assert.match(source, /DOTNET_ADD_GLOBAL_TOOLS_TO_PATH/u);
}
process.stdout.write(JSON.stringify({ status: 'passed', files: files.length, assignments, negativeSyntaxCases: 4,
  runtimeCoverage: 'SafeProcess child environment is exercised by hosted integration tests; native disconnected evidence remains separate.' }) + '\n');
