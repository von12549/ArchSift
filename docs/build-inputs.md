# 声明发现、输入身份与只读边界

W03 使用 XML/solution 声明模型，不执行 MSBuild，不启动目标程序。支持 SDK-style C# csproj、sln、slnx；范围由 TargetRoot 决定，solution 成员与同范围未列入项目分开报告，外部引用不递归扫描，未解析引用不构造虚假的内部项目。

入口 entry 为 TargetRoot 内相对 solution/project，必须通过根边界与 reparse-point 检查；输出路径不得与 TargetRoot 相互包含。项目 ID 为相对 csproj 路径，大小写碰撞报错。solution 只提供入口/成员关系，根内项目图仍清点全部支持项目。没有 solution 的 project 入口不宣称完成 solution-membership 检查。

TFM 采用项目 unconditional literal 声明，或根内最近的 Directory.Build.props literal；条件、表达式、导入、歧义、缺失与不支持 TFM 均记录限制。多 TFM 需显式选择，保留候选列表，不从一端结果外推。NuGet 检查读取直接 PackageReference ID 和可解析的最近 Directory.Packages.props 包版本；ID 大小写不敏感，导入或 conditional version 不做求值。

统一 InputCapture 记录相对路径、长度及 SHA-256，稳定排序后形成源身份。普通 .git/.vs/.idea/bin/obj/artifacts/.archsift 不参加默认范围；项目显式使用的 Compile/Content/None/EmbeddedResource/AdditionalFiles/Analyzer 输入另外清点，因此显式生成源码或资源不会被普通 bin/obj 排除隐藏。根外、条件、表达式及缺失输入作为限制，不能声称可完整复现。

运行开始/结束可重复 Capture 并 VerifyUnchanged，相关源码新增或保存使身份失效，返回 source-changed-during-analysis；IDE 或普通生成目录变化不影响无关身份。所有路径均检查链接/重解析点，XML 禁用 DTD 与外部 entity resolver。扫描上限为 100000 个文件，超限准确报错，不 silently truncate。

W05 已另记录构建快照输入、实际 SDK/TFM/Configuration、程序集 SHA-256 与生成方式，Worker 验证 assembly 版本/token 闭包并区分 assemblies-only 与 source-bound。Razor/JSON/lock 也参加保守快照；未求值的构建条件/表达式不会被标为当前源已绑定。标准 SDK isolated 模式不支持未审查任务/导入/自定义输出/包构建脚本或生成器；完整 MSBuild 输入访问轨迹仍 defer。W06 将加入规则/引擎/上下文的完整运行身份和报告。单独源身份仍不能证明任意现有 DLL 对应当前源码。
