# LT06 预发布源码回归记录

日期：2026-10-16（Australia/Sydney）。状态：**0.9.0-dev 源码预发布候选机器复核通过；PR #8 待合并，尚未发布**。本记录对应 `codex/archsift-lightweight-workbench` 的 `d11234064c19bcb715127df021cecb4a46f35d6e`，不改写用户对先前固定 UI 载荷的[独立人工结论](20261016-lightweight-lt06-human-ui-review.md)。

PR 的 Windows 与 Ubuntu [双平台 CI](https://github.com/von12549/ArchSift/actions/runs/38836852632) 全部通过。本机离线 Release 开发门禁使用 .NET SDK 10.0.303：旧 ArchitectureTests 2/2、UnitTests 151/151、IntegrationTests 95/95、原生 MTP 测试 18/18，规则合同检查通过；开发运行证据位于 `D:/ArchSift-lab/runs/development-e3ac8a93db6f4109a46a65552a9d2afc`。本次新增的目录快照核对弥补递归文件观察器在新源码目录刚建立时可能漏报的事件；若目录／相关文件清单变化或枚举失败，门禁保守标记输入已变化。

从该提交离线发布 Release/net10.0、依赖本机框架的 CLI 与 Web 至 `D:/ArchSift-lab/packages/lt06-release-d112340`。84 个文件的 `manifest.json` SHA-256 为 `6af647a3f148a2477fbf3518cec30036878a7fe84ca6c992fad0482e2d8c7f34`；逐文件重算 0 个不符。新载荷与用户复核的 `lt06-ui-revision-20261016-a` 的 30 个 Web `wwwroot` 静态文件逐一 SHA-256 相同。此包只用于本机回归，不作为 GitHub 可移植发行资产。

真实门禁仅作用于已授权的 `D:/ArchSift-lab/fixtures/lt05-shared-cc06528` 独立 IFX 工作树，固定提交 `cc06528ab17052baafa496df2a89f6cde9207384`。前置核对发现的工作树状态仅为 LT02A 已审查的一个项目引用修改及 `NuGet.config`、`ArchSiftJsonTests.cs`、`lt02a-mechanism.json` 三个接入文件；规则 SHA-256 `abd6a7d94e3655b4255b2a280e8fa58c6c50e64ce91dce6848121d24d20a92aa`，80 项原案例基准 SHA-256 `74a1d63dabbb0be14e139134661fed6bf0af77fe3ba00f13fc69959de66ca72b`。新载荷从单独配置 `D:/ArchSift-lab/runs/lt06-release-d112340/test-gate-config.json` 顺序完成离线 restore、Release/net10.0 build、发现及测试，均 exit 0；原 IFX 80/80、JSON 1/1，发现／完成 81/81，覆盖完整，588 项未纳入，`allow/0`。权威报告 `D:/ArchSift-lab/runs/lt06-release-d112340/cli-gate/architecture-test-run.json` SHA-256 `b79925a881a928ff84dfeea86fb2cd0760d1ffbf81b9f3107e961664ff799a1d`，通过冻结 schema 校验，报告 `sourceChangedDuringRun=false`。运行后外置工作树状态与前置一致，原 IFX checkout／安装未改动。

开发测试、封装与真实门禁的宿主环境／Profile 哈希均保持 `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`。既有独立人工 UI 结论由相同的静态文件哈希支持；这次未要求用户重新执行视觉验收。当前放行范围仍是源码预发布，IFX 正式政策待发布后另行处理。
