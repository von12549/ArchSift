# JSON/HTML 结果与可信度

report.schema.json 是 schemaVersion=1 权威契约。operation、源/规则/引擎/程序集输入身份、scope、buildContext、engineVersions、execution、compliance、ruleResults、findings、coverage、limitations、executionErrors 和 runMetadata 均显式输出。

逐规则 pass/violation/inconclusive/not-applicable/error 与总体执行状态分开。已判定违规默认 exit 0；有违规与无法判定同时出现时总体 noncompliant 并保留部分覆盖。没有有效违规但任何需要的规则无法判定时不得 compliant；全部 disabled/不适用为 not-applicable；无规则 analyze 为 null。

未绑定程序集保留实际 DLL finding，但相关规则是 inconclusive，不能从旧 DLL 推出当前源码不合规/合规。规则例外保留原 finding 和稳定 id/reason，统计有效违规时排除明确豁免。源码/规则/程序集在运行中变化导致本次当前源码结论失效，独立 runId 不借用上次结果。

核心 finding 排序和 ID 稳定；输入身份含相对源记录、原始规则哈希、程序集字节、TFM/configuration 与引擎/工具版本。时间、耗时、本机路径位于 runMetadata/定位字段。无源码映射时只显示程序集和类型位置，不虚构行号。HTML 是 JSON 数据的投影，所有用户文本均转义。

每次保存新 runId；历史报告保留其输入身份，不能当作当前运行。配置错误可在分析前拒绝；执行错误/取消时保留已完成结果和未完成规则标识。报告写入失败返回 3，不等同于合规通过。
