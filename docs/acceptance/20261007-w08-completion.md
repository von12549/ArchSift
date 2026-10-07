# W08 0.1.0 验收完成报告

日期：2026-10-07（Australia/Sydney）

结论：`W08 COMPLETE / PASS`。A01–A12 均有自动、真实 IFX、独立操作员和最终安全清理证据；W09/W10 的依赖已满足，但尚未开始。

## 冻结候选

- ZIP：`D:\ArchSift-lab\packages\w08-0488c7a30a8943c9b97ff3c61a2913bf\archsift-0.1.0-dev-win-x64.zip`
- SHA-256：`61d5e5df7e87858a0086691f7284925407977304d84d861c4fae93661435f23a`
- 身份：`0.1.0-dev`、`win-x64`、self-contained；manifest 581 个 payload 条目，解压后含 manifest 共 582 个文件。
- ZIP/解压文件集合、逐文件哈希、路径 containment、大小写重复和 reparse point 检查全部通过。

候选 manifest 中的 `w08Accepted=false` 是候选冻结和测试前写入的历史字段。验收后不改写已验证 ZIP，否则会破坏上述哈希及全部运行证据的同一性。最终接受状态由本报告、固定 ZIP SHA-256 和最终外部审计共同记录；这不是远程发布或将版本改成正式 `0.1.0`。

## 自动与性能证据

- 92/92 回归通过：70 unit、20 integration、2 architecture，覆盖 net8/net9/net10、六类规则、真实 ArchUnitNET、CLI/UI parity、失败/取消、根边界、输入身份、Worker 与主机安全。
- 冻结预算基于 100-project-chain；候选五次分析 108–243ms、墙钟 225–373ms、采样峰值约 27–32MiB，均在 analysis 700ms、CLI wall 1000ms、working-set 64MiB 阈值内。
- Windows x64 自包含 CLI、Worker 和 loopback UI 在空 child PATH、无 SDK 路径条件下启动；未把 isolated build 的 SDK prerequisites 误写成无需 SDK。

## 真实 IFX

- 声明发现：105/105 项目、346 条 resolved 项目引用、114 条直接包声明；unlisted/external/unresolved 为 0。
- existing/net10.0/Debug 模式，无 build/restore。105 条限制来自声明模型不能完整求值条件/属性表达式，准确报告为 `partial`，0 execution error。
- 精确规则稳定命中 `IFX.Application.Primitives -> IFX.Domain.Primitives`，结果为 `violation / noncompliant / partial / exit 4`。违规成立，partial 表示覆盖限制，不是执行失败。
- 真实 IFX UI 再次确认 105 项目、已知违规、错误后当前旧结果/下载清除、历史和规则保留，以及 JSON/HTML 导出。

## 独立操作员 A10

未参与实现的操作员完成外置 fixture 和真实 IFX 两轮 UI 任务。首次 confidence=3 和理解偏差保留；真实 IFX 补充复核后最终 confidence=4/5。全流程最高帮助等级保守按 L2，未提供逐字段/逐点击代操作。操作者认为最终结果与任务描述相符，且 UI 信息比 Guard 更简单明了。A10 为 `PASS`。

可用性改进项不阻断 W08：105 项目默认逐行展开，建议可折叠；中文 UI 与英文任务说明并置时需要更清晰的术语对照。第一次独立操作曾进入以 ArchSift fixture 为目标的页面，而任务真实意图是复核 IFX；后续真实 IFX UI 复核已单独完成并通过，但页面仍应更醒目地区分产品根、目标根、入口、规则和输出位置，降低把示例目标误认为真实目标的风险。启动脚本当时先显示 UI URL，只有操作结束后才形成最终 JSON，操作者对等待/完成时机有过不确定；一次已复核 UI 进程也在最终审计前残留，随后由身份、哈希、启动时间和监听端口精确核对并安全终止。这些现象均已被最终审计闭合，不改变 W08 PASS，但应进入发布后 UX 与进程生命周期改进。

W08 还暴露了规则表达能力的后续需求：`project-reference` 与 `nuget-denylist` 当前只能表达禁止项，未提供“只能引用这些项目/包”的封闭允许列表；现有组合校验只覆盖部分 TFM、命名和重复 ID 冲突，也没有通用的允许/禁止冲突矩阵。该需求不追溯扩大 0.1.0 或 W09/W10 范围，已登记到正式计划的发布后下一目标。

## 最终安全与清理

最终通过审计：`D:\ArchSift-lab\runs\ifx-w08-final-audit-917e35b210d3490d953288cd89db5f64\audit-result.json`，SHA-256 `b100cd3704e4802ec64db870c7e5606acafbe3435acaa52829cb71564a19f39b`。

- 冻结候选和 10 项关键证据哈希一致，`failure=null / pass=true`。
- candidate process count 为 0；已登记端口 2278、9255、14383 的 listener 均为 0。
- trusted persisted host、IFX、ArchSift 状态一致；本轮 host/IFX/product 前后状态也全部一致。
- 一个 03:53 重叠 UI 会话曾留下 PID 29220 的孤儿候选进程。它经 PID、路径、文件 SHA-256、启动时间和 loopback 端口共同确认后被精确结束；收尾证据 SHA-256 为 `5865ce38fbca6614aa8f0d6c7c182675bb542ee179e94dcf793254ffee2dcd9b`。
- 没有可安全删除且不损失证据的登记目录，因此最终清理为零删除。成功、失败、中断、报告及操作员证据全部保留；未登记浏览器下载和其他宿主路径未触碰。

协调脚本参数错误、三次停止审计和中断重复会话均保留，不改写为成功。它们没有造成持久环境/Profile/PATH、IFX checkout 或 ArchSift 产品树漂移。

## 门禁结论

| 标准 | 结论 |
| --- | --- |
| A01–A08 | PASS：自动 fixtures、CLI/UI、报告、状态语义、错误/取消与旧结果清理均有证据 |
| A09 | PASS：目标只读、根边界、环境隔离、真实 IFX 与最终清理状态一致 |
| A10 | PASS：独立操作员、confidence 4/5、最高 L2，真实 IFX 与构造样例完成 |
| A11 | PASS：预算先冻结，候选后测且全部低于阈值；不另作 IFX 性能预算声明 |
| A12 | PASS：自包含 ZIP 身份、许可、字节、CLI/Worker/UI 和无持久 PATH/Profile 修改成立 |

W08 已关闭。任何后续 W09/W10 工作必须作为新阶段单独开始，不复用或改写 W08 历史结果。
