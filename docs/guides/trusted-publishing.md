# ArchSift 0.11.0 public preview: NuGet publication remains closed

TA00–TA03 completed through PRs #31/#32 with exact-head/main Windows/Ubuntu CI and a default closed preflight. TA04 is now authorized: review/merge the preview tooling, transfer five original accepted packages to an independent public data branch and generate a verified Actions preview. Local Guard/dotnet remains stopped. Publishing credentials, NuGet push, tags/Release, a new installer and persistent host changes remain prohibited.

## What is enabled

[publishing-activation.json](../releases/0.11.0/publishing-activation.json) enables only **realAssetTransferEnabled**. **publicationEnabled remains false** and repository switch **ARCHSIFT_NUGET_PUBLISH_0110 remains absent/false**. Inputs and the repository variable cannot bypass the committed publication lock. [TA04 implementation/verification plan](../plans/20261025-trusted-publishing-preview.md).

The manual-only [workflow](../../.github/workflows/publish-nuget.yml) remains main-only and defaults to **preflight** with empty identity inputs. That mode checks source/PR/main CI and existing environment rules without transferring package bytes. Explicit **preview** requires exact workflow SHA, independent data commit and request SHA-256, then validates and uploads only seven allowlisted files and independently verifies the downloaded raw artifact ZIP. Preview has no OIDC permission, NuGet login, SDK execution or public registry push. Both publisher and public NuGet verification jobs remain skipped.

The [original reference](../releases/0.11.0/trusted-publishing-reference.json) is historical immutable evidence. Original package source is `8b721f44e90a41285fa74a6f72576c7f6996ef7d`, tree `2c18d18145beb52a917a3884ddd7f442ec486967`; five package hashes and NR04 human acceptance (4/5) remain unchanged. Workflow tooling main and the data-only commit are separate identities. Do not rebuild or edit original submissions to make a preview pass.

## Public data and operator preview

The explicitly authorized transfer uses [stage-preview.mjs](../../scripts/nuget-release/stage-preview.mjs), run against a clean, reviewed and CI-verified main checkout with an external original asset directory, fresh external evidence directory, exact workflow commit and unique `release-inputs/archsift-0.11.0/<transaction-id>` branch. Its explicit public-upload flag records the operation scope; no new human permission is inferred from another phase. It uses existing GitHub repository authorization, not a NuGet credential. It creates only Git blobs/tree/orphan commit/new heads ref, never a tag, Release or merge of binaries into main. Do not reuse/force-update an existing data branch.

The public tree contains exactly the five original nupkg files, original hash-bound `package-payload-manifest.json` and generated `release-request.json`. Request fields: schemaVersion, version, sourceCommit, sourceTree, workflowCommit, repositoryId, ownerId, exact ordered packages, payloadManifestSha256 and acceptanceSha256. Raw host/IFX/private acceptance evidence and credentials are excluded. Source/metadata are never executed. Public branch removal cannot guarantee Git-object/cache erasure; preserve this branch and the operation ledger.

After public blob/ref readback succeeds, run Actions → **ArchSift NuGet preview (publication closed)** → branch **main**, mode **preview**, and enter the reviewed workflow commit, exact data commit and exact request digest. Do not dispatch from the data branch and do not select publish. Verify the completed run is a fresh first attempt on the expected main, preflight succeeds, publisher/public-verification are skipped, and the artifact is independently verified after upload.

Review run/artifact ID, raw ZIP digest/size/expiry, data commit, request digest, original source/tree, current workflow commit and all five package digests. An upload step or display name alone is insufficient. Actions artifacts expire after seven days; preserve an independently downloaded copy and the public data commit. A later changed workflow requires renewed request/preview identity bindings.

## Configuration and later publication boundary

Operator-reported NuGet policy **ArchSift** is Active: package owner **XiaolongFeng**, GitHub **von12549/ArchSift**, **publish-nuget.yml**, **nuget-release**, **ArchSift.***, Push new packages/versions, no Unlist/relist. This is not proof of live OIDC or package-ID rights. GitHub environment remains reviewer **von12549**, no admin bypass, exactly **Branch/main**; sole-maintainer self-review prevention remains off but actual publication still requires a separate approval click. Preserve existing IFX gates and host rules; no new secrets/settings are needed for preview.

TA04 grants no release approval. Before any future NuGet credential exchange/push, separately review and authorize the publication-enabling change, current policy/account/rights and exact new workflow/request/artifact identities; enable the reviewed publication lock/switch and approve that exact environment deployment. No manual API-key fallback, blind retry, skip-duplicate-as-proof or public-byte replacement. The current public preview is unsigned submission evidence, not proof of a NuGet-served signed package or new consumer/UI acceptance.

On any transfer/preview mismatch, stop and preserve the ledger, original files and failed run. Reconcile ambiguous Git writes; do not overwrite a ref or silently substitute packages. A reviewed inverse PR can close transfer before further work. TA05–TA09, NR05–NR08, actual signed/public consumer/audit checks and final human acceptance remain separate. No automatic local Guard/dotnet resumption or IFX adoption follows a successful preview.
