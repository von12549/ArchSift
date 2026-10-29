## Original evidence archive verified, 2026-10-30

The new-machine original evidence archive is now verified and retained: 35 copied files, six outer closeouts (five success plus the original SDK-prerequisite failure), eight restore-stage receipts, three initializations and two exit-zero Launcher closeouts. SDK 10.0.303 and Core/ASP.NET 10.0.11 are recorded. All 11 original reuse checks are true, and before.json configuration hashes match the archived configuration bytes. The source application, package and baseline backup hash records also reconcile.

See [the archive verification record](20261030-ifx-host-archive-verification.md) for archive identity and evidence limitations. Original machine-receipt archival is closed; historical Verify/page observations remain operator evidence. The original archived-with-gaps report is preserved unchanged. Doctor, Workbench, the full real negative matrix, RC offline qualification, migration/activation and public-feed-only clone acceptance remain pending. No product/test execution, active configuration switch, commit or publication occurred. Earlier dated entries below retain their checkpoint meaning; this verification supersedes their pending archive/runtime-metadata statements.

# IFX 0.11.3-rc.1 local and new-machine acceptance closure

Recorded 2026-10-30 (Australia/Sydney). This is a documentation closure of operator-run acceptance, not a new execution or release. The machine-readable acceptance records are authoritative. The dated preparation checkpoints retain their historical outcomes; this addendum supersedes their pending status only for the gates below.

## Completed scope and evidence

| Gate | Existing local machine | New machine |
| --- | --- | --- |
| Source transfer | Complete kit prepared; 14 source-only simulations passed | Operator confirmed ZIP/checksums, clone baseline and read-only Verify matched; 54-file overlay applied with external backup and receipt |
| Prepare | Passed; archived receipt | Passed; operator report, exit 0 |
| Locked Restore | Passed; archived outer/stage receipts | Passed after SDK prerequisite correction; successful outer closeout pasted in conversation |
| Init | Passed; archived initialization/outer receipts | Passed; operator report: initialized, correct version/paths, exit 0, hostEqual=true, activationPerformed=false |
| Launcher / Close launcher | Passed; archived closeouts and operator page inspection | Passed; operator confirmed page version/project/configuration, safe close, exit 0 and host equality |
| Repeat Launcher / Close launcher | Passed; archived closeouts and operator reuse checks | Passed; operator confirmed exit 0, all 11 reuse checks true, no state conflicts or environment drift |

Local evidence includes five outer closeouts, eight restore-stage receipts, three initialization records and two product closeouts, preserved with a hash manifest. UI inspection and repeat comparisons are operator evidence. Local SDK is 10.0.303 and recorded Core/ASP.NET runtime patches are 10.0.11. The local input identity is `6003805add6768e16672838d7a60b694a8d24758a87f89a2a7934cfe7c0c8506`.

The new target is `C:\IFX\IFX-New`, storage is `C:\IFX\host-state-01`, and checkout key is `63d78752ee0b1d1b3da66477`. The new-machine input identity is `628150e41bb4825f774e72a7f98338b964ca664d27c4ca9bc7ed2976ebf5f8d2`; it is stable within that machine and need not equal the local identity. The successful Restore run is `23bc6b80a9564b46b6438be6a4568a76`, selecting `C:\Program Files\dotnet\dotnet.exe`. SDK 10.0.303 x64 installation was confirmed by the operator. Exact new-machine runtime patch versions and absolute per-user startup/state paths have not been supplied; do not substitute local-machine values.

New-machine acceptance is operator-reported, supplemented by the pasted Restore JSON. The agent has not accessed that machine, read its original receipt files, or independently inspected its UI. The closure stores a clearly labeled Restore transcription and operator-report record, not fabricated byte-for-byte receipts or hashes. Complete new-machine receipt archival is pending; this evidence limitation does not turn the reported successful gates into unexecuted gates.

## Retained SDK prerequisite failure

The initial new-machine Restore failed with outer exit 1 and host exit -2147450725 (0x8000809B, SdkResolveFailure). The operator confirmed only SDK 10.0.102 was installed. IFX global.json requires 10.0.300 with latestPatch, so a stable eligible 10.0.3xx SDK is required; 10.0.1xx or 10.0.4xx does not satisfy that selection. Prepare resolves the executable/configuration without executing the SDK, so Prepare success alone does not establish SDK eligibility.

The operator installed SDK 10.0.303 x64 and repeated Restore against the existing directory successfully. Keep the original failed attempt and successful retry separate. The product did not silently install an SDK or relax global.json, locks or runtime policy. This observed prerequisite failure/recovery is useful evidence but does not complete the planned real negative-case matrix.

## Meaning of this acceptance

The installed-host, configured-source IFX workflow is accepted for the tested local candidate and baseline-bound new-machine clone: Prepare -> Restore -> Init -> Launcher/Close -> repeat/Close. It uses exact approved local RC packages and permits locked third-party restore from configured network sources. Repeated startup preserves configuration bytes, configuration/state paths and input identity; new run/launch IDs and logs are expected. Configured mode has no daily architecture configuration; the reported dailyTestConfigAbsent check is true. No architecture test result is claimed.

This is not yet clone-alone public distribution. The 54-file source overlay remains uncommitted, and the five exact RC packages remain unsigned, unpublished development artifacts. A future clone-only/public-source claim needs reviewed source distribution, approved published package identities and locks, then a distinct public-feed-only acceptance. Allowed network access does not prove fully cold downloads, and local RC success does not prove disconnected operation.

## Remaining scope and conditions

1. Archive the new machine's original source-application/backup metadata, Prepare/Restore/Init/Start/repeat receipts, restore-stage receipts, initialization records, product closeouts, before/reuse JSON and selected host/runtime metadata. Preserve original bytes, record SHA-256 and reconcile them with these reports. Keep both the SDK failure and retry. No repeat execution is needed solely to collect existing evidence.
2. Standalone Doctor, direct Workbench and real negative scenarios (package corruption/missing payload, incompatible hosts, busy state and related rejection cases) remain separately pending. One SDK failure is not the complete matrix.
3. RC exact/offline qualification and architecture-gate acceptance remain unverified. Historical offline/CI/architecture helpers are not qualified by Launcher acceptance.
4. Migration, rollback rehearsal and activation require reviewed scope and explicit approval. Preserve active 0.11.0 configuration/state/receipts; do not copy leases, journals or old markers into new active state.
5. Formal release qualification, source distribution and published-package public-feed-only clone acceptance remain pending. No commit, PR, push or publication is authorized by this closure.

The next documentation step is original-evidence archival and reconciliation. Any further product verification is a separately reviewed gate. There is no automatic transition from Launcher acceptance to architecture tests or activation.

## Immutable delivery and current instructions

The complete kit targets IFX commit `3e330fd9d651b2b43fe4d4e2f27099a141705892`. Its 54 source changes are 16 modified plus 38 added files, with no deletions. Baseline/result inventories contain 936/974 files. The five package bytes, source patch, source manifest, kit manifest and ZIP remain unchanged.

ZIP SHA-256: `2b7f94df6f441d86689f4bc94a19a0ef72675a215dd7e597da66d930788974f8`.

Delivery manifest SHA-256: `b5659c010234527de18000f6b35ad9ea1f3fa8d3d32b8cf8cb32061a5a289a8b`.

Ship this dated addendum alongside the existing kit if communicating the new status. Do not insert it into the hash-bound kit, repackage the ZIP under the old checksum or reapply the source overlay to the already-patched clone. Current working documentation was updated separately; the original source inventory still describes the original delivered bytes. Existing C:\IFX state/configuration is retained, with no additional initialization or launch performed by this documentation task.
