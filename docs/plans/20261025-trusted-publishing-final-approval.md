# TA05 final publication approval preparation

Authorized on 2026-10-25: implement, test, review by PR, merge, refresh the original-byte public preview and produce an external final release confirmation card. No NuGet credential acquisition or push, repository switch activation, tags/Release, installer, local Guard/dotnet or persistent host changes are authorized. Preserve the accepted original packages, all prior evidence and existing IFX gates.

## Implementation and tests

Prepare the committed publication capability while the operational repository variable ARCHSIFT_NUGET_PUBLISH_0110 remains absent/false. Publication requires all three: an explicit fresh main-only publish dispatch with exact identities, the variable equal to true, and the existing nuget-release human environment approval. Preview and default preflight cannot acquire OIDC/NuGet credentials. This preparation does not authorize any of those activation actions.

Verify the uploaded original input artifact before approval in preview and publish modes. Add a credential-free hosted audit job before the protected publisher: fresh clone of original source 8b721f44e90a41285fa74a6f72576c7f6996ef7d/tree 2c18d18145beb52a917a3884ddd7f442ec486967, fresh isolated caches/home, locked full-solution restore from official NuGet sources, audit enabled/all/moderate with warnings as errors and no suppressions. Retain official advisory-data hashes and effective project audit settings. Bind the audit report to the exact current run, workflow, data/request and original input artifact. The protected publisher independently downloads and validates both artifact ZIP digests before login and rejects reports older than 24 hours. An expired approval wait requires a new run, never a credential refresh or audit bypass.

Retain all prior package/archive/context/CI/provenance, sequential publication, ambiguous-outcome, credential redaction, session-baseline and host-isolation tests. Add fresh/stale/mismatched audit rejection, credential injection, both preview/publication capability states and structural workflow mutations. Execute only Node/PowerShell metadata and synthetic tests locally; use normal GitHub Windows/Ubuntu CI for SDK tests and a hosted Ubuntu preview for the real fresh audit.

## Migration and confirmation

Merge through the normal PR path after exact-head checks; verify actual merged-main CI independently. Keep the original package source and NR04 acceptance immutable. Refresh only release-request.json for the new workflow commit on a new independent data branch; independently verify and reuse the six unchanged prior public blobs. Preserve the old branch, request, preview ZIP and all originals.

Run the default closed preflight and then a fresh preview. The input and audit jobs must succeed, publisher and public-verification must remain skipped, and no environment approval or token exchange may occur. Independently download both official artifacts and verify raw ZIP SHA-256, exact request and five package hashes, audit bindings, effective policy and freshness. Preserve the original downloads, ledgers, failed attempts and readbacks outside source roots.

Produce a final external confirmation card containing NuGet owner/source/version, ordered five package hashes, immutable source/tree, current workflow/data/request identities, preview run and both artifact identities/digests/expiry, audit completion/freshness deadline, environment reviewer/branch/bypass settings, switch-off proof and manual preconditions. Mark live NuGet policy/ID rights and actual OIDC exchange unproven until the separately authorized publication phase. The card must not fill human approval answers.

## Future human operation and rollback

After preparation, stop and wait for separate authorization. The operator must read back NuGet policy ArchSift (XiaolongFeng, von12549/ArchSift, publish-nuget.yml, nuget-release, ArchSift.*, Push new packages and versions), confirm the five exact 0.11.0 submissions and official source, and authorize the operational switch/fresh publish dispatch. Review that new run's input ZIP and fresh audit artifacts before Approve and deploy; that click starts credential-capable publication. Historical preview artifact IDs are not publisher inputs in a later run.

Before credentials: cancel the run, keep/restore the repository switch closed and preserve evidence. An inverse reviewed PR may disable code capability without changing packages or earlier references; rehearse inverse-patch applicability without applying it. Do not automatically modify environment rules, trust policy or host PATH. After any future partial/uncertain push, stop later pushes, close the switch and reconcile official registry state read-only. Never retry blindly, use skip-duplicate as evidence, replace immutable versions or assume unlisting deletes bytes. Owner-operated unlisting (separate permission) and a newly qualified patch version are the recovery routes. Keep old installers/configuration/state and do not resume local Guard/dotnet.

## Retained CI failure and correction

The Windows PR run 40107539968 failed an existing cancellation-output fixture: cancellation 700 ms after process creation could precede cold PowerShell output. The prior head passed both platforms and the failed run retained equal host checks. Preserve that failure; replace the fixture timer with a bounded explicit flushed-output readiness marker, then cancel and require the same cancellation, retained output and owned-tree cleanup assertions. Product/package bytes and test coverage are unchanged. Fresh exact-head/main CI must pass; no rerun-only or bypass substitutes for verification.
