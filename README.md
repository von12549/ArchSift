# ArchSift

独立的本地 .NET 架构与依赖分析工具。`0.3.0` 包含项目发现、八类规则（含项目引用与 NuGet 直接包 allowlist）、真实 ArchUnitNET、JSON/HTML/SARIF、Git 两端/工作区比较和 loopback UI。Windows x64 自包含分发，Linux CLI 已验证；无 Guard 安装前置。ArchSift 使用 [MIT 许可](LICENSE)，第三方许可单独保留。

[下载 0.3.0](https://github.com/von12549/ArchSift/releases/tag/v0.3.0)；先核对 release 的 SHA256SUMS.txt。W11 实现与人工验收见 [0.3.0 范围记录](docs/acceptance/20261007-w11-allowlists-ui.md)，正式资产身份将在 [发布记录](docs/releases/0.3.0-publication.md) 中固定。

## 开发与验证

本机检查需要 .NET 10 SDK、PowerShell 7、Git 和完整本地 NuGet feed。`global.json` 以 10.0.100 为基准并允许 latestFeature；Windows 验证 SDK 10.0.303，Linux 验证 SDK 10.0.401。net8/net9 构建 fixtures 另需 SDK 9.0.314 和对应 packs。

在仓库根运行：

```powershell
pwsh -NoProfile -File scripts/Invoke-DevelopmentChecks.ps1
```

默认使用用户 `.nuget/packages` 作为明确本地 feed，也可以通过 `-LocalFeed <目录>` 指定其他离线 feed。恢复到隔离的 `artifacts/development/packages`，使用仓库内的锁文件，随后编译和运行单元、集成、架构测试。缺包会失败，不联网补包。`-InitializeLocks` 只用于有意更新依赖后的首次锁文件生成；常规检查保持 locked mode。

检查脚本对 .NET 子进程设置独立 `DOTNET_CLI_HOME` 和 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`；执行前后比较 User/Machine 环境、Process PATH 与四个 PowerShell Profile，只保存哈希。新日志/TRX 默认位于外置 `D:/ArchSift-lab/runs`，可通过 `-LabRoot` 指定外置路径；旧开发证据保留不改写。开发 bin/obj 与隔离 NuGet 缓存仍在忽略目录。

CLI 可用 analyze/verify/changes、rules validate/render/draft、ui --config、--version/--help。Web 工作台只绑定 loopback 随机端口并使用会话 token；规则 JSON 和报告写入明确的目标外目录。违规默认 exit 0，无法判定/配置/执行错误分别可见；[CI 样例](samples/ci)仅 opt in，不注册 required check。Linux 本机验证可运行 `scripts/Verify-Linux.ps1`，使用已安装 Docker SDK 镜像、外置快照和显式离线 feed，不自动 pull。

## 项目结构

| 项目 | 职责与引用方向 |
| --- | --- |
| Contracts | 共享产品身份，规则和报告 DTO 在 W02 扩展 |
| Core | 分析服务与引擎接口，引用 Contracts |
| ArchUnit | ArchUnitNET 适配，引用 Core 与固定引擎包 |
| CLI / Web | 本地入口，引用 Core，并在组合层引用 ArchUnit |
| UnitTests / IntegrationTests / ArchitectureTests | 共享版本、进程入口、项目及编译依赖方向验证 |

工具运行于 .NET 10；目标支持 SDK 风格 C# net8/net9/net10，与工具自身 TFM 分开记录。没有 Guard 安装或旧 Profile/Stage 前置。Windows ZIP 的声明分析无需 SDK；changes 需要 Git；isolated build 需要目标 prerequisites，不是完整沙箱。

## 计划与证据

- [正式计划](docs/plans/20261006-archsift-implementation-plan.md)
- [W00 完成记录](docs/acceptance/20261006-w00-completion.md)
- [W01 验收记录](docs/acceptance/20261006-w01-foundation.md)
- [W02 验收记录](docs/acceptance/20261006-w02-contracts.md)
- [W07 工作台验收](docs/acceptance/20261007-w07-workbench.md)
- [W08 完成报告](docs/acceptance/20261007-w08-completion.md)
- [变更比较](docs/changes.md)与 [W09 验收](docs/acceptance/20261007-w09-comparison.md)
- [W10 跨平台/SARIF 验收](docs/acceptance/20261007-w10-report-integration.md)
- [W11 allowlist/UI 完成记录](docs/acceptance/20261007-w11-allowlists-ui.md)
- [CLI 使用与配置](docs/cli.md)
- [规则与 schema 边界](docs/rules.md)
- [已解析依赖与许可](docs/dependencies/w01/README.md)
- [迁移来源与用户授权](docs/migration/source-inventory.json)
- [项目操作与提交指令](AGENTS.md)

W08 冻结 `0.1.0-dev` 候选与独立操作员证据保持原样；0.2.0 发布与发布后验收保持冻结。W11 已完成 130 项本地回归、真实 IFX CLI/UI、独立操作员和最终安全审计；0.3.0 发布使用单独的固定资产记录。网络漏洞审计未运行；NuGet tool、传递包/版本范围、Linux Web UI、fail-on 与强制 CI 仍未交付。
