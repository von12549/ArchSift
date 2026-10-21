# JSON, HTML and SARIF semantics

JSON is the authority; HTML and SARIF are projections. Analysis report schemaVersion 1 records operation, source/rule/engine/assembly identity, scope, build context, execution, compliance, per-rule results, findings, coverage, limitations, errors and run metadata.

Per-rule pass/violation/inconclusive/not-applicable/error is separate from execution. Effective violations produce noncompliant policy even when coverage is partial. With no effective violation, required inconclusive checks prevent compliant policy. All disabled/not-applicable checks yield not-applicable; analyze has null compliance.

Unbound assembly findings are retained as actual DLL evidence while associated current-source checks remain inconclusive. Exceptions preserve findings, stable IDs and user reasons. Source/evidence drift invalidates current-source conclusions. Input identity includes source records, exact rule/assembly bytes, TFM/configuration and tool/engine versions; timestamps/duration/local paths remain run metadata or locator fields.

HTML escapes user-authored content. Genuine assembly/type evidence never acquires guessed source files or line numbers. Unique output directories prevent old reports from standing in for failed or cancelled current jobs. Report-write failures return execution error 3.

## Chain summaries

chain-summary schemaVersion 1 is additive and independent of existing analysis/comparison schemas. It records the immutable chain snapshot, one source/build input identity, ordered entry identities/hashes, execution, compliance, exit code, unique project count and per-entry results/coverage/binding/limitations/report links. Project counts are not summed across repeated analysis of the same target. Rule identity is qualified by entry; findings from different policies are not deduplicated.

A missing/invalid ruleset gets typed diagnostic JSON/HTML/SARIF, with no invented rule results/findings. Later entries continue. Effective violation takes precedence; otherwise missing/error/inconclusive/skipped means inconclusive, otherwise any compliant child means compliant, otherwise all-not-applicable is not-applicable. Any incomplete child makes execution partial. Cancellation retains completed evidence, a cancelled summary and skipped remaining entries. A partial/compliant summary must expose its uncovered scope.

Output: snapshot.json; independent entry report directories; diagnostic.json/html/sarif where appropriate; chain-summary.json/html/sarif. CLI and UI invoke the same service. Card/chain verification always writes all three formats.

## SARIF 2.1.0

Stable findings use partialFingerprints. Info maps to note; warning/error retain their levels. Exceptions use accepted external suppressions. Inconclusive assembly findings use review rather than conclusive fail. Only genuine root-relative paths present in inputIdentity receive artifact URIs, encoded per segment and bound to the actual source root via %SRCROOT%. No source mapping means no invented location.

Invocations carry executionSuccessful and execution/coverage notifications; properties retain status/coverage/input information. Only completed comprehensive changes comparisons use baselineState new/unchanged/absent. Policy changes and partial comparisons do not make that promise.

Chain SARIF has one run per analyzed child, entry-qualified rule IDs and genuine findings, plus an orchestration run for diagnostics/limits/skips. Missing/invalid entries never become fabricated results. Official OASIS schema validation applies to examples and generated outputs. [SARIF specification](https://docs.oasis-open.org/sarif/sarif/v2.1.0/os/sarif-v2.1.0-os.html) and [GitHub support](https://docs.github.com/en/code-security/reference/code-scanning/sarif-files/sarif-support) describe the projection boundaries. Logic-only results may not appear as source alerts; default CI samples upload artifacts without enabling required checks.

Generated product prose is English in every format. The Chinese UI changes presentation only; user-authored policy IDs, descriptions, exception reasons, paths and source text are preserved.

## 可移植测试报告的责任投影（IFX现役0.10.0-cl03.1）

`architecture-test-run` v1的`notIncluded`继续表示“未作为JSON执行”，保留原ID和LT00历史原因；它不是当前无人检测或政策未采纳的证明。新增可选`responsibilityProjection`（独立schemaVersion 1）按正式NativePolicy、批准决定引用及本次结构化案例投影政策状态、执行责任和本次结果；它不参与启用集合、规则求值或门禁判定。

三项A在批准映射存在时显示IFX C#责任；仅本次对应案例真实通过且运行完整时显示passed。缺案例、skip、重复、取消、不完整、未知映射均不借历史成功补足。Registry4显示adopted-pending-activation及not-run，不自动加入JSON集合。其余无可靠当前决定的条目显示unknown，旧候选标签仅保留为历史元数据。

当前冻结20条JSON时568项未作为JSON执行，三项C#之外565＝179A＋386B，含四项已采纳待启用及561待决／暂缓；这些数字来自输入，不是产品常量，也不代表全部无检测缺口。UI从投影实时计数。旧报告不含该对象时保留原结果并提示当前责任投影不可用；未知版本或格式标不可用，不改历史字节。CL02修正已打入独立 `0.10.0-cl03.1`，并经[CL04实际main a2adb87验收](https://github.com/von12549/IFX-New/actions/runs/39543191970)在IFX现役报告生效；后续说明已正式交付至IFX main `f58205b`，本地IFX也在该基线，见[说明交付验收](https://github.com/von12549/IFX-New/actions/runs/39550973000)。匹配Web ZIP仍为本地合成已验候选，未由IFX消费或公开发布。产品源码最小归档43文件已通过[PR #9](https://github.com/von12549/ArchSift/pull/9)合并至ArchSift main `3f61be0`，实际main正常CI验收及本地固定更新已完成，见[源码归档合并说明](https://github.com/von12549/ArchSift/pull/9)；其余未提交文档、模板、历史及操作材料保留，未包含在这43文件中。本次说明交付不新增包／Web发布或报告／IFX运行；CI验收单独绑定对应PR和实际main提交。
