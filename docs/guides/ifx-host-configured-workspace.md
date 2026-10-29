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

## Original evidence archive verified, 2026-10-30

The new-machine original evidence archive is now verified and retained: 35 copied files, six outer closeouts (five success plus the original SDK-prerequisite failure), eight restore-stage receipts, three initializations and two exit-zero Launcher closeouts. SDK 10.0.303 and Core/ASP.NET 10.0.11 are recorded. All 11 original reuse checks are true, and before.json configuration hashes match the archived configuration bytes. The source application, package and baseline backup hash records also reconcile.

See [the archive verification record](../acceptance/20261030-ifx-host-archive-verification.md) for archive identity and evidence limitations. Original machine-receipt archival is closed; historical Verify/page observations remain operator evidence. The original archived-with-gaps report is preserved unchanged. Doctor, Workbench, the full real negative matrix, RC offline qualification, migration/activation and public-feed-only clone acceptance remain pending. No product/test execution, active configuration switch, commit or publication occurred. Earlier dated entries below retain their checkpoint meaning; this verification supersedes their pending archive/runtime-metadata statements.

## Current acceptance closure, 2026-10-30

The local and new-machine host/configured 0.11.3-rc.1 sequences have passed Prepare, locked Restore, Init, Launcher/Close launcher and repeat/Close launcher. Both machines have operator-confirmed page identity and all 11 reuse checks true. New-machine source Verify and the 54-file overlay also passed. The new-machine SDK 10.0.102 failure is retained separately from the successful retry after operator installation of eligible 10.0.303 x64.

Local original receipts are archived; the new machine has a pasted successful Restore closeout plus explicit operator reports for other gates. Its original receipt bundle, exact runtime patches and per-user absolute paths are not yet archived here. No agent remote execution, independent UI inspection, fully cold network acquisition or public-feed-only readiness is claimed. This documentation closure runs no product or tests and changes no active configuration.

Current scope and evidence are in [the two-machine acceptance record](../acceptance/20261030-ifx-host-two-machine-acceptance.md). Remaining: original new-machine evidence archival, standalone Doctor, direct Workbench, the full real negative matrix, RC offline qualification, migration/activation and future published-package clone acceptance. Architecture tests, active configuration switching, commits and publication remain excluded. The existing kit/ZIP/packages and historical evidence stay immutable; use the separate dated delivery addendum. Older checkpoint text below records its status at that time and does not override this closure.

# IFX workspace with an installed host and configured NuGet sources

This contract describes the product repair based on 0.11.2. Compilation passed, and all 93 selected synthetic cases have passing evidence across the initial execution and an authorized single-case elevated retry. The original failure and blocked retry remain retained; packaged acceptance is still pending. This contract is not supported by the unchanged published 0.11.2 package. Consumers must wait for a separately versioned, verified repaired package before activating it.

## Repository and local responsibilities

IFX tracks the workspace policy, tool manifest, lock files, package source configuration, gate metadata, policy/case hashes and approved CLI package hash. Machine-specific paths and generated state belong in local overrides and external state directories. Restores and upgrades may use the configured network sources; they must resolve an explicit pinned version rather than select latest.

The IFX entry point selects the installed host, resolves the external cache and state root, performs explicit tool restore and locked solution restore, verifies the selected package, and invokes workspace initialization/startup. It passes the same effective NuGet cache to both restore and the product. A separate preassembled offline delivery directory is not required for this mode. ArchSift does not silently restore, install a runtime, edit host PATH or weaken restore failures.

## Supported IFX profiles

| Field | Installed host / configured sources | Exact runtime / offline sources |
| --- | --- | --- |
| `runtime.mode` | `host` | `approvedExact` |
| `nuget.mode` / `allowNetwork` | `configured` / `true` | `offline` / `false` |
| `runtime.approvalFile` | null/absent | Original `architecture/runtime-approval.json` |
| `nuget.sourceApprovalFile` | null/absent | Original `architecture/package-source.json` |
| `nuget.configFile` | Explicit existing NuGet.Config selected by IFX | Explicit approved offline NuGet.Config |
| IFX gate identity | `gate.kind=ifx`, original `architecture/portable-gate.json` | Same |
| CLI archive source | Explicit external NuGet global-packages cache | Approved local feed |
| Daily gate | Unavailable; `dailyTestConfig: null` | Existing schema 2 offline gate |

Paths in repository configuration and local overrides resolve relative to the workspace configuration directory. Cache resolution uses `nuget.cacheDirectory` first; otherwise it accepts the explicitly supplied absolute `NUGET_PACKAGES` environment value. The archive layout is `archsift.cli/<lowercase-version>/archsift.cli.<lowercase-version>.nupkg`. No default-cache guessing, fallback feed or global tool lookup is performed. The cache must be disjoint from the target and resolved workspace state and cannot cross links/reparse points. Restoring the tool into a different cache does not satisfy this prerequisite.

An IFX repository cannot opt out of its gate identity by deleting the gate setting while retaining its metadata file. Host/configured mode rejects nonnull exact runtime/source approvals instead of ignoring them. Cross-mode pairs are rejected. Local overrides still cannot change mode, network policy, gate identity or minimum SDK policy.

## Integrity and runtime compatibility

Both profiles retain root tool-manifest identity, exact running version, no tool roll-forward, gate version, policy/case hashes, approved archive hash, installed CLI/Web inventory and byte checks, and Web payload validation. Host mode changes runtime approval policy only; it does not relax package integrity.

The existing WorkspaceHost requirements remain: eligible Windows x64 host, stable supported .NET SDK, project global.json selection and required Core/ASP.NET runtimes. Compatible installed runtime patches are selected by the host contract. Exact mode retains its approved host/SDK/runtime byte and patch checks. Missing or incompatible prerequisites remain explicit errors. Neither mode automatically repairs an environment or falls back from an explicitly selected invalid host.

## Normal startup and architecture gates

For host/configured IFX, init/start reports `dailyTestConfig: null` and stderr explains that the IFX daily architecture gate requires explicit approved offline preparation. Doctor emits the same capability message without changing its existing JSON schema. This permits ordinary workspace/UI readiness without claiming architecture gate readiness. No invalid `test-gate.json` is written and no architecture runner is invoked.

The repair does not add online architecture-gate restore. To use the existing daily gate, prepare and explicitly select a complete approved exact/offline profile. Daily configuration generation is not a test pass. Running that gate remains a separate, authorized action.

## Upgrade boundary

The current IFX 0.11.0 profile remains selected. A future repaired release must update the candidate tool, dependency locks, gate version and approved package hashes consistently. Startup, shutdown, repeated startup, migration and activation must be accepted against that new package. Keep prior state and receipts separate and preserve them for rollback; do not infer activation from successful dependency restore.
