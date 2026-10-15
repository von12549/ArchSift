# IFX 架构测试接入样例（LT02A）

此目录保存 LT02A 在独立 IFX 副本中实际构建、运行的新增测试代码和机制验证规则。`lt02a-mechanism.json` 只验证 JSON → ArchUnitNET → xUnit → MTP/CTRF → 门禁的链路；它不是已批准的 IFX B 类政策。

被验证的 IFX 源提交为 `cc06528ab17052baafa496df2a89f6cde9207384`。将两个文件复制到该提交的独立副本 `tests/Architecture/IFX.Architecture.Tests/`，并在原测试项目末尾增加指向本仓库 `src/ArchSift.Testing/ArchSift.Testing.csproj` 的 `ProjectReference`。保留原来的全部 16 个 `ProjectReference`、三个原测试类、Theory 数据及五秒断言。IFX 副本根目录的 `NuGet.config` 清空隐式包源；restore 命令显式指定获准的本机离线源。不要在原 IFX checkout 或安装上应用此样例。

门禁配置须设置 `runnerFormat: "mtp-ctrf"`，`testProject` 指向原 `IFX.Architecture.Tests.csproj`，`originalBaseline` 指向 [`docs/decisions/20261015-lightweight-ifx-original-case-ids.json`](../../docs/decisions/20261015-lightweight-ifx-original-case-ids.json)，`originalBaselineSha256` 使用该文件的实际 SHA-256。`rulesets` 指向副本内的规则文件；运行目录和包缓存放在源根外。协调器对子进程强制 `IFX_UPDATE_SNAPSHOTS=0`。正式 B 类规则需另行审查和启用，不由此样例自动采用。

实际机器结果和输入身份见 [`LT02A 验收记录`](../../docs/acceptance/20261015-lightweight-lt02a-ifx-gate.md)。
