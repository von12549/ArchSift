# Integrating ArchSift into a project

This guide covers the unified 0.10.0 release and its candidates. The separate matching acceptance record binds accepted artifact bytes; source [release status](releases/0.10.0/release-status.json) records its dated preparation progress. The generic templates contain no private IFX inputs. Adopting them is a consumer change requiring review of that project's tests, policy and dependencies.

## Package and runner inputs

Keep your dedicated architecture-test project and its existing tests/assertions. Use exact `ArchSift.Testing` and local `ArchSift.Cli` versions from one release. Four library package IDs remain `ArchSift.Contracts`, `ArchSift.Core`, `ArchSift.ArchUnit` and `ArchSift.Testing`; the CLI package ID is `ArchSift.Cli` and its command is `archsift`. The Web binary is delivered in the Windows ZIP, not by the CLI tool package.

The [generic templates](../samples/workbench/README.md) show the test project, local tool manifest, offline NuGet configuration, rules, Web configuration and gate configuration. Replace template paths, hashes and identities deliberately. Existing central package management may declare the one exact `ArchSift.Testing` version centrally; do not introduce conditions, floating ranges, `VersionOverride` or duplicate product declarations that bypass package approval.

The portable gate currently validates SDK **10.0.303**, **net10.0**, **xUnit v3 4.0.1** and **Microsoft.Testing.Platform/CTRF**. Keep the corresponding `global.json` pinned. Analysis support for net8/net9/net10 does not mean that this daily gate accepts every test runner or SDK. Do not silently update the runner or rewrite the required-case manifest to make a mismatch pass.

## Sources, restore and package approval

Use the release's minimal offline feed plus separately approved consumer dependencies. A generic ArchSift feed is not the complete dependency source for every target. Configure `<clear />` in NuGet package sources and an explicit local feed. Keep `packages.lock.json`, local tool manifest and reviewed package approval with the consumer. Missing dependencies must fail rather than fall back to network.

During initial integration only, restore deliberately to create/review the new lock. Then use locked restore and fresh external caches for qualification. `PackageToolApproval.Verify` checks the exact Testing version, the locked transitive Testing closure with content hashes and the exact local CLI version/command. Its approval document has `schemaVersion`, `testingVersion`, `cliVersion` and `packages` entries containing `id`, `version` and lock-file `contentHash`. This is package integrity approval, not a vulnerability audit. Preserve other legitimate consumer tools.

Use child-only `DOTNET_CLI_HOME`, paired with `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`; keep `NUGET_PACKAGES` or the explicit `--packages` directory external. Do not alter persisted environment variables, PATH or shell startup files. The gate uses its configured explicit offline source, while tool restore needs the separately reviewed local NuGet configuration.

## JSON adapter and policy

Compile the [JSON adapter example](../samples/ifx-lt02a/ArchSiftJsonTests.cs) only after adapting its namespace and assembly selection to your project; its IFX-specific assembly helper is an example, not a reusable dependency. [PortableTests.cs](../samples/portable-verification/PortableTests.cs) is a small independent example using its own assembly. Each enabled rule becomes an actual test and emits the `ARCHSIFT_JSON_CASE` and `ARCHSIFT_JSON_EVALUATION` markers. Load the explicitly reviewed production assembly set once, evaluate through `ArchitectureTestAdapter` and call `AssertPassed`. Do not fabricate a pass from rule inventory or display text.

This test adapter accepts enabled **type-dependency** rules only. The ordinary analyzer's other rule types remain available through their own workflow. A selector matching no source types is inconclusive unless explicitly reviewed `allowEmpty` semantics apply. Ruleset IDs, rule IDs, reasons, scope, disabled/deferred items and exceptions require consumer review. A UI copy changes neither the original policy nor its approval hashes automatically.

`policy.json` binds ruleset paths/hashes and responsibility sources. Its `sources` can be empty for a generic synthetic fixture. Where populated, the existing responsibility vocabulary is `ifx-csharp`, `json` or `deferred`; those stable machine values must not be renamed during documentation or branding. Deferred responsibilities are not passed cases.

## Original C# case identities

For portable schemaVersion 2, compile the [case-identity attribute](../samples/ifx-formal/ArchSiftCaseIdentityAttribute.cs) into the test project and preserve existing test signatures/arguments. It emits actual execution IDs plus stable typed logical identities. Supported parameter types and validation are defined by `CaseIdentities`; generic signatures or unsupported argument types reject rather than receiving invented identities.

Establish `required-cases.json` by independently running/reviewing all original native tests before accepting the integration. Record the approved SDK/runner/TFM/package version, group and each `CaseIdentity` (assembly, type, method, declared parameter types and typed arguments). Review names, Theory rows, assertions, skips and intended responsibility coverage. Keep the manifest and its actual SHA-256 fixed. JSON additions cannot compensate for missing originals. Do not automatically bless whatever a failing integrated run happened to discover.

Discovery UIDs, xUnit case IDs and execution IDs belong to different domains and runs. Reconcile the actual current discovery/execution with approved logical keys. Duplicate, missing, unknown, skipped or ambiguous cases prevent full allowance. Historical schemaVersion 1 instead uses a path-bound execution-ID baseline; do not combine legacy baseline/mapping fields with portable schemaVersion 2 or reuse old execution IDs after moving paths.

## Gate configuration fields

| Field | Meaning and constraint |
| --- | --- |
| `schemaVersion` | `2` for reviewed portable logical identities; `1` for the historical baseline workflow |
| `targetRoot` | Absolute independent consumer checkout/snapshot being tested |
| `testProject`, `nuGetConfig` | Paths under the target root |
| `testExecutableName`, `targetFramework`, `configuration` | Actual executable stem, `net10.0`, and Debug/Release |
| `localFeed`, `packageCache` | Existing approved offline source and external package-cache directory |
| `rulesets` | Reviewed enabled ruleset paths; relative paths resolve under the target root |
| `runDirectory` | Fresh, nonexistent, absolute directory outside and disjoint from the target root |
| `dotnetCliHome` | Optional absolute owned child of this run directory |
| `runnerFormat` | `mtp-ctrf` for the portable xUnit v3/MTP contract |
| `auditMode` | `inherit` by default, or the explicitly reviewed mode; `offline-architecture` records audit `not-run` |
| `toolIdentity` | Exact full package/tool version, including an RC suffix when applicable |
| `policyFile`, `policySha256` | In-target native policy and SHA-256 of its actual bytes |
| `requiredCases`, `requiredCasesSha256` | In-target approved logical case manifest and actual-byte SHA-256 |
| `toolRoot`, `toolManifest`, `toolManifestSha256` | Optional fixed-tool payload identity; supply the complete matching set when that protection is used |
| `filterCaseId` | Optional actual discovered UID for a diagnostic subset; never a complete-gate pass |

Use the [gate template](../samples/workbench/test-gate.template.json) after supplying real paths, approved case entries and hashes. Never use the all-zero example hashes as approval. Keep Web configuration and gate configuration separate, with matching target roots. Each CLI invocation needs a fresh `runDirectory`; Web creates controlled run copies for its jobs.

## Local use and CI

```powershell
dotnet tool run archsift -- test-gate --config 'D:/ArchSift-state/MyProject/gate.json'
```

Restore the exact local tool from the approved offline configuration before this command. Alternatively invoke the verified ZIP's `archsift.exe` by absolute path. In CI, provision the pinned SDK and reviewed package feed explicitly, restore the tool, create fresh external state/cache directories and invoke the same full gate. Propagate its actual exit code and retain JSON, HTML, state markers, CTRF and stage logs even on failure. Do not add a test filter to a required full-project check.

Test-gate exits are `0` complete allowance, `2` actual assertion/rule violation, `3` execution/report error, `4` inconclusive/subset/input-change coverage, `5` invalid configuration and `130` cancellation. These differ from ordinary analysis exits, where a complete violation may return 0. `offline-architecture` is explicitly **audit not run**, not a security pass. The gate forces snapshot updates off in controlled children; it does not run target application entry points.

## Workbench, evidence, upgrade and rollback

Start the verified Windows Workbench with the ordinary Web JSON, select Architecture tests and load the external gate JSON. Full execution, subset, cancellation, errors, current results and read-only History share the CLI service. Old success cannot replace a failed/cancelled current run. Download the current authoritative JSON/HTML and inspect exclusions as well as passed cases.

Dedicated tests and the daily gate do not establish current-source assembly binding. Use the [external evidence workflow](external-build-evidence.md) for that separate claim. Unbound/stale/incomplete evidence stays inconclusive or error.

Upgrade consumer tooling as one reviewed combination: packages, local tool manifest, locks, package approval, policy/required-case compatibility, configuration and qualified Workbench payload. Preserve the complete previous combination and failed attempts. Re-run native identity/coverage, cold/moved-path and gate checks before adoption. [Setup rollback](setup.md) changes a managed binary selection; it does not roll back consumer policies or package locks. Never overwrite an existing version's bytes or discard later user edits. Live IFX upgrade/adoption remains a separate authorized task.

For framework-dependent library/tool/test use, provision a supported patched .NET 10 runtime (at least 10.0.13 at this release) separately from the pinned 10.0.303 SDK. The SDK version does not prove the selected shared-runtime patch. The Windows ZIP includes its reviewed 10.0.13 runtime; it does not install or repair system tools.
