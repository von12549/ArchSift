# 轻量架构测试工作台：无界面门禁与报告

`archsift test-gate --config <JSON>` 在指定的独立目标副本中，依次执行显式离线 restore、正常增量 build、测试发现与完整测试工程。它运行目标现有的专用 C# 架构测试和经审查启用的 JSON 类型依赖测试；CLI 与后续 UI 使用同一 `ArchSift.TestGate` 服务。完整取证仍走原 ArchSift evidence 工作流，两个结果互不替代。

配置使用 schemaVersion 1，并明确填写目标绝对路径、测试项目相对路径、测试可执行名、`net10.0`、Debug/Release、目标内 `NuGet.config`、本机离线 `localFeed`、源根外 `packageCache`、目标内规则路径、受信的原 IFX 执行案例 ID 文件及其 SHA-256、每次新建的源根外 `runDirectory`。IFX xUnit v3/Microsoft.Testing.Platform 使用 `runnerFormat: "mtp-ctrf"`；合成 xUnit 原生事件工程使用默认 `xunit-json`。IFX 接入源文件和机制验证规则见 [LT02A 样例](../samples/ifx-lt02a/README.md)。样例规则不是正式批准的 B 类政策。

`originalBaseline` 应引用[原 IFX 80 项实际执行 ID 基线](decisions/20261015-lightweight-ifx-original-case-ids.json)。必须对配置中使用的**当前文件字节**计算 `originalBaselineSha256`；仓库 LF 版本为 `55b0f65b535516993761a0121ed4b6db26a98a698291c03056b1a85052cf3a33`。新增或减少原案例需单独审查，不能靠新增 JSON 案例抵消。原生 MTP 发现 UID 与 xUnit CTRF 执行 ID 是不同体系；门禁从实际终态 ID 核对基线，不从显示名称推断执行。

每个受控 restore/build/发现/test 子进程都设置 `IFX_UPDATE_SNAPSHOTS=0`；父进程即使为 `1` 也不能让本次门禁更新 API 快照。若设置隔离的 `DOTNET_CLI_HOME`，同一子进程设置 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`。目标应用入口不参与此操作。`filterCaseId` 只用于局部诊断，结果为 subset，不能取得完整放行。

完成的运行目录含 `architecture-test-run.json`、`architecture-test-run.html`、`architecture-test-state.json` 和各阶段日志。JSON 按[冻结 schema](../schemas/architecture-test-run.schema.json)记录本次范围、实际案例、原始 JSON 规则状态、matched、finding、阶段、未纳入项与 `runGate`，是权威结果。HTML 只安全投影同一信息；原 IFX 测试若只有断言消息，不会伪造类型 finding 或源码行号。未纳入的 A 类和未启用 B 候选有来源、责任方与原因，不计入本次通过。权威报告保存失败时本次退出码为 `3`，不会沿用旧成功。

退出码：`0` 完整通过，`2` 实际断言／规则违规，`3` 执行或权威报告错误，`4` 覆盖不确定、subset 或已知运行中输入变化，`5` 配置错误，`130` 取消。报告中的 `runGate` 固定为本次结果；之后规则或已知输入变化只影响历史适用性，不改写历史结论。

使用 `archsift test-history --directory <运行目录的共同父目录> [--target <目标根目录>]` 只读列出有运行状态标记的目录。`finished`、`failed`、`cancelled`、`interrupted`、`unavailable` 保持区别；缺失或损坏的报告不显示放行。已知规则字节或 Git 提交变化使 `historyFreshness=stale`；无法证明当前完整输入一致时为 `unknown`，即使原运行是 `allow` 也不会自动补建或升级为当前合规结论。
