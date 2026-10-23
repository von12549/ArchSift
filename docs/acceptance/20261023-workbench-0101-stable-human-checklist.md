# ArchSift Workbench 0.10.1 independent human review

Status: **ACCEPTED BY USER; THREE CLOSEOUTS VERIFIED**. [Recorded user response](20261023-workbench-0101-stable-human-acceptance.md) and [publication](../releases/0.10.1-publication.md) supersede the earlier pending status. The original unanswered checklist remains retained externally. Exact machine-qualified source: `66cf7c4d4ca2a4a0b492fad3f9edb87c1e4e3b4c`. [Machine qualification](20261023-workbench-0101-stable-machine-qualification.md). Official Windows ZIP SHA-256: `47440fe808b6eaadd32932d0c1023270245f0bcfacff8e842189780407d3d94f`; Setup SHA-256: `ec5e75c274e7b1f27ce9a609d9885a16c732d2bb81e7972ab2ae9f9de77afcd7`; CLI nupkg SHA-256: `a586dc8208b105ea13cb98d1022078b7c42e4bdb2464a5d3d4c696683ed86b23`.

The three prepared review entrances use only isolated files under `D:/ArchSift-lab/runs/workbench-0101-20261023-01`. Their target is a separate copy at `D:/ArchSift-lab/fixtures/workbench-0101-ifx-human-01/IFX-New`, not the live IFX checkout or sealed machine snapshot. Two different JSON contracts are needed. The **Workbench startup/profile configuration** is `D:/ArchSift-lab/runs/workbench-0101-20261023-01/stable-human-review-prep-01/review-ifx-config.json` (`schemaVersion: 1`, `target.root`). The **Architecture tests → Daily-test configuration file → Load configuration and inventory** input is `D:/ArchSift-lab/runs/workbench-0101-20261023-01/stable-human-review-prep-01/review-ifx-test-config.json` (`schemaVersion: 2`, `targetRoot`, `testProject`, `requiredCases`). Use the second file in that input; passing the first file produces the reported unmapped `target` error. Both point to the separate human IFX copy. [Preparation correction and evidence](20261023-workbench-0101-human-config-correction.md). Use a PowerShell terminal without Profiles and run **one entrance at a time**; each command waits for Safe shutdown and saves a token-free exit/host record:

```powershell
pwsh -NoProfile -File D:/ArchSift-lab/runs/workbench-0101-20261023-01/Start-StableHumanReview.ps1 -Mode NuGetUI
pwsh -NoProfile -File D:/ArchSift-lab/runs/workbench-0101-20261023-01/Start-StableHumanReview.ps1 -Mode PortableLauncher
pwsh -NoProfile -File D:/ArchSift-lab/runs/workbench-0101-20261023-01/Start-StableHumanReview.ps1 -Mode ManagedLauncher
```

1. In direct NuGet UI, confirm displayed version 0.10.1 and the copied IFX target. Open **Architecture tests**, enter the full `review-ifx-test-config.json` path above, click **Load configuration and inventory**, and confirm 92 C# cases plus 13 JSON rulesets/401 enabled JSON cases. JSON groups should start collapsed, disclose paths/counts and expand in original order. Check per-file copy/edit access, Expand all/Collapse all, keyboard Tab/Enter/Space, visible focus, English/Chinese and narrow layout. Folding must not select or skip cases. Review full/subset, Results/History and JSON/HTML download if you run a new copied-fixture session; keep runs sequential.
2. In portable Launcher, confirm the explicit external state marker `workbench-state.json` and portable mode/version. The tool/Web payload must be adjacent to the NuGet tool, while configuration and reports stay external. Load `review-ifx-test-config.json` under **Architecture tests**, review the same inventory and finish with **Safe shutdown**; wait for terminal return.
3. In managed Launcher, confirm selected version 0.10.1 and managed mode. Its initial synthetic target is expected. For the IFX inventory, first use the Launcher's **Register an existing JSON** form to register `review-ifx-config.json` as an external profile, select that profile, and open its workbench. Then load `review-ifx-test-config.json` under **Architecture tests**. The daily-test target must match the current Workbench target. Keep the managed default profile and live IFX entrance unchanged. Check version/selection diagnostics, profile clarity, history, and **Safe shutdown** with terminal return.
4. Inspect any behavior, layout, usability or accessibility concern yourself. Changes to copied review files are allowed for hands-on testing; do not edit the machine snapshot, original IFX checkout, approved feed or existing entrance. If a review session fails, retain its closeout record and describe the issue before any release.

| User-only answer | Response |
| --- | --- |
| Reviewer and review date | User in this conversation; exact review timestamp and personal identity not supplied |
| Direct NuGet UI behavior and Safe shutdown | User reports normal version, target, inventory and closing flow; exit 0, host equal |
| Portable Launcher behavior and Safe shutdown | User reports normal version, target, inventory and closing flow; exit 0, host equal |
| Managed Launcher behavior and Safe shutdown | User reports normal version, target, inventory and closing flow; exit 0, host equal |
| JSON folding/copy/edit, keyboard, locale and layout | User reports completion with no untested mandatory items; no separate per-control narrative supplied |
| Usability score / 5 | 4/5 |
| Defects and severity | None reported |
| Untested mandatory items | None reported |
| Final exact-stable acceptance | Accepted by user; machine closeouts verified |

The proposed acceptance threshold is at least 4/5, no untested mandatory item and no unresolved blocking defect; any nonblocking limitation needs explicit user acceptance. All three Safe shutdown closeouts were verified after the user response. Publication and anonymous download/usage verification are complete for the accepted stable payload.
