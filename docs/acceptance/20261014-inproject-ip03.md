# ArchSift.InProject 0.1.0 IP03：xUnit 规则与严格门禁验收

日期：2026-10-14（Australia/Sydney）。状态：**IP03 MACHINE COMPLETE / IP04 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。本记录只覆盖外置合成 checkout，不涉及 Guard、IFX 或真实业务目标。

## 本阶段交付

专用 net10.0 xUnit 测试工程不属于业务 solution。固定包中的 `ArchitectureRules.cs` 按冻结的 JSON 规则键发现 B 类测试；桥接从本次 Worker 材料读取逐规则原始状态，核验材料摘要，写入每测试回执。Runtime 对 A 类使用共享 Core，对 B 类使用同一 ArchUnitNET Worker 的程序集模型，并交叉核对测试发现清单、TRX 结果、回执、输入／规则／工具身份及 IP02 独立绑定。测试只提供逐规则诊断；`InProjectGatePolicy` 统一计算严格门禁，不把 Worker 或测试通过本身当作源码绑定。

新增版本 1 的 [`gate-result.schema.json`](../../schemas/inproject/gate-result.schema.json) 与 Contracts，分别保存原始规则状态、finding、覆盖、门禁决定、材料等级和新鲜度。CLI `gate --project` 返回门禁码；IP05 前 `--evidence full` 明确返回 5。当前成功运行写 `gate-result.json`，持久任务、失败／取消的统一当前态、历史及投影在 IP04 实现。

## 实际合成检查

可重放入口为 [`Test-InProjectIp03.ps1`](../../scripts/Test-InProjectIp03.ps1)，最终运行根为 `D:\ArchSift-lab\runs\inproject-ip02-probe-3d69018686b64731adbf4f59acec2877`，对应 fixture 位于同名 `D:\ArchSift-lab\fixtures` 目录。Windows x64、SDK 10.0.303，产品与专用测试为 Release/net10.0，业务合成 solution 为 Debug/net8.0、net9.0、net10.0 四项目。脚本明确指定 `-TargetPackageSource https://api.nuget.org/v3/index.json` 仅填充合成参考包；产品恢复使用本机外置包缓存作显式离线 feed。每个 .NET 操作前后比较安全环境／Profile 哈希，仅输出摘要与相等结果；本次全程相等。

| 场景 | 实际结果 |
| --- | --- |
| 既有 IP01／IP02 定向回归 | 15/15 集成测试通过；实际 build、11 类拒绝／失败／漂移／取消矩阵通过 |
| 六种规则类型、七条启用规则 | 五条 A 类、两条 B 类全部终结一次，`requiredRules=executedRules`，通过运行 `0a2f2e0272a842e98f605902ae97cda4` 的 `sourceBound=true`、schema 有效、门禁退出 0 |
| 专用 xUnit 真正发现／执行 | 通过运行的 TRX 为 2/2；违规运行 `4895bd118987447bb814bdad6a3c0e20` 的 TRX 为 1/2，违规 B 测试实际失败 |
| 真正 ArchUnitNET 类型依赖违规 | B.Bar 依赖 A.Foo 形成独立 finding；逐规则状态为 `violation`，schema 有效、门禁退出 2；没有将测试失败误作执行错误 |
| 未提供完整材料及篡改测试源 | `--evidence full` 返回 5；改动固定测试源后预运行拒绝，返回 5 |
| 业务执行安全 | 两次门禁后目标模块初始化器标记仍不存在；没有运行目标应用入口或功能测试 |

严格状态表另有 [`InProjectGatePolicyTests.cs`](../../tests/ArchSift.IntegrationTests/InProjectGatePolicyTests.cs) 覆盖通过、违规、无法判定、错误、取消、失去绑定、零测试、漏规则、全不适用及可归属／不可归属 partial。Worker 使用 ArchUnitNET 0.13.4；业务程序集按 IP02 本次实际编译上下文及完整引用闭包传入。两次门禁的 JSON、逐阶段日志、Worker 材料、envelope、回执和 TRX 均保留在 fixture 的 `.archsift/reports/<run-id>/`。

## 范围与下一入口

IP03 的运行路径已经完成逐规则机器验收。任务持久化、失败／取消后的统一 `gate-result`、当前与历史分离、轻量报告投影和 CI 示例属于 IP04；完整证据、轮转／归档属 IP05。真实业务目标仍需具体授权，最终独立人工 UI／易用性验收仍由用户执行。此次没有把合成结果当作产品总验收或发布资格。
