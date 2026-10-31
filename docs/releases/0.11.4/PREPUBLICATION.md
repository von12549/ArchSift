# ArchSift 0.11.4 prepublication readiness

Date: 2026-10-31 (Australia/Sydney).

Status: the unpublished product candidate and IFX secondary-machine acceptance passed; final main-source publication inputs are not frozen. This document is a preparation/approval card, not publication authorization.

## Accepted candidate

- Product source: `b7dd87f7f8d073a3fde46bd7068c8911b832972d`, tree `d6a861dded687955df61645d6fe50bf17d3713f9`, clean build-08. All five original archives and every internal file were rehashed during this preparation. Full identities are in [candidate inventory](candidate-inventory.json) and [payload manifest](candidate-payload-manifest.json).
- IFX source: `9fd3e267533988a948359b2d3c3b2ad502f9a86a`; all four jobs in CI run `41157989728` passed. Earlier qualified SDK 10.0.303, 10.0.300 and 10.0.403 lanes each executed all 493 cases; the exact formal lane and separate 105-project/CLI/Web audit passed.
- The user's secondary Windows machine passed one integrated batch: four human local-console UI reviews, automatic 13-rule/92+401 inventory, 493 native cases with no skip/missing/duplicate, fresh official 105-project/distribution audit and four paired safe closeouts. Its 87-member evidence ZIP is `c6a5e19cdbd12979653c7df522a6f843d1bddb00393447a48863f1344190841d`. [Sanitized review](evidence/secondary-machine-candidate-review.json) preserves the scope and digest.
- This secondary-machine batch used SDK 10.0.303 and an existing default StorageRoot. Absent-root creation was separately tested in the development qualification. The secondary-machine result does not certify future main-source packages, public acquisition, actual disconnection or every eligible future SDK.

## Closed preparation

The 0.11.4 publishing profile has both `realAssetTransferEnabled=false` and `publicationEnabled=false`. Its state is `AWAITING_MAIN_SOURCE_AND_FINAL_IDENTITIES`; missing final reference, payload digest and hosted candidate identity are intentional. The actual driver rejects before reference/network access in preflight, preview and publish modes. An operational switch alone cannot open these capabilities.

The [proposed workflow](publish-nuget.proposed.yml) retains main-only execution, first-attempt manual dispatch, the protected `nuget-release` environment, fresh credential-free audit, immutable artifact verification, sequential original submissions and independent served signature/owner/payload verification. The active `.github/workflows/publish-nuget.yml` remains on historical 0.11.3.

The versioned verifier now accepts 0.11.4 while retaining historical behavior. In addition to regression, package-integrity and consumer-lifecycle evidence, final 0.11.4 qualification requires source/package-bound IFX SDK matrix, independent audit and secondary-machine lanes. A different final package set cannot inherit old proof by relabeling a PASS.

## Remaining release blockers

1. The current product source is an accepted branch candidate, not the required fresh successful `main` push candidate. PR run `41068122581` has artifact `12984201165`, but its merge-tree name/source identity and `pull_request` event make it ineligible for the protected publication transaction. Keep that restriction.
2. The final `main` source CI, canonical five submissions, full payload manifest, accepted qualification/reference and version-specific request must be frozen and verified. Never infer ZIP-byte equality from a shared source tree.
3. The final closed publication workflow must pass normal PR/main CI and a credential-free preview plus a fresh hosted audit. This preparation does not claim that hosted preview has run.
4. Reconfirm official version absence, protected environment and NuGet account trust immediately before the eventual authorized transaction. The current five 0.11.4 HEAD checks returned 404; the operational switch is absent and the main-only environment has required review and no administrator bypass.

## Next source-integration approval

Request one preparation-only authorization for ArchSift product PR 47 and the separate closed-release-readiness PR: source integration, successful main CI, canonical candidate/payload comparison, necessary Agent requalification, final disabled-publication binding and credential-free hosted preview/audit. IFX PR 32 remains a private candidate until actual served packages are available.

The preferred source integration preserves the accepted product commit if repository policy permits a non-forced fast-forward from current main `340f68e1939e3beedc9460b09186b623231ff155` to `b7dd87f7f8d073a3fde46bd7068c8911b832972d`. Recheck ancestry and the exact remote tip at execution; do not bypass repository review/rules or force an update. A normal merge producing a different commit requires an explicit new freeze and affected evidence. The release-tooling PR can then follow the normal reviewed merge flow without changing the original product source reference.

Download the fresh main artifact and compare all five raw archives and every payload entry with build-08. Reuse evidence only through explicit verified identity or payload-equivalence records; rerun affected tests using the delegated Agent route. Keep old submissions and evidence immutable. A spare-machine round trip is needed only if affected coverage cannot be established by those checks and Agent tests.

This approval excludes NuGet switch activation, OIDC/login or publishing credentials, NuGet push, tag/GitHub Release creation, IFX main merge, installer changes and live daily cutover. Before those actions, present a final publication card containing literal source/workflow/artifact/request/package digests and fresh preview/audit evidence. Do not approve a guessed future hash.

## Publication and IFX completion order

After separate explicit publication confirmation: activate only the exact 0.11.4 transaction under the existing human environment review, submit the frozen originals in Contracts/Core/ArchUnit/Testing/Cli order, stop on an uncertain response, anonymously verify served signatures/owners/full payloads, and restore the version switch to its originally absent state. Never rebuild or silently retry a possibly published version.

Then atomically bind IFX to actual served signed identities (separate submission and served hashes), public-only restore, lock/config/provenance/host contracts, full 493-case CI, independent audit and final root-entry Agent review. Prepare its final PR/activation card from those results. The current local feed and unpublished provenance must not be represented as final public clone-and-run delivery.
