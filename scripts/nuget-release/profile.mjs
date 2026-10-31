import { requireThat } from './model.mjs';

export function checkReleaseProfile(policy, version) {
  requireThat(['0.11.0', '0.11.2', '0.11.3', '0.11.4'].includes(version), 'Unreviewed release profile');
  requireThat(policy.version === version && policy.originalReference === `docs/releases/${version}/trusted-publishing-reference.json`, 'Reference/profile differs');
  if (version !== '0.11.0') {
    requireThat(policy.state === 'FINAL_IDENTITIES_FROZEN', `Final ${version} identities not frozen`);
  }
  if (['0.11.3', '0.11.4'].includes(version)) {
    requireThat(typeof policy.realAssetTransferEnabled === 'boolean' && typeof policy.publicationEnabled === 'boolean', 'Release capabilities must be typed booleans');
    requireThat(policy.versionSwitch === ({'0.11.3':'ARCHSIFT_NUGET_PUBLISH_0113','0.11.4':'ARCHSIFT_NUGET_PUBLISH_0114'})[version], 'Wrong version-specific switch');
    requireThat(/^[a-f0-9]{64}$/.test(policy.originalReferenceSha256 ?? '') && /^[a-f0-9]{64}$/.test(policy.payloadManifestSha256 ?? ''), 'Final identity hashes missing');
    if (version === '0.11.4') {
      const b = policy.candidateArtifact;
      requireThat(b && Number.isSafeInteger(b.id) && b.id > 0 && Number.isSafeInteger(b.runId) && b.runId > 0 && /^[a-f0-9]{40}$/.test(b.sourceCommit ?? '') && /^[a-f0-9]{64}$/.test(b.sha256 ?? '') && Array.isArray(b.files) && b.files.length > 0, 'Frozen main candidate identity missing');
    }
  }
}
