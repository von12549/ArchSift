# ArchSift.InProject 0.1.0 IP08：IFX 真实 worktree 机器验收

日期：2026-10-15（Australia/Sydney）。状态：**机器验收完成；用户独立 UI 视觉与上手验收待用户填写；IP08 未关闭**。逐块条件见 [操作清单](inproject-ip08-operator-checklist.md)。本记录只记真实执行，不把产品正确报告的 `violation`、`inconclusive` 或 `error` 改写成通过。

## 目标、授权与固定输入

用户授权从 `D:\IFX-10-Root\IFX-New` 建立两个新的 IFX worktree，使用完整 `IFX.sln` 测试；并单独授权仅在 B 工作树加入一处可回退、不提交的 C# 注释。A 为 `D:\ArchSift-lab\fixtures\ifx-inproject-ip08-a`，B 为 `D:\ArchSift-lab\fixtures\ifx-inproject-ip08-b`，均指向 IFX 提交 `cc06528ab17052baafa496df2a89f6cde9207384`。B 的受控差异在 `src/BuildingBlocks/IFX.Domain.Primitives/TenantId.cs`，原件 SHA-256 `3e5d34584573931aca715287623f1af4c91dab24dd7efa97584049047d0b3eb0`，B SHA-256 `07583e6e8e06605596af2a42f8d69b58d663c54b71570cd85d2c5eec0c3d4d37`；该注释保留供用户核查。原始 checkout 未修改。除 B 的获授权注释及两工作树中工具自有的 `.archsift/`、`.gitignore` 外，业务源码只读。

完整 solution 包含 105 个 SDK 风格 C# net10.0 项目，目标 SDK 实测为 10.0.303。离线源为本机精确包字节；280 项第三方 build／analyzer／生成器输入逐文件哈希审查清单为 `D:\ArchSift-lab\runs\inproject-ip08-ifx-6f11ca2e4e8c4567be2ec548d03b5466\ifx-build-input-review-v3.json`，SHA-256 `985bd80fc9684bdc88b0c1299987644c1ba2fb13537fc6aa45ae1b1a4f6d7e0f`。配置禁止网络回退；只运行新快照构建与专用 ArchUnitNET/xUnit 规则测试，不运行 IFX 应用或功能测试。

本次采用同一探索性规则配置（两 worktree 的 `project.json` SHA-256 均为 `d10cf1c2ce7f60c0e3043c44c25599086196719961150b611d339da75de76b47`）：一个目标框架规则覆盖 105 个项目上下文，一个真实 ArchUnitNET 类型依赖规则匹配 2 个源类型。此规则只验证 InProject 的真实执行与关联，不代表 IFX 正式采纳该架构政策。

最终候选来源为 ArchSift 提交 `846dabc805c1ffb58e0a28e7324212b76072f2eb`，ZIP `D:\ArchSift-lab\packages\inproject-0.1.0-846dabc\archsift-inproject-0.1.0-win-x64-bed54651872b.zip`，SHA-256 `bed54651872b8fa7763d33957c37c68465bd3266b7d37a765617ae9df49cc6d8`。其外置合成机器资格记录在 `D:\ArchSift-lab\runs\inproject-ip07-candidate-22471101e61248dea569d7676f2a83ce\candidate-qualification.json`，冷／热 daily、full 独立复核、真实 xUnit、Web/CLI 一致及负例均通过；这不代替下述 IFX 验收。

## 顺序门禁与未通过历史

首个旧候选在完整 IFX 构建前因条件属性与第三方构建输入缺乏关联审查而拒绝。审查完成后，替代候选分别暴露编译诊断截断、Razor SDK 后置 analyzer 与生成源码关联问题；均保留失败运行和原始日志，没有计作通过。`f5dc838` 候选的 A daily 已实际编译 105/105 项目且源码绑定通过；首次 full 在 IFX 根 `global.json` 的 Microsoft.Testing.Platform 设置下拒绝 VSTest 专用测试发现，退出 3，运行 `0a32c07646c74d6bab2d62a1b99bbcf3`。随后 `5754e7a` 候选的 full 实际运行 1 条 xUnit，但长规则键的显示名被 xUnit 截断，覆盖核对判为不完整、门禁 block／退出 4，运行 `4d9304ab6d9f4bcb8d4904994d8408c5`。两次失败的宿主安全哈希比较均相等，失败结果保留为历史。

`a6c524c` 将专用 VSTest runner 固定在工具自有运行目录，并用短而稳定的测试标识关联发现、TRX 与回执；完整规则键仍保存在规则材料与回执。Release 开发检查 281/281 通过，SDK 10.0.303、工具与专用测试 net10.0，宿主哈希相等；记录在 `D:\ArchSift-lab\runs\development-fff0acfde1b545569c03072099341489`。

并发／取消／失主恢复后，外置导出预览发现：使用外置 evidenceRoot 时，安装遗留的空默认 `.archsift/evidence/` 无 owner 标记，原版本会拒绝整套导出。`846dabc` 只跳过该空且未使用的目录；非空无 owner 的证据根继续拒绝。Release 开发检查 281/281 通过，记录在 `D:\ArchSift-lab\runs\development-8b0f1b404f6d417dafd6663a4b9748fb`。新候选的 A/B 升级均保留旧报告与受控 B 注释、宿主哈希相等；A/B 导出预览限制均为空。

| 块 | 实际机器结果 |
| --- | --- |
| 0 预检 | 两工作树同一 Git 提交、B 仅一处授权的未提交业务注释；完整 105 项目 solution、条件属性、280 项离线构建输入逐项审查。 |
| 1 A 安装 | 固定候选初始安装与最终 `846dabc` 升级收据见运行根 `a-init.*`、`a-upgrade-846dabc.log`；载荷、版本锁及 manifest 经目标核验，旧规则与失败证据保留，业务源码和宿主哈希相等。 |
| 2 A daily | 运行 `03a5f9616eeb48d08b1119ff3720dafe`，退出 0、`sourceBound=true`、`coverage.complete=true`、`freshness=current`；105 个实际编译上下文，2/2 必检规则；B 类 ArchUnitNET/xUnit 发现／开始／完成 1/1/1、TRX 通过、匹配 2 个源类型；无绑定限制。输入 SHA-256 `4f0a980837e9180382bf0efd666dde6989ff4bb2d2aff01c3f7f59b9a82eb8d9`，宿主哈希相等。 |
| 2 A full | 运行 `489ebb9b2df441b293343991a825452b`，退出 0、`sourceBound=true`、`coverage.complete=true`、`freshness=current`；105/105 构建上下文、2/2 必检规则，B 类 xUnit 发现／开始／完成 1/1/1、TRX 通过。外置 bundle `D:\ArchSift-lab\runs\inproject-ip08-ifx-6f11ca2e4e8c4567be2ec548d03b5466\evidence-a\489ebb9b2df441b293343991a825452b` 的独立 `evidence-verify` 返回 `verified=true`、22,325 文件、worktree ID `59044a891ac4d995fb55779c6e2d8f595e4a9f06b24fb31e8677c2bb1b83b798`；宿主哈希相等。 |
| 2 A 最终候选 full | `846dabc` 运行 `c5f48205052940a99712a9b00a62e703`，退出 0、`sourceBound=true`、完整覆盖、当前、105/105 实际构建上下文与 2/2 必检；B 类 xUnit 1/1/1、TRX 通过。外置 bundle `D:\ArchSift-lab\runs\inproject-ip08-ifx-6f11ca2e4e8c4567be2ec548d03b5466\evidence-a\c5f48205052940a99712a9b00a62e703` 的独立 `evidence-verify` 返回 `verified=true`、22,325 文件及同一 A worktree ID；宿主哈希相等。 |
| 3 B 安装与门禁 | 同一最终 ZIP SHA-256 `bed54651872b8fa7763d33957c37c68465bd3266b7d37a765617ae9df49cc6d8` 已升级于 B；A/B 项目配置 SHA-256 均为 `d10cf1c2ce7f60c0e3043c44c25599086196719961150b611d339da75de76b47`，第三方审查清单 SHA-256 均为 `985bd80fc9684bdc88b0c1299987644c1ba2fb13537fc6aa45ae1b1a4f6d7e0f`。最终 B daily `8cecb2fb4be846ba818c9332156c5c10`：105/105 实际编译、`sourceBound=true`、2/2 必检覆盖、B 类 xUnit 发现／开始／完成 1/1/1、TRX 通过、门禁 allow／退出 0／当前。B 输入 SHA-256 `776799b3a468569d489276659e7e958e0bece9c3e8b8c33fac734ef384cf4006` 与 A 的 `4f0a980837e9180382bf0efd666dde6989ff4bb2d2aff01c3f7f59b9a82eb8d9` 不同；B worktree ID `b43ba2c4db833f464a7eee0c7519b0d28799f9217d72cd89b5982cbaa2507d21` 与 A 不同。B 外置证据根独立，获授权 C# 注释哈希与 A full 报告哈希未变，宿主哈希相等。先前 B daily `e3980f6f927e422ba5ec0ad97fd9d345` 也通过，但不作为最终候选当前结果。 |
| 4 并发、取消与恢复 | A 运行 `546bc230cc154096b59d1da57bc42a00` 与 B 运行 `af019af9db6145a49c1e35d4c975e966` 同时进入 build；仅取消 A 后，A 为 cancelled／130／stale，B 继续完成 105/105 项目、源码绑定、2/2 必检及 B 类 xUnit 1/1/1，门禁 allow／0／current。原 A full、原 B daily 报告摘要不变，worktree ID 不同、宿主哈希相等；`D:\ArchSift-lab\runs\inproject-ip08-ifx-6f11ca2e4e8c4567be2ec548d03b5466\concurrency-result.json`。新启动的 A 运行 `8f45849c4ac7455fbb6bbde6873d1d1f` 在 build 失主，`status` 将当前置为 failed／3／stale，旧 full 摘要不变，宿主哈希相等；`orphan-recovery-result.json`。 |
| 5 保留、归档与导出 | A 保留预览：7 个可用 daily、1 个当前保护、3 个 full 保护、0 个选中清理项、无预算超限；`D:\ArchSift-lab\runs\inproject-ip08-ifx-6f11ca2e4e8c4567be2ec548d03b5466\a-retention-preview.json`。最终 A full 整包归档为 `D:\ArchSift-lab\archives\inproject-ip08-a-full-c5f48205-846dabc.zip`，SHA-256 `6470f466dadfce870ce8a0720d04d15a04190d4b442f9db215f4cdfed08a0707`；独立归档复验、外置恢复后再次复验均通过，均为 22,325 文件与同一运行 ID。当前 full 清理预览显示 `protectedBy=["current"]`、`canDelete=false`，未实际清理。B 外置 worktree 导出 `D:\ArchSift-lab\archives\inproject-ip08-b-export-846dabc`，导出及独立复验均通过 3,975 文件、同一 B worktree ID；A/B 导出预览均无限制。材料见运行根的 `a-full-archive*.json`、`a-full-restore.json`、`a-current-full-prune-preview.json`、`b-worktree-export*.json`。 |
| 6 最终源码与宿主审计 | 原 IFX checkout Git 状态为空。两 worktree 同一 HEAD；对各自预检时登记的 782 个 tracked 文件逐一复算，A 仅 `.gitignore` 改变，B 仅 `.gitignore` 改变（B 注释在预检基线前登记，最终 SHA-256 仍为 `07583e6e8e06605596af2a42f8d69b58d663c54b71570cd85d2c5eec0c3d4d37`）。其余新增文件限于 owned `.archsift/`；A/B `.gitignore` 仅添加精确 owned 忽略条目。没有遗留 InProject CLI/Web 进程。用预检所用的 `scripts/HostState.ps1` 算法跨新 PowerShell 进程复核，宿主基线与最终摘要同为 `152936458922c7e5ba28ad86cf5d3ca27ea565806f2ca60ac1d63cb16320d750`；各门禁运行内检查也相等。 审计 JSON：`D:\ArchSift-lab\runs\inproject-ip08-ifx-6f11ca2e4e8c4567be2ec548d03b5466\final-machine-audit.json`，SHA-256 `f82aafe6cff6256839574b6f3c760bf4ccf6a0696cf31bfd11d7937e8322b6e2`。 |

用户独立人工验收仍按 [操作清单](inproject-ip08-operator-checklist.md) 由用户亲自填写；本记录不代填视觉、上手、信心或辅助级别，也不关闭 IP08、合并或发布。
