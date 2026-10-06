# W10 0.2.0 报告接入验收

日期：2026-10-07（Australia/Sydney）。状态：COMPLETE / PASS；正式发布资产另行构建、校验和登记，不把工作区候选当作正式资产。

发布前远程 CI 补充：首次 run [37509047659](https://github.com/von12549/ArchSift/actions/runs/37509047659) 在两平台均真实失败；仅 net8/net9 fixture 的 SDK 身份断言失败。GitHub runner 同时预装 9.0.318，fixture 的 latestPatch 正确选择了新 patch，而断言仍要求 9.0.314。修正为 fixture 内 rollForward=disable，以落实测试明确要求的固定 9.0.314；不改产品 global.json，不放宽结果断言，不更新宿主 SDK/PATH。两平台宿主哈希仍相等，失败日志/TRX/artifact 保留；发布暂停到修复后的远程检查通过。

第二轮 [37509769540](https://github.com/von12549/ArchSift/actions/runs/37509769540)：两平台 net9 已通过，仅 net8 离线 restore 缺少固定 SDK 所需 8.0.27 targeting packs，准确失败而未回退联网。修复是在一次性 CI 的显式联网准备阶段，从实际 SDK 9.0.314 的 KnownFrameworkReferences 读取并固定所需 pack，下载到 runner temp feed；合成测试只读取该明确本地 feed，产品仍默认离线且不忽略失败。另按官方相对 URI 语义补充 SARIF originalUriBaseIds；schema 结构校验之外同时验证消费定位基准。

## 当前源码验证

- Windows x64 / SDK 10.0.303 / net10.0：Release 109/109（78 unit、29 integration、2 architecture），零编译警告/错误，运行 `development-76dcf0d5e3024406aa9b1350139c91df`；最终 Debug/静态安全复核同为 109/109，运行 `development-3e2011b369e84a97980c17b387bb2c88`。net8/net9 真实程序集 fixture 使用已安装 SDK 9.0.314/packs。
- Linux Docker / SDK 10.0.401 / net10.0 / Release：107/107（78 unit、27 integration、2 architecture），离线 locked restore、编译、真实 ArchUnitNET/隔离构建、CLI/HTTP/比较均通过。仅固定 SDK 9.0.314 的 net8/net9 两例未在该镜像重复，Windows 已验证；不将它写成 Linux 109 项通过。
- Linux 最终运行 `D:\ArchSift-lab\runs\linux-953b0ab394714fc3941ad42286f7fa6f`，result.json SHA-256 `b543a9f4a36f569f6ac256de9577c7bd59a0d37aca311998a0472949b7096e8e`。容器按已存在镜像 ID 固定，禁止网络/pull、只读根、cap-drop ALL，源码副本与输出在外置 lab。同一两项目违规输入的 Windows/Linux 核心 JSON（除本机路径/时间元数据）逐字段相等，包括 inputs、findings、coverage、状态及引擎身份。
- 每次 .NET 检查及最终 User/Machine、Process PATH、四个 Profile 哈希相等。Linux 的 workload 提示保留，未依提示运行更新或自动修复。

## SARIF 与 CI

- Analysis 与 comparison 导出 SARIF 2.1.0；CLI 配置、UI 下载、错误/例外/无映射及 fingerprint fixtures 通过。只有全面可比结果使用 baselineState；partial/policy change 不伪造解决。JSON 仍为权威，HTML/SARIF 为投影。
- OASIS 官方 schema 下载到外置 evidence；SHA-256 `ad6db49878699b091f3eeb765b6e29e92a34bad4da88664d000c923b549c3a25`。Windows/Linux 普通报告、原生包普通/比较 SARIF 均通过独立 PowerShell Test-Json。
- 自身 Windows/Linux CI 和 samples/ci workflow_dispatch 样例以官方 action commit SHA 固定；actionlint 通过。首轮 runner context 作用域错误被真实检测并修正。当前只记录本地 YAML 校验；远程 CI 是否通过以实际 run 为准。
- 样例不写消费 CI/required checks，不启用 code scanning；违规 exit 0，错误 2/3/4/130 保留且 always 上传 artifact，不用 continue-on-error 隐藏失败。

## 候选与 UI

- 先提交 `63419d4` 冻结 W10 固定输入预算，再测候选。Windows 自包含原型 ZIP SHA-256 `3991e436833bc3936a919fcf46c503a2e29f6f7fc587506c2e1d540731f3e966`，位于 `D:\ArchSift-lab\packages\release-c13e99ee76874803aa0c21f98efd7808`；它基于未提交 W10 工作区，只作为功能候选证据，禁止作为最终上传资产。
- 新解压 smoke `smoke-409d0e8f22e74381975c6fac491f4817`：版本、许可/hash/字节/文件集合、native CLI/Worker/UI、缺失 Git exit 3、只含 Git 而无 SDK PATH 的 changes（新增 1 / 既有 100）、SARIF/schema、参数错误、无绑定 exit 4 均通过。
- 五次候选分析 111–139ms，墙钟 231–269ms，采样峰值约 29–33MiB；低于 700ms/1000ms/64MiB。仅声明链输入，不承诺 Git 快照/IFX/构建性能，也不声称强制 cold disk。
- 最终真实浏览器合成运行 `w09-ui-a028b68eaacd4a80a4dd18afbab8dd23` 显示已知新增违规及 JSON/HTML/SARIF 三下载入口，[截图](w10/sarif-comparison.png)。认证 HTTP 已实际下载 SARIF。预览 PID 经配置/入口精确确认后停止，端口 12320 listener=0，preview-host-state-equal=True。未替独立操作员打分，未运行真实 Guard/IFX。

## 发布边界

用户选择 MIT；第三方库与 .NET runtime 完整 notices 不变。正式包将从已提交产品输入构建，source commit、产品输入哈希、逐文件 manifest 和 ZIP SHA-256 固定后再验收/上传。冻结候选不改写。W08 与失败/中断历史全部保留。

NuGet tool 分发、W11 allowlist/UX、Linux Web UI、传递包/版本范围、网络漏洞审计、fail-on、强制 CI 不在本轮交付内。没有发布前自动新增消费项目策略。
