# ArchSift.InProject IP00-A：组件、身份与测试兼容契约

日期：2026-10-14（Australia/Sydney）。适用范围：ArchSift.InProject 0.1.0。依据：[产品规格](20261014-archsift-inproject-0.1.0-product-spec.md)与[实施计划](../plans/20261014-archsift-inproject-0.1.0-implementation-plan.md)。本记录冻结组件和测试边界；构建关联、路径和门禁字段分别由 IP00-B–E 冻结。

## 产品与包身份

| 身份 | 契约 |
| --- | --- |
| 产品 | `ArchSift.InProject`，首版 `0.1.0`，发布标签 `inproject/v0.1.0`；既有 ArchSift 的 `v…` 标签与版本独立。 |
| 主入口 | Windows x64 `archsift-inproject`；`gate`、`status`、`ui` 为同一产品命令，CLI 和 loopback Web 调用同一个任务服务。 |
| 目标安装 | 目标仓库 `.archsift/version.lock.json` 固定产品版本、契约版本、包摘要和所含共享组件摘要，不追随 latest。 |
| 分发 | 一个自含的 InProject Windows x64 载荷，包含主入口、共享 Core／Contracts／ArchUnitNET Worker、测试桥接、固定测试依赖和许可；不改变已有 ArchSift 包的身份或字节。 |
| 源码身份 | 每包记录产品 Git 提交、产品输入摘要、共享组件输入／程序集摘要、依赖锁和 manifest。不同产品可复用同一共享源码提交，但发布和回退分别管理。 |

版本锁和 manifest 的具体 JSON 字段、路径及失效规则归 IP00-B；IP07 才冻结正式候选包和升级／回退／移除行为。IP01 使用具有明确身份与摘要的本地开发包验证初始化，不将开发包冒充正式发布资产。

## 项目与依赖方向

新增工程按以下边界实施；名称是项目身份，实际文件路径由对应工作包建立：

| 项目 | TFM | 职责与允许引用 |
| --- | --- | --- |
| `ArchSift.InProject` | net10.0 | CLI 与 loopback Web 入口；引用 `ArchSift.InProject.Runtime` 和共享 Contracts，不实现规则判断。 |
| `ArchSift.InProject.Runtime` | net10.0 | InProject 专属任务、worktree、门禁、证据与测试编排；引用共享 `ArchSift.Core`、`ArchSift.Contracts` 和 `ArchSift.ArchUnit`，不引用 xUnit。 |
| `ArchSift.InProject.TestBridge` | net10.0 | 专用测试项目的薄桥接 API；引用共享 Contracts，不引用 Core、宿主或业务程序集，不自行核验／设置 `SourceBound`。 |
| `.archsift/tests/ArchSift.InProject.ArchitectureTests` | net10.0 | 目标仓库跟踪的专用 xUnit 项目；引用固定桥接载荷与固定测试包，JSON 驱动可识别的逐 B 类规则测试；不加入业务 solution。 |

既有 `ArchSift.Contracts` 是共享机器 DTO；`ArchSift.Core` 继续只依赖 Contracts 和分析接口；`ArchSift.ArchUnit` 持有 ArchUnitNET 适配与 Worker 客户端。既有 ArchSift CLI／Web 及 InProject 宿主分别组合共享服务。InProject 专属 worktree 管理、xUnit、门禁策略和项目内写入权限不得下沉为 Core 的默认行为。任何共享代码变化需要两产品适用回归。

首期复用既有 `ArchSift.Cli` 的 `__worker` 进程入口作为 InProject 包内固定的内部 Worker 载荷，由 Runtime 经已有 `AssemblyWorkerClient` 启动。包 manifest 锁定这份内部载荷及其依赖摘要；用户入口仍是 `archsift-inproject`。若后续抽出独立 Worker 可执行文件，必须保持相同 DTO 语义并重新执行双产品回归，不在 IP00-A 静默改变已有 ArchSift CLI 行为。

## 专用测试与 Worker 边界

1. 门禁服务固定规则字节、必检清单和由 IP00-C 验证的本次编译上下文。只有验证器能构造可用于当前源码结论的内部 `AssemblyInputs`；测试桥接不接收可自行设为 true 的 `SourceBound` 参数。
2. Runtime 对本次所有 B 类规则与项目上下文调用共享 Worker，按上下文复用已加载的程序集模型。Worker 的真实 ArchUnitNET 结果连同规则 ID、上下文、finding、错误和输入身份写入本次受控材料。
3. 门禁显式构建并运行专用 xUnit 项目。每条启用 B 类规则至少有稳定、可发现的测试身份；桥接按规则 ID 读取本次受控结果并在测试输出中保留实际 finding 或无法判定原因。专用测试项目不得以项目引用或测试发现触发业务 solution、目标应用或其他业务测试。
4. Runtime 独立核对预期规则、xUnit 发现／执行数、测试结果与 Worker 结构化结果。xUnit 的绿色摘要、跳过或零用例不能单独给出门禁放行；分析结果和门禁决定分别保存。A 类规则继续由共享 Core 评估，同一必检集合核对 A／B 覆盖。

Runtime 到专用测试的受控上下文采用版本化 JSON 协议。最小信封包含 `schemaVersion`、`runId`、`productVersion`、`rulesetSha256`、`inputIdentitySha256`、`contextIdentitySha256`、`expectedRuleIds`、`resultMaterialSha256` 和 `workerIdentitySha256`。测试回执包含同一运行及规则身份、发现／执行计数、逐规则测试终态和结果材料摘要。IP00-C 冻结每个摘要的实际覆盖与取证来源；IP00-D 冻结终态、漏执行和退出码。未知协议版本、身份不符、材料缺失／篡改、Worker 失败及零测试均为阻断，不能回退到旧结果。

## TFM 与固定测试依赖

专用测试项目固定 `net10.0`。net8.0／net9.0／net10.0 是业务编译目标和程序集上下文，不是专用测试项目的 TFM。桥接在 net10 测试进程中传递数据；元数据分析由 net10 Worker 在独立进程完成，业务项目不引用 Core、桥接或 xUnit，也不因安装本产品改变业务 TFM。

首期测试包沿用仓库已锁定并在本机可用的 `xunit` 2.9.3、`xunit.runner.visualstudio` 3.1.4、`Microsoft.NET.Test.Sdk` 17.14.1，使用 VSTest 模式和专用测试项目自己的 lock；ArchUnitNET 保持 0.13.4。版本不会因为新上游版本出现而自动升级。目标需具备 .NET 10 工具／测试运行时及与各业务 TFM 匹配的 SDK／targeting packs；restore 默认离线、使用明确本地来源。缺少任何前提按缺失类型报告，不能把测试未运行写成 pass。

| 业务项目 TFM | 专用测试／桥接／Worker | 可形成结论的前提 | 后续实际验证 |
| --- | --- | --- | --- |
| net8.0 | net10.0／net10.0／net10.0 | net8 构建上下文与引用包、实际编译及依赖闭包已关联 | IP02 构建关联；IP03 真实 xUnit／Worker 正反例 |
| net9.0 | net10.0／net10.0／net10.0 | net9 构建上下文与引用包、实际编译及依赖闭包已关联 | IP02／IP03 同上 |
| net10.0 | net10.0／net10.0／net10.0 | net10 构建上下文与引用包、实际编译及依赖闭包已关联 | IP02／IP03 同上 |
| 其他／缺少 SDK 或测试运行时 | 不执行或仅保留已完成的独立结果 | 对应必检范围为 error／inconclusive，门禁阻断 | IP02／IP03 缺失与不兼容反例 |

本机只读清点：SDK 9.0.314、10.0.301／10.0.303；.NET 8／9／10 运行时和引用包存在，没有 .NET 8 SDK。SDK 10 可针对较早 TFM 构建的能力由官方 [.NET SDK 版本说明](https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs)支持；实际目标的 global.json、构建任务和测试运行环境仍需在 IP02／IP03 实测。官方 [VSTest `dotnet test` 文档](https://learn.microsoft.com/dotnet/core/tools/dotnet-test-vstest)明确测试项目指定 TFM，且多目标测试需明确选择框架。本阶段不把框架／包兼容元数据当成 InProject 已执行测试的证据。

## IP00-A 检查与后续门槛

现有 `Directory.Packages.props` 与 architecture test lock 固定上述测试包；`ArchSift.ArchUnit` lock 固定 ArchUnitNET。2026-10-14 使用 SDK 10.0.303、net10.0、Release，对现有 architecture test 项目以明确本地包源完成 locked restore，随后 2/2 测试通过；构建与运行前后 User／Machine 环境、Process PATH 和四个 Profile 的综合哈希相等。隔离执行中的首次 restore 返回 1 且没有 NuGet 诊断；同一参数在正常审批执行环境通过，该失败保留为基础设施记录，不计为产品测试通过。首次 .NET 子进程还输出了“Installed an ASP.NET Core HTTPS development certificate”；环境／Profile 哈希不覆盖证书存储，且没有运行前证书基线，因此本记录不声称证书状态未变化，也不自动清理它。此检查验证现有共享组件依赖方向和 net10 VSTest 基线，尚未验证新增 InProject 桥接、net8／net9 业务目标或完整门禁。

IP00-B 可以基于此组件边界设计 schema 与 owned 路径。IP02／IP03 必须分别以 net8／net9／net10 的独立真实编译与专用 xUnit 发现／执行、Worker 正反例关闭上表；任一组合失败时不能继续声称该 TFM 获支持，也不能进入固定候选验收。
