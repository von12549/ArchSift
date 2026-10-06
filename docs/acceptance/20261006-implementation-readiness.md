# ArchSift 计划交付与实施条件检查

日期：2026-10-06（Australia/Sydney）

结论：READY FOR W00。仓库、项目定位、工具链和参考资料满足启动实施前核对的条件；Guard 源码迁移仍须完成来源及许可证检查。此结论不代表 W00 已完成，也不代表构建、全部离线依赖或产品验收通过。

## 交付对象

Codex Local Project：ArchSift。

Project ID：51f5ef48-d816-423e-a544-3a28f7580655。

工作目录：`D:\ArchSift`。本次列表中没有归属该 Project 的现有对话，采用项目文件交付，不创建新的 Codex 对话。

| 文件 | 目标路径 |
| --- | --- |
| 正式实施计划 | `D:\ArchSift\docs\plans\20261006-archsift-implementation-plan.md` |
| Draft 历史 | `D:\ArchSift\docs\plans\20261006-dotnet-architecture-analyzer-draft.md` |
| 本检查报告 | `D:\ArchSift\docs\acceptance\20261006-implementation-readiness.md` |

交付通过复制源文档、比较源/目标 SHA-256 完成；已存在且内容不同的目标不得覆盖。跨项目 P05 证据链接采用绝对路径，正式计划与 Draft 之间的相对链接在目标目录内仍可解析。后续实施计划变更在 ArchSift 目录维护。

## 已核对的实施条件

| 条件 | 结果 | 意义与限制 |
| --- | --- | --- |
| 项目目录映射 | Local Project ArchSift 指向 D:\ArchSift，isGitRepository=true | 产品中的 Project 与用户指定目录一致 |
| Git 仓库 | main 跟踪 origin/main；HEAD 为 a5bc15cca1211b5098edc762440c595c996064f8 | 仓库有初始提交，无须重新 git init |
| 工作区 | 交付前 git status --porcelain 为空；根目录仅 .git 与 README.md | 可保留初始文件并导入文档；交付后文档将是预期未跟踪改动 |
| 远程 | origin 为 https://github.com/von12549/ArchSift.git | 已配置真实远程 URL；本次未验证远程连通、认证或推送权限 |
| 适用指令 | 未发现 D:\AGENTS.md、D:\ArchSift\AGENTS.md、D:\ArchSift\CLAUDE.md | W01 应写入已定案的项目说明与安全原则；当前不缺失新项目既有指令 |
| 父目录构建配置 | 未发现 D:\global.json、D:\NuGet.Config、D:\Directory.Build.props、D:\Directory.Build.targets、D:\Directory.Packages.props | 本次没有发现这些指定父级文件的隐式继承；用户级 NuGet 配置由 W01 明确管理 |
| PowerShell | 7.6.6 | 足以支撑计划中的 pwsh 验证入口 |
| Git | 2.49.0.windows.1 | 可开展本地版本及两端输入处理 |
| .NET SDK | 安装有 10.0.301、10.0.303，另有 9.0.314、3.1.426 | 具备 .NET 10 工具开发环境；新 global.json 的实际解析版本在 W01 再记录 |
| .NET runtime | NETCore/ASP.NET Core 10.0.9、10.0.11，另有 8/9 runtime | 满足本机 .NET 10/Web 运行条件；尚未启动新工具 |
| Reference packs | NETCore.App.Ref 8.0.27/28、9.0.16/17、10.0.9/11 在磁盘存在 | 具备目标 net8/9/10 的基础 targeting packs；特定项目仍可能缺其他包或 workload |
| 计划与资料 | 正式计划、Guard 3 个主要迁移源、IFX.sln、P05 最终验证可读 | 可在 W00 进行来源清单和迁移分析 |
| 文件访问 | 常规跨目录 Git 读取在本对话沙箱受限，经批准的只读命令在 D:\ArchSift 成功 | 证明检查可以完成；不将本对话权限等同目标 Project 日后所有操作权限 |

## ArchUnitNET 缓存检查

| Package | 版本 | 缓存存在 | nupkg.sha512 元数据与 Guard lock 一致 |
| --- | --- | --- | --- |
| TngTech.ArchUnitNET | 0.13.4 | 是 | 是 |
| CycleDetection | 2.0.0 | 是 | 是 |
| JetBrains.Annotations | 2026.2.0 | 是 | 是 |
| Mono.Cecil | 0.11.6 | 是 | 是 |
| Newtonsoft.Json | 13.0.4 | 是 | 是 |

核对对象为缓存记录与现有锁文件。未重新下载包，未校验缓存中每个 DLL 的完整字节，未 restore 新项目。新项目 schema 校验、测试框架等后续选用依赖的缓存与离线闭包仍由 W01 固定和验证。

## 诊断与安全结果

工具链诊断于 2026-10-06T10:18:51.4119983Z 汇总，实际 cwd 为 D:\ArchSift。仅调用 dotnet --list-sdks、dotnet --list-runtimes，两项 exit 0；每项结束后及最终分别比较 User PATH、Machine PATH、Process PATH 和四个 PowerShell Profile 的哈希/存在性，全部一致。

比较结果不包含 PATH/Profile 原文；没有 Guard 调用、安装、restore、build、测试执行或持久环境写入。此次检查只证明被检查的 PATH 与 Profile 状态未变，未对所有 User/Machine 环境项进行全量比较。

上一条包含完整环境数据处理的诊断命令被自动审批拒绝，拒绝理由为潜在环境秘密泄露；该命令未执行。已使用仅输出哈希和 equality 的安全版本完成工具链检查，没有绕过审批或继续执行被拒绝命令。

## W00 与 W01 中仍需完成的工作

1. 在目标 Project 读取导入的正式计划与本报告，记录现有 HEAD、README 和之后的文档改动；保留用户文件，不重新创建仓库。
2. 固定 Guard 迁移来源、文件哈希和权属记录。根目录未发现 LICENSE/COPYING/NOTICE；迁移具体文件前核对来源权利及第三方许可证，不能由当前缓存存在性推断许可检查完成。
3. 写入目标项目的 AGENTS.md、安全回归及真实集成操作规则。涉及 IFX/Guard 现场测试继续一次一块已审查命令，收到人工结果后继续。
4. 创建 .NET 10 基础结构、独立 global.json、显式 NuGet sources/lock 和依赖清单；执行首次 restore/build/fixtures 前按计划完成相应基线。
5. 确认目标 Project 对代码、外置 D:\ArchSift-lab 及必要参考路径的权限。实际拒绝时通过正常审批处理，不把已完成的诊断视为任意后续写入授权。
6. 新项目配置冻结后验证实际 SDK 解析、完整依赖恢复和代码构建；远程认证/发布能力等在需要该操作时检查。

产品问题 Q01–Q14 均已关闭；以上是正式计划本来要求的实施任务。可以从 W00 开始，不应直接宣称已通过 W08 或发布条件。

## 参考

- [目标正式计划](D:/ArchSift/docs/plans/20261006-archsift-implementation-plan.md)。
- [目标 Draft 历史](D:/ArchSift/docs/plans/20261006-dotnet-architecture-analyzer-draft.md)。
- [源计划基准](D:/IFX-10-Root/docs/plans/20261006-archsift-implementation-plan.md)。
