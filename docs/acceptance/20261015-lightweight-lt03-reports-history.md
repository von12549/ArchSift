# Lightweight architecture-test Workbench LT03: reports and basic history

Date: 2026-10-15 (Australia/Sydney). **LT03 REPORT/HISTORY COMPLETE / LT04–LT06 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**. Executed against the LT02A external IFX copy at `cc06528ab17052baafa496df2a89f6cde9207384` and synthetic project. No formal IFX B policy or final human UI approval.

## Deliverables and report semantics

- Shared ArchSift.TestGate writes architecture-test-run.json/html into each fresh external run. [Frozen LT00 schema](../../schemas/architecture-test-run.schema.json) records current run, Git/SDK/TFM/config, actual rule SHA, native MTP discovery/execution identities, retained IFX baseline, stages/gate. HTML is an escaped projection; JSON publishes last as authority. Save failure becomes block/3, never old success.
- Before assertions, adapter emits compact structured evaluations correlated by native MTP/CTRF/xUnit IDs. JSON retains pass/violation/inconclusive/not-applicable/error, matched, finding IDs, actual source/target and available locations. IFX-specific C# tests retain native results/original messages; no fabricated subchecks/findings without structured Violation.
- IFX report lists 588 not-included LT00 responsibilities: A 182, disabled B 406, none counted as passed. Synthetic targets do not inherit IFX inventory. SARIF is optional later; JSON/HTML delivered here.
- Each run writes architecture-test-state.json. Read-only archsift test-history --directory <root> [--target <worktree>] lists marked runs. Missing/corrupt materials unavailable, unconfirmed terminal state interrupted, never old allow. Known rule SHA/Git HEAD changes stale; otherwise unknown, neither a gate failure nor current-source proof. See [Workbench guide](../architecture-test-workbench.md).

## Real IFX results

Final positive D:/ArchSift-lab/runs/lt03-ifx-final-positive-20261015: parent IFX_UPDATE_SNAPSHOTS=1, all controlled children 0. Offline restore, incremental build, native discovery and full tests passed; 80 originals+1 JSON, allow/0. Original 80/80 passed, mechanism pass matched=1, 182+406 explicitly excluded. State finished/allow/0; 11 API snapshot hashes unchanged. Authoritative JSON SHA-256 `fd389800466d6f2dc6ac0faf8ceafd27b22c393736bdedabf1e3b5159ffb7713`, HTML `3142f314ab821db9685c1e662e02ee5caf51e09e48c7c644f4d6ff3eb42ac23d`.

Real JSON negative D:/ArchSift-lab/runs/lt03-ifx-json-negative-20261015: all 80+1 ran, block/2, ifx-context-mechanism violation matched=1. One actual finding IFX.Platform.Context.Contracts.EndpointScopeMetadata → IFX.Platform.Context.Contracts.ExecutionScope. Report SHA-256 `db14070b630a0fd197cfe530069db71e90297857555cf96a1bf8a5490d6f48f9`. Temporary rules restored byte-for-byte.

Real API negative D:/ArchSift-lab/runs/lt03-ifx-api-negative-20261015: all 80+1 ran; only PublicApi_ContractsSnapshotsMatch failed, block/2. Original Shouldly message retained, findings 0, no invented type location. SHA-256 `bede3d7fd18aa22b4524b6f06d8c7238a2789a9ed818f40be1ab4ec8c9f78faa`. Snapshot restored; parent remained 1.

NJsonSchema 10.9.0 from local offline cache validated those three real JSON reports plus synthetic complete/violation/missing-original/restore-failure reports against frozen schema: zero errors. Read-only history D:/ArchSift-lab/runs/lt03-ifx-history-page.json lists three marked runs, one allow/two block, without old success masking negatives. Restored negative rules make only the related history stale. Page SHA-256 `ddf47295a42404103b372461a74e18aa706dcbf4e2809e1dc0ebf9b1d7fb5d4d`.

## Synthetic negatives and retained scope

Updated synthetic suite: 16 originals+1 JSON. Seven exits: complete 0, actual violation 2, missing original 4, subset 4, offline restore failure 3, recovery 0, invalid config 5; host hashes equal. Complete/violation/coverage/restore failures retain current authoritative reports without unexecuted passes. Root D:/ArchSift-lab/runs/archsift-lt02-59dd226049bc4d93a795863b95ff09de, summary SHA-256 `eb6faed9c1f941aeb6c352f42b3bbceadd3bf4aea8ca8a23f37b9727a8349264`.

A dedicated synthetic fixture preoccupies architecture-test-run.json inside the test process. All 16+1 can finish, but authority cannot overwrite: block/3, failed/3 marker, conflict bytes retained, history unavailable. Two earlier allows remain separate history, not current conclusions. Other negatives verify missing HTML unavailable, changed rules stale, abandoned running interrupted, marker/report contradictions unavailable, and report-without-confirmed-terminal never allow.

Solution Release/net10.0 build 0 warnings/errors; existing UnitTests 151/151, IntegrationTests 95/95, ArchitectureTests 2/2, zero skips. Original IFX main clean; external copy original sources 20/20 hashes equal; adapter matches repository sample. Key .NET gate/build/regression before/after and final environment/Profile hashes all `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`, no persistent drift.

Reports do not provide SourceBound/full build causality; Git/dirty are context only. Basic history has no rotation/pinning/archive recovery/automatic reruns. UI editing/start/cancel and matched-input CLI comparison await LT04; matched three-run cold/warm/disk measurements await LT05.
