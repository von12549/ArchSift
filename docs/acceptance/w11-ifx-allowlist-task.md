# W11 IFX allowlist 现场复核任务

状态：`COMPLETE / PASS`。W11 自动化门禁、真实 IFX CLI allowlist、信息层级修正后的独立人工 UI 复核及最终安全审计均已通过；完整结论见 [W11 完成记录](20261007-w11-allowlists-ui.md)。本任务及下列命令仅作审计历史保留，不要重复执行；W11 尚未提交、推送或发布。

目标固定为 `D:\IFX-10-Root\IFX-New`、入口 `IFX.sln`、HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`、`existing / net10.0 / Debug`。脚本在任何产品执行前拒绝非预期 HEAD 或有普通改动的 IFX 工作区。它不 build/restore IFX，不执行 IFX 应用，不访问 Guard，不修改持久环境或 Profile；规则、报告、日志和结果仅写到 `D:\ArchSift-lab\runs\w11-ifx-allowlists-*`。

以下 CLI 命令块作为已完成阶段的历史保留，不要重复执行：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' `
    -NoProfile `
    -NoLogo `
    -NonInteractive `
    -File 'D:\ArchSift\scripts\Invoke-W11IfxManualStep.ps1'
```

预期为 `status=pass`、`exitCode=4`、`execution=partial`、`compliance=noncompliant`、105 项目、0 程序集、0 execution error，并生成内容一致的 JSON、HTML、SARIF 三份报告。项目 allowlist 以空允许目标精确暴露 `IFX.Application.Primitives -> IFX.Domain.Primitives`；NuGet allowlist 应只暴露被故意遗漏的直接包 `FluentValidation.DependencyInjectionExtensions`。`partial` 继续表示声明模型覆盖限制，不得改写为完整成功。

脚本同时固定当前 CLI 目录字节身份，比较运行前后的 ArchSift/IFX HEAD 与普通状态，以及 User/Machine environment、Process PATH 和四个 PowerShell Profile 的组合哈希。任何不相等或断言失败均保留证据并停止；不得自动修复或递归清理。

CLI 结果：`D:\ArchSift-lab\runs\w11-ifx-allowlists-3ba4055f16ab446ba47652515c47d8d3\operator-result.json`，`status=pass`。项目/NuGet 两个预期 finding、105 项目、三格式报告、CLI 目录字节、目标/产品/主机状态均通过；未 build/restore 或执行 IFX 应用。下一块为单独的 UI 会话启动任务。

## 信息层级修正后的人工复核

在正常 PowerShell 中只执行下面这一块。脚本会先校验真实 IFX HEAD/清洁状态、已接受的 CLI 证据、主机安全哈希，以及本次修正版 `index.html / app.js / style.css` 的语义标记和 SHA-256；随后仅启动 loopback UI，不 build/restore 或执行 IFX 应用。保持该 PowerShell 窗口开启；脚本会在 UI 服务安全退出前持续附着，避免启动命令结束时宿主回收子进程。看到 session JSON 后立即使用其中的新 URL 完成下述人工任务；不要为回传中间 JSON 而中止命令。完成安全关闭、PowerShell 恢复提示符后，一次性回传 attestation：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' `
    -NoProfile `
    -NoLogo `
    -NonInteractive `
    -File 'D:\ArchSift\scripts\Start-W11IfxUiManualSession.ps1'
```

首次 W11 UI 人工复核会话为 `D:\ArchSift-lab\evidence\w11-ifx-ui-4abcd2e2545341d8bb6b4cd470b93a73`。操作员确认目标快照、两条预期 finding、项目筛选、窄屏、三格式入口、历史快照和安全关闭均符合预期，confidence 4/5、帮助 L1；PID `10104` 已退出、端口 `10850` listener 为 0、三份报告已保存。但操作员截图指出：105 个项目本体虽已折叠，105 条逐项目 coverage limitation 仍作为长文本默认展开。该观察准确，首次 UI 复核因此记录为 `NEEDS FIX` 而非 PASS；后续修复必须保留限制总数及其对 `partial/inconclusive` 的影响，只折叠明细，不能删除或弱化限制。

操作员进一步指出，单纯折叠不足以解决理解问题：项目名称、规则结果和覆盖限制在原页面中使用近似的表格/文本样式，项目与结果、不同种类结果之间均缺少明显区分。修正后的信息架构将结果拆为“运行结论、规则结论、违规证据、覆盖边界”四个语义区域，并将“项目与引用”作为独立的目标清单区域；每区使用一致但可辨认的标题、说明、边线和状态标识。规则表只显示限制数量，完整限制保留在覆盖边界中按需展开。该修正需通过自动回归及一次新的短人工 UI 复核后才能把 UI 状态改为 PASS。

信息层级修正后的完整开发门禁证据为 `D:\ArchSift-lab\runs\development-53b4733726dc4624a6a46b063d9f4997`：SDK 10.0.303、Debug、离线 restore/build 0 警告错误，97 unit + 31 integration + 2 architecture 全通过，8 个模板独立校验/渲染通过，逐步及最终 host state 均相等。另以 ArchSift 自身 8 项目合成目标产生 `partial / noncompliant / exit 4`、2 条不同严重度 finding 和 15 条 coverage limitation，验证五个区域同时出现、限制默认折叠。桌面与 480px 窄屏截图位于 `D:\ArchSift-lab\runs\w11-ui-hierarchy-visual-check\desktop.png` 与 `narrow.png`，SHA-256 分别为 `dce77610deb1a6e06d1476933ce15636751c64fb4ff1e4b33de7f30416bcfa9f`、`80f2906f07ee398e6fe4fa0d01766e0726a2bcac9622029eafbcb060facf98e8`；窄屏最终 `scrollWidth == clientWidth == 465`、overflow 元素为 0。UI PID `8256`、端口 `6498` 及其精确隔离 Chrome profile 进程均已归零。该合成验证不替代真实 IFX 独立操作员复核。

第二轮人工复核首次启动记录 `D:\ArchSift-lab\evidence\w11-ifx-ui-1a0904c479f342e69bb88c6581ddd6b5` 正确绑定修正版三项静态资源，IFX/规则/主机身份也全部通过，但没有进入操作员观察，因此不作为产品 UI 结论。中间跨 shell 查询曾误报 PID `34900` 与端口 `4435` 已不存在；最终安全审计后来确认该精确记录的 ArchSift Web 会话仍在监听。审计按 session、进程名和命令行完成身份核对后只终止 PID `34900`，端口归零。该失败会话和误判作为启动器/验收工具历史保留，不改写产品结论。

附着式启动器的最终会话为 `D:\ArchSift-lab\evidence\w11-ifx-ui-a0d97ac98b5541748d877e41698adbe0`。中间跨 shell 查询一度误判 PID `11792` 和端口 `11925` 已消失，实际启动器仍附着等待；操作员随后使用该会话的认证 URL 完成观察并请求安全关闭，最后一次性回传 attestation。`final-result.json` 为 `status=pass`：105 项目、0 程序集、`partial / noncompliant / exit 4`、两条规则/两条 finding、105 条默认折叠 limitation、项目筛选、窄屏、三格式入口和信息层级均通过；独立操作员帮助 L1、confidence 4/5。最终 PID/端口/启动器均归零，三格式报告、官方 SARIF schema、CLI/UI parity 及宿主/产品/目标状态全部通过。

最终安全审计为 `D:\ArchSift-lab\evidence\w11-final-safety-audit-1c8087f725b645678ee8776c27e8b963\audit-result.json`，`status=pass`。审计复核 130/130 自动测试、八模板、CLI/UI/report 身份、24 份关键证据、IFX 清洁状态、宿主哈希、进程/端口/容器零残留；未 build/restore 或执行 IFX 应用。
