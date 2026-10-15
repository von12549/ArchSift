# LT00：轻量架构测试工作台范围与契约

日期：2026-10-15（Australia/Sydney）。本记录固定 LT00 的设计输入，产品代码和真实 IFX 接入仍未开始。执行依据为[轻量工作台计划](../plans/20261015-archsift-lightweight-test-workbench-plan.md)；旧 ArchSift 0.8.0 的发布与验收身份不改变。

## 身份、组件和支持范围

- 功能名为 **ArchSift Architecture Test Workbench**，属于现有 ArchSift 仓库的新测试工作流。开发身份为 `0.9.0-dev`；首版不发布独立 NuGet 包、不引入 InProject 的安装身份。最终公开版本仍按 LT06 验收与另行发布授权确定。
- 无界面入口拟用 `archsift test-gate --config <file>`；UI 调用同一个运行协调服务。JSON 规则解释与类型依赖计算放在不引用 xUnit、CLI 或 Web 的共享库；xUnit v3 适配层只加载配置、构造稳定规则案例、调用 ArchUnitNET 并断言。协调服务负责目标、进程、覆盖、取消及报告，不预计算 finding 供 xUnit 复读。实现项目/命名空间可按此依赖方向命名，不要求 LT00 建空项目。
- 首版测试目标仅为 SDK 风格 C#、IFX 的 `net10.0` 架构测试工程、xUnit v3 4.x、Microsoft.Testing.Platform、Windows x64。按 IFX `global.json` 要求使用 .NET 10 SDK，正常离线恢复、增量构建和测试。混合 TFM、其他测试框架、Linux Web、任意外部 DLL 与自定义插件不在承诺范围。旧 ArchSift 产品测试栈不整体升级。
- IFX 原测试工程必须全部运行；新增 JSON 测试优先接入该工程。发现依赖冲突时才评估独立薄测试工程，完整入口仍执行原工程全套。IFX 目标、安装与业务源码仅作只读设计参照；LT02A 对获授权的外置副本执行真实接入。

## 输入身份与规则责任

IFX 源 checkout 在 LT00 只读核对为 clean HEAD `cc06528ab17052baafa496df2a89f6cde9207384`。现有安装规则库的五个 ruleset、逐规则 ID、类型、来源哈希和审查关系见[规则归属清单](20261015-lightweight-policy-inventory.json)。清单为导航和审查元数据，不是执行配置、C# 语义解释器或新政策副本。`dailySelection=not-selected` 表示目前**没有**获审查启用的真实 B 类 JSON 规则；LT01 仅用合成规则验证机制。未来正式配置必须显式列出已审查的 ruleset 路径、字节 SHA-256、启用规则 ID、范围与例外；不能直接把 406 条候选默认全开。

| 来源 | 静态现状 | 本次日常责任与限制 |
| --- | --- | --- |
| IFX 原 C# 专用测试 | 原工程 `ArchitectureTests`、`FixtureTests`、`ApplicationPortAdapterTests` | 全工程为完整门禁必检。原 ProjectGraph／LayerPolicy、Handler、TenantId、ApplicationOwnedPorts、Catalog、权限、API 快照及夹具断言继续由原 C# 负责；不得过滤或替换。 |
| A 类原 ArchSift JSON | 四份规则集，24＋33＋35＋90＝182 条 | 原 ArchSift/Core 继续负责未迁入增量。`target-framework` 1 条与 `graph-integrity` 2 条明确不在新 daily；89 条项目 allowlist 与 90 条包规则须逐项对照 IFX 断言。25 条空包 allowlist 与 IFX 零包政策存在具体重叠，其余精确包白名单与 IFX 包前缀禁令语义不同。清单仅标审查关系，不能将重叠自动判为等价。 |
| B 类候选 JSON | `type-dependency` 406 条：B01 133、B02 5、B03 100、B04 63、B05 100、B07 5 | 仅经审查启用的规则可进入新参数化测试。B02 的全关系 IServiceProvider 比 IFX 构造参数／字段断言宽，B03 的类型关系与 IFX `TransactionScope` 方法调用不同，B07 与 IFX 实际程序集和递归签名检查不同；各结果并列保留。B05 是第一方 Newtonsoft API 候选，IFX 现有包策略不是同义替代。 |

首版日常完整门禁的可声明范围是“IFX 原工程全部实际案例＋正式配置中经审查启用的 B 类 JSON 案例”。原 A 类报告独立保留，未纳入增量在报告和 UI 显示来源、责任方及原因；旧成功不能填本次通过数。完整取证沿用旧 ArchSift 0.8 evidence 工作流的独立入口，不变更它的 `SourceBound` 保证，也不声称它已执行新测试。

## IFX 原测试保留基线

[源码基线清单](20261015-lightweight-ifx-source-baseline.json)记录原工程项目文件、16 个 `ProjectReference`、20 个测试/规则/基础设施/夹具 C# 文件的 SHA-256，并列出每个 `[Fact]`／`[Theory]` 方法与原 `InlineData` 文本。静态清点为 `ArchitectureTests` 16 方法/16 案例、`FixtureTests` 41 方法/47 案例、`ApplicationPortAdapterTests` 8 方法/17 案例，共 65 方法、80 个按当前内联数据推导的案例。这个数字**不是运行器发现数或执行结果**。所有原方法、Theory 行、断言和项目引用均需逐项比较；删除原案例后新增等数量 JSON 不能抵消。原 `AllRules_RunUnderFiveSeconds` 的五秒阈值保留。

LT02A 要在获授权的 IFX 外置副本中，对原版与接入版分别读取 MTP 原生发现/运行结果，建立实际案例身份映射，并验证全工程执行、未 skip、原断言与 Theory 行不减损。新增 JSON 案例另计。显示名称、静态 80 数字或源文件存在本身都不构成执行证明；如 MTP 无可靠结构化身份或结果，完整门禁必须阻断并修正适配方式。

## 报告、覆盖与退出

首版独立报告的机器 schema 为[architecture-test-run.schema.json](../../schemas/architecture-test-run.schema.json)，文档类型 `architecture-test-run`、`schemaVersion=1`。JSON 为权威，HTML 为同一报告的安全投影。每次运行记录 run ID、目标/worktree/Git 上下文、SDK/TFM/Configuration、测试项目、规则字节、IFX 基线、发现/开始/完成/跳过/漏执行案例、来源与执行方、原生断言结果、JSON 规则状态、finding、未纳入项、阶段状态和最终门禁。Git 提交和 dirty 标志只提供上下文，不证明构建与源码因果。

- `runGate` 是已完成本次运行的不可改结论。完整运行须强制快照更新关闭、正常恢复与构建成功、原 IFX 全工程和启用 JSON 案例均实际完成且通过、无必检 skip/重复/漏执行及已知运行中相关输入变化。过滤一律为 `subset`，只能诊断。规则零源匹配默认 `inconclusive`；配置无有效 JSON 规则、未知启用类型或无法固定运行输入时不放行。
- `historyFreshness` 属于只读历史展示模型，取 `current`、`stale` 或 `unknown`；不存作 `runGate` 的证明条件。有效运行可 `allow` 而历史适用性为 `unknown`。运行后变化只影响历史展示，不能改写原报告、原退出码或自动触发补构建／哈希取证。
- 逐检查保留 IFX 原生 `passed/failed/skipped/not-run` 与 JSON 原始 `pass/violation/inconclusive/not-applicable/error` 两层状态。缺少结构化 IFX Violation 时仅显示原断言消息，不捏造细分规则数、源行或类型定位。读取、发现、查看、编辑、旧报告 pass、候选和未纳入均不计入本次通过。
- 新入口退出码：`0` 本次完整通过，`2` 违规／断言失败，`3` 恢复/构建/测试/权威报告错误，`4` 覆盖无法判定或已知运行中变化，`5` 配置/不支持输入，`130` 取消。多种问题都入报告，终码优先级为取消、无效输入、执行错误、覆盖无法判定、违规、通过。旧 ArchSift CLI 退出契约不改。

报告 JSON 保存失败时不得显示本次成功；允许输出诊断日志及非零进程退出，待写入问题修复后重新运行。首版历史仅独立 owned 报告目录、基本终态与只读列表；材料丢失显示 `unavailable`。不引入轮转、引用保护、归档恢复或任务重放。

## 子进程与测量口径

所有受控 restore/build/test 子进程显式设置 `IFX_UPDATE_SNAPSHOTS=0`；无法强制时拒绝启动。更新快照仍是独立开发操作，不以门禁通过名义执行。LT02A 需在获授权副本验证父值 `1`、子值 `0`，匹配基准通过、不匹配基准失败，前后基准字节与父值相等。设置隔离 `DOTNET_CLI_HOME` 时，同一子进程同时设置 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`。宿主环境/Profile 哈希在 .NET 操作前、关键阶段及结束时核对；只记录哈希与相等性。

测量以同一 IFX 提交、规则集字节、SDK、TFM、Configuration、离线源、机器与并行度为对照条件。分别记录原生 IFX 全工程、无界面新入口和 UI 启动，阶段耗时为 restore、build、test、报告；总墙钟、进程峰值资源、owned 报告目录增长、成功/取消终态单列。冷运行定义为新进程且没有本次构建输出，热运行复用已完成的增量输出；保留包缓存状态并记录，绝不暗中删除共享缓存。LT02A 做首次冷/热观察，LT05 在可比条件下各至少三轮并呈现中位数与范围，不提前设提速预算。任何测试过滤、规则差异或重建比例差异须标明，不能把不等价时长称为改善。

本阶段只冻结契约和只读来源清单。LT01 的测试直连最小闭环、LT02 的实际协调门禁、LT02A 的 IFX 真实接入与后续 UI 均按计划依赖分别验收。
