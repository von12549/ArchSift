# ArchSift 0.11.2 NuGet packages

ArchSift provides .NET architecture analysis, an architecture-test gate and a loopback Web Workbench. This version distributes four net10.0 libraries and the `ArchSift.Cli` local tool. The CLI package contains its matching framework-dependent Web payload, workspace schemas and `.archsift/` project template. No separate Web installation, new Setup executable or self-contained Windows ZIP is part of 0.11.2.

Publication and acceptance are recorded separately for the exact final artifacts. A locally qualified package is not proof that version 0.11.2 is available on nuget.org. Consult the release's publication.json and checksums. The previous 0.10.1 installer/assets remain unchanged.

## First use

Use the [complete NuGet adoption recipe](https://github.com/von12549/ArchSift/blob/main/docs/guides/nuget-first-use.md). It verifies the downloaded CLI package, copies the package-owned entry template into `.archsift/`, creates the exact root tool manifest and standard NuGet configuration in a new project, then runs the project entry. No global tool or external startup script is required. `workspace init` initializes external state from existing workspace files; it does not scaffold missing project files.

The project-root `.config/dotnet-tools.json` pins `archsift.cli` to 0.11.2, command `archsift`, with rollForward=false and isRoot=true. Standard NuGet.Config/global.json and existing policies/tests keep their normal locations. Merge into existing projects deliberately, preserving unrelated tools/configuration. A local tool is not a PackageReference that launches a UI during builds.

After adopting the verified template, run from the project root:

```powershell
pwsh -NoProfile -File ./.archsift/start.ps1 -Action Init
pwsh -NoProfile -File ./.archsift/start.ps1 -Action Doctor
pwsh -NoProfile -File ./.archsift/start.ps1
pwsh -NoProfile -File ./.archsift/start.ps1 -Mode Workbench
```

Supply an absolute entry path when calling from another directory. The default opens Launcher, which owns its Workbench. Use Safe shutdown and wait for terminal return; browser closure alone leaves the service running. Startup never builds the target or executes its application. Do not persist session tokens.

## Host, source and state

The entry/UI route supports Windows x64 and PowerShell 7.4+. Generic use requires a stable SDK >=10.0.0 (actual SDK feature bands include 10.0.100), the project's global.json and compatible .NET 10 Core/ASP.NET shared frameworks. A newer SDK alone is insufficient. The minimum-host fixture used SDK 10.0.100/Core+ASP.NET 10.0.0; this is a compatibility floor, not a recommendation to retain an old security patch. Use a supported current .NET 10 patch. CLI-only analysis has its own Core-runtime prerequisite. Linux CLI regression does not establish Linux project-entry/Web support.

Host discovery reads existing DOTNET_HOST_PATH, DOTNET_ROOT_X64, DOTNET_ROOT and PATH; runtime.dotnetPath is an optional authoritative override. No SDK installation, major runtime roll-forward, elevation or persisted environment/Profile/PATH change is performed. Isolated child CLI homes disable global-tool PATH registration.

The public network source is https://api.nuget.org/v3/index.json. Configured network mode requires allowNetwork=true and honors normal NuGet source mapping, authentication and cache selection. Standard configuration inheritance is supported; an explicit NuGet.Config may instead select approved sources. Source lists are not ordered fallback lists. Missing pinned packages fail without changing version/source. Use an explicitly local-only configuration and allowNetwork=false for offline mode; the release offline bundle supplies the documented generic closure, not private IFX inputs.

Editable state defaults to the per-user LocalApplicationData/ArchSift/state directory with separate user/project partitions; initialization creates it when absent. A workspace/local setting may choose another writable external root; cache/package/runtime directories stay separate. State initialization is idempotent and preserves existing user edits; unknown/busy/root-mismatched state fails without automatic repair. The workspace document, schema-1 startup config and schema-2 daily-test config are distinct. A project policy file can supply hash-verified rulesets automatically at startup; otherwise explicit workbench.rulesets remain supported. Load the daily-test path printed by the current invocation into Architecture tests.

The optional ignored workspace.local.json permits only state.root, runtime.dotnetPath, nuget.cacheDirectory and nuget.configFile. Do not store credentials or weaken policy there. See the [workspace guide](https://github.com/von12549/ArchSift/blob/main/docs/guides/project-workspace.md).

## Libraries, recovery and evidence

Reference ArchSift.Contracts, ArchSift.Core, ArchSift.ArchUnit and ArchSift.Testing through normal PackageReference using one exact release. ArchUnit depends on the pinned ArchUnitNET engine; the tool embeds its own complete executable closure. Rules, reports and editable state remain outside analyzed source. JSON reports are authoritative; partial coverage or historical success is not current full protection. Standalone analysis exit semantics and the complete architecture-test gate are distinct.

Preserve prior manifests/locks/package identities/configuration and compatible state before upgrading. Rollback restores that complete recorded combination and retains later edits and new-version evidence; do not recursively delete .archsift/, clear shared caches or reactivate an old managed installation automatically. Nuget.org unlisting hides discovery but does not revoke exact-version restores. Published package bytes are never replaced under the same ID/version.

LICENSE contains ArchSift's existing MIT terms. Preserve all included third-party licenses/notices. The final release binds source commit, submitted/served package hashes, repository-signature verification, machine qualification and separate human acceptance. IFX retains its independently approved offline source, exact SDK/runtime, policies and gates; these are not general consumer prerequisites.
