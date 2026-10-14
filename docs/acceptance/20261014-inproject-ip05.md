# ArchSift.InProject 0.1.0 IP05：完整证据与保留生命周期验收

日期：2026-10-14（Australia/Sydney）。状态：**IP05 MACHINE COMPLETE / IP06 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。本记录只覆盖外置合成 checkout，不涉及真实 Guard／IFX 目标或最终人工 UI 验收。

## 本阶段交付

`gate --evidence full` 与 daily 使用同一构建、规则和严格门禁服务。full 先保存权威 JSON、HTML、SARIF 与日常索引，再把本次快照、生成输出、构建／测试原始记录、固定工具载荷、项目／规则／测试配置、离线包缓存和实际编译引用闭包封存到本 worktree 的默认 `.archsift/evidence/<run-id>/` 或本机设置中的外置专属根。版本 1 manifest 列出产品版本、规则集版本与摘要、工作树／输入／规则／工具身份及每个相对路径的长度和 SHA-256；`closure.json` 将 IP02 捕获的实际输入映射到封存内逻辑路径。独立验证器逐文件重算摘要并交叉核对报告、绑定 verdict、capture 与闭包，迁移后不依赖旧机器路径存在。

显式 `evidence-archive`／`evidence-verify-archive`／`evidence-restore` 对整套 full 包工作，拒绝改字节、越界与业务源码目录恢复。`evidence-prune` 先按精确 bundle、run-id 和已验证归档预览，当前或固定运行不能删；执行后历史状态标记 archived、记录归档位置，原门禁结论不改写。日常轮转按完成时间先超过 `dailyDays`，再使未受保护的运行数不超过 `dailyMaxRuns`；当前、活动、固定及 full 始终保护，超预算明示，未知目录不清。精简历史保留原退出码与 `materialsState=compacted`。`worktree-export` 先预览配置、任务历史、报告、可用 full 包和已归档 ZIP，再向新外置目录导出并逐文件独立校验，用于移除 worktree 前的明确选择和留存。

## 合成机器检查

可重放入口为 [`Test-InProjectIp05.ps1`](../../scripts/Test-InProjectIp05.ps1)，最终运行根为 `D:\ArchSift-lab\runs\inproject-ip02-probe-0885f1c6689048dda7be0ee9856b4d30`，对应 fixture 在同名 `D:\ArchSift-lab\fixtures`。Windows x64、SDK 10.0.303；产品／专用 xUnit 为 Release/net10.0，业务四项目为 Debug/net8.0、net9.0、net10.0。合成参考包初次填充使用明确给出的 NuGet URL，产品 restore 只用外置本地 feed。运行前后及每个 .NET 检查点只比较安全环境／Profile 哈希，全程相等；目标应用入口／模块初始化器没有执行。

| 场景 | 实际结果 |
| --- | --- |
| 既有 IP01–IP04 回归 | 16/16 定向集成测试、IP02 负例、IP03 真实违规与 IP04 任务／取消矩阵继续通过 |
| daily／full 一致性 | 同七条启用规则的逐规则状态与门禁一致；full 报告只在完整封存和独立验证完成后才为 `evidenceGrade=full` |
| 默认／外置 full | 两种 owned 根封存与独立校验通过；外置包有 2531 个文件、554 条实际输入映射，约 328.33 MiB，并显式绑定 `0.1.0-dev` 与规则集版本；外置根 owner 与 worktree 身份相符 |
| 字节与位置负例 | 修改封存源码、修改 ZIP 和修改 worktree 导出文件均拒绝；恢复字节后验证重新通过；向业务源码目录恢复被拒且无写入 |
| 整套归档恢复 | ZIP 校验、异地恢复、恢复后全量独立复验通过；旧报告身份及门禁不变 |
| full 清理保护 | 当前和固定 full 的预览标明保护并拒绝执行；解除固定后按精确 bundle 与已验证 ZIP 清理，历史显示 archived／原门禁 0，归档继续有效 |
| 日常轮转 | 将合成保留参数收紧到 1 天／1 次后，两个固定 daily 导致明确超预算；另外 3 个未受保护运行被精简，违规历史仍保留退出码 2；当前 full、固定 daily、未知目录、外置 full 包和归档不受清理 |
| worktree 移除前导出 | 预览包含报告、full 包及 ZIP，外置导出与逐文件独立校验通过，篡改被拒；未实际移除 worktree |

本阶段的体积与冷／热耗时只是合成实测，不冻结产品预算；IP07 将以代表输入量化后固定候选。IP06 仍需 loopback Web 工作台、双入口共享任务与浏览器机器检查。IP08 的真实两 worktree 及用户独立人工门槛仍待分别进入。
