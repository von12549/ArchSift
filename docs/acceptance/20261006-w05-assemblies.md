# W05 程序集与隔离构建验收

日期：2026-10-06（Australia/Sydney）

结论：W05 COMPLETE。已接入真正 ArchUnitNET 类型依赖与类型/程序集命名、独立进程 Worker、程序集 manifest、existing/isolated 输入、离线与显式来源 restore、独立包缓存和输入/hash/context 校验。

最终运行 `529a15adc1c646cab192010b2379a7a8`：SDK 10.0.303、net10.0、Debug；八项目离线 locked restore/build 通过、零警告/错误；65 unit + 16 integration + 2 architecture = 83/83 通过。多轮真实合成 fixture 另外执行标准 SDK 子进程编译；每步及最终 User/Machine 环境、Process PATH、Profiles 哈希一致。

真正的正负例从当前合成源码在目标外创建快照后编译，用 ArchUnitNET 的 Cecil 文件加载 API 检查 namespace/type 依赖，包含泛型字段目标。Worker 不通过 CLR 加载目标 assembly，不调用业务方法；带 module initializer 的目标没有写出陷阱 marker，原 source 没有 bin/obj 且 hash 未变。编译程序集的 type/assembly naming 正负结果也成立。

manifest 核对当前源身份、构建快照输入身份、项目/assembly 映射、SDK/TFM/Configuration、实际 PE assembly identity/context 与 SHA-256。Worker 再核对 bytes、版本/token 闭包、缺失依赖，完成后复核 bytes。未绑定 DLL 保留 assemblies-only 证据但规则返回 inconclusive；没有时间戳新鲜度替代来源绑定。所有 Worker 每次新建，没有旧结果缓存；NuGet 本地 cache 可重用，artifact 重用通过 existing 的完整 hash/身份复核实现。

负例覆盖错误 hash、源改动后旧 manifest、缺失程序集闭包、选择器零匹配、TFM/configuration 不一致、项目内真实编译错误、unsafe 自定义输出路径、取消；首次错误 hash 未被结构化捕获及错误 fixture 不在项目 compile 范围的问题已修正，失败 runs 保留。当前依赖仅集成测试新增 Core/ArchUnit 项目引用，刷新 lock 后 Newtonsoft.Json 13.0.3 不再出现在实际 union；无新增第三方运行包。

标准 SDK 的 isolated 模式复制保守根内输入快照，在外部输出中写入隔离 sentinel；构建身份明确记录快照、SDK/TFM/configuration 和最终分析 assembly。它不是完整 MSBuild 文件访问轨迹。完整求值仍 defer；自定义 SDK、任务/Import、输出路径、条件/表达式以及包 build tasks/analyzers/generators 不在可安全复制的首期 isolated 范围，准确拒绝或无法绑定当前源，不自动联网、不改原 checkout。Razor/JSON/lock 输入参加源快照。该范围限制不等于普通 SDK 类型分析不支持，existing 仍可检查给定 DLL 并明确绑定程度。

归档：[checks](w05/checks.json)、[host-final](w05/host-final.json)、[build](w05/build.stdout.log)、[test](w05/test.stdout.log)。[ArchUnitNET 官方文件加载接口](https://github.com/TNG/ArchUnitNET/blob/1ab5943d761d48f86b42f45ef047130dc9aff1c6/ArchUnitNET/Loader/ArchLoader.cs) 与缓存禁用选项作为 API 参考；没有复制其源码或 Guard 实现。

W06 将合并项目/程序集逐规则状态，生成 JSON/HTML、CLI analyze/verify/draft、退出码和取消/部分报告。IFX 现场运行仍未执行。
