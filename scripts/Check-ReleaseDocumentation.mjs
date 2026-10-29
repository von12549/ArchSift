import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const languagePath = 'docs/releases/0.10.1/documentation-language.json';
const language = JSON.parse(readFileSync(path.join(root, languagePath), 'utf8'));
const finalEvidence = JSON.parse(readFileSync(path.join(root, 'docs/releases/0.11.3/documentation-evidence-exceptions.json'), 'utf8'));
language.exceptions.push(...finalEvidence.exceptions);
const contract = JSON.parse(readFileSync(path.join(root, 'docs/releases/0.10.1/release-contract.json'), 'utf8'));
const exceptions = new Map(language.exceptions.map(entry => [entry.path, entry]));
const files = [...new Set(execFileSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], {
  cwd: root, encoding: 'utf8'
}).split('\0').filter(Boolean))].sort();
const errors = [];
const dispositions = [];
const han = /[\u3400-\u9fff]/u;
let documents = 0;
let localLinks = 0;

for (const relative of files) {
  if (!/\.(?:md|html|json|ya?ml|txt|template)$/i.test(relative)) continue;
  const full = path.join(root, relative);
  const bytes = readFileSync(full);
  const text = bytes.toString('utf8').replace(/^\uFEFF/u, '');
  if (relative.startsWith('src/') || relative.startsWith('tests/')) continue;
  documents++;
  if (han.test(text)) {
    const exception = exceptions.get(relative);
    const sha256 = createHash('sha256').update(bytes).digest('hex');
    if (!exception || exception.sha256 !== sha256) errors.push({ path: relative, reason: 'Unclassified Han prose or changed immutable evidence.' });
    else dispositions.push({ path: relative, classification: exception.classification, sha256 });
  }
  if (!relative.endsWith('.md')) continue;
  let fenced = false;
  for (const [index, line] of text.split(/\r?\n/u).entries()) {
    if (/^\s*```/u.test(line)) { fenced = !fenced; continue; }
    if (fenced) continue;
    for (const match of line.matchAll(/\]\(([^)]+)\)/gu)) {
      const link = match[1];
      if (/^[a-z][a-z0-9+.-]*:/iu.test(link) || link.startsWith('//')) continue;
      let local;
      try { local = decodeURIComponent(link.split('#')[0]); }
      catch { errors.push({ path: relative, line: index + 1, reason: 'Malformed link encoding.' }); continue; }
      if (!local) continue;
      const target = path.resolve(path.dirname(full), local);
      const underRoot = path.relative(root, target);
      if (underRoot === '..' || underRoot.startsWith(`..${path.sep}`) || path.isAbsolute(underRoot)) continue;
      localLinks++;
      if (!existsSync(target)) errors.push({ path: relative, line: index + 1, reason: 'Missing local link target.', link });
    }
  }
}
for (const entry of language.exceptions) {
  const target = path.join(root, entry.path);
  if (!existsSync(target) || createHash('sha256').update(readFileSync(target)).digest('hex') !== entry.sha256)
    errors.push({ path: entry.path, reason: 'Immutable exception missing or changed.' });
}
for (const relative of contract.bundledDocumentation.requiredFiles) {
  if (!existsSync(path.join(root, relative))) errors.push({ path: relative, reason: 'Required bundled guide missing from source.' });
}
const result = {
  schemaVersion: 1, status: errors.length ? 'failed' : 'passed', scope: 'source-document language and local-file links',
  filesConsidered: files.length, proseBearingDocuments: documents, localLinksChecked: localLinks,
  originalEvidenceDispositions: dispositions, errors,
  limitations: ['Does not certify prose quality, remote URLs, Markdown anchors or generated package contents.', 'Independent final user guidance/UI acceptance remains separate.']
};
if (process.argv[2]) writeFileSync(path.resolve(process.argv[2]), `${JSON.stringify(result, null, 2)}\n`);
process.stdout.write(`${JSON.stringify({ status: result.status, documents, localLinks, immutableEvidenceExceptions: dispositions.length, errors })}\n`);
process.exitCode = errors.length ? 1 : 0;
