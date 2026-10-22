# Lightweight architecture-test Workbench LT02A: real IFX integration gate

Date: 2026-10-15 (Australia/Sydney). **LT02A REAL IFX GATE COMPLETE / LT03–LT06 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**. Covers only the authorized independent IFX copy and mechanism JSON; no formal B policy approval or substitute for final independent human UI acceptance.

## Target and input identity

- Original checkout D:/IFX-10-Root/IFX-New, main `cc06528ab17052baafa496df2a89f6cde9207384`; final git status --short --branch remained ## main...origin/main. No application entry, original checkout/install or Guard changes.
- Independent D:/ArchSift-lab/fixtures/lt02a-ifx-cc06528-20261015, local clone without hardlinks, same detached commit. Only this copy adds ArchSiftJsonTests.cs, mechanism JSON, explicit-source NuGet.config and one test ProjectReference to this repository's ArchSift.Testing. Reviewable inputs: [samples/ifx-lt02a](../../samples/ifx-lt02a/README.md). Copy changes are not formal IFX branch/policy publication.
- Original test-project SHA-256 `ea8b35cef1b06153c01d5adb44b0d3e1a3550bd0efe9ffe40ac113182c10b684`; all 20 original test-source hashes in [LT00 baseline](../decisions/20261015-lightweight-ifx-source-baseline.json) match before/after. Preserve all 16 original ProjectReferences (17 after integration), three original classes, Theory data and AllRules_RunUnderFiveSeconds five-second threshold.
- SDK 10.0.303, .NET 10.0.11, Windows x64, Release/net10.0, IFX xUnit v3 4.0.1/MTP. Restore explicitly uses local C:/Users/von12/.nuget/packages and external D:/ArchSift-lab/packages/lt02a-ifx-cc06528-20261015, no network/NuGetAudit. Original/integrated restore/build passed, 0 warnings/errors.

## Native baseline and integrated results

Before integration, native MTP --list-tests json discovered 80 cases and CTRF actually passed 80/80, zero skipped/failed: ArchitectureTests 16, FixtureTests 47, ApplicationPortAdapterTests 17. First/repeat CTRF extra.id sets matched. Discovery uid and CTRF execution extra.id are different identity systems. The [80 original execution IDs](../decisions/20261015-lightweight-ifx-original-case-ids.json) come from actual CTRF, normalized repository LF SHA-256 `55b0f65b535516993761a0121ed4b6db26a98a698291c03056b1a85052cf3a33`, not display names/static counts. Early PowerShell CRLF serialization of the same IDs had SHA-256 `df2ad85d73d723387342d54fc89db327038a1f61bec8e8f93cca4adbe10cf940`; final positive reran with repository LF bytes. Discovery SHA-256 `18b46616703bd1246a8684402a75946a0b2e8a1d8979d2ae21e7807a43c07264`, original CTRF `046f291d200bedfdc36f20371f86b2fcbbf8258dd030da9225bbb22df276aff4`, stored in D:/ArchSift-lab/runs/lt02a-original-20261015/.

Integrated full project executes the same 80 original IDs plus one JSON Theory row, 81/81. Rule SHA-256 for archsift-json: ifx-context-mechanism: `abd6a7d94e3655b4255b2a280e8fa58c6c50e64ce91dce6848121d24d20a92aa`. Source IFX.Platform.Context.Contracts.ExecutionScope actually matches; target is a nonexistent verification namespace, mechanism-positive only. New runnerFormat mtp-ctrf discovers with native IFX MTP, then correlates stable CTRF execution IDs, class/method, terminal states/output markers. --list-tests is not execution; discovery UID is not CTRF execution ID. Native xUnit JSON events remain for synthetic projects.

Final full gate with repository LF baseline: D:/ArchSift-lab/runs/lt02a-gate-positive4-canonical-20261015, allow/0. Restore/build/test passed; 81 discovered/executed, zero missing originals/JSON/skips, rule hashes matched. CLI D:/ArchSift-lab/runs/lt02a-gate-positive4-canonical-result.json SHA-256 `9f690dd8ba9c7def92479ddaade2de712e95553ea2557beffbe7a93a3154d3e0`. Equivalent earlier CRLF positive remains at lt02a-gate-positive3-20261015, SHA-256 `77c6f6b79f38a07e42eb59f6853effb271dd77bcf6d7a5dafcd9cb4bf701cbf7`.

## Negative cases and protection

| Scenario | Actual result and restoration |
| --- | --- |
| Parent IFX_UPDATE_SNAPSHOTS=1, controlled children 0 | Full positive passed; all 11 API snapshot hashes unchanged; parent remained 1 |
| Temporarily append one byte to copy IFX.Platform.Context.Contracts.txt | All 80+1 executed; only PublicApi_ContractsSnapshotsMatch failed, block/2; finally restored exact bytes; result SHA-256 `4691c0d9103b98c5f9477a98249729e44f1485365018cd3ae9559dd4d49eec95` |
| Temporarily forbid actual EndpointScopeMetadata → ExecutionScope | All 80+1 executed; only JSON assertion failed with explicit types, block/2; finally restored rules; result SHA-256 `babf9584bad9f98de570bf65419dd1fb3b6be04ba5980b3cdfb16feb21c2d549` |
| CTRF output shape | Initial gate correctly blocked missing JSON marker, block/4. IFX stdout is a string array; parser fixed/tested. Retain failed D:/ArchSift-lab/runs/lt02a-gate-positive-20261015 and pass in a fresh directory |

MtpCtrfTests covers native IDs/markers, missing originals, assertion failure, duplicates/corrupt reports. Updated seven synthetic exits: 0, 2, 4, 4, 3, 0, 5; native synthetic 16/16. Solution Release build 0 warnings/errors. Existing UnitTests 151/151, IntegrationTests 95/95, ArchitectureTests 2/2, zero skips. Every checked .NET host environment/Profile hash remained `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`, no persistent drift.

## Preliminary costs and next boundary

Same IFX copy: initial native full test about 4.245 s, warm 1.163 s; initial integrated test-only process 3.536 s; observed warm complete gates (offline restore, incremental build, discovery/test) 11.189 s and final LF 15.767 s. These are preliminary, not three matched cold/warm comparisons. Gate includes extra stages, so no faster-than-native claim. LT05 measures at least three cold/warm runs, stages/disk growth, and five-second stability under parallel load. All actual original five-second assertions passed without relaxed thresholds.

LT03 still needs independent architecture-test-run JSON/HTML, provenance/responsibilities, structured findings and history. Immediate CLI results/external logs are not full reports. Unmigrated A/formal B rules are not proved by this gate. Final independent human visual/hands-on acceptance stays user-owned.
