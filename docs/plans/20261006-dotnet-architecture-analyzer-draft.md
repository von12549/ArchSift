# ArchSift .NET architecture analyzer draft plan

Date: 2026-10-06 (Australia/Sydney)

Status: DRAFT ARCHIVED — User adopted all Q01–Q14 defaults on 2026-10-06; the formal plan was created.

Final name: ArchSift. Discussion-stage proposals/candidates below remain historical. The [formal implementation plan](20261006-archsift-implementation-plan.md) governs execution.

## Purpose and positioning

Build an independent local .NET analyzer. Users choose targets and optional rules; the tool reads projects, source and necessary compiled artifacts, discovers dependencies, checks rules and produces traceable reports. CLI, configuration and UI share one analysis core.

Analysis works without rules; verification adds rulesets; change verification compares two code states. Natural-language plan impact and database-specific analysis are deferred. CI may verify/publish reports without blocking on violations by default.

This draft discusses product/engineering boundaries and delivers documents/questions only. Formalization creates a separate implementation plan. Repository creation, migration and implementation are outside this document task.

## Naming proposals

Recommend ArchSift, combining Architecture and Sift to describe screening architecture/dependencies. The descriptive label means .NET architecture and dependency analyzer; no separate Chinese brand is required.

Proposed identity: product ArchSift, repository archsift, command archsift, namespace ArchSift. These candidates do not authorize directory creation, package registration or publication.

Alternatives: IFX ArchSift (organization prefix) and ArchSift.NET (initial platform). Keep generations out of the name and use independent semantic versions.

Public search on 2026-10-06 found no clear ArchSift product in returned results, not proof of uniqueness/package availability. Check repositories, NuGet IDs and command conflicts before release. StrataLens already names a software product and is not recommended: [existing product](https://stratalens.co.uk/).

## Agreed decisions

User accepted these discussion decisions; remaining questions refine implementation boundaries.

| ID | Decision |
| --- | --- |
| D01 | Independent project selectively reuses Guard analysis, tests and lessons. |
| D02 | Local analysis, verification and reports; initial targets limited to .NET. |
| D03 | Users compose rule instances into rulesets. JSON executes; generated Markdown is read-only. |
| D04 | Supported rule types/parameters only; arbitrary scripts/plugins deferred. |
| D05 | Rule-free analyzer produces graph/facts/optional rule drafts. |
| D06 | Verifier takes target, rules, code and necessary compiled outputs. |
| D07 | Distinguish uncommitted changes from commit ranges; compare states, prioritizing correctness over incremental speed. |
| D08 | ArchUnitNET supplies core assembly checks; project readers, Roslyn and CLI supplement gaps. |
| D09 | Pass, violation, inconclusive, not-applicable and execution errors remain distinct; incomplete coverage never passes. |
| D10 | CLI/config/UI share analysis services and contracts. |
| D11 | Natural-language plan semantics and LLM/Agent impact inference deferred. |
| D12 | Database-specific analysis deferred; ordinary data-access C# still gets generic type/package checks. |
| D13 | Consumer CI defaults to reporting, future blocking explicit; tool development retains normal builds/tests. |
| D14 | Read-only targets, explicit outputs, child isolation and no persisted environment changes. |
| D15 | First useful version includes JSON rules, graph, verification, real ArchUnitNET, CLI, simple UI and reports; change verification follows. |

## Features and outputs

| Feature | Input | Output | Stage |
| --- | --- | --- | --- |
| Analysis | Target, scope, optional build context | Graph, declarations, frameworks, assembly mapping, unresolved items, optional drafts | First useful version |
| Verification | Analysis input and rulesets | Rule results, findings, coverage, inconclusive items | First useful version |
| Change verification | Explicit base/target and rules | New/existing/resolved findings and comparison limits | Second stage |
| Plan verification | Future plan format and rules | Proposed-change conflicts | Deferred |
| CI reports | Same CLI and fixed config | Machine/readable report artifacts | After core/change verification stabilizes |

Plan-format validation differs from architecture-impact inference. Even future deterministic proposed-reference checks cannot claim validation of free text or future implementation.

## Proposed architecture

```text
CLI / Local UI
       │
       ▼
Unified analysis service
       ├── Configuration and rule validation
       ├── Input identity and scope
       ├── Project/package model
       ├── Source checks
       ├── Compiled artifacts and ArchUnitNET
       ├── Rule evaluation
       ├── Before/after comparison
       └── Report generation
```

Use .NET libraries for core, CLI entry and separate ArchUnitNET adapter. Retain useful algorithms/examples while removing PowerShell Stage context as an internal protocol. SDK, UI technology and initial platform remain questions below.

Core never depends on Guard onboarding, Profile approvals, promotion, composition, short-lived safety proofs or protected status. Versioned inputs/explicit options govern operation without a new authorization workflow.

Configuration expresses target, rules, build inputs, outputs and options; rules express constraints without machine-specific absolute paths. Freeze and display precedence: proposed explicit CLI/UI inputs override config, then documented defaults.

## Rulesets and templates

Implement finite types with JSON templates. Rulesets carry schemaVersion, stable ID, version, description and rules. Rules have at least ID, type, enabled, scope, parameters, severity and reason.

Scopes use project-relative, assembly, namespace or type selectors. Distinguish exact/glob/regex explicitly, never reinterpret one string implicitly. Templates document valid examples, fields and unsupported cases.

Combined rulesets report order/identity/hashes; duplicate IDs/conflicts default to configuration errors. Generate Markdown from JSON with hash/version; editing Markdown changes no executable policy. Regenerate after UI saves.

Candidate initial types; Q06 fixes the final list.

| Type | Example | Evidence |
| --- | --- | --- |
| project-reference | Domain cannot reference Infrastructure | Declared/evaluated references |
| graph-integrity | References resolve; scoped projects inventoried | Inventory/graph |
| target-framework | Only specified frameworks | Declared/evaluated TFMs |
| nuget-denylist | Forbidden IDs or version ranges | Direct/resolved package graph |
| type-dependency | One namespace cannot depend on another | ArchUnitNET model |
| implementation-location | Interface implementation in allowed assemblies/namespaces | ArchUnitNET and supplements |
| naming | Project/assembly/type/source naming | Project model, ArchUnitNET or Roslyn |

Draft generation separates current facts, proposed rules and human policy choices. Existing dependencies never automatically become allowed policy.

## Scope and input identity

Define graph scope without unconditional completeness promises. List projects, solution entry, external/conditional/unresolved references, TFM/config and exclusions. Label declarations separately from evaluated MSBuild facts.

Source, project, rule, build-argument and assembly hashes identify a run. Machine paths locate files, not portable project identity; relative paths/hashes/optional Git commit-tree support portability.

All phases share selection, distinguishing source/build/generated evidence. IDE caches/ordinary output should not cause irrelevant invalidation. Explicitly include or mark unsupported custom imports, generated source and external build inputs; never ignore all nonsource files.

Verify relevant inputs at start/end and explain run-time changes. Open editors alone do not reject. Exclude reports/temp output from identity. Same core input yields stable sorted findings, without demanding identical times/durations/machine metadata.

## Artifacts and trust

Separate existing artifacts from explicit builds. Never silently restore/build by default; show actual build scope/config/restore/network choices when enabled.

Existing DLLs support reports about those DLLs. Combine with current-source conclusions only when provenance binds source/scope/SDK/TFM/config/rules/assembly hashes. Modification times alone prove no freshness.

Explicit temporary directories and isolated children for builds. MSBuild may execute project logic; directory/environment isolation is not a full sandbox. Q08 freezes modes/cache/network.

Build failures preserve completed project checks; dependent assembly rules become inconclusive with execution errors. Never show complete compliance or equate different TFMs/configurations/build modes.

ArchUnitNET analyzes compiled artifacts. Its guidance favors Debug to reduce optimized-away dependencies, but verify specific rules: [README](https://github.com/TNG/ArchUnitNET/blob/main/README.md), [Debug limitations](https://archunitnet.readthedocs.io/en/latest/limitations/debug_artifacts/).

## Reports and change verification

Reports include toolVersion, reportSchemaVersion, run type, source/rule identities, scope, build/engine context, rule results/findings/coverage/unsupported/errors. Findings have stable ID, rule, subject, relative/type evidence, explanation/context.

Overall compliance requires every enabled applicable rule checked/passed. Separate severity/execution; nonblocking CI never converts violations to passes. Explicitly explain all-not-applicable, zero selectors and partial coverage.

Change verification takes explicit base/target. Initially compare final states, not every intermediate commit. Identify index/worktree/untracked/ranges separately; Q10 fixes defaults.

Use identical rules for new/existing/resolved findings. If baseline fails, report target without inferred newness. Mark rule changes; deletion/narrowing never pretends source repair. Full two-sided analysis first, incremental optimization later.

CI normally generates artifacts despite findings; config/tool/incomplete execution remain distinct. Future fail-on is explicit; integration never silently changes remote protections.

## Guard migration

Reference source `C:\Users\von12\OneDrive\Desktop\Guard`. Re-freeze commit/hashes/licenses at actual migration; this path is not a fixed execution baseline.

| Asset | Proposal | Formal acceptance |
| --- | --- | --- |
| modules/architecture-conformance/adapter.ps1 | Extract reference/framework/source/assembly algorithms | Per-rule positive/negative/missing/real examples |
| modules/architecture-conformance/fixtures | Select fixtures/dependencies | Independent execution without Guard install |
| core/host/V4.Guards.Host/ProfileRuntime.cs | Reuse discovery/framework/path ideas, discard draft approvals | Accurate scope/graph/unsupported facts |
| modules/build-evidence-provider/adapter.ps1 | Learn manifest/hash/copy/isolation, redesign interface | Consistent source/artifact binding without mixed inputs |
| Capability matrix/tests | Coverage/boundary references, rebuild contracts | Actual positive/negative proof, not copied claims |
| Web Companion | Configuration/report/error lessons | Same-input core CLI/UI parity |
| Profile/Stage/trust/promotion/remote activation | Retain in original project, exclude core | No legacy entry prerequisites |

P05 real 8/8 proves prerequisite technical path, not analyzer accuracy/cross-machine parity. Preserve failures/cleanup evidence.

## Proposed sequence

| Stage | Work/deliverables | Completion |
| --- | --- | --- |
| P0 design | Close questions, name/scope/rules/reports/formal plan | All Q decided or bounded deferred |
| P1 engine/rules | Independent structure/schema/templates/Markdown/scope/identity | Valid-invalid inputs; no old lifecycle dependencies |
| P2 analysis | Graph/rules/real ArchUnitNET/artifacts/CLI/reports | Actual clean/violating/missing/IFX results |
| P3 UI | Target/rule editing/validation/run/cancel/report/export | Complete version/parity/no stale success |
| P4 changes | Base-target/two analyses/finding comparison | Cross-project/rule changes/baseline failure/delete-rename limits |
| P5 CI | Optional workflow/artifacts | Nonblocking violations, visible execution failures |

No draft dates/effort promises. Formal Q decisions determine executable tasks/files/checks.

## First-version acceptance draft

1. Rule-free supported-target discovery with projects/references/frameworks/scope/unresolved facts.
2. JSON/UI/CLI/Markdown agree; invalid/duplicate rules never execute.
3. Every initial type has clean/violation/missing-unsupported fixtures; real compiled ArchUnitNET dependency checks.
4. Correct missing/stale DLL, unresolved, zero-match, config and build-failure states; incomplete never passes.
5. Input/rule/engine/build identity and locatable evidence; identical core findings.
6. Open VS Code/Visual Studio adds no irrelevant invalidation; relevant saves detect change.
7. No persistent environment writes; explicit outputs; no eight-step onboarding/manual hash workflow.
8. Independent operator understands violation versus inconclusive; proposed confidence>=4/5/max L2, method Q13.
9. Reviewable real IFX and constructed violations; sequential reviewed human commands/results.
10. Q03/Q13 frozen platform/performance gates, no invented performance pass.

## Closed questions

All Q01–Q14 CLOSED—DECIDED. User adopted defaults 2026-10-06. Original proposals remain traceable; formal plan details final decisions.

| ID | Question | Proposed default | Impact |
| --- | --- | --- | --- |
| Q01 | Product/CLI/repository/namespace | ArchSift/archsift/ArchSift; independent SemVer | Naming/package identity |
| Q02 | Local/remote/create timing | Candidate D:\ArchSift, independent repo after formalization, personal ownership | Permissions/delivery/migration |
| Q03 | SDK/platform | .NET 10/Windows x64 UI; Linux CLI later | Build/package/parity |
| Q04 | Targets | SDK C# sln/slnx/csproj, net 8/9/10; old Framework/F#/VB deferred; explicit per-TFM runs | Testable support |
| Q05 | Graph/MSBuild | Explicit root/solution plus unlisted projects; external uncovered; declarations first with condition limits, evaluation later | Graph promise/build dependency |
| Q06 | Types/selectors | References/integrity/TFM/direct NuGet deny/type/naming; exact/glob; regex/code deferred | Scope/templates |
| Q07 | NuGet | Direct declarations/resolved central versions, case-insensitive IDs; version/transitive later; no auto-network | Restore/version semantics |
| Q08 | Artifacts/build/network | Existing default, assembly-only when unbound; explicit isolated build/offline restore unless configured, Debug/default, record SDK/TFM/args; disposable local cache | Input correctness/cost |
| Q09 | UI | Loopback Web/.NET, JSON forms/read-only Markdown, no accounts/remote service/marketplace | Experience/effort |
| Q10 | Changes | HEAD versus staged/unstaged/selected untracked; explicit base..head; final-state/no fetch/no per-commit | Unambiguous scope |
| Q11 | Exceptions/history/zeros/composition | RuleID/scope/reason; compare history not exemptions; zero inconclusive unless allowEmpty; duplicate/conflict errors | Avoid empty/stale passes |
| Q12 | Formats/language/exits | JSON/HTML first, SARIF later; English machine fields, Chinese-readable UI preferred then; violations 0, errors nonzero, separate partial code, future explicit fail-on | CI/contracts/UX |
| Q13 | Samples/human/budget | IFX/independent fixtures, human field rules, 4/5/max L2; measure then freeze, no guessed budget | Quantified gates |
| Q14 | Distribution/licenses/Guard | Local ZIP first, NuGet tool later; provenance/license checks; Guard new features paused, old UI maintenance separate | Distribution/dual maintenance |

Explicit deferral closes Q only with removed scope/current limits/revisit conditions. MVP cannot defer everything: actual ArchUnitNET, CLI, simple UI, templates/reports required.

## Formalization

After all Q closed (decided/explicit deferred), reconcile scope/stages/acceptance. Freeze dependency versions/provenance/release identity formally.

Adopt name/decisions/contracts; specify repository/tasks/dependencies/checks/paths/delivery/completion/deferred list. Preserve draft; save linked `<date>-<final-name>-implementation-plan.md` separately.

Open Q means revise draft, not relabel formal. Guard old execution-plan schema is no implicit constraint; define machine plans only when needed.

## Decision log

| Date | Questions | Decision | Status |
| --- | --- | --- | --- |
|2026-10-06|D01–D15|User accepted independent lightweight project/prior analysis|Decided|
|2026-10-06|Q01–Q14|All defaults adopted; formal scope/sequence/deferrals recorded|CLOSED—DECIDED|

## References

- [P05 test plan](D:/IFX-10-Root/docs/plans/20261006-guard-ifx-p05-1.2.2-ui-first-human-retest.md).
- [P05 final manifest](D:/IFX-10-Root/guard-lab/archives/p05-20261006/final-manifest.json).
- [P05 safety/cleanup audit](D:/IFX-10-Root/guard-lab/archives/p05-20261006/cleanup/h4-final-safety-and-cleanup-audit.json).
- [Guard analysis adapter](C:/Users/von12/OneDrive/Desktop/Guard/modules/architecture-conformance/adapter.ps1).
- [Guard discovery](C:/Users/von12/OneDrive/Desktop/Guard/core/host/V4.Guards.Host/ProfileRuntime.cs).
- [Guard build evidence](C:/Users/von12/OneDrive/Desktop/Guard/modules/build-evidence-provider/adapter.ps1).
- [Official ArchUnitNET](https://github.com/TNG/ArchUnitNET).
