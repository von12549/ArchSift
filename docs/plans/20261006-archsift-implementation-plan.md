# ArchSift .NET 架构与依赖分析工具正式实施计划

日期：2026-10-06（Australia/Sydney）

计划 ID：20261006-archsift-implementation

状态：FORMAL — W00–W11 已完成；0.2.0 及发布后验收保持冻结，W11 已作为 0.3.0 正式发布并完成下载回验。0.3.0 Windows/Ubuntu 各 130/130 CI、固定 ZIP/SARIF、真实 IFX、独立人工 UI 和最终安全门禁均有证据；历史 CI、验证器、人工流程和发布 smoke 失败均保留。当前 `W11 COMPLETE / 0.3.0 PUBLISHED`。2026-10-09 已登记 [0.4.0 改进计划](20261009-archsift-0.4.0-improvement-plan.md)，历史起点状态为 `IN PROGRESS / NOT ACCEPTED / NOT RELEASED`；同日用户授权在该计划范围内合理设计 UI 逻辑和布局，要求功能分区明确、信息简洁明要、颜色区分明显，已纳入 0.4-A 设计约束及后续视觉验收；用户已授权在新分支 `codex/archsift-0.4.0` 执行全部 0.4.0 开发计划及后续提交、推送、PR、合并、发布；当前 0.4-A 已冻结，0.4-B–E 代码与自动检查完成（Windows 142 项、前端逻辑/语言检查、合成桌面/窄屏与 SARIF），0.4-F 候选自动门禁已通过：固定 061a53d 的 Windows/Ubuntu 各 142 项、两次 17 项原生包 smoke、仓库草稿独立下载字节一致；Draft PR #1 与未公开 release 草稿已准备，真实 IFX 首步发现已核对（105 项目、0 程序集、105 条限制、exit 4，目标/宿主不变），第二步 182 条候选规则验证已核对（全部 pass、0 findings、partial/compliant/exit 4，563 源码记录与发现一致，规则/包/配置/目标/宿主不变），第三步规则库与规则链 API/CLI 首次停止证据已保留，修正后八项全部通过且目标/宿主不变，第四步真实 IFX 浏览器操作员虽回答六项 yes、可用性 4/5，但结果写入脚本语法错误，且截图发现语言/安全关闭按钮错位与规则集卡片展开过长，故第四步未通过；旧会话已由操作员恢复安全结果并确认目标/宿主不变；UI 修复、结果写入修复及替代包 a561e7d 的 Windows/Ubuntu 各 142 项 CI、两次 17 项 smoke、草稿资产独立下载回验已完成，替代包第四步复测六项 yes、评分 4/5，但操作员新发现 182 条规则在编辑器中全部展开；此段为阶段历史；最新状态见下段，尚未合并/公开发布/通知 IFX 项目，仍遵守各项验收门禁与人工真实集成流程。

当前更新：0.4.0 精确产品提交 `bb599bd` 的真实 IFX 浏览器复测六项均为 yes，可用性 5/5；显式两次提交取消契约测试和最终只读 IFX/宿主/进程审计均通过。见 [最终验收](../acceptance/20261009-0.4-final-acceptance.md)。`v0.4.0` 已 [正式发布并下载回验](../releases/0.4.0-publication.md)，状态为 `ACCEPTED WITH LIMITATIONS / PUBLISHED / VERIFIED`；声明层 105 项目、零程序集、105 条限制及 IFX 候选策略未采纳的边界保留。上段长记录保留此前阶段和停止证据。

后续交接：2026-10-09 用户授权在 0.4.0 验收完成后通知 Codex Local Project `IFX-Guard-Test-Project`，由该项目编写 ArchSift–IFX 的 Agent 自动测试计划；已追加到 0.4.0 改进计划末尾。当前 `PUBLISHED / HANDOFF PENDING`，合并、发布及下载回验已完成；本项授权为测试计划编写，尚未启动测试执行。

2026-10-10 后续工作：用户授权完善 ArchSift 安装配置与环境文档，并设计 installer／upgrade；见 [安装指南](../installation.md) 与 [安装升级设计](20261010-installation-upgrade-design.md)。文档已补充发布状态、自包含包的实际需求、UI 静默启动与遗留进程问题、直接 Web 临时入口和手工升级边界；installer/upgrade 处于设计阶段，未实现或执行。IFX 已有安装的配置读取、直接 Web UI 启动及安全关闭得到操作者确认，标准 CLI UI 启动契约仍待修复。规则方向见 [IFX 0.4.0 备选](../../../IFX-10-Root/docs/plans/20261010-archsift-0.4.0-ifx-rule-candidates.md)，尚未生成或采纳正式策略。既有 0.4.0 发布与验收身份保持冻结；本次不选择新版本、不运行安装/升级或真实分析。

来源：[Draft 计划](20261006-dotnet-architecture-analyzer-draft.md)。用户于 2026-10-06 统一采用 Q01 至 Q14 默认值。所有产品决策项已关闭；下列实施前检查和阶段内测量是执行任务，不是未决产品问题。

2026-10-10 0.5.0 执行授权：用户指定 0.5.0，授权新分支中执行 CLI 启动修复及 installer/upgrade/rollback 计划，以及对应提交、推送、PR、合并、验证和发布。已创建 `codex/archsift-0.5.0`；详见 [0.5.0 计划](20261010-archsift-0.5.0-plan.md)。当前 IN PROGRESS / NOT ACCEPTED / NOT RELEASED；真实 IFX 人工操作及安全门禁不变。上文 HANDOFF PENDING 是旧阶段记录；0.4.0 改进计划已记录通知送达，仅证明测试计划请求已发送。

0.5.0 开发进度：新 CLI 转发与私有父生命周期通道已复现并修复静默启动；独立 Setup、schema/兼容性、所有权、包核验、安全解压、配置、备份、并存升级、回退和中断恢复已实现。Release 178 项、八模板及 Node 检查通过，宿主哈希相等；真实包、固定源码 CI、独立下载、人工终端与真实 IFX 门禁尚待关闭。版本文档更新为 0.5.0 开发态，0.4.0 指南另存且旧资产/验收不改。详见 [开发验收](../acceptance/20261010-0.5-setup-development.md)。

0.5.0 自动门禁关闭：精确产品 `c4010d8` 的 Windows/Ubuntu 各 179 项（含运行中父进程退出取消 audit）、两次 17 项标准原生 CLI UI/Worker/chain/SARIF/performance ZIP smoke、两次 13 项独立 Setup/旧包接管/升级回退、四项草稿资产 digest 与独立下载 bytes、宿主 equality 全通过。版本化当前/0.4.0 安装指南已随 ZIP 保留。见 [候选就绪](../acceptance/20261010-0.5-candidate-readiness.md)。Draft PR #2 与未公开 release `408066486` 已准备，状态 AUTO GATES PASSED / OPERATOR GATES PENDING / NOT ACCEPTED / NOT PUBLISHED；真实 IFX/人工终端/最终审计和合并发布尚待关闭，用户授权持续有效。

## 目标与交付边界

0.5.0 操作员第一步停止：误用旧安装根读取 manifest，实际包根为 `D:/IFX-10-Root/ArchSift/versions/0.4.0`。已修正操作员脚本及初始化证据，五项合成布局回归通过且宿主相等；真实步骤未通过，等待重试。产品源码/资产保持 c4010d8，未合并或发布；见 [候选记录](../acceptance/20261010-0.5-candidate-readiness.md)。

0.5.0 操作员第一步重试：真实 plan 输出、文件和 before/after 审计已读取，目标/原 0.4.0/宿主均相等；新根、配置和包身份审查通过，未 apply 或分析/build。见 [step01 记录](../acceptance/20261010-0.5-ifx-step01-plan.md)。下一块为操作者执行新根安装、private UI smoke 和 inspect；其包装脚本合成回归已通过。产品/候选资产 c4010d8 保持冻结，合并/发布仍受剩余人工门禁限制。

0.5.0 操作员新根安装通过：真实 receipt/inspect 已核对，`ArchSift-0.5-review` 选中 0.5.0、config/manifest/state 与审查计划一致，private UI smoke 成功，目标/原 0.4.0/宿主相等，无分析/build。见 [step02](../acceptance/20261010-0.5-ifx-step02-install.md)。标准 CLI 可见终端与键盘等人工门禁尚待执行；产品 c4010d8 和资产不变，尚未合并或正式发布。

0.5.0 操作员标准 CLI Safe shutdown 已通过：可见 URL/PID/waiting 与本地配置确认、CLI exit 0、Web PID 退出；宿主/IFX/原 0.4.0/新 config 与 626 payload 均相等，未分析/build。见 [step03a](../acceptance/20261010-0.5-ifx-step03-safe-shutdown.md)。Ctrl+C、pipe/file、外置副本升级回退及最终审计仍待人工逐块执行；产品候选保持冻结，未合并/正式发布。

0.5.0 操作员 Ctrl+C 已通过：独立可见控制台的 CLI exit 130，Web PID 40888 已退出且不再监听，宿主/IFX/原安装/新配置与包相等。见 [step03b](../acceptance/20261010-0.5-ifx-step03-ctrl-c.md)。下一步为管道与文件重定向验收；合成空目标测试通过。外置副本升级回退、最终审计及合并发布仍待完成。

0.5.0 操作员管道/文件输出已通过：新安装 CLI 管道就绪且安全关闭 exit 0、Web/监听退出；文件 stdout/stderr 的无 token 验收、宿主/IFX/原安装/新配置与包相等。见 [step03c](../acceptance/20261010-0.5-ifx-step03-pipe-file.md)。下一步先建立外置的真实保存状态副本和只读 adopt 计划；随后逐块升级回退、最终审计，尚未合并/发布。

0.5.0 操作员外置副本与只读 adopt 计划已通过：原 0.4.0 的 621 个 payload、2 个规则库文件（含墓碑）及 6 份报告复制并核对，副本配置仅将 rules/reports 重定位至外置根。plan ID `09ab270957afd8f66908da4132df1f293677580cdb5b2b077dd6946ba4d9b47a`，文件 SHA-256 `9921ac788fe415556b3bf3919d70517365d2dc47391b6fc6ec009824801ef7e5`；宿主/IFX/原安装/报告相等，未 adopt/upgrade/分析/build。见 [step04a](../acceptance/20261010-0.5-ifx-step04a-copy-plan.md)。下一块只在该外置副本 apply 已审阅 adopt 计划、inspect，并生成只读 upgrade 计划；其后再逐块升级/回退和最终审计。未合并/发布。

ArchSift 是独立的本地 .NET 架构与依赖分析工具。用户可以发现项目图、编写规则、验证项目并生成报告；后续增加变更前后验证与 CI 报告接入。CLI、配置文件和本机 UI 使用同一分析服务与契约。

首个完整版本为 0.1.0，覆盖六类规则、项目分析、项目验证、ArchUnitNET 程序集依赖检查、CLI、本机 Web UI、JSON/HTML 报告和本地 Windows x64 ZIP。后续 0.2.0 覆盖变更验证、SARIF、Linux CLI 验证与非阻断 CI 样例；各版本均以验收结果交付，不预设日期。

最初授权交付为正式计划文档，随后用户已授权完成 W00、本地提交 W00 并开始 W01。后续工作按对应执行请求开展；远程仓库创建、推送、发布包、修改旧 Guard 或消费项目 CI 仍按相应授权执行。

## 已冻结的产品决议

| 问题 | 最终决议 | 状态 |
| --- | --- | --- |
| Q01 名称 | 产品 ArchSift；CLI/仓库 archsift；命名空间 ArchSift；独立语义化版本 | CLOSED |
| Q02 仓库 | 本地 `D:\ArchSift`；用户已创建独立 ArchSift 空 Git 仓库及 Codex Local Project `ArchSift`，实施在该 Project 中开展；远程归属沿用用户个人账号 von12549 | CLOSED |
| Q03 工具平台 | .NET 10；Windows x64 CLI/UI 首发；Linux CLI 随后验证 | CLOSED |
| Q04 目标 | SDK 风格 C#；.sln/.slnx/.csproj；目标 net8.0/net9.0/net10.0；多 TFM 显式选择分别运行；旧式 .NET Framework、F#/VB defer | CLOSED |
| Q05 图范围 | 显式 TargetRoot；solution 为入口并清点范围内未加入 solution 的项目；外部引用未覆盖；首期声明模型，求值模型后续 | CLOSED |
| Q06 规则 | 项目引用、图完整性、TFM、直接 NuGet 禁止列表、类型依赖、命名；精确值和 glob；正则、自定义代码 defer | CLOSED |
| Q07 NuGet | 直接声明及可解析中央版本；包 ID 大小写不敏感；首期包 ID 检查；版本范围与传递依赖随后；不自动联网 | CLOSED |
| Q08 产物 | 默认使用已有产物；无法绑定源码则仅作程序集分析；显式隔离构建，restore 默认离线；网络由配置启用；默认 Debug；可删除本地缓存 | CLOSED |
| Q09 UI | loopback Web UI、.NET 后端、模板表单编辑 JSON、导出只读 Markdown；MVP 无账号与远程服务 | CLOSED |
| Q10 变更 | 默认 HEAD 对当前工作区，含暂存、未暂存与选定未跟踪源码；支持明确 base..head，比较最终状态；无自动 fetch 与逐 commit 验证 | CLOSED |
| Q11 规则边界 | 例外指定规则 ID、范围和理由；历史问题用结果比较，不自动豁免；零匹配默认无法判定；显式允许为空才不适用；重复 ID/冲突报错 | CLOSED |
| Q12 报告 | JSON/HTML 首发，SARIF 第二阶段；英文机器字段、中文 UI/可读说明优先；违规默认 exit 0，配置/运行错误非零，无法判定单独代码；fail-on defer | CLOSED |
| Q13 验收 | IFX 和独立 fixture；现场人工操作，一次一块命令；独立操作员信心至少 4/5、最高 L2；性能先测量后冻结预算 | CLOSED |
| Q14 分发维护 | 0.1.0 本地 ZIP；NuGet tool 发布随后；迁移前核对许可证；旧 Guard 暂停新功能，旧 UI 缺陷单列维护 | CLOSED |

## 实施基准与现有目录

| 对象 | 2026-10-06 只读核对结果 | 实施处理 |
| --- | --- | --- |
| Guard 源 | `C:\Users\von12\OneDrive\Desktop\Guard`；HEAD `40539d4610889483b4aed4637f8f3457098b1ff0`；版本 1.2.2；ordinary status 为空 | 以此作为选择性迁移参考，执行时复核并记录文件哈希 |
| Guard SDK | `build/global.json` 为 10.0.100、rollForward latestFeature、禁用 prerelease | 新项目使用独立 global.json，基准 10.0.100，记录实际 SDK |
| ArchUnitNET | Guard lock 指定 TngTech.ArchUnitNET 0.13.4 | 首期迁移基准固定 0.13.4，锁定实际 NuGet 依赖闭包；兼容故障通过计划变更处理 |
| IFX 样例 | `D:\IFX-10-Root\IFX-New`；HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`；入口 IFX.sln；中央 props 声明 net10.0 | 记录样例基准；在外置快照/测试目录完成构建与违规样例 |
| 新项目目录 | 只读观察到 `D:\ArchSift` 的 .git 与 README；用户随后确认这是其新建的 ArchSift 空仓库，并已创建同目录的 Codex Local Project `ArchSift` | 在目标 Project 中检查适用指令、status、remote 与是否已有初始提交；直接使用现有仓库 |

用户确认建立了目标仓库及 Local Project；本对话此前的 Git 读取受沙箱限制，不作为目标 Project 无法实施的结论。实施在 `ArchSift` Project 的 `D:\ArchSift` 工作目录中开始，保留已有 README 与 .git。空仓库可能没有 HEAD，检查时按新仓库处理，不将缺少初始提交误判为损坏。远程建议身份为 von12549/archsift，实际 URL、存在性及大小写在目标 Project 核对，用户确认本地 Git 仓库不等于确认远程已经创建。

本文件是目标 Project 的正式计划来源。实施入口先将本计划复制到 `D:\ArchSift\docs\plans\20261006-archsift-implementation-plan.md`，目标不存在时创建；若已存在则核对内容与本次决议，保留目标新增记录。后续实施状态、验证证据及计划变更在目标 Project 的计划文件中维护，此处保留为移交基准。

Guard 当前未检索到根 LICENSE 文件。迁移前记录源权属、文件来源与第三方许可证；来源检查未完成的文件不复制。此项是既定迁移检查，不改变已批准的独立项目方向。

## 仓库结构与依赖方向

采用 .NET 核心类库与多个入口，先集中规则和模型，避免为首期六类规则建立动态插件平台。

```text
D:\ArchSift\
  ArchSift.slnx
  global.json
  Directory.Build.props
  Directory.Packages.props
  AGENTS.md
  README.md
  src/
    ArchSift.Contracts/              规则、配置、报告及产物 DTO
    ArchSift.Core/                   输入身份、项目模型、规则、服务与比较
    ArchSift.ArchUnit/               程序集模型与 ArchUnitNET 适配
    ArchSift.Cli/                    命令行与 Worker 入口
    ArchSift.Web/                    本机 ASP.NET Core 服务及静态 UI
  schemas/
    ruleset.schema.json
    config.schema.json
    report.schema.json
    assembly-manifest.schema.json
    comparison.schema.json          P4
  templates/rules/                  六类 JSON 模板及生成的 Markdown
  tests/
    ArchSift.UnitTests/
    ArchSift.IntegrationTests/
    ArchSift.ArchitectureTests/
    fixtures/
  scripts/
    Verify-Contracts.ps1
    Verify-Fixtures.ps1
    Measure-Performance.ps1
    Test-HostSafety.ps1
    Test-PackageSmoke.ps1
    Publish-LocalZip.ps1
  docs/
    rules.md
    reports.md
    cli.md
    build-inputs.md
    decisions/
    migration/source-inventory.json
    migration/third-party-notices.md
    performance/budget.json
    acceptance/
  samples/                         配置与规则，无真实运行缓存
```

依赖方向：CLI/Web → Core；Core → Contracts 与分析接口；ArchUnit → Contracts/Core 的接口，入口通过组合注册实现。Core 不引用 CLI/Web，不依赖 Guard 安装、V4_STAGE_INPUT_JSON 或 Profile/Stage 生命周期。ArchSift 自身的架构测试验证这些方向。

Web 首期使用 ASP.NET Core 与 HTML/CSS/JavaScript 模板表单。服务器使用同一 Core 服务和报告序列化器，页面展示服务结果。CLI 与 UI 均显示最终生效配置，优先级为显式入口参数 → 配置文件 → 文档化默认值。

实现第三方依赖优先使用 .NET 内置 API；对 schema 校验、测试框架或源码解析确需外部包时，P1 记录选择理由、版本、许可证和 lock 文件，不要求用户逐包定案。版本必须显式固定，升级属于后续变更。

## 规则契约

规则 JSON 是唯一可执行定义。schemaVersion 首期为 1；JSON Schema 使用 draft-07，schema 校验与业务语义校验分别运行。未知类型、未知字段、缺失参数或不支持选择器均为 configuration-error。

规则集含 schemaVersion、id、version、description、rules、exceptions。规则含 id、type、enabled、scope、parameters、severity、reason；severity 限定 info/warning/error。每个选择器显式指定 kind、match（exact/glob）与 value。

多个规则集按配置给出的顺序加载，记录每个规则集身份和原始文件哈希。有效规则 ID 全局唯一；重复 ID 或相互矛盾定义不采用最后写入覆盖。允许一个规则集含一个规则，支持多规则文件组合。

glob 明确支持 `*`、`?`，路径额外支持 `**`；所有路径使用根内相对路径和 `/`，不得隐式转为正则。项目 ID 使用相对 csproj 路径；命名空间、类型和普通名称按 Ordinal 比较，NuGet ID 按 OrdinalIgnoreCase 比较。Windows 上大小写碰撞的项目身份报错；Linux 阶段验证同一规范。

| 类型 ID | 首期参数与语义 | 输入及边界 |
| --- | --- | --- |
| project-reference | source/target 项目选择器，禁止 source → target 边 | 原始声明图；条件或表达式涉及的边无法判定 |
| graph-integrity | check 为 resolved-references 或 solution-membership；分别检查引用解析和范围内项目入 solution | 已发现项目清单；外部引用显式显示未覆盖，不假造内部缺失项目 |
| target-framework | 项目范围与 allowedFrameworks | 项目直接值或根内最近祖先 unconditional literal Directory.Build.props |
| nuget-denylist | forbiddenPackageIds | 所选项目的直接 PackageReference；大小写不敏感；不承诺传递依赖 |
| type-dependency | source/forbiddenTarget 类型或命名空间选择器，禁止对应类型依赖 | ArchUnitNET，记录相关程序集与构建上下文 |
| naming | subjectKind 为 project/assembly/type/source-file；requiredName 为 exact/glob | 对应模型；type 使用程序集模型，source-file 仅检查相对文件名/路径 |

interface 实现位置规则不进入 0.1.0；不因 Guard 已有对应算法就增加首期范围。类型源选择器零匹配默认 inconclusive；显式 allowEmpty=true 才为 not-applicable。禁止目标未出现且模型覆盖充分时可以通过，避免把“禁止对象确实不存在”误算零覆盖。

exceptions 项必须具备 ruleId、稳定相对范围或主体选择器与 reason。报告保留原 finding 和对应例外关联；不删除证据。被明确豁免的 finding 不计入有效违规，并报告 exemption 数量。历史基线报告不自动转为例外。

Markdown 从 JSON 生成，包含来源、版本、哈希、规则解释与字段说明。只读表示阅读投影，不将文件权限作为安全机制；编辑 Markdown 不影响执行。生成文件输出到明确目录，不自动写入目标仓库。

draft-rules 将 observedFacts、candidateRules 和 unresolvedDecisions 分别输出。仅提供需要人类选择的建议；已经存在的依赖不会自动成为允许策略。

## 项目发现与扫描范围

TargetRoot 必填；入口为根内显式 solution/project，或在无入口时清点根内支持项目。solution 入口同时报告 includedProjects、unlistedProjects、externalReferences、unresolvedReferences 与 unsupportedConstructs。项目引用图不依赖规则集。

首期支持 SDK 风格 C#、.sln/.slnx/.csproj。工具运行于 .NET 10，目标框架限定 net8.0/net9.0/net10.0；工具运行 SDK 和目标项目 TFM 分别记录。项目构建需要的 targeting pack 未安装或离线不可用时准确返回无法判定。

多 TFM 必须选择，分别生成结果；禁止将一个 TFM 的通过外推到其他 TFM。缺少选择时返回明确配置错误及候选值。未编译声明发现可以列出所有 literal TFM，但仍标为 declared 模型。

解析项目 XML 不执行 MSBuild。复杂 Condition、Import、属性表达式或不支持的中央配置，针对受影响事实标记无法判定。读取直接 PackageReference，并记录根内可解析 Directory.Packages.props 的 literal PackageVersion；版本范围与传递 NuGet 检查延期。未知或条件声明不能当作无引用。

项目越出 TargetRoot 时记录 external/uncovered；工具不自动递归外部目录。链接和 reparse point 不跨越；请求范围内的链接输入显示具体限制或路径错误，不 silently pass。源范围始终是报告的一部分。

## 输入身份与构建产物

定义统一 InputManifest，并区分 analysisInputs、buildInputs 与 evidenceInputs。analysisInputs 包含相关源码、项目/solution、props/targets 和规则；buildInputs 包含实际构建使用的 imports、资源、生成器与上下文。相同范围判定逻辑供 discovery、验证、构建快照和比较使用。

排除 IDE 缓存 `.vs/.idea`、VCS `.git`、普通 `bin/obj` 与工具输出目录，但不能忽略被项目显式引用的资源或生成源码。根外 imports、依赖或生成步骤不能可靠复现时保留限制，不能生成完整源码合规结论。

输入身份使用相对路径、内容哈希、规则及引擎版本、TFM、Configuration、相关 SDK/构建参数与程序集哈希。可选 Git commit/tree 提供版本来源，绝对路径只用于定位。核心 findings 排序和身份确定；运行时间、耗时、本机路径独立为 runMetadata。

运行前后校验输入，相关输入变化返回 source-changed-during-analysis 并使该次当前源码合规结论 inconclusive。IDE 正常开启或创建排除缓存不触发失效；取消结果不能借用上次成功。

已有产物模式为默认。assembly-manifest 包含 schemaVersion、源码/构建输入哈希、项目与程序集映射、SDK、TFM、Configuration、生成方式与程序集 SHA-256。普通 DLL 无来源绑定时可作 assemblies-only 报告；选中需要验证当前源码的规则则 inconclusive，并说明无法证明对应关系。时间戳不能替代输入绑定。

显式 build.mode=isolated 时生成根外快照及构建产物；默认 Debug。生成清单绑定本次构建的保守根内输入快照与最终分析 assembly，记录 SDK/TFM/Configuration/生成方式和程序集闭包；不宣称精确 MSBuild 文件访问轨迹。首期标准 SDK 的安全隔离不包含未审查自定义任务、Import、输出路径、条件/表达式和包构建脚本/生成器，遇到这些情况拒绝或标明无法绑定当前源，完整求值继续 defer。分析程序集通过独立 Worker 进程运行，避免不同版本程序集加载相互污染；不调用目标入口和业务方法。

restore 默认 offline，只使用明确缓存和本地 feed。配置 allowNetwork=true 后才使用显式 NuGet sources；不能把忽略源失败或过期产物当作构建成功。缓存可删除、非权威；缓存命中仍需校验输入身份和字节哈希。

在隔离子进程设置 DOTNET_CLI_HOME 时必须同时设置 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0；不回写父进程与 User/Machine 环境。隔离目录不等同完整沙箱，MSBuild 自定义 targets 可能执行命令，UI/CLI 应在用户启用 build 时准确说明该行为。源目标保持只读契约，实际构建与产物写在外置目录。

构建失败仍输出已完成的项目规则结果；程序集规则 inconclusive，另记录执行错误。缺失依赖、编译失败、配置不同或产物哈希变化不得得到当前源码合规结论。

## CLI 与配置接口

以下是实现后的公开命令目标，当前文档不是可执行程序。

| 命令 | 行为 | 阶段 |
| --- | --- | --- |
| archsift analyze --config <file> | 无规则集发现目标并输出项目图/报告 | P2 |
| archsift verify --config <file> | 读取目标与 rulesets 执行规则 | P2 |
| archsift rules validate --file <rules.json> | schema 与规则语义检查 | P1/P2 |
| archsift rules render --file <rules.json> --output <file.md> | 生成 Markdown 阅读投影 | P1/P2 |
| archsift rules draft --config <file> --output <directory> | 发现事实并生成未接受的规则建议 | P2 |
| archsift ui --config <file> | loopback UI，复用同一运行配置 | P3 |
| archsift changes --config <file> [--base <ref> --head <ref>] | 默认 HEAD 对工作区，或显式两端比较 | P4 |

入口支持显式 --target、--entry、--rules、--output、--tfm、--configuration 覆盖配置；参数类型由同一配置模型验证。相对配置路径以配置文件目录为基准，CLI 显式路径以 invocation cwd 为基准，解析后显示绝对位置。分析不依赖隐藏 cwd 或 PATH 中的 Guard。

示例配置的结构由 schemas/config.schema.json 实现：

```json
{
  "schemaVersion": 1,
  "target": { "root": "D:/sample", "entry": "Sample.sln" },
  "rulesets": ["./rules/architecture.json"],
  "build": {
    "mode": "existing",
    "targetFramework": "net10.0",
    "configuration": "Debug",
    "assemblyManifest": "./evidence/assembly-manifest.json",
    "allowNetwork": false
  },
  "output": { "directory": "D:/archsift-output/sample", "formats": ["json", "html"] }
}
```

assemblyManifest 仅在已绑定产物存在时提供；项目级 analyze 不要求它。build.mode=isolated 时由工具生成产物清单，existing 模式可以显式列出未绑定程序集。两种报告身份必须可见。

## 报告与退出语义

report.schema.json 固定 schemaVersion=1，包含 operation、toolVersion、inputIdentity、rulesetIdentities、scope、buildContext、engineVersions、execution、compliance、ruleResults、findings、coverage、limitations、runMetadata。

逐规则 status 为 pass/violation/inconclusive/not-applicable/error。execution 为 completed/partial/failed/cancelled；compliance 为 compliant/noncompliant/inconclusive/not-applicable；无规则的 analyze 使用 null。若存在有效违规，compliance 为 noncompliant，同时完整保留 partial coverage；无违规但覆盖不足时为 inconclusive；全部规则不适用时不能显示 compliant。

finding 包含稳定 ID、规则 ID、主体、依赖 source/target 或名称证据、相对位置（可得时）、severity、解释、例外关联。没有源码映射时显示程序集和类型，而不虚构行号。JSON 为权威运行报告，HTML 为其投影，显示覆盖与限制并转义所有用户输入。

| Exit code | 定义 |
| --- | --- |
| 0 | 运行完成，检查可判定；可以有违规，默认不阻断 |
| 2 | 参数、JSON schema 或规则语义配置错误 |
| 3 | 工具、依赖、构建或报告写入执行失败 |
| 4 | 运行完成或部分完成但存在无法判定检查、输入变化或产物来源不足 |
| 130 | 用户取消 |

配置/执行错误、取消、无法判定存在时，优先返回对应非零状态；报告仍可保存已完成结果。CI 非阻断指规则违规不触发失败，不隐藏工具失败。未来 fail-on 策略延期，届时另行定义违规失败代码。

## 本机 UI

默认随机可用 loopback 端口，浏览器地址仅绑定 127.0.0.1/localhost。入口提供目标/solution、规则文件及模板表单、构建模式、TFM/Configuration、输出目录和运行按钮；显示项目图、逐规则结果与报告下载。

配置路径由本机服务校验，所有运行和写入边界统一规范化。写操作限制为明确规则编辑目录、输出目录与临时运行目录；目标分析不得修改源码。禁止原始 shell/任意命令输入；服务启动会话 token、Origin 校验和请求限制覆盖配置与运行接口。

报告和错误来自当前运行 ID。运行中修改配置需明确下一次运行；旧结果保留为历史且标出输入身份。失败、取消、源变化及产物失效时当前状态不能残留 success。支持取消，不提供远程账号或长期控制面。

UI 引导用户回答三件事：本次检查了什么、哪些规则违规、哪些规则尚无法判断。技术哈希与诊断默认折叠，不要求手工 hash 或八步 onboarding。

## 变更验证与 CI 报告

P4 默认基线为 HEAD 的完整内容，目标为工作区最终状态，包含暂存、未暂存与符合输入选择规则的未跟踪源码。报告列出实际包含/排除的文件；同一文件同时有暂存和未暂存修改时以当前磁盘最终状态为目标，不冒称只验证暂存区。

显式 --base/--head 用两端可解析本地 Git commit 比较，不自动 fetch 或猜测 upstream。Git ref 通过参数数组传递并校验为 commit，不拼接 shell。记录 base/head SHA；这表示两端状态，非逐 commit 过程验证。

先全量分析两端再比较 findings，覆盖跨项目引用与接口影响。源码/规则相同的稳定 finding ID 用于新增、既有、已解决匹配；没有规则变更或身份可比证据时不能把消失问题归为已解决。规则删除、例外变化、范围缩小另列 policyChanges。

基线构建或分析失败时保留目标结果，comparison 为 inconclusive；缺失基线证据不推断新增。删除、重命名、多项目影响和不同构建上下文均有 fixture。临时两端快照不通过反复 git checkout 修改用户工作区。

P5 添加 Linux CLI 验证、SARIF 与 opt-in CI 样例，上传报告 artifact。消费项目的工作流由用户选择应用，ArchSift 不注册 required checks 或修改仓库策略。自身 CI 检查源码、契约、fixture、包和回归。

## 工作包与实施顺序

| 包 | 依赖 | 具体任务与路径 | 完成证据 |
| --- | --- | --- | --- |
| W00 实施入口 | 本计划 | 在用户已创建的 ArchSift Local Project 中使用现有 D:\ArchSift 仓库；核对指令/Git/remote/初始提交，导入正式计划；记录迁移基准与许可证，创建 docs/migration 清单 | 目标计划、基准记录、源文件 hash、许可清单、保留已有仓库内容 |
| W01 基础项目 | W00 | solution、src 项目、global.json、Directory.*、锁定依赖、测试项目、AGENTS/README | Windows 构建和核心依赖方向测试 |
| W02 契约与模板 | W01 | Contracts、schemas、六类 templates、规则加载/语义/例外/Markdown、CLI rules 入口 | 六类有效/无效/重复/零匹配契约结果与可读模板 |
| W03 输入和项目图 | W01/W02 | Core 输入清单、project discovery、.sln/.slnx、中央 literal props/packages、框架和引用图 | 根边界、范围未覆盖、条件/表达式、编辑器产物及 source-change fixtures |
| W04 项目规则 | W02/W03 | 项目引用、完整性、TFM、直接 NuGet、项目/文件命名与 draft-rules | 每类 clean/violation/missing/unsupported 正负证据 |
| W05 程序集与构建 | W03 | ArchUnit 适配、Worker、manifest、existing/isolated、离线 restore、类型依赖/类型命名、缓存 | 真正 ArchUnitNET 正负例；旧产物、闭包缺失、TFM/config 不匹配和构建失败结果 |
| W06 报告和 CLI | W04/W05 | Core 服务、JSON/HTML、CLI analyze/verify/draft、退出代码、取消 | CLI 集成、输出转义、稳定 findings、部分报告和 errors |
| W07 UI | W02/W06 | Web loopback 服务和表单、规则 JSON 保存/MD 导出、项目图、运行/取消/历史与结果 | UI/CLI parity、当前运行错误清理、回路与路径边界 |
| W08 0.1.0 验收 | W07 | scripts smoke/safety/performance/ZIP；IFX 现场运行；人工任务和归档 | 首个完整版本 A01–A12 全部有证据 |
| W09 变更比较 | W08 | Core comparison、schemas/comparison、CLI changes、UI 比较 | 两端与工作区、跨项目、基线失败、删除/重命名、规则变化 fixtures |
| W10 0.2.0 报告接入 | W09 | SARIF exporter、Linux CLI 验证、samples/ci、文档和包验证 | 非阻断违规、可观察错误、跨平台核心结果一致 |
| W11 允许策略与大范围 UI | W10、0.2.0 发布后验收 | 项目/NuGet allowlist、稳定例外归因、目标上下文、结果信息层级和 UI 生命周期 | 130 项回归、八模板、真实 IFX CLI/UI、独立操作员及最终安全审计 |

W03 与模板文档等互不冲突任务可在工程允许时并行；本计划不要求开启子代理。W05 的真实 ArchUnitNET 检查必须在 0.1.0 内完成，不得用空壳或只有声明图代替。

Guard 迁移仅涉及分析算法、fixture、发现/框架解析与环境隔离经验。实现位置规则、源码语义扩展可留在迁移清单，不自动成为已交付能力。旧 Guard stale UI 缺陷记录为独立维护项，不作为新产品构建前置，也不在本实施计划中修改旧源。

### W11 发布后工作包

以下内容最初登记为发布后候选 `W11`，不改变 W09/W10 历史范围。W09、W10、正式发布和 [0.2.0 发布后验收](../acceptance/20261007-0.2.0-post-release-verification.md) 已完成，用户于 2026-10-07 明确授权启动；设计、实现、自动门禁、真实 IFX CLI/UI 与最终安全审计现已完成，状态为 `W11 COMPLETE`，见 [W11 完成记录](../acceptance/20261007-w11-allowlists-ui.md)。输入来自 [W08 完成报告](../acceptance/20261007-w08-completion.md) 的操作员反馈及随后规则复核，组合语义和兼容性先冻结在 [W11 允许策略决议](../decisions/20261007-w11-policy-composition.md)。

| 方向 | 后续目标 | 最低验收要求 |
| --- | --- | --- |
| 项目引用允许策略 | 在现有禁止规则之外，支持“所选源项目只能引用允许目标”的封闭策略；新项目或新层不得因未列入 denylist 而静默通过 | schema、模板、CLI/UI、JSON/HTML/SARIF 投影一致；clean/violation/零匹配/不完整引用、exact/glob、多规则集 fixtures；真实 IFX 只读复核 |
| NuGet 允许策略 | 支持按项目范围约束允许的直接 `PackageReference` ID，同时保留全局/局部 denylist；首轮不冒称覆盖传递依赖、版本范围或在线漏洞数据 | 包 ID OrdinalIgnoreCase；中央版本与声明不完整状态准确；允许项、未知项、禁止项和例外均有正负证据 |
| 冲突与组合语义 | 在实现允许规则前冻结并记录完整冲突矩阵。规则不采用“最后定义获胜”或文件顺序优先；所有适用约束同时成立，多条 allowlist 取交集，任何适用 deny 命中仍违规，真正放行只能通过有 ID、范围和理由的显式 exception | 规则文件及加载顺序置换得到相同核心结果；重复 ID、明显不可兼容约束和悬空例外仍为配置错误；广域允许加窄域禁止、多个允许集合、allow/deny 重叠、severity 和 exception 重叠均有确定结果与文档 |
| 例外确定性 | 消除多个 exception 同时匹配同一 finding 时依赖首个匹配项进行审计归因的隐含顺序；选择拒绝重叠或冻结稳定的唯一选择规则 | 例外排列顺序不改变合规和审计身份；报告明确被哪一条例外豁免及原因 |
| 目标上下文 UX | 页面醒目且持续显示产品身份与实际 target root、entry、rules、output；fixture/示例与真实目标使用不同标签，运行确认区避免把 ArchSift 示例误认成 IFX | 真实 IFX 与 fixture 的独立操作任务均能在执行前正确复述目标；错误根不会被静默接受为预期任务 |
| 大范围与术语 UX | 105 项目等大范围结果提供摘要、筛选和折叠；中文 UI 与英文任务/机器字段提供一致术语映射 | 桌面/窄屏复核通过；不隐藏 limitation、inconclusive、not-applicable 或真实项目计数 |
| UI 启停与证据 | 启动时说明 URL、当前等待状态、最终 JSON 产生条件及安全退出方式；服务退出、PID/端口跟踪和清理可审计，避免已完成复核后遗留候选进程 | 正常完成、取消、浏览器关闭、异常中断和重复启动 fixtures；最终无记录端口监听或候选进程，且不自动终止未经身份核对的进程 |

W11 设计阶段必须先产出规则组合决议记录和兼容性方案，再修改 schema。新增 allowlist 不得把“被某条规则允许”解释成绕过其他规则的授权，也不得从当前依赖图自动生成并默认为已批准策略。现有规则集须保持可读取，任何 schema/version 迁移必须显式、可验证且不静默改变旧报告结论。

### W00 完成状态

2026-10-06 的目标 Project 复核结果记录在 [W00 接手复核与交接报告](../acceptance/20261006-w00-takeover-review.md)。正式计划导入基准、Git/remote、SDK/reference packs、Guard/IFX HEAD、候选迁移文件哈希、P05 失败与清理证据以及 NuGet 缓存元数据均已重新核对。

后续 W00 完成记录见 [W00 完成报告](../acceptance/20261006-w00-completion.md)。用户明确确认 Guard 所有权与资产复用/分发权利，授权 `GUARD-OWNER-20261006` 已写入 [逐文件来源清单](../migration/source-inventory.json)。14 个候选源、测试及 fixture 已记录来源 commit/hash、迁移方式、选取范围、目标目录和验证入口；旧模块 fixture 的四个 JSON 仅为历史覆盖元数据，不直接迁移。项目 AGENTS.md 与 [第三方许可/notice 清单](../migration/third-party-notices.md) 已创建，完整许可文本、ArchUnitNET NOTICE 和候选包 nuspec 已归档。

当前结论为 `W00 COMPLETE / W01 NOT STARTED`。本轮只读核对 7 个候选缓存包及其 33 个 DLL 与本地 nupkg 字节均一致；不把它等同于新项目 restore/build、完整 NuGet 解析闭包或发布验收。来源清单标记 migrationExecuted=false，代码适配时再次复核并补实际目标哈希；W01 固定真实 lock/新增包，W08 核对实际分发资产与 runtime notices。未运行 Guard 安装或 IFX 集成，未创建 W01–W10 产品代码，也未提交、推送或修改远程。

以上为 W00 关闭时点记录。W00 随后已提交为 `5c378153abde38540dd81184706f92306091c53c`，历史 artifact manifest 继续保持该阶段快照。

### W01 完成状态

2026-10-06 已创建 solution、五个产品项目、三个测试项目、独立 SDK/中央包/显式离线源配置、八份 lock、开发检查脚本及 README。Windows x64 / SDK 10.0.303 / net10.0 / Debug 的离线首次恢复与 locked restore、八项目编译通过，零警告/错误；版本、CLI/Web 基础进程入口、项目和编译依赖方向共 9 项测试通过。每次命令后及最终 User/Machine environment、Process PATH、四个 Profile 哈希一致。

验收及修复历史见 [W01 基础项目验收](../acceptance/20261006-w01-foundation.md)，真实已解析包与新增许可见 [W01 依赖记录](../dependencies/w01/README.md)。当前 `W01 COMPLETE / W02 NOT STARTED`；W01 工作区尚未另行提交，未推送。引擎包身份与 DLL 身份已区分，真实 ArchUnitNET 架构规则仍由 W05 验收；没有开始 W02–W10 功能。

## 验证入口与完成条件

以下入口属于 W01–W08 要创建并实现的验证命令，现在不宣称其已存在或运行通过。新项目内部 fixture 回归可自动化；Guard × IFX 或本工作区真实现场集成继续采用人工一次一块已审查命令流程。

| 检查 | 计划入口 | 完成条件 |
| --- | --- | --- |
| 构建与单元/集成/架构测试 | dotnet build ArchSift.slnx；dotnet test ArchSift.slnx | 在记录的 SDK/平台通过，不引用 Guard 安装 |
| schema 和模板 | pwsh -NoProfile -File scripts/Verify-Contracts.ps1 | 全部有效/无效例按预期；MD 与 JSON 一致 |
| 功能正负例 | pwsh -NoProfile -File scripts/Verify-Fixtures.ps1 | 六类规则均验证行为、覆盖和失败状态 |
| 安全回归 | pwsh -NoProfile -File scripts/Test-HostSafety.ps1 | 无 User/Machine 环境/Profile 修改；隔离 .NET 子进程不能向 User PATH 注册工具 |
| 性能测量 | pwsh -NoProfile -File scripts/Measure-Performance.ps1 | 记录硬件、SDK、输入规模、冷/热缓存、耗时、内存和取消表现 |
| 包与 CLI/UI | pwsh -NoProfile -File scripts/Test-PackageSmoke.ps1 | 解压新目录、迁移路径、无 Guard 前置、按报告语义返回代码 |
| 本地打包 | pwsh -NoProfile -File scripts/Publish-LocalZip.ps1 | win-x64 自包含包、版本一致、依赖/许可清单、SHA-256，离线启动 UI |
| IFX 与人工可用性 | docs/acceptance/ifx-local-analysis.md 中逐步命令及 UI 任务 | 已记录输入、报告、用户评分和清理证据 |

新产品 self-contained 包不代表目标构建无需 SDK/targeting packs。只有项目声明分析的用户无需安装用于编译的 SDK；启用构建的用户需满足目标 prerequisites，工具准确报告缺失。

0.1.0 的验收项：

| ID | 标准 |
| --- | --- |
| A01 | 无规则时能发现支持的项目图，含 unlisted/external/unresolved/unsupported 及上下文。 |
| A02 | 六类规则模板、schema、JSON/MD 和配置覆盖语义一致，错误配置拒绝。 |
| A03 | 每类规则都有有效正例、违规反例、无法判定及边界案例；真实 ArchUnitNET 类型依赖证据成立。 |
| A04 | 多 TFM、中央 props/packages、条件/表达式、不支持项目均得到准确范围与状态。 |
| A05 | 缺失/过期/无源码绑定 DLL、闭包缺失、配置变化及编译失败不能产生当前源码合规。 |
| A06 | IDE 开启和普通生成产物不造成无关失效；保存相关输入时提示变化。 |
| A07 | 同一输入与规则的核心报告一致；CLI/UI 一致；输出无伪造路径/行号，HTML 转义正确。 |
| A08 | findings 默认 exit 0；错误、无法判定与取消准确非零；失败/取消后的 UI 无旧成功残留。 |
| A09 | 目标分析只读、根边界有效、子进程环境隔离；原环境/Profile 和 Guard/IFX checkout 按测试基准不变。 |
| A10 | IFX 与构造违规样例形成真实报告；至少一名未参与实现的操作者独立运行并理解结果，信心 ≥4/5，最高 L2。 |
| A11 | 性能测量后，W08 先提交 docs/performance/budget.json 冻结参考输入/配置和阈值，再验证候选包；超预算需优化或明确调整依据。 |
| A12 | ZIP 独立解压启动 CLI/UI，版本、哈希、运行限制与第三方清单齐全；无持久 PATH/Profile 修改。 |

L0 为独立完成，L1 为一般提示，L2 为定位到文档或解释一次错误，L3 为逐步点击/字段指导，L4 为代操作。记录操作者真实分数与辅助等级，不能由实现者代填。若无独立操作者可用，代码验证可继续，但 A10 保持未通过。

性能预算作为 W08 的工程测量产物自动推进，不重新打开 Q13。归档原始结果和多次代表性运行，记录选择阈值的依据；不将 build/restore 时间混入单纯解析耗时，也不依靠单次最佳值证明达标。

## 运行目录与安全回归

代码仓库为 `D:\ArchSift`。本地真实测试产物统一置于外部 `D:\ArchSift-lab` 下的 runs、fixtures、packages、evidence、archives 子目录，避免污染目标或新仓库。开发构建 bin/obj 由 Git 忽略；现场 Target 构建用外置快照。进入这些路径时使用执行环境允许的正常审批机制，不通过别的工具绕过权限。

真实现场测试前记录原 User/Machine 环境、Process PATH 与四个 PowerShell Profile 的哈希/存在性，只输出 equality 与哈希。安装、可能启动 dotnet 的操作、清理以及测试结束后分别比较；任何持久变化立即停止并保留证据，不自动修复。

回归覆盖拒绝持久环境 mutation API、isolated DOTNET_CLI_HOME 与 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0、父进程环境不变、输出路径越界、符号链接、构建失败与取消。产品默认流程不要求用户手工维护短期 proof。

清理只处理精确记录的工具临时产物，不对源码根、仓库根或宽泛路径递归删除。若测试使用 worktree，通过 Git 正常移除，并确认注册项与磁盘目录同时消失；沙箱拒绝记录为基础设施结果，不归为产品违规。

## 延期项目与重启条件

| 功能 | 当前限制 | 重新评估条件 |
| --- | --- | --- |
| 自然语言/Agent Plan 验证 | 不提供 Plan 合规承诺 | 核心及变更验证稳定，有明确结构化预期变更契约或单独 LLM 工作流设计 |
| 数据库 schema/SQL/迁移验证 | 不连接数据库或运行迁移 | 独立数据库规则需求与只读输入范围已定义 |
| 旧 .NET Framework、F#、VB | 报告不支持，未承诺编译/语义覆盖 | 对应解析器、toolchain 和 fixtures 就绪 |
| MSBuild 完整求值 | 声明模型标明不支持的条件与表达式 | 明确隔离执行、配置维度和跨机器求值证据 |
| NuGet 版本范围、传递依赖 | 首期只做直接包 ID | 依赖解析与离线数据来源契约就绪 |
| 正则、自定义脚本/插件 | 只支持标准模板 exact/glob | 模板不足的真实需求且扩展边界已验证 |
| 接口实现位置与更多源码语义规则 | 不包含在 0.1.0 六类清单 | 核心规则准确且有独立正负 fixtures |
| NuGet tool 分发 | 本地 ZIP | 0.1.0 验收完成，包名/许可证/发布流程核对 |
| fail-on、强制 CI 与远程治理 | 消费 CI 默认仅报告 | 显式新需求及独立策略设计；不自动恢复旧完整门禁 |
| 逐 commit 检查与增量优化 | 两端完整状态比较 | 两端模型稳定，性能测量证明需要优化 |

## 风险与计划变更

主要工程风险为 declaration 与实际构建差异、程序集来源不足、离线包缺失、IDE/生成输入选取不一致、误用零匹配与缓存，以及 JSON/HTML/UI 的结果漂移。通过范围标识、统一输入清单、真实 ArchUnitNET 正负例、逐规则 coverage 和共享报告处理。

来源许可证不足、现有仓库包含未归属改动、不可复现构建输入或主机安全漂移时，在对应任务停止并记录，其他独立工作可继续。普通实现选择如类拆分、内部算法与包选择在已批准范围内由实施者决定并留记录。

改变首期规则、默认网络、目标写入权限、强制 CI、平台承诺或延期范围时更新本计划并说明影响；不能以“已有 Guard 功能”为理由扩大范围。未通过验收不得将阶段或版本标为完成。

## 决议与交付记录

| 日期 | 事项 | 状态 |
| --- | --- | --- |
| 2026-10-06 | D01 至 D15 的产品方向与独立项目 | 已认可 |
| 2026-10-06 | Q01 至 Q14 全部默认值 | 用户统一采纳，全部 CLOSED |
| 2026-10-06 | 正式计划与 draft 归档 | 文档交付；代码实施未开始 |
| 2026-10-06 | 用户确认 D:\ArchSift 为其新建的空 Git 仓库，并已创建 Codex Local Project ArchSift | 实施入口确定为目标 Project；计划在目标 Project 导入并持续维护 |
| 2026-10-06 | W00 接手复核 | 计划/Git/工具链/来源访问条件已复核；READY TO COMPLETE W00；迁移权利、逐文件来源与 notices 尚未关闭；W01 未开始 |
| 2026-10-06 | W00 实施入口关闭 | 用户 Guard 权属/复用/分发授权已记录；14 项来源清单、7 项第三方许可清单/归档、项目 AGENTS 与交付验证完成；W00 COMPLETE，W01 NOT STARTED |
| 2026-10-06 | W00 本地提交与 W01 授权 | 用户要求“提交，然后开始W01”；W00 commit `5c378153abde38540dd81184706f92306091c53c`，保留人类主作者并附 Codex trailer；W01 IN PROGRESS |
| 2026-10-06 | W01 基础项目验收 | 八项目离线恢复/locked restore/Debug 编译及 9 项测试通过；主机哈希不变；W01 COMPLETE，W02 NOT STARTED；W01 修改未提交 |
| 2026-10-06 | W01 提交及后续执行授权 | W01 commit `62bace39cf6acad76c79327ec2ec91ea1c19772d`；用户授权继续后续计划，每阶段完成后提交；推送仍未授权 |
| 2026-10-06 | W02 契约与模板 | 四份 schema、DTO、六类模板/Markdown、组合/例外/冲突校验、CLI rules 入口；56 项测试与独立 Test-Json/CLI 验证通过；见 [W02 验收](../acceptance/20261006-w02-contracts.md)；W02 COMPLETE |
| 2026-10-06 | W02 本地提交 | `424ed1945fbc77cabd82fff8f0198d94b816a1a4`，附 Codex trailer |
| 2026-10-06 | W03 输入与项目图 | 声明发现/根边界/来源身份与编辑器变化 fixtures；64 项测试通过，主机哈希不变；见 [W03 验收](../acceptance/20261006-w03-discovery.md)；W03 COMPLETE |
| 2026-10-06 | W03 本地提交 | `31a3fdede0c93cdc6a235468516a3dea45816d24`，附 Codex trailer |
| 2026-10-06 | W04 项目规则 | 项目规则/命名/例外/规则建议正负与缺失 fixtures，79 项测试通过；见 [W04 验收](../acceptance/20261006-w04-project-rules.md)；W04 COMPLETE |
| 2026-10-06 | W04 本地提交 | `b86c326e497e0d6ea680d3fbb4b8d0025f3c82ea`，附 Codex trailer |
| 2026-10-06 | W05 程序集与构建 | 真实 ArchUnitNET 正负及 module initializer 未执行、isolated/manifest/Worker/闭包负例，83 项回归通过；保守 snapshot/unsupported build 范围明确；见 [W05 验收](../acceptance/20261006-w05-assemblies.md)；W05 COMPLETE |
| 2026-10-06 | W05 本地提交 | `304e1623468c610a2c98d1eae8aa1cc70cc674cc`，附 Codex trailer |
| 2026-10-06 | W06 报告和 CLI | 共享服务/JSON/HTML/CLI/取消/部分结果，89 项测试通过；见 [W06 验收](../acceptance/20261006-w06-reports-cli.md)；W06 COMPLETE |
| 2026-10-06 | W06 本地提交 | `9f7c24dcf0c9d33301e68212b7bd22a187708ee0`，附 Codex trailer |
| 2026-10-07 | W07 本机 UI | 90 项回归、真实 HTTP 安全/parity、浏览器模板/违规/失败旧状态清理与桌面/窄屏验证；见 [W07 验收](../acceptance/20261007-w07-workbench.md)；W07 COMPLETE；独立操作员 A10 尚未执行 |
| 2026-10-07 | W07 本地提交 | `a9f17338a253289ad3d55dac3b35618617426800`，附 Codex trailer |
| 2026-10-07 | W08 性能冻结 | `64b6e238513a94d98f0f04d78cfd3dc47c1eb9f3` 先提交实测参考与预算，再测候选包 |
| 2026-10-07 | W08 自动验收/人工入口 | 92 项回归、host safety、五次候选性能、self-contained CLI/Worker/UI 无 SDK 路径 smoke 通过；候选 ZIP/hash/许可已归档；见 [W08 准备](../acceptance/20261007-w08-readiness.md) |
| 2026-10-07 | W08 真实 IFX/A10/最终清理 | 105 项目声明发现、已知引用违规、fixture 与真实 IFX UI、独立操作员 confidence 4/5（最高 L2）及最终零残留审计通过；见 [W08 完成报告](../acceptance/20261007-w08-completion.md)；W08 COMPLETE，W09/W10 依赖已满足但尚未开始 |
| 2026-10-07 | 发布后下一目标登记 | 用户要求将 W08 的目标上下文、操作等待/JSON 时机、残留 UI 进程、大范围展示与术语反馈，以及项目引用/NuGet 允许列表、冲突和例外确定性纳入后续；登记为 W11 候选，仅在 W09/W10、发布和发布后验证完成后另行启动 |
| 2026-10-07 | 0.2.0 发布后验收 | 正式三资产、发布包行为、固定源码 Windows/Linux、最终托管 CI、合成 UI、真实 IFX 已知违规与人工 UI、三格式报告及最终零残留审计通过；验证器失败与 CI/partial 限制保留；见 [0.2.0 发布后验收](../acceptance/20261007-0.2.0-post-release-verification.md)；发布后验收 PASS，W11 仍未启动 |
| 2026-10-07 | W11 启动与设计门禁 | 用户在发布后验收提交并推送后授权启动 W11；先冻结新增项目引用/NuGet allowlist、交集与 deny 优先、稳定例外归因、旧 schemaVersion 1 兼容及完整测试矩阵；见 [W11 允许策略决议](../decisions/20261007-w11-policy-composition.md) |
| 2026-10-07 | W11 自动实现门禁 | schemaVersion 1 增加两个兼容分支及 JSON/Markdown/UI 模板；Core 支持封闭项目/NuGet 直接依赖策略和稳定例外归因；HTML/UI 显示例外 ID/理由；UI 固定每个 job 的目标快照并增加大范围筛选/折叠与安全关闭；首次 IFX UI 复核准确指出项目卡片已折叠但 105 条 coverage limitation 仍展开，保留为 NEEDS FIX 后改为可见计数/影响说明加按需展开；操作员随后要求进一步区分项目、规则结果、违规证据和覆盖限制，现已拆为四个结果语义区及独立项目清单区，桌面/480px 合成视觉验证通过且窄屏无横向溢出；SDK 10.0.303、Debug、离线 restore/build 0 警告错误，97 unit + 31 integration + 2 architecture 全通过，8 个模板独立校验/渲染通过，主机状态一致；最终自动证据 `D:\ArchSift-lab\runs\development-53b4733726dc4624a6a46b063d9f4997`；真实 IFX CLI 已通过，W11 等待新的短 UI 复核与最终安全审计 |
| 2026-10-07 | W11 人工 UI 与最终关闭 | 真实 IFX 最终 UI 显示 105 项目、0 程序集、`partial / noncompliant / exit 4`、两条规则/两条 finding 与默认折叠的 105 条 limitation；信息区分、项目筛选、窄屏和三格式入口由独立操作员以 L1、confidence 4/5 通过。最终审计精确清理一个弃用会话残留 PID/端口后，自动/CLI/UI/report/SARIF、产品与 IFX 工作树、宿主环境、进程/端口/容器及 24 份关键证据全部通过；见 [W11 完成记录](../acceptance/20261007-w11-allowlists-ui.md)；W11 COMPLETE，未提交、推送或发布 |
| 2026-10-07 | 0.3.0 正式发布 | tag `v0.3.0` 固定源码 `d1b1d7c`；Windows/Ubuntu 各 130/130 CI；88,917,483-byte Windows x64 ZIP SHA-256 `0202e9734ec990474c415d4152274db93337a768e51837ef968fc4b539e44c79`，三项远程资产 digest/下载字节一致，实际下载包完整 smoke、官方 SARIF schema、性能及宿主状态通过；见 [0.3.0 发布记录](../releases/0.3.0-publication.md)；PUBLISHED / VERIFIED |

## 参考资料

2026-10-09 0.4.0 后续：替代包 `a561e7d` 的真实 IFX 浏览器验收六项均为 yes、操作员可用性 4/5，先前的语言/安全关闭对齐和规则集卡片过长问题已修复，目标与宿主状态保持不变；见 [第二次 step 04 记录](../acceptance/20261009-0.4-ifx-step04-browser-retest.json)。操作员随后发现 182 条规则在编辑器中全部展开，已纳入逐规则默认折叠、ID/类型筛选及新候选定向复测。0.4-F 最终门禁尚未关闭，不声明版本验收或发布完成。

编辑器修复已提交为 `bb599bd`，新候选在 Windows/Ubuntu CI 各通过 142 项，本地 Debug/Release、两次原生包 smoke、未公开草稿资产独立下载回验和 IFX 只读预检均通过；见 [新候选准备记录](../acceptance/20261009-0.4-editor-candidate-readiness.md)。等待原操作员对新包进行定向浏览器复测，随后才可推进剩余验收门禁。

2026-10-07：[0.2.0 发布记录](../releases/0.2.0-publication.md)已关闭：GitHub tag `v0.2.0` 指向 `528ebb1`；ZIP SHA-256 `ee2d27b4d7689ce3eef75c0d3402c684ad1c9a8cb492ae7c8e915510038699d8`，三资产 uploaded、digest/下载字节一致，发布后 native smoke 通过。W09 `0286b75`、W10 初始 `8dc363e`、补充修复 `26498f4` / `eb52640` / `528ebb1`、固定验收 `27a5719` 均保留原历史并附 Codex trailer。发布关闭时 W11 尚未授权；随后发布后验收通过，用户另行授权并完成独立 W11 范围。W11 当前尚未提交、推送或发布。

2026-10-07：用户授权开始 W09/W10、继续逐阶段完成后提交，并授权后续发布；明确选择 MIT 许可证。W09 完成提交 `0286b75`，106 项回归通过；W10 预算先冻结为 `63419d4`。[W10 验收](../acceptance/20261007-w10-report-integration.md)记录 Windows 109 项、Linux 107 项、核心 parity、SARIF 官方 schema、真实包/浏览器与 CI 样例。正式包独立构建/校验后才能发布，不上传未提交源码原型；W08 候选及历史验收不改写。

- [Draft 历史](20261006-dotnet-architecture-analyzer-draft.md)。
- [Guard 项目发现](C:/Users/von12/OneDrive/Desktop/Guard/core/host/V4.Guards.Host/ProfileRuntime.cs)。
- [Guard 架构分析适配器](C:/Users/von12/OneDrive/Desktop/Guard/modules/architecture-conformance/adapter.ps1)。
- [Guard 构建证据](C:/Users/von12/OneDrive/Desktop/Guard/modules/build-evidence-provider/adapter.ps1)。
- [P05 最终验证](D:/IFX-10-Root/guard-lab/archives/p05-20261006-final-verification.json)。
- [P05 最终安全审计](D:/IFX-10-Root/guard-lab/archives/p05-20261006/cleanup/h4-final-safety-and-cleanup-audit.json)。
- [ArchUnitNET 官方说明](https://github.com/TNG/ArchUnitNET/blob/main/README.md)及[构建配置限制](https://archunitnet.readthedocs.io/en/latest/limitations/debug_artifacts/)。
