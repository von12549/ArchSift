# N04/N05/N06 scoped batch acceptance

Recorded 2026-10-30 (Australia/Sydney). Status: **passed-new-machine-N04-N06-scoped-batch**. The user explicitly reported checker exit code 0 and supplied `N456-cc4cc2e391d8403b8b50cbffd8effa9b` through the shared evidence location. This exit code is operator-reported, not an independently captured checker-process transcript.

## Verified results

| Subcase | Checks | Reviewed behavior |
| --- | --- | --- |
| N04-local | 10/10 | Explicit local-configuration rejection; changed runtime path preserved; other configuration unchanged |
| N04-nuget | 10/10 | Explicit generated-NuGet rejection; appended comment preserved; local configuration unchanged |
| N05-operation | 11/11 | Real sharing violation, unchanged revision during occupancy and no new run; successful Prepare after release |
| N06-archive | 14/14 | Cache CLI archive rejection at package-verification before product dispatch |
| N06-web | 14/14 | Web DLL integrity rejection at package-verification before product dispatch |

All 59 checks are true and each individual result agrees with the suite result. Ten unique operation closeouts were verified against their copied evidence: six successful prerequisite/recovery Prepare operations and four expected failures. The occupied-lock attempt creates no run or closeout. All receipts preserve the expected version, identity, host, mode and no-product/no-initialization/no-activation flags. No restore-stage receipts or failure.json files were present. N06 records only the three SDK metadata queries per case, with both Doctor failures at package-verification and productDispatchAttempted=false.

All 313 evidence-inventory entries match exact SHA-256 and length, with no unlisted files other than the self-excluded manifest. The reviewed runner bundle agrees with the original handoff ZIP. Five subcase baselines match the four protected configuration hashes, host baseline and copy-manifest digest retained from N03. Original live source/configuration preservation remains operator-checker evidence; the reviewer did not access the new machine's live C:\IFX tree.

Both cache-pristine inventories were independently rebuilt from the approved package bytes: five archives and 108 CLI/Web payload files. Actual mutated caches match their after inventories. Independent byte comparison confirms each mutation changes exactly the final byte from 0 to 1 while preserving file length. Only the selected cache CLI archive or Web DLL differs; all other cache files match approval. The N05 before/after-rejection inventories agree, and their files remain unchanged after successful recovery. N04 mutated configuration bytes and preserved peer-file hashes match their recorded evidence.

All 314 supplied files are archived byte-for-byte under `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-host-0.11.3-rc.1-integration-evidence\N456-acceptance-20261030\original-suite`. Verification report: `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-host-0.11.3-rc.1-integration-evidence\N456-acceptance-20261030\verification.json`. Report SHA-256: `56a4d7adcd3902cb5429f6a244f3d8bc64e44d1747c0c85630229f59d3c18e04`. Shared originals, frozen delivery packages, source overlays and earlier evidence remain unchanged. The reviewer executed only file/hash/archive/document operations, without any IFX entry, SDK or product execution.

## Closure and limits

The explicitly reviewed IFX-layer N01-N06 sequence is closed, with zero pending scenario groups in that handoff. N01 retains its separately documented harness-correction history. N05 qualifies IFX operation.lock and release recovery, not product-internal workspace-session concurrency. N06 qualifies integrity guards on reconstructed caches, not cache creation by a new real NuGet restore. Execution scope is supported by reviewed control flow and receipts, not OS process tracing. Optional offline qualification remains skipped.

This closes the scoped negative validation, not public-feed-only clone readiness, migration/activation or release publication. The next step is a repository integration/release-readiness review: final package version and source identity, repository-carried configuration/locks, remaining clone acquisition evidence and activation/rollback proposal. Architecture tests, active configuration changes, commits and publication were not performed or newly authorized.
