# ArchSift W01 基础项目验收

日期：2026-10-06（Australia/Sydney）

结论：W01 COMPLETE；W02 NOT STARTED。Windows x64、SDK 10.0.303、net10.0、Debug 下完成首次完整离线恢复、锁定恢复、编译和基础测试。未将此结果当作六类分析规则或 0.1.0 产品验收。

W00 本地提交为 `5c378153abde38540dd81184706f92306091c53c`，人类主作者 Xiaolong Feng 保留，消息末尾附 `Co-Authored-By: Codex <noreply@openai.com>`。该提交未推送。本轮 W01 修改留在工作区；用户“提交，然后开始W01”的提交动作针对当时 W00 交付。

## 本轮交付

五个产品项目、三个测试项目与 `ArchSift.slnx` 已建立。global.json、Directory.Build.props、Directory.Packages.props、NuGet.Config 明确 TFM/版本/编译警告/包源，八份 lock 文件固定真实直接和传递依赖。直接包使用精确范围，Core/Contracts 无第三方包；独立 ArchUnit 层引用既定 0.13.4 引擎，入口负责组合。

CLI 提供 help/version 并对未实现命令返回 2；Web 基础入口仅支持 version，其余返回 2，不启动服务。中文 stdout/stderr 显式使用 UTF-8。共享产品版本与引擎 package/assembly 身份分别表示，未执行程序集架构分析。

新增开发检查脚本采用子进程 ArgumentList、独立 CLI home、paired global-tools opt-out、离线本地 feed、隔离开发包缓存和环境/Profile 哈希对比；默认 locked restore。根 `.gitignore` 排除自身 bin/obj、开发 artifacts，`.gitattributes` 明确文本 LF，避免后续 Git checkout 改变文档字节。

[W01 依赖清单](../dependencies/w01/package-inventory.json) 包含 19 个实际 package/version 记录，所有 lock contentHash 与新缓存 metadata 匹配；nuspec 及新增测试工具许可证已归档。W00 的候选包清单保持历史用途，实际 lock 未解析 System.Collections.Immutable。完整许可来源与差异见 [依赖说明](../dependencies/w01/README.md)。本轮没有复制或改写 Guard 实现，迁移清单仍为 `migrationExecuted=false`。

## 最终验证

最终运行 ID：`b5cc04a62542430da23d92c053397edc`。

| 检查 | 真实结果 |
| --- | --- |
| SDK 解析 | global.json 10.0.100/latestFeature → 10.0.303 |
| 首次离线 restore | 八个项目成功，生成 lock；只用显式本地 cache feed |
| locked restore | Bootstrap 同轮第二次和最终修复后的 locked restore 均成功 |
| Debug build | 八个项目通过，0 warning / 0 error |
| UnitTests | 2/2；共享产品版本、引擎包版本与程序集版本身份 |
| IntegrationTests | 5/5；CLI/Web version、CLI help、未实现命令配置错误、Web 无服务时准确拒绝 |
| ArchitectureTests | 2/2；全部产品项目引用方向、编译 Core/Contracts 无入口/Guard/ArchUnit 依赖 |
| 主机哈希 | SDK、restore、build、test 每步及最终 User/Machine 环境、Process PATH、四个 Profiles 均一致 |

归档：[运行 checks](w01/checks.json)、[主机 baseline](w01/host-baseline.json)、[最终对比](w01/host-final.json)、[build 原始输出](w01/build.stdout.log)、[test 原始输出](w01/test.stdout.log)、[测试结果与原始 TRX 哈希](w01/test-summary.json)。开发原始 TRX/日志保留在本机 `artifacts/development/<run-id>`，未跟踪；文档中的文本归档统一 LF。

## 发现并修正的问题

首轮沙箱 restore 在 SDK workload 校验时退出，环境哈希未变；正常审批后的执行可继续。未执行提示中的 workload update。随后修正 NuGet XML 注释中不允许的双连字符。

第一次编译成功后的测试暴露两个实际问题：中文重定向输出编码损失，以及把 NuGet 版本当成 DLL 版本。UTF-8 输出已修正；本机 ArchUnitNET DLL 自报 AssemblyVersion 0.13.0.0 / InformationalVersion 1.0.0，所以适配层从中央版本生成 package-version metadata，另保留 loaded assembly version。第一次修复只更换信息版本仍未满足身份要求，其失败也保留，最终修复后 9/9 全部通过。

失败运行 `abd0849b9aa54549ab61a0296fe7289c`、`35fac73db8de416faa10846fdb9cdcfe`、`a2921f5349aa4f03838e63db7db936fa`、`7abc54570cd540f78852dfb812838182` 的开发证据仍保留；各次最终 host-state-equal 均为 true。W00 manifest 是 W00 提交时的历史快照，计划与 README 的 W01 改动不回写进该历史清单；新阶段交付快照独立记录。

## 验收边界与下一步

本轮验证的是开发基础及真实编译依赖方向。未运行 Guard/IFX 集成、目标源码分析、六类规则、真实 ArchUnitNET 正负规则例、报告生成或 loopback UI。安全检查证明本轮主机状态不变，不等于 A09 所有边界与取消/构建失败 fixtures 已通过。

NuGetAudit=false 是离线配置，网络漏洞检查未运行。新项目没有被发布，公开许可证尚未选定；所有第三方许可继续按来源保留。W02 从规则/config/report/assembly-manifest 契约、JSON Schema draft-07 校验、六类模板和 CLI rules 入口开始，加入外部 schema 依赖前记录理由、版本、许可和 lock 变更。
