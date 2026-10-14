# ArchSift.InProject 0.1.0 IP06：Web 工作台与双入口验收

日期：2026-10-14（Australia/Sydney）。状态：**IP06 MACHINE COMPLETE / IP07 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。本记录只涵盖外置合成 worktree；真实目标和用户独立人工体验仍属 IP08。

## 本阶段交付

`archsift-inproject ui --project <worktree>/.archsift/project.json [--port 0]` 启动仅绑定 `127.0.0.1` 的单 solution 工作台。CLI 与 Web 调用相同的 `InProjectTaskService`、配置、规则、报告与证据契约。页面展示目标、worktree、工具与规则身份，允许编辑已登记的规则文件、启动 daily/full、查看任务进度与当前／历史／新鲜度、读取 JSON 报告及 full manifest、查看清理预览。页面关闭不触发取消；显式取消由共享任务服务停止 owned 运行。规则保存先验证 SHA-256 乐观并发、JSON schema／语义和全规则组合，在任务 lease 空闲时原子替换，不能通过请求指定未登记路径。

本次 HTTP 会话采用启动时生成的随机 token、仅本次浏览器会话的 SameSite/HttpOnly cookie，并对修改请求要求同源 Origin、JSON Content-Type 和会话头。无凭据请求、跨源请求和错误会话头返回 403；未登记规则路径返回 400。资源与报告只从 worktree 安装根及已配置 evidence 根读取。页面为中文、可在桌面和窄屏阅读；机器状态码与 JSON 字段保持英文。最终人工视觉和上手评价未代替用户填写。

## 合成机器检查

可重放入口为 [`Test-InProjectIp06.ps1`](../../scripts/Test-InProjectIp06.ps1)。最终运行根为 `D:\ArchSift-lab\runs\inproject-ip02-probe-49c6da36a2724b44a04b29241180e5ab`，对应同名 `fixtures`，并新增独立 `fixtures/inproject-ip06-sibling-49c6da36a2724b44a04b29241180e5ab`。Windows x64、SDK 10.0.303；产品和专用 xUnit 为 Release/net10.0，合成目标四项目为 Debug/net8.0、net9.0、net10.0。合成目标参考包按显式 `https://api.nuget.org/v3/index.json` 获取；产品 restore 使用外置本地 feed，未忽略失败。运行前后及各 .NET 检查点只比较安全环境／Profile 哈希，均相等。目标模块初始化器未执行。

| 场景 | 实际结果 |
| --- | --- |
| IP01–IP04 回归 | 16/16 定向集成测试、IP02 构建／路径拒绝、IP03 六类七条规则通过与真实 ArchUnitNET 违规、IP04 重复请求／取消／失主恢复继续通过 |
| 实际 HTTP 安全 | 随机端口只在 loopback 提供服务；无 cookie、错误会话头、跨源 Origin 均 403；未登记路径保存 400 |
| CLI/Web daily 一致 | 同一规则和输入下，CLI 与 Web 七条规则的 key／状态、覆盖完整性、门禁决定／原因和输入 SHA-256 一致，均退出 0 |
| Web full 与材料 | Web full 退出 0，与 daily 门禁一致，full manifest 可读且 run-id 匹配；清理预览返回保留预算和受保护限制 |
| 编辑、状态、取消 | Web 原子编辑规则后旧成功显示 stale，恢复原规则成功；Web 显式取消退出 130；先前失败／取消／失主记录不会沿用旧成功 |
| 页面关闭 | 在浏览器点击“运行日常门禁”后关闭两个页面，后台合成运行 `b5fce7a9676d4ea08313f3cd094994a3` 仍以 completed／0 结束 |
| 双 worktree 并发 | Web 与另一个合成 worktree 的 CLI 门禁同时处于活动状态，worktree ID 与 run-id 不同，最终各自退出 0 |
| 浏览器机器检查 | Chrome 宽屏及 Codex 浏览器较窄面板均能读到目标、状态、任务按钮、证据和规则区域；发现并修正标识拆行及宽屏卡片被历史列表撑高的问题 |

最终脚本运行中的 Web daily 为 `a78f6fc5dd91469795cfe4e1dc60a9bb`，full 为 `bc692b42c94942a1bfc29cad7a173675`，取消为 `6466933bf2b5480697b25e926772c692`。最后仅调整了旧成功过期时的中文标题及门禁词汇，并用 `node --check` 和 Release/net10.0 重新编译检查；不改变 HTTP／门禁契约。浏览器测试窗口与 owned UI 进程在检查后关闭。

IP07 仍需固定包、版本与许可清单、升级／回退／移除、冷／热与磁盘预算及固定候选机器资格。真实业务 worktree、最终独立人工 UI 验收和发布门槛均未满足。
