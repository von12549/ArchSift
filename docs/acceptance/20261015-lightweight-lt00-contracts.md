# 轻量架构测试工作台 LT00 静态验收

日期：2026-10-15（Australia/Sydney）。结论：**LT00 设计与静态基线 COMPLETE；LT01 NOT STARTED；产品 NOT ACCEPTED / NOT RELEASED**。该结论不表示测试工作台已实现，也不表示 IFX 测试已运行。

分支从 `main` 的 `acbcff1c18b30f33a41c98e8e76cc5a2ecd3d68e` 建立。两份计划文档作为分支首个提交 `d1b089d` 已推送到 `origin/codex/archsift-lightweight-workbench`；用户随后另行授权提交并推送本 LT00 记录及相关契约。旧 InProject 分支保持 `2877974fb38a12e856a6045ba223615c200841ee`，未合并。

## 固定交付

- [LT00 契约](../decisions/20261015-lightweight-test-workbench-lt00-contracts.md)：功能身份、组件依赖、支持矩阵、规则责任、原测试基线、门禁/历史分离、快照子进程隔离、测量口径。
- [IFX 源码基线](../decisions/20261015-lightweight-ifx-source-baseline.json)，SHA-256 `71e374234ba02e138c763c67f91a2fad8409decdb2d278ca3e6d50cadb0a538f`：IFX clean HEAD `cc06528ab17052baafa496df2a89f6cde9207384`，原工程 16 个项目引用、20 个相关 C# 文件哈希；三类 65 个方法、按源码内联数据推导的 80 个案例与每条 Theory 行。由[只读生成脚本](../../scripts/Generate-Lt00IfxBaseline.ps1)核对 checkout 身份后生成。
- [规则归属清单](../decisions/20261015-lightweight-policy-inventory.json)，SHA-256 `2d64650ae2057fd3249e0a5398585b1210e5bfa06dca4ea514b8f8ef2a50bf01`：IFX 原安装库五个 JSON 源文件字节哈希及 588 个唯一 ID。A 类 182 条，B 类候选 406 条；全部标记为未选入新 JSON 日常测试，关系标签只供审查。
- [独立报告 schema](../../schemas/architecture-test-run.schema.json)，SHA-256 `5e0cc59c28312b35740ba917294b58ebfc0a3a7da511a2e3c6bacb9bb455292c`：`architecture-test-run` v1，来源/责任/执行覆盖、原生与 JSON 状态、未纳入项、`runGate` 和新入口退出码；历史新鲜度另作只读展示。

## 本次验证

只读确认 IFX 原 checkout clean、HEAD 与固定基线相同。生成器严格检查该身份；重复生成基线成功。静态检查确认三类分别为 `16/16`、`41/47`、`8/17`（方法/推导案例），合计 `65/80`；20 个相关源码文件与 16 个项目引用已记录。规则清单的五源 `24+33+35+90+406=588` 与唯一 ID 数 588 相同，`dailySelection=not-selected` 数亦为 588。三份新 JSON 均经 Python 标准 JSON 解析；`git diff --check` 无空白错误。schema 的 Draft-07 元验证器当前本地不可用，故本次只确认 JSON 语法与人工契约审查，未声称完整 schema 元验证通过。

本阶段没有运行 dotnet、restore、build、MTP 测试、IFX 应用入口、安装或取证；没有目标写入。SDK 10、`net10.0`、xUnit v3／MTP 是从 IFX `global.json`、项目文件和中央配置读取的设计基线，不是本轮运行环境测量。环境/Profile 哈希门禁适用于后续 .NET 构建/测试和真实集成操作，本次未触发。

## 后续实际门槛

LT01 需另获实施授权并以合成 net10.0 测试验证 ArchUnitNET 真正的正反例。LT02 需验证运行器结构化案例身份、完整执行与阻断语义。LT02A 还需具体 IFX 外置副本/操作授权；届时通过真实运行器核对原 80 个静态推导案例是否实际发现/执行、原断言与 Theory 行不减损，验证父 `IFX_UPDATE_SNAPSHOTS=1` 而子为 `0`、API 正反基准、五秒断言和初步冷/热成本。LT02A 未通过前不进入报告历史或 UI 阶段。用户独立人工 UI 验收仍由用户填写。
