# IFX architecture-test integration example (LT02A)

This directory preserves the additional test code and mechanism-validation rules actually built and run in an independent IFX copy during LT02A. `lt02a-mechanism.json` validates only the JSON -> ArchUnitNET -> xUnit -> MTP/CTRF -> gate chain. It is not approved IFX Class B policy.

The verified IFX source commit was `cc06528ab17052baafa496df2a89f6cde9207384`. Copy the two files into `tests/Architecture/IFX.Architecture.Tests/` in an independent copy of that commit and append a `ProjectReference` to this repository's `src/ArchSift.Testing/ArchSift.Testing.csproj` in the original test project. Preserve all 16 original `ProjectReference` entries, three original test classes, Theory data and the five-second assertion. Clear implicit package sources in the copy's root `NuGet.config`; restore explicitly selects the approved local offline source. Do not apply this example to the original IFX checkout or installation.

Set `runnerFormat: "mtp-ctrf"`, point `testProject` at the original `IFX.Architecture.Tests.csproj`, and point `originalBaseline` at [`docs/decisions/20261015-lightweight-ifx-original-case-ids.json`](../../docs/decisions/20261015-lightweight-ifx-original-case-ids.json), with that file's actual SHA-256 in `originalBaselineSha256`. `rulesets` points to rules in the copy; keep run directories and package caches outside source. The coordinator forces `IFX_UPDATE_SNAPSHOTS=0` in child processes. Formal Class B rules need separate review/enablement and are not adopted automatically by this example.

Actual machine results and input identities are recorded in [LT02A acceptance](../../docs/acceptance/20261015-lightweight-lt02a-ifx-gate.md).
