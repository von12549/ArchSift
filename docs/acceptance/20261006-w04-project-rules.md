# W04 项目规则验收

日期：2026-10-06（Australia/Sydney）

结论：W04 COMPLETE。项目引用、两类图完整性、literal TFM、直接 NuGet ID 禁止列表、项目/源码路径命名和未接受的规则建议已实现。程序集/type 命名与 type-dependency 留给 W05，未用声明图代替。

逐规则结果区分 pass、violation、inconclusive、not-applicable。source/scope 零匹配默认 inconclusive，显式 allowEmpty 才为 not-applicable；disabled 不运行。已知违规保留并优先报告 violation，同时保留部分覆盖 limitations。例外关联稳定 id/reason，原 finding 与 severity 不删除；有效违规计数排除明确豁免项。finding ID 由规则 ID、主体、依赖证据组成并稳定排序。

规则建议分别输出 observedFacts、candidateRules、unresolvedDecisions。候选全部 disabled，只建议内部引用解析与工具支持框架的审查；既有依赖不自动成为允许策略。

最终运行 `76d50c072ddb469c8ca60018d6d910c1`：SDK 10.0.303、net10.0、Debug；offline locked restore/build 成功、零警告/错误；65 unit + 12 integration + 2 architecture = 79/79 通过，主机环境/Profile 哈希每步及最终相等。

新增真实声明 fixtures 覆盖每类项目规则 clean/violation/missing/unsupported、NuGet 大小写与只做 ID 检查、例外证据保留、allowEmpty/disabled、solution-membership 缺失前置、external/uncovered、稳定 finding ID、源码路径命名和候选未接受状态。

归档：[checks](w04/checks.json)、[host-final](w04/host-final.json)、[build](w04/build.stdout.log)、[test](w04/test.stdout.log)。没有额外第三方依赖或 Guard 源码迁移。W05 将构建程序集输入、Worker、真正 ArchUnitNET 检查、isolated/offline 构建和可信度负例。
