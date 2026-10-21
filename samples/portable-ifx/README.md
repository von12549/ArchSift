# 可移植接入与当前使用说明

SP00–SP05及CL04正式采纳已经完成。CL04的历史验收基点为 `a2adb87e5e54c69c62644f1ede9de5efde2381dd`，见[实际main验收](https://github.com/von12549/IFX-New/actions/runs/39543191970)；后续IFX说明由[PR #15](https://github.com/von12549/IFX-New/pull/15)正式交付至 `f58205b2c1edd4887811d4feed553d61ebddd69a`，当前本地IFX也在该基线。现役 `ArchSift.Testing` 和本地工具 `ArchSift.Cli` 固定 **0.10.0-cl03.1**，Windows x64、指定SDK **10.0.303**、PowerShell **7.6.6**、net10.0、Release。四个库按 Testing → ArchUnit → Core → Contracts 恢复，CLI独立携带运行闭包，不要求Web或ArchSift源码；前一sp03.1组合的正式采纳见[SP05交付](https://github.com/von12549/IFX-New/pull/12)。

本说明仅描述已批准接入及使用边界，归档范围不包含相邻11个旧模板文件。那些代码、工具／包批准配置、入口和工作流作为本机未提交的 `0.10.0-sp03.1` 历史材料保留；本文档不交付可直接复制的整套模板。日常操作以[IFX固定正式说明](https://github.com/von12549/IFX-New/blob/f58205b2c1edd4887811d4feed553d61ebddd69a/docs/architecture-portable.md)及IFX仓库自身已批准的清单、政策、锁和入口为准。固定说明中的“本轮仅本地校正”等文字保留其编写时点，后续正式交付事实以PR #15及其main验收为准。

本机旧模板的 `required-cases.json` 从SP00已批准映射逐项提取，原80分组为17＋16＋47，新增基础检查3及夹具9单列；其来源记录保留在本机 `docs/acceptance/20261019-sp00-boundary.json`。现役清单以IFX批准文件为准，JSON必检集合由现役政策的20条启用规则推导，Registry4四条不包含在内。本说明不重新学习案例身份、不更新包版本或清单，完整程序集类型版本、参数类型和值保持；真实执行与静态清单检查分别记账。

现役包引用形式如下；其他目标仅在获准接入时使用，保留原测试、Theory行、五秒断言、API快照、项目范围及成员调用/Handler检查：

```xml
<PackageReference Include="ArchSift.Testing" Version="[0.10.0-cl03.1]" />
```

使用中央版本管理时，版本写入 `PackageVersion`，引用本身不写版本；不同时声明两个版本。保留 xUnit v3 4.0.1 和 `UseMicrosoftTestingPlatformRunner=true`，不升级框架。本机保留的相邻工具／批准／入口及案例清单示例冻结为前一 `0.10.0-sp03.1` 组合，原字节保持，未纳入本说明的交付范围；当前IFX以自身已批准的 `.config/dotnet-tools.json`、`architecture/portable-gate.json`、包批准／源清单及精确锁为准，不将旧示例整份覆盖现役配置或只改单个版本字段。其他目标安装时合并已有清单，保留原 `dotnet-ef 10.0.12` 等条目；批准检查精确核验 ArchSift 条目、拒绝重复／其他 ArchSift 工具及命令冲突，其他工具仍由目标仓库负责。现役Testing十包批准闭包及89份锁已在CL04核对，非ArchSift依赖保持；其他目标须核对实际锁，不能自动扩大批准。本说明不改任何配置、锁、政策或CI。

IFX现役声明适配器保留正式 `DeclarationInputs.Validate` 调用及以消费工程目录解析引用的行为，并使用包批准；必须保留IFX原 `Infrastructure/DeclarationInputs.cs` 和相关解析实现。本机旧 `FoundationDeclarationChecks.cs` 是未交付的历史模板，不能用它覆盖现役实现。原3项检查和9个夹具继续使用。Testing包中的 `PackageToolApproval` 校验精确声明、版本、锁定内容身份、完整工具依赖闭包和本地CLI清单；条件、override、任意工具包、缺依赖或改内容均拒绝。业务工程不获得测试工具包批准。没有把A类强制转换为JSON。适配器遵守IFX原有花括号／未用using检查，不能通过禁用分析器接入。

入口放在目标的 `eng/Test-Architecture.ps1`，旁置 `HostSafety.ps1`；根从入口自身及 IFX.sln 定位，与 CWD 无关。获准迁移后的普通调用形式：

```powershell
pwsh -NoProfile -File <目标根>/eng/Test-Architecture.ps1 -PackageSource <批准离线包源> -StateRoot <源码根外状态目录>
```

入口固定本地工具版本并核验源包与恢复后工具文件字节；本地工具使用状态根中生成的NuGet配置清除其他源，仅允许指定源。配置位于 `config/<run-id>.json`，`runs/<run-id>` 及其 CLI home 必须在启动前不存在，由 gate 创建。日常每次正常恢复、一次增量构建、一次全工程测试及覆盖核对。IDE 可直接恢复/运行原测试工程，使用相同 NativePolicy 与身份 hook，不要求启动 CLI 或 UI。

配置版本2专用于 `requiredCases`＋摘要；不能同时传旧 baseline/mapping/toolRoot。版本1保持旧契约。清单v1固定 runner/SDK/TFM/包组合；实际输出通过已验证的 xUnit4.0.1 MTP 适配关联 xUnit case ID 与 discovery UID，再关联 execution ID；三域各自保留，只对本次运行有效。未知、重复、缺失、同数替换、skip、取消、政策篡改及报告错误均不能完整通过。普通运行不生成或改写清单。

报告继续使用 architecture-test-run v1 的兼容扩展：新 `inputs.caseManifest` 和 `testScope.expectedLogicalCaseKeys`，不伪造旧映射/SourceBound。每项保留三种 runner 字段；未知清单/报告版本显示 unavailable。JSON 为权威，HTML/UI读同一结果，失败不使用历史成功补足当前覆盖。

离线架构报告的 Audit 为 `not-run`。独立正式审计仍须 true/all/moderate 及原警告策略；按实际目标包依赖重算，CLI 的独立 deps.json/包分发闭包另审，不以测试工程恢复或旧109项目数替代。

先备齐批准离线数据目录中的251个原始nupkg及指定SDK。现役数据目录为 `D:\ArchSift-lab\packages\cl04-ifx-offline-20261021-01`，可换绝对目录；执行前依IFX `architecture/package-source.json`核对完整文件名及SHA-256，缺包／改包／额外未批准包停止，不回退公网，不把缓存存在当完整源。离线清单分别记录原始包字节SHA-256／SHA-512和SDK计算的NuGet内容哈希，后者排除签名元数据，用于assets／lock核对，两者不能直接比较。旧误报记录保留。任意冷机器未经这些准备不保证restore。

日常一次全量为92个C#＋20个JSON，共112；原80分组、Theory参数、五秒阈值和API快照保持，快照更新强制关闭。三项A由批准C#承担，Registry4政策已采纳待启用。`notIncluded`沿用“未作为JSON执行”含义；剩余清单不能等同全部无检测。CL02责任投影修正已随 `0.10.0-cl03.1` 经CL04实际main验收在IFX生效，仅对应本次完整运行通过的案例显示pass，不用历史成功补本次结果。匹配Web ZIP仅为本地已验候选，未由IFX消费或公开发布。

正常IFX CI已采用可移植入口和独立Audit，三个必需检查为 `IFX Windows architecture`、`IFX dependency audit`、`IFX architecture acceptance`，绑定同次SHA/run/attempt及Actions来源、strict与管理员限制；重型 `ci.yml` 保持。CL04实际main a2adb87的正常push run39543191970／attempt1已通过112/112、独立105/105/0及CLI审计和三项检查；PR #15的[实际main f58205b验收](https://github.com/von12549/IFX-New/actions/runs/39550973000)也已完成。以上为既有验收，本说明不代表新执行。未来说明交付仍须按单独授权走PR/main正常自动CI，无文档豁免，不需新包，不另手工重跑SP00–SP05，不skip或调整保护。

普通版本回退基线是前一已验可移植完整组合（`0.10.0-sp03.1`、入口、锁、原251包、全部已采纳policy/cases及现行三项保护），保留并行政策；现役仍为 `0.10.0-cl03.1`。更早源码／props／映射／profile仅为另授权迁移级备用。包、旧组合、历史、缓存、refs、原本地备份和精确27项例外保持。维护者保留原包、摘要及许可，新版本另建源和验证；保留期限待定。ArchSift产品源码43文件已由[PR #9](https://github.com/von12549/ArchSift/pull/9)正式交付到main3f61be0，根README／报告说明已由[PR #10](https://github.com/von12549/ArchSift/pull/10)交付到main5e6c4cd，当前ArchSift本地也在5e6c4cd；这些事实不构成旧包精确编译器输入闭包、逐字节可重现构建或新包／Web发布声明。其余未提交材料及旧CL01草稿保留，本文档归档不包含这些材料、旧模板或政策扩批；后续交付、发布和实际清理分别授权。
