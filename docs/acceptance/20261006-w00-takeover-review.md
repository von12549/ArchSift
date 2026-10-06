# ArchSift W00 接手复核与交接报告

日期：2026-10-06（Australia/Sydney）

后续状态：本文件保留首次接手复核的历史结论。用户随后确认 Guard 所有权和资产复用/分发权利；剩余 W00 项目已关闭，当前结果见 [W00 完成记录](20261006-w00-completion.md)。以下“尚未完成/未校验”描述对应第一次复核时点。

结论：**READY TO COMPLETE W00**。正式计划、目标仓库、开发工具链、Guard/IFX 参考基线和已缓存的 ArchUnitNET 依赖闭包均可读取并与交接基准一致。W00 尚未完成，W01 未开始；直接复制或改编 Guard 源码及 fixture 仍受来源权利与许可记录门槛约束。

本报告只完成接手复核和实施入口整理。没有 restore、build、测试、Guard 安装、IFX 现场集成、产品代码创建、源码复制、提交、推送、远程变更或发布。

## 计划与仓库身份

三份导入文件在接手编辑前与交接 SHA-256 完全一致：

| 文件 | 导入基准 SHA-256 | 结果 |
| --- | --- | --- |
| `docs/plans/20261006-archsift-implementation-plan.md` | `4760fb91708c5931c21f0c51638ad47768d584fb5017dff75c1b10f65acd9090` | 匹配；本次随后只增加 W00 接手状态记录，因此当前哈希会变化 |
| `docs/acceptance/20261006-implementation-readiness.md` | `00ad1874a792a1877768b709bef6b62d5357a3fd939ea7122a913c6d8825f425` | 匹配，未修改 |
| `docs/plans/20261006-dotnet-architecture-analyzer-draft.md` | `c70ad19f6d2beab6a5d765c1e800418b40e14af4f2e255a6c201906f7df75c31` | 匹配，继续作为归档历史，未修改 |

目标仓库当前为 `main`，跟踪 `origin/main`；`HEAD` 与本地 `origin/main` 都是 `a5bc15cca1211b5098edc762440c595c996064f8`。远程为 `https://github.com/von12549/ArchSift.git`。已跟踪文件没有差异；接手编辑前只有上述三份 `docs/` 文件未跟踪。根 `README.md` 内容仍为 `# ArchSift`，SHA-256 为 `9bdc57fe03036665439f35cdd2032a180c76c8b23ae3914f546b4716cfc142cd`。

没有发现 `D:\AGENTS.md`、仓库内 `AGENTS.md`/`CLAUDE.md`，也没有发现 `D:\` 或仓库根的 `global.json`、`NuGet.Config`、`Directory.Build.props`、`Directory.Build.targets`、`Directory.Packages.props`。因此没有发现需合并的现有项目指令或指定父级构建配置；W01 仍应由 ArchSift 自己显式创建这些配置。

## 工具链与包访问条件

只读复核确认 PowerShell 7.6.6、Git 2.49.0.windows.1；已安装 .NET SDK 10.0.301、10.0.303，另有 9.0.314 和 3.1.426。NETCore/ASP.NET Core 8、9、10 runtime 及 `Microsoft.NETCore.App.Ref` 8.0.27/28、9.0.16/17、10.0.9/11 可见。

Guard 的 `build/global.json` 仍指定 SDK 10.0.100、`rollForward: latestFeature`、`allowPrerelease: false`。这只是 ArchSift W01 的已批准基准；必须在 ArchSift 自己的 `global.json` 创建后重新记录实际解析到的 SDK。

| Package | 版本 | 缓存 | `.nupkg.sha512` 与 Guard lock | nuspec 许可表达式 |
| --- | --- | --- | --- | --- |
| TngTech.ArchUnitNET | 0.13.4 | 存在，2 个 DLL | 匹配 | Apache-2.0 |
| CycleDetection | 2.0.0 | 存在，5 个 DLL | 匹配 | MIT |
| JetBrains.Annotations | 2026.2.0 | 存在，4 个 DLL | 匹配 | MIT |
| Mono.Cecil | 0.11.6 | 存在，8 个 DLL | 匹配 | MIT |
| Newtonsoft.Json | 13.0.4 | 存在，8 个 DLL | 匹配 | MIT |

这些结果证明已知闭包的缓存入口与 Guard lock 元数据可访问，不证明 ArchSift 的完整离线 restore 或 build。没有用权威清单逐个校验缓存 DLL 字节；除 Newtonsoft.Json 缓存中可见 `LICENSE.md` 外，其余四个缓存目录没有发现 license/notice 命名文件。W00/W01 仍须从权威来源固定实际许可文本和发行 notice，而不能只复制 nuspec 表达式。

## Guard、fixture 与 IFX 基线

经批准的跨目录只读 Git 检查确认：

| 对象 | 当前状态 |
| --- | --- |
| Guard | `C:\Users\von12\OneDrive\Desktop\Guard`；`main`；HEAD `40539d4610889483b4aed4637f8f3457098b1ff0`；ordinary status 0；origin `https://github.com/von12549/Guard.git` |
| IFX | `D:\IFX-10-Root\IFX-New`；`main`；HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`；ordinary status 0；`IFX.sln` 存在 |

Guard 候选迁移源当前哈希为：

| 来源 | 字节 | SHA-256 |
| --- | ---: | --- |
| `modules/architecture-conformance/adapter.ps1` | 46,407 | `d92df650fbda0a5f51a3dcde3e83de78ed29b218b44e56b90c64da2e9dc2781b` |
| `core/host/V4.Guards.Host/ProfileRuntime.cs` | 77,077 | `6b89ee26fb9cdd0c4d6b20c0838949a1916a08412efeece343ea51655b5962ca` |
| `modules/build-evidence-provider/adapter.ps1` | 10,067 | `12ec6b707043477fc0ef0e86cc62ab1671f8f33f9b028ad8958cece1df45c92e` |

`modules/architecture-conformance/fixtures` 当前只有四个文件：

| 相对路径 | SHA-256 |
| --- | --- |
| `clean/fixture.json` | `f52e13f73fa16a83fc866bb2e10dc7bf962b7d228d4ecf93239ec5bc5b8b4044` |
| `manifest.json` | `94b9339432e7b08e213853f1dbe49bfd601f38a0139132903a9b2eaba39573a2` |
| `missing/fixture.json` | `788cf1f0bee722f867e0ddbcf198c60f8e0dbe21b7868e340a15fe04576a4b30` |
| `violating/fixture.json` | `c0e2c9d3d77dc4045207fb5651b644adb847f84f49bdc2d30e40b9ffff086954` |

这些是只读候选清单，不是迁移批准，也没有复制到 ArchSift。

## P05 证据边界

P05 最终验证、manifest 与安全清理审计均可读，当前 SHA-256 分别为：

- `p05-20261006-final-verification.json`：`07067b32151513e14ebc14975c44b95f1b98ffe26f81794f6f275d7beceaaf59`
- `p05-20261006/final-manifest.json`：`007190e56b555daa67802ff4d5db7da106e806678ee33c461e4505609c1066a1`
- `p05-20261006/cleanup/h4-final-safety-and-cleanup-audit.json`：`bce2afba069e7c08d8224f03e3526c0580ee1a2269c7ceb24b8674b2245ac21d`

最终 manifest 的事实是 `TechnicalMainPathPassed: true`，但 `OverallTestStatus: FAIL`、`FailurePathPassed: false`、`HumanUsabilityPassed: false`。失败原因是过期 proof UI 保留先前 profile configure / exit 0 成功结果，以及操作者信心 3/5、需要 L3 辅助。H4 同时证明最终 User/Machine environment、Process PATH、Profiles 未变化，原 Guard/IFX checkout 保留，活动测试路径已清理。

因此，交接中的“真实 8/8”只保留为 Guard 前置技术主路径历史；它不是 ArchSift 的项目图、六类规则、ArchUnitNET 类型依赖、CLI/UI 一致性或跨机器能力验收。P05 的失败路径和安全清理证据继续保留，不重写为成功。

## W00 尚未关闭的来源与许可工作

Guard 根目录及全仓 license-named 文件扫描没有发现 LICENSE/COPYING/NOTICE。个人远程地址与提交署名是来源线索，不构成可复制、改编或再分发的许可结论。直接复用前必须完成以下工作：

1. 创建 `docs/migration/source-inventory.json`，逐项记录 source repository、commit、relative path、SHA-256、选取范围、迁移方式（复制/改写/仅参考）、目标路径、验证入口和责任人确认。
2. 由有权人员明确记录 Guard 代码与 fixture 可供 ArchSift 使用和再分发的依据；若不能形成该记录，则不得复制，改用从已批准需求和独立测试重写的路径，并在 inventory 中标为 `reference-only`。
3. 对四个 fixture 逐项判断是否迁移；只有选中的文件进入正式来源清单，未选中项不因同目录存在而自动导入。
4. 创建 `docs/migration/third-party-notices.md`，固定 ArchUnitNET 及其闭包的权威许可文本、copyright/notice、实际打包范围和归档来源；Apache-2.0 与 MIT 的表达式本身不等于发行清单完成。
5. 核对候选源中是否包含来自第三方的片段、生成内容或嵌入资源；把这项结论写到文件级记录，不能仅用 Guard 仓库级声明覆盖。
6. 创建项目 `AGENTS.md`，写明目标源码只读、外置报告/构建快照、禁止持久环境/Profile/PATH 修改、隔离 `DOTNET_CLI_HOME` 时同时设置 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`、以及 Guard/IFX 真实集成一次一块人工命令的规则。

上述 1–6 完成并复核后，W00 才可标记完成。来源/许可只阻止相应复制或改编；W00 的清单、决策记录、指令编写和纯新设计可以继续。

## 可直接继续的 W01 任务卡

W01 尚未获本次交接授权执行。W00 关闭后，下一次实现应按以下顺序开始：

1. 保留现有 `.git` 与 `README.md`，创建 ArchSift 自己的 `AGENTS.md`、`global.json`、`Directory.Build.props`、`Directory.Packages.props`、显式 `NuGet.Config`、solution 与约定项目骨架。
2. 固定所有直接/传递包版本及 lock 文件；选择 schema/test 依赖时记录版本、用途、许可和离线缓存状态。不要因 Guard 缓存存在就假设新闭包完整。
3. 建立 Contracts/Core/ArchUnit/CLI/Web 与测试项目的依赖方向检查；W01 只搭基础与安全回归，不提前声称六类规则或 ArchUnitNET 行为已实现。
4. 在隔离子进程中执行首次 restore/build；如设置 `DOTNET_CLI_HOME`，同时设置 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`。记录实际 SDK、NuGet sources、offline/network 行为和 lock 结果。
5. 执行并记录首次完整 restore/build/architecture test；失败即保留真实失败，不使用 Guard 的旧成功或包缓存命中替代。

## 当前真实阻碍与非阻碍

- **阻碍直接迁移：** Guard 复用权利/许可、文件级 provenance 和第三方 notices 尚未关闭。
- **阻碍宣称 W01 可构建：** 新项目尚不存在，完整 restore/build 和缓存 DLL 完整性均未验证。
- **不构成阻碍：** 计划身份、Git 仓库、remote 配置、.NET 10 SDK、8/9/10 reference packs、Guard/IFX 只读访问及已知 ArchUnitNET 缓存入口均已确认。
- **按需审批：** `D:\ArchSift-lab` 外置写入、Guard/IFX 真实集成、网络 restore、远程认证/推送/发布均应在实际任务中单独取得授权；本次只读成功不扩大授权。

## W00 后续关闭记录

用户明确声明“Guard项目所有权完全属于我，确认拥有资产复用和分发的权利”。逐文件来源/处理选择、第三方完整许可与 notices、项目 AGENTS.md 已创建。另识别 NuGet 传递候选 System.ValueTuple 与 System.Collections.Immutable，完成 7 个本地包及 33 个 DLL 的包内字节一致性核对。旧 fixture JSON 为覆盖声明，新的测试输入须从已选定 Guard 测试/中央 props fixture 选择性重建，按 ArchSift 新契约验收。W00 已完成，W01 尚未开始；首次复核中尚未关闭的迁移权利和文档工作不再是当前阻碍。

