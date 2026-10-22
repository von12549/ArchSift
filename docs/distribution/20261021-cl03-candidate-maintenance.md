# 可移植包源维护与保留约定（CL03候选／CL04现役）

初版：2026-10-21；当前状态同步：2026-10-22。现役 `0.10.0-cl03.1`；完整门禁 **493项＝92项C#＋401项JSON**，政策责任来源 **404项＝3项C#＋401项JSON**。B01／B02／B03／B04／B05分别启用133／5／100／63／100条，Registry4已包含在B03中；A179按既定选择暂缓迁入，A三项继续由C#承担，B07五条暂缓并保留原IFX检查。 已验IFX基线为 `b591b9f2ffca5d0bbbfb1f9352a56870fadf927c`，见[最终政策启用记录](https://github.com/von12549/IFX-New/pull/25)及[实际main正常CI](https://github.com/von12549/IFX-New/actions/runs/39733781519)。

历史交付：CL03已完成独立候选打包及合成验包；随后CL04通过[实际main a2adb87验收](https://github.com/von12549/IFX-New/actions/runs/39543191970)并正式采用 `0.10.0-cl03.1`。后续IFX说明经[PR #15](https://github.com/von12549/IFX-New/pull/15)正式交付到main `f58205b`，该提交为当时说明交付基线，见[该main既有验收](https://github.com/von12549/IFX-New/actions/runs/39550973000)。匹配Web ZIP仍是本地已验候选，没有公开发布；本说明记录包源维护和保留边界，不更新包或重跑验收。

## 现役、前一组合及候选材料

| 用途 | 身份与位置 | 责任边界 |
| --- | --- | --- |
| 现役已验组合 | `0.10.0-cl03.1`；`D:\ArchSift-lab\packages\cl04-ifx-offline-20261021-01`，251包 | IFX在历史main `a2adb87` 正式采用，截至本次同步，已验main及本地 `b591b9f` 使用493项门禁及404责任来源；批准精确包引用、完整251包源、锁、可移植入口和三项检查保持。`f58205b`为前期说明交付记录。 |
| 前一已验可移植组合 | `0.10.0-sp03.1`；`D:\ArchSift-lab\packages\sp03-ifx-offline-20261020-02`，原251包 | 作为普通回退完整组合保留；保留全部并行已采纳政策及现行三项检查，实际回退另授权，原验收不改写。 |
| 原候选打包材料 | `0.10.0-cl03.1`；`D:\ArchSift-lab\packages\cl03-20261021-01`，28个nupkg | 四库＋CLI＋23个既有第三方包。CL03的合成验证记录保持；相同五个ArchSift包已由CL04纳入现役251包源，不将28包合成目录当成完整IFX源。 |
| 匹配Web候选 | 同目录`ArchSift.Web.0.10.0-cl03.1.framework-dependent.zip` | 含责任投影读端修正及许可；依赖已准备好的.NET／ASP.NET Core 10共享运行时，不含运行时，也不自动安装。 |

原候选以外置250项源字节快照、构建锁和交付SHA-256绑定，本机CL03验收记录位于 `D:\ArchSift\docs\acceptance\20261021-cl03-candidate-packaging.md`；其中“未采纳”为CL03当时时点，后续正式采纳由CL04建立。ArchSift产品源码43文件随后已由[PR #9](https://github.com/von12549/ArchSift/pull/9)正式合并至main `3f61be0`，根README／报告说明由[PR #10](https://github.com/von12549/ArchSift/pull/10)交付至main `5e6c4cd`，5e6c4cd是当时说明交付基线；截至本次核对本地main为892422e，后续废止与材料登记不改变包身份。原 `044b67a` 及并行材料作为历史保留，不是当前HEAD。源码归档与旧候选包按各自摘要追溯，不声明旧包的精确编译器输入闭包、逐字节可重现构建、冷机器自动准备或新包／Web公开发布。原候选SDK为10.0.303、Windows x64、net10.0、Release。

## 数据源核对与换目录

`package-manifest.json`记录28个原始nupkg的SHA-256及原始文件SHA-512；`source-inputs.json`记录选定源字节。完整交付清单在`D:\ArchSift-lab\runs\cl03-20261021-01\artifact-review-03\delivery-manifest.json`，另覆盖Web ZIP、外置许可证和清单本身。先使用已保管的摘要核对清单，再逐文件验证；摘要清单不能自行给被替换的数据重新授权。NuGet锁定`contentHash`另保留其原语义，不以ZIP摘要替代。

允许将整套候选数据复制到新目录，逐包比较原批准摘要，保留清单和许可，再由合成调用参数指定位置。已实际验证含空格、不同深度的目标和包源位置，均使用独立空缓存；政策及案例身份不包含机器路径或凭据。不复制SDK树，不自动切公网、忽略恢复错误或重新学习必检清单。

CL03演示入口为外置纯合成目标中的`eng/Test-Architecture.ps1`，参数仍是`-PackageSource <已核验候选数据目录> -StateRoot <源码外新状态目录> -RunId <新ID>`，使用`pwsh -NoProfile`。合成夹具包含明确标注的基础设施替身，只证明候选合同和恢复行为，不能替代真实IFX门禁；CL04历史验收为112项，当前已验范围为493项。不要把该夹具或批准映射复制到IFX消费入口；现役已按CL04独立流程核验并采用0.10.0-cl03.1，不通过复制合成示例更新消费。

库恢复链为Testing → ArchUnit → Core → Contracts，内部依赖精确固定候选版本。Testing工具批准闭包10包；CLI解析元数据六包、实际deps运行闭包五包，与现役第三方版本相同。合成消费者实际解析27包，加独立CLI即28包离线数据。测试开发构建另外显式读取本机已有VS runner缓存；该开发依赖没有进入候选包源，两个冷消费验收只使用候选数据目录。

## 许可、报告与更新责任

所有第三方原始nupkg保留原元数据／许可文件；四库、CLI及Web候选保留原`third-party-notices.md`和九份归档许可／NOTICE，逐字节摘要已核对。根`LICENSE`另随数据和Web交付，四库包内原文位于`notices/ArchSift-LICENSE.txt/LICENSE`（NuGet对无扩展名源文件的实际归档路径）；CLI第三方许可在`tools/net10.0/any/notices`。本轮不新授予或改写许可；Guard来源复用仍受[源清单](../migration/source-inventory.json)原范围约束。

报告保持architecture-test-run v1和旧`notIncluded`语义，新责任投影为独立v1扩展。旧报告不补造当前责任或pass，未知投影格式显示unavailable；缺失、skip、失败、取消、未知或重复关联不能取得当前pass。Registry4已正式启用，包含在现役B03全部100条中；“已采纳待启用”为此前阶段状态。CL03合成选择只有两条JSON，合成报告有588项未作为JSON执行；后续CL04实际main验收已核对正式20条选择的568／3／565及四项pending-not-run，责任投影修正在现役包生效。当前401条JSON选择对应187项未作为JSON执行，其中3项由C#承担、179项A及5项B07暂缓；CL04的568／3／565及四项pending-not-run保留为历史结果。本次文档同步不新运行IFX，不将合成或历史结果冒充本次执行。

源包完整性、离线恢复和许可核对通过，不等于新的漏洞审计。离线架构Audit仍为`not-run`；CL04实际main正常push run39543191970／attempt1已完成全量112/112、独立官方105/105/0及CLI六包元数据／五包运行闭包和三项必需检查。后续493项基线的[实际main验收](https://github.com/von12549/IFX-New/actions/runs/39733781519)已完成493/493、独立105/105及三项检查。本次仅引用既有证据；未来变更须按具体授权取得自身所需验收，不能借用旧main成功。任何二进制或批准输入变化都使用新版本和新目录；先验证，再按具体授权更新消费，禁止覆盖现役或原候选同版本字节。无效候选和失败证据也保留。

## 最小保留责任

保管责任人为项目操作者／仓库维护者；本轮仅登记，期限均为待明确处置授权，不自动到期或清理。

| 必须保留 | 直接记录或位置 |
| --- | --- |
| 现役251包、原始来源摘要与许可、完整policy／案例、精确锁及正常三项保护 | [CL04实际main验收](https://github.com/von12549/IFX-New/actions/runs/39543191970)、[当前IFX已验基线说明](https://github.com/von12549/IFX-New/blob/b591b9f2ffca5d0bbbfb1f9352a56870fadf927c/docs/architecture-portable.md)及[后续main验收](https://github.com/von12549/IFX-New/actions/runs/39550973000)；早期CL00／CL01记录按其历史时点保留 |
| 前一已验可移植完整回退组合，以及更早固定源码／props／映射迁移级备用 | [现役说明的回退边界](https://github.com/von12549/IFX-New/blob/b591b9f2ffca5d0bbbfb1f9352a56870fadf927c/docs/architecture-portable.md)及[SP05交付](https://github.com/von12549/IFX-New/pull/12)；两类组合不互相替代 |
| 原本地备份、27项精确生成副本差异接受、历史失败／取消及其他分支材料 | 本机保留 `docs/acceptance/20261020-sp05-local-ifx-update.md` 和 `docs/acceptance/20261021-cl01-local-ifx-update.md` 及各自外置备份；这些历史记录未随本说明交付 |
| 候选28包、Web ZIP、250项源快照、构建／消费锁、清单、脚本和完整成功／失败证据 | `D:\ArchSift-lab\packages\cl03-20261021-01`、`D:\ArchSift-lab\runs\cl03-20261021-01`、`D:\ArchSift-lab\fixtures\cl03-20261021-01` |

IFX校正文档已由PR #15正式交付；本说明与可移植接入README的交付范围以对应PR变更清单为准，其余记录和旧模板继续作为本地材料保留。未进行回退、删除、移动旧材料、缓存清理、环境修复或InProject操作。后续清理须先列精确对象再获授权，不作为收尾通过前置。

当前保留状态另见[退役后材料登记](20261022-materials-retention.md)：原InProject已停止推进，归档及195项待复核材料继续保留，无自动到期。外层独立0.8.0安装和规则库也保留，不与现役包源或493项门禁混用。本次只更新说明，不执行包源迁移、规则采纳或材料处置。
