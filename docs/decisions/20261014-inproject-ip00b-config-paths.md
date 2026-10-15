# ArchSift.InProject IP00-B：配置、输入身份与 owned 路径契约

日期：2026-10-14（Australia/Sydney）。依据：[产品规格](20261014-archsift-inproject-0.1.0-product-spec.md)、[IP00-A 组件契约](20261014-inproject-ip00a-components.md)及[实施计划](../plans/20261014-archsift-inproject-0.1.0-implementation-plan.md)。本记录是 InProject 新宿主的显式路径模式；既有 ArchSift 的源根外报告与证据模式保持原行为。

## 文件归属和配置基准

| 相对目标仓库根 | Git | 所有权与处理 |
| --- | --- | --- |
| `.archsift/project.json` | 跟踪 | 规则集合、相对 solution、构建上下文与保留参数；schema 为 [`project.schema.json`](../../schemas/inproject/project.schema.json)。 |
| `.archsift/rules/`、`.archsift/version.lock.json`、`.archsift/bootstrap/`、`.archsift/tests/` | 跟踪 | 用户可审查的规则及理由、固定包身份、启动入口、薄专用测试项目与依赖锁；锁 schema 为 [`version-lock.schema.json`](../../schemas/inproject/version-lock.schema.json)。 |
| `.archsift/runtime/` | 忽略 | 固定包解压载荷；[payload manifest schema](../../schemas/inproject/payload-manifest.schema.json)及实际字节需与版本锁匹配。 |
| `.archsift/state/` | 忽略 | 本 worktree 的本机覆盖值、任务锁、临时快照与缓存；`state/local-settings.json` 使用[本机 schema](../../schemas/inproject/local-settings.schema.json)。 |
| `.archsift/reports/`、`.archsift/evidence/` | 忽略 | 本 worktree 的报告和默认完整证据；每个 run-id 独立目录。 |

初始化只为四个本地产物目录增加精确忽略项，不能忽略整个 `.archsift/`。配置路径以实际 `project.json` 所在目录为基准，含 `solution`、规则集路径和逐项目 `build.contexts.project`。例如 [`project.example.json`](../contracts/inproject/examples/project.example.json) 中 `../Product.slnx` 是从 `.archsift/` 回到同一 worktree 根的合法相对路径；不能把现有 `PathSafety.Under` 的“任何 `..` 都拒绝”直接用于此配置格式。解析时先标准化为绝对路径，再检查归属、真实路径及链接，禁止越出当前 worktree 根。规则集路径还必须位于该 worktree 的 `.archsift/rules/`；绝对路径和跨 worktree 指向均拒绝。

多候选 solution 必须由用户明确选择并保存相对路径；只发现唯一候选时可以默认。普通 Git checkout 和 linked worktree 的根及 common dir 均由 Git 查询取得；`.git` 可以是文件，不能假定为目录。工作目录不参与已保存路径的解释。无法核验 Git checkout 身份时不生成 worktree 隔离或当前门禁保证，报告不支持的目标状态。

本机外置证据根与本地包源只保存在被忽略的 `state/local-settings.json`，使用本机绝对路径；缺少 `evidenceRoot` 时默认 `.archsift/evidence/`。外置根必须经规范绝对路径、包含关系、链接和所有权标记核对，不能与业务源、另一 worktree、工具安装根或受保护目录重叠。不同 worktree 不共享运行态目录；跨 worktree 的证据只能作历史参考，当前门禁必须重新核验。样例中的哈希、包和路径均为结构说明，不能作为真实安装或证据使用。

## schema 后的语义校验

四份 schema 固定 draft-07、`schemaVersion=1`、英文机器字段和 `additionalProperties=false`。schema 只验证字段形状；加载器在产生任何写入计划前还须执行下列语义校验：

1. `product` 必须为 `ArchSift.InProject`。`solution` 须是当前根内唯一选中的 `.sln`／`.slnx`；`rulesets` 每个路径须在本 worktree 的规则目录、存在且字节身份可绑定。空规则集合可供发现，但门禁零测试不能放行。
2. `build.contexts` 中 `(project,targetFramework)` 不得重复；项目必须属于明确支持的 solution 配置及必检引用闭包。未列出的单 TFM 项目可由实际构建发现，多个 TFM 必须明确选择；具体求值矩阵由 IP00-C 冻结。
3. `allowNetwork=false` 时 `networkSources` 必须为空；为 true 时必须给出显式来源。任何网络凭据不得写入跟踪配置。restore 仍以明确本地包源为默认，不能隐式改用公网或忽略失败。
4. 版本锁须含七个不同且完整的组件 ID，产品、版本、RID、commit、契约版本与 payload manifest 一致；每个声明的 SHA-256 与实际包字节核对，不能只检查 schema。manifest 中每个载荷路径必须唯一、相对载荷根且无链接／重解析点；不能省略实际文件或多出未声明可执行文件。
5. 本机 `evidenceRoot`、`localFeed` 若存在必须是绝对路径。外置根不能落入另一个 checkout 或未经授权的目录。容量／时间保留值仅用于未受保护的日常证据，完整材料的保留和引用优先级由 IP00-E 冻结。
6. schema 无法证明真实路径归属、Git 身份、输入完整性、包字节、构建安全或规则语义。上述任一校验失败时不得创建目录、安装、构建或返回旧成功；配置错误与运行无法判定分别记录。

[`version-lock.example.json`](../contracts/inproject/examples/version-lock.example.json)、[`payload-manifest.example.json`](../contracts/inproject/examples/payload-manifest.example.json) 使用占位摘要，仅供 schema 校验；真实性校验必须拒绝其与任何实际包字节不符的情况。[`local-settings.example.json`](../contracts/inproject/examples/local-settings.example.json) 的外置路径也不指示在真实目标创建目录。

## 三种输入身份与失效

| 类别 | 内容和哈希 | 排除／失效规则 |
| --- | --- | --- |
| 业务输入 | solution、项目、源码、相关 props／targets、显式生成输入和实际构建输入；按当前 worktree 的规范相对路径、长度、原始字节 SHA-256 与构建上下文排序绑定 | `.archsift/`、`.git`、IDE 缓存与普通 bin／obj 不进入默认发现和快照；业务项目显式引用其中工具文件时报范围冲突，不静默隐藏。相关输入变化撤销本次关联。 |
| 规则／工具输入 | `project.json`、所有启用规则和例外、版本锁、bootstrap／专用测试源码及锁、实际载荷 manifest、Worker／桥接摘要 | 即使 `.archsift/` 被业务扫描排除，这些文件仍单独按原始字节绑定；改变规则或工具使旧门禁失效，不改写历史结果。 |
| 运行和证据身份 | worktree 根、run-id、Configuration、逐项目 TFM、SDK、必要环境摘要、实际程序集与依赖选择、报告及证据等级 | 运行时间和绝对路径用于定位，不能代替内容证明；轻量摘要不能被重新标为完整证据，其他 worktree 的材料不能自动充当前运行。 |

IP00-C 将定义编译／生成输入的取证字段及本次源码关联验证器；本阶段的哈希目录不自行声明 `SourceBound=true`。当前 `InputCapture` 已把 `.archsift` 排除在业务扫描外，同时仍可能纳入普通排除目录下被业务项目显式引用的文件；新模式须对显式工具文件引用先作范围冲突判断，不让它误入业务快照或被隐去。

## 写入、快照与清理边界

InProject 的受控写入仅包括初始化时的 `.archsift/` 跟踪文件及精确 `.gitignore` 项，以及运行时 `.archsift/runtime/`、`state/`、`reports/`、`evidence/` 或经本 worktree 本机设置明确选定的外置证据根。普通业务源码、solution、项目文件、祖先构建配置和其他 worktree 全程只读。运行时写入使用唯一 run-id／owner 标记、原子创建和每 worktree lease；清理只能针对清单中精确拥有且无引用的运行材料，不得以仓库根、`.archsift/` 根或外置证据根作为递归删除目标。

每次临时快照位于 `.archsift/state/build/<run-id>/`，复制时排除整个 `.archsift/` 及其他运行目录，防止递归复制。由于快照嵌在目标根下，IP00-C／IP02 必须检查复制前后 MSBuild 的有效 imports、SDK 与输入：祖先 `Directory.Build.props`、`Directory.Build.targets`、`Directory.Packages.props`、`NuGet.Config`、`global.json` 或自定义 Import 若因路径层级变化而不同，相关必检范围阻断。快照不视为 OS 沙箱，未知自定义任务不能默认为安全。只有工具拥有的测试项目可执行；不能启动目标应用、模块初始化器或业务测试。

现有 `PathSafety.EnsureDisjoint` 的默认语义及其在 `AnalysisService`、`ChainService`、`ComparisonService`、`Evidence*`、`HistoryStore`、`ReportWriter`、`IsolatedBuild`、CLI、Web 和 Setup 的调用保持原状。IP01／IP02 通过 InProject 专用的路径策略显式允许上述 owned 子根；禁止改成全局“源根内输出均可写”。现有 `PathSafety.Under`、`IsUnder` 和 `EnsureNoLinks` 可复用作规范化与链接检查，但配置基准中的合法 `../` 必须先解析后按最终包含关系判定。对文件名大小写碰撞、交叉 junction／symlink、绝对路径伪装、UNC／卷边界及跨 worktree 标记分别给出正反 fixture。

## 交付边界

IP00-B 冻结的是配置形状、路径基准、输入分类和所有权判断。IP01 实现初始化和 worktree 定位，IP02 实现实际编译关联，IP05 实现证据保留／清理。每包必须用合成 fixture 验证本记录中的正反行为；现有 ArchSift 源根外路径拒绝回归属于共享修改的必要检查。本阶段没有真实目标初始化、构建或清理。

2026-10-14 使用 PowerShell `Test-Json` 对四份 draft-07 schema 的结构样例逐一校验，4/4 有效；错误产品名、未知项目字段、短包摘要、未知本机字段、空载荷路径五种反例均被拒绝，5/5。检查仅证明 schema 形状；`allowNetwork`／来源组合、路径归属、重复上下文、七组件完整性与实际哈希仍须由 IP01 的语义验证及相应合成反例证明。现有 `PathSafety`、`ConfigLoader`、`InputCapture` 和调用点已只读核对；本阶段没有改变共享代码，因此不把旧 ArchSift 测试数记作 InProject 行为验收。
