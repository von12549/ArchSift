# W02 schema 校验选择

日期：2026-10-06（Australia/Sydney）

采用 .NET 内置 System.Text.Json 读取契约，固定内嵌四份 draft-07 schema，并实现仅覆盖本产品 schema 所用关键字的校验器；不引入通用 schema 外部包。这样 Core/Contracts 继续没有外部运行依赖，离线 lock 不变。

规则加载先运行 schema 校验，再独立运行范围/参数/重复 ID/例外/策略冲突语义校验。拒绝重复 JSON key，最多 4 MiB 与深度 64；$ref 只允许当前文档内部指针，无远程 schema 执行面。未知对象字段为 configuration-error。JSON 解析后的整数接受数学整数表示，DTO 对 int 额外检查范围；输入原始字节单独作哈希，不用重新序列化值代替来源身份。

这是固定配置契约的校验，不提供任意用户 schema 或完整 draft-07 校验库。PowerShell Test-Json 独立验证六个模板，契约正负例和 JSON round-trip 在 .NET 测试验证；如未来固定 schema 需要额外关键字，应增加实现与独立正负测试，或通过记录明确改用完整校验库。

此次没有新增 NuGet 包或第三方源码；既有依赖 lock 与许可记录仍有效。
