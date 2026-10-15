# ArchSift.InProject 0.1.0 本地包与维护

本产品的 0.1.0 包是独立的 Windows x64 ZIP。包内含 .NET 10 CLI／loopback Web、共享分析组件、独立 xUnit 桥接和测试项目、net8／net9 离线参考包、维护入口、版本清单及第三方许可清单。业务 solution 仍可由 net8、net9、net10 的 SDK-style C# 项目组成；执行端需有 .NET 10 SDK 和测试运行时。只使用明确的本地包及 SHA-256，不从网络静默下载 SDK 或 NuGet 包。

以下命令在不加载 PowerShell Profile 的会话中执行。将 `<解压目录>`、`<目标worktree>`、`<本地包.zip>` 和 `<sha256>` 换成绝对路径；包摘要取自随候选保存的 `candidate-metadata.json`，不要自行猜测。每个 worktree 独立初始化，真实业务目标须经目标所有者授权。

```powershell
pwsh -NoProfile -File <解压目录>/bootstrap/archsift-inproject.ps1 init --target <目标worktree> --package <本地包.zip> --sha256 <sha256> --solution Product.sln
pwsh -NoProfile -File <目标worktree>/.archsift/bootstrap/archsift-inproject.ps1 verify --target <目标worktree>
pwsh -NoProfile -File <目标worktree>/.archsift/bootstrap/archsift-inproject.ps1 gate --project <目标worktree>/.archsift/project.json --evidence daily
```

初始化在 `.archsift/` 写入版本锁、运行时、专用测试、配置和规则；`runtime`、`state`、`reports`、`evidence`、`offline`、`licenses`、`maintenance` 是精确忽略的本地产物。`project.json` 和规则文件保留在 worktree 中，版本锁及包内固定载荷由摘要校验。默认离线包源为安装内的 `offline`；如果显式配置外部 `localFeed`，它必须存在且不能与业务 worktree 重叠。构建会在 owned 状态目录创建新快照，不启动业务入口。`full` 证据、报告与历史有各自的保留及导出入口，移除包时不会自动删除。

维护入口必须通过安装内的 PowerShell 启动器调用。它先逐文件校验并复制维护二进制到 `.archsift/state/maintenance-runners/<id>`，从该副本执行切换，使运行中的维护程序不占用待替换目录。运行结束后仅清理该次 runner；宿主漂移或中断时保留材料。不要直接运行安装内的维护 EXE 来执行变更。

```powershell
pwsh -NoProfile -File <目标worktree>/.archsift/maintenance/archsift-inproject-maintenance.ps1 -Command inspect -Target <目标worktree>
pwsh -NoProfile -File <目标worktree>/.archsift/maintenance/archsift-inproject-maintenance.ps1 -Command upgrade -Target <目标worktree> -Package <新本地包.zip> -Sha256 <新sha256>
pwsh -NoProfile -File <目标worktree>/.archsift/maintenance/archsift-inproject-maintenance.ps1 -Command rollback -Target <目标worktree> -Receipt <升级operationId>
pwsh -NoProfile -File <目标worktree>/.archsift/maintenance/archsift-inproject-maintenance.ps1 -Command remove -Target <目标worktree>
```

升级先完整核对新 ZIP，写操作日志，再切换固定载荷与版本锁。成功返回的 `operationId` 对应 `.archsift/state/maintenance-receipts/` 的收据；旧载荷按哈希保留在 `.archsift/state/maintenance/<operationId>/old`，供显式回退。回退要求当前包与收据记录一致，旧载荷也须通过摘要复核。`remove` 撤下活动载荷但保留配置、规则、任务历史、报告、证据和回退收据。移除后安装内的维护入口也会撤下，可从已核对摘要的 ZIP 解压副本调用外部维护二进制：`dotnet <解压目录>/maintenance/archsift-inproject-maintenance.dll rollback --target <目标worktree> --receipt <移除operationId>`。执行中断后，如 `inspect` 显示 `recovery-required`，先从受信任的外部维护副本调用 `recover --target <目标worktree>`，核对结果和宿主状态后再继续其他操作。

工具版本变化会使旧门禁标记为过期；历史报告与证据仍保留原工具和输入身份。重新运行门禁后才可形成新版本的当前结果。维护操作会排斥同一 worktree 的活动任务，不影响另一 worktree。包的许可和来源清单见 `licenses/third-party-inventory.json`、`licenses/*` 及 `runtime/THIRD-PARTY-NOTICES.md`。
