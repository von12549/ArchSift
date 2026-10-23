# Windows installation and configuration — ArchSift Workbench 0.10.0

Current-source update: 0.10.1 is a candidate under qualification; final human acceptance and publication are pending. See [portable Workbench](portable-workbench.md) for NuGet UI, portable Launcher and rollback. References to published 0.10.0 below retain their historical context.
Release status: **0.10.0 published and download-verified**. See the [publication record](releases/0.10.0-publication.md) for exact assets, final acceptance and successful public-download usage. The following instructions retain their packaging-stage context; immutable bundled copies are qualified by the matching final acceptance record.

These instructions were prepared for 0.10.0 qualification. Exact assets, support and final acceptance are established by the separate matching release record; source [release status](releases/0.10.0/release-status.json) retains its packaging-stage timing. These instructions describe the intended verified Windows delivery and retain the established profile/ownership model. They do not claim new package qualification. Start with [getting started](getting-started.md) and [project integration](project-integration.md). Earlier [0.5.0 installation](installation-0.5.md), [Setup](setup-0.5.md) and [0.4.0 manual](installation-0.4.md) guides remain historical.

## Requirements

| Operation | Requirements |
| --- | --- |
| Run Windows Setup, CLI or UI | Windows x64, verified complete self-contained assets and execution/write permissions; no SDK/runtime, Guard or global tools. |
| Follow commands | PowerShell 7 with -NoProfile and full executable paths; no PATH/Profile changes. |
| Open UI | Local JavaScript browser, 127.0.0.1, random port and private session token. |
| Store config/library/reports | Writable fixed local directories disjoint from target, without links/reparse points. |
| Declaration analysis | SDK-style C# csproj/sln/slnx, net8/net9/net10 declarations; no automatic build/restore. |
| Git comparison | Local Git and requested local revisions; no fetch. |
| Isolated build | Separate authorization, compatible SDK/targeting packs, explicit sources; offline by default. |
| Develop ArchSift | .NET 10 SDK, PowerShell 7, Git and local NuGet feed; see README. |

Windows ARM64 and minimum Windows release are not established support guarantees. Linux has source CLI regression, not a self-contained distribution/Web claim. Setup uses user-writable fixed local volumes without elevation. MSI/services/PATH/shell integration and SDK installation are deferred.

## Package and configuration

Obtain the version-specific Workbench ZIP, separate ArchSift.Setup.exe, SHA256SUMS.txt, artifact-manifest.json and acceptance.json. Compare every asset's length/hash with that release's publication record before execution. Never reuse an older version's hashes. The immutable manifest records packaging-time candidate state; final acceptance binds exact ZIP/Setup identities. Hash agreement proves byte integrity, not an independent publisher signature.

Managed installations use config/default.json; existing installations retain their registered configuration filename. The root holds versions/<version>, config, rules, reports, downloads, operations/<id> and install.json. Preserve the complete payload and licenses. Setup protects one registered ConfigPath/library state. Launcher config/profiles.json and config/selection.json are retained separately and excluded from receipt backup/rollback state. Another file under config is not automatically enrolled. External configurations receive no Setup protection; legacy managed paths outside config require separate relocation review before upgrade.

## Profile launcher

Use the selected version's verified executable by absolute path: `archsift.exe launch --root <install-root>`. The launcher opens a loopback selection page independently of the target JSON. Register an existing file by its full path, or review normalized JSON before creating an unused file inside config. It lists names, paths, targets, output/library directories, health and resource protection. The default/recent item is only preselected; click Open selected workbench explicitly. No discovery/build/restore happens on selection.

The launcher owns the workbench process and stays alive. Reopen workbench reuses its private session; browser-tab closure does not stop it. Finish/cancel jobs and save/export or explicitly discard edits before closing and selecting a different profile. Config edits in the workbench remain in memory unless exported; they are not automatically written into the original file. Session URLs/tokens are not saved in metadata or logs. Keep `ui --config <JSON>` for explicit direct sessions, whose ownership/protection is not inferred from filenames. Never upgrade while the launcher or workbench is active.

Planning displays generated JSON, normalized paths and identities before apply. First-install defaults: existing, explicit TFM, Debug, allowNetwork:false, empty rulesets and JSON/HTML/SARIF. Existing config requires explicit adoption and is never overwritten. Installation does not select policy or prove source compliance. See [setup workflow](setup.md).

Manual ZIP extraction into an empty version directory remains supported. Create config using [CLI configuration](cli.md), retaining config/library/reports outside target. Upgrade preserves config and the entire library, including entry IDs, deleted-entry tombstones and chain references. Export/import is not an upgrade strategy.

## UI startup and shutdown

Use the selected binary's full path with ui --config and the saved config. CLI forwards stdout/stderr and displays ARCHSIFT_UI=, ARCHSIFT_PID= and waiting state. A private stdin pipe binds Web to its parent. Startup timeout is 30 seconds. Ctrl+C requests shutdown, waits up to 10 seconds, then stops only the owned tree if needed. Parent exit closes the pipe and stops Web. Web cancels and drains current jobs for up to 8 seconds before its owned-tree fallback, retaining completed cancellation evidence when cleanup succeeds. The 0.5.0 package and real IFX terminal lifecycle passed acceptance; historical 0.4.0 evidence is unchanged.

Open the complete URL locally; never publish its session fragment. Confirm target/entry/TFM/configuration/build/output/library before actions. Installation-only checks perform no discovery, verification, chains, comparison, drafts, imports or build. Browser library listing can create internal empty registry/lock metadata; preserve existing identity/tombstones.

Use Safe shutdown and wait for terminal return. Closing the browser alone leaves service running. If a child remains, verify current PID, full executable path and listener before precise cleanup. Never reuse old PIDs or terminate unrelated services. The [0.4.0 workaround](installation-0.4.md#known-040-cli-ui-launch-issue) applies only to that older version.

## Upgrade and safety

See [setup](setup.md) for consistent backup, immutable versions, atomic selection, receipt rollback and journal recovery. Unknown schemas/migrations reject. Later user edits cause rollback rejection and remain intact. Old binaries/config/rules/chains/reports are never automatically deleted.

Never change PATH at any scope, User/Machine environment, registry environment or PowerShell startup files. Child-only DOTNET_CLI_HOME is paired with DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0. Host drift stops work and preserves evidence; no automatic repair. Real IFX remains operator-driven, one reviewed block at a time. Installation/upgrade does not establish source compliance or adopt candidate policy.
