# IFX stable 0.10.0 upgrade: final acceptance

Date: 2026-10-23. **Both live entrances use released 0.10.0. Machine qualification and independent human acceptance passed; the IFX upgrade is complete.** The user separately accepted this IFX adoption with usability 4/5, expected behavior, no defects and no untested items. The earlier release review remains a separate acceptance.

[IFX PR #27](https://github.com/von12549/IFX-New/pull/27) merged normally to `30cca60467e737ba794e98cde608c713e7f17ff9`, tree `c8a94069130b0a16606319c6ab656a32632c903f`. All three required checks passed for the [PR](https://github.com/von12549/IFX-New/actions/runs/39888414050) and [actual main](https://github.com/von12549/IFX-New/actions/runs/39889184641). Downloaded official artifact sizes/digests, actual checkout trees, typed cases, audit evidence and same SHA/run/attempt/workflow were independently checked. Branch protection and administrator enforcement remain unchanged.

## Qualified behavior

| Area | Observed result |
| --- | --- |
| Runtime | Explicit child-only SDK 10.0.303; Core/ASP.NET/host 10.0.13; eight selected input hashes and 619 preserved official runtime images verified |
| Daily gate | Candidate, rollback, forward and live full runs each passed 493/493 = 92 C# + 401 JSON; no failures or skips; 89 actual fresh production assemblies in the qualified CLI run |
| Policy | Same 404 responsibility sources and exact original 14 policy/rule files; A179, five B07 decisions and three C# A responsibilities unchanged |
| Official audit | Separate fresh 105/105 solution audit and independent CLI distribution audit; true/all/moderate and original warning policy retained; architecture reports still correctly say audit not-run |
| Live Web | Final installed direct entry passed the same 493 typed identities, inputs, Git/target identity and outcome as CLI; JSON/HTML downloads, history and discovery checked; 932 source inputs and 89 CLI assembly hashes preserved |
| Launcher | Actual outer Launcher opened the selected IFX 0.10.0/current-source/Release profile; old profiles remained; latest 493-case report visible; child Safe shutdown and Launcher confirmation completed |
| Setup and rollback | Exact released Setup/ZIP reused; copied real installation upgraded, receipt rolled back to 0.8.0, original scripts/catalog/selection restored exactly, then upgraded again; live Setup plan/apply/smoke completed |
| Preservation | All 0.4–0.8 payloads, old 251-package feed, original main configuration/library/reports and raw source/configuration archives retained; 3,750 original installed/outer files unchanged; only five reviewed old state/entry files changed |
| Host and runner | Host hashes equal; no persisted environment/PATH/Profile, service/autostart or policy expansion; existing registered runner 22 restored and online/idle; owned UI/CLI test sessions closed |

The exact source, receipts and SHA-256 bindings are in the [machine record](20261023-ifx-stable-upgrade.json). Private raw evidence remains under `D:/ArchSift-lab/runs/ifx-stable-upgrade-20261023-01`; it is not copied into the public repository. Release source `f08289433b9bc62c50f0f5c2baf9be9d746510b4`, tag and public assets are unchanged.

## Fixes and retained stops

The external common startup helper now concurrently drains redirected UTF-8 stdout/stderr so `workbench.ps1` exposes its ready address. Its first copied-entry readiness failure remains recorded and is not a successful Safe shutdown.

Three live Web runs passed all tests but failed full parity because Git metadata timed out (`unknown`/dirty). An isolated A/B run using Git for Windows' documented `GIT_REDIRECT_STDIN=off` child setting restored Git identity and parity. The final installed helper includes that setting; a new full run passed without diagnostic overrides. [Git documents standard-handle redirection for multithreaded Windows callers](https://git-scm.com/docs/git/2.46.0). Original reports and failed assertions are retained; no report was rewritten, assertion removed or published binary patched. History freshness remains `unknown` by design: matching observations alone cannot prove current-source compliance.

The delivered [startup helper](../../samples/portable-ifx/Start-ManagedWorkbench.ps1) matches the installed bytes. It is a site-specific external integration example, requires the separately approved runtime/configuration files and explicit installation root, and is not retroactively inserted into the frozen release package. The new current-source profile is an external configuration using the managed rule library; only original `config/ifx.json` retains Setup configuration protection. Preserve both backups separately.

## Rollback operation boundaries

Daily rollback restores the complete pre-upgrade cl03.1 source/locks/approvals/feed combination with the same 493-case policy scope. Following merge, use an inverse PR and normal fresh CI; do not reset unrelated user work. The separate old-source rehearsal passed 493/493 on isolated runtime 10.0.13, followed by stable forward validation.

Workbench rollback requires the active successful Setup receipt and unchanged post-upgrade state. Outer scripts, catalog and selection have independent exact backups and require separate reviewed restoration. New helper/config/state files remain retained but unused after rollback. Stop on later configuration drift; do not force replacement. The live installation was not downgraded during rehearsal. See the [execution plan](../plans/20261023-ifx-stable-upgrade-plan.md).

## Independent human acceptance and closeout

On 2026-10-23 (Australia/Sydney), the user confirmed expected behavior for both entrances, usability **4/5**, **no defects** and **no untested items**. Launcher and its Workbench child were reviewed first; the user then completed the independent `workbench.ps1` step and confirmed the same conclusion and Safe shutdown.

| Entry | Independent closeout UTC | Verified result |
| --- | --- | --- |
| Launcher and owned child | 2026-10-23T04:00:39.6027544Z | Selected 0.10.0, SDK 10.0.303/runtime 10.0.13, child exit 0, host before/after equal |
| Independent Workbench | 2026-10-23T04:09:56.4503374Z | Selected 0.10.0, SDK 10.0.303/runtime 10.0.13, child exit 0, host before/after equal |

The user confirms the Safe shutdown actions; the two separate closeout files independently establish clean entry exits and within-session host equality. No installed CLI/Web process remained. Individual operator-session ports were not separately logged. Operator shell hashes are compared within each session, not against an Agent shell's different process PATH context. The [completed checklist](20261023-ifx-stable-upgrade-human-checklist.md) and [acceptance JSON](20261023-ifx-stable-upgrade.json) bind the original confirmations and closeout hashes. Private evidence is `human-acceptance-01/verification.json` under the existing external evidence root. Original pending/handoff and machine-completion snapshots are retained.

The preceding machine/documentation delivery [PR #20](https://github.com/von12549/ArchSift/pull/20) reached main `91f90a9`; its [actual-main CI](https://github.com/von12549/ArchSift/actions/runs/39896157137) passed both platforms in attempt 2. Attempt 1's single existing parallel-progress assertion failure, official artifacts and logs remain preserved. Exactly one full same-source rerun passed with unchanged assertions. This is not evidence that the timing sensitivity was repaired.

## Next iteration, outside this accepted scope

The user retained two improvements: include the matching Web component in NuGet Workbench delivery so `ui`/Launcher need no separate Workbench installation, and make the Architecture tests JSON inventory collapsible to reduce page length. The released CLI nupkg currently has no Web payload; the separate Windows distribution does include it. The flat JSON inventory is distinct from existing collapsible result groups. See [NX01/NX02 scope and acceptance criteria](../plans/20261023-workbench-next-improvements.md). Both items are recorded, not implemented; the target version remains unassigned and will use one unified product version. Neither item rewrites immutable 0.10.0 artifacts or expands IFX policies.
