# CLI 与配置

已实现 analyze/verify、rules validate/render/draft，以及 --help/--version。示例以本机已解压/构建的 archsift 为入口；真实现场 IFX 命令另按人工流程执行。

```text
archsift analyze --config config.json
archsift verify --config config.json
archsift rules validate --file architecture.json
archsift rules render --file architecture.json --output architecture.md
archsift rules draft --config config.json --output D:/archsift-output/drafts
```

analyze 不需要规则，输出声明图范围/限制，compliance=null。verify 要求至少一个规则文件，使用共享服务合并逐规则结果。draft 分离观察事实、disabled 的未接受候选和待决定项，不自动接受现有依赖。

参数 --target/--entry/--rules/--output/--tfm/--configuration 覆盖配置；--rules 可重复并替换配置中的规则路径集合，其他选项不可重复。配置 root、rulesets、output、manifest、cache/feed 及规则编辑目录相对配置文件目录；target.entry 为 target.root 内入口，CLI --entry 相对 invocation cwd 后转换为根内路径。有效位置、TFM/configuration、build.mode 与网络选项显示在 stderr。stdout 是权威 JSON（规则管理命令为确认/导出路径）。

输出按独立 runId 子目录保存 report.json/report.html，不写入目标；目标/输出相互包含和链接路径拒绝。Markdown 只新建，不覆盖已有文件/JSON。默认 existing 不 restore/build，未绑定 DLL 只作为 assemblies-only；isolated 需显式配置，默认 Debug/offline，支持范围见 build-inputs 和 W05 验收。

退出：0 为已完成且检查可判定（可有违规）；2 配置错误；3 构建/Worker/报告等执行失败；4 无法判定/来源变化/部分覆盖；130 用户取消。Ctrl+C 取消当前进程树/运行，已完成项目结果可保留，取消不能借旧成功。
