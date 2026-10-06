# ArchSift W00 完成记录

日期：2026-10-06（Australia/Sydney）

结论：W00 COMPLETE。正式计划及 Git 基准、迁移来源、用户权属授权、逐文件处理选择、第三方许可记录和项目指令均已准备完成。W01 可以作为下一次实施任务开始，当前仍为 NOT STARTED。

## W00 完成证据

| 条件 | 证据与结果 |
| --- | --- |
| 目标计划与既有仓库 | 正式计划在本 Project 持续维护；HEAD `a5bc15cca1211b5098edc762440c595c996064f8`，main 跟踪 origin/main；保留 .git/README，已跟踪文件无差异 |
| 来源基准 | Guard HEAD `40539d4610889483b4aed4637f8f3457098b1ff0`、IFX HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`；ordinary status 均为空；本轮再次只读核对 |
| 来源权利 | 用户确认 Guard 完全归其所有并确认复用、分发权利；授权原文与范围写入 `GUARD-OWNER-20261006` |
| 文件级来源 | [source-inventory.json](../migration/source-inventory.json)：14 个源码、测试、fixture 元数据/输入记录，含 commit、hash、处理范围、迁移方式、目标目录与未来验证入口 |
| fixture 选择 | 四个模块 fixture JSON 为旧 12-claim 元数据，标记 reference-only；三个 Guard 测试用于重建测试设计；选定中央 props fixture 的四个受跟踪输入文件，排除 bin/obj 与生成文件 |
| 第三方记录 | [第三方 notices](../migration/third-party-notices.md) 与 [机器清单](../migration/third-party-inventory.json)：七个候选包、完整许可文本、ArchUnitNET NOTICE、Microsoft THIRD-PARTY-NOTICES、nuspec 来源快照 |
| 缓存字节核对 | 7 个本地 nupkg SHA-512 与元数据一致；33 个 DLL 与各自 nupkg 内对应字节一致；这不替代新项目 restore/build 或发布者真实性验证 |
| 项目指令 | [AGENTS.md](../../AGENTS.md)：范围、依赖方向、目标只读、子进程环境、安全快照、外置输出、人工 IFX/Guard 集成和验收语义 |
| 保留历史事实 | 原 readiness 和 Draft 未修改；接手复核报告保留为历史并追加关闭记录；P05 保持总体 FAIL 与安全/清理结论 |

## W00 的迁移边界

`source-inventory.json` 当前是准备清单，各项 `migrationExecuted=false`。正式代码迁移时需再次复核源 commit/hash，记录实际目标文件与新测试。当前只归档来源元数据及第三方许可文本，没有复制 Guard 产品源码、fixture 源输入或执行旧 Guard 测试。

W00 许可清单已完成，后续包解析与实际分发检查属于相应工作包：W01 冻结完整 lock 并记录新增开发依赖；W08 核对实际 ZIP 中组件与 .NET 自包含运行时 notices。它们不是本轮虚构通过的结果，也不重新打开 Q01–Q14。

## W01 入口

下一步创建独立 .NET 10 solution 与 Contracts/Core/ArchUnit/CLI/Web/测试项目、独立 global.json（10.0.100/latestFeature）、Directory.* 和显式 NuGet.Config，固定依赖与 lock；随后按 AGENTS 的环境基线和子进程隔离规则进行首次 restore/build 与依赖方向测试。

实际 SDK、NuGet 选组、完整离线闭包、编译与测试尚未验证。本轮没有启动 dotnet、运行 Guard/IFX 产品操作、创建 D:\ArchSift-lab、提交、推送或发布。外置目录、真实集成与网络 restore 按实际任务正常审批处理。

## 交付核验

机器 JSON 可解析，14 个来源记录 ID 唯一且哈希格式有效，来源文件仍与记录匹配；7 个候选包检查均无 DLL 差异；内部文件链接及许可路径存在。正式计划同步登记 W00 COMPLETE、W01 NOT STARTED。文档产物的 SHA-256 归档在 [交付清单](20261006-w00-artifact-manifest.json)，供下一轮验证变更。
