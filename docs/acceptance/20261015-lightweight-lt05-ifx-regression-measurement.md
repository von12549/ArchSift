# LT05 IFX 回归与同条件测量验收

日期：2026-10-15（Australia/Sydney）。状态：**LT05 MACHINE COMPLETE；LT06 未开始；产品未接受、未发布**。本阶段所有目标操作均在用户授权的 `D:/ArchSift-lab` 独立副本／工作树执行；原 IFX checkout 和安装只读。最终独立人工 UI 视觉与动手验收仍由用户完成。

## 固定输入与基准

目标为 `D:/ArchSift-lab/fixtures/lt05-shared-cc06528`，源提交 `cc06528ab17052baafa496df2a89f6cde9207384`；SDK 10.0.303、net10.0、Release、MTP/CTRF、显式本地 NuGet 源 `C:/Users/von12/.nuget/packages` 与外置包缓存 `D:/ArchSift-lab/packages/lt02a-ifx-cc06528-20261015`，配置固定为离线。接入规则 SHA-256 `abd6a7d94e3655b4255b2a280e8fa58c6c50e64ce91dce6848121d24d20a92aa`，新增适配器与 LT02A 样例字节一致；正式 IFX B 政策未采纳。

初次在新路径原生运行时，80 个 IFX 原测试名称、三类与 Theory 行均与 LT02A 相同且 80/80 通过，但 CTRF 执行 ID 与 LT02A 另一工作树的全部 80 个 ID 不同；同一构建重复执行的 ID 则一致。脚本按基准不匹配立即停止，保留 `D:/ArchSift-lab/runs/lt05-metrics-20261015/native-1/cold` 及 `id-diagnostic`，不纳入测量。共享工作树的首次预检中，原 80 项通过而新增 JSON 适配器因直接原生入口缺显式 `ARCHSIFT_TEST_GATE_CONFIG` 失败；该次保留在 `baseline-preflight-1`，也不纳入测量。只在原生测试**子进程**传入显式配置后，`baseline-preflight-2` 完成 80＋1 通过，并逐项对照旧 80 个测试名称，建立该固定路径的实际执行 ID 文件 `D:/ArchSift-lab/runs/lt05-metrics-20261015/worktree-original-case-ids.json`，SHA-256 `74a1d63dabbb0be14e139134661fed6bf0af77fe3ba00f13fc69959de66ca72b`。旧 LF 基准 SHA-256 `55b0f65b535516993761a0121ed4b6db26a98a698291c03056b1a85052cf3a33` 保留，不被自动改写。后续每份 CLI/Web 配置均显式引用新路径基准与哈希。

冷运行定义为同一工作树、全新受控进程且 IFX 目标内没有 bin/obj 构建输出；每次冷测前列明、核对绝对路径及包含关系并仅清理 178 个已记录的 bin/obj 工具目录，清单 `clean-before-*.json` 保存在外置运行根。热运行紧接对应冷测，复用其增量输出；包缓存、源文件和先前报告从未清理。原生、CLI、Web 共用固定源码路径、提交、规则字节、SDK、TFM、配置和离线包输入；原生直接运行同一 80＋1 测试工程，CLI/Web 增加同服务报告与门禁投影。Web 冷墙钟包含本地服务启动，热测复用已启动服务。外置 ArchSift.Testing 构建输出与 OS 文件缓存没有清空，因此“冷”不是整机／所有依赖缓存全冷。

## 三轮冷热与资源

18 次正式运行全部为原 80＋JSON 1 通过、无 skip、无漏 ID；CLI/Web 的每次权威报告均为 `allow/0`、588 项未纳入。原生直接测试也执行同一 81 项，结果原生 CTRF，不宣称其 CTRF 自身具备新门禁的报告语义。中位数及最小–最大范围如下；详细每次阶段、RSS 采样、bin/obj 和报告目录字节见 `D:/ArchSift-lab/runs/lt05-metrics-20261015/measured` 及 `summary.json`（SHA-256 `63c1febee31667bd0b948570ed43832452ad022398ae53de1758bd6cdfd615c9`）。

测量命令与每次运行后的验证写在 `scripts/lt05_measure.py`：依次 `prepare native 1`、`baseline native 1`，每组再单独执行 `clean <入口> <轮次>`、`run-cold <入口> <轮次>`、`run-warm <入口> <轮次>`；`summarize` 只读取已验收的 18 份记录。脚本对既有工作树、测量或摘要拒绝覆盖，复跑需另建隔离外置根并审查命令。已停止的预检与诊断证据均保留。

| 入口 | 冷墙钟 s | 热墙钟 s | 冷峰值进程树 RSS MiB | 热峰值 RSS MiB | 冷报告 KiB | 热报告 KiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 原生 IFX | 118.129（118.014–118.439） | 12.796（12.784–13.108） | 599.8 | 300.0 | 173.0 | 157.9 |
| CLI | 117.736（117.728–117.976） | 12.272（12.220–12.435） | 659.4 | 346.2 | 513.2 | 498.0 |
| Web | 118.419（117.907–118.424） | 12.367（12.251–12.449） | 702.6 | 403.3 | 513.4 | 498.3 |

冷阶段构建中位数：原生直接计时 106.150 s，CLI 按顺序日志时刻估算 106.227 s，Web 估算 106.345 s；restore 分别 3.327／3.354／3.331 s。热构建分别 5.198／5.204／5.012 s，测试分别 3.397／3.355／3.378 s；Web 冷服务启动中位数 0.212 s。CLI/Web 报告阶段估算约 0.2 s；原生 CTRF 生成包含在测试阶段。每次冷运行目标构建输出约 147.2 MiB，每次运行报告目录中位数如表，18 个 owned 报告目录合计 7,231,150 字节。峰值是定期采样的受控进程及子进程 RSS 之和，不是整机瞬时峰值；CLI/Web 分阶段耗时由顺序日志修改时刻估算。三者冷耗时接近且被构建主导，没有可证明的明显提速；不据此扩展规则或新增材料生命周期系统，也不把小于采样噪声的差异称为改善。

## 三入口一致性与真实失败

同一固定目标的最终 CLI、Web 与本地无 UI CI 模拟全部 `allow/0`；逐项比较权威报告的规则 SHA、80 个原案例 ID／结果／执行标志、1 个 JSON 案例、未纳入 ID、当前门禁与 `snapshotUpdatesDisabled=true`，结果一致。原三个测试类实际分布仍为 `ArchitectureTests` 16、`FixtureTests` 47、`ApplicationPortAdapterTests` 17；无删减、skip 或放宽五秒断言。182 项旧 ArchSift A 与 406 项未启用 B 候选仍标记未纳入，包括 TFM、solution 成员、解析引用与包闭集等，未由原工具历史 pass 或此次日常 `allow` 升级为本次已检查。对照见 `D:/ArchSift-lab/runs/lt05-metrics-20261015/parity.json`。CI 是本地非交互式 `CI=true` 模拟，未修改 IFX 工作流、必需检查或部署策略。

在同一外置工作树执行两种先改输入、后恢复的负例，父受控进程的 `IFX_UPDATE_SNAPSHOTS=1` 只作用于该次子环境，门禁继续强制其子进程为 0。API 快照添加 1 字节时，原 80＋JSON 1 全部执行，`PublicApi_ContractsSnapshotsMatch` 唯一失败，`block/2`、finding 0；报告 SHA-256 `9a22bd6908435acfcc999f40fea6b69df480ffe372305446fce60d7b3bc0479a`。将 JSON 机制规则临时改为禁止实际 `EndpointScopeMetadata → ExecutionScope` 依赖时，原 80 项通过，JSON 唯一失败，`block/2`、一条真实 finding；报告 SHA-256 `67b4df5e3f1075b671d53b7ffbbde76f377e25708a0c9b6fe7a01fddfe4323e9`。两次在 `finally` 恢复原输入字节；11 个 API 快照逐文件哈希与原 checkout 相同，规则恢复 SHA 为 `abd6a7...`，先前成功仍只是独立历史，不替代当前失败。15 份 CLI/Web/CI/负例权威 JSON 均通过冻结 schema 的本地 NJsonSchema 10.9.0 校验，0 错误。

## 源、宿主与剩余范围

LT00 基线的 20 个原测试源码 SHA-256 均一致，原 16 个项目引用均保留（另增 ArchSift.Testing 一项），原五秒性能断言仍在，11 个 API 快照与原 IFX checkout 字节一致。原 checkout Git 干净；共享外置工作树只保留 LT02A 范围的 csproj 引用、NuGet 配置、JSON 适配器和规则。ArchSift 整解 Release 构建 0 警告／0 错误；旧 UnitTests 151/151、IntegrationTests 95/95、ArchitectureTests 2/2 和 Testing.Tests 原生 xUnit v3 17/17 均通过且 0 skip。关键受控 restore/build/test、报告 schema 校验和回归前后宿主安全环境／Profile 哈希均为 `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`。

本阶段完成机器可验证范围；浏览器视觉、交互易用性和最终独立人工验收仍未由用户完成。没有发布、PR、合并、IFX 政策采纳或持久宿主配置变更。
