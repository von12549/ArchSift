# 轻量架构测试工作台 LT03：报告与基本历史验收

日期：2026-10-15（Australia/Sydney）。结论：**LT03 REPORT/HISTORY COMPLETE / LT04–LT06 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。本阶段在已通过 LT02A 的外置 IFX 副本 `cc06528ab17052baafa496df2a89f6cde9207384` 和 ArchSift 合成工程上执行；正式 IFX B 类政策、最终人工 UI 验收未由本记录批准。

## 交付与报告语义

- 同一 `ArchSift.TestGate` 运行完成时将 `architecture-test-run.json` 与 `architecture-test-run.html` 写入每次新建的源根外目录。JSON 按 [LT00 冻结 schema](../../schemas/architecture-test-run.schema.json)记录本次 run ID、Git/SDK/TFM/配置上下文、实际规则 SHA、MTP 原生发现与执行身份、原 IFX 保留基线、阶段和门禁。HTML 对同一信息作转义后的可读投影，JSON 最后发布为权威。保存失败转换为 `block/3`；旧成功不成为本次结果。
- 测试适配层在断言前写入紧凑结构化规则求值，由 MTP/CTRF 或 xUnit 原生测试输出按测试 ID 关联。JSON 检查保留 `pass/violation/inconclusive/not-applicable/error`、matched、finding ID、实际源／目标类型及能取得的定位。IFX 专用 C# 测试只保留原生结果和原断言消息；没有结构化 Violation 时不伪造细分检测或 finding。
- IFX 报告从 LT00 责任清单列出未纳入 588 项：旧 A 类 182、未启用 B 候选 406，均不计入本次通过。合成目标不套用 IFX 政策清单。SARIF 为可选后续投影，此阶段交付 JSON/HTML。
- 每次运行另写 `architecture-test-state.json` 基本状态标记。`archsift test-history --directory <root> [--target <worktree>]` 只读列出有标记的运行；保存材料缺失／损坏为 `unavailable`，未确认终态为 `interrupted`，不能显示旧放行。已知规则 SHA 或 Git HEAD 变化为 `stale`；其余为 `unknown`，不会把 `unknown` 当成本次门禁失败或当前源码合规证明。使用说明见 [工作台文档](../architecture-test-workbench.md)。

## 真实 IFX 结果

最终正例目录 `D:/ArchSift-lab/runs/lt03-ifx-final-positive-20261015`：父进程 `IFX_UPDATE_SNAPSHOTS=1`，所有受控子进程强制 0；离线 restore、增量 build、MTP 发现与原测试全工程通过，原 80 个执行 ID＋JSON 1 个案例均执行，`allow/0`。报告中原 IFX 检查 80/80 passed；JSON 机制案例 `pass`、matched=1；182＋406 项明确未纳入。`architecture-test-state.json` 为 `finished/allow/0`；11 个 API 快照逐文件哈希未变。权威 JSON SHA-256 `fd389800466d6f2dc6ac0faf8ceafd27b22c393736bdedabf1e3b5159ffb7713`，HTML SHA-256 `3142f314ab821db9685c1e662e02ee5caf51e09e48c7c644f4d6ff3eb42ac23d`。

真实 JSON 依赖负例目录 `D:/ArchSift-lab/runs/lt03-ifx-json-negative-20261015`：原 80＋JSON 1 全部运行，`block/2`，规则 `ifx-context-mechanism` 的原始状态 `violation`、matched=1，一条真实 finding 指出 `IFX.Platform.Context.Contracts.EndpointScopeMetadata → IFX.Platform.Context.Contracts.ExecutionScope`；报告 SHA-256 `db14070b630a0fd197cfe530069db71e90297857555cf96a1bf8a5490d6f48f9`。临时规则改动按原字节恢复。

真实 API 快照负例目录 `D:/ArchSift-lab/runs/lt03-ifx-api-negative-20261015`：原 80＋JSON 1 全部运行，`PublicApi_ContractsSnapshotsMatch` 唯一 failed，`block/2`；报告保留原 Shouldly 断言消息、finding 数 0，未杜撰类型位置；报告 SHA-256 `bede3d7fd18aa22b4524b6f06d8c7238a2789a9ed818f40be1ab4ec8c9f78faa`。临时快照字节恢复，父进程值仍为 1。

以上三个真实 JSON，以及合成的完整、违规、漏原案例和 restore 失败四种报告，使用本机离线缓存的 NJsonSchema `10.9.0` 对冻结 schema 校验，均为 0 错误。真实历史页 `D:/ArchSift-lab/runs/lt03-ifx-history-page.json` 只读列出 3 次带标记运行，1 allow、2 block，未把先前成功盖到负例；规则负例复原后只将对应历史新鲜度标为 stale。页 SHA-256 `ddf47295a42404103b372461a74e18aa706dcbf4e2809e1dc0ebf9b1d7fb5d4d`。

## 合成负例与保留范围

更新后的合成全工程为 16 个原案例＋1 个 JSON 案例。七种门禁场景依次为：完整 `0`、真实依赖违规 `2`、漏原案例 `4`、subset `4`、离线 restore 失败 `3`、正常恢复 `0`、无效配置 `5`；宿主哈希一致。完整／违规／覆盖失败／restore 失败均保存本次权威报告，未执行不显示 pass。合成结果根目录见 `D:/ArchSift-lab/runs/archsift-lt02-59dd226049bc4d93a795863b95ff09de`，摘要 SHA-256 `eb6faed9c1f941aeb6c352f42b3bbceadd3bf4aea8ca8a23f37b9727a8349264`。

另用专门合成夹具在测试进程中预占本次 `architecture-test-run.json`：16 个原案例＋JSON 1 均可完成，但权威文件不能覆盖，最终为 `block/3`、状态标记 `failed/3`，原冲突文件字节保留；历史将该次标为 `unavailable`，此前两次 allow 保留为独立历史而不作为本次结论。独立历史负例还验证缺 HTML 为 `unavailable`、已知规则变化为 `stale`、遗留 running 为 `interrupted`、标记／报告矛盾为 `unavailable`，包括报告存在但未确认终态时不显示 allow。

整解 Release/net10.0 构建 0 警告／0 错误；旧 ArchSift UnitTests 151/151、IntegrationTests 95/95、ArchitectureTests 2/2 通过且无 skip。原 IFX checkout `main` 仍干净，独立副本中原 20 个测试源文件哈希 20/20 一致，新增适配器与仓库样例字节一致。关键 .NET 门禁／构建／回归的前后宿主安全环境／Profile 哈希和最终复查均为 `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`，未检测到持久漂移。

本阶段报告不提供 `SourceBound` 或完整构建因果保证；Git 提交与 dirty 标志只是上下文。基本历史没有轮转、固定保护、归档恢复或自动重跑。UI 编辑／启动／取消及同输入 UI/CLI 对照留待 LT04；LT05 才做同条件三次冷热与磁盘测量。
