# W11 允许策略与大范围 UI 完成记录

日期：2026-10-07（Australia/Sydney）。状态：`COMPLETE / PASS`。

W11 在已发布并完成发布后验收的 0.2.0 基线上，交付项目引用允许列表、NuGet 直接包允许列表、确定的 allow/deny/exception 组合语义，以及真实大范围结果的信息层级和可审计 UI 生命周期。本记录关闭 W11；它不改写 0.2.0 发布资产，不代表新版本已经提交、推送或发布，也不授权开始新的工作包。

设计权威为 [W11 允许策略决议](../decisions/20261007-w11-policy-composition.md)。JSON 继续是规则与报告权威；Markdown、HTML、SARIF 和 UI 是投影。

## 交付范围

- `schemaVersion: 1` 以 additive union 分支加入 `project-reference-allowlist` 和 `nuget-allowlist`，旧六类规则与模板继续可读取。八类规则均有 JSON/Markdown 模板。
- 项目允许列表检查所选源项目的可解析直接项目引用；NuGet 允许列表只检查声明模型中的直接 `PackageReference`，包 ID 使用 `OrdinalIgnoreCase`。空允许集合是合法的封闭策略。
- 每条适用规则独立求值；多个 allowlist 等效于集合交集，deny 不会被 allow 覆盖。重叠例外按 exact、文字长度、通配符数量、selector、exception ID 的固定顺序选择唯一审计归因。
- 不完整引用或包声明继续产生 coverage limitation，不把未知项静默视为允许；零 source/scope 匹配继续区分 `inconclusive` 与显式 `allowEmpty` 的 `not-applicable`。
- JSON、HTML、SARIF、CLI 与 Web UI 保留新增规则 ID、severity、finding source/target、exception ID/reason 和 limitation。HTML/UI 显示例外归因。
- Web UI 固定每个 job 的目标快照，持续显示真实/fixture 目标身份、root、entry、rules、output、build mode、TFM 与 configuration。结果拆为“运行结论、规则结论、违规证据、覆盖边界”，项目与引用另成目标清单区；大范围明细支持筛选和折叠，限制数量及其对 `partial/inconclusive` 的影响始终可见。
- UI 启动器明确区分 session JSON 与最终 JSON，在服务退出前保持父 PowerShell 附着，并提供认证 loopback URL、安全关闭和 PID/端口证据。

## 自动门禁

最终自动证据位于 `D:\ArchSift-lab\runs\development-53b4733726dc4624a6a46b063d9f4997`：

| 项目 | 结果 |
| --- | --- |
| SDK / 配置 | .NET SDK 10.0.303 / Debug |
| restore / build | 显式离线；0 警告、0 错误 |
| 单元测试 | 97/97 |
| 集成测试 | 31/31 |
| 架构测试 | 2/2 |
| 总计 | 130/130，0 failed，0 other |
| 模板 | 8 个模板全部独立 validate/render，共 16 项通过 |
| 主机安全 | 每步及最终 User/Machine environment、Process PATH、四个 PowerShell Profile 均与基线一致 |

测试覆盖旧契约、两个 allowlist 的 exact/glob/空集合/零匹配/不完整输入、包 ID 大小写与中央版本、allow/deny 交集和双重拒绝、不同 severity、跨规则 exception 隔离、重叠 exception 排列置换、多文件/规则顺序置换，以及 JSON/HTML/SARIF/CLI/UI 投影。旧规则集无需迁移；重叠例外的合规结论不变，但审计归因从未定义的输入顺序改为已冻结的稳定选择，这是明确记录的兼容性变化。

桌面和 480px 合成视觉证据位于 `D:\ArchSift-lab\runs\w11-ui-hierarchy-visual-check`。`desktop.png` SHA-256 为 `dce77610deb1a6e06d1476933ce15636751c64fb4ff1e4b33de7f30416bcfa9f`，`narrow.png` 为 `80f2906f07ee398e6fe4fa0d01766e0726a2bcac9622029eafbcb060facf98e8`；窄屏 `scrollWidth == clientWidth == 465`，无横向溢出元素。该合成检查没有替代真实 IFX 人工复核。

## 真实 IFX CLI

操作员命令与边界记录在 [W11 IFX 现场复核任务](w11-ifx-allowlist-task.md)。接受结果为 `D:\ArchSift-lab\runs\w11-ifx-allowlists-3ba4055f16ab446ba47652515c47d8d3\operator-result.json`：

- 目标 `D:\IFX-10-Root\IFX-New`，入口 `IFX.sln`，HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`，`existing / net10.0 / Debug`；运行前后目标 HEAD 与普通状态相等。
- `exitCode=4`、`execution=partial`、`compliance=noncompliant`、105 项目、0 程序集、0 execution error。
- 项目规则 `w11-ifx-project-allowlist` 精确发现 `IFX.Application.Primitives.csproj -> IFX.Domain.Primitives.csproj`。
- NuGet 规则 `w11-ifx-nuget-allowlist` 精确发现直接包 `fluentvalidation.dependencyinjectionextensions`。
- JSON/HTML/SARIF 三份报告存在、身份固定，保存 JSON 与 stdout 相同；HTML/SARIF 均含两条规则。未 build/restore IFX，未执行 IFX 应用。

## 真实 IFX UI 与独立操作员

首次人工复核正确指出：虽然 105 个项目本体已折叠，但 105 条 coverage limitation 仍默认展开，且项目、规则结果、违规证据和覆盖限制的视觉区分不足。该轮保留为 `NEEDS FIX`，没有被改写成通过。实现随后把结果分为四个语义区和独立项目区，并保留限制总数、影响说明和按需展开。

最终接受会话为 `D:\ArchSift-lab\evidence\w11-ifx-ui-a0d97ac98b5541748d877e41698adbe0\final-result.json`，状态 `pass`：

- 目标上下文、真实目标标签、105 项目、0 程序集、`partial / noncompliant / exit 4` 均由操作员观察确认。
- 两条规则、两条预期 finding、规则限制计数、默认折叠的 105 条 coverage limitation、项目明细折叠/筛选和 JSON/HTML/SARIF 按钮均确认。
- 窄屏可读且无水平滚动；项目与结果、不同结果类型和 severity 在视觉上可区分。
- 操作员未参与实现，帮助等级 L1，confidence 4/5；备注为“页面比修改前清晰很多，不同的信息也在不同的区域内，以不同的颜色标记”。
- UI JSON/HTML/SARIF 与 CLI 核心语义一致；SARIF 通过 SHA-256 为 `ad6db49878699b091f3eeb765b6e29e92a34bad4da88664d000c923b549c3a25` 的 OASIS 官方 schema。
- 成功会话安全关闭，记录的 PID 与端口最终不存在，启动器退出；产品、目标和宿主状态相等。

人工流程中的失败与误判均保留：一个会话未进入操作员观察；跨 shell 的中间 PID 查询曾把仍附着的进程误判为不存在；首次 finalizer 误匹配自身命令行。最终安全审计还发现该弃用会话的 PID `34900` 仍监听端口 `4435`。审计先按 session 路径、进程名和命令行确认其为精确记录的 ArchSift Web 会话，再只终止该 PID；端口随后为 0。这是工具生命周期缺陷与清理证据，不是对最终 UI 语义结果的改写。

## 最终安全审计

最终结果为 `D:\ArchSift-lab\evidence\w11-final-safety-audit-1c8087f725b645678ee8776c27e8b963\audit-result.json`，`status=pass`：

- 自动门禁 20 项、130 项测试、八模板校验/渲染、CLI 与 UI 门禁、报告哈希、CLI/UI SARIF 一致性及官方 schema 全部通过。
- 产品 HEAD 为 `6330951f833537c13bd6f04c293cc4dfb951e58b`，W11 未提交状态与人工会话前一致，`git diff --check` 通过。
- IFX HEAD 为 `64ef2674c57e6cf9031481d6d4410cca55b0dad5`，工作树清洁；没有 build、restore 或执行 IFX 应用。
- User/Machine environment、Process PATH、四个 PowerShell Profile 与基线逐项相等。
- 所有已知 UI 会话身份、端口、ArchSift Web 候选进程、启动器和 .NET SDK 容器均为 0；Docker 查询成功。
- 24 份关键证据写入 `critical-evidence-manifest.json`，manifest SHA-256 为 `d44d7fbc0a3c147056fb510a79bfb71e1053f86d89c982e8663b6ba0c81db77c`。错误审计器运行、人工失败会话和原始报告均保留，没有递归或宽泛文件清理。

## 已知边界

- 真实 IFX 使用声明模型和 `existing` 模式；105 条 coverage limitation、0 程序集和 `execution=partial` 是接受的保守结果，不能解释为完整源码或程序集合规。
- NuGet allowlist 只覆盖直接声明包 ID，不覆盖传递依赖、版本范围或在线漏洞；项目 allowlist 只对可解析直接项目引用作允许判断。
- W11 没有启用 fail-on、required checks、远程治理或自动生成批准策略，也没有修改 Guard/IFX 源码或持久宿主配置。
- W11 源码与本文档在本记录生成时尚未提交、推送或发布；0.2.0 正式资产保持不变。

## 结论

设计门禁、实现与自动回归、真实 IFX CLI、真实 IFX 独立人工 UI 复核以及最终零残留安全审计均已满足。W11 状态为 `COMPLETE / PASS`。后续提交、推送、发布或新工作包必须获得相应授权。
