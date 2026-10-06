# ArchSift W00 第三方许可与来源记录

日期：2026-10-06（Australia/Sydney）

状态：W00 候选依赖的许可清单、文本与来源归档完成。实际 NuGet 解析闭包在 W01 的 lock 文件中冻结，ZIP 中的实际运行时组件及分发清单在 W08 验收。当前未生成产品包。

## 归档清单

| 组件 | 版本基准 | 许可 | 本地归档 | 权威来源与归属 |
| --- | --- | --- | --- | --- |
| TngTech.ArchUnitNET | 0.13.4 | Apache-2.0 | [LICENSE](licenses/ArchUnitNET-0.13.4-LICENSE.txt)、[NOTICE](licenses/ArchUnitNET-0.13.4-NOTICE.txt) | [包源提交 LICENSE](https://github.com/TNG/ArchUnitNET/blob/1ab5943d761d48f86b42f45ef047130dc9aff1c6/LICENSE)、[NOTICE](https://github.com/TNG/ArchUnitNET/blob/1ab5943d761d48f86b42f45ef047130dc9aff1c6/NOTICE)；Florian Gather、Paula Ruiz、Fritz Brandhuber、Pavel Fischer；TNG Technology Consulting GmbH |
| CycleDetection | 2.0.0 | MIT（包发布者表达式） | [MIT 条款](licenses/CycleDetection-2.0.0-MIT.txt)、[nuspec](evidence/cycledetection-2.0.0.nuspec)、[NuGet MIT 参考](evidence/nuget-MIT-license-reference.html) | Daniel Bradley、Robert Giesecke；[官方来源 README](https://github.com/RobertGiesecke/CycleDetection/blob/4f6a70768ec1354efb08841f8cc0839b8e1b0586/README.markdown)；实际包以 nuspec 声明 MIT |
| JetBrains.Annotations | 2026.2.0 | MIT | [LICENSE](licenses/JetBrains.Annotations-2026.2.0-LICENSE.txt)、[nuspec](evidence/jetbrains.annotations-2026.2.0.nuspec) | [官方固定提交许可](https://github.com/JetBrains/JetBrains.Annotations/blob/51456a32946d20b160c91b384e08ba6d4a5731e4/license.md)；许可文本 Copyright (c) 2016–2024 JetBrains s.r.o.；包元数据 Copyright (c) 2016–2025 JetBrains s.r.o.，两项均保留 |
| Mono.Cecil | 0.11.6 | MIT | [LICENSE](licenses/Mono.Cecil-0.11.6-LICENSE.txt) | [0.11.6 tag 的 LICENSE](https://github.com/jbevain/cecil/blob/0.11.6/LICENSE.txt)；Copyright (c) 2008–2015 Jb Evain；Copyright (c) 2008–2011 Novell, Inc. |
| Newtonsoft.Json | 13.0.4 | MIT | [LICENSE](licenses/Newtonsoft.Json-13.0.4-LICENSE.txt)、[nuspec](evidence/newtonsoft.json-13.0.4.nuspec) | 实际 nupkg 内 LICENSE.md；包源提交 `4e13299d4b0ec96bd4df9954ef646bd2d1b5bf2a`；Copyright (c) 2007 James Newton-King（许可文本），Copyright © James Newton-King 2008（包元数据），保持原文 |
| System.ValueTuple | 4.6.2，补充传递候选 | MIT | [LICENSE](licenses/System.ValueTuple-4.6.2-LICENSE.txt)、[nuspec](evidence/system.valuetuple-4.6.2.nuspec) | [包源提交 LICENSE](https://github.com/dotnet/maintenance-packages/blob/84b081710c66cc560b77d4de38cee493c3a38e9d/LICENSE)；.NET Foundation and Contributors；包版权 Microsoft Corporation |
| System.Collections.Immutable | 1.5.0，补充传递候选下界 | MIT | [LICENSE](licenses/System.Collections.Immutable-1.5.0-LICENSE.txt)、[THIRD-PARTY-NOTICES](licenses/System.Collections.Immutable-1.5.0-THIRD-PARTY-NOTICES.txt)、[nuspec](evidence/system.collections.immutable-1.5.0.nuspec) | 实际 nupkg 内许可与 notices；[元数据指向的 CoreFX 提交许可](https://github.com/dotnet/corefx/blob/30ab651fcb4354552bd4891619a0bdd81e0ebdbf/LICENSE.TXT)；.NET Foundation and Contributors；包版权 Microsoft Corporation |

远程许可归档为 UTF-8/LF 文本快照，末尾补换行；本地 nuspec 和许可快照也只统一换行，原缓存字节 SHA-256 另在 [机器清单](third-party-inventory.json) 记录。快照文件自身哈希记录在 [W00 文档交付清单](../acceptance/20261006-w00-artifact-manifest.json)。没有把不同换行的快照哈希冒称为上游字节哈希。

## 证据与限制

Guard 的手工加载清单包含前五个包，只是选择的 DLL 闭包。ArchUnitNET 的 netstandard2.0 nuspec 还声明 `System.ValueTuple >=4.6.2`，CycleDetection 的可兼容候选组还声明 `System.Collections.Immutable >=1.5.0`。因此前一轮“完整依赖闭包”措辞应理解为 Guard 的已知手工加载基准，不能推导 ArchSift 的 NuGet restore 完整性。其他框架组中的 NETStandard.Library/System.Runtime 不自动列为 net10 的已解析依赖。

本轮只读核对 7 个本地 nupkg 的实际 SHA-512 与缓存 `.nupkg.sha512`，以及包内 33 个 DLL 与解包缓存文件的 SHA-256，全部一致。前五个包的哈希还与 Guard 固定 lock 一致。补充两包仅证明本地包/元数据一致；未校验发布者签名或重新下载。NuGet 真实选组、版本提升、完整依赖与实际发布资产仍以 W01/W08 结果为准。

CycleDetection 固定来源树没有独立 LICENSE/NOTICE；包发布者 nuspec 明确声明 MIT。归档的 MIT 文件是对应标准条款阅读副本，不假称来自该仓库的原 LICENSE，也没有捏造版权年份。保留 Daniel Bradley 与 Robert Giesecke 署名；官方 README 还记录了早期算法示例来自 Stack Overflow 的来源线索。ArchSift 使用该 NuGet 二进制依赖，不选择复制其算法源码；如后续需要复制其源码，必须另作源片段许可核对。该信息应随 W08 的第三方清单继续保留。

JetBrains 官方仓库许可提交与 2026.2.0 包没有 commit 绑定，已明确记录所取提交和包元数据，不推断它就是包构建提交。许可证和包版权行同时保留。

## 实施与分发处理

W01 固定实际直接/传递包版本、TFM 选组和 lock；新增 schema/test 或其他依赖时扩展本清单。W05 使用 ArchUnitNET API，不复制第三方实现源码。尚未把任何依赖 DLL 放入 ArchSift。

W08 从实际 publish 输出及 lock 生成发行组件清单：保留 MIT 的完整许可及版权行；携带 ArchUnitNET 完整 Apache-2.0 LICENSE 与 NOTICE；保留实际分发 Microsoft 组件的 notices。若只分发部分组件，在清单中注明，不以当前候选清单替代实际包检查。自包含 .NET 运行时的许可及 notices 随实际选定运行时在 W08 收集。

## Guard 资产授权

用户于本聊天明确声明：“Guard项目所有权完全属于我，确认拥有资产复用和分发的权利”。[来源清单](source-inventory.json) 将其记录为 `GUARD-OWNER-20261006`，覆盖已选定资产的选择性复制、改写和随 ArchSift 分发；第三方库仍使用上述许可。Guard 没有公开 LICENSE 不再阻碍本项目内已授权资产迁移。该记录没有为 Guard 或 ArchSift 选择公开开源许可证。
