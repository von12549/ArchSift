# W10 0.2.0 report-integration acceptance

Date: 2026-10-07 (Australia/Sydney). Status: COMPLETE / PASS. Formal release assets are built, verified and registered separately; working candidates are not formal assets.

Pre-release remote-CI update: the first [run37509047659](https://github.com/von12549/ArchSift/actions/runs/37509047659) actually failed on both platforms, solely in the net8/net9 fixture SDK-identity assertion. GitHub runners also had 9.0.318 installed. The fixture's latestPatch correctly selected it while the assertion required 9.0.314. The fix set fixture rollForward=disable to enforce its explicitly required 9.0.314, without changing product global.json, weakening assertions or updating host SDK/PATH. Both host hashes still matched. Failed logs/TRX/artifacts remained, and release paused until repaired remote checks passed.

Second [run37509769540](https://github.com/von12549/ArchSift/actions/runs/37509769540): net9 passed on both platforms; only net8 offline restore failed accurately because the fixed SDK needed missing 8.0.27 targeting packs, without network fallback. The repair explicitly prepared the ephemeral CI feed online, deriving/pinning required packs from actual SDK 9.0.314 KnownFrameworkReferences into runner temp. Synthetic tests read only that explicit local feed; the product remained offline by default without ignored failures. SARIF originalUriBaseIds was also added according to official relative-URI semantics, checking consumer location bases as well as schema structure.

The repaired [run37511008332](https://github.com/von12549/ArchSift/actions/runs/37511008332) and final NoProfile-commit [run37511164383](https://github.com/von12549/ArchSift/actions/runs/37511164383) each passed 109/109 on Windows/Linux, zero failed/skipped, covering net8/net9/net10. Final TRX files were downloaded to `D:\ArchSift-lab\evidence\github-ci-37511164383`; both earlier failed artifact sets were archived separately without deletion. Final local URI-fix regression was `development-582cecc73cf04997840c72068c635f43` (109/109); Docker was `linux-c8d019cdce9b452c87e86a55613596df` (107/107, core parity=true). Host hashes always matched.

Formal fixed-asset source commit `528ebb1237256018e3ca556919d25908c9b5c92d`, ZIP SHA-256 `ee2d27b4d7689ce3eef75c0d3402c684ad1c9a8cb492ae7c8e915510038699d8`; output root `release-bcb316f692f44725850ad6867c4bc6ab`, final fresh extraction `smoke-b51e9d52f7e14f5783b452965f2cf627`. All 607 payload entries plus manifest passed byte/path/file-set/license, native CLI/Worker/UI, no-SDK changes and official ordinary/comparison SARIF-schema checks. Five runs measured 113-130ms, 231-263ms and approximately 27-34MiB, below frozen thresholds. W09/W10 User/Machine, Process PATH and four Profiles matched the initial W09 baseline. Packages used committed product inputs; unconfirmed W08 feedback documentation stayed uncommitted/outside the ZIP, so whole-source sourceDirty=true but productSourceDirty=false. [Fixed release acceptance](../releases/0.2.0/acceptance.json) binds final acceptance; frozen manifests remain unchanged.

## Source verification at this historical stage

- Windows x64 / SDK 10.0.303 / net10.0: Release 109/109 (78 unit, 29 integration, 2 architecture), zero build warnings/errors, run `development-76dcf0d5e3024406aa9b1350139c91df`. Final Debug/static-safety regression also 109/109, run `development-3e2011b369e84a97980c17b387bb2c88`. Actual net8/net9 assembly fixtures used installed SDK 9.0.314/packs.
- Linux Docker / SDK 10.0.401 / net10.0 / Release: 107/107 (78 unit, 27 integration, 2 architecture). Offline locked restore, build, real ArchUnitNET/isolated-build, CLI/HTTP/comparison passed. The two fixed-SDK 9.0.314 net8/net9 fixtures were not repeated in that image and were verified on Windows; do not report Linux 109 passes.
- Final Linux run `D:\ArchSift-lab\runs\linux-953b0ab394714fc3941ad42286f7fa6f`, result.json SHA-256 `b543a9f4a36f569f6ac256de9577c7bd59a0d37aca311998a0472949b7096e8e`. The container pinned an existing image ID, disabled network/pull, used a read-only root/cap-drop ALL and external source/output copies. Windows/Linux core JSON for the same two-project violating input matched field by field except machine-path/time metadata, including inputs/findings/coverage/status/engine identity.
- User/Machine environment, Process PATH and four Profile hashes matched every .NET check and completion. Linux workload notices were retained without following them to update or automatically repair anything.

## SARIF and CI

- Analysis/comparison export SARIF 2.1.0. CLI configuration, UI downloads, error/exception/unmapped and fingerprint fixtures passed. baselineState is used only for fully comparable results; partial/policy changes never fabricate resolution. JSON remains authoritative; HTML/SARIF are projections.
- The official OASIS schema was downloaded to external evidence, SHA-256 `ad6db49878699b091f3eeb765b6e29e92a34bad4da88664d000c923b549c3a25`. Independent PowerShell Test-Json verified Windows/Linux ordinary reports and native-package ordinary/comparison SARIF.
- Product Windows/Linux CI and samples/ci workflow_dispatch example pin official action commit SHAs. actionlint passed. The first runner-context-scope error was actually detected/fixed. This stage originally recorded local YAML checks only; remote-CI status depends on actual runs.
- The example changes neither consumer CI/required checks nor code-scanning opt-in. Violation exit 0 and errors 2/3/4/130 remain, with always-uploaded artifacts and no continue-on-error hiding failures.

## Candidate and UI

- Commit `63419d4` first froze W10's fixed-input budget, then the candidate was measured. Windows self-contained prototype ZIP SHA-256 `3991e436833bc3936a919fcf46c503a2e29f6f7fc587506c2e1d540731f3e966` was at `D:\ArchSift-lab\packages\release-c13e99ee76874803aa0c21f98efd7808`. It used an uncommitted W10 worktree and is functional-candidate evidence only, prohibited as the final upload.
- Fresh extraction smoke `smoke-409d0e8f22e74381975c6fac491f4817` passed version/license/hash/byte/file-set, native CLI/Worker/UI, missing-Git exit 3, changes with Git but no SDK PATH (one new/100 existing), SARIF/schema, invalid arguments and unbound exit 4 checks.
- Five candidate analyses measured 111-139ms, wall 231-269ms and sampled peak approximately 29-33MiB, below 700ms/1000ms/64MiB. These declaration-chain inputs promise neither Git-snapshot/IFX/build performance nor forcibly cold disk.
- Final actual synthetic browser run `w09-ui-a028b68eaacd4a80a4dd18afbab8dd23` showed a known new violation and JSON/HTML/SARIF download actions; see the [screenshot](w10/sarif-comparison.png). Authenticated HTTP actually downloaded SARIF. The exact preview PID was confirmed by configuration/entry before stopping: port 12320 listener=0 and preview-host-state-equal=True. No independent operator score or real Guard/IFX run was supplied.

## Release boundaries

The user selected MIT; complete third-party library/.NET runtime notices stayed unchanged. Formal packages would build from committed product inputs, then fix source commit, product-input hash, per-file manifest and ZIP SHA-256 before acceptance/upload. Frozen candidates were not rewritten. W08 and failed/interrupted history remained.

NuGet-tool distribution, W11 allowlist/UX, Linux Web UI, transitive-package/version-range rules, online vulnerability auditing, fail-on and mandatory CI were outside this delivery. No consumer policy was automatically added before release.
