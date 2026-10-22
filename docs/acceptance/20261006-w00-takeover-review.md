# ArchSift W00 takeover review and handoff

Date: 2026-10-06 (Australia/Sydney)

Later status: preserve this initial review. User subsequently confirmed Guard ownership/reuse/distribution; remaining W00 items closed, see [completion](20261006-w00-completion.md). Incomplete/unverified wording below describes the first review.

**READY TO COMPLETE W00**. Plan/repository/toolchain/Guard-IFX references/cached ArchUnitNET closure readable and consistent with handoff. W00 incomplete/W01 unstarted here. Guard source/fixture copying/adaptation still requires rights/license records.

Read-only takeover and entry preparation only. No restore/build/tests/Guard install/real IFX/product creation/source copying/commit/push/remote mutation/release.

## Plan and repository identity

Three imported documents exactly matched handoff SHA-256 before edits:

| File | Imported SHA-256 | Result |
| --- | --- | --- |
| docs/plans/20261006-archsift-implementation-plan.md | `4760fb91708c5931c21f0c51638ad47768d584fb5017dff75c1b10f65acd9090` | Matched; appended W00 status subsequently changes current hash |
| docs/acceptance/20261006-implementation-readiness.md | `00ad1874a792a1877768b709bef6b62d5357a3fd939ea7122a913c6d8825f425` | Matched, unchanged at that point |
| docs/plans/20261006-dotnet-architecture-analyzer-draft.md | `c70ad19f6d2beab6a5d765c1e800418b40e14af4f2e255a6c201906f7df75c31` | Matched, retained historical draft unchanged at that point |

main tracks origin/main; HEAD/local origin/main `a5bc15cca1211b5098edc762440c595c996064f8`, remote https://github.com/von12549/ArchSift.git. No tracked differences; only three imported docs untracked before edits. README.md still # ArchSift, SHA-256 `9bdc57fe03036665439f35cdd2032a180c76c8b23ae3914f546b4716cfc142cd`.

No D:\AGENTS.md/repository AGENTS.md-CLAUDE.md, nor D:\ or repository global.json/NuGet.Config/Directory.Build.props/targets/Directory.Packages.props. No existing instructions/named parent config to merge; W01 explicitly creates ArchSift-owned configuration.

## Toolchain and package access

Read-only: PowerShell 7.6.6, Git 2.49.0.windows.1; SDK 10.0.301/303, 9.0.314, 3.1.426; NETCore/ASP.NET 8/9/10 runtimes and NETCore.App.Ref 8.0.27/28, 9.0.16/17, 10.0.9/11 visible.

Guard build/global.json selects SDK 10.0.100/latestFeature/allowPrerelease=false. Approved W01 reference only; record actual resolution after creating ArchSift global.json.

| Package | Version | Cache | .nupkg.sha512 versus Guard lock | nuspec license |
| --- | --- | --- | --- | --- |
| TngTech.ArchUnitNET |0.13.4|Exists, 2 DLLs|Match|Apache-2.0|
| CycleDetection |2.0.0|Exists, 5 DLLs|Match|MIT|
| JetBrains.Annotations |2026.2.0|Exists, 4 DLLs|Match|MIT|
| Mono.Cecil |0.11.6|Exists, 8 DLLs|Match|MIT|
| Newtonsoft.Json |13.0.4|Exists, 8 DLLs|Match|MIT|

This proves known cache/lock metadata access, not complete ArchSift offline restore/build. No authoritative per-DLL byte verification yet. Only Newtonsoft.Json cache has visible LICENSE.md; no license/notice-named files found for other four. W00/W01 must archive authoritative license/notices, not just nuspec expressions.

## Guard, fixtures and IFX baselines

Approved cross-directory read-only Git checks:

| Object | Current state |
| --- | --- |
| Guard | `C:\Users\von12\OneDrive\Desktop\Guard`；`main`；HEAD `40539d4610889483b4aed4637f8f3457098b1ff0`；ordinary status 0；origin `https://github.com/von12549/Guard.git` |
| IFX | D:\IFX-10-Root\IFX-New; main; HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`; ordinary status 0; IFX.sln exists |

Current Guard candidate hashes:

| Source | Bytes | SHA-256 |
| --- | ---: | --- |
| `modules/architecture-conformance/adapter.ps1` | 46,407 | `d92df650fbda0a5f51a3dcde3e83de78ed29b218b44e56b90c64da2e9dc2781b` |
| `core/host/V4.Guards.Host/ProfileRuntime.cs` | 77,077 | `6b89ee26fb9cdd0c4d6b20c0838949a1916a08412efeece343ea51655b5962ca` |
| `modules/build-evidence-provider/adapter.ps1` | 10,067 | `12ec6b707043477fc0ef0e86cc62ab1671f8f33f9b028ad8958cece1df45c92e` |

modules/architecture-conformance/fixtures currently contains four files:

| Relative path | SHA-256 |
| --- | --- |
| `clean/fixture.json` | `f52e13f73fa16a83fc866bb2e10dc7bf962b7d228d4ecf93239ec5bc5b8b4044` |
| `manifest.json` | `94b9339432e7b08e213853f1dbe49bfd601f38a0139132903a9b2eaba39573a2` |
| `missing/fixture.json` | `788cf1f0bee722f867e0ddbcf198c60f8e0dbe21b7868e340a15fe04576a4b30` |
| `violating/fixture.json` | `c0e2c9d3d77dc4045207fb5651b644adb847f84f49bdc2d30e40b9ffff086954` |

Read-only candidate inventory, not migration approval; nothing copied into ArchSift.

## P05 evidence boundary

P05 final verification/manifest/safety-cleanup audit readable, current SHA-256 values:

- `p05-20261006-final-verification.json`：`07067b32151513e14ebc14975c44b95f1b98ffe26f81794f6f275d7beceaaf59`
- `p05-20261006/final-manifest.json`：`007190e56b555daa67802ff4d5db7da106e806678ee33c461e4505609c1066a1`
- `p05-20261006/cleanup/h4-final-safety-and-cleanup-audit.json`：`bce2afba069e7c08d8224f03e3526c0580ee1a2269c7ceb24b8674b2245ac21d`

Final manifest TechnicalMainPathPassed=true but OverallTestStatus=FAIL, FailurePathPassed=false, HumanUsabilityPassed=false. Stale proof UI retained old profile-configure/exit 0 success; confidence 3/5 required L3. H4 proves unchanged User/Machine environment/Process PATH/Profiles, retained original checkouts and cleaned active test paths.

Real 8/8 is only Guard prerequisite technical history, not ArchSift graph/six rules/ArchUnitNET/parity/cross-machine acceptance. Preserve failed paths/safety evidence, never convert to success.

## W00 provenance/license work still open then

No Guard LICENSE/COPYING/NOTICE in root/repository license-named scan. Personal remote/author names are provenance clues, not copying/adaptation/distribution permission. Before reuse:

1. Create docs/migration/source-inventory.json per asset: repository/commit/path/SHA-256/scope/method(copy-adapt-reference)/destination/verification/owner confirmation.
2. Record authorized rights basis for ArchSift reuse/distribution. If unavailable, do not copy; independently rebuild approved requirements/tests and mark reference-only.
3. Select fixtures individually; directory proximity never authorizes import.
4. Archive authoritative full license/copyright/notices/provenance and actual package scope in third-party-notices.md. Expressions alone do not complete distribution inventory.
5. Check selected assets for third-party snippets/generated content/embedded resources at file level; repository-wide ownership cannot replace this.
6. Create AGENTS.md:read-only targets/external outputs/no persistent env-Profile-PATH changes/paired DOTNET_CLI_HOME and DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0/sequential reviewed human real-integration blocks.

Mark W00 complete only after all six verified. Rights limit copying/adaptation, not inventory/decisions/instructions/new designs.

## Next W01 task card

W01 not authorized by this handoff. After W00 closes:

1. Preserve .git/README; create own AGENTS/global.json/Directory.Build.props/Directory.Packages.props/explicit NuGet.Config/solution/projects.
2. Pin direct/transitive versions/locks; record schema-test rationale/licenses/offline availability. Guard cache alone not complete closure.
3. Add Contracts/Core/ArchUnit/CLI/Web/test dependency checks; foundation/safety only, no premature rule/engine completion claim.
4. First isolated restore/build, paired child CLI-home/global-tools opt-out; actual SDK/sources/offline-network/locks recorded.
5. Run/record full restore/build/architecture tests; preserve failures, no Guard-success/cache substitution.

## Actual blockers and nonblockers then

- **Migration blocker:**Guard rights/file provenance/third-party notices open.
- **W01 buildability unproved:**new projects absent, complete restore/build/cache DLL verification pending.
- **Not blockers:**plan/Git/remote/SDK 10/ref packs 8-9-10/read-only references/known cache confirmed.
- **Task-specific authority:**D:\ArchSift-lab writes/real integration/network restore/remote authorization-push-release need actual-task authorization. Read-only success does not broaden authority.

## Subsequent W00 closure

User confirmed full Guard ownership/reuse-distribution (English translation, original retained in inventory). Created per-file provenance/choices/full licenses-notices/AGENTS. Added System.ValueTuple/System.Collections.Immutable candidates; seven packages/33DLLs byte-verified. Old fixture JSON only coverage claims; rebuild inputs selectively from selected Guard tests/central props under ArchSift contracts. W00 complete/W01 unstarted. Former rights/document blockers now closed.

