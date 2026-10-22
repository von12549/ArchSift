# LT04 Web integration and machine acceptance

Date: 2026-10-15 (Australia/Sydney). **LT04 UI/MACHINE COMPLETE; LT05 NOT STARTED; PRODUCT NOT ACCEPTED / NOT RELEASED**. User owns final independent visual/hands-on review.

## Inputs and implementation

- Branch codex/archsift-lightweight-workbench. Actual target external copy D:/ArchSift-lab/fixtures/lt02a-ifx-cc06528-20261015, source `cc06528ab17052baafa496df2a89f6cde9207384`. Original D:/IFX-10-Root/IFX-New stays read-only/clean.
- Session config/evidence D:/ArchSift-lab/runs/lt04-web-smoke-20261015. Gate inputs from lt03-ifx-final-config.json; copy target rules into the external library and generate CLI-readable .test-workbench-*.json. Ruleset lt02a-mechanism-only, case ifx-context-mechanism. Original 80-ID baseline SHA-256 `55b0f65b535516993761a0121ed4b6db26a98a698291c03056b1a85052cf3a33`. MTP/CTRF, Release/net10.0; reviewed LT02A offline feed/cache.
- Web/CLI share TestGateService.RunAsync, config and authoritative reports. No editing IFX C# tests; copied external JSON uses existing validated rule editor. UI shows project, original baseline IDs, JSON cases and old excluded A/B inventory; actual class/method/results come only from reports. Full mode has no original filter; subset requires explicit UID. Cancel/history/downloads/existing full-evidence entry integrated. New/old Web tasks share the run lock.

## Real gates and API checks

Local loopback API uses session token. Before execution, config returns baseline 80, JSON 1, excluded 588 and empty history. External copied rules/config exist and editor reads original rule ID. Target-source rule SHA-256 remains `abd6a7d94e3655b4255b2a280e8fa58c6c50e64ce91dce6848121d24d20a92aa`.

Final Web full run D:/ArchSift-lab/runs/lt04-web-smoke-20261015/output/test-runs/20261015T115459285-69847902: 80 originals+1 JSON passed, complete coverage, excluded 588, allow/0. JSON SHA-256 `9b683ad25b326ccf60d4696f79b81a1b2d9cdbc2273c55f79789ba3610ac6a37`. Concurrent old /api/run/analyze returned 409, proving shared lock.

Matched-rule/baseline/target CLI full run .../test-runs/manual-106f538d0c14478ba638775609a42ff7 also 80+1 allow/0; SHA-256 `9b5940a104d30fac77181e2bad05e88ae4a520bf11061317713e3c2283fc3077`. runGate, exit, rule SHA, every case ID/result/executed flag, expected originals and exclusions match; run identities/times differ.

Web subset using one discovered UID yields block/4; immediate cancellation of another full run yields cancelled/block/130. Separate reports/history retained without prior allow reuse. Existing rule API temporarily forbids actual EndpointScopeMetadata → ExecutionScope in **external** rules: full gate block/2, one actual finding, all 80 originals executed. Negative .../test-runs/20261015T115903823-f292d7a6 JSON SHA-256 `9b75e50cb4076139424bed518ff5e02011f8ce9db251cdf5d88adaac1aaae975`. Restore external rules exactly; negative history stale, old success separate/unknown freshness. Final history distinguishes finished/failed/cancelled and each gate.

JSON/HTML downloads HTTP 200 with readable 80/1/588 and complete coverage. All six Web/CLI reports validate against frozen schema with local NJsonSchema 10.9.0, zero errors. UI uses DOM text nodes and never recalculates authoritative JSON.

## Regression, host and boundaries

Solution Release/net10.0 build 0 warnings/errors. Existing UnitTests 151/151, IntegrationTests 95/95, ArchitectureTests 2/2, no skips; Testing.Tests native xUnit v3 17/17, no skips. Solution VSTest dotnet test is inapplicable to that MTP project; use generated native executable instead. Architecture checks register Web→Testing/TestGate. Key builds/real gates/regressions retain environment/Profile hash `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`. Original checkout clean; external copy retains only four reviewed LT02A reference/NuGet/adapter/rules changes. No install/original-test-source changes.

Independent visual/manual usability is not yet confirmed. LT05 still requires matched UI/CLI/CI, full IFX regression and cold/warm/disk measurements. Excluded A/B rules are not passed; unknown history is not current-source compliance proof.
