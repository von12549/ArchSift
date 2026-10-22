# W08 0.1.0 acceptance completion

Date: 2026-10-07 (Australia/Sydney)

Conclusion: `W08 COMPLETE / PASS`. A01–A12 have automated, real IFX, independent-operator and final safety/cleanup evidence. W09/W10 prerequisites are satisfied, but neither has started.

## Frozen candidate

- ZIP：`D:\ArchSift-lab\packages\w08-0488c7a30a8943c9b97ff3c61a2913bf\archsift-0.1.0-dev-win-x64.zip`
- SHA-256：`61d5e5df7e87858a0086691f7284925407977304d84d861c4fae93661435f23a`
- Identity: `0.1.0-dev`, `win-x64`, self-contained; manifest lists 581 payload entries, with 582 files including the manifest after extraction.
- ZIP/extracted file sets, per-file hashes, path containment, case-insensitive duplicates and reparse-point checks passed.

Candidate manifest `w08Accepted=false` was frozen before testing. Do not rewrite the verified ZIP after acceptance: that would invalidate its hash and run identities. This report, fixed ZIP SHA-256 and final external audit record acceptance; this is neither remote publication nor a version change to stable `0.1.0`.

## Automated and performance evidence

- 92/92 regressions passed: 70 unit, 20 integration, 2 architecture. Coverage includes net8/net9/net10, six rules, real ArchUnitNET, CLI/UI parity, failure/cancellation, root boundaries, input identity, Worker and host safety.
- Budget frozen using 100-project-chain. Five candidate analyses took 108–243ms, wall time 225–373ms, sampled peak approximately 27–32MiB, all below analysis 700ms, CLI wall 1000ms and working-set 64MiB limits.
- Windows x64 self-contained CLI, Worker and loopback UI started with empty child PATH and no SDK path. Isolated-build SDK prerequisites were not described as SDK-free.

## Real IFX

- Declaration discovery: 105/105 projects, 346 resolved project references, 114 direct package declarations; zero unlisted/external/unresolved.
- existing/net10.0/Debug, no build/restore. The declaration model cannot fully evaluate conditions/properties, producing 105 accurately reported `partial` limitations and 0 execution errors.
- Exact rule consistently found `IFX.Application.Primitives -> IFX.Domain.Primitives`, yielding `violation / noncompliant / partial / exit 4`. The violation stands; partial describes coverage, not execution failure.
- Real IFX UI rechecked 105 projects, the known violation, stale current results/downloads cleared after error, retained history/rules and JSON/HTML exports.

## Independent operator A10

An operator uninvolved in implementation completed external-fixture and real-IFX UI tasks. Initial confidence=3 and misunderstanding remain recorded; additional real-IFX review ended at confidence=4/5. Maximum help is conservatively L2, without field-by-field or click-by-click operation on the operator's behalf. The operator found results consistent with the task and UI information simpler and clearer than Guard. A10 is `PASS`.

Nonblocking usability findings: 105 project rows expand by default and should be collapsible; Chinese UI alongside English tasks needs clearer terminology mapping. The first independent task opened an ArchSift fixture target although IFX was intended; subsequent real-IFX UI review passed separately. Product root, target root, entry, rules and output need clearer separation to reduce mistaken-target risk. The launcher displayed the URL before final JSON was available, creating uncertainty about completion. One reviewed UI process remained until the final audit, then was identified by identity, hash, start time and port and safely terminated. Final audit closed these findings without changing W08 PASS; retain them for post-release UX/lifecycle improvements.

W08 also identified later rule-expression needs: `project-reference` and `nuget-denylist` express prohibitions but no closed project/package allowlists. Composition checks cover only some TFM, naming and duplicate-ID conflicts, without a general allow/deny conflict matrix. This does not retroactively expand 0.1.0 or W09/W10 scope; the formal plan records it as the next post-release target.

## Final safety and cleanup

Final passing audit: `D:\ArchSift-lab\runs\ifx-w08-final-audit-917e35b210d3490d953288cd89db5f64\audit-result.json`, SHA-256 `b100cd3704e4802ec64db870c7e5606acafbe3435acaa52829cb71564a19f39b`.

- Frozen candidate and 10 key evidence hashes matched; `failure=null / pass=true`.
- Candidate process count 0; no listeners on registered ports 2278, 9255 or 14383.
- Trusted persisted host, IFX and ArchSift state matched; before/after host/IFX/product state also matched.
- An overlapping 03:53 UI session left orphan candidate PID 29220. PID, path, file SHA-256, start time and loopback port jointly established its identity before precise termination. Closure evidence SHA-256: `5865ce38fbca6614aa8f0d6c7c182675bb542ee179e94dcf793254ffee2dcd9b`.
- No registered directory could be safely removed without losing evidence, so final cleanup deleted nothing. Success/failure/interruption/report/operator evidence was retained. Unregistered browser downloads and other host paths were untouched.

Coordination-script argument errors, three stopped audits and interrupted duplicate sessions remain recorded as such. None caused persistent environment/Profile/PATH, IFX checkout or ArchSift product-tree drift.

## Gate conclusions

| Criterion | Conclusion |
| --- | --- |
| A01–A08 | PASS: automated fixtures, CLI/UI, reports, status semantics, error/cancellation and stale-result clearing have evidence |
| A09 | PASS: read-only targets, root boundaries, environment isolation, real IFX and final cleanup state matched |
| A10 | PASS: independent operator, confidence 4/5, maximum L2, real IFX and constructed fixture completed |
| A11 | PASS: budget frozen first, candidate measured afterward, all below limits; no separate IFX performance-budget claim |
| A12 | PASS: self-contained ZIP identity, licenses, bytes, CLI/Worker/UI and no persisted PATH/Profile changes established |

W08 is closed. W09/W10 must begin separately as later stages without reusing or rewriting W08 historical results.
