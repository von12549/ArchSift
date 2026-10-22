# ArchSift lightweight architecture-test Workbench implementation plan

Date: 2026-10-15 (Australia/Sydney). **LT00–LT06 COMPLETE / 0.9.0-dev LOCAL CANDIDATE ACCEPTED WITH LIMITATIONS / NOT RELEASED** at this point. Original planning authorization/scope remain below.

Release update 2026-10-16: user authorized PR, merge and publication as 0.9.0-dev prerelease; formal IFX policy follows publication separately. Scope is accepted local candidate source; public assets only tagged source archives, not path-bound local payloads as generic installers. [Notes](../releases/0.9.0-dev-notes.md). Record actual PR/CI/merge/release evidence, never equate authority with success.

Final human update 2026-10-16: user individually confirmed UID search/fill, languages, Results/History integration, existing analysis entry, narrow/keyboard. [Human](../acceptance/20261016-lightweight-lt06-human-ui-review.md)/[machine](../acceptance/20261016-lightweight-lt06-ui-revision.md) complete local LT06. Acceptance binds manifest-fixed 0.9.0-dev; [limits](../lightweight-test-workbench-candidate.md) retain full-source binding/excluded rules/portability. That review itself did not authorize PR/merge/publication/install/IFX adoption.

Earlier revision update: first human found UID difficult, Chinese-only tests, results after long history and global-page separation. [Observations/checklist](../acceptance/20261016-lightweight-lt06-human-ui-review.md) preserve feedback. [Revision machine checks](../acceptance/20261016-lightweight-lt06-ui-revision.md) add UID search/fill, languages/global Results/History; fresh Web real IFX 80+1 allow 0 with matching coverage/identity. Human recheck/commit/push pending then.

Earlier LT06 [candidate](../acceptance/20261016-lightweight-lt06-candidate.md) fixes development 0.9.0-dev/source e0b3bdd/external CLI-Web hashes. Offline locked restore/265 tests/packaged real IFX 80+1 allow 0/Web inventory/host passed. [Guide](../lightweight-test-workbench-candidate.md)/[blank human](../acceptance/20261016-lightweight-lt06-human-ui-review.md) ready, unaccepted/unreleased then.

LT05 update: [measurements](../acceptance/20261015-lightweight-lt05-ifx-regression-measurement.md) same external worktree/native-CLI-Web/three cold-three warm each=18, all 80+1 passed. Local headless CI/parity agrees. API/JSON negatives block 2, source/rules/11 snapshots/host restored-equal. CTRF IDs not reusable across worktrees; compare 80 names/create explicit local baseline. Cold 118 s rebuild-dominated, no clear cost improvement; LT06/human pending then.

LT04 [Web acceptance](../acceptance/20261015-lightweight-lt04-web-workbench.md): reused editor, three sources/full-subset-cancel/history/downloads. External IFX Web 80+1 allow 0/subset block 4/cancel 130/real JSON negative block 2-one finding. Matched CLI allow 0/cases-exclusions agree.248 legacy+17 Testing passed, host/original/copy stable. LT05/human pending.

LT03 [reports/history](../acceptance/20261015-lightweight-lt03-reports-history.md): frozen JSON/safe HTML/actual JSON findings-native assertions/exclusions/history. Real 81 positive/two block 2 negatives/save failure block 3/stale-unavailable-interrupted passed.248 legacy passed; UI/measurement/human unstarted.

LT02A [real gate](../acceptance/20261015-lightweight-lt02a-ifx-gate.md): authorized cc06528 copy/native 80 pass/integrated same 80+1/allow 0/API-JSON block 2/parent 1-child 0/snapshots unchanged.20 original files/16 refs/five-second assertion retained.Original read only, LT03-04 unstarted/no formal B-human pass.

LT02 [synthetic gate](../acceptance/20261015-lightweight-lt02-synthetic-gate.md), code 3ddd0dc:headless/offline-incremental-MTP/full case checks/snapshot isolation/cancel-logs-source watch positives-negatives, 248 legacy passed. Real IFX/native baseline/cold-warm remain LT02A prerequisite, not run then.

LT01 authority covers later development/acceptance/stage commits-pushes and specific LT02A/LT05 external IFX copies/worktrees/reviewed offline build/tests/snapshot negatives/measurements under D:/ArchSift-lab. Originals read only/final UI user. [Synthetic](../acceptance/20261015-lightweight-lt01-synthetic.md)082d0ec/actual ArchUnitNET-MTP/regressions/limits. LT02 unstarted, LT02A waiting it.

Initial execution authority created/pushed codex/archsift-lightweight-workbench plan-first d1b089d, then LT00. [Contracts](../decisions/20261015-lightweight-test-workbench-lt00-contracts.md), [static](../acceptance/20261015-lightweight-lt00-contracts.md), [baseline](../decisions/20261015-lightweight-ifx-source-baseline.json), [responsibilities](../decisions/20261015-lightweight-policy-inventory.json), [schema](../../schemas/architecture-test-run.schema.json) complete; later LT00 commit-push separately authorized. Runtime counts/JSON/IFX/next stage/PR-merge-release not yet authorized.

Direction: preserve dedicated IFX tests; JSON drives ArchUnitNET parameterized tests; UI edits/starts/reports. Daily gates independent of full evidence. Original authority only saved/pushed old branch, returned main, wrote plan/index; later updates govern expanded authority.

Review 2026-10-15: require whole original test project, force snapshot updates off, separate gate/history, add early LT02A before reports/UI, limit history to independent directories/basic state/list. Docs-only revision.

Responsibility/UI supplement: full original IFX+reviewed enabled B only; unmigrated A remains original ArchSift, not current coverage. Show provenance/executor/inventory/actual results/exclusions, retain C#authority, reading inventory does not interpret C#interpretation/editing. Unexecuted/uncovered never passed; stage/LA18-19 updated, docs only.

## 1. Baselines, provenance and legacy preservation

- Design baseline main `acbcff1c18b30f33a41c98e8e76cc5a2ecd3d68e`, post 0.8 publication records. Recheck actual main/changes before implementation, no stale-worktree assumptions.
- Two existing old branch changes saved unchanged as `2877974fb38a12e856a6045ba223615c200841ee`/pushed: IFX upgrade closeout/evidence-cancel-shared-reference graph regressions, no InProject code changes. Returned clean main equal remote.
- InProject IP08 MACHINE COMPLETE/HUMAN PARTIAL/WORK PAUSED/NOT ACCEPTED/NOT RELEASED. Keep code/spec/plan/evidence/branch unchanged, no merge/rename/delete/new-contract rewrite.
- Main has no InProject files. Fixed [old plan](https://github.com/von12549/ArchSift/blob/2877974fb38a12e856a6045ba223615c200841ee/docs/plans/20261014-archsift-inproject-0.1.0-implementation-plan.md), [IP08](https://github.com/von12549/ArchSift/blob/2877974fb38a12e856a6045ba223615c200841ee/docs/acceptance/20261015-inproject-ip08-ifx-machine.md), [service](https://github.com/von12549/ArchSift/blob/2877974fb38a12e856a6045ba223615c200841ee/src/ArchSift.InProject.Runtime/InProjectRuleService.cs), [bridge](https://github.com/von12549/ArchSift/blob/2877974fb38a12e856a6045ba223615c200841ee/src/ArchSift.InProject.TestBridge/ArchitectureRuleBridge.cs). No nonexistent main-relative links.
- IFX reference D:/IFX-10-Root/IFX-New, commit `cc06528ab17052baafa496df2a89f6cde9207384`. Recheck target/dirty/tests at execution; plan alone grants no writes/runs.
- New same-repository workflow, provisional lightweight architecture-test Workbench. LT00 decides official identity/version/commands/install, never InProject release identity/new repository clone.

## 2. Initial scope

### 2.1 Required

1. Full IFX.Architecture.Tests plus JSON; preserve all tests/Theory/positive-negative/assertions, not subsets/equal counts/generic replacements.
2. Project parameterized tests directly read validated JSON/call ArchUnitNET/emit per-rule results/findings.
3. Initially schema 1 type-dependency only; other enabled types reject explicitly, not silent skip. Extensions require scoped ownership/acceptance.
4. Prefer IFX net 10/xUnit v3/MTP; existing product tests unchanged, no wholesale framework switch.
5. Headless runs, shared CLI-CI-UI execution/coverage.
6. Normal current-worktree restore/incremental build/tests, lightweight reports/logs/explicit gate.
7. UI edits/saves/selects target/full-subset/start-cancel/progress/current results/history/downloads. Unified source/executor/inventory/results, subset/uncovered not full pass.
8. Separate existing 0.8 full-evidence entry/results, not third proof system.

### 2.2 Excluded

- No InProject completion/modification or old IP results counted here.
- No daily full business snapshot/per-project compilation proof/DLL-package archive/SourceBound requirement.
- No history proof rebuild/rotation-cleanup/pinning/reference protection/archive-recovery/capacity system.
- No arbitrary UI C#/scripts/MSBuild/plugins.
- No complete A migration/generic Handler-TenantId-Catalog-permissions-API DSL.
- No arbitrary DLLs earn daily allow/no default no-build unknown outputs.
- No all framework/mixed TFM/multiversion-load promise; explicit support matrix beyond IFX net 10.
- No automatic IFX policy/required CI/deployment/app/all functional integration tests.
- No automatic NuGet publication/install/report upload/branch protection.

## 3. Responsibilities and authority

| Policy | Authority/executor | Initial treatment |
| --- | --- | --- |
| Project boundaries/packages/frameworks/cross-project package bans | IFX ProjectGraph/LayeringRules/LayerPolicy | Original tests, no redundant equivalent A JSON daily |
| Handler/DDL outside Migrator/TenantId/DbContext | IFX TypeRules/C# | Preserve original/negative fixtures |
| ApplicationOwnedPorts/assembly ownership/naming | IFXC# | B07 not replacement for actual assembly/recursive signatures |
| Catalog/permissions/authorization/API snapshots | IFX models/tests | Original native results |
| Generic type bans | Versioned JSON/adapter/ArchUnitNET | Editable JSON, one case per enabled rule |
| TFM/solution/declaration increment | LT00 inventory | Not automatically migrated, independent tests if needed, not XML via ArchUnitNET |

Full daily means original whole project+reviewed B, not automatic union of old A/B/IFX coverage.Excluded A listed, inability to claim full-policy compliance from daily allowance.

B02 IServiceProvider/B03 TransactionScope/B05 Newtonsoft may be stricter increments; review scope/exceptions individually, old zero findings not approval.

182 A/406 B expanded counts are not dedicated policy-equivalence. Inventory/JSON/duplicate Fluent definitions cannot all be authoritative.UI has no extra effective policy copy; imports-exports for selection, project configuration determines execution.

### 3.1 Original IFX retention

Minimum whole IFX.Architecture.Tests, not ArchitectureTests-only/editable groups. Classes below illustrative, future classes normally executed, not a whitelist.

| Class | Required responsibilities |
| --- | --- |
|ArchitectureTests|All actual source/assembly/directory rules/five-second assertion|
|FixtureTests|Positive-negative/Theory/infrastructure|
|ApplicationPortAdapterTests|Context/authorization/audit-events, behavior assertions not excluded|

LT00 file/case/Theory/assertion inventory; LT02A actual runner before-after/no originals lost/JSON separate. Counts insufficient: deletion-plus replacement, skips/reduced Theory/assertion changes/relaxed budget not retention.Normal extensions can update baseline, coverage reductions require review, not UI approval.

Source inventory supports integration/change review. Daily native results verify execution without parsing all C#/source-retention proof/full tree hash chain.

Full entry always whole project, no filter originals. Any case/group/filter subset cannot allow even all selected pass. Separate JSON project still requires original whole project.

### 3.2 Execution ownership

| Check | Authority/executor | Daily scope | UI |
| --- | --- | --- | --- |
| Proven equivalent A project/zero package | original IFX C#, old JSON retained | Specific verified assertions, no equivalent JSON duplicate | Actual test owner/reference only, no fabricated execution |
| Unmigrated A TFM/solution/exact packages/declarations | Original ArchSift JSON-Core | Excluded, gaps listed, later decision | Show old tool/excluded entry/history |
| Reviewed enabled B type | Project JSON/adapter/ArchUnitNET | One identifiable case per rule | Source-ID-scope-reason-state-finding/edit registered JSON |
| Partial overlap B02/B03/B07 | separate C#/JSON authority | Preserve originals, approved increment | Parallel scopes, no merged execution |
| IFX dedicated Handler/TenantId/ports/DbContext/Catalog/permissions/API | C#/models | Whole project, no JSON conversion | Native cases/results/assertions/available Violation, C#logic maintained |
| FixtureTests/ApplicationPortAdapterTests | C#/Theory/fixtures | Whole project | Actual cases, not files as execution |
| Unreviewed-disabled B/unsupported JSON | Original files/tool | Not automatic, 406 not default approval | Candidate-disabled-unsupported reasons, enabled required unsupported rejects |

LT00 lightweight responsibility inventory: identity/source/executor/test-old entry/equivalent-overlap-unmigrated/current required/reason. Navigation metadata, no DSL/C#policy inference/name-equivalence. Separately compare TFM/integrity/closed packages/prefix bans.

Retain A JSON, no automatic test import.New entry only supported types.Old editor still manages old rules; UI excluded read only/old entry, view-edit not execution.Old success keeps run-target-rule-time, never fills missing daily results.

## 4. Component boundaries

```text
Rule-editing UI ── validate / atomic save ──> Project JSON
                                    │
UI / headless entry / CI ──> Lightweight run coordinator
                                    │
                           Normal restore / build / test
                                    │
                    ┌───────────────┴──────────────┐
                    ▼                              ▼
             Dedicated IFX C# tests            JSON parameterized tests
                    │                              │
             Original rules / fixtures         Shared rule and analysis library
                    │                              │
                    │                         ArchUnitNET
                    └───────────────┬──────────────┘
                                    ▼
                    Native results + structured rules / findings
                                    │
                        Required coverage / gate summary
                                    │
                         JSON / HTML / UI history

Full-evidence entry ──> Existing ArchSift evidence workflow / reports
                    (Independent, never a daily prerequisite)
```

| Component | Owns | Exclusions / dependencies |
| --- | --- | --- |
| Shared rule library | Schema/semantic validation, selectors, exceptions, type model, findings | No xUnit/CLI/Web; no builds, test launch or full-evidence binding decision |
| Test adapter | JSON to stable cases, analysis calls, assertions, structured output | No Worker-result playback or fabricated SourceBound |
| IFX integration | Original rules/assertions, small existing-Violation adapter | No rewritten detection or weakened assertions for reporting |
| Coordinator | Worktree/config/processes/cancel/basic state/native discovery/results/original and JSON coverage/provenance | No parallel B evaluation, display-name identities, material lifecycle or task recovery system |
| UI / lightweight CLI | Source/responsibility/inventory/results/exclusions, explicit requests, old-tool/report entries | No separate gate/C# semantics editor, hidden failures or Web prerequisite for CI |
| Full evidence entry | Existing config/workflow, independent provenance | No claim of all IFX tests or relabeled daily proof |

Prefer JSON in IFX.Architecture.Tests with real project references. If LT01 finds conflicts, use compatibility evidence for a thin separate test project, not another business-build/evidence host. LT00 freezes shared files/names; no empty plan-stage projects/solution changes.

## 5. Reuse from main

| Asset | Reuse | Boundary |
| --- | --- | --- |
| [RuleLoader](../../src/ArchSift.Core/RuleLoader.cs), [SelectorMatcher](../../src/ArchSift.Core/SelectorMatcher.cs), schema | Reuse directly or extract host-free logic if needed | No silent schema 1 semantic changes; unsupported enabled types reject |
| [AssemblyWorker](../../src/ArchSift.ArchUnit/AssemblyWorker.cs) type/generic traversal | Extract minimal shared computation/interface | Never call old bound-verification chain then fake binding |
| Finding identity, exceptions, escaping/display | Reuse semantics/pure projections | Old reports/IDs unchanged; separate test-report contract |
| [Web editor](../../src/ArchSift.Web/wwwroot/app.js), validation/save | Adapt existing controls/services | Old Verify/Chain endpoints never become test runners; no auto-import of all library rules |
| History/progress/process/path safety | Necessary display/safety reuse | No evidence registration/rotation/pinning/reference graph/archive recovery; distinguish old/new reports and cancellation owners |
| [Evidence](../external-build-evidence.md), [CLI](../cli.md), [reports](../reports.md) | Preserve independent entry/behavior | No daily dependency or evidence-schema reinterpretation |
| InProject branch | Read-only design/failure lessons | No copied whole runtime/installer/binder or automatic resumption |

Shared extraction must run identical valid/violating/generic/exception examples through old/new entries plus legacy regressions. Existing provenance/license constraints still apply.

## 6. Daily execution

1. Read explicit worktree/configuration/TFM/rules/mode. Full includes whole original project plus JSON; any filter is subset. Validate target/SDK/runner and reject unregistered/unsupported combinations.
2. Freeze current rules, retention baseline and case identities. Editing cannot change completed semantics. Record source context and necessary local input summaries without source copies or full compilation-causality tracing.
3. Force IFX_UPDATE_SNAPSHOTS=0 in every controlled build/test child, overriding inheritance without parent/persisted writes. Normal restore/incremental build with explicit offline/source policy; no silent network/ignored failures or mandatory cache clearing/full rebuild.
4. Run all original classes, fixtures and adapters plus JSON. Original tests read actual target; JSON reads explicit current test outputs/target assemblies, never recursive arbitrary bin scans or fixture-as-production.
5. Share ArchUnitNET only inside current process/valid assembly context. Rebuild for changed rules/context, never global cross-worktree/TFM/config/DLL cache reuse.
6. Native discovery/execution/terminal records plus JSON rule key/matches/types/findings/limits bind run ID, without extra receipt/signature-style chains.
7. Aggregate required coverage/results and save gate. Fail/cancel/incomplete output retains current diagnostics, never stale success.
8. Known relevant input/rule changes during execution block with reason. Post-run changes affect only stale history; unknown observations stay unknown. Never rewrite original gate/results/exit. Lightweight monitoring claims no full causality.

Single IDE tests are subset diagnostics. Full entry satisfies section 3.1. CLI/CI use shared headless coordination or equivalent commands/environment/result checks, no UI requirement. Test process asserts; coordinator checks completeness/summary.

### 6.1 Snapshot update isolation

PublicApiRules with IFX_UPDATE_SNAPSHOTS=1 writes baselines/skips comparison. Full/subset children must force 0, unoverrideable by config/environment options. Log only snapshot updates disabled, not other raw environment values. Reject if unenforceable, never update then pass.

Keep original update code. Explicit developer snapshot updates remain separate, never a Workbench gate mode or imported full success.

Authorized external-copy acceptance: parent 1/child 0/parent unchanged, mismatch fails with unchanged snapshots, matching baseline passes. UI/CLI/CI share enforcement, never modify host.

## 7. Configuration, storage and host

- Prefer tracked architecture-test rules/config, e.g. tests/Architecture/IFX.Architecture.Tests/Rulesets and adjacent config; proposed only, real IFX writes separately authorized. Paths resolve from config; native tests use same input.
- No .archsift/runtime, version payload or dedicated SDK runner. Existing project central versions/locks pin test dependencies; UI does not manage NuGet/build runtime.
- Save registered JSON only, validate then atomic replace with conflict checks. Unsaved drafts excluded; disable/exception/delete are policy changes, not automatic source repairs.
- Normal development owns source/projects/config. Daily builds write bin/obj and need explicit integration authority; no inherited promise of zero target writes.
- External owned run roots, development/acceptance under existing D:/ArchSift-lab/runs rules, registered CI artifacts. Separate worktree/run/success/failure/cancel.
- Initial history: independent report directories/logs/basic states/read-only list. Latest actual outcome never falls back to old success. Summaries or rebuildable simple index, no reference graph.
- No automatic deletion/rotation/archive, 30-day/100-run policy, pinning/reference/capacity system. Show paths; user manages after jobs. Missing unavailable, original result unchanged, no rerun. Never clean business outputs/shared cache/old evidence/InProject.
- Keep cancel/exit handling. Unconfirmed leftover jobs after restart interrupted/unknown, never success/resume. No persistent lease/replay system.
- No User/Machine/registry/Profile/startup/PATH writes. Isolated child DOTNET_CLI_HOME pairs DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0.
- Hash-check host/Profile before/after development/real builds/tests; drift stops/preserves without repair. Cancel exact owned trees only.

## 8. Results and gate

Separate versioned architecture-test-run, not old report with SourceBound=false bypass or ordinary testing relabeled SourceBound=true. LT00 freezes contract.

Minimum: run/worktree/Git/dirty/target/project/SDK-TFM-config/rule hashes/test-adapter-engine versions/source-executor/baseline/discovered-actual IDs/native results/raw rule states/findings/execution/coverage/exclusions-reasons/runGate/logs/material paths. historyFreshness separate display, no gate-proof prerequisite. Git is context, not byte/build proof.

### 8.1 Current gate versus historical applicability

- runGate: this normal build/full test succeeded according to child constraints, outputs, original/JSON coverage and known input changes; immutable after completion.
- historyFreshness: known changes stale, missing observations unknown; identical observed context proves only observed scope, never SourceBound.
- Valid full allow may have unknown history. Unknown does not block run or trigger rebuild/full-tree hashes/input capture/binding, nor prove current source compliance.
- Missing required execution/results or known run-time changes still block. User explicitly reruns for current results, no background evidence acquisition.
- UI separately shows run result/time and current applicability, never one merged green state. Later edits do not turn historical pass into violation.

Show native IFX results/assertions first; adapt actual Violation when available. No invented subrule counts/type/source locations. Coverage may use tests/groups; multi-assertion Fact is not multiple independently executed cases.

Prefer native MTP structured results; TRX only when actually supported, no forced VSTest/display-name guessing. JSON/HTML initial; LT03 decides optional SARIF without blocking basic correctness.

### 8.2 Unified inventory/display

| Source | Inventory/results | Operations |
| --- | --- | --- |
| B JSON | Registered formal JSON, adapter/native identities/findings | Edit/validate/save/scope/results/full/subset |
| Dedicated IFX | Actual native project/class/method/Theory, assertions/Violation | View/full/subset, no JSON-like semantic editing |
| Original ArchSift / excluded A | Registered policy/responsibilities/old entries/independent reports | View ownership/exclusions/open original, no unexecuted current pass |

Each item shows source/executor/identity/scope/requiredness/discovered-executed/result/reason, optional actual duration/assertion/types/location. Expand project→class→method/Theory, all three original classes visible, JSON separate; never disguise C# as JSON.

Use native discovery/results, not a full C# parser. Build/load only explicit discovery or normal current flow; reuse output, never page-open builds/per-rule rebuild/evidence jobs. Undiscovered shows none; old lists carry time/context, not current execution. Runner/adapter identities govern coverage, not names.

A Fact may check multiple policies. Preserve actual result/messages; responsibility metadata explains relations without fabricated subresults/counts/lines. No automatic C# understanding; source links only with real locations, no mandatory source browser; code review still governs dedicated tests.

Excluded A/disabled candidates/unexecuted/discovered-only/filtered/skipped/missing materials get explicit nonpass inventory labels. not-applicable requires actual rule evaluation, never disguises absent integration. Separate inventory states from five rule states; old schema unchanged.

Allow means defined daily scope only; UI/exports show scope/excluded A, never all A+B+IFX compliance. Old passes never merged; counts only current actual passes, no view-edit-discovery increments. Nonrequired excluded items do not invent failure; required missing cases must block, never recategorized to bypass.

| Condition | Raw rule/test state | Complete daily gate |
| --- | --- | --- |
| Updates disabled; original project and required JSON fully executed/passed | Preserve pass and explicitly permitted local not-applicable | allow, no history-proof requirement |
| Rule violation / dedicated assertion failure | violation / Failed | block |
| Invalid JSON/type/target/runner or unenforceable snapshot isolation | invalid-input with diagnostics | block |
| Restore/build/test-process/corrupt results/report save failure | execution-error | block |
| Zero discovery, missing/filtered/skipped/duplicate cases, default zero matches | inconclusive/subset | block, never full pass |
| All disabled/all inapplicable | not-applicable/no effective checks | block |
| Cancellation/disconnection/known run-time input changes | cancelled/failed/inconclusive | block, no old-result replacement |
| Completed valid run later unknown/stale applicability | Original states unchanged, separate historyFreshness | Keep original gate, no new current allowance |

Record native exits unchanged. New entry: 0 complete pass, 2 violation, 3 execution/report error, 4 uncertain coverage/known run-time changes, 5 invalid/unsupported, 130 cancel. Later unknown/stale does not change exits. Legacy exits unchanged. Failed alone does not identify a type violation. Preserve every cause; priority cancel→invalid→execution→inconclusive→violation→pass.

Only explicit local allowEmpty inapplicability accepted. Required skips are not inapplicability; severity does not shrink required scope. Global allowEmpty/all-disabled/filtering never bypasses the gate.

## 9. Existing full evidence

- Existing preview/build/material review/capture/seal/validate/reports/archive contracts remain: [0.8 plan](20261013-archsift-0.8.0-evidence-ui-plan.md), [guide](../external-build-evidence.md).
- Explicit independent entry previews actual config/target/paths, no automatic execution/daily import/install changes.
- Full evidence proves its own materials/rules, not new JSON or all dedicated tests. No initial system sealing every new test's causal materials.
- Separate run IDs/report types/coverage/guarantees. Actual cross-links allowed, never merged green states or one failure overwriting another history.
- Future full test sealing needs separately authorized scope/shared tests/snapshot resolution verification, not implicit initial delivery.

## 10. InProject design comparison

Design choices only, not instructions to modify/delete the old branch.

| Old mechanism | New workflow | Gate |
| --- | --- | --- |
| Validation/selectors/exceptions/stable findings | Reuse semantics | Same supported inputs/findings |
| Shared services/worktree isolation/owned cancellation | Adapt | Parity/no cross-worktree reuse or accidental kills |
| Failure/cancel/latest/history | Simplify | Immutable gates/unknown without binding/no stale fallback |
| Fresh source snapshot/independent business build | Remove from daily | Normal build, no source copying |
| Per-project/csc/reference/generator capture | Full evidence only | No daily capture/seal/material prerequisite |
| BindingVerifier/SourceBound gate | Remove from daily | Separate guarantees, old validator unchanged |
| Worker precheck/material/xUnit playback | Remove | Actual direct tests fail on negatives |
| Envelope/receipt cross-hashes | Remove from daily | Native identities/current details suffice |
| Separate VSTest/tool runner | IFX project/MTP | IFX global.json/old test stack unchanged |
| IFX integration | Whole originals plus JSON | Original/future classes, child updates off, filters subset |
| Equivalent A/Core and IFX double-run | Single responsibility inventory | No duplicated policy or lost assertions |
| Per-rule coverage audit | Simplify | Required execution checked without names/proof chain |
| Full sealing/validation/archive | Existing separate capability | No relabeled daily guarantee |
| Runtime/init/maintenance installer | Not reproduced | Lightweight dependencies, no InProject layout |
| Rotation/pinning/references/capacity | Excluded initially | Directories/states/read-only list, no automatic cleanup |

## 11. Stages and prerequisites

Original plan itself grants no later execution authority. Follow dependencies under subsequent authorization; target operations/human acceptance separately scoped, no inherited InProject authority.

| Stage | Deliverables | Entry | Acceptance | Status |
| --- | --- | --- | --- | --- |
|LT00|Components/support/responsibilities/exclusions/original baseline/schema/identities/exits/isolation/layout/measurement|User scope approval|No InProject/old-schema changes; full-subset/gate-history separate; excluded A visible; no lifecycle system|DESIGN COMPLETE, [static](../acceptance/20261015-lightweight-lt00-contracts.md), product unstarted then|
|LT01|Shared JSON/adapter/thin synthetic net 10-v3-MTP|LT00+authority|No UI/Worker playback/binder; actual positive/negative, stable IDs, zeros/invalid/generic/exceptions|SYNTHETIC COMPLETE, [record](../acceptance/20261015-lightweight-lt01-synthetic.md)|
|LT02|Headless/incremental/native identities/results/coverage/isolation/cancel/logs|LT01|Child 0, originals/JSON identified, discovery not execution, coverage failures/cancel block, unknown history does not, no evidence chain|SYNTHETIC COMPLETE, [record](../acceptance/20261015-lightweight-lt02-synthetic-gate.md); real LT02A pending then|
|LT02A|Authorized external IFX, native before-after, MTP/layout/outputs/isolation/preliminary costs|LT02+specific target/run authority, otherwise wait before LT03|All original classes/cases/Theory/assertions/refs/five-second limit preserved; parent 1-child 0/API mismatch failure/unchanged baseline, no load-induced threshold relaxation|COMPLETE, [record](../acceptance/20261015-lightweight-lt02a-ifx-gate.md)|
|LT03|Separate reports/provenance/inventory/exclusions/actual details/basic history/optional SARIF|LT02A|No fake binding/counts/lines; gate-history separate, legacy A separate, no unexecuted pass/stale fallback/lifecycle system|COMPLETE, [record](../acceptance/20261015-lightweight-lt03-reports-history.md)|
|LT04|Reused editor/three sources/native inventory/excluded A/full-subset/run-cancel/history/download/full entry|LT03|CLI parity, JSON editable/C# not, no discovery-as-pass/filter-full/unknown-triggered evidence|UI/MACHINE COMPLETE, independent review LT06|
|LT05|Authorized CLI-Web-CI parity/original retention/provenance negatives/JSON negatives/cold-warm-disk|LT04+confirm target authorization covers operations|No coverage loss/source confusion/persistent drift/adoption, original install untouched|MACHINE COMPLETE, [record](../acceptance/20261015-lightweight-lt05-ifx-regression-measurement.md)|
|LT06|Fixed candidate/guides/limits/human checklist/dependencies/distribution/optional release gates|LT05/user human review|Machine+independent human+legacy regression, InProject unchanged, PR-merge-release separately authorized|COMPLETE/LOCAL CANDIDATE ACCEPTED WITH LIMITATIONS, [machine](../acceptance/20261016-lightweight-lt06-ui-revision.md)/[human](../acceptance/20261016-lightweight-lt06-human-ui-review.md)|

Each record gives actual commit/target/SDK-runner-TFM-config/rules/results/limits. LT01 direct positives/negatives prove adapter; old 0.8/IP08 counts do not.

## 12. Acceptance matrix

| ID | Scenario | Criterion / limit | Stage |
| --- | --- | --- | --- |
|LA01|Branch/legacy|Verified main, unchanged InProject/history, no old runtime merge|LT00/06|
|LA02|Actual type positives/negatives|Single/multiple JSON call ArchUnitNET, pass/accurate failing types|LT01|
|LA03|Generics/fields/signatures/B02-03-05|Engine-recognized dependencies, actual fixtures not policy passes|LT01/02 A/05|
|LA04|Invalid/duplicate/exception/zero/disabled|Explicit rejection, exception identity/reason, no empty allowance|LT01/02|
|LA05|Assembly/selector scope|Production-test-external explicit, missing required assemblies block, wrong namespaces not covered, IFX ownership limits|LT01/02 A/05|
|LA06|Original retention|All classes/future classes/cases/Theory/assertions/refs/five-second limit, no equal-count replacement or excluded behavior tests|LT00/02 A/05|
|LA07|Runner/layout/cache|Native MTP, correct source-Catalog-permissions-snapshots, incremental outputs, added load preserves threshold|LT01/02 A/05|
|LA08|Full/subset|Original whole+JSON, every filter subset, missing-skip-duplicate no pass, config cannot bypass baseline|LT02/02 A/03|
|LA09|Failure/cancel/disconnect/concurrency|Actual current terminal/owned exit/other worktrees safe/old success history only|LT02/04|
|LA10|UI/parity|Validated saves/conflicts/frozen rules/same input-result/headless CI|LT04/05|
|LA11|Gate/freshness|Allow+unknown possible, later edits stale only, run changes block, no unknown-triggered proof/build|LT02/03/04|
|LA12|Paths/host/retention|Authorized writes/no persisted drift/no auto cleanup/legacy materials unchanged|All|
|LA13|Full separation|No daily snapshot-capture-seal-binder-receipt, old full semantics, no relabeling|LT02/02 A/04/06|
|LA14|Legacy compatibility|Shared changes preserve schemas/reports/exits/rules/evidence|Affected stages/LT06|
|LA15|Measurements/human|LT02A integration first, LT05 matched stage-cost-disk-peak, user observations|LT02A/05/06|
|LA16|Snapshot isolation|Parent 1 unchanged/child 0/mismatch fails/match passes/bytes unchanged/reject unenforceable/shared entry logic|LT02/02 A/04/05|
|LA17|Basic history|Directories/states/list, no rotation-pinning-reference graph-recovery-replay, unavailable/interrupted not success|LT03/04/06|
|LA18|Responsibilities|Verified equivalent/overlap/excluded; A increments visible, old JSON retained, B not auto-approved, no union claim|LT00/02 A/03/05|
|LA19|Unified inventory|Three sources/native originals-Theory/results/JSON editable; view-edit-discovery-old pass-excluded-skip not current passes; required missing blocks, no fake C# subresults|LT02/03/04/05|

LT00 freezes measurement definitions; LT02A compares real native/new IFX and preliminary costs before reports/UI, never substitutes synthetics. LT05 >=3 matched cold/warm runs for native/headless/UI. InProject history is nonequivalent reference, no unauthorized old execution. Explain rebuild/cache/rule differences; no promised speedup/old budget borrowing. If no material improvement, record causes/trim scope without inventing lifecycle infrastructure.

## 13. Risks and closure

1. Binding coupling: extract pure computation, old/new regressions, no global provenance weakening.
2. TRX/failure text insufficient for detailed findings: preserve originals/add small structured adapters, no rewritten IFX suite.
3. Required inventory drift: frozen JSON expected set/original baseline; no configurable exclusions/equal-count offsets, filters subset.
4. Dual policy paths: explicitly registered formal inputs, distinguish drafts/exports/changes/effective bytes.
5. Load/runner/heavy dependencies: minimal LT01, actual LT02A before UI, no whole Web/CLI/installer dependency or wholesale xUnit upgrade.
6. Full-evidence misunderstanding: old guarantees only, no IFX-all-tests claim; extensions separately authorized.
7. Distribution/lifecycle: existing workflow/light dependencies, no full Setup clone; LT00 identity/maintenance, delivery not automatic publication.
8. Snapshot update mode: child-only forced off, original tests/parent unchanged; updates separate development.
9. Freshness expansion: gate does not require proof for unknown history; basic storage only, no restored rotation/reference graphs/replay.
10. Provenance/coverage confusion: descriptive inventory not executable policy; discovery not execution, no old A gap filling, clear UI scope/no C# inference/merged assertions.

Original planning round delivered this plan/master index only, no product/schema/config/testproject/branch/dotnet/IFX tests/install/evidence. Subsequent branch/first commit/LT00 and implementation follow execution updates/acceptance/actual authority.
