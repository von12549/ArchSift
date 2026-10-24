## Trusted Publishing activation preparation (2026-10-25)

TA00-TA03 implementation/tests/PR/CI/merge authorized; see [activation plan](20261025-trusted-publishing-activation.md). Real asset transfer and publication remain committed-locked false; repository switch stays closed. No local Guard/dotnet, credentials, NuGet push, tag/Release, installer or host mutation. Original acceptance/reference and IFX policies remain unchanged.

Session PATH baseline checker, 2026-10-24: checker/tooling/tests/PR/CI/merge are authorized. Fix the external cross-session comparison with an immutable original persistent baseline and separately human-confirmed parent/session/PATH record. Product runtime checks and accepted packages remain unchanged. No actual local session candidate or confirmation, Guard/dotnet resumption, environment repair or public publication is authorized. See [implementation scope](20261024-session-path-baseline.md) and [operator guide](../guides/session-host-baseline.md). Earlier checkpoints retain their timing.

Trusted Publishing preparation, 2026-10-24: the user authorized plan/workflow/tests/PR/CI/merge only. NR04 exact final-build-02 human acceptance passed (4/5, no reported defects); its packages remain unchanged at source 8b721f4. New publish-nuget.yml is manual/read-only preflight only, with no OIDC permission, environment job or publishing path. GitHub environment and NuGet policy are user-operated and pending; credential acquisition and NR05–NR08 remain unauthorized. See [scope and test plan](20261024-trusted-publishing-preparation.md) and [operator guide](../guides/trusted-publishing.md). Earlier checkpoints retain their timing.

Formal NuGet release preparation, 2026-10-24: the user authorized NR00–NR04 necessary changes, tests, PR/CI/merge and final asset/preview preparation only. NR05–NR08 public publication and new installers are excluded; final human review remains user-only. Earlier 0.11.0 implementation/adoption passed product PR #26/actual-main CI, IFX PR #29/actual-main CI, full copied migration/rollback and user acceptance (4/5). This preparation corrects current NuGet guidance, builds fresh final packages and requalifies their exact identities without changing live IFX or host policy. Prior checkpoints below retain their historical timing. See [preparation scope](20261024-nuget-0110-preparation.md).

Unified workspace execution, 2026-10-24: revision-2 implementation, tests, external migration/rollback, PR/CI/merge and fixes are authorized. UP00 backup/restoration verification passed. The user explicitly authorized adding only stable 0.11.0 to the Setup known-version allowlist; regexes and schemas are unchanged. Complete local regression passed: 2 architecture, 157 unit, 154 integration including 26 workspace cases, 57 native MTP, and eight rule-template validation/rendering checks, with equal host hashes. Draft PR #26 receives this fix and requires fresh normal CI. Exact final packages, IFX full migration/rollback/cutover and independent human acceptance remain pending. No public publication. See docs/acceptance/20261024-unified-workspace-development.md; evidence D:/ArchSift-lab/runs/archsift-unified-init-20261024-01.

0.10.1 completion, 2026-10-23: [published and download-verified](../releases/0.10.1-publication.md) at source `66cf7c4d4ca2a4a0b492fad3f9edb87c1e4e3b4c`. User accepted all three entrances, usability 4/5, no defects or untested mandatory items; three exit-zero/host-equal closeouts verified. Eleven public attachments, both 752-file tagged source archives, 28-package feed, cold NuGet Web/portable use, four-case package consumption and downloaded Setup smoke passed. PX00-PX09 and PA01-PA12 are complete for GitHub Releases. All old releases/configurations/evidence and live IFX inputs remain retained; no existing IFX entrance upgrade or policy change. Earlier pending and failed-attempt entries retain their historical timing.

0.10.1 human-review preparation correction, 2026-10-23: the user reported loading the Workbench startup JSON into the daily-test inventory input. Added the separate schema-2 `review-ifx-test-config.json` for the human IFX copy and corrected both paths and managed-profile selection in the checklist. Exact official stable Web loading returned 92 C# / 13 rulesets / 401 JSON cases, Safe shutdown exited zero, and host hashes remained equal. [Correction evidence](../acceptance/20261023-workbench-0101-human-config-correction.md). Product bytes and original evidence are unchanged; all human answers and publication remain pending.

# ArchSift .NET architecture and dependency analyzer implementation plan
0.10.1 exact stable machine qualification, 2026-10-23: source `66cf7c4d4ca2a4a0b492fad3f9edb87c1e4e3b4c` passed PR #23 and actual-main Windows/Ubuntu typed CI, independently downloaded/hashed official Windows no-SDK archive (2,965 files), stable package/Web negatives, browser component, frozen native/SARIF and HTTP checks, fresh advisory/legal review, copied IFX 493-case CLI/Web parity with separate 105-project official audit, and complete managed/NuGet rollback. [Machine qualification](../acceptance/20261023-workbench-0101-stable-machine-qualification.md) binds 31 external evidence files. PX07 and machine PA01–PA10 are complete; [unanswered user-only checklist](../acceptance/20261023-workbench-0101-stable-human-checklist.md) is PX08/PA11. No tag/release/publication or live IFX upgrade has occurred. PX09/PA12 waits for explicit human acceptance. Earlier candidate and failed-harness entries retain their historical outcomes.

0.10.1 stable source cut, 2026-10-23: RC2 PR #22 merged as `71539f2c`; actual-main [Windows/Ubuntu CI](https://github.com/von12549/ArchSift/actions/runs/39925606616) passed with exact typed identities (2 architecture, 157 unit, 123 integration, 57 MTP per platform) and independently verified official archive bytes. The `v0.10.1` tag/release remained absent at the cut recheck. A separate stable source/lock cut follows; fresh stable packages, machine gates and user-only visual acceptance remain pending. Publication follows human acceptance. Existing IFX entrances, policy, older versions and evidence remain unchanged. Earlier entries retain their historical timing.


0.10.1 RC2 machine qualification, 2026-10-23: source `c2de2d25` passed clean offline packages with embedded NuGet Web, Windows/Ubuntu typed CI, independent Windows/no-SDK, copied IFX 493-case CLI/Web parity, separate official 105-project audit, negative/lifecycle/security/license and old/new rollback gates. The exact external evidence and retained harness stops are bound in [RC2 machine qualification](../acceptance/20261023-workbench-0101-rc2-machine-qualification.md). PR/normal merge, actual-main CI, exact stable build/requalification and user-only visual acceptance remain pending; publication follows human acceptance. Live IFX entrances, policy and persistent host settings remain unchanged. Earlier entries retain their historical timing.

0.10.1 RC2 development checkpoint, 2026-10-23: offline 2 architecture / 157 unit / 123 integration / 57 MTP cases and eight templates passed with equal host hashes. RC1 package/cold probes remain retained; RC2 strengthens immutable host/cache boundaries and Git child handling. Read-only IFX preflight passed at 30cca604; copied integration, exact committed RC2/stable artifacts, official CI and user acceptance remain pending. [Evidence](../acceptance/20261023-workbench-0101-rc2-development.md).

0.10.1 execution authorized, 2026-10-23: NX01/NX02 implementation, tests, copied rollback rehearsals, PR/CI/merge and fixes are authorized on codex/workbench-0.10.1. PX00 input freeze completed against main 8f84254; PX01 is active. Final human acceptance remains user-only and precedes publication. Existing IFX entrances, policy, older versions/configuration/evidence and persistent host settings remain protected. See [execution plan](20261023-archsift-0.10.1-plan.md).

Next patch planning, 2026-10-23: [0.10.1 implementation/release plan](20261023-archsift-0.10.1-plan.md) covers NX01/NX02, unified 0.10.x versioning, embedded NuGet Web plus explicit portable Launcher bootstrap, JSON disclosures, compatibility, rollback and PA01–PA12. 0.10.1 is a candidate requiring pre-cut/publication rechecks; GitHub Packages availability is inconclusive. PX00–PX09 are planned, not started. Only local planning documents changed; code, version properties, locks, IFX, Git state and published 0.10.0 are unchanged. Implementation, commit/push/PR/merge and publication require subsequent authority. Earlier entries retain their historical timing.

IFX stable upgrade fully accepted, 2026-10-23: the user completed Launcher/owned-child and separate `workbench.ps1` reviews, confirmed expected behavior, usability 4/5, no defects and no untested items. Both post-handoff closeouts independently show exit 0, 0.10.0/runtime 10.0.13 and equal within-session host hashes; no installed CLI/Web remains. [Final acceptance](../acceptance/20261023-ifx-stable-upgrade.md) closes UG00–UG07. Earlier machine/pending checkpoints remain historical; old versions/configuration/evidence and all policy boundaries are retained. [Next iteration NX01/NX02](20261023-workbench-next-improvements.md) records NuGet Web payload delivery and collapsible JSON inventory, with no implementation started or target version assigned.

IFX dual-entry stable upgrade machine-qualified, 2026-10-23: both daily tooling and managed Launcher/Workbench now use released 0.10.0/current IFX main `30cca604`, SDK 10.0.303 and isolated Core/ASP.NET/host 10.0.13. IFX PR #27 and actual-main CI passed; 493/493 typed CLI/Web parity, fresh 89-assembly CLI qualification, separate official 105-project/CLI audit, daily rollback/forward and copied Setup/outer-profile rollback rehearsals passed. Both live outer sessions were safely closed; old versions/configuration/feed/evidence, host hashes, policy boundaries and protections remain preserved. Existing runner 22 is online/idle. [Machine acceptance](../acceptance/20261023-ifx-stable-upgrade.md) and [execution plan](20261023-ifx-stable-upgrade-plan.md) record UG00–UG06 complete and UG07 handoff prepared. Final independent human acceptance remains pending with an unanswered checklist. External helper fixes do not modify published binaries; earlier release exclusions retain their historical timing.

Published and download-verified, 2026-10-23: ArchSift Workbench 0.10.0 at accepted source `f082894` completed WB00-WB10 and G01-G15 for the GitHub Release scope. The user accepted the exact stable payload (4/5, no defects or untested items); both Safe shutdown records passed. All 11 public attachments, both tagged source archives, fresh offline consumption and downloaded Setup installation/smoke passed. See [publication and final acceptance](../releases/0.10.0-publication.md). Frozen assets, original IFX and protected evidence remain unchanged. Registry publication, live IFX upgrade and deferred policies remain excluded. Earlier entries retain their historical timing.

Release execution update, 2026-10-23: verified PR #17 and actual-main 64897e3 passed Windows/Ubuntu CI; exact 0.10.0-rc.3 assets passed independent Windows/no-SDK, fresh advisory/license, generic/negative/native/HTTP, 493-case CLI/Web IFX parity with 89 fresh assemblies, separate 105-project official audit and 20 lifecycle checks. See [RC3 machine qualification](../acceptance/20261023-workbench-rc3-machine-qualification.md). RC1 and RC2 rejections and all checker stops remain preserved. WB08 is active for the unified stable 0.10.0 source/lock cut. SDK 10.0.303/runtime 10.0.13 stay fixed; new stable CI, fresh assets, byte-bound gates and user-only final UI acceptance are required. Publication remains user-authorized; live IFX, system tools, registry scope and deferred policy remain unchanged. Earlier entries retain their historical timing and outcomes.

Archive update, 2026-10-22: 1,289 raw worktree materials were independently ZIP-archived and verified, with originals retained at that stage. Active/previous combinations and the 580-item union of the 548/568 protection inventories remained protected. See [retention](../distribution/20261022-materials-retention.md) and [JSON](../distribution/20261022-materials-retention.json). This does not claim a clean repository, complete classification or reclaimed space. CL00–CL05, source/document delivery and IR01–IR05 remain closed; final checks and normal CI are recorded separately.

Route retirement, 2026-10-22: packaged project integration formally supersedes separate ArchSift.InProject 0.1.0. Stop future development, human acceptance and publication of that route. Preserve IP08 as machine complete, human partial, paused, unaccepted and unreleased. The fixed 2877974 history was independently archived and restored: 191 commits, 2,929 objects and 12 key blobs; 80 external originals were preserved. Retain old local/remote branches, A/B worktrees, active/previous feeds, policies and shared capabilities at this decision point. See [decision](../decisions/20261022-inproject-retirement.md) and [JSON](../decisions/20261022-inproject-retirement.json). Physical handling follows exact external receipts; retirement requires neither empty roots nor deleted branches and never retroactively passes human acceptance.


Published, 2026-10-16: [PR #8](https://github.com/von12549/ArchSift/pull/8) merged as 0c2d9f1. Windows/Ubuntu main CI passed; [v0.9.0-dev](https://github.com/von12549/ArchSift/releases/tag/v0.9.0-dev) points directly to that commit. The public source ZIP was anonymously downloaded and content-verified. See [publication](../releases/0.9.0-dev-publication.md). Status: LT00–LT06 ACCEPTED WITH LIMITATIONS / SOURCE PRERELEASE PUBLISHED / IFX FORMAL POLICY PENDING. Earlier statuses remain historical.

Prerelease regression, 2026-10-16: [PR #8](https://github.com/von12549/ArchSift/pull/8), d112340, passed Windows/Ubuntu CI. The recursive-watcher new-directory fix passed 18 native MTP and 248 legacy tests. A fresh packaged external IFX full gate passed 80+1, allow/0. All 30 static Web hashes matched the user-reviewed payload. See [regression](../acceptance/20261016-lightweight-lt06-release-regression.md). At that point: SOURCE PR READY / UNMERGED / UNRELEASED; formal IFX policy remained later work.

Authorization, 2026-10-16: user approved PR, merge and publication as 0.9.0-dev, with formal IFX policy after publication. Public delivery is tagged source archives only; local path-bound payloads are not generic installers. Record actual PR/CI/merge/release evidence before claiming success. See [plan](20261015-archsift-lightweight-test-workbench-plan.md) and [notes](../releases/0.9.0-dev-notes.md).

LT06 complete, 2026-10-16: the [revised fixed payload](../acceptance/20261016-lightweight-lt06-ui-revision.md) passed a full Web IFX gate, 80+1 allow/0. The [independent user](../acceptance/20261016-lightweight-lt06-human-ui-review.md) confirmed UID search, languages, global Results/History, existing analysis access, narrow layout and keyboard use. At that point: LOCAL CANDIDATE ACCEPTED WITH LIMITATIONS / NOT RELEASED. PR, merge, publication, installation and formal IFX adoption required their own decisions.

Earlier LT06 candidate: [record](../acceptance/20261016-lightweight-lt06-candidate.md) fixes 0.9.0-dev source e0b3bdd and external CLI/Web per-file hashes. Offline locked restore, 265 old/new tests, packaged real IFX CLI allow/0, Web loopback smoke and host protection passed. Guides and independent checklist were ready. At that point: MACHINE READY / HUMAN PENDING / NOT ACCEPTED / NOT RELEASED; user answers and publication authority remained separate.

LT05, 2026-10-15: [acceptance](../acceptance/20261015-lightweight-lt05-ifx-regression-measurement.md) records native/CLI/Web full-project tests, three cold and three warm runs per entry on one external IFX worktree, local headless CI parity, original-test retention, excluded A/B items, real API and JSON block/2 negatives, schema checks and legacy regression. At that point: MACHINE COMPLETE / LT06 NOT STARTED / NOT ACCEPTED / NOT RELEASED. Final independent UI remained user-owned.

LT04, 2026-10-15: [acceptance](../acceptance/20261015-lightweight-lt04-web-workbench.md) covers editor reuse, three source groups, full/subset/cancel, history/downloads and full-evidence access. External IFX Web passed 80+1 allow/0; actual JSON violation block/2, subset block/4 and cancellation block/130. Matched CLI full execution also allowed with identical cases. At that point: UI/MACHINE COMPLETE / LT05 NOT STARTED / NOT ACCEPTED / NOT RELEASED; measurements and human review remained.

LT03, 2026-10-15: [acceptance](../acceptance/20261015-lightweight-lt03-reports-history.md) delivers separate architecture-test-run JSON/HTML, provenance/exclusions, actual JSON findings, original IFX assertions, state markers and read-only history. Real positives/negatives, synthetic save/history failures and 248 legacy tests passed. At that point: REPORT/HISTORY COMPLETE / LT04 NOT STARTED / NOT ACCEPTED / NOT RELEASED.

LT02A, 2026-10-15: [acceptance](../acceptance/20261015-lightweight-lt02a-ifx-gate.md) established 80 native execution IDs on an authorized copy. Integration retained all 80 and added one JSON case; the full gate passed. API/JSON negatives blocked correctly; original sources, snapshots, checkout and host remained intact. At that point: REAL IFX COMPLETE / LT03 NOT STARTED / NOT ACCEPTED / NOT RELEASED. Formal B policy, reports, UI and independent review remained later stages.

LT02, 2026-10-15: [synthetic acceptance](../acceptance/20261015-lightweight-lt02-synthetic-gate.md), code 3ddd0dc, passed headless offline restore/incremental build/MTP case and rule identity checks, child snapshot isolation, cancellation/logging/source-change detection and blocking scenarios; 248 legacy tests passed. At that point: SYNTHETIC COMPLETE / LT02A NOT STARTED / NOT ACCEPTED / NOT RELEASED. Specific external IFX authority existed, but LT02A had to pass before reports/UI.

LT01, 2026-10-15: user authorized later development, acceptance, stage commits/pushes and specific reviewed offline LT02A/LT05 operations on external IFX copies. Originals stayed read-only; final UI remained user-owned. [Synthetic acceptance](../acceptance/20261015-lightweight-lt01-synthetic.md), code 082d0ec, covers shared validated JSON/xUnit v3/MTP, actual ArchUnitNET positive/negative cases, seven MTP and 248 legacy passes. At that point: LT01 COMPLETE / LT02 NOT STARTED / NOT ACCEPTED / NOT RELEASED; real integration awaited LT02A.

LT00, 2026-10-15: user authorized codex/archsift-lightweight-workbench from main acbcff1 and plan-first d1b089d push, then LT00. [Contracts](../decisions/20261015-lightweight-test-workbench-lt00-contracts.md), [static acceptance](../acceptance/20261015-lightweight-lt00-contracts.md), source baseline, responsibilities and report schema completed; subsequent LT00 commit/push was separately authorized. At that point: DESIGN COMPLETE / LT01 NOT STARTED / NOT ACCEPTED / NOT RELEASED. The 80 cases were statically derived, not runtime measurements; later packages/real runs/PR/merge/release were not yet authorized.

Documentation-only responsibility/UI supplement: the [plan](20261015-archsift-lightweight-test-workbench-plan.md) defines full original IFX plus reviewed B tests. Proven equivalent A responsibilities stay in C#; unmigrated TFM, solution and package increments stay with original ArchSift and are explicitly excluded. No combined all-policy compliance claim. UI distinguishes source, executor, inventory, execution and exclusions; viewing/editing/discovery/old passes never count as current passes. C# semantics remain outside UI editing. LA01–LA19, LT02A-before-UI and basic history/no daily evidence chain remain. Implementation was unauthorized/unstarted, with plans/index only.

Documentation-only review: the [plan](20261015-archsift-lightweight-test-workbench-plan.md) preserves the entire original project, classes, Theory data and assertions; filters are subset. Force child IFX_UPDATE_SNAPSHOTS=0, keep completed runGate separate from history unknown/stale, add early real LT02A before reports/UI, and limit history to directories/basic states/read-only lists. Rotation, pinning and archive recovery are deferred. LA01–LA17/dependencies updated; no code, InProject, target, build/test or Git changes at that stage.

New direction, 2026-10-15: existing changes were saved/pushed as 2877974 on codex/archsift-inproject-0.1.0, then returned to clean remote-matching main acbcff1 for the [new plan](20261015-archsift-lightweight-test-workbench-plan.md). Preserve dedicated C#, direct JSON/ArchUnitNET tests, UI editing/start/report, normal daily builds/tests and separate existing full evidence. InProject code/spec/branch and IP08 paused/unaccepted/unreleased history stayed intact and unmerged. This round wrote plans only; no new code, builds, target tests or plan commit/push yet.

Published, 2026-10-13: [PR #6](https://github.com/von12549/ArchSift/pull/6) merged and [v0.8.0](https://github.com/von12549/ArchSift/releases/tag/v0.8.0) targets fixed source ab3d2ea. Final PR/main Windows and Ubuntu CI, four anonymous downloads, digest/size checks, the ZIP manifest and original-source/host/process audit passed. See [publication](../releases/0.8.0-publication.md). ACCEPTED WITH LIMITATIONS / PUBLISHED / VERIFIED; earlier pending states remain historical.

Final 0.8.0 acceptance: ab3d2ea passed 248 tests, 17 ZIP checks, 20 Setup checks, both CI platforms and read-only real IFX checks with B406/A182. The user reported expected behavior, 4/5 usability, L0 and no untested items; shutdown froze the page and returned the CLI prompt. Post-human audit passed, although session exit/port evidence was not independently captured. See [final acceptance](../acceptance/20261013-0.8-final-acceptance.md). Stage 0.8-5 was complete, accepted with limitations and authorized for release, but not yet published at that point.

Earlier 0.8.0 machine candidate c10ceb9 passed 17 ZIP checks, 13 Setup checks, 245 tests, eight templates, both CI platforms and 10 browser groups. The delegated single IFX build covered 105 projects; review, sealed v3 binding, B406/A182, schema, preflight and host checks passed. See [record](../acceptance/20261013-0.8-5-machine-qualification.md). S4 removal/uninstall was not run and no command was issued. Human review was unstarted; the stage remained active, unaccepted and unreleased.

0.8.0 stage 3 machine work completed and stage 4 became active: actual scope/review rationale, paginated preview, create-new v3 sealing, validation and configuration-library copies passed, alongside 244 tests, eight templates, nine browser groups, eight screenshots and host checks. See [record](../acceptance/20261013-0.8-3-review-seal.md). Rejections for a missing TFM or empty SDK were retained. Synthetic sealing did not count as real IFX or human acceptance; a new package was still pending.

0.8.0 stage 2 machine work completed and stage 3 became active: shared persistent jobs, preview/execution review, snapshots, offline capture/restore, project build/evaluation, Csc audit and cancellation of owned work passed. Validation covered 244 tests, eight templates, eight browser groups, seven screenshots and unchanged source/host. See [record](../acceptance/20261013-0.8-2-evidence-jobs.md). Evidence was still unapproved and required review; there was no sealed manifest or current-source compliance claim. Final packaging, real integration, human review and release remained pending.

0.8.0 stage 1 machine work completed, human review remained pending and stage 2 became active. Validation passed 240 Release tests, eight templates, seven browser groups, six screenshots, eight checkpoints and final host checks. The historical A182/B406 projection covered 105 projects. History restart, pagination, roots, time, downloads and failure clearing were checked. See [1E](../acceptance/20261013-0.8-1e-workbench.md). Two navigation failures were retained. These checks were not independent UI acceptance or current IFX proof. The full plan was authorized; human acceptance of the formal package remained pending.

0.8.0 stage 1D completed and 1E became active: read-only evidence details, storage preview, shared validation, chunked hashing/cancellation and actual three-context positive/stale-negative cases passed, with 239 tests, eight templates and host checks. See [1D](../acceptance/20261013-0.8-1d-evidence.md). Browser work began; human review and later release gates remained open.

0.8.0 stage 1C completed and 1D became active: persistent UI/CLI History in 0.8.0-dev, pagination, registration, live identity, unknown time for old schemas, time zones, duration and terminal failure records passed, with 233 tests, eight templates and host checks. See [1C](../acceptance/20261013-0.8-1c-history.md). Browser checks, human review and evidence orchestration remained pending.

0.8.0 stage 1B completed and 1C became active: Core, CLI, Web and HTML shared the aggregate model, five result states, findings and entry states. The 105-project count was unique and partial exit behavior was retained. Validation passed 229 Release tests, eight templates and host checks. See [1B](../acceptance/20261013-0.8-1b-aggregate.md). History, evidence and UI work remained pending.

0.8.0 stage 1A completed and 1B became active. Plan-first commit d07fba9 was pushed to codex/archsift-0.8.0. History/time, aggregation, storage and validation DTOs/bounds followed the [contracts](../decisions/20261013-0.8-workbench-contracts.md). Offline Release validation used SDK 10.0.303/net10, with zero warnings/errors, 226 tests, eight templates and equal host state. See [1A](../acceptance/20261013-0.8-1a-contracts.md). Overall work remained active, unaccepted and unreleased; UI/human review was pending.

0.8.0 authority covered a plan-first codex/archsift-0.8.0 branch, the full [implementation](20261013-archsift-0.8.0-evidence-ui-plan.md), stage commits/pushes, PR, merge and release; the user retained human review. Machine and safety gates still applied. At the initial authorization point, implementation was unstarted, unaccepted and unreleased. Earlier unauthorized states remain historical. Original-installation upgrades, policy adoption and unrelated Guard work were excluded.

0.8.0 storage planning selected explicit external data at ArchSift\evidence\<projectId>\<runId>\assembly-manifest-v3.json or a custom root, with absolute preview paths outside version directories. Stages 1A–2 covered identity, create-new behavior, indexing and paths. Default retention included successful, failed and cancelled evidence plus metadata. Cleanup required explicit selection, ownership and reference/lease/path checks with a preview. See S1–S5 in the [plan](20261013-archsift-0.8.0-evidence-ui-plan.md). Implementation was unstarted; no directories, migration or cleanup had been performed.

0.8.0 handoff completed before product implementation: main was clean at `43a7b1e62699bc6ca64ed512763eb93a74746ace`; AGENTS, 0.7.0 records, source hashes, the [plan](20261013-archsift-0.8.0-evidence-ui-plan.md) and absolute evidence links were checked. The latest [IFX final result](D:/IFX-10-Root/validation/20261013-ifx-v3-7e9c2a1b/ui-closeout-20261013-02/final-result.json) recorded user A/B review, shutdown and partial exit 4. Earlier not-run states remained historical. See [handoff limits](../acceptance/20261013-0.8-plan-handoff.md). This was planning only; 0.7.0 implementation, real-run, adoption or Git authority did not carry forward automatically.

0.8.0 dependencies were 1A contracts, then 1B aggregation / 1C History and time / 1D details and explicit validation, then 1E parity and machine/human entry checks. Implementation was unstarted; F1-01–F1-10 belonged to the new plan. Later work covered persistent jobs, input capture, identical snapshots, offline build/evaluation, review of unknowns, sealing/validation/copying, recovery/cancellation, owned leases and final real/human acceptance. Matching authority, frozen Q01–Q14/W00–W10 and deferred scope remained required. 0.7.0 was accepted, published and verified.

0.7.0 final, 2026-10-12: direct user authorization, latest PR/main CI with 222 tests each, PR #5 merge, v0.7.0 publication, four downloads, 17 ZIP checks, 13 Setup checks, performance budgets, SARIF and final audit all completed. See [publication](../releases/0.7.0-publication.md). Independent Workbench/Launcher usability was 5/5. The original 0.6.0 installation was unchanged and B policies were unadopted. Earlier pending states and failures remain historical.

0.7.0 readiness: human usability was 5/5 and post-human audit passed. Final a2c9f27 CI passed 222 tests per platform; four draft digests/downloads, 17 ZIP checks, 13 Setup checks and a new audit passed. See [readiness](../releases/0.7.0-readiness.md). PR #5 was ready but unmerged and the release remained a draft. Automatic approval review had rejected merging pending trusted user authorization; no bypass occurred. Technical work was complete, publication pending, and the original installation/B policies unchanged.

0.7.0 final acceptance: ff9a7d1 machine checks, independent Workbench/Launcher review at 5/5, session exit with zero owned processes and full original/host audit passed. See [final](../acceptance/20261012-0.7-final-acceptance.md). Stages 0–4 and P0–P3 completed with acceptance limitations. Release was authorized, followed by authorization to merge, publish and verify downloads. The original installation was not upgraded and B policies were not adopted.

0.7.0 ff9a7d1 included filename validation, error classification and parent-lease fixes. Validation passed 222 local tests and both CI platforms, 17 ZIP checks, 13 Setup checks, performance budgets/SARIF, 105 bound contexts with B406/A182, seven browser groups/six screenshots, four Launcher groups/10 screenshots and lifecycle checks. Audit matched 653 new payloads, 647 old payloads, 14 history files, 782 tracked and 783 captured inputs, with equal host state and no owned process remnants. See [candidate](../acceptance/20261012-0.7-candidate-readiness.md). Two lock failures and one shutdown timeout were retained. Other earlier human checks were good; new-fix/Launcher review and ratings remained pending at this draft stage.

0.7.0 intermediate 59cde17 passed localized blank-name checks, 222 CI tests, 17 ZIP checks and 13 Setup checks. Two real B browser cleanup-handle failures remained inconclusive. The parent-owned lease fix after Worker exit passed actual success/failure/cancellation/neighbor-process cases, 222 tests, eight templates and host checks. See [cleanup](../acceptance/20261012-0.7-worker-cleanup.md). Refreezing, complete rechecks and human review of the fix/Launcher with a rating remained; old failures were retained.

0.7.0 [human review of bc936cb](../acceptance/20261012-0.7-human-workbench-bc936cb.md) confirmed copying, B rules, sorting/history, languages and layout. A blank filename caused a false session-error message and required a fix. Safe shutdown and original/host equality passed. Localized presubmit validation, HTTP error classification and frontend tests were added. The replacement was active; Launcher review/rating, merge and publication remained pending.

0.7.0 human review started on bc936cb in fresh library/report session human-ui-0.7-a409b4d22ecb4f01b8d736dbce513286. Payload, original inputs and host were validated before and after startup. User rating, Launcher review and post-human audit remained pending. Machine checks had passed; the release was still a draft.

0.7.0 bc936cb machine checks completed: actual CI SDK 10.0.401 passed 222 tests per platform; local SDK 10.0.303 also passed 222 tests. Eight templates, 17 ZIP checks, 13 Setup checks, budgets/SARIF, 105 bound contexts with B406/A182, six browser groups/six screenshots, four Launcher groups/10 screenshots and lifecycle checks passed. Audit matched 653 new payloads, 647 old payloads, 14 state files, 782 tracked and 783 captured inputs, with equal host/process state. See [candidate](../acceptance/20261012-0.7-candidate-readiness.md) and its JSON identities. Partial/compliant exit 4 and external-review limitations remained. P2 was complete and P3 machine-ready; human review was pending. Stage commits/pushes and draft preparation could proceed, but final acceptance awaited human/post-human checks. No B policy was adopted; earlier unbound/unanswered states remain historical.

0.7.0 b56d652 passed CI, packaging, native A/B and lifecycle checks, but an invisible chain editor blocked acceptance. [Closeout](../acceptance/20261012-0.7-context-closure.md) retains the failures. An explicit panel and unsaved-close fix passed 222 tests, eight templates, four targeted real-browser checks and original/host equality. Rebuilding after commit and new P3/human review remained pending; the PR was unmerged.

0.7.0 P2 completed: 105 bound contexts, B406/A182 passes, zero findings/errors and partial/compliant exit 4, with identities in [closeout](../acceptance/20261012-0.7-context-closure.md). Hardening passed 222 Release tests, eight templates and original/host equality. The next steps were a stage commit and a fixed P3 candidate. Human review was unstarted and the PR unmerged.

0.7.0 resumed after the user reauthorized records, review, stage commits/pushes and all machine checks, retaining final independent UI review. [Closure](../acceptance/20261012-0.7-context-closure.md) recorded 105 bound contexts, B406 passes, 222 tests, eight schemas and equal host state. A work was active; the P2 commit and P3 were pending. Earlier unanswered/unbound records remained historical.

0.7.0 P1 was accepted: manifest v3/review v2, actual dependency selections, unique owners, context aggregation and JSON/HTML/SARIF/chain localization passed, with 220 Release tests, eight templates, zero warnings/errors and equal host state. See [acceptance](../acceptance/20261012-0.7-context-implementation.md). Fresh P2 IFX, packaging and human review remained pending; the PR was unmerged.

0.7.0 context-partition authority covered project contexts, real IFX binding and stage commits/pushes; the user retained UI review. The [design](../decisions/20261012-0.7-project-context-closure.md) specified manifest v3/review v2, actual references, owner aggregation and compatibility/safety. P0 completed and P1 started. Earlier single-context closure failures were preserved.

0.7.0 candidate 27c993a passed CI with 214 Release tests, ZIP/Setup, real lifecycle, library/Launcher browser checks and final audit. B evidence remained inconclusive for multiple dependency versions; the scope question was unanswered and human review unstarted. Stage 4 was open, the release a draft and the PR unmerged.

0.7.0 real 406-rule library fixes addressed registry-lock HTTP 500 and narrow-layout overflow. Validation passed 214 Release tests, eight templates, five browser groups/six screenshots and original/host equality. Fixed-package rechecks were active; B-context limitations and independent human review remained.

0.7.0 real offline Debug build completed with zero warnings/errors and 105 evaluations. [Review](../acceptance/20261012-0.7-ifx-external-evidence.md) found three multiple-version conflicts; evidence stayed unapproved and sealing was rejected. B covered 105 DLLs and 406 matches but was unbound, so all B results were inconclusive. A passed 182 cases with partial exit 4. Machine operations ran, while trusted B evidence and human acceptance remained open at the draft stage.

0.7.0 offline snapshot restore passed. Implicit .editorconfig/.globalconfig/.ruleset identities and five invalidation tests were added; 213 Release tests, eight templates, zero warnings/errors and host checks passed. Old e5147a5 evidence remained historical and a new candidate was required after the fix. Real evidence work was active; human review remained pending.

0.7.0 delegation covered all remaining machine-verifiable work, including preflight, external build, integration and lifecycle, except final independent UI review. Commands/results had to be reviewed sequentially; drift required stopping and source inputs remained read-only. Earlier operator-waiting states were historical. Stage 4 was active and unreleased.

0.7.0 authority covered implementation, stage commits/pushes, PR, merge and release. The [plan](20261012-archsift-0.7.0-improvement-plan.md) had stages 0–3 complete and stage 4 machine checks active. Candidate [e5147a5](../acceptance/20261012-0.7-candidate-readiness.md) passed CI with 208 Release tests, 17 ZIP checks, 13 Setup checks, budgets/SARIF, native bilingual checks and equal host/target/state. Real evidence and human review still awaited the first operator result. No original installation was upgraded or B compliance accepted; publication remained a draft.

0.6.0 final, 2026-10-11: replacement real IFX machine checks, independent user UI and post-human audit passed. [PR #3](https://github.com/von12549/ArchSift/pull/3) merged, then [public downloads, ZIP/Setup and post-publication audit](../releases/0.6.0-publication.md) passed. Original IFX, installation and host remained equal. ACCEPTED WITH LIMITATIONS / PUBLISHED / VERIFIED; earlier states remain historical.

Date: 2026-10-06 (Australia/Sydney)

Plan ID: 20261006-archsift-implementation

Historical 0.6 update: independent original-IFX step 04 recorded six yes answers, confidence 4/5 and maximum issue L2. Originals, host, configuration and policy remained equal; processes closed. [Findings](../acceptance/20261011-0.6-ifx-step04-human-usability.md) required fixes for excessive profile whitespace and the missing external JSON template. Original a05c44e measurements remained historical. Replacement de222c2 passed CI with 200 tests, 17 ZIP checks, 13 Setup checks, browser checks and draft downloads; see [replacement](../acceptance/20261011-0.6-replacement-candidate.md). Real replacement checks, human review, legacy lifecycle and audit were pending: IN PROGRESS / NOT ACCEPTED / NOT RELEASED at that point.

Historical FORMAL status: W00–W11 completed; 0.2.0 was frozen and W11 published/verified 0.3.0, with 130 CI tests per platform, ZIP/SARIF, real IFX, human and safety checks. Failures were retained. The [0.4 plan](20261009-archsift-0.4.0-improvement-plan.md) was active and unaccepted on 2026-10-09. User authority covered clear functional UI sections, concise information, distinct colors and the full codex/archsift-0.4.0 implementation/Git/PR/merge/release. A was frozen; B–E passed 142 Windows tests, frontend, desktop/narrow layout and SARIF. F candidate 061a53d passed 142 CI tests per platform, two 17-check ZIP runs and downloads, with draft PR #1/private release. IFX discovery recorded 105 projects, zero assemblies, 105 limitations and exit 4; verification had 182 passes, zero findings and partial/compliant exit 4. All 563 sources, inputs and host stayed unchanged. The initial step-3 API/CLI stop was retained; eight checks passed after the fix. Step 4 had six yes answers and 4/5 usability, but result-writer syntax, button layout and long cards blocked acceptance; operator recovery preserved originals/host. Revised a561e7d passed 142 CI tests per platform, two 17-check ZIP runs, downloads and step-4 six yes answers/4 of 5, before an expanded-editor problem with 182 rules. At that historical point, merge, publication and notification were pending and gates still applied.

Later 0.4.0 candidate bb599bd passed real-browser review with six yes answers and 5/5 usability, two explicit cancellation-contract tests and final read-only audit. See [final acceptance](../acceptance/20261009-0.4-final-acceptance.md) and [publication](../releases/0.4.0-publication.md): ACCEPTED WITH LIMITATIONS / PUBLISHED / VERIFIED. Limits remained 105 declaration projects, zero assemblies, 105 coverage limitations and no policy adoption. Prior failures were preserved.

Handoff, 2026-10-09: the user authorized notifying IFX-Guard-Test-Project for Agent planning of ArchSift–IFX integration; this was added to the 0.4 plan. At that point: PUBLISHED / HANDOFF PENDING. Merge, release and download verification were complete. The handoff authorized planning rather than target tests.

2026-10-10: the user authorized installation/environment guides and installer/upgrade design; see [guide](../installation.md) and [design](20261010-installation-upgrade-design.md). Documents described actual prerequisites, silent CLI/orphan-process behavior, temporary direct-Web use and manual-upgrade limits. This was design only. The operator confirmed existing configuration, direct Web and shutdown; the standard CLI fix remained pending. [IFX candidates](../../../IFX-10-Root/docs/plans/20261010-archsift-0.4.0-ifx-rule-candidates.md) were not formal policy. 0.4.0 identity stayed frozen; no new version, installation, upgrade or analysis was performed.

Source: [draft](20261006-dotnet-architecture-analyzer-draft.md). User adopted Q01–Q14 defaults 2026-10-06. All product questions closed; preflight/measurements below are execution tasks.

0.5.0 authority, 2026-10-10: the user fixed version 0.5.0 and authorized the CLI startup fix, installer/upgrade/rollback, stage Git work, PR, merge, verification and release. Branch codex/archsift-0.5.0 was created; see [plan](20261010-archsift-0.5.0-plan.md). Work was active, unaccepted and unreleased; real/human/safety gates remained. The earlier handoff-pending state was superseded by delivery of a planning-only 0.4.0 notification.

0.5.0 development reproduced and fixed the silent CLI using stream forwarding and a private parent channel. Setup implemented schema compatibility, ownership, safe package extraction, configuration backup, side-by-side versions, rollback and recovery. Validation passed 178 Release tests, eight templates, Node checks and host checks. Exact-package CI, download, human and real integration checks remained pending. Development guides were updated while the 0.4.0 guide was preserved. See [development](../acceptance/20261010-0.5-setup-development.md).

0.5.0 automated candidate c4010d8 passed 179 CI tests per platform, parent-exit audit, two 17-check ZIP runs, two 13-check Setup runs, four draft digests/download-byte comparisons and host checks. Versioned guides were bundled. See [candidate](../acceptance/20261010-0.5-candidate-readiness.md), draft PR #2 and private release 408066486. At that point: AUTO PASSED / OPERATOR PENDING / NOT ACCEPTED / NOT PUBLISHED. User execution authority continued.

## Goals and delivery boundaries

0.5.0 was published after [PR #2](https://github.com/von12549/ArchSift/pull/2) merged. Tag c4010d8, four public downloads, 17 ZIP checks, 13 Setup checks and host checks passed; see [publication](../releases/0.5.0-publication.md). ACCEPTED WITH LIMITATIONS / PUBLISHED / VERIFIED; earlier pending states remain historical.

0.6.0 improvements were registered: chain progress, bounded parallelism, actual UI version and Launcher profiles, without dependency orchestration. Configuration preloading, a single Setup state and protection contracts had to be resolved first. See [register](20261011-archsift-0.6.0-improvements.md). Design, implementation and publication had not started at that point.

0.6.0 plan freeze specified one managed profile plus external profiles, configuration metadata, default.json, no Setup protection for external profiles, concurrency 1–4 with serial default, and measurements before performance budgets. See [plan](20261011-archsift-0.6.0-plan.md): FORMAL / FROZEN / UNAUTHORIZED / UNSTARTED; A–F were unstarted. This was documentation only, with no code, builds, tests, measurements, installation, real integration, Git or release operations. 0.5.0 remained frozen.

0.6.0 Git-only authority covered creating codex/archsift-0.6.0 and committing/pushing three plans. It did not cover implementation, builds, measurements, real integration, PR, merge or release. A–F remained unstarted.

0.6.0 full authority then covered the entire plan, stage commits/pushes, PR, merge, documentation and release on the current branch. A started; overall work was active, unaccepted and unreleased. Real/human UI and final host/process gates remained required.

0.6.0 A completed DTO/schema, v2 summary and host contracts. Offline locked Release checks used SDK 10.0.303/net10 and passed 189 tests, eight templates and host checks. See [A](../acceptance/20261011-0.6-a-contracts.md). This was development validation, without runtime, Launcher, parallelism or release acceptance.

0.6.0 B completed actual-version display, cancellable preparation, shared serial progress, report/summary timing and final drift checks. Validation passed 191 tests, eight templates, Node bilingual-state checks and host checks. See [B](../acceptance/20261011-0.6-b-progress.md). Parallelism, Launcher, measurements and final real/visual review remained later work.

0.6.0 C completed v2 timings and external synthetic serial CLI/Web measurement, with 191 tests and host equality. Six runs per entry measured CLI 647–668 ms, Web job 616–657 ms, peak 62 MiB and rechecks 250 ms. The cached memory sample was retained and superseded by a Refresh-based remeasurement. See [C](../acceptance/20261011-0.6-c-serial-baseline.md). Source hashes fixed a development baseline, without a release budget or IFX assembly/parallel performance claim.

0.6.0 D completed profile selection, configuration metadata, normalized preview, protection/default behavior, legacy single-configuration lifecycle, private parent channel and native owned-process exit. Validation passed 195 tests, eight templates, Node and host checks; failures were retained. See [D](../acceptance/20261011-0.6-d-launcher.md). The old 0.5.0 guide, real IFX and release gates remained; E was next.

0.6.0 E implementation candidate added bounded concurrency 1–4, serial default, global drift/cancellation, stable order and CLI/API/UI support. Validation passed 200 tests, eight templates and Node checks, using one shared isolated build and two actual ArchUnitNET Workers without executing target entry points. Run development-4fe9696c6cd3445e9a2fd28aa1a02e5e preserved host equality. Measurement code had to be committed before concurrency 1/2/4 measurements and budget freeze. E was not yet accepted.

0.6.0 E completed: 4975dbb measurements and 7918f7c budget freeze preceded the candidate. Validation passed 200 tests, eight schemas, 54 CLI/Web performance runs, 30 actual cancellations and host checks. See [E](../acceptance/20261011-0.6-e-parallel-performance.md). Serial remained default; synthetic concurrency 2/4 improved performance by 11–22%, without an IFX promise. PR #3 was a draft; the Ubuntu Windows-fixture failure was fixed and F rechecks remained pending.

0.6.0 F automated candidate a05c44e passed 200 CI tests per platform, eight templates, Node, 17 ZIP checks, 13 Setup checks, native parallelism, SARIF/budgets, nine browser groups, four digests/download rechecks and host checks. Failures were retained. See [candidate](../acceptance/20261011-0.6-candidate-readiness.md). PR #3 and private 0.6.0 remained drafts, awaiting the operator. Real steps 01, 02 and 03a reviewed planning, installation and a six-policy freeze; original target/host stayed equal, without analysis/build. The [initial 03a stop](../acceptance/20261011-0.6-ifx-step03a-stop.md) occurred before copying due to unordered hashing and was retained; see [retry](../acceptance/20261011-0.6-ifx-step03a-policy-freeze.md). Step 03b was next under the [gates](../acceptance/20261011-0.6-operator-gates.md).

0.6.0 F [03b](../acceptance/20261011-0.6-ifx-step03b-serial-canary.md) used a fixed package, four rule groups and fixed inputs: 105 projects, 585 inputs, 182 passes and 105 unevaluated limitations per group. Evidence was unbound with partial/compliant exit 4, without complete source compliance. Originals, host, configuration and policy remained equal; owned processes exited and no target build/entry point ran. Step 03c was next; acceptance/publication were pending.

0.6.0 F [CLI](../acceptance/20261011-0.6-ifx-step03c-cli-measurements.md) and [Web](../acceptance/20261011-0.6-ifx-step03d-ui-measurements.md) each measured 18 runs with identical inputs/semantics. Warm CLI timings were 1638/1236/1054 ms; Web job timings 1576/1195/1022 ms and total 2094/1703/1546 ms. The [initial port stop](../acceptance/20261011-0.6-ifx-step03d-first-stop.md) was excluded. These were declaration-only partial exit-4 results; originals, host, configuration and policy stayed equal and owned processes exited. Fresh [18 CLI](../acceptance/20261011-0.6-ifx-step03e-cli-qualification.md) and [18 Web](../acceptance/20261011-0.6-ifx-step03f-ui-qualification.md) runs passed the [frozen IFX budget](../performance/0.6-ifx-budget.json). Serial remained default; human, lifecycle and audit gates were pending.

0.5.0 final: c4010d8 passed automated and real operator checks. Audit covered complete source, four assets, original IFX 0.4.0 reports, the new installation, external-copy versions/backups, operations, processes/listeners and host. At that point: ACCEPTED WITH LIMITATIONS / RELEASE AUTHORIZED / NOT YET PUBLISHED. See [final](../acceptance/20261010-0.5-final-acceptance.md). Historical pending states are not current status.

0.5.0 first planning stop confused the installation root with D:/IFX-10-Root/ArchSift/versions/0.4.0. The script/baseline were fixed and five synthetic layouts plus host checks passed. A real retry was pending; c4010d8 stayed unchanged. See [candidate](../acceptance/20261010-0.5-candidate-readiness.md).

0.5.0 [step 01](../acceptance/20261010-0.5-ifx-step01-plan.md) retry read and reviewed plan/files, before/after states, original/target/host equality and the new-root configuration/package. No apply, analysis or build ran. Installation, private UI and inspection were next; the wrapper was tested, c4010d8 frozen and gates pending.

0.5.0 [step 02](../acceptance/20261010-0.5-ifx-step02-install.md) inspected the receipt, selected 0.5.0, matched configuration/manifest/state to the plan and checked private UI plus original/host equality. No build or analysis ran. Terminal/human checks were next; c4010d8 was unmerged at that point.

0.5.0 [03a](../acceptance/20261010-0.5-ifx-step03-safe-shutdown.md) confirmed visible URL/PID/waiting status and configuration. CLI exited 0, Web exited, and host, IFX, original configuration and 626 payloads remained equal; no build ran. Ctrl+C, redirection, lifecycle and audit were next.

0.5.0 [03b](../acceptance/20261010-0.5-ifx-step03-ctrl-c.md) confirmed Ctrl+C, CLI exit 130, Web/listener exit on port 40888 and unchanged protected states. Redirection was next and its synthetic checks had passed; lifecycle, audit and release remained pending.

0.5.0 [03c](../acceptance/20261010-0.5-ifx-step03-pipe-file.md) confirmed piped readiness, shutdown exit 0, Web/listener exit, token-free files and unchanged protected states. An external state copy and read-only adoption planning were next, followed by lifecycle/audit.

0.5.0 [04a](../acceptance/20261010-0.5-ifx-step04a-copy-plan.md) copied 621 payloads, two library files including a tombstone and six reports, verifying bytes and changing configuration paths only. Plan `09ab270957afd8f66908da4132df1f293677580cdb5b2b077dd6946ba4d9b47a`, SHA-256 `9921ac788fe415556b3bf3919d70517365d2dc47391b6fc6ec009824801ef7e5`. Originals and host remained equal; no adoption, upgrade, analysis or build ran. Only the copy would be adopted/inspected and its upgrade previewed next.

0.5.0 [04b](../acceptance/20261010-0.5-ifx-step04b-adopt-upgrade-plan.md) produced receipt `77534c6576a1405d93fffb69031f3893`, selected/verified 0.4.0 and confirmed zero pending operations with state/original/host equality. Reviewed upgrade ID `b66f8bb972f168bbfea6cac91da4b7e47b794d0bbd4cdc458c05b2afe5b620bf`, SHA-256 `69bc2c984d3d1849713b3795b1c990dc6d76de11bf8d5a72177735aa0fe06332`. Smoke checks ran without apply, analysis or build. Upgrading the copy and checking backup were next, then rollback/audit.

0.5.0 [04c](../acceptance/20261010-0.5-ifx-step04c-upgrade.md) produced receipt `6efbaf703da244aa8ee24f175fa6952e`, selected 0.5.0, retained 0.4.0 and verified private UI/full backup with zero pending operations. State, six reports, originals and host stayed equal; no build ran. Rollback selection/hash checks on the copy and then audit were next.

0.5.0 [04d](../acceptance/20261010-0.5-ifx-step04d-rollback.md) produced receipt `8d04aec04cf043d1acd9468b45b901a2`, selected 0.4.0, retained 0.5.0 and confirmed zero pending operations. State, backup, six reports, originals and host stayed equal; no build ran. Final read-only audit was required before release.

ArchSift is an independent local .NET architecture and dependency analyzer. It discovers graphs, supports rule authoring, verifies projects and generates reports; change verification and CI integration follow. CLI, configuration and local UI share services and contracts.

The first complete 0.1.0 covers six rule types, discovery/verification, real ArchUnitNET, CLI/loopback Web, JSON/HTML reports and a Windows x64 local ZIP. 0.2.0 adds changes, SARIF, Linux CLI and nonblocking CI examples. Releases depend on evidence rather than preset dates.

Initial authority covered a formal plan only, followed later by W00 completion, a local commit and W01. Subsequent user requests authorize the corresponding work package; remote changes, pushes, releases, Guard work and consumer CI require matching authority.

## Frozen product decisions

| Question | Final decision | State |
| --- | --- | --- |
| Q01 identity | ArchSift product/namespace, archsift CLI/repository and independent SemVer | CLOSED |
|Q02 repository|Existing user-created D:\ArchSift Git/Local Project ArchSift, personal remote von12549|CLOSED|
|Q03 platform|.NET 10, Windows x64 CLI-UI first, Linux CLI later|CLOSED|
| Q04 targets | SDK-style C#, sln/slnx/csproj, net8/net9/net10 and explicit TFM runs; older .NET Framework, F# and VB deferred | CLOSED |
|Q05 scope|Explicit root/solution plus unlisted projects, external uncovered, declarations first, evaluation later|CLOSED|
|Q06 rules|References/integrity/TFM/direct NuGet deny/type dependency/naming, exact-glob, regex-code deferred|CLOSED|
| Q07 NuGet | Direct references, resolvable central versions and case-insensitive IDs; version/transitive checks deferred; no automatic network access | CLOSED |
| Q08 artifacts | Existing artifacts by default; assembly-only evidence is unbound. Explicit isolated builds use offline restore unless network is configured, Debug and a disposable cache | CLOSED |
| Q09 UI | Loopback .NET service, JSON forms and read-only Markdown; no accounts or remote service | CLOSED |
|Q10 changes|HEAD-worktree/index-unstaged-selected untracked/base..head/final states, no fetch-per commit|CLOSED|
|Q11 rules|Scoped reasoned exceptions, history comparison not exemption, zero inconclusive/explicit allowEmpty NA, duplicates-conflicts error|CLOSED|
|Q12 reports|JSON-HTML first/SARIF later, English machine fields, Chinese UI-readable preferred originally; violations 0/errors nonzero/separate inconclusive/fail-on deferred|CLOSED|
|Q13 acceptance|IFX/independent fixtures/sequential human commands/confidence>=4 of 5/max L2/measure then budget|CLOSED|
|Q14 distribution|Local ZIP first/NuGet tool later/licenses before migration/Guard new features paused/old UI maintenance separate|CLOSED|

## Implementation references

| Object | Read-only 2026-10-06 result | Treatment |
| --- | --- | --- |
|Guard|C:\Users\von12\OneDrive\Desktop\Guard, HEAD `40539d4610889483b4aed4637f8f3457098b1ff0`, 1.2.2, ordinary clean|Selective reference, recheck file hashes|
| SDK | Guard build/global.json selects 10.0.100/latestFeature without prerelease | Independent global.json with the same baseline; record actual SDK |
| ArchUnitNET | Guard lock pins 0.13.4 | Pin the initial engine and actual dependency closure; compatibility failures require plan changes |
|IFX|D:\IFX-10-Root\IFX-New, HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`, IFX.sln, central net 10|Record baseline, external build/violation fixtures|
| New project | D:\ArchSift has .git/README; user confirmed an empty repository and Local Project | Check AGENTS, status, remote and initial commit; reuse the existing repository |

The user confirmed the repository and Local Project. Earlier sandbox Git denials do not prove that execution is unavailable. Begin there and preserve README/.git. An empty repository can legitimately lack HEAD. Proposed remote von12549/archsift still requires an actual URL, existence and case check; local confirmation does not authorize remote creation.

Formal handoff destination: D:\ArchSift\docs\plans\20261006-archsift-implementation-plan.md. Create it only if absent; otherwise compare and preserve added records. Maintain subsequent status, evidence and changes in the target Local Project; retain the source handoff baseline.

No root Guard LICENSE found then. Before copying record ownership/provenance/third party licenses; unchecked files not copied. Existing migration task, not reopened product direction.

## Repository and dependency direction

Use .NET core libraries and multiple entries, central models/rules, no dynamic plugin platform for six initial types.

```text
D:\ArchSift\
  ArchSift.slnx
  global.json
  Directory.Build.props
  Directory.Packages.props
  AGENTS.md
  README.md
  src/
    ArchSift.Contracts/              Rule/config/report/artifact DTOs
    ArchSift.Core/                   Input identities, project model, rules, services, comparisons
    ArchSift.ArchUnit/               Assembly model / ArchUnitNET adapter
    ArchSift.Cli/                    CLI and Worker entry
    ArchSift.Web/                    Local ASP.NET Core and static UI
  schemas/
    ruleset.schema.json
    config.schema.json
    report.schema.json
    assembly-manifest.schema.json
    comparison.schema.json          P4
  templates/rules/                  Six JSON templates / generated Markdown
  tests/
    ArchSift.UnitTests/
    ArchSift.IntegrationTests/
    ArchSift.ArchitectureTests/
    fixtures/
  scripts/
    Verify-Contracts.ps1
    Verify-Fixtures.ps1
    Measure-Performance.ps1
    Test-HostSafety.ps1
    Test-PackageSmoke.ps1
    Publish-LocalZip.ps1
  docs/
    rules.md
    reports.md
    cli.md
    build-inputs.md
    decisions/
    migration/source-inventory.json
    migration/third-party-notices.md
    performance/budget.json
    acceptance/
  samples/                         Configuration/rules, no real-run caches
```

Dependencies: CLI/Web→Core; Core→Contracts/analysis interfaces; ArchUnit implements Contracts/Core interfaces, composed by entries. Core never references CLI/Web, Guard install, V4_STAGE_INPUT_JSON or Profile/Stage. Product architecture tests enforce direction.

Initial Web uses ASP.NET Core/HTML/CSS/JavaScript forms and shared services/serializers. Display effective configuration: explicit entry options→config→documented defaults.

Prefer built-in .NET APIs. Necessary external schema/test/source packages get rationale/version/license/lock in P1 without per-package user decisions. Explicit pins; upgrades are later changes.

## Rule contracts

JSON is sole executable authority. Initial schemaVersion 1/draft-07, separate schema/semantic validation. Unknown type/field, missing parameter or unsupported selector is configuration-error.

Ruleset: schemaVersion/id/version/description/rules/exceptions. Rule: id/type/enabled/scope/parameters/severity/reason. Severity info/warning/error. Selectors explicitly kind/match(exact-glob)/value.

Load configured order and record each identity/raw hash. Effective IDs globally unique; no last-wins for duplicates/conflicts. Single-rule sets and multi-file composition supported.

Glob supports * and ?, paths additionally **. Root-relative / paths, never implicit regex. Project ID relative csproj; names/types/namespaces Ordinal, NuGet OrdinalIgnoreCase. Case-colliding project IDs reject on Windows and follow same normalization on Linux.

| Type | Initial semantics | Inputs/boundary |
| --- | --- | --- |
|project-reference|source/target selectors prohibit edge|Declarations; conditional/expression edges inconclusive|
|graph-integrity|resolved-references or solution-membership|Inventory; external explicitly uncovered, no invented missing internal project|
|target-framework|Project scope/allowedFrameworks|Direct literals or nearest root-contained unconditional literal Directory.Build.props|
|nuget-denylist|forbiddenPackageIds|Direct PackageReference/case-insensitive/no transitive promise|
|type-dependency|source/forbiddenTarget type-namespace selectors|ArchUnitNET/assembly-build context|
|naming|project/assembly/type/source-file, exact-glob requiredName|Corresponding models; source files only relative names/paths|

Implementation-location excluded from 0.1.0 despite Guard algorithms. Zero type-source matches inconclusive unless explicit allowEmpty=true→not-applicable. Absent forbidden targets may pass with adequate coverage, distinct from missing sources.

Exceptions require ruleId/stable scope-or-selector/reason. Retain original finding/association; exempt findings do not count effective violations and exemptions are counted. Historical baselines never automatically exempt.

Generate Markdown with provenance/version/hash/explanations/fields. Read-only means projection, not file-permission security. Edits never change execution. Explicit outputs, no automatic target writes.

Draft-rules separates observedFacts/candidateRules/unresolvedDecisions. Suggestions require human choices; existing dependencies never auto-approved.

## Discovery and scope

Required TargetRoot, explicit root-contained solution/project or inventory of supported projects without entry. Report included/unlisted/external/unresolved/unsupported. Graph discovery independent of rules.

SDK C#/sln-slnx-csproj, tool .NET 10/targets net 8-9-10, separately record SDK and target TFM. Missing/offline targeting packs yield accurate inconclusive.

Explicit per-TFM selection, no extrapolation across frameworks. Missing selection config error lists choices. Declaration discovery may list literal TFMs but labels declared model.

XML reading executes no MSBuild. Complex conditions/imports/expressions/central configuration mark affected facts inconclusive. Read direct packages/root-contained literal central versions; version/transitive checks deferred. Unknown declarations never mean no references.

Outside-root projects external/uncovered, no recursive external scan. Never cross links/reparse. Requested linked input gets explicit limit/path error, no silent pass. Scope always reported.

## Input identity and artifacts

Unified InputManifest distinguishes analysisInputs/buildInputs/evidenceInputs. Analysis includes relevant source/projects/solutions/props-targets/rules; build includes actual imports/resources/generators/context. Shared scope selection across discovery/verification/snapshots/comparison.

Exclude .vs/.idea/.git/ordinary bin-obj/tool outputs, but retain explicitly referenced resources/generated source. Unsupported external imports/dependencies/generation keep limits, not full-source compliance.

Identity includes relative paths/hashes/rule-engine versions/TFM/config/SDK-build arguments/assemblies. Optional Git provenance, absolute paths only locators. Stable finding ordering/IDs, separate runMetadata time/duration/paths.

Verify before/after. Relevant changes produce source-changed-during-analysis/current-source inconclusive. Open IDE/excluded caches alone do not invalidate; cancellation never reuses old success.

Existing artifacts default. Manifest records schema/source-build hashes/project mapping/SDK/TFM/config/generation/assembly SHA. Unbound DLLs allow assemblies-only; current-source rules inconclusive with reason. Timestamps not binding.

Explicit isolated mode creates external snapshot/artifacts, default Debug. Manifest binds conservative captured root inputs/final assemblies/context/closure, not exact MSBuild access trace. Initial SDK isolation excludes unreviewed custom tasks/imports/output paths/conditions/package scripts/generators: reject or disclose unbound, full evaluation deferred. Separate Worker avoids assembly-version contamination, never calls target entry/business methods.

Offline restore uses explicit caches/local feeds. Network only configured allowNetwork=true with explicit sources. Ignored restore failures/stale outputs never successful builds. Disposable cache not authority; hits still verify input/bytes.

Child DOTNET_CLI_HOME pairs DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0, no parent/User/Machine writes. Isolation not full sandbox; disclose possible MSBuild custom execution when build enabled. Original source read-only, builds external.

Build failure preserves completed project checks; assembly rules inconclusive plus execution error. Missing dependencies/failure/config mismatch/hash changes never current-source compliance.

## CLI/configuration

These were implementation command targets, not executable behavior at original planning time.

| Command | Behavior | Stage |
| --- | --- | --- |
|archsift analyze --config <file>|Rule-free discovery/graph/report|P2|
|archsift verify --config <file>|Target/rules execution|P2|
|archsift rules validate --file <rules.json>|Schema/semantics|P1/P2|
|archsift rules render --file <rules.json> --output <file.md>|Markdown projection|P1/P2|
|archsift rules draft --config <file> --output <directory>|Unaccepted suggestions|P2|
|archsift ui --config <file>|Shared-config loopback UI|P3|
|archsift changes --config <file> [--base <ref> --head <ref>]|HEAD/worktree default or explicit states|P4|

Explicit target/entry/rules/output/tfm/configuration override shared validated config. Config-relative paths use config directory; CLI paths invocation cwd. Show resolved absolute locations, no hidden cwd/Guard PATH dependency.

Example structure implemented by schemas/config.schema.json:

```json
{
  "schemaVersion": 1,
  "target": { "root": "D:/sample", "entry": "Sample.sln" },
  "rulesets": ["./rules/architecture.json"],
  "build": {
    "mode": "existing",
    "targetFramework": "net10.0",
    "configuration": "Debug",
    "assemblyManifest": "./evidence/assembly-manifest.json",
    "allowNetwork": false
  },
  "output": { "directory": "D:/archsift-output/sample", "formats": ["json", "html"] }
}
```

assemblyManifest optional for project discovery, supplied for bound artifacts. Isolated generates manifest; existing may explicitly list unbound assemblies. Distinguish report identities.

## Reports and exits

report.schema.json schemaVersion 1:operation/toolVersion/inputIdentity/rulesetIdentities/scope/buildContext/engineVersions/execution/compliance/ruleResults/findings/coverage/limitations/runMetadata.

Rule states pass/violation/inconclusive/not-applicable/error; execution completed/partial/failed/cancelled; compliance compliant/noncompliant/inconclusive/not-applicable, null for rule-free analysis. Effective violations noncompliant with partial coverage retained. No violation+insufficient coverage inconclusive; all inapplicable never compliant.

Finding: stable ID/rule/subject/source-target-or-name/evidence/available relative location/severity/explanation/exception. Without source mapping show assembly/type, never invented lines. JSON authority, escaped HTML projection with coverage/limits.

| Exit | Meaning |
| --- | --- |
|0|Completed/determinate, may contain nonblocking violations|
|2|Argument/schema/semantic configuration error|
|3|Tool/dependency/build/report execution failure|
|4|Completed/partial with inconclusive checks, input change or insufficient provenance|
|130|User cancellation|

Errors/cancellation/inconclusive select corresponding nonzero exits while preserving completed results. Nonblocking means violations alone do not fail, never hidden tool errors. Future fail-on/code deferred.

## Local UI

Random free loopback port, only 127.0.0.1/localhost. Target/solution/rules/templates/build/TFM-config/output/run, graph/results/downloads.

Server validates canonical paths/shared boundaries. Writes only explicit editor/output/temp roots; no target-source edits or arbitrary shell/command input. Session token/Origin/request limits protect configuration/execution APIs.

Current runID owns results/errors. Editing config during run applies next time. Old results historical/input-labeled; fail/cancel/drift/stale artifacts clear current success. Cancellation supported, no remote accounts/control plane.

UI answers what was checked, which rules violated and which inconclusive. Hashes/diagnostics collapsed, no manual hash/eight-step onboarding.

## Change verification and CI

P4 defaults to full HEAD versus final worktree, including index, unstaged and selected untracked source. Report included/excluded files. Mixed staged/unstaged uses final disk content, not index-only claim.

Explicit base/head are local resolvable commits, no fetch/upstream guessing. Validate refs and pass argument arrays without shell concatenation. Record SHAs; final states, not each intermediate commit.

Analyze both fully, compare stable findings across projects/interfaces. New/existing/resolved require comparable identity/policy; disappearing results without comparability not resolved. Rule deletion/exceptions/scope reductions separate policyChanges.

Failed baseline retains target, comparison inconclusive, no inferred new findings. Fixtures cover delete/rename/cross-project/context. Temporary snapshots never repeatedly checkout user worktree.

P5 Linux CLI/SARIF/opt-in CI artifacts. Consumer chooses workflow, no automatic required checks/policies. Own CI verifies source/contracts/fixtures/packages/regression.

## Work packages

| Package | Depends | Tasks | Evidence |
| --- | --- | --- | --- |
|W00|Plan|Reuse D:\ArchSift/Project, check instructions/Git/remote/initial commit/import/provenance/licenses|Plan/baselines/hashes/license inventory/existing files preserved|
|W01|W00|Solution/projects/global/Directory files/locks/tests/AGENTS-README|Windows build/dependency tests|
|W02|W01|Contracts/schemas/six templates/load-semantics-exceptions-Markdown/CLI|Valid-invalid-duplicate-zero/template contracts|
|W03|W01-02|Input manifest/discovery/sln-slnx/literal props-packages/graph|Roots/uncovered/conditions/IDE/source change fixtures|
|W04|W02-03|References/integrity/TFM/packages/naming/drafts|Clean/violation/missing/unsupported|
|W05|W03|ArchUnit/Worker/manifest/existing-isolated/offline/type checks/cache|Actual positive-negative/stale-closure-context-build failures|
|W06|W04-05|Services/JSON-HTML/CLI/exits/cancel|Integration/escaping/stable findings/partial-errors|
|W07|W02-06|Loopback/forms/JSON save-Markdown/graph/run-cancel-history|Parity/stale clear/loopback-paths|
|W08|W07|Smoke/safety/budget/ZIP/real IFX/human/archive|A01–12 proof|
|W09|W08|Comparison/schema/changes/UI|States/cross project/baseline failure/delete-rename/policy fixtures|
|W10|W09|SARIF/Linux CLI/CI examples/docs/package|Nonblocking violations/visible errors/core parity|
|W11|W10/post release 0.2.0|Allowlists/stable exceptions/context-hierarchy/lifecycle|130/eight templates/real CLI-UI/independent operator/audit|

Independent discovery/template work may parallelize, no subagent requirement. Actual W05 ArchUnitNET required inside 0.1.0, no shell/declaration-only substitute.

Guard reuse limited to analysis/fixtures/discovery/framework/isolation. Implementation-location/source semantics may stay inventory, not automatic delivery. Old stale UI maintenance separate, not prerequisite or old-source edits here.

### W11 post-release package

Originally future W11, unchanged W09-W10 scope. Both 0.2.0 release/[post verification](../acceptance/20261007-0.2.0-post-release-verification.md) complete/user authorized 2026-10-07; design/code/auto/real CLI-UI/audit COMPLETE, [record](../acceptance/20261007-w11-allowlists-ui.md). [W08feedback](../acceptance/20261007-w08-completion.md)/review informed [frozen composition](../decisions/20261007-w11-policy-composition.md).

| Direction | Goal | Minimum gate |
| --- | --- | --- |
|Project allows|Closed selected-source targets, unlisted deny is not a loophole|All formats/templates/parity/clean-violation-zero-incomplete/exact-glob/multi file/real read only|
|NuGet allows|Scoped direct IDs/retained deny, no transitive-version-vulnerability claim|Case insensitive/central-incomplete facts/allow-unknown-deny-exception positive-negative|
|Composition|Freeze full matrix first, independent constraints/intersection/deny retained/explicit exceptions only|Order-permutation invariant/duplicate-incompatible-dangling errors/broad allow-narrow deny/multi allow/severity-overlap|
|Exceptions|Replace first match attribution with rejection or stable choice|Permutation invariant/result identity/reason|
|Target UX|Persistent product/root-entry-rules-output/real-fixture distinctions|Independent tasks correctly identify target before execution/no silent wrong root|
|Large UX|Summary/filter/collapse/translated terminology|Desktop-narrow/no hidden limits-status-counts|
|Lifecycle|URL/waiting/final JSON/shutdown/PID-port audit|Normal-cancel-tab close-interrupt-duplicate/no residue/no unknown termination|

Freeze composition/compatibility before schema. One allow never exempts other constraints; no automatically approved graph-derived policy. Old rules readable; explicit verified migrations, no silent old-conclusion changes.

### W00 closure

[Takeover](../acceptance/20261006-w00-takeover-review.md) rechecked plan/Git/remote/SDK-packs/Guard-IFX heads/provenance/P05 failures-cleanup/cache metadata.

[Completion](../acceptance/20261006-w00-completion.md):user Guard ownership/reuse-distribution recorded GUARD-OWNER-20261006 in [inventory](../migration/source-inventory.json).14 assets commit-hash-method-scope-destination-checks, four old fixture JSON reference only.AGENTS/[notices](../migration/third-party-notices.md)/full licenses-NOTICE-nuspec archived.

W00 COMPLETE/W01 NOT STARTED then.Seven packages/33DLLs byte consistent, not solution restore/full closure/release proof.All migrationExecuted=false, recheck before code/record actual hashes.W01 locks/new deps, W08 actual notices.No Guard-IFX/product W01–10/Git remote operations.

Later W00 commit `5c378153abde38540dd81184706f92306091c53c`; historical manifest retains stage identity.

### W01 closure

2026-10-06:solution/five products/three tests/SDK-central packages-offline sources/eight locks/check script/README.Windows x64 SDK 10.0.303/net 10 Debug/initial-locked restores/eight builds 0 warnings-errors/nine identity-entry-direction tests.Host environment-PATH-four Profiles equal each step/final.

[Acceptance](../acceptance/20261006-w01-foundation.md)/[dependencies](../dependencies/w01/README.md).W01 COMPLETE/W02 UNSTARTED, W01 uncommitted-unpushed then.Engine package-versus DLL distinct, actual rules W05, no W02–10 features started.

## Verification and completion

Originally planned W01–08 commands, not then-existing passes. Synthetic internal regressions automated; real Guard-IFX sequential reviewed operator blocks.

| Check | Entry | Gate |
| --- | --- | --- |
|Build/tests|dotnet build/test ArchSift.slnx|Recorded SDK/platform, no Guard install|
|Contracts|pwsh -NoProfile -File scripts/Verify-Contracts.ps1|Valid-invalid/JSON-Markdown parity|
|Fixtures|pwsh -NoProfile -File scripts/Verify-Fixtures.ps1|Six rules/coverage/failures|
|Host|pwsh -NoProfile -File scripts/Test-HostSafety.ps1|No persisted mutation/child tools registration|
|Performance|pwsh -NoProfile -File scripts/Measure-Performance.ps1|Hardware SDK scale/cold warm/time-memory-cancel|
|Package|pwsh -NoProfile -File scripts/Test-PackageSmoke.ps1|Fresh relocation/no Guard/correct exits|
|Build ZIP|pwsh -NoProfile -File scripts/Publish-LocalZip.ps1|Self-contained win-x 64/version-deps-licenses-hashes/offline UI|
|Real/human|docs/acceptance/ifx-local-analysis.md|Inputs/reports/scores/cleanup|

Self-contained tool does not remove target SDK/packs prerequisites. Declaration-only needs no compiler SDK; explicit builds require target prerequisites with accurate diagnostics.

0.1.0 criteria:

| ID | Criterion |
| --- | --- |
|A01|Rule-free graph/scope/unlisted-external-unresolved-unsupported/context|
|A02|Six templates/schema/JSON-Markdown/overrides agree, invalid rejects|
|A03|Every rule valid-violation-inconclusive-boundaries/actual ArchUnitNET|
|A04|Accurate multi TFM/central config/conditions/unsupported scope|
|A05|Missing-stale-unbound DLL/closure/context/build fail no current source pass|
|A06|IDE/cache no irrelevant invalidations/relevant save detected|
|A07|Stable core/CLI-UI parity/no invented locations/safe HTML|
|A08|Violations 0/errors-inconclusive-cancel correct/no stale success|
|A09|Read only target/root boundaries/child isolation/host-original checkout unchanged|
|A10|Real IFX+fixture/reviewable/independent operator>=4 of 5, max L2|
|A11|Measure then commit budget/reference-config/qualify the candidate/justified changes|
|A12|Fresh ZIP CLI-UI/version-hash-limits-notices/no PATH-Profile writes|

Help: L0 independent, L1 general, L2 document location/one error, L3 step-field guidance, L4 proxy.Record actual human scores, never implementer filled.Without operator continue code but A10 unpassed.

W08 performance engineering does not reopen Q13. Archive raw representative runs/rationale, separate restore-build from analysis, no single best proof.

## Run roots and safety

Source D:\ArchSift; real outputs D:\ArchSift-lab\runs/fixtures/packages/evidence/archives. Own bin-obj ignored, target builds external snapshots.Normal approval, no tool-based permission bypass.

Before real tests hash User/Machine environment/Process PATH/four Profiles/existence, only hash-equality output.Recheck install/.NET/cleanup/end.Persistent drift stop/preserve, no repair.

Regression rejects persisted APIs/paired child CLI home/parent unchanged/escaped outputs/links/build failure/cancel.No manual short proof maintenance.

Clean only recorded temporary artifacts, no recursive source-repo-broad roots.Worktrees normal Git removal/registration-disk check.Sandbox denials infrastructure, not product violations.

## Deferrals and revisit conditions

| Feature | Limit | Revisit |
| --- | --- | --- |
|Natural-language/Agent plans|No compliance promise|Stable core-changes/structured contract/separate LLM design|
|Database SQL-migrations|No connections/execution|Scoped read only rules defined|
|Old Framework/F#/VB|Unsupported|Parsers/toolchain/fixtures|
|Full MSBuild|Declaration conditions limited|Explicit isolated dimensions/cross machine proof|
|NuGet versions-transitives|Direct IDs|Offline closure contract|
|Regex/scripts/plugins|Standard exact-glob|Real need/verified boundary|
|Implementation-location/source semantics|Outside initial six|Accurate core/independent fixtures|
|NuGet tool|Local ZIP initial|0.1.0 accepted/identity-license-process checked|
|Fail-on/enforced CI/governance|Report only default|Explicit needs/separate policy design|
|Per commit/incremental|Full final states|Stable models/measured need|

## Risks and changes

Risks: declaration/build mismatch, insufficient provenance/offline packages, IDE/generated selection, false zero/cache passes, projection drift. Address explicit scope/shared inputs/actual ArchUnitNET/coverage/shared reports.

Stop affected work for inadequate rights/unowned changes/unreproducible inputs/hostdrift, continue independent tasks. Ordinary classes-algorithms-package choices within scope belong implementer, record decisions.

Changes to initial rules/network/target writes/enforced CI/platform/deferrals update plan/impact.No Guard availability scope expansion or premature completion.

## Decisions and delivery log

| Date | Event | Status |
| --- | --- | --- |
|2026-10-06|D01–15 independent product|Accepted|
|2026-10-06|Q01–14 defaults|All adopted/CLOSED|
|2026-10-06|Formal plan/archived draft|Docs only/code not started|
|2026-10-06|User created D:\ArchSift/Local Project|Reuse/import/maintain there|
|2026-10-06|Takeover|Plan-Git-tools-provenance access checked/ready W00/rights-notices pending/W01 unstarted|
|2026-10-06|W00 closed|Guard rights/14 assets/7 licenses/AGENTS/delivery checked/W01 unstarted|
|2026-10-06|W00 commit/W01 authority|`5c378153abde38540dd81184706f92306091c53c`, human primary/Codex trailer, W01 active|
|2026-10-06|W01|Eight offline builds/nine tests/host equal/complete/W02 unstarted/uncommitted then|
|2026-10-06|W01 commit/later authority|`62bace39cf6acad76c79327ec2ec91ea1c19772d`, stage commits authorized, push not yet|
|2026-10-06|W02|Four schemas/six templates/composition/CLI/56 tests/independent Test-Json-CLI, [acceptance](../acceptance/20261006-w02-contracts.md), COMPLETE|
|2026-10-06|W02 commit|`424ed1945fbc77cabd82fff8f0198d94b816a1a4`, Codex trailer|
|2026-10-06|W03|Discovery/boundaries/identity/IDE 64 tests/host, [acceptance](../acceptance/20261006-w03-discovery.md), COMPLETE|
|2026-10-06|W03 commit|`31a3fdede0c93cdc6a235468516a3dea45816d24`, Codex trailer|
|2026-10-06|W04|Rules-naming-exceptions-drafts/79 tests, [acceptance](../acceptance/20261006-w04-project-rules.md), COMPLETE|
|2026-10-06|W04 commit|`b86c326e497e0d6ea680d3fbb4b8d0025f3c82ea`, Codex trailer|
|2026-10-06|W05|Real ArchUnitNET/no initializer/isolated-manifest-Worker-closure/83 tests/limits, [acceptance](../acceptance/20261006-w05-assemblies.md), COMPLETE|
|2026-10-06|W05 commit|`304e1623468c610a2c98d1eae8aa1cc70cc674cc`, Codex trailer|
|2026-10-06|W06|Shared JSON-HTML-CLI/cancel-partial/89 tests, [acceptance](../acceptance/20261006-w06-reports-cli.md), COMPLETE|
|2026-10-06|W06 commit|`9f7c24dcf0c9d33301e68212b7bd22a187708ee0`, Codex trailer|
|2026-10-07|W07|90 tests/HTTP safety-parity/browser positive-negative-stale clear/layout, [acceptance](../acceptance/20261007-w07-workbench.md), COMPLETE/A10 not run then|
|2026-10-07|W07 commit|`a9f17338a253289ad3d55dac3b35618617426800`, Codex trailer|
|2026-10-07|W08 budget|`64b6e238513a94d98f0f04d78cfd3dc47c1eb9f3` frozen before candidate|
|2026-10-07|W08 auto|92/host/five budgets/self-contained no SDK path/identity-licenses, [readiness](../acceptance/20261007-w08-readiness.md)|
|2026-10-07|W08 real-human-audit|105 projects/known violation/fixture-real UI/4 of 5-max L2/zero residue, [completion](../acceptance/20261007-w08-completion.md), COMPLETE/W09-10 not started then|
|2026-10-07|Future W11|User context/wait JSON/orphans/large UX/terms/allowlists/composition-exceptions, after W09-10-release-post verification|
|2026-10-07|0.2.0 post verify|Assets/package/Windows-Linux/CI/UI-IFX/formats/audit passed, failures-limits retained, [record](../acceptance/20261007-0.2.0-post-release-verification.md), W11 not started then|
|2026-10-07|W11 authority/design|After post verify commit-push, user authorized; freeze allows/intersection-deny/exceptions/schema 1/matrix, [decision](../decisions/20261007-w11-policy-composition.md)|
|2026-10-07|W11 auto|Two additive schema 1 types/templates/Core closed policies/stable exceptions/projections/context/filter/shutdown.Initial human 105 expanded limits NEEDS FIX, then visible counts/collapse/four sections+project inventory/Desktop 480px/no overflow.SDK 10.0.303 Debug offline 0 warnings-errors/97+31+2/eight validate-render/host; D:\ArchSift-lab\runs\development-53b4733726dc4624a6a46b063d9f4997.Real CLI passed/new UI-audit pending then|
|2026-10-07|W11 human-close|105 projects/0 assemblies/partial-noncompliant 4/two rules-findings/105 collapsed limitations/filter/layout/formats, independent L1-confidence 4 of 5.Audit precisely reclaimed abandoned PID-port, all checks/24 materials/host-process-container passed, [completion](../acceptance/20261007-w11-allowlists-ui.md), COMPLETE/uncommitted then|
|2026-10-07|0.3.0 published|v0.3.0d1b1d7c/CI 130 each/88,917,483 byte ZIP SHA-256 `0202e9734ec990474c415d4152274db93337a768e51837ef968fc4b539e44c79`/three downloads/smoke-SARIF-budget-host, [publication](../releases/0.3.0-publication.md), VERIFIED|

## References

Historical 0.4.0 a561e7d real browser, six yes answers/4 of 5, earlier button-card fix/original-host safe, [second04](../acceptance/20261009-0.4-ifx-step04-browser-retest.json). New 182 expanded editor finding added collapse-ID-type filter/new targeted candidate; Fopen/not final accepted.

Editor fix bb599bd/CI 142 each/local Debug-Release/two native smoke checks/draft downloads/real preflight passed, [candidate](../acceptance/20261009-0.4-editor-candidate-readiness.md). User targeted retest remaining.

0.2.0 [publication](../releases/0.2.0-publication.md) closed: v0.2.0→528ebb1/ZIP SHA-256 `ee2d27b4d7689ce3eef75c0d3402c684ad1c9a8cb492ae7c8e915510038699d8`/three assets-downloads/native smoke.W090286b75/W108dc363e/fixes 26498f4-eb52640-528ebb1/acceptance 27a5719 history/Codex trailers retained.W11 not authorized at release, later post verify/authority/completion, separate scope, uncommitted-unpublished then.

W09-W10 authority 2026-10-07:stage commits/later release/MIT license.W090286b75/106 passed, W10 budget 63419d4 before qualification.[W10](../acceptance/20261007-w10-report-integration.md) Windows 109/Linux 107/parity/official SARIF/package-browser-CI examples.Build verify committed source before release, never prototype; W08 unchanged.

- [Historical draft](20261006-dotnet-architecture-analyzer-draft.md).
- [Guard discovery](C:/Users/von12/OneDrive/Desktop/Guard/core/host/V4.Guards.Host/ProfileRuntime.cs).
- [Guard architecture adapter](C:/Users/von12/OneDrive/Desktop/Guard/modules/architecture-conformance/adapter.ps1).
- [Guard build evidence](C:/Users/von12/OneDrive/Desktop/Guard/modules/build-evidence-provider/adapter.ps1).
- [P05 final verification](D:/IFX-10-Root/guard-lab/archives/p05-20261006-final-verification.json).
- [P05 safety audit](D:/IFX-10-Root/guard-lab/archives/p05-20261006/cleanup/h4-final-safety-and-cleanup-audit.json).
- [ArchUnitNET](https://github.com/TNG/ArchUnitNET/blob/main/README.md), [build limitations](https://archunitnet.readthedocs.io/en/latest/limitations/debug_artifacts/).
