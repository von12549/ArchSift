# ArchSift

ArchSift is an independent local .NET architecture and dependency analyzer. **0.5.0 is published and independently verified.** It adds native CLI UI output/lifetime repair and a separate portable Windows setup with install/adopt/upgrade/rollback/recovery. The workbench retains the reusable ruleset library, independent chains, English/Chinese UI, eight rule templates, real ArchUnitNET checks, JSON/HTML/SARIF reports and Git comparison.

0.6.0 is [in development](docs/plans/20261011-archsift-0.6.0-plan.md): current version display, real chain progress, a config profile launcher and bounded optional parallel execution. The current source guides describe development behavior; stable package users should use the [0.5.0 installation](docs/installation-0.5.md) and [Setup](docs/setup-0.5.md) guides. No 0.6.0 publication or performance acceptance is claimed yet.

Obtain the Windows x64 0.5.0 package and separate Setup from the [v0.5.0 Release](https://github.com/von12549/ArchSift/releases/tag/v0.5.0), then verify all assets against the [0.5.0 publication record](docs/releases/0.5.0-publication.md). The [v0.4.0 Release](https://github.com/von12549/ArchSift/releases/tag/v0.4.0) and its [historical publication record](docs/releases/0.4.0-publication.md) remain available. Acceptance retains declaration-level IFX limitations; it does not adopt the tested IFX candidate policy.

The tool runs on .NET 10 and targets SDK-style C# net8/net9/net10. Windows x64 is the self-contained distribution; running that package does not require an installed .NET SDK/runtime or Guard. Linux CLI is tested separately. Start with [Windows installation and environment requirements](docs/installation.md) and [portable setup](docs/setup.md). [0.5.0 execution and release gates](docs/plans/20261010-archsift-0.5.0-plan.md) record progress. ArchSift uses the [MIT license](LICENSE), with separate third-party notices.

## Development

Requires .NET 10 SDK, PowerShell 7, Git, and a complete explicit local NuGet feed. `global.json` starts at SDK 10.0.100 with latestFeature roll-forward; Windows development checks use SDK 10.0.303. Multi-framework build fixtures also need their targeting packs and supported SDKs.

```powershell
pwsh -NoProfile -File scripts/Invoke-DevelopmentChecks.ps1 -VerifyContracts
node scripts/Test-WorkbenchLifecycle.mjs
```

Checks restore in locked mode from the explicit offline feed (default user NuGet cache), build, and run unit/integration/architecture tests. Missing packages fail; the script does not silently enable network access. Use `-LocalFeed` for another offline feed. `-InitializeLocks` is for an intentional dependency update only.

Each dotnet child receives isolated `DOTNET_CLI_HOME` and `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`. User/Machine environment, process PATH and four PowerShell Profiles are compared using hashes. New logs and TRX files go to external `D:/ArchSift-lab/runs` by default; development bin/obj and the isolated package cache are ignored in this repository. Real IFX integration remains operator-driven under the acceptance plan.

## Use and boundaries

CLI: `analyze`, legacy composed `verify`, `chain verify`, `changes`, `rules validate/render/draft`, and `ui --config`. The UI binds to a random loopback port and requires a session token. Closing the browser does not stop the server; use Safe shutdown. 0.5.0 forwards Web stdout/stderr and binds Web to the parent CLI lifetime; real IFX terminal acceptance and public-package smoke passed. Older users should consult the [0.4.0 diagnostic and temporary Web entry](docs/installation-0.4.md#known-040-cli-ui-launch-issue).

Rules, state and reports live outside the analyzed target. Violations return exit 0 when the run is complete and conclusive; partial/inconclusive/configuration/execution/cancellation statuses remain distinct. A `partial / compliant` result means checked policies passed with limited coverage. It never certifies full source or IFX protection. Source-bound assemblies are required for compiled-code compliance.

Projects reference Contracts and Core in one direction: Core owns analysis/services; ArchUnit adapts the pinned engine; CLI/Web compose those services. User-authored rule descriptions and exception reasons are preserved. UI locale does not change report bytes.

## Guidance and evidence

- [Windows installation and environment requirements](docs/installation.md)
- [0.5.0 publication and verified asset hashes](docs/releases/0.5.0-publication.md)
- [Installer and upgrade design](docs/plans/20261010-installation-upgrade-design.md)
- [CLI and configuration](docs/cli.md)
- [Rules and schema boundaries](docs/rules.md)
- [Discovery and build inputs](docs/build-inputs.md)
- [Changes comparison](docs/changes.md)
- [Report semantics](docs/reports.md)
- [0.4.0 plan](docs/plans/20261009-archsift-0.4.0-improvement-plan.md)
- [0.4.0 contracts and UX](docs/decisions/20261009-0.4-contracts-ux.md)
- [Main implementation record](docs/plans/20261006-archsift-implementation-plan.md)
- [Migration permissions](docs/migration/source-inventory.json) and [third-party notices](docs/migration/third-party-notices.md)
- [Project instructions](AGENTS.md)

Historical 0.1/0.2/0.3 acceptance evidence remains unchanged. NuGet tool distribution, transitive package/version rules, Linux Web UI, fail-on, required CI checks and IFX C/D/E coverage remain deferred. [CI samples](samples/ci) are opt-in reporting examples.
