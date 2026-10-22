# ArchSift

IFX当前接入（2026-10-22）：**`0.10.0-cl03.1`／493项门禁＝92项C#＋401项JSON**，404个政策责任来源；B01–B05全部正式启用（含Registry4），A179与B07五条保持暂缓，A三项由C#承担。使用[可移植接入说明](samples/portable-ifx/README.md)及[包源维护说明](docs/distribution/20261021-cl03-candidate-maintenance.md)。下文0.7.0公开发行及其验收描述保留该产品线的历史范围，不作为当前IFX包版本或门禁结果。

ArchSift is an independent local .NET architecture and dependency analyzer. **0.7.0 is published and independently verified.** It provides a profile launcher, chain progress, optional bounded parallel validation and a separate portable Windows setup with install/adopt/upgrade/rollback/recovery. The workbench retains the reusable ruleset library, independent chains, English/Chinese UI, eight rule templates, real ArchUnitNET checks, JSON/HTML/SARIF reports and Git comparison.

0.7.0 adds reviewed external build evidence with per-project dependency contexts, saved Ruleset/Chain copies, result sorting and launcher shutdown feedback. Its [final acceptance](docs/acceptance/20261012-0.7-final-acceptance.md) includes 105 source-bound IFX project contexts, 406 B rules and 182 A rules passing, and independent human Workbench/Launcher acceptance with usability 5/5. Declaration-model limitations remain; the overall IFX result is still partial/compliant/exit 4.

Obtain the stable Windows x64 package and separate Setup from the [v0.7.0 Release](https://github.com/von12549/ArchSift/releases/tag/v0.7.0), then verify all assets against the [0.7.0 publication record](docs/releases/0.7.0-publication.md), SHA256SUMS.txt and acceptance.json. The public ZIP and Setup passed 17 and 13 native checks. Earlier release records and guides remain historical. Acceptance does not adopt the tested IFX candidate policy or claim full architecture compliance.

The tool runs on .NET 10 and targets SDK-style C# net8/net9/net10. Windows x64 is the self-contained distribution; running that package does not require an installed .NET SDK/runtime or Guard. Linux CLI is tested separately. Start with [Windows installation and environment requirements](docs/installation.md) and [portable setup](docs/setup.md). [0.5.0 execution and release gates](docs/plans/20261010-archsift-0.5.0-plan.md) record progress. ArchSift uses the [MIT license](LICENSE), with separate third-party notices.

## Development

IFX 已通过 CL04 正式采用固定 `0.10.0-cl03.1` 可移植测试组合，[CL04实际 main 验收](https://github.com/von12549/IFX-New/actions/runs/39543191970)的基点为 `a2adb87`。后续说明由 PR #15 交付到历史基线 `f58205b`，见[说明交付验收](https://github.com/von12549/IFX-New/actions/runs/39550973000)；政策扩批已由 PR #25 完成至 `b591b9f`，493/493、零跳过和独立审计105/105见[完成记录](https://github.com/von12549/IFX-New/pull/25)及[实际main正常CI](https://github.com/von12549/IFX-New/actions/runs/39733781519)。当前使用及边界见[IFX已验基线说明](https://github.com/von12549/IFX-New/blob/b591b9f2ffca5d0bbbfb1f9352a56870fadf927c/docs/architecture-portable.md)。CL02责任投影修正已随该版本在 IFX 生效；匹配 Web ZIP 仍为本地已验候选，未由 IFX 消费或公开发布。

ArchSift 产品源码最小归档 **43 文件**已通过[PR #9](https://github.com/von12549/ArchSift/pull/9)正式合并至 ArchSift main `3f61be0`，实际 main 正常 CI 验收及本地固定更新已完成，见[源码归档合并说明](https://github.com/von12549/ArchSift/pull/9)。其余未提交文档、模板、历史及操作材料均保留，未包含在这43文件中；这两份说明的交付范围以对应PR变更清单为准，未纳入提交的材料继续保持本地状态。源码归档、IFX现役、既有包及Web候选分别记账，不因此新增包发布或真实IFX运行，也不声明旧包的精确编译器输入闭包或可重现构建。这与已发布的通用0.7.0产品分开记账。

Requires .NET 10 SDK, PowerShell 7, Git, and a complete explicit local NuGet feed. `global.json` starts at SDK 10.0.100 with latestFeature roll-forward; Windows development checks use SDK 10.0.303. Multi-framework build fixtures also need their targeting packs and supported SDKs.

```powershell
pwsh -NoProfile -File scripts/Invoke-DevelopmentChecks.ps1 -VerifyContracts
node scripts/Test-WorkbenchLifecycle.mjs
```

Checks restore in locked mode from the explicit offline feed (default user NuGet cache), build, and run unit/integration/architecture tests. Missing packages fail; the script does not silently enable network access. Use `-LocalFeed` for another offline feed. `-InitializeLocks` is for an intentional dependency update only.

Each dotnet child receives isolated `DOTNET_CLI_HOME` and `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`. User/Machine environment, process PATH and four PowerShell Profiles are compared using hashes. New logs and TRX files go to external `D:/ArchSift-lab/runs` by default; development bin/obj and the isolated package cache are ignored in this repository. Real IFX integration remains operator-driven under the acceptance plan.

## Use and boundaries

IFX日常入口为目标仓库 `eng/Test-Architecture.ps1`，使用批准的251包离线源与源外状态；三项必需检查为 `IFX Windows architecture`、`IFX dependency audit`、`IFX architecture acceptance`。`test-gate`要求完整案例通过；下面独立分析CLI的退出码描述不适用于该门禁。外层Launcher／Workbench仍为独立0.8.0安装。原InProject已[正式废止](docs/decisions/20261022-inproject-retirement.md)，保留历史验收事实及归档。

CLI: `analyze`, legacy composed `verify`, `chain verify`, `changes`, `rules validate/render/draft`, and `ui --config`. The UI binds to a random loopback port and requires a session token. Closing the browser does not stop the server; use Safe shutdown. 0.5.0 forwards Web stdout/stderr and binds Web to the parent CLI lifetime; real IFX terminal acceptance and public-package smoke passed. Older users should consult the [0.4.0 diagnostic and temporary Web entry](docs/installation-0.4.md#known-040-cli-ui-launch-issue).

Rules, state and reports live outside the analyzed target. Violations return exit 0 when the run is complete and conclusive; partial/inconclusive/configuration/execution/cancellation statuses remain distinct. A `partial / compliant` result means checked policies passed with limited coverage. It never certifies full source or IFX protection. Source-bound assemblies are required for compiled-code compliance.

Projects reference Contracts and Core in one direction: Core owns analysis/services; ArchUnit adapts the pinned engine; CLI/Web compose those services. User-authored rule descriptions and exception reasons are preserved. UI locale does not change report bytes.

## Guidance and evidence

- [Windows installation and environment requirements](docs/installation.md)
- [0.7.0 publication and verified asset hashes](docs/releases/0.7.0-publication.md)
- [Installer and upgrade design](docs/plans/20261010-installation-upgrade-design.md)
- [CLI and configuration](docs/cli.md)
- [Rules and schema boundaries](docs/rules.md)
- [Discovery and build inputs](docs/build-inputs.md)
- [Changes comparison](docs/changes.md)
- [Report semantics](docs/reports.md)
- [0.4.0 plan](docs/plans/20261009-archsift-0.4.0-improvement-plan.md)
- [0.4.0 contracts and UX](docs/decisions/20261009-0.4-contracts-ux.md)
- [Main implementation record](docs/plans/20261006-archsift-implementation-plan.md)
- [Migration permissions](docs/migration/source-inventory.json) and [third-party notices](docs/migration/third-party-notices.md)
- [Project instructions](AGENTS.md)

Historical 0.1/0.2/0.3 acceptance evidence remains unchanged. The earlier general-product roadmap deferred NuGet tool distribution, transitive package/version rules, Linux Web UI, fail-on, required CI checks and IFX C/D/E coverage; its [CI samples](samples/ci) remain historical opt-in reporting examples. The separately adopted IFX portable integration now uses the pinned local CLI package and three required CI checks described above; this does not announce a new public release or completion of the other deferred capabilities.
