# IFX 现场分析与独立操作员任务

状态：`COMPLETE / PASS`。真实 IFX、构造样例、独立操作员评分与最终安全清理均已完成；详情见 [W08 完成报告](20261007-w08-completion.md)。

前置：使用 W08 候选 ZIP 的新解压目录，先完成包 hash/manifest 校验；TargetRoot 为原 IFX checkout，只读，报告在 D:\ArchSift-lab。原参考 HEAD 为 `64ef2674c57e6cf9031481d6d4410cca55b0dad5`，操作时重新记录实际 HEAD/普通改动，不覆盖用户改动。

## 第一块（仅发现，不 build/restore）

在正常 PowerShell 中执行这一块，完成后回传 `operator-result.json`；不要自行连跑后续块：

```powershell
pwsh -NoProfile -File D:\ArchSift\scripts\Invoke-IfxManualStep.ps1 -PackageRoot D:\ArchSift-lab\packages\smoke-8492698999f94ea9a535a42263fc417f\unpacked
```

该脚本校验候选文件、记录实际 Git HEAD/普通状态和环境/Profile 安全哈希，使用原生 CLI analyze、显式 net10.0/Debug、默认已有产物模式，无规则要求。它不 restore/build IFX，不运行 Guard，不修改源码/CI/持久环境。0 表示声明发现完成；4 表示存在准确报告的覆盖限制，不应改填 success。其他非零和安全漂移需保留证据后停下。

收到第一块结果后，再审查第二块的规则验证/UI 配置命令，逐块运行。若 IFX 的条件/表达式或生成器未支持，应由操作员从报告确认“无法判定”及原因，不能使用旧 Guard 8/8 或手写 manifest 冒充新能力通过。

## 独立操作员任务（A10）

由至少一名未参与实现的人独立完成：选择支持的目标/入口与外置输出、无规则发现、从模板创建项目引用或 NuGet 规则并写理由、运行并识别违规、制造/查看缺失输入的无法判定状态、保存规则与导出报告/Markdown；理解两类结果为何不同。再查看真实 IFX 报告及一个构造违规样例。

记录真实开始/结束、使用文档、帮助等级 L0–L4、confidence 1–5、每个任务结果与失败证据。L0 独立、L1 一般提示、L2 定位文档/解释一次错误、L3 逐步字段指导、L4 代操作。A10 要求 confidence>=4/5 且最高 L2；实现者不能代填，浏览器自动化成功不等于独立可用性通过。

现场测试完成后复核 User/Machine 环境、Process PATH、四个 Profiles 与原 checkout；仅精确清理记录的测试产物，保留 reports/operator evidence，不递归删除源码或宽泛 lab 根。

## 完成结果

真实 IFX 在 existing/net10.0/Debug 下发现 105/105 项目；105 条声明模型限制准确形成 partial，0 execution error。精确项目引用规则命中 `IFX.Application.Primitives -> IFX.Domain.Primitives`，结果为 partial/noncompliant。未 build/restore，目标、产品和持久环境未变化。

独立操作员完成 fixture 与真实 IFX UI；最终 confidence=4/5，最高帮助等级保守按 L2，A10 PASS。最终清理结束精确确认的孤儿候选 UI 进程，不删除证据；最终审计候选进程为 0、三个登记端口 listener 为 0、host/IFX/product 全部一致，`failure=null / pass=true`。
