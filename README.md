# ArchSift

独立的本地 .NET 架构与依赖分析工具。当前已完成 W02 契约与规则模板，版本为 `0.1.0-dev`；项目发现、六类规则求值、报告和本机 Web UI 按后续工作包实现。

## 开发与验证

需要 Windows x64、.NET 10 SDK、PowerShell 7，以及完整的本地 NuGet feed。`global.json` 以 10.0.100 为基准并允许 latestFeature；本次使用 SDK 10.0.303。

在仓库根运行：

```powershell
pwsh -NoProfile -File scripts/Invoke-DevelopmentChecks.ps1
```

默认使用用户 `.nuget/packages` 作为明确本地 feed，也可以通过 `-LocalFeed <目录>` 指定其他离线 feed。恢复到隔离的 `artifacts/development/packages`，使用仓库内的锁文件，随后编译和运行单元、集成、架构测试。缺包会失败，不联网补包。`-InitializeLocks` 只用于有意更新依赖后的首次锁文件生成；常规检查保持 locked mode。

检查脚本对 .NET 子进程设置独立 `DOTNET_CLI_HOME` 和 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`；执行前后比较 User/Machine 环境、Process PATH 与四个 PowerShell Profile，只保存哈希。每次检查的日志和 TRX 位于忽略的 `artifacts/development/<run-id>`。这些是 ArchSift 自身开发产物；真实分析目标的快照/报告仍须外置。

当前 CLI 可用 `--version`、`--help`、`rules validate --file` 与 `rules render --file --output`；未实现的命令返回配置错误 2。Web 项目只提供版本入口，暂不监听端口。W02 验证八个项目编译成功、零警告/错误、56 项测试通过；六类模板 JSON/Markdown 可校验。真实 ArchUnitNET 规则验收在 W05。

## 项目结构

| 项目 | 职责与引用方向 |
| --- | --- |
| Contracts | 共享产品身份，规则和报告 DTO 在 W02 扩展 |
| Core | 分析服务与引擎接口，引用 Contracts |
| ArchUnit | ArchUnitNET 适配，引用 Core 与固定引擎包 |
| CLI / Web | 本地入口，引用 Core，并在组合层引用 ArchUnit |
| UnitTests / IntegrationTests / ArchitectureTests | 共享版本、进程入口、项目及编译依赖方向验证 |

运行时基于 .NET 10；后续待分析目标支持 net8/net9/net10，与工具自身 TFM 分开记录。没有 Guard 安装或旧 Profile/Stage 前置。

## 计划与证据

- [正式计划](docs/plans/20261006-archsift-implementation-plan.md)
- [W00 完成记录](docs/acceptance/20261006-w00-completion.md)
- [W01 验收记录](docs/acceptance/20261006-w01-foundation.md)
- [W02 验收记录](docs/acceptance/20261006-w02-contracts.md)
- [规则与 schema 边界](docs/rules.md)
- [已解析依赖与许可](docs/dependencies/w01/README.md)
- [迁移来源与用户授权](docs/migration/source-inventory.json)
- [项目操作与提交指令](AGENTS.md)

当前提供的是基础开发项目，尚未通过 0.1.0 产品验收。网络漏洞审计、本地 ZIP、真实 IFX 集成与操作员可用性验收在对应工作包执行。
