# Windows installation and configuration

This guide covers the published ArchSift 0.4.0 Windows x64 package: download verification, a versioned local installation, configuration, and the first UI session. Ruleset adoption and analysis are separate steps. The installation and upgrade helper is [a design proposal](plans/20261010-installation-upgrade-design.md), not a command shipped in 0.4.0.

## Environment requirements

| Operation | Requirements | Boundary |
| --- | --- | --- |
| Run the Windows release CLI or UI | Windows x64, the complete `win-x64` ZIP, permission to execute its native programs | Self-contained; no separately installed .NET runtime, SDK, Guard, or global tool registration is needed. |
| Follow the manual commands | PowerShell 7 opened with `-NoProfile` | Do not initialize Conda or edit a Profile for this installation. Use full executable paths. |
| Open the UI | A local browser with JavaScript enabled and access to `127.0.0.1` | Random local port; session token required; no remote hosting. |
| Store configuration and library data | Writable local directories outside the target source root | Output and target must be disjoint; links/reparse points are rejected. Preserve existing data. |
| Obtain release assets | Browser access to the specific GitHub Release | Runtime UI startup and declaration checks do not require package restore or remote services. |
| Discover declared projects later | SDK-style C# `.csproj`, `.sln`, or `.slnx`; supported `net8.0`, `net9.0`, or `net10.0` declarations | Reads declarations without building the target. Complex MSBuild expressions remain limitations. |
| Compare Git revisions later | Git and the requested locally available revisions | No automatic fetch. Not required for the first UI session. |
| Perform an isolated build later | Explicit build authorization, a compatible target SDK and targeting packs, reviewed inputs and explicit restore sources | Separate from installation. Never trigger a build to repair UI startup. |
| Develop ArchSift | .NET 10 SDK and the development prerequisites in [README](../README.md#development) | Development requirements are not release-user prerequisites. |

The verified distribution is Windows x64. Linux CLI test evidence does not establish a Linux UI or self-contained Linux release. Windows ARM64 and a minimum Windows release have not been established as release-support guarantees by the current acceptance record.

Never modify User/Machine environment variables, any PATH including process PATH, registry environment entries, or PowerShell Profiles. Do not use `setx`, `dotnet tool install`, or host security-policy changes. If unexpected persisted environment/Profile drift is observed, stop and preserve evidence; do not repair it automatically. Real IFX integration remains human-operated, one reviewed command block at a time.

## Download and verify 0.4.0

Open the [v0.4.0 Release](https://github.com/von12549/ArchSift/releases/tag/v0.4.0), rather than downloading repository source through Code. Download all three original assets into an external installation directory, for example `D:\Tools\ArchSift\downloads`. These example directories must be adapted to your own target and permissions.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| `archsift-0.4.0-win-x64.zip` | 89,024,638 | `4f35dfdb0f9a14966b0d0d0c375e2c6d505d09c36f18ff673c4f686b72bba97c` |
| `SHA256SUMS.txt` | 93 | `1d4005a247668ca9aa063e10ff87f3e43ed3e13e94b0d31bb300e913cceb5fc4` |
| `acceptance.json` | 2,383 | `7b949c8023ffda8c1fee44143bbfd628ce098804e38e76030b01b3960b5971f9` |

The values are fixed to the [0.4.0 publication record](releases/0.4.0-publication.md); they are not checksums for other releases. In a no-Profile PowerShell session, manually check file lengths and each SHA-256. Check that `SHA256SUMS.txt` and `acceptance.json` identify the same ZIP. Hash equality establishes byte consistency with this release record, not an independent publisher signature.

```powershell
Get-Item -LiteralPath 'D:\Tools\ArchSift\downloads\archsift-0.4.0-win-x64.zip','D:\Tools\ArchSift\downloads\SHA256SUMS.txt','D:\Tools\ArchSift\downloads\acceptance.json' | Select-Object Name,Length
Get-FileHash -LiteralPath 'D:\Tools\ArchSift\downloads\archsift-0.4.0-win-x64.zip','D:\Tools\ArchSift\downloads\SHA256SUMS.txt','D:\Tools\ArchSift\downloads\acceptance.json' -Algorithm SHA256
Get-Content -LiteralPath 'D:\Tools\ArchSift\downloads\SHA256SUMS.txt'
```

Stop before extraction if any asset, length, version, or hash differs. Preserve the downloaded files. Do not substitute lab packages or disable security controls.

## Extract the complete package

Use Explorer to extract into an empty `D:\Tools\ArchSift\versions\0.4.0` directory. Keep `config`, `rules`, and `reports` beside `versions`, outside the source repository. Do not overwrite an existing version or move individual DLLs out of the package.

The actual version root must directly contain `archsift.exe`, `package-manifest.json`, and `web\ArchSift.Web.exe`. It must also retain the schemas, libraries, licenses, and other payload files. If Explorer inserts an extra directory, use the actual package root in every executable path.

The manifest records version `0.4.0`, RID `win-x64`, self-contained distribution, and source commit `bb599bd87aaa3322229c049442e135f3c091b72a`. Its `acceptanceStatus=candidate` is a frozen build-time fact; published `acceptance.json` records the subsequent acceptance decision.

```powershell
& 'D:\Tools\ArchSift\versions\0.4.0\archsift.exe' --version
```

Expect `archsift 0.4.0`. A missing native executable or a launch refusal is a package/host diagnostic, not a reason to install an SDK or change PATH.

## Create configuration

Create `D:\Tools\ArchSift\config\project.json` manually in a text editor and save UTF-8. Replace the example target, entry, and framework with the actual values. Keep the report/library directories outside the target.

```json
{
  "schemaVersion": 1,
  "target": {
    "root": "D:/source/MyApp",
    "entry": "MyApp.sln"
  },
  "rulesets": [],
  "build": {
    "mode": "existing",
    "targetFramework": "net10.0",
    "configuration": "Debug",
    "allowNetwork": false
  },
  "output": {
    "directory": "D:/Tools/ArchSift/reports",
    "formats": ["json", "html", "sarif"]
  },
  "rulesDirectory": "D:/Tools/ArchSift/rules"
}
```

`existing` does not restore/build automatically. An empty ruleset list is intentional during installation. `acceptance.json` is release evidence, not this runtime configuration. A syntax-only check is:

```powershell
Get-Content -LiteralPath 'D:\Tools\ArchSift\config\project.json' -Raw | ConvertFrom-Json | Out-Null
```

The UI also validates schema and paths. See [CLI configuration](cli.md), [input boundaries](build-inputs.md), and the localized [IFX installation guide](../../IFX-10-Root/docs/ArchSift-0.4.0-IFX-安装配置说明书.md) for the reviewed IFX paths.

## Start and close the UI

The normal CLI entry is:

```powershell
& 'D:\Tools\ArchSift\versions\0.4.0\archsift.exe' ui --config 'D:\Tools\ArchSift\config\project.json'
```

A successful session prints `ARCHSIFT_UI=`, `ARCHSIFT_PID=`, and a waiting state. Keeping the terminal occupied after these lines is normal: the service runs until shutdown. Open the complete URL only in your local browser. Never share its `#session=` token or include it in screenshots, tickets, or saved public logs.

Confirm target, entry, framework, build mode, output directory, and library directory. UI library listing can create `.archsift-library.json` and its lock file; these are internal metadata, not imported user rules. Preserve an existing registry, including deleted-entry tombstones. Do not click Discover projects, Verify, Verify Chain, Compare changes, Draft, or rule import/save actions during an installation-only check.

Click Safe shutdown and wait for the terminal to return. Closing a browser tab does not stop the server. If the page cannot operate, try Ctrl+C in the attached terminal, then verify the exact session process/listener has exited. Do not terminate unrelated services.

## Known 0.4.0 CLI UI launch issue

A manual IFX installation session on 2026-10-09 observed no startup lines from `archsift.exe ui`, although the correctly located Web child was listening on `127.0.0.1`. A PowerShell `2>&1 | Out-Host` retry did not restore output and left a Web process after CLI cancellation. Direct native Web launch with the same config displayed the URL, loaded the expected configuration, and exited through Safe shutdown with no remaining ArchSift process.

The release CLI starts the Web child with `CreateNoWindow=true`, without explicit stdout/stderr forwarding. This is a suspect launch-path mechanism; the precise cause and terminal-specific cancellation behavior are not yet proven by a regression test. Direct Web success does not certify the CLI launch contract or the whole installation on other hosts.

If startup is silent, first inspect the named processes and their executable paths from a second no-Profile session. A listener proves that a socket is listening, not that every UI function works. Do not guess a token, disable authentication, or start another instance. Try Ctrl+C once; if a child remains, identify its current PID and full executable path before a narrowly scoped stop. PIDs from earlier sessions must not be reused. Do not use the unsuccessful pipe workaround as the standard launcher.

After confirming the previous instance has exited, the temporary native workaround is:

```powershell
& 'D:\Tools\ArchSift\versions\0.4.0\web\ArchSift.Web.exe' --config 'D:\Tools\ArchSift\config\project.json'
```

Use the resulting URL and Safe shutdown as above. This starts the same self-contained UI with the same configuration; it does not enable analysis or builds. It bypasses CLI-specific child-environment setup, so its verified use here is limited to installation/configuration and declaration-only usage with no build authorization. SDK/build checks require their own reviewed environment isolation.

## Upgrade before an installer exists

0.4.0 has no installation/upgrade command. For a future published release, safely close the current session, independently verify that release's assets and compatibility notes, and extract into a new empty version directory. Back up config and the entire library while the service is stopped, including the registry, tombstones, and chains. Do not reimport rules merely to upgrade binaries: that changes entry identity and can leave chain references dangling.

If schemas remain compatible, use the new executable's full path with the existing config. Perform the same startup/config/shutdown check without analysis. Keep the old binary version available. If a state migration is required, review its backup/restore and compatibility requirements before running it; an older binary may not understand migrated state. Reports and policy versions do not become current compliance evidence merely because binaries were upgraded.
