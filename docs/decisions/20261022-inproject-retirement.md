# ArchSift.InProject 0.1.0 路线废止决定

日期：2026-10-22（Australia/Sydney）。当前状态：**SUPERSEDED / 停止推进 / 未作为旧产品验收或发布**。机器权威为[决定 JSON](20261022-inproject-retirement.json)。

正式产品路线采用现有包化 ArchSift 项目内接入。原 ArchSift.InProject 0.1.0 独立宿主、安装／绑定／日常取证链停止继续开发和发布；其未来开发、人工验收和发布待办随路线废止关闭。这项路线决定不声明新方案完整实现旧规格，也不把旧 IP08 人工验收补记通过。

## 固定历史与原验收

原完整引用为 refs/heads/codex/archsift-inproject-0.1.0，固定提交为 2877974fb38a12e856a6045ba223615c200841ee。IP00–IP07 完成；IP08 机器完成、人工部分完成、暂停、未正式验收、未发布。失败、取消、安全限制及用户原话保留。

历史原件：[规格](https://github.com/von12549/ArchSift/blob/2877974fb38a12e856a6045ba223615c200841ee/docs/decisions/20261014-archsift-inproject-0.1.0-product-spec.md)、[实施计划](https://github.com/von12549/ArchSift/blob/2877974fb38a12e856a6045ba223615c200841ee/docs/plans/20261014-archsift-inproject-0.1.0-implementation-plan.md)、[IP07](https://github.com/von12549/ArchSift/blob/2877974fb38a12e856a6045ba223615c200841ee/docs/acceptance/20261014-inproject-ip07.md)、[IP08 机器记录](https://github.com/von12549/ArchSift/blob/2877974fb38a12e856a6045ba223615c200841ee/docs/acceptance/20261015-inproject-ip08-ifx-machine.md)、[人工部分记录](https://github.com/von12549/ArchSift/blob/2877974fb38a12e856a6045ba223615c200841ee/docs/acceptance/inproject-ip08-operator-checklist.md)。固定原件不重写，本地及远端旧分支继续保留。

## 最小归档与保留

完整 Git bundle 位于 D:\ArchSift-lab\archives\inproject-retirement-20261022-01\archsift-inproject-2877974-full.bundle，SHA-256 为 2d62b333ed9c7059e704708f686a443bf0efb9054a92f154e52aba9378b5733e。空 bare 仓无共享对象／alternates，独立恢复与严格 fsck 通过；191 个提交、2,929 个可达对象与原分支完整集合一致，12 个关键源码／文档 blob 可读取。旧分支中的非 InProject 历史同时保留。

同目录的 manifest.json、scope.json、RESTORE.md 记录身份、恢复方式和保管责任；80 份 Git 外关键原件按原字节保存，包括机器／失败／取消记录、候选元数据及 A/B worktree 未提交差异。原 A/B worktree、完整 full ZIP、原机器运行根和最终旧候选载荷继续保留。本次恢复检查未构建、安装或运行旧产品，不声称在当前机器重新验收旧工具。

物理处置按 D:\ArchSift-lab\runs\inproject-retirement-20261022-01\scope.json 的精确清单另记结果。归档与废止生效不以清空目录或删除分支为条件；未知、共享或独有证据保留。保管责任为项目操作者／仓库维护者，无自动到期，只在明确后续处置请求时复核。

## 现役与共享能力

现役 0.10.0-cl03.1、前一 0.10.0-sp03.1 的两套完整 251 包源及全部入口、锁、policy、案例、批准元数据、独立审计和三项 IFX 必需检查保持；当前 28 包合成源及更早迁移级备用继续保留，回退不在本决定中执行。

现有 Core／Contracts／ArchUnit／Worker、独立完整取证、Guard 安全回归、第三方许可、044 历史及全部备份保持。IFX 规则库、A/B 审查采纳、专用测试和 Rules HTML 清单不因本决定清理。112 项为当前已批准基线而非未来上限，Registry4 仍已采纳待启用。日常门禁不增加扫描或取证链。

执行主计划仍为 D:\IFX-10-Root\docs\plans\20261021-archsift-inproject-retirement-and-cleanup-plan.md；本文件登记当前路线决定，历史章节保留各自形成时点。

## 本批物理处置结果

精确清单共 201 个已定位对象；其中一处已证实重复的恢复目录完成可恢复隔离，其余对象按保留或待分类保留登记。唯一原路径为 D:\ArchSift-lab\archives\inproject-ip08-a-full-restored-846dabc，现保管于 D:\ArchSift-lab\archives\inproject-retirement-quarantine-20261022-01\inproject-ip08-a-full-restored-846dabc。

22,326 个文件、2,483,859,451 字节的路径集合及每个 SHA-256 均与保留的原 full ZIP 相同，移动前后再次逐项核对相符；包含清单文件，故比历史验证器报告的 22,325 项多一项。链接与相关活动进程为零，当前产品入口无该目录消费引用。原 full ZIP、原机器证据及 A/B worktree 保持，可依据隔离收据恢复原路径。

永久删除 0，分支删除 0，回收磁盘空间 0；隔离材料无自动到期。未分类项保留，不宣称全部历史材料均已清理。正式文档交付由本次 PR 和对应正常 CI 记录；归档检查不代替产品验收。
