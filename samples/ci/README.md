# 可选 CI 报告

`archsift-report.yml` 仅为 workflow_dispatch 样例。用户自行复制到消费项目 `.github/workflows`，填入已复核发布版本、ZIP SHA-256 与配置路径；ArchSift 不自动修改消费项目工作流、required checks、分支策略或代码扫描权限。

配置应显式包含 `output.formats: ["json", "html", "sarif"]`；源码根/输出由样例覆盖为 checkout/runner temp。项目声明分析不需安装 SDK；isolated build 必须另行配置目标 SDK/pack、离线 feed 或明确的联网来源，不应默认开启。

违规默认 exit 0，不阻断；参数错误、执行错误、无法判定、取消分别保留 2/3/4/130。样例不使用 `continue-on-error` 隐藏工具失败；always 上传报告及 exit-code.txt，检查未配置为 required。SARIF 默认只作 artifact；上传 GitHub code scanning 需要消费项目自行 opt in 和授权，不在样例中开启。

自身 CI 的显式联网 restore 仅作用于一次性 GitHub runner，不改变产品默认离线策略。actions 以复核过的官方 v4 commit SHA 固定。CI 完成与否必须看实际运行证据，文件存在不等于跨平台通过。
