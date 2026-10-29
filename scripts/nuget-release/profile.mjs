import { requireThat } from './model.mjs';

export function checkReleaseProfile(policy, version) {
  requireThat(['0.11.0', '0.11.2', '0.11.3'].includes(version), 'Unreviewed release profile');
  requireThat(policy.version === version && policy.originalReference === `docs/releases/${version}/trusted-publishing-reference.json`, 'Reference/profile differs');
  if (version !== '0.11.0') {
    requireThat(policy.state === 'FINAL_IDENTITIES_FROZEN', `Final ${version} identities not frozen`);
  }
  if (version === '0.11.3') {
    requireThat(typeof policy.realAssetTransferEnabled === 'boolean' && typeof policy.publicationEnabled === 'boolean', 'Release capabilities must be typed booleans');
    requireThat(policy.versionSwitch === 'ARCHSIFT_NUGET_PUBLISH_0113', 'Wrong version-specific switch');
    requireThat(/^[a-f0-9]{64}$/.test(policy.originalReferenceSha256 ?? '') && /^[a-f0-9]{64}$/.test(policy.payloadManifestSha256 ?? ''), 'Final identity hashes missing');
  }
}
