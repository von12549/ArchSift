# LT05 IFX regression and matched measurements

Date: 2026-10-15 (Australia/Sydney). **LT05 MACHINE COMPLETE; LT06 NOT STARTED; PRODUCT NOT ACCEPTED / NOT RELEASED**. All target operations use authorized external D:/ArchSift-lab copies/worktrees. Original checkout/install read-only; final independent visual/hands-on UI user-owned.

## Frozen inputs and baseline

Target D:/ArchSift-lab/fixtures/lt05-shared-cc06528, source `cc06528ab17052baafa496df2a89f6cde9207384`; SDK 10.0.303/net10.0/Release/MTP-CTRF. Explicit offline C:/Users/von12/.nuget/packages plus external D:/ArchSift-lab/packages/lt02a-ifx-cc06528-20261015. Rule SHA-256 `abd6a7d94e3655b4255b2a280e8fa58c6c50e64ce91dce6848121d24d20a92aa`; adapter matches LT02A sample. No formal B policy adopted.

At new path all 80 original names/classes/Theory rows matched LT02A and passed, but every CTRF execution ID differed from that other worktree; repeated same-build IDs stable. Stop on mismatch, retain D:/ArchSift-lab/runs/lt05-metrics-20261015/native-1/cold and id-diagnostic, exclude measurements. First shared preflight: 80 original pass/adapter failed without explicit ARCHSIFT_TEST_GATE_CONFIG, retained baseline-preflight-1/excluded. Child-only explicit config baseline-preflight-2 passed 80+1; compare 80 names and create fixed-path worktree-original-case-ids.json, SHA-256 `74a1d63dabbb0be14e139134661fed6bf0af77fe3ba00f13fc69959de66ca72b`. Old LF baseline `55b0f65b535516993761a0121ed4b6db26a98a698291c03056b1a85052cf3a33` retained, never auto-rewritten. Later configs explicitly bind new path/hash.

Cold=same worktree/new controlled process/no target bin-obj. Before each cold run verify absolute containment and clean only 178 recorded tool directories, external clean-before-*.json receipts. Warm immediately reuses outputs; packagecache/source/prior reports untouched. Native/CLI/Web share source path/commit/rules/SDK/TFM/config/offline inputs. Native runs same 80+1; CLI/Web add shared reports/gates. Web cold wall includes service launch, warm reuses service. External Testing outputs/OS caches retained, so not fully cold host/dependencies.

## Three cold/warm rounds and resources

All 18 measured runs passed 80+1, no skips/missing IDs. Every CLI/Web authority allow/0,588 excluded. Native same 81 with CTRF, without claiming new report semantics. Median/range below; per-stage/RSS/bin-obj/report bytes D:/ArchSift-lab/runs/lt05-metrics-20261015/measured and summary.json SHA-256 `63c1febee31667bd0b948570ed43832452ad022398ae53de1758bd6cdfd615c9`.

scripts/lt05_measure.py records commands/post checks: prepare native 1, baseline native 1, then separately clean/run-cold/run-warm <entry> <round>; summarize reads only 18 accepted records. Reject existing worktrees/measurements/summary; reruns need new isolated root/review. Retain stopped preflights/diagnostics.

| Entry | Cold wall s | Warm wall s | Cold peak tree RSS MiB | Warm peak RSS MiB | Cold reports KiB | Warm reports KiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Native IFX |118.129 (118.014–118.439)|12.796 (12.784–13.108)|599.8|300.0|173.0|157.9|
| CLI | 117.736（117.728–117.976） | 12.272（12.220–12.435） | 659.4 | 346.2 | 513.2 | 498.0 |
| Web | 118.419（117.907–118.424） | 12.367（12.251–12.449） | 702.6 | 403.3 | 513.4 | 498.3 |

Cold build medians: native 106.150 s, CLI estimated 106.227 s, Web 106.345 s; restores 3.327/3.354/3.331 s. Warm builds 5.198/5.204/5.012 s, tests 3.397/3.355/3.378 s. Web cold startup 0.212 s. CLI/Web report stage estimated 0.2 s; native CTRF included test. Cold target outputs 147.2 MiB; 18 owned report directories 7,231,150 bytes. Peak sums sampled owned processes, not instantaneous host peak; CLI/Web stages estimated from sequential log mtime. Similar build-dominated costs prove no material speedup. No rule/lifecycle expansion or noise-sized improvement claims.

## Three-entry parity and actual failures

Final matched CLI/Web/local headless CI all allow/0. rule SHA, 80 original IDs/results/executed, one JSON, exclusions, gate, snapshotUpdatesDisabled=true agree. Original classes 16/47/17, no deletion/skip/relaxed five-second assertion.182 A+406 disabled B remain excluded, including TFM/solution membership/resolved refs/closed packages; old passes/daily allow do not check them. D:/ArchSift-lab/runs/lt05-metrics-20261015/parity.json. CI=true is local simulation, no IFX workflow/required check/policy deployment.

Same external worktree negatives restore original inputs in finally. Parent-controlled IFX_UPDATE_SNAPSHOTS=1 only in child environment; gate children 0. API+1 byte: 80+1 execute, only PublicApi_ContractsSnapshotsMatch fails, block 2/findings 0, report SHA-256 `9a22bd6908435acfcc999f40fea6b69df480ffe372305446fce60d7b3bc0479a`. Temporary forbidden EndpointScopeMetadata → ExecutionScope: 80 pass/only JSON fail, block 2/one finding, SHA-256 `67b4df5e3f1075b671d53b7ffbbde76f377e25708a0c9b6fe7a01fddfe4323e9`.11 snapshots match original, rule restored abd6a7..., earlier success separate.15 CLI-Web-CI-negative JSON reports schema-valid with local NJsonSchema 10.9.0, zero errors.

## Source, host and remaining scope

LT00 original 20 source hashes equal, 16 refs retained plus Testing, original five-second assertion, 11 snapshots match. Original Git clean; external only LT02A reference/NuGet/adapter/rules. Solution Release 0 warnings-errors; Unit 151/151, Integration 95/95, Architecture 2/2, native Testing 17/17, no skips. Key restore/build/test/schema/regression before-after environment/Profile hash `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`.

Machine scope complete; user visual/interaction/final independent acceptance pending. No publication/PR/merge/IFX policy/persisted host changes.
