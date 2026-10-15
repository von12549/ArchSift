# ArchSift.InProject 0.1.0 IP07：固定包与候选机器资格

日期：2026-10-14（Australia/Sydney）。状态：**IP07 MACHINE COMPLETE / IP08 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。本记录仅证明 Windows x64 上的外置合成门禁；真实业务 worktree、用户独立人工视觉与上手验收及正式发布仍在 IP08／IP09。

## 固定源码、包和许可

固定产品源码为 `5f46548af9f34a7a9d4714905fcff769053e76d5`。`scripts/Build-InProjectCandidateBinary.ps1` 以 SDK 10.0.303、Release/net10.0、锁定的本地 NuGet 来源和子进程隔离环境依次构建 Worker、测试桥接、CLI 与维护程序；每步后宿主安全环境／Profile 哈希相等。候选元数据在 `D:\ArchSift-lab\packages\inproject-0.1.0-5f46548\candidate-metadata.json`；独立 ZIP 为同目录 `archsift-inproject-0.1.0-win-x64-679cea968136.zip`，SHA-256 `679cea968136bad996a8e87fe6812df7918ddac94bbc48d0ecdbf6bd9a0e8311`，载荷 manifest SHA-256 `256433b35c6d2088237a81572dea419efd875967e0fcb4178d149b0b31b0a1db`，共 81 个文件。包内包含 net8/net9 参考包与专用 xUnit 锁的 19 个离线 nupkg；逐包 SHA-256、许可和归属见 [`inproject-third-party-inventory.json`](../migration/inproject-third-party-inventory.json)，包内保留许可文本与原有第三方 notices。`LocalPackage.Inspect` 在最终版本核对离线闭包与清单一致性。

维护入口以独立 sidecar 运行，支持 `inspect`、`upgrade`、`rollback`、`remove`、`recover`。安装内 PowerShell 启动器先校验复制维护程序到 owned 状态目录，再切换自身载荷；版本锁和固定目录以日志、摘要及回退收据复核。规则、项目配置、报告、证据与任务历史不参与切换。操作说明见[本地包与维护指南](../guides/inproject-0.1-maintenance.md)。

## 先冻结预算，再测固定候选

先用开发包 `26bcedf89b8ca6896f0f66f0d0a8c7e4e235901136f7e1f6a968aef3389d7a66` 测四项目混合 TFM、六类七条规则的冷／热运行：复制 19 ms，整次 41,755／32,572 ms，500 ms 采样的 `.archsift` 占用峰值 379,619,143／累计 438,879,083 字节。参考业务文件摘要 `ce198733b04734142066fdaa22c93411790a737d18430cede42042cb39cebd1e`；实测记录在 `D:\ArchSift-lab\runs\inproject-ip07-performance-cba564573dde4109a5de8cc37e2a0672\measurement.json`。[预算 JSON](../performance/inproject-0.1-budget.json)已在候选构建前随提交 `5011ae3` 冻结：复制 1,000 ms、冷／热整次 90,000／75,000 ms、采样峰值 600,000,000／累计 700,000,000 字节，另有恢复、构建、关联分析和 xUnit 分段阈值。阶段采样边界存在最多约 500 ms 的观测误差，峰值是采样到的普通文件字节数，不冒充物理块分配上界。

固定候选的全新合成安装测得复制 29 ms、冷／热 40,621／32,076 ms、采样峰值 379,635,292／累计 438,895,333 字节，全部分段与总预算通过。Windows x64，SDK 10.0.303，业务 Debug/net8.0、net9.0、net10.0；工具与专用测试 Release/net10.0。测量原件在 `D:\ArchSift-lab\runs\inproject-ip07-performance-da095afdeb334e06a9942a4841344868\measurement.json`。

## 固定候选验收

可重放脚本为 [`Test-InProjectIp07Candidate.ps1`](../../scripts/Test-InProjectIp07Candidate.ps1) 和 [`Test-InProjectIp07Lifecycle.ps1`](../../scripts/Test-InProjectIp07Lifecycle.ps1)。候选机器结果在 `D:\ArchSift-lab\runs\inproject-ip07-candidate-04df31e46a744fb5b43c4142c4eb0e9d\candidate-qualification.json`；生命周期结果在 `D:\ArchSift-lab\runs\inproject-ip07-lifecycle-035f1a81ec1241e3ba9ec0cca1326e91`。每个受控步骤比较安全宿主哈希，均相等；目标模块初始化器在既有合成回归中未执行。

| 场景 | 实际结果 |
| --- | --- |
| 全新安装与身份 | SHA-256、source commit、0.1.0 CLI、版本锁、81 文件 manifest、离线 19 包与许可清单均复核；错误包摘要拒绝，未切换原载荷 |
| 七规则 daily 与专用测试 | 冷／热两次均 source-bound、门禁通过；TRX 实际执行 2 条 B 类 xUnit 测试且均通过，七条必检完整 |
| full 与独立复核 | 固定包 full 通过，独立验证器复核 2,531 文件、run/worktree 身份；历史材料不充当前门禁 |
| Web 与 CLI | 候选 Web 仅回环随机端口启动；无会话和跨源请求均 403；Web daily 与 CLI full 的七条规则状态、门禁决定与输入摘要一致 |
| 缺少 SDK／运行时 | 子进程 `DOTNET_HOST_PATH` 指向不存在的 SDK 时生成新 `failed`／退出 3 任务，旧成功不保留为当前；`dotnet exec --fx-version 99.0.0` 的不存在测试运行时由 .NET 宿主明确非零拒绝，不假称实际卸载了宿主 SDK |
| 并存与维护 | 新根固定 0.1.0 与既有开发版本分离；现有外置丰富合成 worktree 上的升级、安装内 sidecar 回退、再次升级、移除、恢复移除以及模拟中断恢复均通过 |
| 旧材料和新鲜度 | 升级后旧版本成功门禁显示 stale；配置、规则、历史任务、报告和证据逐文件摘要在升级／回退／移除／恢复后相等，旧 full 保持原身份 |
| 双产品回归 | 离线实际 restore 后 Windows Release/net10.0：单元 151/151、架构依赖 2/2、集成 119/119；见 `D:\ArchSift-lab\runs\inproject-ip07-regression-4b5f4183606246eab9f8f900b1c1fac0`，宿主哈希相等 |

IP07 的开发过程中发现并修复离线 WindowsDesktop 参考包遗漏、维护目录未随载荷切换、部分中断恢复范围过宽，以及 SDK 缺失时未记录失败任务。早期失败的外置实验记录保留，不作为候选通过证据。当前候选是修复后 `5f46548` 的固定 ZIP，未拿开发包、旧成功报告或历史 ArchSift 检查数代替本次结论。

下一入口 IP08 仍要求用户具体指定并授权同一真实 solution 的两个 worktree、其 owned `.archsift/` 写入与外置证据位置；真实门禁须逐块审阅并比较原件／宿主。用户独立完成视觉与上手清单，信心至少 4/5、辅助不高于 L2。其前不声明产品验收、合并或发布。
