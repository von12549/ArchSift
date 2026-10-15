# 轻量架构测试工作台：本地候选使用与分发边界

2026-10-16 固定的**本地评审候选**产品源码提交为 `e0b3bddabefd4561ca9ddfc13ed68e7d91a4f5b0`，开发版本 `0.9.0-dev`。候选载荷在 `D:/ArchSift-lab/packages/lt06-candidate-e0b3bdd`，逐文件清单 `manifest.json` 的 SHA-256 为 `4a19cfd8ae57464d4b6a010dec9a2b923ab1e5b5aa5bf287b3b308fc2d768212`。它用于本机独立评审；尚未完成[人工 UI 验收](acceptance/20261016-lightweight-lt06-human-ui-review.md)，也未获公开发布、安装、IFX 政策采纳或 PR／合并授权。[机器候选记录](acceptance/20261016-lightweight-lt06-candidate.md)列出实际门禁与限制。

## 在已授权的外置 IFX 工作树使用

本机演示目标固定为 `D:/ArchSift-lab/fixtures/lt05-shared-cc06528`，IFX 源提交 `cc06528ab17052baafa496df2a89f6cde9207384`，规则 SHA-256 `abd6a7d94e3655b4255b2a280e8fa58c6c50e64ce91dce6848121d24d20a92aa`。它是带 LT02A 接入文件的独立 worktree；`D:/IFX-10-Root/IFX-New` 原 checkout 与安装只读。案例 ID 基准取自**该固定路径**的 `D:/ArchSift-lab/runs/lt05-metrics-20261015/worktree-original-case-ids.json`，SHA-256 `74a1d63dabbb0be14e139134661fed6bf0af77fe3ba00f13fc69959de66ca72b`。移动工作树后须重做原生 80 项核对与显式基准，不能直接沿用此 ID 文件。

运行环境为 Windows x64、.NET 10 SDK（本机验证 `10.0.303`）、net10.0/Release、IFX xUnit v3/Microsoft.Testing.Platform；候选 Web/CLI 是依赖本机 .NET 10 运行时的 publish 输出。测试工程继续通过已审查的项目引用使用本仓库 `ArchSift.Testing`，同时使用原有 IFX 锁定依赖与显式离线 NuGet 源、外置包缓存。载荷并未将 IFX 测试工程、原测试、NuGet 包或测试适配源码安装到其他目标。

在 `pwsh -NoProfile` 中，可用以下固定输入启动 UI。命令会打印一次性 loopback 会话 URL；只在本机浏览器打开该地址，勿公开其中的会话令牌。关闭终端前按 Ctrl+C，并等待受控任务退出。

```powershell
& 'D:/ArchSift-lab/packages/lt06-candidate-e0b3bdd/web/ArchSift.Web.exe' --config 'D:/ArchSift-lab/runs/lt06-candidate-e0b3bdd/web-config.json'
```

在“架构测试”标签选择外置配置 `D:/ArchSift-lab/runs/lt06-candidate-e0b3bdd/test-gate-config.json`。读取配置只显示基准／JSON／未纳入清单；选择“完整”并启动后，才可依据本次权威报告判断结果。UI 的规则复制会写入 `D:/ArchSift-lab/runs/lt06-candidate-e0b3bdd/web-rules`，运行与历史保存在同一外置运行根的 `web-output/test-runs`；不编辑原 IFX 规则。若只复核 CLI，在当前已保存的 `cli-gate` 之外新建一个未使用的外置运行目录，并把配置副本的 `runDirectory` 指向它，然后运行：

```powershell
& 'D:/ArchSift-lab/packages/lt06-candidate-e0b3bdd/cli/archsift.exe' test-gate --config '<外置配置副本绝对路径>'
```

门禁强制测试子进程 `IFX_UPDATE_SNAPSHOTS=0`；`0` 是完整通过，`2` 是实际违规，`3` 是执行／报告错误，`4` 是覆盖不确定或 subset，`5` 是配置错误，`130` 是取消。完整取证继续使用原 ArchSift evidence 入口，本日常门禁的 `allow` 不提供源码绑定证明。报告以 `architecture-test-run.json` 为权威，HTML 与 UI 是投影；未迁入 A 类 182 项和未启用 B 候选 406 项仍是未纳入，不计入本次通过。历史 `unknown` 不代表当前源码合规。当前仅有基本独立运行目录和只读历史，无自动轮转、归档恢复或续跑。

## 测试依赖与 UI 分发方案

首版随 ArchSift 仓库交付 `ArchSift.Testing` 与 `ArchSift.TestGate` 源码和锁文件：目标测试项目由显式 `ProjectReference` 接入前者；CLI/Web publish 输出包含执行协调所需程序集，均使用同一 TestGate 服务。受控目标必须独立审查新增引用、适配器、规则、离线源与原测试基准；不能把 LT02A 样例当作 IFX 正式政策。首版不发布单独 NuGet 包，也不复制 InProject runtime 或修改旧 Setup／安装选择。跨机器交付前须另行解决可移植的测试依赖路径、目标授权、锁定包源与案例 ID 基准；本地候选清单不能冒充通用安装包。

UI 采用已有 Web 的 loopback 会话、安全令牌和规则编辑器，候选只包含 `dotnet publish` 的 CLI/Web 目录与 SHA 清单；它没有新安装器、后台服务或公开站点。后续若获发布授权，再从冻结源码与已确认的依赖布局构建、审核包和发布门槛。LT05 的同条件冷运行约 118 秒，由目标重建主导，没有可证明的明显提速；不据此增加材料生命周期系统。
