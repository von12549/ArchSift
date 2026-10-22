# CL03 candidate packaging and synthetic verification

2026-10-21 (Australia/Sydney). Historical status: **AUTHORIZED CL03 PACKAGING AND SYNTHETIC VERIFICATION COMPLETE / NOT PUBLISHED OR ADOPTED / CL04–CL05 NOT STARTED**. See [raw JSON](20261021-cl03-candidate-packaging.json) and [package-source maintenance](../distribution/20261021-cl03-candidate-maintenance.md).

## Source and delivery

CL01 actual-main acceptance/local update and CL02 were checked first. IFX stayed clean main `305fc99c61a22cad48b22f5f524673ca0d95675d`; fixed 0.10.0-sp03.1 packages, policies, cases and locks stayed unchanged. CL01 CI was cited from existing records, without remote re-query or rerun.

All 11 CL02 candidate corrections matched their frozen SHA-256. The original 159-input scope matched except for reviewed CL02 fixes. Actual required projects/resources, schemas/templates, Web static files, licenses and development examples formed a new external 250-input freeze. The whole dirty worktree, acceptance history, IFX and unknown concurrent materials were excluded. ArchSift HEAD/index stayed unchanged and source was uncommitted then. The byte inventory and delivery hashes establish provenance, without an exact compiler-input closure or reproducible-build claim.

New candidate **0.10.0-cl03.1** supplied four libraries, CLI and matching framework-dependent Web ZIP under `D:/ArchSift-lab/packages/cl03-20261021-01`. Its 28-package source comprised four libraries, CLI and 23 existing third-party packages; the original 251-package source was retained. CLI SHA-256: `aa8062e622a9d96d28b7c7619722200c8ed641a17e39effcdcbc566c20e57d2e`. Web ZIP SHA-256: `581c280b3910471d91ef803c06f6ee049ee83884b9f869a95b3cc8a5546f02f1`. The delivery manifest holds all remaining hashes; a version string alone cannot authorize replacement bytes.

## Actual checks

| Check | Result and scope |
| --- | --- |
| Offline build | SDK 10.0.303, Windows x64, net10.0/Release. CLI/Web explicit restore, locked recheck and build passed with zero warnings/errors, using only the original approved offline source. |
| Responsibility projection | 3/3, zero failures/skips. The frozen formal 20-rule selection retained 568 items, three C# responsibilities, 565 remaining and four pending items. Missing/skipped/failed/duplicate/unknown/incomplete execution did not gain a pass. Tested TestGate/Core/Testing DLLs matched CLI payload bytes. |
| Cold candidate use | Two synthetic targets at different depths with spaces, each using an empty independent cache. Normal tool restore, locked dependency restore, build and full 16/16 execution (14 logical C# plus two real ArchUnitNET JSON cases) allowed with complete coverage. Moved feed hashes matched. This was not the IFX 112-case gate. |
| Actual projection | Three synthetic approved C# responsibilities passed. Registry4 stayed adopted-pending-activation/not-run. Other associations did not inherit passes. Two synthetic JSON cases were absent from the old list, so notIncluded/projection each retained 588 entries. |
| Nine contract rejections | Unknown format 5; missing manifest 3; silent legacy fallback 5; mixed mode 5; manifest tamper 5; policy tamper 5; wrong Testing version 3; cold missing dependency 3; zero match 2. All rejected; finally restored synthetic edits. |
| Two package-tamper rejections | Only copied sources changed. Wrong CLI raw hash rejected before tool execution (1). Modified Testing DLL caused cold locked restore NU1403 (3), without a pass report. Original candidate source stayed unchanged. |
| Report compatibility | New, old and cancelled reports passed schema validation; unknown projection 99 rejected. History retained old report bytes/outcomes and cancellation did not inherit an old pass. |
| Web compatibility | Eleven production-renderer checks passed. Reviewed screenshots at widths 1365/390 had no page overflow. ZIP JS/CSS/i18n matched tested source. No real HTTP session or final human acceptance occurred. |
| Dependencies/licenses | Testing approval covered 10 packages; CLI resolved six metadata packages and five runtime deps packages. Versions matched the fixed combination. All 28 raw package hashes/metadata licenses, nine archived LICENSE/NOTICE files and third-party explanations matched. Framework-dependent Web embedded no runtime. |
| Host/preservation | Full process hashes matched around SDK/child operations; stable host hash stayed `59c686803d67d98b70de45b00e7dbe3b7376d40c01d12f6565ad553a8503bd20`. Final review covered 911 direct IFX inputs including two ignored files and HTML, original 251 packages, existing work files and backups. Only two plan status additions were allowed. |

Offline Audit was explicitly `not-run`, without a new vulnerability pass. No real IFX, independent online audit, CI, consumer update, commit/push/publication, cleanup, environment repair or CL04 execution occurred.

## Retained stops and evidence

The first cold restore succeeded, but the old synthetic template lacked current `DeclarationInputs`, causing compile/block3 before tests. Only the two new synthetic targets received an explicitly identified stand-in, with original repair text/hashes retained; no IFX-equivalence claim was made for it. Fresh first-02 state/cache passed, without rewriting packages. Initial license lookup used the wrong expected extensionless LICENSE location; checking NuGet's actual path proved equal original bytes. artifact-review-02 stayed retained; artifact-review-03 distinguished six resolved metadata packages from five runtime packages. Failed/intermediate records and original packages were preserved.

Evidence root: `D:/ArchSift-lab/runs/cl03-20261021-01`. preflight.json, source-inputs.json and source hold prerequisites/250 frozen inputs; build/pack hold restore, locks, builds, three tests, packaging and Web output. first, repairsynthetic, first-02 and moved retain failure/repair/two complete passes. negatives/compatibility hold 11 rejections, three report forms, 11 browser checks and screenshots. artifact-review-03 contains delivery-manifest.json, packages-and-licenses.json, cli-closure.json and consumer-closure.json. preservation/completion.json record retained scope. Old combinations, backups and InProject stayed intact at this historical point; later exact cleanup receipts govern subsequent handling.
