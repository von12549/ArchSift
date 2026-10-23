# Proposed .archsift configuration examples

Date: 2026-10-24. Status: **PLANNING TEMPLATES / NOT IMPLEMENTED / NOT LIVE CONFIGURATION**.

These files accompany [planning revision 2](../20261024-archsift-unified-package-initialization-plan.md). The new workspace/local kinds, fields and package commands are proposed contracts. They must not be passed to the current 0.10.1 CLI or its startup/daily-test inputs. This revision creates documentation examples only; it does not create a live `.archsift/` directory, execute an entry or move target files.

## Intended deployment

| Example | Intended future location | Purpose |
| --- | --- | --- |
| `workspace.network.example.json` | `<project>/.archsift/workspace.json` | Generic project using standard configured NuGet sources, including permitted network sources |
| `workspace.offline.example.json` | `<project>/.archsift/workspace.json` | Alternative generic offline configuration with an explicit external standard NuGet config |
| `workspace.ifx.example.json` | `<IFX-root>/.archsift/workspace.json` | IFX strict approval/gate references at their existing paths |
| `workspace.local.example.json` | `<project>/.archsift/workspace.local.json` | Optional ignored generic host paths; it does not choose rules or relax runtime/source policy |
| `workspace.ifx.local.example.json` | `<IFX-root>/.archsift/workspace.local.json` | Alternative ignored IFX machine paths for a separately prepared candidate deployment |
| `NuGet.offline.example.config` | An explicitly selected file outside the repository, such as the `nuget.configFile` in the local example | Standard NuGet XML for a reviewed local-only source; not an extra live root NuGet.Config |

Choose exactly one workspace example, then optionally one matching local example. Do not load all examples as layers. Copying/editing/adopting them is future implementation/migration work. `start.ps1` and a version-matched README will be delivered by the product template; there is no executable starter in this documentation-only example set.

Standard `.config/dotnet-tools.json`, root `NuGet.Config`, `global.json`, IFX `architecture/` and `tests/Architecture/` retain their original paths. The shared tool manifest remains the sole authority for the exact ArchSift package version; preserve dotnet-ef and other tools. Existing rule/policy bytes and test semantics remain protected. Only independently reviewed product-version/hash metadata changes may be necessary at their original paths.

## Path and override rules

Workspace file references and `project.root` resolve relative to the deployed `.archsift/workspace.json`, not this examples directory and not the terminal working directory. `project.root=..` selects the project root; `project.entry` then resolves relative to that root. Consequently the IFX references `../architecture/runtime-approval.json`, `../architecture/package-source.json` and `../architecture/portable-gate.json` resolve to the unchanged files in the project. Configured NuGet file contents follow standard NuGet path rules.

The local configuration has proposed `kind=archsift-workspace-local`, `schemaVersion=1`. Its allowed payload fields are only `state.root`, `runtime.dotnetPath`, `nuget.cacheDirectory` and `nuget.configFile`. Metadata fields identify the local contract; they are not policy overrides. A selected NuGet config still has to satisfy offline mode, permitted sources and independent package/source approvals. Reject local changes to `allowNetwork`, runtime mode, SDK floor, approvals, target identity or gate settings. The future ignore rule is exactly `/.archsift/workspace.local.json`; keep the entry, workspace configuration and README versioned.

Example absolute paths describe intended deployment locations and are not assertions that those inputs exist. The IFX local example points to the existing isolated host as a candidate to revalidate, a **new** candidate cache/state and a **new external** NuGet config. It does not reuse active 0.10.1 state. The external NuGet XML uses a sample local feed path that must be replaced with the exact qualified feed; prepare the file before bootstrap, then validate it against the source approval. The current root IFX NuGet.Config has no sources; the explicit local-file selection avoids modifying it. No example supplies missing packages or establishes that the future version is available on a public registry.

The initial `project.id` in each example must be adopted as a unique stable ID with canonical-root binding. Generic `Example.sln` must be replaced by the consumer's actual entry. State defaults to `C:/ArchSift-lab` when omitted; the examples show it explicitly. Owned state is partitioned under the configured base by user/project. Package/cache/runtime roots remain separate from the actual project-state partition. Permission errors do not trigger elevation or a silent alternate directory.

The generic configuration intentionally has no selected rules and uses existing-build mode. IFX's startup analysis selection also remains empty here; the `gate` reference retains its separate approved daily-test selection. Initializing or opening a UI does not build a target or enable IFX rules in a generic project. Generated schema-1 startup files and schema-2 daily-test files belong in external state, not in `.archsift/`.

## Future validation checklist

1. Parse the workspace and local contracts independently and reject unexpected/forbidden fields.
2. Resolve examples at their intended deployment paths, including `..`, `../architecture`, the solution entry and alternate terminal working directories.
3. Verify standard root manifest/NuGet/global.json discovery stays unchanged after moving the entry; no hidden `.archsift/.config/dotnet-tools.json` or copied NuGet/global.json is created.
4. Verify the IFX offline config's real source contents against the exact approved candidate feed and hash manifest; observe zero package HTTP requests. Generic configured mode must use standard NuGet behavior.
5. Verify the local file is ignored while the entry/workspace/README are tracked. No cache, runtime, extracted Web, generated config, state or evidence is written to `.archsift/`.
6. Exercise the new entry with old outer/eng starters absent, then restore the recorded old entry/config/reference combination on copies. Preserve unknown/later `.archsift/` files and the local file during rollback; never recursively remove the directory.

These are proposed acceptance checks, all **NOT RUN** against a new implementation. Documentation verification only parses JSON/XML and checks internal references and protected live-file preservation.
