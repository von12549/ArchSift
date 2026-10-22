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

## Responsibility projection in portable test reports (IFX version 0.10.0-cl03.1)

`notIncluded` in `architecture-test-run` v1 continues to mean "not executed as JSON", retaining original IDs and LT00 historical reasons. It does not prove that an item currently has no checks or that its policy was not adopted. The optional `responsibilityProjection` extension (independent schemaVersion 1) projects policy state, execution responsibility and this run's result from the formal NativePolicy, approved decision references and structured cases. It does not affect the enabled selection, rule evaluation or gate decision.

When an approved mapping exists, three A items show IFX C# responsibility. They show passed only when the associated cases actually pass in this complete run. Missing cases, skip, duplicates, cancellation, incomplete runs and unknown mappings cannot borrow historical success. In the historical projection described here, Registry4 shows adopted-pending-activation and not-run and is not automatically added to the JSON selection. Items without reliable current decisions show unknown; old candidate labels are historical metadata only.

For the historical frozen selection of 20 JSON rules, 568 items were not executed as JSON. Excluding three C# responsibilities left 565 = 179 A + 386 B, including four adopted-pending-activation and 561 pending/deferred items. These values come from inputs, are not product constants and do not mean every item lacks checks. The UI counts the projection dynamically. Reports without the extension retain their original result and show that current responsibility projection is unavailable; unknown versions/formats are unavailable without changing historical bytes. The CL02 fix was packaged independently in `0.10.0-cl03.1` and activated in IFX reports through [CL04 actual-main a2adb87 acceptance](https://github.com/von12549/IFX-New/actions/runs/39543191970). Subsequent documentation was formally delivered to IFX main `f58205b`, also the local baseline at that stage; see [documentation-delivery acceptance](https://github.com/von12549/IFX-New/actions/runs/39550973000). The matching Web ZIP remained a locally tested synthetic candidate, not consumed by IFX or publicly released. The minimal 43-file product-source archive was merged through [PR #9](https://github.com/von12549/ArchSift/pull/9) to ArchSift main `3f61be0`, with actual-main normal CI acceptance and a pinned local update complete; see the [source-archive merge](https://github.com/von12549/ArchSift/pull/9). Other uncommitted documentation, templates, history and operations materials remained outside those 43 files. This guide's delivery adds no package/Web release, report run or IFX execution; CI acceptance is bound separately to the relevant PR and actual-main commits.
