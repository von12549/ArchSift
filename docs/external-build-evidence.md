# 经审查的外部构建证据（0.7.0）

## 项目分区证据（manifest v3／review v2）

对不同项目实际选择同名不同版本依赖的目标，使用 `evidence capture ... --context-model project` 生成未批准的 review v2 模板。构建快照必须与 capture 的当前源码字节一致；构建与逐项目求值在源根外完成。每项目求值命令须包含 `-t:ResolveReferences -getProperty:MSBuildProjectFullPath,TargetPath,AssemblyName,TargetFramework,Configuration,<capture 中的 propertyNames> -getItem:ReferencePath,ReferenceCopyLocalPaths`，保留实际输出作 evaluation-trace 材料，并审查实际成功构建日志与生成输入。

填写 buildSnapshotRoot，以及每个 projectContexts 的稳定 id、sourceProject、实际 assemblyPath、evaluationPath、**求值输出全部 ReferencePath.FullPath** 和显式 `dotnet-reference-path` 策略。所有发现项目必须恰好有一个 primary owner；引用文件在验证时保持可读取，框架与外部引用须已预捕获，第一方依赖必须匹配本次同一构建的当前 DLL。review.assemblies 只列第一方 owner，global frameworkPaths 可为空（每上下文的实际框架选择已在 referencePaths 中）。未知输入／解析缺项仍不可批准。

`evidence seal` 生成 v3，再 `evidence validate`／ordinary verify／chain verify 复用该证明。上下文内名字／culture／token 必须一致，选中版本必须不低于请求且与实际求值绑定；不会扫描目录猜最高版本。旧 v1／v2 仍严格核对精确闭包。Worker 分别读取每个上下文的元数据，仅统计所属第一方源；不运行程序集或测试入口，不证明任意运行时动态加载行为。报告显示 project-contexts、context count 和外部审查来源；声明模型限制与整体 partial／exit 4 可能仍存在。

需要实现依赖补全时，将全部 `ReferenceCopyLocalPaths.FullPath` 填入可选 `runtimePaths`；不得按目录扫描补猜。每个文件仍须有构建前捕获或本次生成证明。便携编译使用未限定 RID 的实际托管资产；其他 RID 候选、卫星资源及 native/data 文件保留哈希，但不作为中性托管程序集选择，不证明部署 RID 或动态加载行为。非 SDK 编译引用可由兼容的实际 copy-local 实现替代；带 token 的请求要求精确相符，无 token 请求允许已证明的强名称选择。旧 v2 的精确身份规则保持。

静态实现闭包从每个 owner 递归检查。Worker 可合批解析共享简单名所选 DLL 身份和字节完全一致、且 owner 名称互不重复的上下文；结果仍保留每个 owner 的稳定上下文身份，版本冲突的上下文继续隔离。

capture 与旧证据的 4 MiB 界限保持，新的 v3 manifest／review v2 和每份求值 JSON 最多 16 MiB，读取求值 JSON 前先检查大小和材料结构，不能靠删除引用或材料满足大小限制。完整契约和阶段门禁见 `docs/decisions/20261012-0.7-project-context-closure.md`。

## 单闭包证据（manifest v2／review v1）

用于 existing 模式，assembly-manifest v2 标注 `external-reviewed`。ArchSift 验证输入、材料、DLL 和依赖身份；操作员负责依据实际构建记录审查“这些 DLL 由这些输入成功编译”的因果关系。完整 JSON 或一个总哈希不自行证明构建真实性。v1 与 isolated 的保守限制保持兼容。

1. 在目标根外建立取证目录。配置指定 existing、明确 TFM／Configuration。先审查外部快照构建命令、离线 restore 来源、生成器／任务风险和输出路径，不运行目标入口。
2. 准备 `external-input-paths.json` 字符串数组：实际 SDK 工具与导入、框架 DLL、外部导入、生成器／任务及其其他文件输入的绝对路径。目标根内已纳入源码捕获的文件不用重复列为 external input；漏掉的源码根内非标准输入须先纳入可复核输入方案，不应批准为完整。
3. 构建前运行 `archsift evidence capture --config config.json --inputs external-input-paths.json --output <取证目录>/capture.json`。工具计算源码／构建输入身份和外部文件身份，捕获声明所引用属性对应的环境值哈希，不输出原始环境。若 SDK／导入／生成器／任务还读取其他环境键，另用 `--environment-inputs <环境键名 JSON 数组文件>` 捕获并在后续校验其哈希；漏项必须保留 unknown，重新 capture／build。它还生成 `capture.json.review-template.json`，默认为未批准。
4. 在源根外、由捕获内容建立的快照上执行已审查的显式命令。记录成功退出码、SDK／TFM／Configuration、命令参数数组、实际 MSBuild 输入／求值 trace 与日志。取证应覆盖 restore 生成的 assets、包构建逻辑、SDK 导入、自定义输入和全部项目；如果构建后才发现新外部输入，重新 capture／build。构建失败或不完整不得 seal。快照不是安全沙箱。
5. 审查模板填写实际 reviewer、sdk 与 sdkToolPath（SDK 目录中的 MSBuild.dll，`.version` 也须提前捕获）、命令、退出码、每项目已求值程序集／TFM／属性、显式全局属性、有序程序集来源路径、框架路径、imports／generatorsAndTasks。材料角色包含 build-log／evaluation-trace／review-notes，以及 generated-input：按 trace 列出 restore／SDK／生成器产生后被构建读取的 assets、生成源码／props 等文件，记录内容身份及其生成与读取的因果关系；至少提供一个生成输入记录，缺失项保留 unknown。材料身份字段通过 PowerShell `Get-FileHash -Algorithm SHA256`（转小写）和 `Get-Item.Length` 得到；source InputIdentity／BuildInputIdentity 由 capture 自动生成，不手算。`approved`、`inputsComplete` 只有实际核查完成后才置 true；`unknowns` 必须为空，无法追踪的输入保留具体原因并停止。
6. `archsift evidence seal --config config.json --capture <capture.json> --review <已审查 JSON> --output <取证目录>/assembly-manifest.json`。所有 DLL 必须在 manifest 目录或其子目录内，frameworkPaths 是构建前捕获且实际使用的框架 reference DLL，不作为第一方分析程序集加载。netstandard／net8／net10 可以各有精确 PE 身份的框架 reference surface；工具不以较新的同名框架冒充所需版本。非框架依赖仍禁止同名冲突，并逐项核对版本／culture／token。清单采用 create-new，不覆盖已有清单；失败不留下成功清单。
7. 将配置 `build.assemblyManifest` 指向 seal，然后 `archsift evidence validate --config config.json`。绑定失败返回 4，配置错误返回 2，材料读取／执行错误返回 3。随后 ordinary verify、saved-card verify 或 chain verify 均复用相同 Core 校验。报告 buildContext 的 evidenceOrigin／evidenceSha256 与 inputIdentity 记录此次证据来源和身份。

verify 时保留 capture、review、日志、trace、review-notes、外部输入及 DLL，工具会再读取并校验。源码／props、材料、环境输入或 SDK 文件变更都要求重新取证。绑定只支持捕获配置下的编译后类型依赖；声明项目／包规则仍按声明模型显示条件限制，整体报告可为 partial。零匹配、闭包不足和未绑定材料不能 pass；Release 优化的覆盖限制继续显示。

源码捕获同时包含隐式编译配置 `.editorconfig`／`.globalconfig`／`.ruleset`；这些输入修改后，旧证据失效。其他未被捕获的编译输入仍须明确追踪，不能批准为完整。

真实 IFX 按授权分工逐块运行审查过的命令。先用合成项目完成正反例，再核对 105 项目映射和 B01–B05/B07 代表性夹具；尚无真实 IFX 取证结果时不能宣称 406 条候选通过。保留原安装、A 类规则、chain 和历史报告；取证输出统一放在目标源码根外。2026-10-12 用户将机器门禁委托 Agent，最后独立人工 UI 验收仍由用户完成。
