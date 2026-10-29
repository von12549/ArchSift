# N03 CLI package hash mismatch acceptance

Recorded 2026-10-30 (Australia/Sydney). Status: **passed-new-machine-cli-hash-mismatch-rejection**.

The operator supplied `N03-tampered-cli-d026ebe0ed924d2ca6a775082bf49ecc` from the disposable new-machine fixture. Prepare exited 1 with `Candidate source package differs from approval.` and a no-fallback child error. stdout is empty. result.json reports expected-rejection-verified; its 13 checks match checks.json and are all true. The before/result helper SHA-256 matches the reviewed N03 script.

Seven manifest entries match their SHA-256 and byte lengths. All five package identities and pre-mutation hashes match the immutable delivery approval. Four supplied package files are unchanged. The CLI has the approved length of 2906316 bytes and recorded mutated SHA-256 `9932142c2a5283100c1eef9938f69fc4a8bfe6461fdaf382eb7323a3ece2c528`. Independent comparison with the approved original confirms exactly one byte changed, at offset 2906315 from 0 to 1. mutation.json and result.json agree.

The four protected configuration hashes, copy-manifest digest and host baseline agree with retained N02 evidence. The operator checker records unchanged original/copied inputs and four other case packages, preserved CLI mutation, exactly five files, both disposable state roots empty and host equality. All SDK/restore/product/architecture-test/activation/commit/publication flags are false. This is supplied-evidence review, not live remote filesystem inspection or OS process tracing. No separate checker process exit-code transcript was supplied; the successful result, thirteen true checks and completed inventory establish the scoped acceptance.

Eight evidence files and five package files were archived byte-for-byte under `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-host-0.11.3-rc.1-integration-evidence\N03-acceptance-20261030\original-case`. The independent report is `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-host-0.11.3-rc.1-integration-evidence\N03-acceptance-20261030\verification.json`, SHA-256 `f8796862111f63be63e8306ff6ab041064de4741dbfc640fe9a30beede5be755`. Shared originals, approved delivery bytes and prior evidence were not changed. No entry, SDK, restore or product was executed during this review.

## Remaining negative validation

| Group | Scope | Status |
| --- | --- | --- |
| N04 | Generated local configuration modification and preservation on rejection | Pending |
| N05 | Occupied operation/workspace state and conflicting-use rejection | Pending |
| N06 | Restored package and CLI/Web payload integrity rejection | Pending |

These three groups summarize already-planned work; they do not promise exactly three command invocations or add new scope. Concrete variants and prerequisite fixture preparation will be reviewed sequentially. N01-N03 are closed; SDK incompatibility and recovery have prior retained evidence. Optional offline qualification is skipped. Architecture tests, migration/activation, commits and publication are excluded. Public-feed-only clone readiness and release adoption remain separate from this negative-validation count.
