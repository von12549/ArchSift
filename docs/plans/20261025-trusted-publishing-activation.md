# Trusted Publishing activation preparation: TA00–TA03

The user authorized implementation, tests, PR, CI and merge for TA00–TA03. Real asset uploads, publishing credentials, NuGet publication, tags, Releases and a new installer remain prohibited. Local Guard/dotnet remains stopped; no persistent host configuration changes are allowed.

## Implementation contract

Retain the original accepted package source `8b721f44e90a41285fa74a6f72576c7f6996ef7d` and the immutable [preparation reference](../releases/0.11.0/trusted-publishing-reference.json). The new workflow commit identifies release tooling only. Do not rebuild, rewrite or upload the accepted submission archives during TA00–TA03. Preserve their earlier machine/human acceptance (4/5), original configuration/evidence and IFX gates.

The [new activation record](../releases/0.11.0/publishing-activation.json) sets both `realAssetTransferEnabled` and `publicationEnabled` to Boolean false. A repository variable cannot override these committed locks. They require a future reviewed change under separate user authorization; all request, workflow and artifact identities must then be refreshed. `ARCHSIFT_NUGET_PUBLISH_0110` remains absent or false. This additional code lock intentionally prevents accidental preview uploads and live login/push in the current scope.

The manual-only [workflow](../../.github/workflows/publish-nuget.yml) defaults to preflight on main. Its read-only job checks original source/tree/CI, actual workflow-main CI and the existing environment's reviewer, Branch/main and no-admin-bypass rules. It checks no local release archives and obtains no publishing credential. It reports that distinction explicitly.

Future preview/publish code validates a data-only commit, exact request hash, accepted package hashes and payload-manifest hash. It never runs staged scripts. It constructs an immutable same-run artifact and records ID/digest. Publisher preparation redownloads the raw ZIP and independently checks its hash, provenance, safe entry allowlist and every submission. Wrong repository IDs, main moving, malformed input or rerun attempt fail closed.

The publish job additionally requires explicit mode, exact switch value true and the existing `nuget-release` approval. Only it receives id-token-write. NuGet/login is pinned to a reviewed full commit; its masked temporary output is passed to a narrowly scoped push child environment. It is never a durable secret, command-line argument, cross-job output or artifact. Child-only CLI home is always paired with `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`. The SDK is pinned to 10.0.303 on a GitHub-hosted runner; local SDK execution is not resumed.

The publisher is sequential (Contracts, Core, ArchUnit, Testing, Cli), bounded, and stops on uncertain outcomes. After each accepted push it compares every nonsignature payload entry and requires a real NuGet signature, repository certificate/owner restriction and gallery owner evidence. It records unsigned submission, signed served and NuGet content hashes separately. A separate job repeats public signature/owner/payload verification without OIDC or a NuGet key. This job does not replace NR06 consumer/UI acceptance, audit, offline-bundle verification or final human acceptance.

The original preparation-only workflow checker is strengthened into a structural YAML capability validator with pinned parser dependencies and mutation tests. Existing 18 preflight cases remain; new synthetic/mock tests cover context, artifact/archive boundaries, partial failures, owner-vs-author distinction and credential redaction. The model never treats a synthetic `.signature.p7s` fixture as cryptographic proof. Existing product, architecture, baseline and IFX checks remain unchanged.

## Stage exit criteria

| Stage | Exit criterion |
| --- | --- |
| TA00 | Read-only input freeze; exact original hashes and host baseline preserved; existing main/environment/IFX and dirty-checkout state recorded. |
| TA01 | Narrow reviewed workflow/helpers/reference/guide change; committed locks false; no package payload changes. |
| TA02 | Synthetic/mock and unchanged applicable Windows/Ubuntu CI pass on the reviewed head; failures retained and fixed, no weakened assertions. |
| TA03 | Normal merge; actual-main CI passes; one default closed preflight succeeds. Publish and public-verification jobs are skipped, and the real-submission uploader is skipped. |

## Future authorization boundaries

TA04 requires explicit authorization to expose exact accepted archives. Proposed transport is a unique data-only orphan commit/branch in the public repository, then an identified Actions artifact. Deleting that branch does not guarantee public Git-object erasure. No staging commit, real request or real artifact is created by this preparation. Do not silently rebuild or substitute another transfer route.

The future data commit contains exactly five packages plus `release-request.json` and `package-payload-manifest.json`. The request keys are schemaVersion, version, sourceCommit, sourceTree, workflowCommit, repositoryId, ownerId, packages (the exact ordered original reference objects), payloadManifestSha256 and acceptanceSha256. Pin its exact SHA-256 and the data commit; no mutable-ref resolution, script paths or URLs are accepted. Keep private IFX and host evidence out of public data. The original payload manifest must retain its original hash; any sanitized derivative needs a new explicitly reviewed binding.

Before NR05, refresh private NuGet policy readback and package-ID rights; the user authorizes the exact account, source/workflow/data/request/artifact identities and five pushes, enables the reviewed locks/switch, dispatches from main, then separately approves the concrete environment deployment. The current single-maintainer environment permits self-review but does not bypass that separate click. Actual OIDC authentication remains untested until this later authorization. A failed exchange has no manual-key fallback.

Per-job switch values are evaluated by GitHub and checked again by the job. Closing a repository variable is not a transactional revocation of a queued/running job or issued token: reject/cancel pending work as well. These workflow gates do not create branch protection where none exists. No admin bypass or IFX policy changes are introduced.

Before publication, failures leave all originals intact and may be reverted by an inverse PR. After partial publication, stop, reconcile exact server state and obtain new approval before any resume. Reruns are rejected; no skip-duplicate/overwrite/automatic refresh. The user can separately authorize NuGet UI unlisting/deprecation; exact-version restores can continue after unlisting. Fix defective public bytes in a newly qualified version. No automatic PATH repair, live IFX adoption, installer or old-version deletion.

NR06 includes fresh public cold network/offline consumer checks and audit. Additional Guard integration stays human-operated, one reviewed command block at a time on an approved external disposable host; local Guard/dotnet remains stopped unless separately authorized. NR07 tag/Release needs another decision, targets original package source, is package-only and uses make_latest=false. NR08 requires final human acceptance, preserved evidence and closure of the release switch.

References: [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), [deployment review](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/review-deployments), [repository signatures](https://learn.microsoft.com/en-us/nuget/api/repository-signatures-resource), [NuGet configuration/trusted signers](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file), [unlisting limits](https://learn.microsoft.com/en-us/nuget/nuget-org/policies/deleting-packages).
