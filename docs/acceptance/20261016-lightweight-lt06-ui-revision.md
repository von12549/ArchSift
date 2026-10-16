# LT06 UI 修订候选机器记录

日期：2026-10-16（Australia/Sydney）。状态：**机器复核通过；用户独立人工 UI 复核五项通过；LT06 本地候选验收完成、未发布**。本页记录用户提出的 UID、语言、结果位置及全局 Results／History 接入问题的修订候选，不改写[首轮候选记录](20261016-lightweight-lt06-candidate.md)或[用户首轮观察与修订复核](20261016-lightweight-lt06-human-ui-review.md)。

载荷位于 `D:/ArchSift-lab/packages/lt06-ui-revision-20261016-a`，CLI／Web 均为 Release/net10.0 framework-dependent、`0.9.0-dev`，SDK `10.0.303`。`manifest.json` 的 SHA-256 为 `34147699cb67d5f24d68e48de93152208ca75519fefbea38df66dc2376443c41`；84 个载荷文件共 7,750,978 字节，清单中的逐文件哈希与实际字节一致。由于本阶段须待独立人工 UI 验收后才能完成提交，这份评审载荷明确标为未提交的 LT06 修订源码：基底提交 `e0b3bddabefd4561ca9ddfc13ed68e7d91a4f5b0`，清单另列 7 个改动源码文件的 SHA-256，均已复核一致。它不是已提交源码或正式发布物。

修订后，“架构测试”保留配置、三种来源和运行控制；本次报告在全局 Results 的“架构测试结果”视图中优先显示，历史列表在全局 History 的“架构测试历史”视图中。历史条目的“在结果页查看”会打开对应权威报告并定位到报告标题。局部模式从最近一次覆盖完整的完整运行读取 `discovery.log`，要求其 81 个发现 UID 与权威报告的发现 ID 集合一致；用户可按案例名称或 UID 搜索并填入 UID，避免混用 CTRF 执行 ID。架构测试静态与动态 UI 已接入中英切换，局部模式有独立的不可放行说明。原 IFX C#、JSON 与未纳入清单的规则和边界保持原义。

本机离线 Release 整解构建 0 警告／0 错误，前一轮同一修订系列的旧 ArchitectureTests 2/2、UnitTests 151/151、IntegrationTests 95/95、Testing.Tests MTP 原生 17/17 通过；最终小幅调整后 `node --check` 检查三个 Web JS、`git diff --check`、整解重建通过。使用新封装 Web 与隔离配置 `D:/ArchSift-lab/runs/lt06-ui-revision-20261016-a/web-config.json` 执行了新的真实 IFX 完整门禁：已审查的离线 restore、Release/net10.0 build、MTP 发现及 CTRF 测试依次完成；权威 JSON `D:/ArchSift-lab/runs/lt06-ui-revision-20261016-a/web-output/test-runs/20261015T153210767-6f6f240d/architecture-test-run.json` 的 SHA-256 为 `0d6af68909babde2ff6b97eb4fd277a77440832a1b3619b57ccf9f0dd472ff50`，通过冻结 schema 校验。原 IFX 80/80、JSON 1/1、覆盖完整、588 未纳入、`allow/0`。Web 当前任务的 `reportRunId`、权威报告 `run.id`、历史最新条目和报告 API 返回的 ID 同为 `0cd5f8533a934200a20aff7108d79e66`；UID API 提供 81 项。机器核对数据见 `D:/ArchSift-lab/runs/lt06-ui-revision-20261016-a/machine-web-validation.json`。

机器浏览器检查核实：英文架构测试页可读；切换中文后全局 Results／History 与报告分组显示中文；从历史查看报告切入 Results，不再置于历史清单之后；局部模式搜索真实案例名称后准确填入 `66a28de3e39a157dbf58fa835c28ecac228f77f7fe60c65cc9fb322ff3158cac`，未启动额外局部运行。新 Web 只读清单为 80 个原 ID、1 个 JSON 规则，首次历史和 UID 不可用；完整运行后历史为 1、UID 为 81。机器浏览器检查不代替用户在 Chrome 的视觉与可用性判断。

真实门禁前后 IFX 外置工作树仍为 `cc06528ab17052baafa496df2a89f6cde9207384`，仅保留 LT02A 已审查的项目引用与三个新增接入文件；规则和 80 ID 基准 SHA-256 与原记录相同，门禁后无额外源码／快照变动。宿主环境／Profile 哈希在恢复、构建、真实门禁及完成检查时均为 `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`。原 IFX checkout 与安装未改动，未运行目标应用入口。LT05 性能测量不记作本修订的新测量。

用户随后独立确认受影响的四点和新布局下的窄窗口、键盘操作，五项均符合预期；逐项观察见[人工清单](20261016-lightweight-lt06-human-ui-review.md)。封装时的清单保留当时未提交源码身份；后续阶段提交不会改变载荷字节。PR、合并、公开发布及 IFX 政策采纳均未执行。
