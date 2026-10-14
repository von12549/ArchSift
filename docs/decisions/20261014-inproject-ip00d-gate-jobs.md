# ArchSift.InProject IP00-D：逐规则覆盖、严格门禁与任务契约

日期：2026-10-14（Australia/Sydney）。依据：[产品规格](20261014-archsift-inproject-0.1.0-product-spec.md)、[IP00-C 日常绑定](20261014-inproject-ip00c-build-binding.md)和[实施计划](../plans/20261014-archsift-inproject-0.1.0-implementation-plan.md)。本契约由 IP03／IP04 实现；现有 ArchSift `AnalysisService` 的 compliance 和退出码不能直接充当 InProject 严格门禁。

## 规则身份与执行覆盖

选择的每份 JSON ruleset 按文件原始字节固定身份。启用规则的稳定键是 `(ruleset id, ruleset version, rule id)`；同一选择中键重复即配置错误，按 ordinal 比较并排序，不能用显示名、severity 或测试索引作为身份。用户显式选择的规则集合内，所有 `Enabled=true` 规则组成必检集合 `requiredRules`；`Enabled=false` 不进入集合，但保留在配置身份中。空集合拒绝当前门禁。IP03 应先冻结规则与例外，再从同一权威 JSON 生成独立的 xUnit 发现清单，避免 test case 和 Core 使用不同规则字节。

现有六类规则按实际元数据需求分为 A 类项目／声明规则和 B 类类型／程序集依赖规则；具体一个规则实例的分类由共享解析器确定，不能只看显示名称。A 类由 Core 逐规则运行，B 类由专用 xUnit 项目逐规则发现和执行、通过薄桥接调用同一 Worker。每条启用 B 类规则必须有恰好一个稳定测试 ID，或有可解释的多个测试 ID 并聚合成一条规则记录；任何过滤、重复、缺失或框架跳过都进入覆盖审计。专用测试项目独立于业务 solution；测试发现不触发业务构建。单条测试可诊断，不可给出完整门禁放行。

覆盖清单至少记 `ruleKey`、分类、测试 ID、发现数、开始数、终结数、规则原始状态、finding IDs、限制及其影响范围、分析程序集上下文。A 类也有开始／终结计数。核对 `requiredRules == executedRules`、每条必检规则终结一次且没有意外执行、程序集上下文来自 IP02 已验证本次 run-id；零发现、全部框架跳过或过滤后缺项即覆盖不完整。一个 finding 可属于多条规则，但规则执行不能由 finding 数反推。逐规则报告原始 `pass`／`violation`／`inconclusive`／`error`／`not-applicable`；不得把构建错误伪造成 violation 或把门禁判定写回原始状态。

## 独立判定字段与严格表

运行 JSON 的逻辑字段固定为 `execution`、`binding`、`evidenceGrade`、`coverage`、`rules[]`、`limitations[]`、`gate`、`materials` 与 `freshness`；[结构样例](../contracts/inproject/examples/gate-result.example.json)仅供后续 Contracts 实现核对，不能作为实际放行材料。`execution` 表示任务是否完成；`binding` 表示 IP02 独立验证器的本次源码关联；`evidenceGrade` 为 `daily`／`full`，不能由摘要自行升级；`coverage` 逐规则显示发现与执行；`gate` 只表示当前运行的放行或阻断及原因；`materials` 指实际保留材料，`freshness` 比较当前业务／规则／工具身份。JSON 为判定权威，HTML／SARIF 是投影。规则状态、执行状态、证据等级、门禁结果和材料去向不得共用一个模糊状态字段。

| 条件（按优先顺序） | 原始记录 | 当前门禁 |
| --- | --- | --- |
| 取消 | `execution=cancelled`，记录已完成规则但不补造成功 | block；`cancelled` |
| 配置／产品／路径／不支持上下文错误 | `execution=failed` 或预运行拒绝，保留具体原因 | block；`invalid-input` |
| restore／build／测试运行器／Worker 错误，任务重启后失联 | `execution=failed`；已得规则状态不抹除 | block；`execution-error` |
| 输入漂移、无本次 source binding、缺必检项目／测试、零发现、过滤、漏执行、未知影响范围 | `execution=partial` 或 `completed`，`coverage.complete=false` | block；`inconclusive` |
| 必检规则 `error`／`inconclusive` | 原状态保留 | block；`inconclusive` 或 `execution-error`（有实际执行错误时） |
| 任一必检 `violation` | 原 finding 和例外保留 | block；`violation`，但更高优先级错误先行 |
| `not-applicable` 且规则未显式允许为空，或全部必检不适用 | 原状态保留 | block；`inconclusive` |
| 可归属的非必检范围 partial，全部必检实际执行、已绑定且有至少一条有效 `pass` | 原 `execution=partial` 保留，限制记录明确不影响必检范围 | allow；仅此狭窄例外 |
| 全部必检 `pass` 或显式允许为空的局部不适用、覆盖与绑定有效、至少一条有效检查 | 原状态保留 | allow；`passed` |

`Selector.AllowEmpty` 是显式允许空匹配的现有字段；只有适用规则类型与上下文确认它的语义时才可接受局部 `not-applicable`。全不适用始终不放行。severity 仅展示／排序，不能改变必检集合。partial 限制必须给出受影响的规则键和项目上下文；无法归属即影响全部必检。即使 analysis 的旧 `Compliance=compliant`，若覆盖、关联或新鲜度不成立，InProject 仍阻断。

## CLI 与任务状态

InProject 独立 CLI 冻结为 `gate --project <.archsift/project.json> [--evidence daily|full]`、`status --project <...> [--run <id>]`、`ui --project <...>`。不指定证据等级默认 `daily`；IP05 前显式 `full` 返回 `unavailable` 和非零，不能生成轻量材料后贴 full 标签。`gate` 退出码：0 放行，2 规则违规，3 执行错误，4 覆盖／关联／新鲜度无法判定，5 配置／不支持输入，130 取消。优先级为取消、无效输入、执行错误、无法判定、违规、通过；码 1 保留给宿主未分类故障，必须伴随诊断。`status` 成功读取任务返回 0，任务本身的门禁码在 JSON 字段中；查询失败返回 5／3。`ui` 成功启动返回 0，绑定或启动失败返回非零。CLI、Web、Agent 和 CI 均调用同一任务／门禁服务，不各自实现判定表。

以规范 worktree 根和产品锁身份确定任务分区。一个 worktree 同时只允许一个门禁任务；同一有效请求重复触发返回现有 run-id／状态，不创建第二个构建；不同 worktree 的 lease、状态、端点和材料互不复用。请求不同但锁被占用时返回明确冲突与当前任务 ID。任务持久状态：`queued -> preflight -> snapshot -> restore -> build -> bind -> test -> evaluate -> persist -> completed`，任何活动阶段可到 `failed`／`cancelled`。终态不可改写；重启后验证 lease 与 owned 进程，失联任务转 `failed`，不重放旧成功。取消为显式命令，终止 owned 子进程并等待退出；关闭浏览器页面只停止观看。新运行创建时旧结果留历史但 `current` 立即变为 running，失败／取消后当前显示该失败／取消；源／规则／工具变化时旧结果标 stale，不能作为当前放行。

状态写入使用原子替换与单调 revision；Web 和 CLI 读同一持久状态。端点仅 loopback，令牌或同等本地会话身份绑定 worktree；跨 worktree 的请求和非法路径拒绝。IP04／IP06 需要合成并发、重启、取消、页面关闭以及 CLI/Web 一致性测试，任务日志不能把机密环境原值写入报告。

## 契约验收边界

IP03 用独立编译的合法／违规类型依赖样例证明真实 ArchUnitNET finding，并验证六类规则逐条发现／执行。IP04 检查退出码和任务持久语义。IP00-D 冻结判定表，尚未运行新产品测试；旧 `AnalysisService` 在某些 partial／not-applicable 下可返回 0，故不能直接作为本产品严格门禁。完整记录形状与版本化 schema 可在 IP03 的产品 Contracts 固化，不能由测试桥接自行填写 `SourceBound`。

2026-10-14 已对本阶段结构样例做 JSON 语法解析，并静态核对必检集合、执行集合与退出码一致；实际服务、测试发现及状态机行为须由 IP03／IP04 正反场景验收。
