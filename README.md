# ArchSift Workbench

The unpublished **0.11.0 project-workspace candidate** adds one `.archsift/` entry, package-owned initialization and standard NuGet source/cache integration. See the [workspace guide](docs/guides/project-workspace.md), [revision-2 plan](docs/plans/20261024-archsift-unified-package-initialization-plan.md) and [development checkpoint](docs/acceptance/20261024-unified-workspace-development.md). Full CI/adoption and independent human acceptance remain pending; the existing Setup compatibility boundary is under explicit scope review.

**ArchSift Workbench 0.10.1 is released and download-verified.** [Download v0.10.1](https://github.com/von12549/ArchSift/releases/tag/v0.10.1), read the [publication record](docs/releases/0.10.1-publication.md), [release notes](RELEASE_NOTES.md) and [portable usage](docs/portable-workbench.md). The user accepted NuGet UI, Portable Launcher and Managed Launcher at 4/5, with no defects or untested mandatory items. All 11 public attachments, both tagged source archives and cold downloaded usage passed verification.

The previous **ArchSift Workbench 0.10.0** remains available. [Download the stable release](https://github.com/von12549/ArchSift/releases/tag/v0.10.0), then read the [publication and verification record](docs/releases/0.10.0-publication.md), [release notes](RELEASE_NOTES.md) and [getting started](docs/getting-started.md). The user accepted the final payload with usability 4/5; all 11 public attachments, tagged source archives and documented cold usage passed verification. IFX now uses stable 0.10.0 through both its daily gate and independent Workbench; SDK 10.0.303/runtime 10.0.13 machine qualification passed. Its independent human acceptance passed (4/5, no defects or untested items); see [IFX adoption and rollback](docs/acceptance/20261023-ifx-stable-upgrade.md) and [IFX integration](samples/portable-ifx/README.md).

ArchSift is an independent local .NET architecture and dependency analyzer. ArchSift Workbench combines a loopback Web workspace, CLI, JSON rule library, dedicated architecture-test gate, reviewed build evidence, History and JSON/HTML/SARIF reports. CLI and Web share Core services and contracts; actual type-dependency checks use ArchUnitNET.

Start with [getting started](docs/getting-started.md) for Windows launch and configuration, then [project integration](docs/project-integration.md) for exact package references, offline sources, native C# cases, JSON adapters, case identities and CI. The [generic templates](samples/workbench/README.md) contain example configuration and package/tool declarations without private IFX inputs. Dedicated daily tests and complete source-bound evidence remain separate workflows.

The latest stable release is [v0.10.1](https://github.com/von12549/ArchSift/releases/tag/v0.10.1). The accepted [v0.10.0](https://github.com/von12549/ArchSift/releases/tag/v0.10.0) and its existing IFX adoption remain unchanged. Earlier [v0.8.0](https://github.com/von12549/ArchSift/releases/tag/v0.8.0) and source-only [0.9.0-dev](https://github.com/von12549/ArchSift/releases/tag/v0.9.0-dev) remain historical. Consult each version's acceptance record and checksums; existing CL03 and release-candidate artifacts keep their original identities.

The product targets .NET 10 and analyzes SDK-style C# net8/net9/net10. The Windows x64 delivery is self-contained for launch; target builds/tests still need their approved SDKs, packs and dependencies. The portable daily gate currently requires SDK 10.0.303, net10.0 and xUnit v3 4.0.1/Microsoft.Testing.Platform. Linux CLI regression is separate from Windows binary qualification. ArchSift uses the [MIT license](LICENSE), with [third-party notices](docs/migration/third-party-notices.md).

## Development

IFX [PR #27](https://github.com/von12549/IFX-New/pull/27) adopted released 0.10.0 at `30cca604`, preserving 493/493 cases, 404 responsibility sources and existing policy boundaries. Its [actual-main CI](https://github.com/von12549/IFX-New/actions/runs/39889184641), independent 105-project/CLI audit, package and Workbench rollback rehearsals, live CLI/Web parity and safe lifecycle passed. Both outer entries use the current IFX profile and child-only runtime 10.0.13. [Machine acceptance](docs/acceptance/20261023-ifx-stable-upgrade.md) and the [completed human checklist](docs/acceptance/20261023-ifx-stable-upgrade-human-checklist.md) distinguish machine qualification from user acceptance. Earlier CL04/cl03.1 and policy-activation PR #25 remain historical baselines with their original packages/evidence.

ArchSift [PR #9](https://github.com/von12549/ArchSift/pull/9) delivered the minimal 43-file product-source update to main 3f61be0; subsequent documentation PRs completed their declared file lists. Those source deliveries, accepted IFX use and immutable old packages remain historical evidence. They do not establish final 0.10.0 compiler inputs, reproducible builds, general Web installation or new release acceptance. The new release has its own source, package, CI, security/license, integration, lifecycle, independent-machine and independent-human gates.

Development requires .NET SDK 10.0.303 pinned by global.json with roll-forward disabled, PowerShell 7, Git and a complete explicit local NuGet feed. Multi-framework fixtures also require their approved SDKs/targeting packs. Product/tool/runtime closures and actual compiler inputs are recorded separately for each release build.

```powershell
pwsh -NoProfile -File scripts/Invoke-DevelopmentChecks.ps1 -VerifyContracts
node scripts/Test-WorkbenchLifecycle.mjs
```

Checks restore in locked mode from the explicit offline feed (default user NuGet cache), build, and run unit/integration/architecture tests. Missing packages fail; the script does not silently enable network access. Use `-LocalFeed` for another offline feed. `-InitializeLocks` is for an intentional dependency update only.

Every isolated dotnet child pairs `DOTNET_CLI_HOME` with `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0`. Checks compare only hashes for User/Machine environment, process PATH and PowerShell Profiles. Logs, real snapshots, packages and reports belong outside source roots, normally under `D:/ArchSift-lab`; development bin/obj are ignored. Real consumer operations require explicit scope and sequential reviewed gates; final independent visual/hands-on review remains user-owned.

## Use and boundaries

The daily IFX entry point is `eng/Test-Architecture.ps1` in the consumer repository, using its approved 251-package offline source and external state. Its three required checks are `IFX Windows architecture`, `IFX dependency audit` and `IFX architecture acceptance`. `test-gate` requires every required case to pass; the standalone analysis CLI exit-code description below does not apply to that gate. The independent managed Launcher/Workbench is selected at 0.10.0; old 0.4–0.8 versions, original configuration and historical profiles remain retained. The original InProject route was [formally retired](docs/decisions/20261022-inproject-retirement.md); its historical acceptance facts and archives are retained.

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

IFX 0.10.0 human acceptance is complete. The next iteration records [NuGet Web delivery and collapsible JSON inventory](docs/plans/20261023-workbench-next-improvements.md). The current CLI nupkg has no Web payload and still needs a separately supplied Web component for `ui`/Launcher; this does not affect CLI architecture-gate use. These improvements are not part of the accepted 0.10.0 payload.
