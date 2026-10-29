## N04/N05/N06 passed; reviewed IFX negative scope closed, 2026-10-30

The operator supplied the complete N456 suite and reported checker exit 0. All five subcases and 59 checks pass. Independent review verified all 313 manifest entries, ten unique original operation closeouts and their evidence copies, both exact one-byte cache mutations and configuration/host baseline agreement with N03. All 314 supplied files were archived byte-for-byte. N04 rejects and preserves local/NuGet edits; N05 rejects an occupied operation lock without a new run and recovers after release; both N06 Doctor attempts fail at package-verification with productDispatchAttempted=false. N06 queries SDK metadata only; no restore, product, architecture test or activation was recorded.

N01-N06 are now complete within the explicitly reviewed IFX-layer scope, with zero pending groups in that handoff. N05 covers IFX operation.lock, not internal product session locks. N06 uses reconstructed caches, not a new real NuGet restore. Those are qualification limits, not implicit additional test authorization or broader product/public-feed acceptance. Optional offline qualification remains skipped. Next review repository integration and release readiness, final package identities and clone-only acquisition; migration/activation, commits and publication still require their own scoped decision. No such action occurred during this evidence review. Earlier dated sections retain their historical checkpoint meaning.

## N04/N05/N06 batch handoff ready, 2026-10-30

The user requested the three remaining groups together. A separate ZIP now provides a sequential All runner and individual group wrappers. Five subcases cover local configuration, generated NuGet configuration, IFX operation-lock rejection/recovery, cache archive integrity and Web payload integrity. Each result is checked before advancing; any failure stops the batch with evidence. The original accepted source/packages/state and N01-N03 artifacts are preserved.

Syntax, five simulated subcases and three negative harness guards passed; no actual entry, SDK or product ran during validation. New-machine execution remains pending. N04/N05 use Prepare only. N06 reconstructs fresh isolated caches from approved packages and uses Doctor only for SDK metadata and expected pre-dispatch integrity rejection, with no restore stage or intended product execution. N05 does not qualify internal product workspace-session locking; N06 does not qualify a new real restore. These limits must remain in acceptance. Optional offline qualification remains skipped; architecture tests, activation/migration, commits and publication remain excluded.

## N03 passed; three negative scenario groups remain, 2026-10-30

N03 is closed on reviewed operator evidence: Prepare exited 1 with the exact package-approval rejection, expected-rejection-verified and 13 true checks. Seven evidence manifest entries and all five supplied package files were verified. An independent byte comparison confirms only CLI byte 2906315 changed from 0 to 1, with length preserved. Eight original evidence files and five package files were archived unchanged. Protected configuration and host baseline hashes match N02. No reviewer entry/SDK/product operation was executed.

The remaining previously planned scope is now labeled as three groups: N04 generated local configuration modification; N05 occupied operation/workspace state; N06 restored package/CLI-Web payload integrity. These are scope groups, not a fixed count of command invocations; each group's variants and fixture prerequisites must be reviewed before execution. N01-N03 are closed, while N04-N06 have not run. SDK-incompatibility failure/recovery remains covered by the earlier retained SDK 10.0.102/10.0.303 history. Optional offline qualification is skipped and is not part of the remaining count. Architecture tests, activation/migration, commits and publication remain excluded. Earlier dated sections retain their historical checkpoint meaning.

## N03 command ready; operator result pending, 2026-10-30

The user authorized N03. The helper creates a new five-package case, verifies every copy, then flips exactly one final-byte bit in the CLI copy while retaining its length. It records before/after hashes and bytes and invokes only Prepare. The expected rejection is the hash-bound source literal `Candidate source package differs from approval.`; a generic child error is insufficient. Thirteen operator checks cover exact rejection, the retained CLI mutation, the four unchanged other packages, both empty storage roots, original/copied input preservation, accepted configuration and host equality. Original packages, accepted state and N01/N02 cases remain intact.

Syntax and 16 local synthetic checks passed, including independent byte comparison, mutation containment/repetition guards, actual file-hash assertion formatting and positive/negative error fixtures. No IFX entry, SDK or product was run during helper validation. N03 remains awaiting the new-machine operator; N01/N02 remain passed. Inspect N03 evidence before advancing. Optional offline qualification remains skipped; architecture tests, activation/migration, commits and publication remain excluded.

## N02 passed on operator evidence, 2026-10-30

The new-machine missing-CLI case is closed: Prepare exited 1 with the exact absent CLI path, status expected-rejection-verified and all 11 checks true. Seven evidence-inventory entries and four supplied non-CLI package hashes/lengths were verified; eight original evidence files and four package files were archived byte-for-byte. Protected configuration and host baseline hashes agree with N01. The operator checker records unchanged original/copied inputs, an unchanged four-package subset and both disposable storage directories still empty. This is a supplied-evidence review, not live remote filesystem inspection. No standalone checker exit-code transcript was supplied; no new entry/SDK/product operation was run by the reviewer.

N01 and N02 are closed within their entry-preflight scope; the broader negative matrix remains incomplete. Next recommended case is N03, package-hash mismatch in a fresh five-package copy with only its CLI copy altered. Preserve the approved originals, accepted state and previous case evidence. N03 has not run; subsequent configuration, occupancy and restored-payload cases remain pending. Optional offline qualification remains skipped. Architecture tests, activation/migration, commits and publication remain excluded. Earlier sections below retain their historical checkpoint meaning.

## N02 command ready; operator result pending, 2026-10-30

The user authorized N02. A separate helper now validates missing candidate CLI rejection through the actual IFX Prepare entry, using the installed host and a new four-package subset/fresh storage inside the existing disposable fixture. The helper preserves the original approved five-package sets, source copies, accepted configuration and empty base fixture storage. It rechecks the pinned preparation/copy manifests and 979 source/package hashes and holds the accepted operation lease during the case.

The missing-file classifier derives the localized exception message from the verified read-only hash function and checks the specific CLI path, rather than accepting any error. Syntax and 14 matcher/file-I/O regression checks passed with host equality; these did not run an IFX entry, SDK, restore or product. The new-machine N02 run and its 11 acceptance checks remain pending. Checker exit 0 will mean expected rejection verified, while the child Prepare must exit nonzero. Rejection precedes normal closeout creation; retain the separate case evidence and SHA-256 inventory. Inspect the returned evidence before the next negative case. Optional offline qualification remains skipped; architecture tests, active configuration switching, commits and publication remain excluded.

## Scope update: optional offline qualification skipped, 2026-10-30

The user explicitly made the offline scheme nonmandatory and requested skipping it. RC offline packaged qualification is now skipped-by-user and is not a completion/activation prerequisite for this host/configured IFX scope. This is not an offline pass or an architecture-gate acceptance claim. Existing offline files and historical results remain unchanged; older pending-offline statements below are superseded for the current scope.

Next recommended gate is N02: missing candidate CLI package rejection in a disposable package-source copy, using Prepare and the installed host. Keep the accepted source/state and original approved packages intact; require a nonzero error that identifies the missing input, no source/version fallback, and unchanged protected inputs. The case has not run. Subsequent scoped negative checks cover tampered packages, altered generated local configuration, occupied state/operation leases and restored-payload integrity; inspect each actual result before the next gate. After negative closure, review source distribution, published-package identities/locks and public-feed-only clone acceptance. Migration/activation requires separate review when selected; no architecture tests, commits or publication are authorized here.

## Original evidence archive verified, 2026-10-30

The new-machine original evidence archive is now verified and retained: 35 copied files, six outer closeouts (five success plus the original SDK-prerequisite failure), eight restore-stage receipts, three initializations and two exit-zero Launcher closeouts. SDK 10.0.303 and Core/ASP.NET 10.0.11 are recorded. All 11 original reuse checks are true, and before.json configuration hashes match the archived configuration bytes. The source application, package and baseline backup hash records also reconcile.

See [the archive verification record](../../acceptance/20261030-ifx-host-archive-verification.md) for archive identity and evidence limitations. Original machine-receipt archival is closed; historical Verify/page observations remain operator evidence. The original archived-with-gaps report is preserved unchanged. Doctor, Workbench, the full real negative matrix, RC offline qualification, migration/activation and public-feed-only clone acceptance remain pending. No product/test execution, active configuration switch, commit or publication occurred. Earlier dated entries below retain their checkpoint meaning; this verification supersedes their pending archive/runtime-metadata statements.

## Current acceptance closure, 2026-10-30

The local and new-machine host/configured 0.11.3-rc.1 sequences have passed Prepare, locked Restore, Init, Launcher/Close launcher and repeat/Close launcher. Both machines have operator-confirmed page identity and all 11 reuse checks true. New-machine source Verify and the 54-file overlay also passed. The new-machine SDK 10.0.102 failure is retained separately from the successful retry after operator installation of eligible 10.0.303 x64.

Local original receipts are archived; the new machine has a pasted successful Restore closeout plus explicit operator reports for other gates. Its original receipt bundle, exact runtime patches and per-user absolute paths are not yet archived here. No agent remote execution, independent UI inspection, fully cold network acquisition or public-feed-only readiness is claimed. This documentation closure runs no product or tests and changes no active configuration.

Current scope and evidence are in [the two-machine acceptance record](../../acceptance/20261030-ifx-host-two-machine-acceptance.md). Remaining: original new-machine evidence archival, standalone Doctor, direct Workbench, the full real negative matrix, RC offline qualification, migration/activation and future published-package clone acceptance. Architecture tests, active configuration switching, commits and publication remain excluded. The existing kit/ZIP/packages and historical evidence stay immutable; use the separate dated delivery addendum. Older checkpoint text below records its status at that time and does not override this closure.

# IFX acceptance handoff for 0.11.3-rc.1

## Candidate boundary

Five local development packages are prepared: ArchSift.Contracts, Core, ArchUnit, Testing and Cli, all version `0.11.3-rc.1`. The CLI contains the matching framework-dependent Web payload and project entry template. These bytes are unpublished and unqualified. Source is based on `91cd73adc85a4da8f79fffa04ad834c870d78d07` plus the uncommitted repair and version preparation; use the accompanying complete source manifest and overlay, not the base commit alone.

The preparation record includes locked offline restore, Release/net10.0 build with SDK 10.0.303 (zero warnings/errors), 31 passing policy regressions for this version, five-package identity/dependency/readme checks and 69 Web inventory file checks. Third-party dependency versions and content hashes are unchanged. Ten project-reference lock graphs were refreshed. Historical RID-specific/self-contained locks were not used or requalified by this framework-dependent build.

The earlier 93-case result qualifies the preceding repaired source before the version change. It remains historical evidence, including its original failure and targeted pass. It is not a 93-case acceptance claim for these package bytes. Subsequent local operator acceptance passed Prepare, locked Restore, Init and two Launcher/Close launcher cycles with all 11 repeat reuse checks true. Five outer receipts, eight restore-stage receipts, three initialization records and two product closeouts were archived byte-for-byte. Page inspection and reuse checks are operator-confirmed. No architecture gate, Workbench, migration or activation has run; new-machine acceptance remains pending.

## Isolated integration and local Launcher acceptance completed; new-machine acceptance pending

The existing isolated IFX 0.11.2 candidate is unchanged and still contains its intentional product-contract blocker. A separate `ifx-host-0.11.3-rc.1-candidate` copy now implements the updates below, including complete restore/verification/startup behavior. The initial 33 static checks and 26 mock groups are retained as preparation evidence. Local operator locked restore subsequently validated the candidate library graph, and Init/Launcher/repeat exercised actual packages on SDK 10.0.303 and Core/ASP.NET 10.0.11. Fresh new-machine restore and lifecycle remain pending. See the repair acceptance JSON for local source/evidence locations.

| Input | Implemented isolated-candidate update |
| --- | --- |
| Root tool manifest | Pin `archsift.cli` to 0.11.3-rc.1 with isRoot=true and rollForward=false. |
| Central package versions and relevant locks | Use matching Testing/product dependencies; update locks from approved archive/nuspec metadata without changing unrelated dependencies; local locked restore passed; new-machine validation remains pending. |
| Gate, package approval and provenance | Bind the five exact archive hashes and candidate version; retain IFX gate identity and unchanged policy/case approvals. Record dirty development provenance honestly. |
| Host entry implementation | Replace the unconditional product-contract failure with the complete reviewed restore, archive verification, local configuration and init/start/doctor flow. Keep failures explicit. |
| Workspace/local configuration | Keep host/configured, allowNetwork=true and null exact approvals. Resolve explicit NuGet.Config, installed dotnet, external cache and external state. The repository default state root requires a generated external local override. |
| Receipts | Bind source inputs, version, restore output and selected host. A preparation/restore receipt does not activate a profile. |

The machine-readable `ifx-adoption-inputs.json` in the candidate evidence folder contains the exact five hashes, local package source and change checklist. It is the immutable product-package handoff manifest, not an execution command. The subsequent IFX integration has its own source manifest, change report and validation receipts; do not overwrite the original package evidence.

## Operator-led acceptance order

1. Review the isolated IFX candidate diff and package hashes. Capture User/Machine environment, all PATH hashes and Profile hashes. Verify eligible installed SDK/Core/ASP.NET, PowerShell x64 and the exact project baseline. Keep the current 0.11.0 profile and existing state selected.
2. Use a fresh external package cache and state root. While this candidate is unpublished, explicitly configure the local candidate package source alongside any reviewed network sources needed for other locked dependencies. Restore the exact local tool and solution locks. Fail on missing/version-mismatched packages; do not rewrite locks or fall back to another version at startup.
3. Run doctor and init only after preparation succeeds. Verify correct target/local paths, version and installed host. Require initialized state, null dailyTestConfig and the explicit unavailable architecture-gate message. No architecture tests or application entry points may execute.
4. Open Launcher, verify the project, use Safe shutdown and check exit code/environment equality. Repeat startup and compare project, local configuration and state paths. Inspect the independent Workbench route in a separately reviewed step. Browser closure alone is not shutdown.
5. Exercise missing/incompatible host, missing/tampered package and duplicate/busy state rejection in disposable external fixtures. Retain the old active profile, markers and receipts. Migration and activation require separate review after those results.
6. After a repaired version is actually published and separately approved, perform a distinct fresh-machine cold online-only restore from the intended public sources. A local-feed candidate test cannot establish public NuGet availability or clone-first online readiness.

Provide one reviewed command block at a time and inspect the operator's result before the next real operation. This document is a sequence and acceptance contract; it is not authorization to execute real tests now. Architecture gates, active configuration switching, commits and publication remain outside this preparation checkpoint.

## Delivery and rollback

Keep the five `.nupkg` files with package receipts, `SHA256SUMS.txt`, source-input hashes, tracked patch/untracked overlay, candidate review and environment closeout. Do not redistribute the entire build cache as the product. Rebuilt bytes require fresh hashes and package acceptance; the candidate identity must not be silently reused for different delivered bytes.

No migration has been performed. A future activation stages new external state and imports only explicitly reviewed inactive data. Keep old 0.11.0 state and receipts for rollback. No automatic PATH repair, runtime installation or global tool registration is permitted.

## New-machine delivery gate

Before execution, record the new IFX clone's exact HEAD and clean status and prepare/verify a complete candidate source overlay against that baseline. The existing IFX source delta is only relative to the previous isolated 0.11.2 candidate, not arbitrary IFX Git HEAD. The current source is uncommitted; do not claim clone-alone readiness or apply the old delta without base verification. The subsequent complete kit now supplies a guarded source installer for exact commit 3e330fd9d651b2b43fe4d4e2f27099a141705892; actual new-machine transfer/preflight and execution remain pending.

Transfer reviewed source/configuration/locks, the five unchanged RC package archives with checksums/provenance, and current instructions. Keep the candidate package source external. Exclude bin/obj, SDK/runtime bundles, caches, machine-local settings, generated NuGet.Config, active state, locks, journals and markers. Use installed prerequisites and fresh external storage on the new machine. Preserve any historical receipts as evidence only.

The minimum new-machine sequence is preflight/source hashes, Prepare, Restore, Init, Launcher/Close launcher and repeat/Close launcher. Inspect each result before issuing the next command. New-machine path/user/input hashes may differ; require stability within that machine and correct target binding, not the literal local hashes. Require exit 0, host equality, null dailyTestConfig, unchanged local/startup configuration on repeat and activationPerformed=false. Collect actual host metadata, source/package hashes, outer/stage/product receipts, UI confirmations and the 11 reuse results. Do not open Workbench, run architecture tests or activate during this sequence. Standalone Doctor, Workbench, real rejection scenarios and migration remain separate pending gates.

Keep original packages, ZIPs and static/mock evidence immutable. The current documentation/acceptance addendum supersedes preparation-time status without relabeling the old archives. Local-source RC success does not establish public-feed-only availability, fully cold network acquisition or disconnected support. Public clone readiness additionally needs reviewed source distribution and approved published-package identities, followed by its own acceptance. No commit or publication is authorized by this closure.

## Complete source kit checkpoint

The new ifx-0.11.3-rc.1-new-machine-delivery ZIP supersedes the previous-candidate-only delta for the exact baseline above. It carries 54 source changes, five unchanged packages, full inventories, guarded Verify/Apply and checksums. Fourteen source-only simulation checks passed; the applied host input fingerprints match the local accepted source. Use its top-level README for source transfer/application and retain source-application.json before requesting new-machine product commands. No new-machine runtime or UI acceptance is inferred from these simulations. Exact delivery hashes are recorded in the repair acceptance JSON.
