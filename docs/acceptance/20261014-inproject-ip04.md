# ArchSift.InProject 0.1.0 IP04：CLI、任务与日常材料验收

日期：2026-10-14（Australia/Sydney）。状态：**IP04 MACHINE COMPLETE / IP05 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。本记录只针对外置合成 checkout；未运行真实业务目标，也不替代用户独立人工验收。

## 本阶段交付

`gate` 经同一个 `InProjectTaskService` 执行，按 worktree 独占 lease，持久写入有单调 revision 的当前任务与逐 run-id 历史。阶段状态从 queued／preflight 进入 snapshot、restore、build、bind、test、evaluate、persist，再到 completed／failed／cancelled；第二个相同请求获取现有 run-id，不重复构建。CLI `status` 查询当前或指定历史运行，按现有业务、规则、工具身份重新判定 stale；`cancel` 按精确 run-id 请求正在持有 lease 的任务停止 owned 子进程。持有者异常消失后读取状态会标记 failed，不使用旧通过。

每个运行把权威 `gate-result.json` 投影成中文 HTML 和 SARIF 2.1.0，并生成 `daily-index.json` 逐文件长度与 SHA-256。HTML 对规则和 finding 文本转义；SARIF 对没有真实源码位置的 finding 不臆造行号。配置拒绝、失败和取消写各自的当前任务及版本化阻断报告。Windows CI [显式调用示例](../examples/inproject-windows-ci.yml)仅由 `workflow_dispatch` 触发，要求固定包 URL／摘要与事先准备的目标离线 NuGet 包源；产品运行仍不隐式访问网络。`--evidence full` 在 IP05 前返回非零。

## 合成机器检查

可重放入口为 [`Test-InProjectIp03.ps1`](../../scripts/Test-InProjectIp03.ps1)，本阶段最终运行根为 `D:\ArchSift-lab\runs\inproject-ip02-probe-70b33fd8b1c3462cafc8137c0eda7c98`，对应 fixture 在同名 `D:\ArchSift-lab\fixtures` 目录。Windows x64、SDK 10.0.303；产品和专用测试为 Release/net10.0；业务四项目覆盖 Debug/net8.0、net9.0、net10.0。合成包填充使用明确给出的 NuGet URL，实际产品 restore 只读外置本地缓存；没有启动目标应用入口。每个 .NET 操作前后仅比较安全环境／Profile 哈希，全程相等。

| 场景 | 实际结果 |
| --- | --- |
| IP01–IP04 定向回归 | 16/16 集成测试通过；包含 HTML 注入文本转义测试；IP02 的构建／失败／漂移矩阵继续通过 |
| 日常结果与投影 | 六类型七规则通过和实际违规分别返回 0／2；两份 JSON 符合门禁 schema，两份 SARIF 符合官方 2.1.0 schema；HTML、逐文件摘要索引存在且索引哈希复算一致 |
| 当前与历史／新鲜度 | 通过后 `status` 为 completed/current；改规则后旧通过为 stale；违规运行成为新 current；测试源篡改后当前切到 failed/5，旧通过仍保留历史而不被当作当前放行 |
| 重复触发与显式取消 | 活动 gate 的第二个相同请求返回原 run-id，退出 4；按 run-id 取消使原进程返回 130、当前状态 cancelled、阻断报告 schema 有效，owned 子进程退出 |
| 重启失联模拟 | 写入合成的活动态且不持有 lease 后，`status` 改为 failed/3，生成版本化失败报告；未显示历史成功 |
| 未提供完整材料 | IP05 前显式 full 仍返回 5；未将 daily 报告标成 full |

当前日常索引保留构建关联、Worker／xUnit 原始记录、测试回执与报告，但不是可独立重放的完整证据。IP05 负责完整材料、30 天／100 次轮转、保护引用与归档恢复；IP06 负责 loopback Web 双入口及浏览器机器检查。真实 worktree 和独立人工仍在 IP08；本阶段不声明产品总验收或发布资格。
