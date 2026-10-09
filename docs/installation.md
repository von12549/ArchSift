# Windows installation and configuration — 0.5.0

**0.5.0 is published and independently downloaded/verified.** It adds CLI UI output/lifetime repair and a separate portable setup with install/adopt/upgrade/rollback/recovery. Obtain the four official assets from the [v0.5.0 Release](https://github.com/von12549/ArchSift/releases/tag/v0.5.0), verify them against the [publication record](releases/0.5.0-publication.md), then follow [setup commands and transaction boundaries](setup.md). The [0.4.0 manual guide](installation-0.4.md) preserves its original checksums and launcher workaround.

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

Obtain version-specific ZIP, separate ArchSift.Setup.exe, SHA256SUMS.txt and acceptance.json. Compare every asset's length/hash with that release's publication record before execution. Never reuse 0.4.0 hashes for 0.5.0. The immutable manifest's candidate status records packaging time; final acceptance binds exact ZIP/Setup identities. Hash agreement is integrity evidence, not an independent publisher signature.

The installation root holds versions/<version>, config/project.json, rules, reports, downloads, operations/<id> and install.json. Config/library may be external. Payload contains archsift.exe, package-manifest.json, setup-compatibility.json, web/ArchSift.Web.exe, schemas/templates/docs and licenses. Preserve the complete package. The separate single-file Setup embeds licenses available through --notices.

Planning displays generated JSON, normalized paths and identities before apply. First-install defaults: existing, explicit TFM, Debug, allowNetwork:false, empty rulesets and JSON/HTML/SARIF. Existing config requires explicit adoption and is never overwritten. Installation does not select policy or prove source compliance. See [setup workflow](setup.md).

Manual ZIP extraction into an empty version directory remains supported. Create config using [CLI configuration](cli.md), retaining config/library/reports outside target. Upgrade preserves config and the entire library, including entry IDs, deleted-entry tombstones and chain references. Export/import is not an upgrade strategy.

## UI startup and shutdown

Use the selected binary's full path with ui --config and the saved config. CLI forwards stdout/stderr and displays ARCHSIFT_UI=, ARCHSIFT_PID= and waiting state. A private stdin pipe binds Web to its parent. Startup timeout is 30 seconds. Ctrl+C requests shutdown, waits up to 10 seconds, then stops only the owned tree if needed. Parent exit closes the pipe and stops Web. Web cancels and drains current jobs for up to 8 seconds before its owned-tree fallback, retaining completed cancellation evidence when cleanup succeeds. The 0.5.0 package and real IFX terminal lifecycle passed acceptance; historical 0.4.0 evidence is unchanged.

Open the complete URL locally; never publish its session fragment. Confirm target/entry/TFM/configuration/build/output/library before actions. Installation-only checks perform no discovery, verification, chains, comparison, drafts, imports or build. Browser library listing can create internal empty registry/lock metadata; preserve existing identity/tombstones.

Use Safe shutdown and wait for terminal return. Closing the browser alone leaves service running. If a child remains, verify current PID, full executable path and listener before precise cleanup. Never reuse old PIDs or terminate unrelated services. The [0.4.0 workaround](installation-0.4.md#known-040-cli-ui-launch-issue) applies only to that older version.

## Upgrade and safety

See [setup](setup.md) for consistent backup, immutable versions, atomic selection, receipt rollback and journal recovery. Unknown schemas/migrations reject. Later user edits cause rollback rejection and remain intact. Old binaries/config/rules/chains/reports are never automatically deleted.

Never change PATH at any scope, User/Machine environment, registry environment or PowerShell startup files. Child-only DOTNET_CLI_HOME is paired with DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0. Host drift stops work and preserves evidence; no automatic repair. Real IFX remains operator-driven, one reviewed block at a time. Installation/upgrade does not establish source compliance or adopt candidate policy.
