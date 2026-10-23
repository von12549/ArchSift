# IFX dual-entry adoption of ArchSift Workbench 0.10.0

Date: 2026-10-23 (Australia/Sydney). Status: UG00–UG07 COMPLETE / LIVE UPGRADE AND FINAL HUMAN ACCEPTANCE PASSED.

The user authorized the complete dual-entry upgrade, child-only .NET 10.0.13 isolation, restoration of the existing CI runner, upgrade/rollback rehearsals, necessary fixes, commits, PRs, normal CI and merges. Final independent visual/hands-on acceptance remains user-owned. Persisted environment/PATH/Profile changes and policy expansion are excluded.

## Frozen starting points

- IFX local and remote main: `96803ed6165c46c0b1d561fddebfc33f5501a0e1`, clean, 928 tracked files. Daily tooling is `0.10.0-cl03.1` with the original 251-package approved source.
- Existing managed Workbench: `D:/IFX-10-Root/ArchSift`, selected 0.8.0. Both outer scripts explicitly reference 0.8.0. The selected Launcher profile points to historical evidence; preserve it separately from current daily use.
- Published target: [v0.10.0](https://github.com/von12549/ArchSift/releases/tag/v0.10.0), accepted source `f08289433b9bc62c50f0f5c2baf9be9d746510b4`. Reuse exact public assets and checksums; do not rebuild or replace the release.
- SDK 10.0.303, .NET Core/ASP.NET Core/host 10.0.13, net10.0/Release/Windows x64. System runtime 10.0.11 stays installed; explicitly select the separately approved runtime root in owned children.
- Keep 493 cases = 92 C# + 401 JSON; 404 responsibility sources; 89 production assemblies; expected full solution audit 105 projects plus CLI distribution closure. A179, five B07 items and retired routes retain their existing decisions.

## Sequential work packages

| ID | Scope | Required evidence |
| --- | --- | --- |
| UG00 | Preserve source/configuration/versions/evidence and verify target assets | Verified raw-source archive, configuration backup, source/installed file inventories and old/new feed hashes |
| UG01 | Explicit isolated runtime selection for gate/audit/CI/Web children | Exact input hashes, actual SDK/Host/runtime output, rejection of missing/wrong root, unchanged host hashes |
| UG02 | Upgrade package metadata and locks on isolated branch | Only four ArchSift library lock nodes change; other tools/dependencies, assertions, policies and case identities unchanged |
| UG03 | Machine qualification and package rollback rehearsal | New cold full 493-case gate, fresh separate official audit, old complete combination 493-case replay, then new combination restored and revalidated |
| UG04 | Restore existing runner and formal IFX delivery | Existing labels/required checks unchanged; same-run SHA/attempt/artifact identity and full evidence verified for PR and actual main |
| UG05 | Workbench upgrade/rollback rehearsal then live upgrade | Real copied state and exact public Setup/ZIP; plan/apply/smoke/inspect, 0.8 rollback, scripts/profile restoration and evidence protection |
| UG06 | Current IFX Web/CLI parity and safe lifecycle | Actual current-source 493-case Web pass, equal typed identities/outcomes, reports/history, safe shutdown, runtime and host equality |
| UG07 | Prepare final independent human review | Both updated outer entries point to 0.10.0/current IFX; old profiles/reports retained; truthful machine record and unanswered checklist |

## Rollback boundaries

Daily rollback restores the complete pre-upgrade `0.10.0-cl03.1` combination, its exact locks/approvals/source/workflow inputs and current 493-case policy scope. Rehearse on a separate owned checkout; do not downgrade policies or reset unrelated changes. An actual post-merge rollback uses a reviewable inverse PR and fresh required CI.

Workbench rollback uses the successful active upgrade receipt and matching selection/state hashes, preserving every old version and report. Outer scripts and profile/selection changes have separate backups and must be reversed separately. Later user edits cause Setup to reject; never force replacement. New daily-test state/configuration is external and does not rewrite historical source-bound evidence.

Evidence root: `D:/ArchSift-lab/runs/ifx-stable-upgrade-20261023-01`. New package source: `D:/ArchSift-lab/packages/ifx-archsift-0.10.0-20261023-01`. Original packages, versions and failed attempts remain retained. Every real gate is reviewed and run sequentially; host drift stops work without automatic repair.

## Execution checkpoint (2026-10-23)

UG00–UG06 passed, including actual-main IFX CI at `30cca604`, current-source live CLI/Web 493-case parity, independent 105-project/CLI audit, complete daily rollback/forward validation, copied Workbench receipt and outer-profile rollback/forward rehearsal, then actual live Setup/cutover and both outer-entry Safe shutdown checks. Runtime selection is child-only SDK 10.0.303/Core+ASP.NET+host 10.0.13. Old payloads/configuration/packages/reports and failed attempts remain retained. Existing runner 22 is restored and online/idle; host hashes and policy/protection scope are unchanged.

The outer helper's output forwarding and Git stdin isolation fixes are qualified without changing released binaries. The final installed-helper full parity attempt is `live-web-parity-05`. [Machine acceptance](../acceptance/20261023-ifx-stable-upgrade.md) and its JSON bind exact source and receipts. UG07's [independent human checklist](../acceptance/20261023-ifx-stable-upgrade-human-checklist.md) is ready with every user answer pending. Do not mark overall human acceptance complete from these machine checks. ArchSift documentation delivery uses its own normal PR/main CI; the release tag/assets remain immutable.

## Final closure (2026-10-23)

The user completed both independent entrance reviews and accepted expected behavior with usability 4/5, no defects and no untested items. Launcher and separate Workbench closeouts at 04:00:39Z and 04:09:56Z each show 0.10.0, runtime 10.0.13, exit 0 and equal before/after host hashes; no installed CLI/Web remained. [Final acceptance](../acceptance/20261023-ifx-stable-upgrade.md) completes UG07. Earlier pending checkpoint text above retains its historical timing. Old versions/configuration/evidence remain protected. [NX01/NX02](20261023-workbench-next-improvements.md) record the two next-iteration suggestions only; no implementation or new release is started.
