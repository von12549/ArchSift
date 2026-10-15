# 轻量架构测试工作台 LT01 合成闭环验收

日期：2026-10-15（Australia/Sydney）。阶段结论：**LT01 SYNTHETIC COMPLETE / LT02 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。代码提交为 `082d0ec0e92ea805c1630ee29631c0ee0ba06603`，分支 `codex/archsift-lightweight-workbench`，从 LT00 提交 `da1bcff326e895baa4c80879b4cb0a1608b0ad32` 继续。本文及计划状态另作 LT01 记录提交。用户已授权后续阶段开发、验收及每阶段提交推送，并具体授权 LT02A/LT05 在 `D:/ArchSift-lab` 下使用独立 IFX 副本执行所列离线操作；原 IFX checkout 与安装仍只读，最终独立人工 UI 验收仍由用户完成。

## 范围与输入

- SDK `10.0.303`，运行时 `.NET 10.0.11`，`net10.0`，Release，Windows x64；xUnit v3 `4.0.1`／Microsoft.Testing.Platform 原生 in-process runner。NuGet 从本机 `C:/Users/von12/.nuget/packages` 显式离线源还原到仓库忽略的开发包目录，solution `--locked-mode` 串行还原成功；未启用网络源。
- 合成正常规则字节 SHA-256：`b9669d62d9195f9322a1fda34b46003472564070c8ec492d71234900cc88b341`；合成违规规则：`f2a9e089715735f07b1ef55f8e4462ef9e4ee6b80dfdbb75780e1683e3470274`。两者仅用于机制验收，不是批准的 IFX B 类日常政策。现有 IFX 原测试源码基线仍为 `cc06528ab17052baafa496df2a89f6cde9207384`，本阶段未运行或改写该 checkout。
- 新的 `ArchSift.Testing` 适配层复用 Core schema／语义校验、规则身份、选择器与例外语义；测试案例以 `archsift-json:<rule-id>` 标识。`TypeDependencyTestEngine` 在测试进程读取实际程序集并用 ArchUnitNET 遍历类型依赖、泛型参数，不引用 UI／CLI、Worker 预计算结果或伪造 `SourceBound`。旧 `AssemblyWorker` 与新入口共用依赖目标遍历逻辑；旧报告／schema 未改。

## 实际检查

| 检查 | 结果 |
| --- | --- |
| `dotnet restore ArchSift.slnx --locked-mode`，显式本机离线源，`-m:1` | 11 项目还原通过；初次默认并行调用无诊断退出 1，串行重试通过 |
| `dotnet build ArchSift.slnx --no-restore -c Release -m:1` | 11 项目通过，0 警告、0 错误 |
| 新 xUnit v3/MTP 正常合成入口 | 7/7 通过，0 skip／not-run；实际字段、方法签名、泛型依赖违规分别产生稳定 finding，例外保留 ID／理由，零匹配为 inconclusive，全禁用、重复 ID 与不支持规则被拒绝 |
| 新 MTP 负例子进程，`ARCHSIFT_LT01_NEGATIVE=1` 仅设置于子进程 | 7 例中 1 例实际失败，退出码 `1`；失败消息包含 `ForbiddenSource -> ForbiddenTarget`，证明违规使真实参数化测试失败 |
| 旧 UnitTests／IntegrationTests／ArchitectureTests | 分别 151/151、95/95、2/2 通过，0 skip；新增项目依赖方向已登记并重验 |
| `git diff --cached --check` | 通过；代码提交含要求的 Codex `Co-Authored-By` trailer |

首次受限沙箱内 VSTest testhost 在 90 秒内无法连接，旧单元测试中止；在获准宿主执行环境按相同 `--no-build --no-restore` 参数重试后，上表三个旧工程全部通过。该中止不计作通过结果。每次 .NET 操作前后环境／Profile 哈希均为 `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`，未检测到持久漂移。测试生成的临时 JSON 在新用例中以 `finally` 精确删除。

## 覆盖边界与下一阶段

LA02–LA04 的合成机制已通过；LA05 的真实程序集范围与 LA07 的 IFX 项目运行器兼容仍待 LT02A。当前没有正式 B 类规则被选入日常门禁，未执行 IFX 原架构测试，未证明原静态推导 80 案例的运行器发现／执行数、全工程覆盖、快照开关或真实源构建因果。没有 UI、运行协调、报告历史、安装或完整取证操作。LT02 须实现无界面协调与覆盖阻断；LT02A 须在获授权的外置 IFX 副本完成真实接入门槛，之后方可进入 LT03。
