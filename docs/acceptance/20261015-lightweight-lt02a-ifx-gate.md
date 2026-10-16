# 轻量架构测试工作台 LT02A：IFX 真实接入门槛

日期：2026-10-15（Australia/Sydney）。结论：**LT02A REAL IFX GATE COMPLETE / LT03–LT06 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。这项结论只覆盖获授权的独立 IFX 副本与机制验证 JSON，未批准任何正式 B 类政策，也不替代最终独立人工 UI 验收。

## 目标与输入身份

- 原 IFX checkout：`D:/IFX-10-Root/IFX-New`，`main` 的 `cc06528ab17052baafa496df2a89f6cde9207384`；验收后 `git status --short --branch` 仍为 `## main...origin/main`。未运行应用入口，未改原 checkout、安装或 Guard。
- 独立副本：`D:/ArchSift-lab/fixtures/lt02a-ifx-cc06528-20261015`，本地无硬链接 clone，detached HEAD 同提交。仅此副本新增 `ArchSiftJsonTests.cs`、机制 JSON、清空隐式源的 `NuGet.config` 及 1 条指向本仓库 `ArchSift.Testing` 的测试项目引用。新测试和规则的可复核源文件在 [`samples/ifx-lt02a`](../../samples/ifx-lt02a/README.md)。副本内改动不构成 IFX 正式分支或政策发布。
- 原测试项目 SHA-256 `ea8b35cef1b06153c01d5adb44b0d3e1a3550bd0efe9ffe40ac113182c10b684`；[LT00 源基线](../decisions/20261015-lightweight-ifx-source-baseline.json)中的 20 个原测试源文件接入前后 20/20 哈希一致。原项目 16 个 `ProjectReference` 全保留，接入后 17 个；三个原测试类、Theory 数据和 `AllRules_RunUnderFiveSeconds` 的 5 秒阈值未更改。
- SDK `10.0.303`，运行时 `.NET 10.0.11`，Windows x64、Release/net10.0；IFX xUnit v3 `4.0.1` 与 Microsoft.Testing.Platform。restore 显式使用本机 `C:/Users/von12/.nuget/packages` 和源根外 `D:/ArchSift-lab/packages/lt02a-ifx-cc06528-20261015`，禁用网络源与 NuGetAudit；原项目与接入后 restore/build 均成功，构建 0 警告／0 错误。

## 原生基线与接入结果

原 IFX 测试工程在接入前以 MTP `--list-tests json` 发现 80 个案例，原生 CTRF 实际运行 80/80 通过、0 skip、0 failed：`ArchitectureTests` 16，`FixtureTests` 47，`ApplicationPortAdapterTests` 17。首次和再次运行的 CTRF `extra.id` 集合完全一致。MTP 发现 `uid` 与 CTRF 执行 `extra.id` 是不同 ID 体系；[80 个原执行 ID 基线](../decisions/20261015-lightweight-ifx-original-case-ids.json)取自原生实际 CTRF，仓库规范化 LF 字节的 SHA-256 `55b0f65b535516993761a0121ed4b6db26a98a698291c03056b1a85052cf3a33`，不以显示名或静态源码计数认定覆盖。早期运行配置使用同一 80 ID 内容的 PowerShell CRLF 序列化，字节 SHA-256 为 `df2ad85d73d723387342d54fc89db327038a1f61bec8e8f93cca4adbe10cf940`；最终正例以仓库 LF 字节重跑。原发现文件 SHA-256 `18b46616703bd1246a8684402a75946a0b2e8a1d8979d2ae21e7807a43c07264`，原 CTRF `046f291d200bedfdc36f20371f86b2fcbbf8258dd030da9225bbb22df276aff4`，保存在 `D:/ArchSift-lab/runs/lt02a-original-20261015/`。

接入后原工程仍全量运行 80 个相同执行 ID，另有 1 个 JSON Theory 行，合计 81/81。新增案例 `archsift-json:ifx-context-mechanism` 的规则 SHA-256 为 `abd6a7d94e3655b4255b2a280e8fa58c6c50e64ce91dce6848121d24d20a92aa`；其源 `IFX.Platform.Context.Contracts.ExecutionScope` 实际匹配，目标是不存在的验证命名空间，仅用于正向机制检查。协调器新增 `runnerFormat: "mtp-ctrf"`：先以 IFX 原生 MTP 发现，再以 CTRF 的稳定执行 ID、类／方法、终态和测试输出标记核对原案例与 JSON 规则。`--list-tests` 不被当作已执行；原生发现 UID 不被错误地等同于 CTRF 执行 ID。原先的 xUnit JSON 事件路径仍供合成工程使用。

最终仓库 LF 基线的完整门禁在 `D:/ArchSift-lab/runs/lt02a-gate-positive4-canonical-20261015` 为 `allow/0`：restore/build/test 均通过，发现／实际执行 81 项，原案例缺失 0、JSON 缺失 0、skip 0、规则摘要一致。CLI 结果 `D:/ArchSift-lab/runs/lt02a-gate-positive4-canonical-result.json` 的 SHA-256 为 `9f690dd8ba9c7def92479ddaade2de712e95553ea2557beffbe7a93a3154d3e0`。此前 CRLF 基线字节的同等正例保留在 `lt02a-gate-positive3-20261015`，结果 SHA-256 `77c6f6b79f38a07e42eb59f6853effb271dd77bcf6d7a5dafcd9cb4bf701cbf7`。

## 负例与保护

| 场景 | 真实结果与复原 |
| --- | --- |
| 父进程 `IFX_UPDATE_SNAPSHOTS=1`，受控子进程必须为 0 | 完整正例通过；11 个 API 快照逐文件 SHA-256 前后一致，父进程仍为 1 |
| 在副本的 `IFX.Platform.Context.Contracts.txt` 临时追加 1 字节 | 原 80＋JSON 1 全部执行，`PublicApi_ContractsSnapshotsMatch` 唯一失败，`block/2`；`finally` 恢复精确原字节，结果 SHA-256 `4691c0d9103b98c5f9477a98249729e44f1485365018cd3ae9559dd4d49eec95` |
| 将机制规则临时改为禁止真实 `EndpointScopeMetadata → ExecutionScope` 依赖 | 原 80＋JSON 1 全部执行，JSON 断言唯一失败、明确源／目标类型，`block/2`；`finally` 恢复规则原字节，结果 SHA-256 `babf9584bad9f98de570bf65419dd1fb3b6be04ba5980b3cdfb16feb21c2d549` |
| CTRF 输出结构差异 | 首次门禁正确因缺 JSON 标记 `block/4`；确认 IFX CTRF `stdout` 是字符串数组后修正解析并加入测试，保留 `D:/ArchSift-lab/runs/lt02a-gate-positive-20261015` 失败证据，再以新运行目录通过 |

`MtpCtrfTests` 对原生 ID、规则标记、缺原案例、断言失败、重复与损坏报告作负例。更新后的合成门禁 7 场景退出码依次为 `0,2,4,4,3,0,5`，原生合成套件 16/16。ArchSift.slnx Release 构建 0 警告／0 错误；旧 UnitTests 151/151、IntegrationTests 95/95、ArchitectureTests 2/2，均 0 skip。所有 .NET 操作检查的宿主安全环境／Profile 哈希保持 `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`，未检测到持久漂移。

## 初步成本和后续边界

同一 IFX 副本原生首次全工程测试约 `4.245 s`、热运行约 `1.163 s`；接入后单独测试进程首次约 `3.536 s`，热完整门禁（含显式离线 restore、增量 build、原生发现和测试）观察值为 `11.189 s` 与最终 LF 基线的 `15.767 s`。这些是 LT02A 初测，不是同条件三次冷热对照；门禁包含原生测试没有的阶段，不能声称比原生测试更快。LT05 再按冻结口径测冷／热各至少三次、阶段与磁盘增长，并观察五秒断言是否在并行负载下稳定。此次所有真实运行的原五秒断言均通过，未放宽阈值。

LT03 尚需交付独立 `architecture-test-run` JSON/HTML、来源责任清单、结构化 finding 投影与基本历史；当前 CLI 即时结果和外置运行日志不冒充完整报告。未迁入 A 类和正式 B 类仍未被本次日常门禁证明。最终独立人工 UI 视觉／操作验收留给用户。
