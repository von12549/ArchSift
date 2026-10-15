# 轻量架构测试工作台 LT02 合成门禁验收

日期：2026-10-15（Australia/Sydney）。结论：**LT02 SYNTHETIC COMPLETE / LT02A REAL IFX NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。代码提交 `3ddd0dc0717ce7640b9a6f9755fe60a306f62e8d`，分支 `codex/archsift-lightweight-workbench`。本记录与计划状态另作阶段记录提交。本阶段完整通过只针对合成测试工程；不能代替 LT02A 的 IFX 原全工程基线或实际 B 类政策审查。

## 本阶段交付与保证

- 新无界面入口 `archsift test-gate --config <JSON>` 调用 `ArchSift.TestGate` 共享协调服务，按明确工程执行本机离线 restore、正常增量 build、MTP 可执行测试工程。子进程都强制 `IFX_UPDATE_SNAPSHOTS=0`；隔离 `DOTNET_CLI_HOME` 时同一子进程设置 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`。restore/build/test 各自日志写入源根外新建运行目录；取消只终止所启动的进程树。
- 原生 xUnit v3 JSON reporter 的 `TestCaseUniqueID` 对应发现的方法／案例，`TestUniqueID` 对应实际执行的 Theory 行。协调服务按原生 `test-starting`、终态与 `test-finished` 核对原测试 ID；JSON 测试通过原生 `test-output` 与同一个 `TestUniqueID` 关联规则 ID、规则文件 SHA-256。未用显示名称解析身份，未把发现数当执行数。
- 全工程、启用 JSON 规则、原案例基线均须非空。必检漏跑／skip／无终态／重复／规则摘要不符／过滤阻断；实际断言违规返回 `2`，还原／构建／测试进程错误返回 `3`，覆盖不确定返回 `4`，配置错误返回 `5`，取消返回 `130`。运行中相关源码事件或明确配置／规则文件哈希变化阻断。本次 gate 不要求 `SourceBound`，不运行旧 evidence、快照／capture／seal／BindingVerifier。
- ArchUnitNET 类型模型由测试入口在本次进程内创建并供规则案例复用；无跨进程／worktree 缓存。CLI 仍使用旧主入口但调用相同协调服务，UI 调用留待 LT04。

## 机器检查与输入身份

SDK `10.0.303`，运行时 `.NET 10.0.11`，Windows x64，`net10.0`，Release；本机 `C:/Users/von12/.nuget/packages` 为显式离线源，仓库忽略的开发包缓存为 restore 输出。`ArchSift.slnx` 12 项目 `--locked-mode` 串行还原与 Release 构建通过，构建 0 警告／0 错误。MTP 合成套件 14/14 通过；旧 UnitTests 151/151、IntegrationTests 95/95、ArchitectureTests 2/2，通过且均无 skip。

[合成验证脚本](../../scripts/Verify-Lt02Synthetic.ps1)运行前从本次 MTP 原生执行生成独立于 JSON marker 的 13 个合成原案例 ID 基线（该合成基线并非 IFX 信任基线），该轮基线字节 SHA-256 为 `c86bb6c2439cb4d53d45aa473a4d5c988add7782b25b6bc33714afabfaf10be0`。正常规则 `rules.json` SHA-256 为 `b9669d62d9195f9322a1fda34b46003472564070c8ec492d71234900cc88b341`；故意违规规则 `rules-violation.json` SHA-256 为 `f2a9e089715735f07b1ef55f8e4462ef9e4ee6b80dfdbb75780e1683e3470274`。这些是合成输入，不是已启用 IFX B 政策。

| 合成入口场景 | 实际结果 |
| --- | --- |
| 全工程：13 个原案例＋1 个 JSON 案例 | `allow/0`，restore/build/test 均通过 |
| 故意 JSON 类型依赖违规，原案例仍全跑 | `block/2`，MTP 真实断言失败 |
| 基线额外要求一个未运行原案例 | `block/4`，明确缺失原案例 |
| `-id` 过滤一个原案例 | 实际只跑 1 个原案例，`block/4`、subset，JSON 案例未运行 |
| 空本机离线 feed 与空包缓存 | restore 失败，`block/3`；随后正常离线恢复再次 `allow/0`，未忽略失败 |
| 空 rulesets 配置 | `block/5`，未启动运行、未创建运行目录 |

测试调用方进程将 `IFX_UPDATE_SNAPSHOTS` 设为 `1`；每次完整子测试都断言读取 `0`，调用方值保持 `1`。取消用例对启动的长时间子进程在 300ms 后取消，验证快速终止；源码观察用例验证 `src/*.cs` 变化触发阻断标志而 `bin` 输出不触发。MTP 解析负例分别验证漏行、skip、重复、规则 SHA 不符、缺少终态、进程错误与 subset。所有 .NET 操作前后安全环境／Profile 哈希均为 `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`，无持久漂移。旧 VSTest 工程仍需在获准宿主执行环境运行；受限沙箱 testhost 连接问题已在 LT01 记录。

## 限制与 LT02A 门槛

合成基线来自当前测试自身，仅用于证明协调逻辑；CLI 配置中的基线路径／哈希不是独立受信的 IFX 保留清单。真实 IFX 原 `ArchitectureTests`／`FixtureTests`／`ApplicationPortAdapterTests` 全工程案例、Theory 行、断言和五秒性能尚未实际验证。本阶段仅保存阶段日志与即时 JSON 结果；完整 `architecture-test-run` 报告、HTML、来源清单、基本历史和 `historyFreshness` 留待 LT03，UI 留待 LT04。源码观察是轻量运行中变化检测，不是完整源构建绑定证明。LT02A 必须在已获授权的 `D:/ArchSift-lab` 独立 IFX 副本建立可信的原生原测试基线、验证接入前后案例与源码不减损、快照正反例和初步冷热成本；未通过前不进入 LT03。
