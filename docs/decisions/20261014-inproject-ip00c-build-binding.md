# ArchSift.InProject IP00-C：构建支持矩阵与日常源码关联契约

日期：2026-10-14（Australia/Sydney）。依据：[产品规格](20261014-archsift-inproject-0.1.0-product-spec.md)、[路径与输入身份](20261014-inproject-ip00b-config-paths.md)和[实施计划](../plans/20261014-archsift-inproject-0.1.0-implementation-plan.md)。本记录冻结 IP02 的实现门槛，不声称现有 `IsolatedBuild`、旧证据或示例已证明 InProject 源码关联。

## 支持范围与构建前审查

一个运行绑定一个明确选定的 SDK-style C# `.sln`／`.slnx`、worktree、Configuration、产品／规则／工具身份与新的 run-id。项目目标仅支持 net8.0、net9.0、net10.0；主机工具及专用 xUnit 项目使用 net10.0。solution 的配置映射、逐项目目标框架和项目引用闭包必须由实际 MSBuild 求值确定，并与 `project.json` 的显式选择一致。混合 TFM 合法：分别构建每个 `(项目, TFM, Configuration, 全局属性)` 上下文；同名不同版本的程序集只能凭上下文内实际解析路径、程序集身份和原始字节哈希区分，不合并成一个按文件名索引的 DLL 集合。多 TFM 项目如未显式选择必检上下文，拒绝猜测。

| 输入或行为 | IP02 支持判据 | 无法证明时的结果 |
| --- | --- | --- |
| 普通 C#、ProjectReference、PackageReference、条件属性／Item | 每个实际构建上下文的求值结果、条件所依赖的属性／文件、引用闭包及编译调用均可取证 | 必检上下文阻断 |
| props／targets、SDK、中央包版本、NuGet 配置和锁 | 记录实际 import／SDK／还原来源与选中版本；快照前后上下文等价，离线来源明确且 restore 结果成功 | 阻断；不隐式联网或忽略还原失败 |
| source generator、AdditionalFiles、Analyzer、生成源码、响应文件 | 扩展实际 `csc` 响应文件，记录传入输入和参数、生成后的源码／资源／分析器及字节摘要 | 未观察到必要输入、无法扩展响应文件或生成器输出时阻断 |
| 自定义 MSBuild task／target、动态文件和环境读取 | 运行前人工可审查执行方案；必要输入、输出、版本和环境影响均可枚举并绑定，且不得启动目标应用 | 未知副作用或必要输入则拒绝执行／阻断；快照不等于沙箱 |
| 外部文件、符号链接／重解析点、祖先 import | 实际路径归属、内容及快照重定位后求值可核对；外置输入只读且明确允许 | 越界、未知祖先输入或不能安全重定位时阻断，不退回原地构建 |
| 失败、取消、运行中变化 | 保留诊断、上下文及状态；全部所需输出和前后材料一致才可验证 | 不输出 `SourceBound=true`，旧成功仅留历史 |

首次实现可以拒绝某些自定义 task／生成器，不能把它们伪装成受支持的项目。`ReferencePath` 和 `ReferenceCopyLocalPaths` 按每个上下文分别捕获；同名程序集若解析到不同字节，不得被覆盖或跨上下文错用。未出现在业务发现中的工具目录若被项目显式引用，按 IP00-B 的范围冲突处理。

## 每次运行的顺序与记录

1. 在任何目标写入前确认 Git worktree 身份、唯一 solution、规则／工具字节、SDK 与 targeting pack、项目构建方案及精确 owned 路径。保存原始业务输入清单和哈希；对条件求值读取的外部文件、祖先 `Directory.Build.*`／`Directory.Packages.props`／`global.json`／`NuGet.Config` 和显式 import 单独列项。必要环境记录为稳定键、是否设置和不可逆摘要；不能记录原始敏感值。未识别但会影响必检上下文的环境输入阻断。
2. 在 `.archsift/state/build/<run-id>/` 创建全新的快照。排除 `.archsift/` 与运行输出，禁止递归复制和业务 DLL 跨次复用。比较原根和快照根中实际生效的 import、SDK、配置与 project graph；不同则终止。现有 `IsolatedBuild` 强制单一 TFM、按 DLL 名合并输出，不能原样作为混合 TFM 的执行器。
3. 只从明确的本地离线源进行 restore；可选网络须使用配置中显式来源并在证据中标注。`--no-restore` 的后续构建必须绑定这次成功还原的 assets／lock，而不是历史缓存存在。逐上下文采集实际编译命令、展开后的响应文件、编译器与任务版本、源码、资源、AdditionalFiles、分析器／生成器、引用路径与身份、实际输出 PE／PDB 哈希。无法确定生成输入与编译调用的对应关系时终止。
4. 构建后重读原根业务输入与规则／工具输入，并核对快照与还原／编译期间新增材料、实际输出及归属。任何必要输入在取证前后改变、缺失或未列入清单，标为不稳定或未知；不以时间戳或 Git commit 推断字节相等。失败／取消也有独立 run-id、状态与诊断，绝不借用历史输出。
5. 由独立于薄 xUnit 桥接层的关联验证器读取原始捕获、实际编译／还原记录及输出，检查每个必检上下文的闭包和材料摘要，才给出 `SourceBound=true`。桥接层只传 run-id 和已验证的引用，不自行写入或覆盖该布尔值。验证器记录检查范围和版本；任一必检上下文无有效绑定时门禁阻断。

日常绑定记录的最小字段见 [`daily-binding.schema.json`](../../schemas/inproject/daily-binding.schema.json) 与[结构样例](../contracts/inproject/examples/daily-binding.example.json)。schema 只限制形状、稳定英文键和基本枚举，不能证明文件存在、构建真正执行、闭包完整或哈希正确；示例摘要是占位值，不能进入真实门禁。记录只表示**待验证材料**，故没有可由生产者填写的 `sourceBound` 字段。验证器的决定另存独立记录，必须包含记录自身 SHA-256、验证器版本、检查结果及影响范围。

## 必检关联判定

| 场景 | 记录与门禁预期 |
| --- | --- |
| net8/net9/net10 混合 solution、两个上下文分别解析同名不同版本引用 | 各上下文独立编译、独立引用身份／哈希及输出；全部闭包有效方可 `SourceBound=true` |
| 错 Configuration、TFM、SDK、solution 配置映射或项目引用版本 | 记录为不匹配；即便有旧 DLL，也不关联本次输入 |
| 缺失源码、生成文件、import、任务／分析器、必要环境摘要或响应文件展开 | 必检范围为 unknown；阻断，不把普通 partial 自动降级通过 |
| 原根在快照前后变化，快照导入祖先输入不同 | unstable／relocation mismatch；阻断，保留诊断 |
| restore／build 失败、取消、零项目或零实际编译 | failed／cancelled／inconclusive；不可声明当前成功 |
| 仅有 Git commit、文件时间戳、缓存或原有外部完整证据封印 | 均不足以证明这次运行的编译输入关联 |

机器状态与规则状态分开：构建失败是运行 `error`，取消是 `cancelled`，未知必检输入产生 `inconclusive`，真实规则违规仍是 `violation`；统一门禁与退出码由 IP00-D 冻结。完整证据比日常摘要保留更多原始可复核材料，由 IP00-E／IP05 实现；两级都不得跳过本次绑定验证。

## 本阶段检查及后续门槛

本阶段只冻结矩阵和可执行的记录形状。IP02 必须用独立合成 fixture 重建混合 TFM、同名异版本依赖、生成输入、响应文件、祖先 import 与失败／取消正反例，并记录实际 SDK／TFM／Configuration、编译取证和宿主哈希；IP03 测试桥接不能代替关联验证器。适用的旧 ArchSift 回归保留原语义。尚未运行这些新产品构建，不把 IP00-C 的 schema 校验当作源码绑定证明。

2026-10-14 以 PowerShell `Test-Json` 检查结构样例 1/1 有效；将 TFM 改为 net7.0、由生产者添加 `sourceBound`、删除所有材料的三个反例均被拒绝，3/3。此检查仅覆盖记录形状；IP02 须验证实际字节、完整闭包和构建前后稳定性。
