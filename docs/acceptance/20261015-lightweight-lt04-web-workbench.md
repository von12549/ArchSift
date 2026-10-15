# LT04 Web 接入与机器验收

日期：2026-10-15（Australia/Sydney）。状态：**LT04 UI/MACHINE COMPLETE；LT05 未开始；产品未接受、未发布**。最终独立人工 UI 视觉与动手验收仍由用户完成。

## 输入与实现

- 开发分支 `codex/archsift-lightweight-workbench`；真实目标为外置 IFX 副本 `D:/ArchSift-lab/fixtures/lt02a-ifx-cc06528-20261015`，源提交 `cc06528ab17052baafa496df2a89f6cde9207384`。原 IFX checkout `D:/IFX-10-Root/IFX-New` 保持只读且 Git 干净。
- Web 会话配置及外置运行证据位于 `D:/ArchSift-lab/runs/lt04-web-smoke-20261015`；测试门禁输入从 `lt03-ifx-final-config.json` 读取，目标源内规则复制到该目录的外置规则库，并生成可由 CLI 读取的 `.test-workbench-*.json`。规则 ID `lt02a-mechanism-only`、启用案例 `ifx-context-mechanism`。原案例基准 80 个 ID，SHA-256 `55b0f65b535516993761a0121ed4b6db26a98a698291c03056b1a85052cf3a33`；运行器 MTP/CTRF，Release/net10.0，离线源与包缓存沿用 LT02A 审查过的输入。
- Web 调用 `TestGateService.RunAsync`，与 CLI 使用同一服务、配置结构及权威报告。目标内 IFX C# 测试不提供编辑入口；JSON 复制到外置库后由原规则编辑器校验保存。页面显示 IFX 工程、原案例 ID、JSON 案例及旧 A／B 未纳入清单；实际类／方法和结果仅从运行报告显示。完整模式没有原案例过滤，subset 要求显式 UID；取消、历史、JSON/HTML 下载和既有完整证据入口已接入。新测试与旧 Web 任务共用运行锁。

## 真实门禁与接口检查

本地 loopback Web API 用会话令牌调用。读取配置但未执行时返回原基准 80、JSON 1、未纳入 588，历史为空；规则复制后外置文件与保存配置均存在，编辑器可按原规则 ID 读取。目标源内规则 SHA-256 在前后均为 `abd6a7d94e3655b4255b2a280e8fa58c6c50e64ce91dce6848121d24d20a92aa`。

最终构建的 Web 完整运行目录 `D:/ArchSift-lab/runs/lt04-web-smoke-20261015/output/test-runs/20261015T115459285-69847902`：80 个原专用测试和 1 个 JSON 案例均通过，覆盖完整，588 项未纳入，`allow/0`；JSON SHA-256 `9b683ad25b326ccf60d4696f79b81a1b2d9cdbc2273c55f79789ba3610ac6a37`。运行中从旧 `/api/run/analyze` 入口请求任务得到 409，证明 Web 任务锁共享。

使用相同外置规则 SHA、原案例基准与目标的 CLI 完整运行目录 `.../test-runs/manual-106f538d0c14478ba638775609a42ff7` 也为 80＋1 `allow/0`；JSON SHA-256 `9b5940a104d30fac77181e2bad05e88ae4a520bf11061317713e3c2283fc3077`。两份报告的 `runGate`、退出码、规则 SHA、每个案例 ID／结果／执行标志、预期原案例 ID 和未纳入 ID 逐项一致；运行身份及时间不同。

Web subset 用完整运行发现的一个 UID，得到 `block/4`；立即取消另一完整运行得到 `cancelled/block/130`。二者都保留独立权威报告和历史标记，未沿用此前 `allow`。用现有规则编辑 API 在**外置**规则上暂时设置实际依赖 `EndpointScopeMetadata → ExecutionScope` 后，全量 Web 门禁得到 `block/2`、一条真实 JSON finding，原 80 案例仍执行；负例报告目录 `.../test-runs/20261015T115903823-f292d7a6`，JSON SHA-256 `9b75e50cb4076139424bed518ff5e02011f8ce9db251cdf5d88adaac1aaae975`。外置规则按原字节恢复，历史将该违规报告标为 `stale`；旧完整成功仅作为独立历史保留，当前新鲜度 `unknown`。最终历史同时区分 `finished`、`failed`、`cancelled` 和各自门禁。

Web JSON 与 HTML 下载 HTTP 200；原 80／JSON 1／未纳入 588 和覆盖完整字段可读。全部六份 Web／CLI 报告使用本地 NJsonSchema 10.9.0 对冻结 schema 校验均 0 错误。网页文本用 DOM 文本节点投影，权威 JSON 未由 UI 重算。

## 回归、宿主与边界

整解 Release/net10.0 构建 0 警告／0 错误。旧 UnitTests 151/151、IntegrationTests 95/95、ArchitectureTests 2/2 通过且无 skip；新增 Testing.Tests xUnit v3 原生入口 17/17 通过且无 skip。整解 `dotnet test` 的 VSTest 入口对该 MTP 工程不适用，已改用生成的 xUnit v3 可执行入口；架构方向测试已登记 Web 对 Testing/TestGate 的依赖。关键 .NET 构建、真实门禁与回归前后宿主安全环境／Profile 哈希均为 `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`。原 IFX checkout Git 干净，外置副本仅保留 LT02A 审查过的项目引用、NuGet 配置、适配器和规则四项改动／新增；未更改安装或原测试源码。

页面视觉和手动可用性尚未由独立人工确认；LT05 仍需同条件 UI／CLI／CI 对照、完整 IFX 回归与冷热／磁盘测量。A 类未迁入与 B 候选的未纳入状态不代表通过，历史 `unknown` 也不是当前源码合规证明。
