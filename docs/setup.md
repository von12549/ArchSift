# ArchSift 0.5.0 portable setup

0.5.0 is [published and independently verified](releases/0.5.0-publication.md); its [acceptance and release gates](plans/20261010-archsift-0.5.0-plan.md) are closed with stated limitations. The commands below belong to 0.5.0. The published 0.4.0 ZIP has no setup/upgrade commands. Use independently verified official assets.

`ArchSift.Setup.exe` is a separate self-contained Windows x64 executable. It performs user-writable filesystem installation without elevation, SDK/runtime prerequisites, PATH changes, service registration or target execution. `--notices` prints embedded ArchSift/.NET license and third-party notices. Run PowerShell 7 with `-NoProfile` and use full executable paths.

## First installation

Download the version-specific Windows ZIP, Setup executable, checksums and acceptance record. Verify every asset against that release's publication record before executing Setup. ZIP hash agreement is integrity evidence, not a publisher signature. Inspect config/path changes before applying them; an installation does not adopt rules or establish source compliance.

```powershell
$setup = 'D:\Tools\ArchSift.Setup.exe'
$zip = 'D:\Tools\downloads\archsift-0.5.0-win-x64.zip'
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
# Compare zipHash with the independently obtained release record before continuing.
& $setup plan install --root 'D:\Tools\ArchSift' --package $zip --sha256 $zipHash --target 'D:\source\MyApp' --entry 'MyApp.sln' --tfm net10.0 --smoke true > 'D:\Tools\install-plan.json'
if ($LASTEXITCODE -ne 0) { throw 'Planning failed; do not apply.' }
$plan = Get-Content -LiteralPath 'D:\Tools\install-plan.json' -Raw | ConvertFrom-Json
# Read the complete plan, including normalized paths, generated config and identities.
& $setup apply --plan 'D:\Tools\install-plan.json' --plan-id $plan.planId
```

Plan/install accepts `--configuration Debug|Release`, `--output <reports>` and `--library <rules>`. Defaults are existing binaries, Debug, explicit TFM, no network restore, JSON/HTML/SARIF and empty rulesets. Plans are read-only and expire after 24 hours. A plan ID binds all displayed data; any config/library/selection/package drift rejects apply. Unknown files/configs are never overwritten. The config is `config/project.json`; selected binaries are in `versions/<version>`. `install.json` is a local selection/ownership record, not a PATH launcher.

`--smoke true` loads the saved config through native Web and safely stops it. It performs no analysis/build/import and does not persist or display a session token. Open the normal UI separately through the selected executable when desired. Setup stdout is machine JSON; stderr contains errors and hash/equality checkpoints. Preserve both if a step fails.

## Existing manual ZIP installation

Explicit adoption records ownership only for the existing package manifest's payload. Config and library bytes remain unchanged. The version directory may be an existing installation root with user state beside binaries, or a nested version directory. Setup does not assume every file under that root is disposable.

```powershell
& $setup inspect --root 'D:\Tools\ArchSift'
& $setup plan adopt --root 'D:\Tools\ArchSift' --version-directory 'D:\Tools\ArchSift\versions\0.4.0' --config 'D:\Tools\ArchSift\config\project.json' > 'D:\Tools\adopt-plan.json'
```

Review/save/apply the plan as above. Close the owned UI/CLI first. Unknown or future state schemas reject adoption instead of discarding fields. The 0.5.0 real IFX installation and external-copy upgrade/rollback passed individually reviewed operator blocks; future IFX changes still require their own review.

## Upgrade and rollback

Close the service. Independently verify new assets and release compatibility; then plan and review an upgrade:

```powershell
& $setup plan upgrade --root 'D:\Tools\ArchSift' --package $zip --sha256 $zipHash --smoke true > 'D:\Tools\upgrade-plan.json'
```

Apply as above. Setup locks operations, backs up config and the complete library as a consistent byte set under `operations/<id>/backup`, stages immutable binaries on the installation volume, rechecks state and atomically changes selection. Reports are retained. Identical current version/payload is a checked no-op; changed bytes under the same version reject. Upgrade never reimports rules or changes entry IDs, tombstones, chain order/references, policy versions or report conclusions.

0.4.0 schemaVersion=1 and explicitly compatible 0.5.x config/library/chain schemaVersion=1 are supported. This release provides no schema migration. Unknown compatibility rejects before selecting new binaries. Keep the old version; do not overwrite it.

Inspect the receipt and current selection before rollback:

```powershell
& $setup inspect --root 'D:\Tools\ArchSift'
$selectionHash = (Get-FileHash -LiteralPath 'D:\Tools\ArchSift\install.json' -Algorithm SHA256).Hash.ToLowerInvariant()
& $setup rollback --root 'D:\Tools\ArchSift' --receipt '<successful-upgrade-id>' --selection-sha256 $selectionHash
```

Rollback accepts only the currently active upgrade's receipt and unchanged post-upgrade state. Later config/rule edits cause rejection and are preserved. Compatible upgrades need only binary reselection, not byte restoration. An install/adopt receipt is not authority for uninstall. No automatic old-version or user-data deletion is provided.

## Interrupted operations

Inspect `operations/<id>/journal.json` and preserve diagnostics. Use `recover --root <root> --operation <id> --journal-sha256 <reviewed-hash>`. If current selection matches the intended after state, recovery completes the receipt after checking state. If selection still matches before, it marks the operation aborted and retains backup/staging/version/config artifacts for review. Unknown selection/state drift rejects. Precommit install artifacts may be explicitly adopted after review; complete aborted-upgrade version directories are revalidated on retry. Recovery never recursively deletes an arbitrary directory or repairs host drift.

`acquire --version <version> --sha256 <independently-verified-zip-hash> --output <new-zip>` explicitly downloads only the pinned ArchSift GitHub Release ZIP, verifies it and writes a new destination. It does not download Setup, execute content or restore target packages. Manual browser download remains supported. A failed acquisition retains a uniquely named `.download-<id>` for inspection.

## Safety and limits

Local fixed volumes only; installation and target roots are disjoint in both directions. Links/reparse points, Windows reserved paths, conflicting state paths, archive traversal, duplicate/case-colliding payloads and corrupt/missing manifest entries reject. Limits: ZIP 1 GiB, expanded payload 2 GiB, individual file 256 MiB and 10,000 entries. State backup is bounded to 2 GiB/10,000 files.

Setup compares User/Machine environment, process PATH and PowerShell Profile hashes within the current operation. Drift stops further writes; preserve evidence for human review. All .NET child isolation pairs `DOTNET_CLI_HOME` with `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0` and leaves parent environment/PATH unchanged. Exit codes: 0 success/no-op, 2 invalid/conflicting input, 3 execution/verification failure, 130 cancelled. An error does not authorize bypassing safety checks or broad cleanup.
