# IFX 0.10.0 independent human acceptance checklist

Status: PENDING USER REVIEW. Do not infer answers from machine checks or the separate release review.

Machine baseline: IFX `30cca60467e737ba794e98cde608c713e7f17ff9`, ArchSift Workbench 0.10.0, SDK 10.0.303/runtime 10.0.13, Release/net10.0; 493 = 92 C# + 401 JSON. All machine test sessions were safely closed before handoff. The restored runner remains online.

1. Open Launcher: `pwsh -NoProfile -File D:/IFX-10-Root/launcher.ps1`. Confirm 0.10.0, select **IFX 0.10.0**, confirm current `D:/IFX-10-Root/IFX-New`, Release/net10.0, and confirm both old profiles remain listed. Open the selected Workbench.
2. In **Architecture tests**, enter `D:/ArchSift-lab/evidence/ifx-archsift-0.10.0/test-gate.json` and load the inventory. Confirm 92 required C# cases, 401 JSON cases and the unchanged responsibility/deferred boundaries. The initial ordinary Target configuration has no selected analysis rules; it is not the daily gate configuration.
3. Inspect the latest complete result through **View test runs in History**, then **View in Results**. The final machine parity report ID is `a7bb7ae30631414dbdb7f7ff98d20b87`: 493 passed, zero failed/skipped, allow/0. Download JSON/HTML and confirm they describe the selected run. History freshness `unknown` is intentional and does not certify current source. A new complete run is optional if you want hands-on execution; keep runs sequential.
4. Review layout, navigation, English/Chinese locale, clarity and usability. Do not copy/edit source policy files or run unrelated target application entry points during this review.
5. Use Workbench **Safe shutdown**, wait for Launcher to show **Workbench closed**, then **Close launcher** and confirm. Wait for terminal return.
6. Open the direct entry separately: `pwsh -NoProfile -File D:/IFX-10-Root/workbench.ps1`. Open its displayed loopback session address, confirm version/current target, load the same daily configuration and inspect results/history. Finish with **Safe shutdown** and wait for terminal return.

| User response | Answer |
| --- | --- |
| Reviewer / date | Pending |
| Behavior matches expectations | Pending |
| Usability score / 5 | Pending |
| Defects and severity | Pending |
| Untested items | Pending |
| Launcher and its child safely shut down / terminal returned | Pending |
| Direct Workbench safely shut down / terminal returned | Pending |
| Final IFX adoption accepted | Pending |

Keep original versions, packages, profiles and all evidence. If a later user edit exists, stop before rollback rather than overwriting it. [Machine acceptance and rollback boundaries](20261023-ifx-stable-upgrade.md).
