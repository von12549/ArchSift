# Project-local package workspace

Version: 0.11.0 candidate. This guide describes the new NuGet workspace route. Candidate packages require the reviewed candidate source; they have not been publicly published or independently accepted by the user. Existing published 0.10.1 bytes remain unchanged.

## Project files

Keep `start.ps1`, `workspace.json` and this project's usage notes under `.archsift/`. The CLI package contains the reviewed project template at `tools/net10.0/any/templates/project/.archsift/`; copy `start.ps1.template` as `start.ps1` when adopting the template. The same template is in the product source at `templates/project/.archsift/`. These are project integration files. NuGet retains executable ownership; no separate Web installation is created.

Keep the pinned local-tool manifest at `.config/dotnet-tools.json`, standard NuGet configuration at its normal location, and any existing SDK selection in `global.json`. Preserve unrelated local tools. Pin `archsift.cli` to the exact reviewed package version with `commands: ["archsift"]` and `rollForward: false`; the manifest must have `isRoot: true`. Do not move these standard files into `.archsift/`.

The shared `workspace.json` uses `kind: "archsift-workspace"` and `schemaVersion: 1`. Select a stable project ID, project root relative to this file (default `..`), and optional project-root-relative solution entry. Paths in workspace and local-settings files resolve relative to the workspace configuration directory. State defaults to `C:/ArchSift-lab`, with separate user/project partitions. Existing nonempty unknown state, another root binding or an unsupported schema fails without overwriting it.

Run from the project root:

```powershell
pwsh -NoProfile -File ./.archsift/start.ps1 -Action Init
pwsh -NoProfile -File ./.archsift/start.ps1
pwsh -NoProfile -File ./.archsift/start.ps1 -Mode Workbench
pwsh -NoProfile -File ./.archsift/start.ps1 -Action Doctor
```

An absolute script path works from another current directory. The entry deliberately uses the resolved project root for SDK, tool-manifest and standard NuGet resolution. PowerShell 7.4 or later is required. A generic host needs stable SDK >=10.0.0 and compatible .NET 10 Core and ASP.NET shared frameworks; a newer SDK by itself is insufficient. Explicit `runtime.dotnetPath` is authoritative. Otherwise discovery consults existing `DOTNET_HOST_PATH`, `DOTNET_ROOT_X64`, `DOTNET_ROOT` and PATH. Invalid implicit hints may be skipped; an invalid explicit host is an error. Existing `global.json` remains authoritative. The entry does not install an SDK or change persisted environment settings.

## Sources, cache and local paths

For ordinary NuGet network restore, select `nuget.mode: "configured"` and `allowNetwork: true`. Leave `configFile` null for standard NuGet configuration inheritance, or select an explicit standard configuration file. NuGet handles sources, source mapping and credentials. An unavailable exact package version fails without switching versions or silently enabling another source. Use `-InteractiveRestore` only when explicit interactive provider authentication is needed; Doctor does not restore or prompt.

Cache precedence is explicit local/shared `nuget.cacheDirectory`, existing `NUGET_PACKAGES`, then standard NuGet `globalPackagesFolder`/default cache. Relative standard cache settings retain their NuGet configuration base. No custom network-downloader cache replaces NuGet's cache. Removing an owned package cache requires a new restore and does not remove state.

For offline restore, select `mode: "offline"`, `allowNetwork: false`, and an explicit standard local-only NuGet.Config. Clear inherited sources and do not add network audit sources or fallback folders. The entry rejects network/dynamic sources before restore and isolates HTTP/plugin caches in its external bootstrap diagnostics directory. Offline package restore makes no audit-data request. IFX's separately controlled official dependency audit remains a separate operation.

The optional ignored `.archsift/workspace.local.json` has kind `archsift-workspace-local`, schema 1 and only these allowed overrides: `state.root`, `runtime.dotnetPath`, `nuget.configFile`, `nuget.cacheDirectory`. `-LocalConfig` can select an explicit external file. An explicitly selected missing/empty path is an error. Do not put credentials in workspace files; use standard NuGet configuration/providers. Local settings cannot change network permissions, runtime minimums or IFX policy. See the [configuration examples](../plans/20261024-archsift-unified-package-initialization-examples/README.md).

## State and lifecycle

Initialization creates external `config`, `library`, `reports`, `runs`, `launches`, `backups` and portable `launcher` directories. Startup configuration is schema 1 and remains outside the Launcher root. Existing user edits are retained; simultaneous project-generated and external configuration edits require reconciliation. Repeated initialization is idempotent. A per-project lease prevents concurrent initialization, migration or UI ownership; Doctor reports a busy lease without repair. A verified interrupted transaction can resume with the same inputs. Unknown files remain preserved.

The package verifies matching embedded Web bytes before initialization, diagnostics or startup. Missing, altered, extra or mixed payloads fail. Safe shutdown retains dirty-state protection and closes the owned child UI. Closing a browser alone does not close the service. If the project entry dies, its owned Launcher/Workbench children terminate. Keep external initialization/closeout evidence; readiness authentication tokens are transient console output.

The initializer accepts an explicit `--migration`/`-Migration` manifest with kind `archsift-workspace-migration`, schema 1, matching absolute `targetRoot`, and `files` entries containing absolute `source`, canonical relative `destination` and SHA-256 `sha256`. Destinations are restricted to owned `library/` or inactive `backups/imports/`. Sources and existing destinations are hash-checked before transfer; existing different bytes are never overwritten. Preserve old startup/profile/catalog/history bytes as inactive backups and explicitly reconcile their current workspace configuration; historical success is not current acceptance. The initializer does not guess executable paths or reactivate an old installation.

## IFX qualification and rollback

IFX retains its exact SDK 10.0.303/Core+ASP.NET+host 10.0.13 approval, complete approved offline feed, 493-case policy and original architecture test locations. Shared gate/audit helpers remain in `eng/`; the dedicated old UI startup helper is retired through the reviewed migration. Startup only loads configuration/inventory and does not build the live target. Full qualification runs sequentially on explicitly owned external source snapshots.

The immediate rollback is the complete preceding package-only 0.10.1 source/tool/lock/approval/feed/runtime/config/state/entry combination. Retain verified backups and restoration copies, preserve unrelated later edits, and use an inverse PR with fresh required CI after merge. Do not recursively remove `.archsift/` or read new-version history with old code without a demonstrated compatible contract. A host environment/Profile drift stops all further tool execution and requires human review; no automatic repair is performed. Independent final visual/hands-on acceptance remains with the user before public publication.
