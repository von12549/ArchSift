# Lightweight architecture-test Workbench LT02 synthetic gate acceptance

Date: 2026-10-15 (Australia/Sydney). **LT02 SYNTHETIC COMPLETE / LT02A REAL IFX NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**. Code `3ddd0dc0717ce7640b9a6f9755fe60a306f62e8d`, branch codex/archsift-lightweight-workbench; report/plan status receive a separate stage record. Completion covers only the synthetic project, not LT02A original full IFX baseline or real B policy review.

## Deliverables and guarantees

- New headless `archsift test-gate --config <JSON>` calls shared ArchSift.TestGate coordination: explicit project, local offline restore, normal incremental build, MTP test executable. Every child forces IFX_UPDATE_SNAPSHOTS=0; isolated DOTNET_CLI_HOME is paired with DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0. Separate restore/build/test logs go into a fresh external run; cancellation terminates only owned trees.
- Native xUnit v3 JSON TestCaseUniqueID identifies discovered method/case, TestUniqueID identifies executed Theory row. Correlate original IDs through native test-starting/terminal/test-finished events. JSON test-output associates rule ID/file SHA-256 with the same TestUniqueID. No display-name identity parsing or discovery-as-execution.
- Whole project, enabled JSON rules and original-case baseline must be nonempty. Block missing/skipped/nonterminal/duplicate cases, mismatched rule hashes and filtering. Assertion violations exit 2, restore/build/test-process errors 3, uncertain coverage 4, config errors 5, cancellation 130. Relevant source events or explicit config/rule hash changes block. No SourceBound requirement or legacy evidence/snapshot/capture/seal/BindingVerifier execution.
- Test entry builds and reuses the current-process ArchUnitNET model; no cross-process/worktree cache. Existing CLI entry calls the same service; UI waits until LT04.

## Machine checks and input identity

SDK 10.0.303, runtime .NET 10.0.11, Windows x64, net10.0, Release. Explicit local C:/Users/von12/.nuget/packages offline source, ignored development-cache output. Serial locked restore/Release build of 12-project ArchSift.slnx passed with 0 warnings/errors. Native synthetic MTP 14/14; existing UnitTests 151/151, IntegrationTests 95/95, ArchitectureTests 2/2, zero skips.

Before the [synthetic verification script](../../scripts/Verify-Lt02Synthetic.ps1), current native MTP execution generated 13 original synthetic case IDs independently of JSON markers (not an IFX trust baseline), SHA-256 `c86bb6c2439cb4d53d45aa473a4d5c988add7782b25b6bc33714afabfaf10be0`. Passing rules.json SHA-256 `b9669d62d9195f9322a1fda34b46003472564070c8ec492d71234900cc88b341`; deliberate rules-violation.json `f2a9e089715735f07b1ef55f8e4462ef9e4ee6b80dfdbb75780e1683e3470274`. Synthetic inputs only, not enabled IFX B policy.

| Synthetic scenario | Actual result |
| --- | --- |
| Full project: 13 original + 1 JSON | allow/0, restore/build/test passed |
| Deliberate JSON dependency violation with all originals | block/2, actual MTP assertion failure |
| Baseline requires an additional unexecuted original case | block/4, explicit missing case |
| -id filters one original | One original actually ran; block/4, subset, JSON not run |
| Empty local offline feed/cache | Restore failed block/3; normal offline retry allow/0, failure retained |
| Empty rulesets | block/5; no execution or run directory |

Caller sets IFX_UPDATE_SNAPSHOTS=1; every complete child asserts 0, caller remains 1. Cancel a long-running owned child after 300ms and verify prompt termination. Source-watch tests block src/*.cs changes but ignore bin outputs. Negative MTP parsing covers missing rows, skips, duplicates, wrong rule SHA, missing terminal events, process errors and subsets. All .NET before/after environment/Profile hashes stayed `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`. Existing VSTest projects still require approved host execution; LT01 records sandbox testhost connectivity.

## Limitations and LT02A gate

The current synthetic suite supplies its own baseline solely to verify coordination. Config baseline paths/hashes are not an independently trusted IFX retention list. Actual full ArchitectureTests/FixtureTests/ApplicationPortAdapterTests, Theory rows/assertions and five-second performance remain unverified. Only stage logs/immediate JSON exist; full architecture-test-run JSON/HTML, provenance inventory/history/historyFreshness await LT03 and UI LT04. Source watching is lightweight change detection, not full build binding. Authorized D:/ArchSift-lab IFX LT02A must establish trusted native originals, no case/source loss, snapshot positive/negative checks and preliminary cold/warm costs before LT03.
