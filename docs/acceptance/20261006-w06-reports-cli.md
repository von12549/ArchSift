# W06 报告与 CLI 验收

日期：2026-10-06（Australia/Sydney）

结论：W06 COMPLETE。Core 共享服务合并项目/程序集结果，生成 schema 校验的 JSON 与转义 HTML；CLI analyze/verify/draft、参数覆盖、有效配置显示、退出码、取消和部分结果已实现。

最终运行 `b68f322bcef54c9a828bbba34c443bb3`：SDK 10.0.303、net10.0、Debug；offline locked restore/build 成功，零 warning/error；70 unit + 17 integration + 2 architecture = 89/89 通过；每步与最终环境/Profile 哈希不变。

新增验证覆盖无规则 analyze、违规默认 0、零匹配 4/disabled 不适用、CLI/服务稳定 finding parity、未知参数 2、缺失文件执行错误 3、HTML XSS 转义、当前运行 ID 独立、预取消 130、Worker 失败保留已完成项目结果，以及真实 CLI JSON/HTML/draft 与显式覆盖。旧入口测试曾把 verify 当成未实现命令，已调整为真正未知命令；help 仍保留 version/help。

归档：[checks](w06/checks.json)、[host-final](w06/host-final.json)、[build](w06/build.stdout.log)、[test](w06/test.stdout.log)。公共字段和限制见 [reports](../reports.md)、[cli](../cli.md)。没有启动真实 IFX/Guard 集成。W07 将使用同一 Core 服务与序列化器实现 loopback UI 和当前/历史结果区分。
