import fs from 'node:fs';
import path from 'node:path';
import { requireThat, readZip, sha256, checkRequest, checkPackages } from './model.mjs';

export function checkCandidateProvenance(meta, run, binding, context) {
  requireThat(binding && Number.isSafeInteger(binding.id) && binding.id > 0 && /^[a-f0-9]{64}$/.test(binding.sha256), 'Missing candidate identity');
  requireThat(meta.id === binding.id && meta.expired === false && meta.name === `unpublished-recovery-candidate-${binding.sourceCommit}` && meta.digest === `sha256:${binding.sha256}`, 'Candidate artifact identity differs');
  requireThat(meta.workflow_run?.id === binding.runId && meta.workflow_run?.head_sha === binding.sourceCommit && meta.workflow_run?.head_repository_id === Number(context.repositoryId), 'Candidate artifact provenance differs');
  requireThat(run.id === binding.runId && run.repository?.full_name === 'von12549/ArchSift' && run.head_repository?.full_name === 'von12549/ArchSift' && run.head_sha === binding.sourceCommit && run.head_branch === 'main' && run.path === '.github/workflows/recovery-candidate.yml' && run.event === 'push' && run.run_attempt === 1 && run.status === 'completed' && run.conclusion === 'success', 'Candidate run is not a fresh successful main build');
  requireThat(context.dataCommit === binding.sourceCommit, 'Candidate source input differs');
}
export async function candidateInputs({ gh, policy, reference, context, root }) {
  requireThat(['0.11.2','0.11.3'].includes(reference.version) && policy.version === reference.version && context.version === reference.version, 'Candidate release profile differs');
  const b = policy.candidateArtifact;
  requireThat(b && b.sourceCommit === reference.sourceCommit, 'Candidate/reference source differs');
  const meta = await gh.json(`repos/von12549/ArchSift/actions/artifacts/${b.id}`);
  const run = await gh.json(`repos/von12549/ArchSift/actions/runs/${b.runId}`);
  checkCandidateProvenance(meta, run, b, context);
  const raw = await gh.artifact(b.id);
  requireThat(raw.length === meta.size_in_bytes && sha256(raw) === b.sha256, 'Candidate raw ZIP differs');
  const candidate = await readZip(raw, {maxInput:10_000_000,maxTotal:80_000_000,maxEntries:1500});
  requireThat(JSON.stringify([...candidate.keys()].sort()) === JSON.stringify([...b.files].sort()), 'Candidate archive allowlist differs');
  const files = new Map(reference.packages.map(p => [p.file, candidate.get(`packages/${p.file}`)]));
  files.set('package-payload-manifest.json', fs.readFileSync(path.join(root,`docs/releases/${reference.version}/package-payload-manifest.json`)));
  const request = {schemaVersion:1,version:reference.version,sourceCommit:reference.sourceCommit,sourceTree:reference.sourceTree,workflowCommit:context.sha,repositoryId:policy.repositoryId,ownerId:policy.ownerId,packages:reference.packages,payloadManifestSha256:policy.payloadManifestSha256,acceptanceSha256:reference.acceptedPreview.acceptanceSha256};
  const requestBytes=Buffer.from(JSON.stringify(request,null,2)+'\n');
  checkRequest(requestBytes,context.requestSha256,reference,policy,context);
  files.set('release-request.json',requestBytes);
  requireThat(sha256(files.get('package-payload-manifest.json'))===policy.payloadManifestSha256,'Candidate payload manifest differs');
  await checkPackages(files,reference,JSON.parse(files.get('package-payload-manifest.json')));
  return files;
}
