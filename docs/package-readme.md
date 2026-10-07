# ArchSift 0.3.0

Windows x64 自包含 CLI 和本机 Web UI。项目声明发现不需要 SDK、Guard 或持久 PATH/Profile 修改；isolated build 需要目标的 SDK/targeting packs，仍默认离线。

解压后在 PowerShell 中运行：

```powershell
./archsift.exe --version
./archsift.exe analyze --target D:/your-source --output D:/archsift-reports
./archsift.exe verify --config D:/your-config/archsift.json
./archsift.exe changes --config D:/your-config/archsift.json
./archsift.exe ui --config D:/your-config/archsift.json
```

changes 另需本地 Git；默认 HEAD 对磁盘工作区最终状态，也支持同时指定 --base / --head 本地 commit。不 checkout、不 fetch、不写目标。UI 输出带会话凭据的 loopback 地址，使用该地址打开；结束时在启动终端 Ctrl+C 停止服务。

规则 JSON 是执行依据，八份模板在 templates/rules，包括项目引用与 NuGet 直接包 allowlist。多条 allowlist 独立求值且取交集，deny 不被 allow 覆盖；不完整声明继续产生 limitation。报告可选择 json/html/sarif，JSON 为权威，违规默认 exit 0；配置/执行/无法判定/取消为 2/3/4/130。相关配置和报告均应位于源码根外。

详细说明在 docs/cli.md、docs/changes.md、docs/reports.md、docs/rules.md、docs/build-inputs.md；schema 在 schemas。samples/ci 是需要用户自行 opt in 的报告示例，不启用强制门禁。

LICENSE 是 ArchSift 的 MIT 许可；licenses 保留第三方库和 .NET runtime 的完整许可/notice。package-manifest.json 包含源 commit、版本、RID 和逐文件 SHA-256；manifest 中 candidate 是打包时点状态，最终接受状态见对应 release 的固定 ZIP SHA-256 和发布验收。请校验 release 的 SHA256SUMS.txt。

源码与验收：[von12549/ArchSift](https://github.com/von12549/ArchSift)。Linux CLI 已验证，但本 ZIP 不承诺 Linux Web UI 或 Linux 自包含分发；Linux 使用源码和 .NET 10。
