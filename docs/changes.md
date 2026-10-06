# 变更比较

`archsift changes --config config.json` 默认比较本地 HEAD 的完整内容与磁盘工作区最终状态。暂存、未暂存及按共享输入选择规则选中的未跟踪文件一起进入目标；不是只检查 staged，也不是逐 commit 验证。

显式比较：`archsift changes --config config.json --base HEAD~1 --head HEAD`。两端必须同时提供且能解析为本地 commit；不 fetch、不猜 upstream、不 checkout、不修改目标或索引。TargetRoot 可以是仓库内子目录。UI 的“变更比较”使用同一个 Core 服务。

## 输入与规则

快照和报告位于外置 output 下的独立 run ID 目录。历史内容直接从 Git blobs 读取，不受 export-ignore、export-subst、checkout filters 或 hooks 改写。Git 链接、submodule、平台路径碰撞和越界路径保守拒绝；限制 100000 文件、128MiB/文件、512MiB/tree。工作区输入在复制后和比较结束时再次校验，默认 HEAD 也再次检查。

根内规则各自来自对应版本；根外规则冻结一次并供两端使用，结束时校验原文件。修改、删除、禁用规则或改变例外均记录为 policyChanges，不自动成为问题解决。报告列出两端实际输入及 Git 已跟踪/非忽略未跟踪清单中排除的文件；忽略但符合共享输入选择器的文件仍按 InputCapture 纳入。IDE/VCS 缓存与普通 bin/obj 排除，显式引用资源按共享选择器保留。

## 比较与退出状态

先全量分析两端，再按稳定 finding ID 匹配。只有规则/例外指纹、构建/引擎上下文一致且该规则两端可判定，才归入 added、existing、resolved。不可比较证据单列 unclassified；重命名只在唯一相同内容哈希时作为文件变化展示，不推造源码映射。缺失基线或基线构建/分析失败保留目标报告，不推断新增。

comparison.json 是权威；comparison.html 是转义后的投影，嵌入两端完整报告。status 为 completed / inconclusive / cancelled。可判定的规则违规仍 exit 0；配置错误 2、执行/基线错误 3、比较或覆盖无法判定 4、取消 130。部分规则仍可比较时保留其结果，但整体不能显示完整成功。JSON schemaVersion 仍为 1，新增独立 comparison schema，不替代 analysis report。

已有 DLL 仍必须满足原有来源绑定约束，不能把同一外部旧 DLL 的存在当成两个版本的源码证据。isolated build 仍是显式选择、默认离线、受原安全限制约束，不是完整沙箱。
