# LT00: lightweight architecture-test Workbench scope and contracts

Date: 2026-10-15 (Australia/Sydney). LT00 design inputs are frozen; product development and real IFX integration have not started. Follow the [Workbench plan](../plans/20261015-archsift-lightweight-test-workbench-plan.md). Existing ArchSift 0.8.0 release and acceptance identities remain unchanged.

## Identity, components and support

- **ArchSift Architecture Test Workbench** is a new testing workflow in the existing repository, with development identity `0.9.0-dev`. Initially it releases no independent NuGet package and adopts no InProject installation identity. The public version follows LT06 acceptance and separate publication authorization.
- Proposed headless entry: `archsift test-gate --config <file>`. UI calls the same coordinator. Shared rule interpretation and type analysis have no xUnit, CLI or Web dependency. The xUnit v3 adapter loads configuration, creates stable cases, invokes ArchUnitNET and asserts. Coordination owns target, processes, coverage, cancellation and reporting; it does not precompute findings for xUnit to repeat. Project names may follow this dependency direction; LT00 needs no empty scaffolding.
- Initial support: SDK-style C#, the IFX net10.0 architecture-test project, xUnit v3 4.x, Microsoft.Testing.Platform and Windows x64. Use the .NET 10 SDK required by IFX global.json with normal offline restore and incremental builds. Mixed TFMs, other test frameworks, Linux Web, arbitrary external DLLs and plugins are excluded. The existing product test stack is not upgraded wholesale.
- Run the entire original IFX test project and preferably add JSON tests there. Consider a thin separate project only for actual dependency conflicts; the complete entry must still run all original tests. Original target, installation and business source are read-only design references. LT02A integrates an authorized external copy.

## Input identity and responsibilities

LT00 read-only checks found clean IFX HEAD `cc06528ab17052baafa496df2a89f6cde9207384`. The [policy inventory](20261015-lightweight-policy-inventory.json) records five installed rulesets, IDs, types, hashes and review relationships. It is navigation/review metadata, not execution configuration, a C# interpreter or new policy. `dailySelection=not-selected` means no reviewed real B JSON is enabled; LT01 uses synthetic rules only. Future formal configuration explicitly records reviewed paths, SHA-256, enabled IDs, scope and exceptions, never enabling all 406 candidates by default.

| Source | Static baseline | Daily responsibility/limits |
| --- | --- | --- |
| Original IFX C# | ArchitectureTests, FixtureTests, ApplicationPortAdapterTests | The whole project is required. ProjectGraph, LayerPolicy, Handler, TenantId, ApplicationOwnedPorts, Catalog, permissions, API snapshots and fixture assertions remain owned by original C# tests; never filter or replace them. |
| Original A JSON | Four sets, 24+33+35+90=182 | Original ArchSift/Core owns the unmigrated increment. One target-framework and two graph-integrity rules are excluded. Compare 89 project allowlists and 90 package rules individually with IFX assertions. The 25 empty package allowlists overlap the zero-package policy; other exact whitelists differ from prefix bans. A review relationship does not establish equivalence. |
| B candidates | 406 type-dependency rules: B01 133, B02 5, B03 100, B04 63, B05 100, B07 5 | Only reviewed enabled rules enter parameterized tests. B02 all-relationship IServiceProvider checks exceed original constructor/field assertions; B03 type relationships differ from TransactionScope calls; B07 differs from assembly and recursive-signature checks. Preserve parallel results. B05 first-party Newtonsoft API candidates are not equivalent to existing package policy. |

The complete daily claim covers all actually executed original cases plus reviewed B JSON cases enabled in formal configuration. Keep A reports separate; show excluded source, responsibility and reason. Old success never contributes to current pass counts. Full evidence retains its separate 0.8 workflow and SourceBound guarantees without claiming execution of the new tests.

## Original IFX retention baseline

The [source inventory](20261015-lightweight-ifx-source-baseline.json) records the original project, 16 references, 20 test/rule/infrastructure/fixture C# hashes, each Fact/Theory and InlineData text. Static counts: ArchitectureTests 16 methods/16 cases; FixtureTests 41/47; ApplicationPortAdapterTests 8/17; total 65 methods/80 derived cases. **These are not runner discovery or execution results.** Compare every original method, Theory row, assertion and reference; an equal count of new JSON tests cannot compensate for deletion. Preserve AllRules_RunUnderFiveSeconds and its five-second threshold.

Authorized LT02A compares native discovery/execution before and after integration, establishes actual case identities and verifies complete execution, no skips and no assertion/Theory loss. Count JSON cases separately. Names, static counts and file existence do not prove execution. Unreliable structured identities/results must block the complete gate until the adapter is corrected.

## Reports, coverage and exits

The separate [report schema](../../schemas/architecture-test-run.schema.json) uses type architecture-test-run and schemaVersion 1. JSON is authoritative; HTML is its safe projection. Record current run ID, target/worktree/Git context, SDK/TFM/Configuration, project, rule bytes, original baseline, discovered/started/completed/skipped/missing cases, source/executor, native assertions, JSON states, findings, exclusions, stages and final gate. Git/dirty are context, not source/build causality.

- runGate is the immutable conclusion of this run. Full acceptance requires forced snapshot updates off, successful normal restore/build, every original/enabled JSON case completed and passed, no required skips/duplicates/missing cases or known relevant input changes. Any filter is subset and diagnostic only. Zero source matches default to inconclusive. No effective JSON rules, unknown enabled types or unfrozen inputs cannot allow.
- historyFreshness is a read-only current/stale/unknown projection, not persisted gate proof. A valid allow may have unknown historical applicability. Later changes affect history display only, never the original report/exit or an automatic rebuild/evidence capture.
- Preserve native passed/failed/skipped/not-run and JSON pass/violation/inconclusive/not-applicable/error separately. Without structured IFX Violation, show original assertions only, never invent subchecks or source/type locations. Reading, discovery, viewing, editing, old passes, candidates and excluded items are not current passes.
- New exits: 0 complete pass; 2 violation/assertion failure; 3 restore/build/test/report error; 4 uncertain coverage or known input change; 5 invalid/unsupported input; 130 cancellation. Record all issues; priority is cancellation, invalid input, execution error, uncertain coverage, violation, pass. Legacy CLI exits stay unchanged.

Failed authoritative JSON saves never display success. Diagnostics and nonzero exits remain possible; rerun after fixing writes. Initial history includes owned report directories, basic terminal states and a read-only list; missing materials are unavailable. No rotation, pinning, archive recovery or replay.

## Child processes and measurements

Every controlled restore/build/test child explicitly sets IFX_UPDATE_SNAPSHOTS=0 or is rejected. Snapshot updates remain separate development operations. Authorized LT02A verifies parent 1/child 0, matching pass/mismatch failure, unchanged snapshot bytes and parent value. Isolated DOTNET_CLI_HOME must pair DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0 in the same child. Record hash-only host/Profile comparisons before .NET, at key stages and completion.

Compare the same IFX commit, rule bytes, SDK, TFM, configuration, offline source, host and parallelism. Measure native full tests, headless gate and UI separately: restore/build/test/report stages, total wall time, peak resources, owned report growth and success/cancellation states. Cold means a new process without current build outputs; warm reuses incremental outputs. Record cache state without silently deleting shared caches. LT02A observes initial costs; LT05 uses at least three comparable cold and warm runs, medians/ranges, without a premature speed budget. Label filtering, rules and rebuild differences; unequal durations do not establish improvement.

Only contracts and read-only inventories are frozen here. LT01 direct testing, LT02 coordination, LT02A real IFX and later UI are accepted separately in dependency order.
