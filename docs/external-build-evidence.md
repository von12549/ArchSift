# 经审查的外部构建证据（0.7.0）

用于 existing 模式，assembly-manifest v2 标注 `external-reviewed`。ArchSift 验证输入、材料、DLL 和依赖身份；操作员负责依据实际构建记录审查“这些 DLL 由这些输入成功编译”的因果关系。完整 JSON 或一个总哈希不自行证明构建真实性。v1 与 isolated 的保守限制保持兼容。

1. 在目标根外建立取证目录。配置指定 existing、明确 TFM／Configuration。先审查外部快照构建命令、离线 restore 来源、生成器／任务风险和输出路径，不运行目标入口。
2. 准备 `external-input-paths.json` 字符串数组：实际 SDK 工具与导入、框架 DLL、外部导入、生成器／任务及其其他文件输入的绝对路径。目标根内已纳入源码捕获的文件不用重复列为 external input；漏掉的源码根内非标准输入须先纳入可复核输入方案，不应批准为完整。
3. 构建前运行 `archsift evidence capture --config config.json --inputs external-input-paths.json --output <取证目录>/capture.json`。工具计算源码／构建输入身份和外部文件身份，捕获声明所引用属性对应的环境值哈希，不输出原始环境。它还生成 `capture.json.review-template.json`，默认为未批准。
4. 在源根外、由捕获内容建立的快照上执行已审查的显式命令。记录成功退出码、SDK／TFM／Configuration、命令参数数组、实际 MSBuild 输入／求值 trace 与日志。取证应覆盖 restore 生成的 assets、包构建逻辑、SDK 导入、自定义输入和全部项目；如果构建后才发现新外部输入，重新 capture／build。构建失败或不完整不得 seal。快照不是安全沙箱。
5. 审查模板填写实际 reviewer、sdk 与 sdkToolPath、命令、退出码、每项目已求值程序集／TFM／属性、显式全局属性、有序程序集来源路径、框架路径、imports／generatorsAndTasks，以及三种材料角色 build-log／evaluation-trace／review-notes。材料身份字段通过 PowerShell `Get-FileHash -Algorithm SHA256`（转小写）和 `Get-Item.Length` 得到；source InputIdentity／BuildInputIdentity 由 capture 自动生成，不手算。`approved`、`inputsComplete` 只有实际核查完成后才置 true；`unknowns` 必须为空，无法追踪的输入保留具体原因并停止。
6. `archsift evidence seal --config config.json --capture <capture.json> --review <已审查 JSON> --output <取证目录>/assembly-manifest.json`。所有 DLL 必须在 manifest 目录或其子目录内，frameworkPaths 是构建前捕获的框架 DLL，不作为第一方分析程序集加载。清单采用 create-new，不覆盖已有清单；失败不留下成功清单。相同名字而版本／culture／token 不同的依赖拒绝绑定。
7. 将配置 `build.assemblyManifest` 指向 seal，然后 `archsift evidence validate --config config.json`。绑定失败返回 4，配置错误返回 2，材料读取／执行错误返回 3。随后 ordinary verify、saved-card verify 或 chain verify 均复用相同 Core 校验。报告 buildContext 的 evidenceOrigin／evidenceSha256 与 inputIdentity 记录此次证据来源和身份。

verify 时保留 capture、review、日志、trace、review-notes、外部输入及 DLL，工具会再读取并校验。源码／props、材料、环境输入或 SDK 文件变更都要求重新取证。绑定只支持捕获配置下的编译后类型依赖；声明项目／包规则仍按声明模型显示条件限制，整体报告可为 partial。零匹配、闭包不足和未绑定材料不能 pass；Release 优化的覆盖限制继续显示。

真实 IFX 由操作员逐块运行审查过的命令。先用合成项目完成正反例，再核对 105 项目映射和 B01–B05/B07 代表性夹具；尚无真实 IFX 取证结果时不能宣称 406 条候选通过。保留原安装、A 类规则、chain 和历史报告；取证输出统一放在目标源码根外。
