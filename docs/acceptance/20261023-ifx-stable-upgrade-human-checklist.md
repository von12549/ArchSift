# IFX 0.10.0 independent human acceptance checklist

Status: PASSED / 2026-10-23 (Australia/Sydney). The user independently reviewed Launcher and its child, then separately ran `workbench.ps1`, confirmed the same outcome and Safe shutdown. This acceptance is separate from the earlier release review.

Machine baseline: IFX `30cca60467e737ba794e98cde608c713e7f17ff9`, ArchSift Workbench 0.10.0, SDK 10.0.303/runtime 10.0.13, Release/net10.0; 493 = 92 C# + 401 JSON. All machine test sessions were safely closed before handoff. The restored runner remains online.

1. Open Launcher: `pwsh -NoProfile -File D:/IFX-10-Root/launcher.ps1`. Confirm 0.10.0, select **IFX 0.10.0**, confirm current `D:/IFX-10-Root/IFX-New`, Release/net10.0, and confirm both old profiles remain listed. Open the selected Workbench.
2. In **Architecture tests**, enter `D:/ArchSift-lab/evidence/ifx-archsift-0.10.0/test-gate.json` and load the inventory. Confirm 92 required C# cases, 401 JSON cases and the unchanged responsibility/deferred boundaries. The initial ordinary Target configuration has no selected analysis rules; it is not the daily gate configuration.
3. Inspect the latest complete result through **View test runs in History**, then **View in Results**. The final machine parity report ID is `a7bb7ae30631414dbdb7f7ff98d20b87`: 493 passed, zero failed/skipped, allow/0. Download JSON/HTML and confirm they describe the selected run. History freshness `unknown` is intentional and does not certify current source. A new complete run is optional if you want hands-on execution; keep runs sequential.
4. Review layout, navigation, English/Chinese locale, clarity and usability. Do not copy/edit source policy files or run unrelated target application entry points during this review.
5. Use Workbench **Safe shutdown**, wait for Launcher to show **Workbench closed**, then **Close launcher** and confirm. Wait for terminal return.
6. Open the direct entry separately: `pwsh -NoProfile -File D:/IFX-10-Root/workbench.ps1`. Open its displayed loopback session address, confirm version/current target, load the same daily configuration and inspect results/history. Finish with **Safe shutdown** and wait for terminal return.

| User response | Answer |
| --- | --- |
| Reviewer / date | User, explicit confirmation in this task / 2026-10-23 |
| Behavior matches expectations | Yes, both entrances |
| Usability score / 5 | 4/5 |
| Defects and severity | None reported |
| Untested items | None reported |
| Launcher and its child safely shut down / terminal returned | User confirmed; entry closeout exit 0, host equality verified |
| Direct Workbench safely shut down / terminal returned | User confirmed; separate entry closeout exit 0, host equality verified |
| Final IFX adoption accepted | Yes |

Keep original versions, packages, profiles and all evidence. If a later user edit exists, stop before rollback rather than overwriting it. [Machine acceptance and rollback boundaries](20261023-ifx-stable-upgrade.md).

The original handoff instructions above remain for traceability. Both post-handoff closeout records are bound in the [acceptance JSON](20261023-ifx-stable-upgrade.json); no installed CLI/Web process remained. Two user suggestions are recorded in the [next-iteration plan](../plans/20261023-workbench-next-improvements.md), not implemented or reported as defects in this accepted scope.
