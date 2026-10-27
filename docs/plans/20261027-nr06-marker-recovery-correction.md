# NR06 atomic-marker recovery correction

On 2026-10-27 the user explicitly authorized product repair, regression tests, normal PR/CI/merge, candidate-package preparation and human-operated spare verification. Public publication remains excluded. No publishing switches, credentials, tags, Releases, live IFX policy, host environment, PATH or Profiles may change; author-host Guard/dotnet remains prohibited.

## Native finding

Original package source: 8b721f44e90a41285fa74a6f72576c7f6996ef7d. Native run p01-20261027T020918Z-d5876953, seal f2872d2dd235682320508e3e56d842d9127bf84f6260d76f134df4626cde7676, contains 65 sealed files. Central read-only review passed ten evidence checks: exact certificate continuation and stable host; retained-process precommit kill, exit -1, valid journal/configuration, no committed marker and no remaining child. Native status remains INCONCLUSIVE because recovery was not executed.

The extra zero-byte workspace-state.json.1ebd2303d047483cb0bfa2b97d1342d9.tmp is consistent with Atomic's own temporary naming contract. The published recovery whitelist rejects it. No temporary was deleted or moved, no retry was run, and no manual cleanup is counted as recovery. Original 0.11.0 evidence remains unqualified for this observed recovery state.

## Repair and regressions

Only after exact journal equality, recognize the root-level reserved lowercase GUID marker-temporary shape and reject links. Retain its opaque bytes in place, including empty, partial and unrelated content. A filename does not establish ownership. Never deserialize, promote, overwrite or delete this file. Reconstruct the marker using expected transaction identity and the verified generated configuration. Unknown/nested/lookalike names and changed journal/configuration remain rejected.

Twelve new integration cases cover empty/truncated/untrusted bytes, five unknown/lookalike names, changed journal/configuration, multiple temporary files and a symbolic link to an external file. Positive cases require unchanged temporary/config bytes, exact expected marker, journal retirement, ready doctor and repeated-init preservation. Negative cases preserve evidence and do not commit a marker. Existing lease, unknown-root, recovery, migration/rollback and environment guards remain.

The marker-write shape is exercised deterministically using the existing test fixture's exact marker-to-journal staging, then reserved temporary injection; this is explicitly a staged regression, not a real kill. Native process-interruption evidence remains a separate spare gate. Partially written generated configuration remains a conservative refusal outside this narrow patch.

## Version and candidate identity

Development version becomes 0.11.1, distinct from all immutable public 0.11.0 bytes. On 2026-10-27 read-only official NuGet version indexes reported 0.11.0 latest and no 0.11.1 for all five product IDs; this is not a promise of future public availability. Existing Setup ZIP allowlists remain unchanged because this is a NuGet-only candidate.

Initial CI exposed a legacy launcher fixture coupled to the current product version. The managed installation contract deliberately rejects unknown ZIP versions and requires matching launcher/selection identity. Preserve both checks: Windows development checks compile current-source Web into separate external intermediate/output directories under the historical 0.11.0 fixture identity, then pass that path only to the integration-test child. The fixture asserts its binary version. This is synthetic managed-launcher coverage, not a rebuilt public package or 0.11.1 candidate acceptance. Normal solution tests and candidate packaging retain 0.11.1. Failed runs remain retained.

The new read-only candidate workflow has no publishing environment, secrets, OIDC, release command or installer. It uses clean checked-out source, SDK 10.0.303 and compatible 10.0.13 frameworks, explicit hosted prerequisite acquisition followed by local-feed locked build/pack, and host hash checks. Five NuGet archives, exact source/tree, byte hashes and logs are retained as workflow artifacts. Artifacts are unpublished candidates, not publicly released or repository-signed packages. The build script resolves release notes from the actual version.

## Acceptance boundaries

Require the normal Windows/Linux regression and all existing PR checks, candidate build, independent evidence review and main CI after merge. Do not treat the unchanged public 0.11.0 consumer matrix as 0.11.1 candidate qualification. Candidate-specific package provenance, closure, embedded Web and recovery checks are required before spare use.

Preserve the original V3 root and all 53 work-file records. The next human-operated batch uses a new target/state, reviewed candidate hashes and one owned-process interruption followed by conditional recovery/doctor. Retained temporary bytes must match before/after. Staged regression and actual interruption receipts are separately reported. Keep certificate comparison consent explicit for the named batch and stop on any new drift.

Fresh consumer/CLI-Web audit, Windows strict offline and independent human UI remain pending. Ubuntu strict offline is DEFERRED_BY_USER. Full NR06 is not passed. No automatic PATH repair, cache purge, broad process termination or same-version package replacement is permitted.
