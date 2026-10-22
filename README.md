# ArchSift Workbench

**0.10.0 release preparation is in progress.** No 0.10.0 artifact is accepted or published yet; see [release status](docs/releases/0.10.0/release-status.json), [release notes](RELEASE_NOTES.md) and the [execution contract](docs/releases/0.10.0-release-contract.md). The live IFX baseline remains `0.10.0-cl03.1`: 493 cases = 92 C# + 401 JSON, representing 404 responsibilities. B01–B05 include Registry4; A179 and five B07 items stay deferred and three A responsibilities remain in C#. See [IFX integration](samples/portable-ifx/README.md). Publishing Workbench does not upgrade IFX.

ArchSift is an independent local .NET architecture and dependency analyzer. ArchSift Workbench combines a loopback Web workspace, CLI, JSON rule library, dedicated architecture-test gate, reviewed build evidence, History and JSON/HTML/SARIF reports. CLI and Web share Core services and contracts; actual type-dependency checks use ArchUnitNET.

Start with [getting started](docs/getting-started.md) for Windows launch and configuration, then [project integration](docs/project-integration.md) for exact package references, offline sources, native C# cases, JSON adapters, case identities and CI. The [generic templates](samples/workbench/README.md) contain example configuration and package/tool declarations without private IFX inputs. Dedicated daily tests and complete source-bound evidence remain separate workflows.

The latest recorded stable release is [v0.8.0](https://github.com/von12549/ArchSift/releases/tag/v0.8.0); [0.9.0-dev](https://github.com/von12549/ArchSift/releases/tag/v0.9.0-dev) is a source-only prerelease. Consult each version's publication/acceptance record and checksums. Existing CL03 packages and local Web candidates are historical fixed artifacts, not stable 0.10.0. New candidates and stable packages require fresh builds and their own exact-byte qualification.

The product targets .NET 10 and analyzes SDK-style C# net8/net9/net10. The planned Windows x64 delivery is self-contained for launch; target builds/tests still need their approved SDKs, packs and dependencies. The portable daily gate currently requires SDK 10.0.303, net10.0 and xUnit v3 4.0.1/Microsoft.Testing.Platform. Linux CLI regression is separate from Windows binary qualification. ArchSift uses the [MIT license](LICENSE), with [third-party notices](docs/migration/third-party-notices.md).

## Development

IFX formally adopted the fixed `0.10.0-cl03.1` portable test combination through CL04; its [actual-main acceptance](https://github.com/von12549/IFX-New/actions/runs/39543191970) is based on `a2adb87`. PR #15 subsequently delivered documentation at historical baseline `f58205b`; see [documentation-delivery acceptance](https://github.com/von12549/IFX-New/actions/runs/39550973000). PR #25 completed policy expansion at `b591b9f`: 493/493, zero skipped and an independent 105/105 audit, recorded in the [completion PR](https://github.com/von12549/IFX-New/pull/25) and [normal actual-main CI](https://github.com/von12549/IFX-New/actions/runs/39733781519). See the [accepted IFX baseline guide](https://github.com/von12549/IFX-New/blob/b591b9f2ffca5d0bbbfb1f9352a56870fadf927c/docs/architecture-portable.md) for current use and boundaries. CL02's responsibility-projection fix is active in IFX through this version. Its matching Web ZIP remains a locally tested candidate, neither consumed by IFX nor publicly released.

ArchSift [PR #9](https://github.com/von12549/ArchSift/pull/9) delivered the minimal 43-file product-source update to main 3f61be0; subsequent documentation PRs completed their declared file lists. Those source deliveries, accepted IFX use and immutable old packages remain historical evidence. They do not establish final 0.10.0 compiler inputs, reproducible builds, general Web installation or new release acceptance. The new release has its own source, package, CI, security/license, integration, lifecycle, independent-machine and independent-human gates.

Development requires .NET SDK 10.0.303 pinned by global.json with roll-forward disabled, PowerShell 7, Git and a complete explicit local NuGet feed. Multi-framework fixtures also require their approved SDKs/targeting packs. Product/tool/runtime closures and actual compiler inputs are recorded separately for each release build.

```powershell
pwsh -NoProfile -File scripts/Invoke-DevelopmentChecks.ps1 -VerifyContracts
node scripts/Test-WorkbenchLifecycle.mjs
```

Checks restore in locked mode from the explicit offline feed (default user NuGet cache), build, and run unit/integration/architecture tests. Missing packages fail; the script does not silently enable network access. Use `-LocalFeed` for another offline feed. `-InitializeLocks` is for an intentional dependency update only.

Every isolated dotnet child pairs `DOTNET_CLI_HOME` with `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`. Checks compare only hashes for User/Machine environment, process PATH and PowerShell Profiles. Logs, real snapshots, packages and reports belong outside source roots, normally under `D:/ArchSift-lab`; development bin/obj are ignored. Real consumer operations require explicit scope and sequential reviewed gates; final independent visual/hands-on review remains user-owned.

## Use and boundaries

The daily IFX entry point is `eng/Test-Architecture.ps1` in the consumer repository, using its approved 251-package offline source and external state. Its three required checks are `IFX Windows architecture`, `IFX dependency audit` and `IFX architecture acceptance`. `test-gate` requires every required case to pass; the standalone analysis CLI exit-code description below does not apply to that gate. The outer Launcher/Workbench remains a separate 0.8.0 installation. The original InProject route was [formally retired](docs/decisions/20261022-inproject-retirement.md); its historical acceptance facts and archives are retained.

CLI entries include `analyze`, `verify`, `chain verify`, `changes`, `rules validate/render/draft`, `test-gate`, `test-history`, evidence commands and `ui --config`. Workbench binds a random loopback port and requires a private session token. Closing a browser tab leaves the service running; use Safe shutdown and wait for terminal return. See [CLI/configuration](docs/cli.md), [architecture tests](docs/architecture-test-workbench.md) and [installation](docs/installation.md).

Rules, configuration, state, reports and evidence use explicit directories outside analyzed targets. Ordinary analysis can return exit 0 with complete conclusive violations; the daily `test-gate` requires complete passing coverage and uses distinct exit codes. Pass, violation, inconclusive, not-applicable, error and cancellation remain distinct. A partial/compliant result or an old successful run never certifies complete current-source protection. Source-bound assemblies require reviewed current evidence.

Projects reference Contracts and Core in one direction: Core owns analysis/services; ArchUnit adapts the pinned engine; CLI/Web compose those services. User-authored rule descriptions and exception reasons are preserved. UI locale does not change report bytes.

## Guidance and evidence

- [Getting started and example configuration](docs/getting-started.md)
- [Project package integration and CI](docs/project-integration.md)
- [Windows installation](docs/installation.md) and [Setup/upgrade/rollback](docs/setup.md)
- [CLI and configuration](docs/cli.md)
- [Rules and schema boundaries](docs/rules.md)
- [Discovery and build inputs](docs/build-inputs.md)
- [Changes comparison](docs/changes.md)
- [Report semantics](docs/reports.md)
- [External build evidence](docs/external-build-evidence.md)
- [0.10.0 release status](docs/releases/0.10.0/release-status.json) and [release notes](RELEASE_NOTES.md)
- [Main implementation record](docs/plans/20261006-archsift-implementation-plan.md)
- [Migration permissions](docs/migration/source-inventory.json) and [third-party notices](docs/migration/third-party-notices.md)
- [Project instructions](AGENTS.md)

Formal repository and release documentation uses English; Chinese remains available for internal communication and the existing UI locale. Historical acceptance remains tied to its original date, source and artifacts. Transitive package/version policy, Linux Web distribution, fail-on behavior and IFX C/D/E coverage remain deferred. Historical opt-in [CI samples](samples/ci) do not replace the separately adopted IFX portable checks or the full-gate integration described above.
