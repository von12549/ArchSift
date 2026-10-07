# W11 允许策略、组合语义与兼容性决议

日期：2026-10-07（Australia/Sydney）

状态：`DECIDED / IMPLEMENTED / ACCEPTED`。本记录先于 schema 和运行时修改建立；后续实现与验收已完成，见 [W11 完成记录](../acceptance/20261007-w11-allowlists-ui.md)。下文保留设计时点的范围、顺序与约束，不改写为事后决策。

## 目标与边界

W11 新增两种封闭允许策略：

- `project-reference-allowlist`：对所选源项目的每一条可解析直接项目引用，要求目标至少匹配本规则的一个 `allowedTargets` selector。
- `nuget-allowlist`：对范围内项目的每一个已声明直接 `PackageReference`，要求包 ID 位于本规则的 `allowedPackageIds`。

现有 `project-reference` 与 `nuget-denylist` 保持原有禁止语义。首轮不推断传递依赖、不解释 NuGet 版本范围、不访问在线漏洞数据、不从当前依赖图自动生成并启用允许策略，也不将未绑定程序集写成源码合规。

## 契约选择

规则集继续使用 `schemaVersion: 1`。新增规则是现有 `rules` 联合类型的可选分支，不改变旧分支的必填字段、默认值或解释；因此旧 JSON 不需要迁移或重写。模板集合将从六类增加到八类，旧模板名称和内容保持可读取。

`project-reference-allowlist` 参数固定为：

```json
{
  "source": { "kind": "project", "match": "glob", "value": "src/**/*.csproj" },
  "allowedTargets": [
    { "kind": "project", "match": "glob", "value": "src/Contracts/**/*.csproj" }
  ]
}
```

`allowedTargets` 是 selector 数组；单条规则内部为并集。数组可以显式为空，表示范围内源项目不得具有任何已解析直接项目引用。`source` 与规则 `scope` 都必须匹配，零 source 命中仍按现有 `allowEmpty` 规则得到 `inconclusive` 或 `not-applicable`。

`nuget-allowlist` 参数固定为：

```json
{
  "allowedPackageIds": ["Microsoft.Extensions.Logging.Abstractions"]
}
```

包 ID 使用 `OrdinalIgnoreCase` 比较并拒绝大小写不同的重复项；finding target 继续规范化为小写以保持稳定身份。数组可以显式为空，表示范围内项目不得声明直接包引用。中央版本只补充已声明包的版本事实，不改变允许判断；声明覆盖不完整时不能得到 `pass`。

## 组合决议

每条启用规则独立求值，所有适用约束必须同时成立。规则文件顺序、规则加载顺序、severity 或 UI 编辑顺序都不提供优先级，也不存在“最后定义获胜”。

| 组合 | 固定结果 |
| --- | --- |
| 单条 allowlist，目标/包在集合内 | 该规则 `pass`；覆盖限制仍可使其 `inconclusive` |
| 单条 allowlist，目标/包不在集合内 | 该规则产生 `violation` finding |
| 多条适用于同一主体的 allowlist | 每条规则独立检查，效果等于允许集合交集；只要一条不允许即违规 |
| 多条 allowlist 的集合交集为空 | 合法且确定，表示重叠范围内不允许相应依赖；不是配置错误 |
| 广域 allowlist + 窄域 deny | deny 命中仍违规；allow 不能覆盖 deny |
| allow 与 deny 同时拒绝同一依赖 | 保留两个不同 rule ID 的 finding；不去重为一个策略结论 |
| allow 接受但 deny 拒绝 | deny finding 保留，整体仍 `noncompliant` |
| deny 例外但 allow 拒绝 | 只豁免 deny finding；allow finding 仍使整体 `noncompliant` |
| allow 例外且 deny 未命中 | 原 allow finding 保留 exception ID/reason，该规则可通过 |
| severity 不同 | 只改变各 finding 的 severity，不改变规则优先级或合规布尔语义 |
| 相同 rule ID 跨文件重复 | 配置错误，沿用现有行为 |
| TFM 或 naming 的现有明显不兼容约束 | 配置错误，沿用现有行为 |
| allow/deny 内容重叠 | 不是配置错误；按独立规则与显式 exception 得到确定结果 |

结果与 finding 继续按稳定 ID 排序。规则集 identity 保留每个原始文件的字节 hash；文件顺序可出现在来源身份中，但不得改变 `ruleResults`、`findings`、`compliance` 或 SARIF 结果集合。

## 不完整输入与零匹配

- 项目引用允许策略只对 `status=resolved` 且有目标项目 ID 的直接边作允许判断。
- `ReferencesComplete=false` 或任何 unresolved/external/unsupported 引用为 coverage limitation；即使已知边全部允许，也不得返回 `pass`。
- NuGet 允许策略只检查声明模型中的直接包。`PackagesComplete=false` 或无法确认的声明为 limitation；未知项不能静默视为允许。
- 范围内项目引用数或包引用数为零，在声明覆盖完整时可通过；这是“主体存在且没有依赖”，不同于 source/scope 零匹配。
- source/scope 零匹配沿用 Q11：默认 `inconclusive`；只有相应 selector 显式 `allowEmpty=true` 才为 `not-applicable`。

## 例外唯一归因

例外仍通过 `ruleId` 只作用于一条规则，不能跨规则授权。多个例外同时匹配同一 finding 时，不再采用文件中的第一个；选择以下稳定排序的唯一首项：

1. `exact` 优先于 `glob`；
2. glob 中非通配文字字符更多者优先；
3. 文字字符数相同者，通配符更少者优先；
4. 再按 selector value 的 Ordinal 顺序；
5. 最后按 exception ID 的 Ordinal 顺序。

例外排列或规则文件排列不得改变选中的 exception ID/reason。旧规则集中没有重叠例外时行为完全不变；存在重叠时合规结论仍不变，但审计归因从未定义的输入顺序改为上述稳定选择。该兼容性变化必须写入 W11 验收和后续发行说明，不能伪装成旧报告字节稳定。

## 报告与 UI 投影

JSON 继续是权威报告。HTML 与 SARIF 必须保留新增 rule ID、severity、finding source/target、exception ID/reason 和 limitation；不增加会把 allow finding 误写成 deny finding 的特殊报告类型。中文 UI 标签固定映射：

| 机器字段/类型 | 中文术语 |
| --- | --- |
| `project-reference-allowlist` | 项目引用允许列表 |
| `nuget-allowlist` | NuGet 直接包允许列表 |
| `partial` | 部分完成 |
| `inconclusive` | 无法判定 |
| `not-applicable` | 不适用 |
| `noncompliant` | 当前规则违规 |

UI 必须持续显示产品身份、实际 target root、entry、rules、output、build mode、TFM 和 configuration；运行确认区不得仅依赖可编辑表单的瞬时值。大范围项目图默认显示摘要，项目明细通过筛选/折叠按需展开；折叠不能隐藏真实项目计数、limitation 或规则状态。

## 兼容性与实施顺序

1. 先提交本决议并将 W11 标记为 `IN PROGRESS`。
2. 以 additive schema 分支和两个新模板加入契约；旧六类模板必须逐字节或语义回归。
3. 实现项目引用/NuGet allowlist 与稳定 exception 选择；不得用 allow 结果短路 deny 规则。
4. 增加顺序置换、交集、allow/deny、例外、零匹配和不完整声明 fixtures，并验证 JSON/HTML/SARIF 与 CLI/UI parity。
5. 再实施目标上下文、大范围、术语和 UI 生命周期 UX；这些 UX 不改变 Core 合规语义。
6. 自动门禁通过后，按一次一块人工命令对真实 IFX 只读复核；未执行该步骤前不得将 W11 标为完成。

## 最低测试矩阵

| 组别 | 必须覆盖 |
| --- | --- |
| 旧契约 | 六类旧模板、旧规则集、旧报告结果不变；未知类型/字段仍拒绝 |
| 项目 allowlist | exact/glob、集合内/外、空集合、零 source、无引用、不完整/未解析引用 |
| NuGet allowlist | 大小写、集合内/外、空集合、中央版本、声明不完整、只验证直接依赖 |
| 组合 | 两个 allow 集合交集、广 allow/窄 deny、allow/deny 同时拒绝、不同 severity |
| 例外 | allow 与 deny 分别豁免、跨规则不生效、多个重叠例外排列置换后归因一致 |
| 多文件 | ruleset 文件顺序和规则顺序置换后核心结果一致；重复 ID/悬空例外仍报错 |
| 投影 | Markdown、JSON、HTML、SARIF、CLI、UI 对新增类型和例外归因一致 |
| UX | 真实/fixture 目标上下文、105 项目摘要/筛选/折叠、桌面/窄屏、正常/取消/异常/重复启动清理 |

本决议没有修改 schema、产品实现或发布资产，没有执行 Guard/IFX，也不代表 W11 已验收完成。
