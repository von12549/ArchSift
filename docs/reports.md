# JSON/HTML/SARIF 结果与可信度

report.schema.json 是 schemaVersion=1 权威契约。operation、源/规则/引擎/程序集输入身份、scope、buildContext、engineVersions、execution、compliance、ruleResults、findings、coverage、limitations、executionErrors 和 runMetadata 均显式输出。

逐规则 pass/violation/inconclusive/not-applicable/error 与总体执行状态分开。已判定违规默认 exit 0；有违规与无法判定同时出现时总体 noncompliant 并保留部分覆盖。没有有效违规但任何需要的规则无法判定时不得 compliant；全部 disabled/不适用为 not-applicable；无规则 analyze 为 null。

未绑定程序集保留实际 DLL finding，但相关规则是 inconclusive，不能从旧 DLL 推出当前源码不合规/合规。规则例外保留原 finding 和稳定 id/reason，统计有效违规时排除明确豁免。源码/规则/程序集在运行中变化导致本次当前源码结论失效，独立 runId 不借用上次结果。

核心 finding 排序和 ID 稳定；输入身份含相对源记录、原始规则哈希、程序集字节、TFM/configuration 与引擎/工具版本。时间、耗时、本机路径位于 runMetadata/定位字段。无源码映射时只显示程序集和类型位置，不虚构行号。HTML 是 JSON 数据的投影，所有用户文本均转义。

每次保存新 runId；历史报告保留其输入身份，不能当作当前运行。配置错误可在分析前拒绝；执行错误/取消时保留已完成结果和未完成规则标识。报告写入失败返回 3，不等同于合规通过。

## SARIF 2.1.0

配置 output.formats 可加入 sarif，生成 report.sarif / comparison.sarif；UI 也提供下载。JSON 仍为权威。稳定 finding ID 投影为 partialFingerprints；info 映射 note，warning/error 保留。例外为 external accepted suppression，程序集来源不足的 finding 使用 review，不冒充可判定的 fail。只对确实位于 inputIdentity 的根相对文件附 artifact URI，按路径分段 URI 编码；没有源码映射就不附文件位置，不虚构行号。

invocations 记录 executionSuccessful 和执行/覆盖 notifications；properties 保留 execution、compliance、coverage、逐规则状态与输入身份。只有 completed 的全面比较才附 baselineState new/unchanged/absent；政策变化或部分比较不作该承诺。

投影按 [OASIS SARIF 2.1.0](https://docs.oasis-open.org/sarif/sarif/v2.1.0/os/sarif-v2.1.0-os.html) 与 [GitHub SARIF 支持](https://docs.github.com/en/code-security/reference/code-scanning/sarif-files/sarif-support) 核对。只有逻辑/程序集位置的结果可能不会显示为 GitHub 源码告警；默认仅上传 artifact，不擅自启用 code scanning。

相对 artifact URI 显式关联 `%SRCROOT%` 和 originalUriBaseIds 的实际源码根 file URI；比较报告使用原始 TargetRoot，而不是一次性快照目录作为消费映射基准。此信息与已有 JSON 的 TargetRoot 一样属于本次输入定位，不是虚构源码映射。
