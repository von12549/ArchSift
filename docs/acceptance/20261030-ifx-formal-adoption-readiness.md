# IFX formal adoption readiness review

Review date: 2026-10-30, Australia/Sydney. Decision: **not ready for formal adoption or clone-only public acquisition**. The isolated `0.11.3-rc.1` candidate has passed its reviewed technical acceptance scope and is ready for a separately authorized release/integration preparation package.

The machine-readable companion `20261030-ifx-formal-adoption-readiness.json` is the decision authority. This review does not authorize implementation, activation, tests, commits, pull requests or publication. Existing accepted RC packages, delivery ZIP, source overlay and original evidence remain immutable.

## Scope and evidence

The user requires IFX to carry configuration in Git, use an eligible installed host, acquire exact approved package versions online on first use and upgrades, and generate local state automatically. Optional offline delivery is not a readiness requirement. Installing the required runtime/SDK remains a machine prerequisite.

This review read current source/configuration, package archives, locks, acceptance records and upstream metadata. Twenty-three local input/hash consistency checks passed. Five official NuGet flat-container indexes returned HTTP 200. Read-only `git ls-remote` established current remote heads; no fetch or checkout occurred. Exact-revision GitHub source reads established upstream differences. Initial sandbox DNS failures are retained alongside the successful network probes; they are not package availability findings.

Evidence root: `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-adoption-readiness-20261030`. Its evidence manifest records paths, lengths and SHA-256 values. GitHub text snapshots are normalized text references, not claims of original Git blob bytes.

| Item | Reviewed identity / result |
| --- | --- |
| IFX remote main and clone baseline | `3e330fd9d651b2b43fe4d4e2f27099a141705892` |
| IFX baseline tree | `53c0e42198764fcdbba75771975bdaea0a0f0402` |
| ArchSift repair checkout base | `91cd73adc85a4da8f79fffa04ad834c870d78d07`, with uncommitted repair inputs |
| ArchSift remote main | `68f225172ebd2fafd056587015ec37573652b6b3` |
| Tested package identity | `0.11.3-rc.1`, five local-development packages; not publicly qualified |
| Public versions at review time | `0.11.0`, `0.11.2` for Cli, Contracts, Core, ArchUnit and Testing; neither RC nor stable `0.11.3` appeared |
| Delivered IFX overlay | 54 changes: 16 modifications and 38 additions; 936 baseline files to 974 result files |
| Current candidate vs delivery | No non-document input drift; later documentation requires a new integration manifest before redistribution |
| Product source vs RC packaging manifest | No non-document input drift; documentation has advanced |

Public metadata was read from `https://api.nuget.org/v3-flatcontainer/{package-id}/index.json`; exact URLs and retrieval times are in `nuget-version-probe.json`. Remote heads are point-in-time observations and must be checked again before a later integration or release.

## Accepted technical scope

- Local and new-machine Prepare, Restore, Init, Launcher first start and repeat start passed on the retained evidence. Paths/state were reused and host equality and no activation were recorded. Human page/path and safe-close observations remain operator-supplied evidence.
- New-machine standalone Doctor and Workbench first/repeat start passed. Earlier incompatible SDK 10.0.102 failure and recovery after installing 10.0.303 remain in the history.
- The reviewed IFX N01-N06 scope is complete, with zero pending groups. N04-N06 contain five subcases and 59 passing checks; independent review verified 313 manifest entries and archived 314 files unchanged.
- N01 preserves its checker-correction history. N05 covers the IFX operation lock and recovery, not internal product session locks. N06 uses reconstructed caches and expected pre-dispatch rejection, not a fresh real NuGet restore. These limits do not create new mandatory negative groups.
- Current exact RC pins, package hashes/lengths, internal package dependencies, four library lock entries, approval records and provenance bindings are consistent. This consistency applies to the host candidate; it does not qualify the separate legacy offline architecture source.

No architecture-policy compliance, public-source-only acquisition, final published package qualification or activation is inferred from these passes. Optional offline qualification is skipped by user choice.

## Open adoption blockers

### R01 — Stabilized product source and a qualified public version

The tested repair remains outside ArchSift remote main. Remote main's WorkspaceService and WorkspaceSettings still match the repair's original base, while the upstream hosted-NuGet helper has changed independently. Reconcile against the current remote revision before preparing the release; preserve the upstream release helper fix instead of replacing it with the older checkout copy.

The current release workflow fixes `RELEASE_VERSION` to `0.11.2`; its model admits only `0.11.0` and `0.11.2`. A new version needs a version-specific release profile/workflow and its required evidence. Stable `0.11.3` is a proposed next version only, not assigned or authorized here. Do not overwrite published `0.11.2` or replace immutable RC bytes.

Closure evidence: reviewed final source revision, resolved upstream changes, exact package/version dependencies, release qualification, and all five approved packages available from the intended public source. Publication and associated external changes need separate authorization.

### R02 — IFX Git integration and final public identity binding

IFX remote main still equals the old overlay baseline. The actual local IFX checkout still pins CLI `0.11.0`; the accepted candidate is a separate directory. Its RC provenance requires an external `CandidateSource`. A copied overlay plus five local packages does not satisfy clone-only acquisition.

The existing host entry has a public-source branch, but the active candidate configuration does not select it. Bind the final public package identity across `.config/dotnet-tools.json`, `Directory.Packages.props`, the architecture test project's `packages.lock.json`, `architecture/package-approval.json`, `architecture/archsift-package-provenance.json`, `architecture/archsift-host-contract.json` and `architecture/portable-gate.json`. Review `NuGet.Config`, `.archsift/workspace.json` and the host entry together. Do not weaken exact-version, source or integrity guards.

The public branch expects `sourceKind=nuget.org-public-repository-signed`, verified signatures for all five packages and no CandidateSource. Bind hashes/lengths to the actual served package bytes and library restore content hashes, with signature/owner evidence. Repository signing can change archive bytes; setting a public flag on the local RC provenance is insufficient.

Closure evidence: a reviewed integration diff against the then-current IFX base, complete repository-carried configuration and locks, independently qualified served package identities, and no developer-machine package/state path required for the default entry. Existing untracked user documentation must be preserved. The frozen 54-file delivery is historical input, not a current complete integration manifest after later documentation changes.

### R03 — Existing architecture CI has incompatible package inputs

`.github/workflows/fa05-candidate.yml` runs for main pull requests and pushes, supplies a fixed historical offline source and runtime, and calls `Invoke-Sp04CandidateCi.ps1`. Its architecture stage calls `Test-Architecture.ps1`, which first validates the source through `New-PackageSourceConfig.ps1` and then requires the CLI version from `portable-gate.json`.

The candidate's source manifest lists ArchSift `0.11.2` packages. Its strict 251-package validator rejects missing, changed or extra packages. The gate requires CLI `0.11.3-rc.1`. Therefore a source satisfying that current manifest cannot also supply the required RC CLI under this path. This is a static configuration incompatibility, not an observed CI run failure. The actual remote runner's contents and branch-protection settings were not inspected.

Closure evidence: an explicitly reviewed coexistence design for host startup and architecture CI, consistent approved inputs for each entry and separately authorized validation required by repository policy. Either retain a coherently isolated legacy architecture path or deliberately qualify its upgrade; do not disable checks or silently broaden online restore for the offline gate. Startup acceptance does not require executing architecture tests, but formal integration cannot claim this CI is already qualified.

### R04 — Default documentation still describes historical workflows

The root README mixes `0.10.0`/`0.11.0` architecture/workspace guidance, and `docs/architecture-portable.md` describes historical absolute offline paths/runtime. `.archsift/README.md` retains an obsolete static/mock-only verification statement. Some acceptance table rows retain earlier pending checkpoints despite later closure objects. Preserve historical evidence, but provide an unambiguous current status and default guide.

Closure evidence: English root/default instructions for installed-host startup, prerequisites, exact online acquisition, local state, error handling and upgrades. Label legacy offline/architecture material explicitly. The legacy `archsift-companion.json` selection is not the host-mode active selector and must not be presented as such.

IFX `global.json` requests SDK `10.0.300` with `latestPatch`, not an exact `10.0.303` pin. SDK `10.0.303` is the tested eligible SDK; `10.0.102` is outside that feature band. Document PowerShell 7.4+ x64, an eligible .NET 10 SDK and required Core/ASP.NET runtimes. The product build's exact SDK policy is a separate concern. Do not introduce runtime bundling or automatic host repair.

### R05 — Final public-source-only clone acceptance remains pending

The successful new-machine run used a verified source overlay and external candidate packages. It does not prove a fresh clone can acquire final public bytes without those inputs. Once R01-R04 are closed, validate a fresh clone of the intended revision with empty isolated package/state roots and eligible installed prerequisites. Supply no overlay, CandidateSource, copied cache or machine-local configuration.

The future default acceptance target is `git clone` followed by `.\archsift.cmd`; this is a target for the final integrated revision, not a claim that the current public branch is ready. Verify exact public package acquisition, generated external local state, Init, Launcher/Workbench paths, Doctor, safe close and repeat reuse; retain receipts and host equality. Ordinary startup must leave the daily architecture configuration absent and must not execute architecture tests.

Final package/version changes require targeted validation against the final bytes and affected startup/configuration code. Existing N01-N06 remain accepted; do not repeat the entire matrix without a changed input or unresolved concern that justifies it.

## Migration and activation proposal

A fresh clone needs no migration of the previous `0.11.0` workspace. Checkout, version and input identity isolate new state. Default host storage is under `%LOCALAPPDATA%\IFX\ArchSiftHost`; generated state is not committed.

For an existing machine's later cutover, explicitly choose the new revision/version, initialize a separate state identity, verify its project/configuration paths and safe shutdown, then decide whether to activate it. Preserve the old checkout/configuration/cache and receipts for an operator-controlled rollback. Do not copy live locks, journals or initialization markers across identities and do not add silent fallback. Activation is a separate decision and is not a prerequisite for fresh-clone usability.

## Recommended next work package

Prepare the final product release and IFX integration diff: reconcile current ArchSift main, propose the new version and release profile, design the architecture-CI coexistence fix, and refresh the IFX default documentation/configuration templates. This package can prepare reviewable changes before publication; final public provenance and clone acceptance depend on actually available final packages. Any build/test execution, commit, PR, publication or active switch requires the applicable scoped authorization.

This review performed only reads, hashes, metadata queries and documentation/evidence writes. It did not run .NET, restore packages, start the product, execute real integration or architecture tests, change active configuration, commit or publish.
