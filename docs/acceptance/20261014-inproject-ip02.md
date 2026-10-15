# ArchSift.InProject 0.1.0 IP02：日常构建与输入关联验收

日期：2026-10-14（Australia/Sydney）。状态：**IP02 MACHINE COMPLETE / IP03 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**。本记录只覆盖 ArchSift 独立外置合成 checkout；不涉及 Guard、IFX 或真实业务目标。

## 交付和边界

实现了每次新 run-id 的业务输入 SHA-256 清单与源快照、owned 构建／报告目录、项目拓扑和逐项目 TFM／Configuration 选择、普通 `.sln` 的配置映射检查、简单 `.slnx` 范围检查。隔离子进程使用 child-only `DOTNET_CLI_HOME` 和 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`，恢复使用清空默认源后的明确本地 feed；随后逐上下文 `--no-restore` 编译。每个上下文保留实际 `csc` 命令、展开的响应文件、源码／生成源码、引用的程序集身份和哈希、copy-local、SDK／import／assets、输出 PE 哈希及构建前后求值。独立绑定验证器复核已落盘捕获、原根、快照、规则／运行时锁、引用闭包和材料哈希后才写 `SourceBound=true`；构建命令不直接声明结果。

当前支持边界是可审查的 SDK-style C# 标准构建。自定义 task／target／显式 import、未经审查的 analyzer／source generator、包 build 资产、外置直接引用／祖先构建输入和自定义输出重定位一律拒绝；它们不被误报为通过。`allowNetwork=true` 在产品路径中不可用。失败和取消保留独立运行目录、诊断和 `sourceBound=false`，不沿用旧报告；具体任务历史／轮转和轻量报告属于 IP04／IP05。

## 实际检查

可重放脚本为 [`Test-InProjectIp02.ps1`](../../scripts/Test-InProjectIp02.ps1)，运行时显式提供 `-TargetPackageSource https://api.nuget.org/v3/index.json`。只有合成 fixture 的首次参考包填充使用该明确源；产品运行读取外置缓存作为本地离线 feed。本机 SDK 10.0.303 所需 net9.0 9.0.19 参考包原先不在本机，先前离线 restore 的 NU1102 失败保留于 `D:\ArchSift-lab\runs\inproject-ip02-probe-c9682cd244c54c6c8061a57871868600`；未忽略失败或把缺失包视为通过。

最终运行目录 `D:\ArchSift-lab\runs\inproject-ip02-probe-48e5edfeda4245449764c38ada2600fa`，合成 checkout `D:\ArchSift-lab\fixtures\inproject-ip02-probe-48e5edfeda4245449764c38ada2600fa`。实际开发包 SHA-256 `ee721d611beeacd39f18533e8113d2c3cc6c6e1c33280cf37ac865ec4b133f10`；这是含未提交 IP02 工作树源码的开发包，不是固定发布候选。所有 .NET 步骤和负例检查的 User／Machine 环境、Process PATH、四个 PowerShell Profile 综合哈希与基线相等；命令只输出摘要与相等性。

| 场景 | 结果 |
| --- | --- |
| Release / net10.0 的 InProject Runtime、CLI、取消探针及集成测试恢复／构建 | 成功；安装与编译捕获相关测试 6/6 通过 |
| 合成标准 `.sln`：A/net8.0、B/net10.0、C/net9.0、D/net10.0，Debug | 四个上下文均有实际 `csc`、独立材料与引用；`daily-binding.json` 通过 draft-07 schema；运行 `af63a87750ca4a3689109b7a10ccdf3e` 的验证器 `SourceBound=true`，capture SHA-256 `ba06f6e7934e718303384d3dde9a1da19420ca1818816f616540e8d684dff00b` |
| 同名异版本引用 | B 选中 `Shared, Version=1.0.0.0`，D 选中 `Shared, Version=2.0.0.0`；路径、身份与字节哈希分别记录，没有按文件名合并 |
| 模块初始化器及业务入口 | 合成业务只有类库；B 的模块初始化器写文件标记未出现，目标程序集没有被执行 |
| 自定义 target、外部 DLL、错 TFM、错 `.sln` 映射、未显式选定多 TFM、未知 analyzer、缺失显式源码 | 均被拒绝，退出码 5；没有当前绑定成功 |
| 运行中源变化 | 快照开始后修改 B 源码，退出码 4，当前绑定不可用；原文件随后按原字节恢复 |
| 取消 | [`InProjectCancelProbe`](../../scripts/InProjectCancelProbe/Program.cs) 在 owned run 形成后取消；状态 `cancelled`、`sourceBound=false`，运行 `0892fc98fa254771807ce04722fb252b` 是先前探针结果；最终矩阵也通过同项 |
| 编译失败、离线恢复失败 | 均退出码 3；失败运行保存日志、`build-state.json` 和 `binding-verdict.json`，两者均为 `sourceBound=false` |
| 响应文件丢失、落盘捕获篡改 | 专用集成测试分别拒绝，未凭生产者内存状态重写成功 |

较早简单 `.slnx` 两项目正例在 `D:\ArchSift-lab\runs\inproject-ip02-probe-378c7a3886ba49f2aede1c58842454b6` 通过 net8.0/net10.0 实际构建、schema 与 `SourceBound=true`；最终四项目运行选用标准 `.sln`，以覆盖其配置映射。日志、临时包、快照与失败材料均保留在对应外置目录，没有清理或修改目标业务源。

## 限制与后续

此阶段验证构建关联，不表示六类规则已运行。未经审查的生成器、自定义任务、外部引用及复杂 solution 映射需要以后明确扩展支持矩阵；当前行为是阻断。实际编译日志的响应文件没有出现在这组合成项目里，展开和缺失阻断由专用测试覆盖。IP03 才接入独立 xUnit 真实规则执行和严格门禁；IP04／IP05 才完成持久任务、轻量／完整材料及轮转。真实目标须具体授权，独立人工验收仍由用户执行。
