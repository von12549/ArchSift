# W01 依赖与来源记录

日期：2026-10-06（Australia/Sydney）

W01 的实际依赖以八个项目的 `packages.lock.json` 为准，[机器清单](package-inventory.json) 是它们的合并投影，记录 19 个不同的 package/version。Core 与 Contracts 无第三方包；ArchUnit 层独立引用固定的 TngTech.ArchUnitNET。当前版本 0.1.0-dev，工具 TFM net10.0。

## 直接依赖选择

| 依赖 | 固定版本 | 目的 | 许可 |
| --- | --- | --- | --- |
| TngTech.ArchUnitNET | 0.13.4 | 既定程序集分析引擎，W01 只组合引擎身份，真实检查在 W05 | Apache-2.0；[W00 LICENSE/NOTICE](../../migration/third-party-notices.md) |
| Microsoft.NET.Test.Sdk | 17.14.1 | SDK testhost 与 dotnet test 入口；仅测试项目 | MIT；[固定源提交 LICENSE](licenses/vstest-17.14.1-LICENSE.txt) |
| xunit | 2.9.3 | 单元、集成与依赖方向测试；已在本机缓存且可离线复现 | Apache-2.0；[固定 v2 源许可与 MIT 例外](licenses/xunit-2.9.3-LICENSE.txt)、[完整 Apache-2.0 条款](../../migration/licenses/ArchUnitNET-0.13.4-LICENSE.txt) |
| xunit.runner.visualstudio | 3.1.4 | 支持 .NET 8+ 与 xUnit v2 的 VSTest runner；PrivateAssets=all | Apache-2.0；包 nuspec 和版权行在 evidence/清单中保留 |

不用 Guard 的 shell 加载与安装作为入口，不为 W01 添加 Roslyn/schema/coverage 收集器等额外产品依赖。Microsoft.CodeCoverage 是 Test SDK 自带的传递包，本轮没有采集覆盖率。

中央管理的四个直接包使用精确范围 `[version]`；传递解析版本和 NuGet contentHash 固定在 lock 中。测试项目的 runner 为 PrivateAssets=all；全部测试项目不打包，产品项目不引用测试项目。

## 真实解析与身份区别

ArchUnit 项目的实际运行闭包含六个包：TngTech.ArchUnitNET、CycleDetection、JetBrains.Annotations、Mono.Cecil、Newtonsoft.Json 13.0.4、System.ValueTuple。W00 的 System.Collections.Immutable 只是候选，不在当前实际 lock 中。

IntegrationTests 不引用 ArchUnit DLL，TestHost 单独解析 Newtonsoft.Json 13.0.3；另两个测试项目使用 13.0.4。两种版本分别锁定、分别保存 nuspec 和嵌入 LICENSE。不要把独立测试进程的版本差异误写成全 solution 只能有一个包版本。

NuGet lock 的 contentHash 与恢复后 `.nupkg.metadata` 的 contentHash 对应；全 ZIP 字节的 rawNupkgSha512 与 W00 整包 SHA-512 对应，两种定义分开记录。恢复后全部 19 项 lock/metadata 匹配，但这不构成发布者签名验证或漏洞审计。

本机该包的 ArchUnitNET DLL AssemblyVersion 为 `0.13.0.0`、InformationalVersion 为 `1.0.0`。适配层的 package version 由中央版本生成 assembly metadata，明确返回 `0.13.4`；同时保留 LoadedAssemblyVersion。引擎版本信息不从 DLL 的信息版本猜测 NuGet 版本。

## 许可和来源

全部解析包的 nuspec（统一 LF）存于 evidence，原缓存 nuspec 字节哈希、作者、版权、repository URL/commit 在清单中记录。已有运行依赖继续使用 W00 的完整许可/NOTICE；新增测试依赖许可仅影响开发工具链，W08 还需以真正 publish 输出筛选发行组件。

xUnit 的包元数据声明 Apache-2.0；其固定 v2 源提交许可还注明特定导入代码的 MIT 例外，归档保留该文本。xunit.abstractions 2.0.3 只有旧 licenseUrl，没有许可证表达式与源码 commit，保留此限制，不冒称精确源码绑定。Microsoft 测试四包固定源码提交 `490850ae3fdc1b470e3804ceab4f6a41cf89ae51` 的完整 MIT 文本已归档。

参考：[官方 restore 参数和显式 config/source](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-restore)、[NuGet 中央包管理](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)、[xUnit v2 的 VSTest 使用说明](https://xunit.net/docs/getting-started/v2/getting-started)。

## 离线与后续

根 NuGet.Config 清空继承源；开发检查通过显式本地 feed 恢复到仓库的隔离开发缓存。网络源、隐式 fallback 与漏洞 feed 均未启用；NuGetAudit=false 的原因是本轮离线，不代表漏洞检查通过。联网恢复或漏洞审计须作为显式操作另行记录。

W02 选择 schema 校验依赖时补理由、许可与 lock。W05 接入真正 ArchUnitNET 类型依赖正负例；W08 核对实际分发组件和 .NET 自包含运行时 notices。
