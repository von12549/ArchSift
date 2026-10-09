# ArchSift installation and upgrade design

Date: 2026-10-10 (Australia/Sydney). Original status: **PROPOSED DESIGN; DOCUMENTATION IMPLEMENTED; INSTALLER CODE NOT STARTED**. The initial documentation/design authorization did not authorize implementation or select a version. The user subsequently authorized execution on a new branch, including commits, push, PR, merge, verification and publication, and selected **0.5.0**; see the [execution plan](20261010-archsift-0.5.0-plan.md). Real IFX remains operator-driven; policy adoption is outside scope. This document preserves the proposal; frozen implementation contracts and acceptance records supersede proposed syntax.

Users should be able to obtain a verified Windows package, select an external installation root and target, create a reviewed configuration, and upgrade binaries without losing saved rules or chain references. A portable per-user installer should perform those filesystem operations without modifying the host environment. Existing manual ZIP installation remains supported through [the installation guide](../installation.md).

## Problems and delivery order

| Problem | Planned change | Acceptance |
| --- | --- | --- |
| README still described 0.4.0 as unreleased; installation and development prerequisites were mixed | Correct the recorded release status and add a version-specific installation/environment guide | A release user can distinguish self-contained runtime use from SDK development/build requirements. |
| Native CLI UI launch was silent in the manual IFX session; the pipe retry left a child | Repair output forwarding and child lifetime before making setup depend on CLI UI launch | Native no-Profile launch shows URL/PID/state; Safe shutdown and Ctrl+C return; no owned child/listener remains. |
| Download/extraction/path/config choices require repeated manual steps | Add a standalone portable setup application with a preview and operation receipt | A validated config loads in the UI; target and host state remain unchanged. |
| No upgrade operation or compatibility/rollback contract | Add staged side-by-side upgrades, consistent external-state backups, and explicit rollback | Ruleset bytes, entry IDs, tombstones, chains, and user config survive a compatible upgrade. |

Delivery order is documentation → launch lifecycle repair → setup contracts/prototype → install acceptance → upgrade/rollback acceptance. The current change completes documentation and specifies the later packages; it does not repair the launcher.

## Proposed distribution and commands

Use a separately published, self-contained Windows x64 `ArchSift.Setup.exe`, verified as a release asset. It must start on a host without an installed SDK/runtime and without administrative elevation for a user-writable root. This is a proposed artifact; no such installer is claimed in 0.4.0. MSI registration, a Windows service, shell integration, global .NET tools, and PATH shims are outside the first design.

| Proposed operation | Inputs | Behavior |
| --- | --- | --- |
| Inspect or doctor | Explicit install root, local package, optional config | Read-only package/path/state checks; no environment repairs, builds, restore, or rule verification. |
| Plan installation | Version/RID, verified asset source, install root, target root, entry, TFM, configuration, output/library paths | Display normalized paths, changes, source identities, host requirements, defaults and conflicts; no policy adoption. |
| Apply installation | A fresh reviewed plan identity | Create owned version directories and a new config; refuse stale plans and conflicting existing files. |
| Plan/apply upgrade | Existing owned installation, explicit new release identity | Validate compatibility, stage new binaries, back up external state, perform a limited startup check, then select the version. |
| Rollback | Explicit successful installation/upgrade receipt | Select retained binaries and restore state only when required by the migration contract and reviewed change check. |

Names and UI/CLI flags require contract review before coding. This table is not runnable 0.4.0 syntax. In IFX, automatic real-target execution remains outside authorization until explicitly changed; the prototype can be tested with synthetic targets and user-operated installation steps.

## Installation root and ownership

Keep `downloads`, `versions/<version>`, `config`, `rules`, `reports`, staging and operation receipts beneath a user-selected local root outside the target. For IFX this root is `D:/IFX-10-Root/ArchSift`; the source remains `D:/IFX-10-Root/IFX-New` and is read-only.

Add a local `install.json` only when a setup operation actually adopts/creates an installation. Its proposed schema records setup schema version, canonical root, selected version, installed version paths and manifests, config paths, owned-file identities, and operation receipts. This setup manifest is distinct from the frozen release `package-manifest.json`. A local selected-version field does not imply a command exists on PATH; launch instructions still give verified absolute paths.

Existing ZIP installations need explicit adoption. Inspect the package manifest and directory boundaries, show which files setup would own, then record only that ownership. Do not treat all files under an arbitrary user-selected directory as disposable tool files. An unknown existing config is never overwritten by default. Repeated installation of the same verified bytes should be a checked no-op; changed bytes under the same version must reject or be staged separately for review.

Refuse target/install overlap in either direction, unsafe output/library relationships, links/reparse points, and unsupported remote roots. Normalize and recheck containment immediately before every write, move, replace, or removal. Download names and archive paths must not control filesystem destinations unchecked.

## Host safety contract

Never change persisted User/Machine environment variables, registry environment values, PowerShell Profiles or startup files, or PATH at any scope. Do not install an SDK, repair security policy, register global tools, or execute the target. Any .NET child receiving isolated `DOTNET_CLI_HOME` also receives `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0` in that child's environment only. Do not copy process PATH into persisted storage or replace an inherited Windows environment with an incomplete custom map.

Before installation/upgrade or an integration operation, capture User/Machine/process PATH byte values and User/Machine environment/Profile identities privately. Compare at the download/extraction/config/launch/shutdown/commit checkpoints, after any operation capable of launching dotnet, and on completion or cleanup. Emit only equality, hashes and changed-variable names as appropriate, never values or tokens. A baseline is valid only within its operator/process context; a different shell hash is not a before/after comparison.

Unexpected host drift is a critical stop: preserve evidence, stop further product operations, and require explicit human review. Filesystem rollback is not authority to repair host environment drift. Ordinary installation failure with equal host state can undo precisely owned staging files, but never broad directories or target files.

## Package intake and configuration

Accept either an explicitly selected official release or independently verified local assets. Network download must be an explicit setup option scoped to release acquisition, never target restore. Pin version/RID and asset identity; reject missing/checksum-mismatched assets and unexpected release metadata. Show that checksum agreement is integrity evidence, not an independent publisher signature. The release acceptance record and manifest build-time status have different roles; do not rewrite the published manifest to make them agree.

Extract into a new staging directory on the installation volume. Reject archive traversal, absolute paths, duplicate/case-colliding paths, links/reparse points and Windows reserved path forms. Apply bounded expanded-size/file-count limits, verify every manifest payload entry, then atomically rename the complete staged version into its final empty directory. Existing versions are immutable. A hostile/truncated archive never reaches launch.

The configuration form collects target root, real entry, supported TFM, configuration and external output/library locations. First-install defaults are `existing`, `Debug`, `allowNetwork:false`, JSON/HTML/SARIF output, and empty `rulesets`. Show generated JSON before saving. Validate syntax, schema and safe paths without analysis. Do not infer approved rules from discovered dependencies, select test rules, create a chain, or enable isolated build during installation.

Write new configuration through a temporary sibling file and atomic replacement only for a newly owned destination. Editing an existing user config is a distinct reviewed operation with a byte backup and conflict detection. Preserve unsupported/unknown future schemas by stopping, rather than dropping fields.

## UI startup and process ownership

Repair CLI child stdout/stderr forwarding with concurrent stream consumption, bounded startup detection and a recorded native Web PID. The current `CreateNoWindow` mechanism is a hypothesis for the observed missing output; reproduce before choosing a fix. Test terminal and redirected-output paths independently. Shutdown must attach to the owned process tree, wait for completion, and detect surviving child/listener state.

The installer may offer a user-requested UI smoke after configuration is committed. The smoke loads config and performs Safe shutdown, with no discovery, verification, draft, comparison, import or target build. Do not print session tokens in setup receipts; any private temporary communication containing a token must be tightly scoped and removed through recorded cleanup. Display the authenticated URL only to the local operator.

On timeout, preserve diagnostics and identify the exact owned PID/path/session. Graceful shutdown comes first; forced cleanup is limited to the validated setup-owned process. An unknown PID or listener is not authority to terminate another process. Never silently launch a second instance while the first is unresolved. Direct Web startup remains a documented 0.4.0 workaround, not proof that a repaired CLI lifecycle passes.

## Upgrade and rollback transaction

| Phase | Operation | Failure behavior |
| --- | --- | --- |
| Inspect | Lock setup operations; read installation/state hashes; find processes using the selected installation | Ask the operator to close the UI; do not kill unrelated processes or modify a live library. |
| Validate | Resolve release/RID; verify assets, available space, old/new config/library/chain schemas and migration support | Reject before changing active state if compatibility is unknown. |
| Backup | With the service stopped, snapshot user config and the entire library, registry, tombstones and chains as a consistent byte set | Stop if files change during capture; reports are retained and need not be copied merely to switch binaries. |
| Stage | Extract and verify new immutable version; keep the old version | Failed staging leaves selected binaries and external state unchanged. |
| Migrate if necessary | Run a separately versioned, reviewed migration on a staging copy; retain original bytes and identity mapping | Unsupported downgrade or identity changes require explicit review. Never regenerate entry IDs or reconnect chains by filename. |
| Check | Validate staged config/state and perform the requested installation-only UI smoke | A compatible-state failure can keep/select the old version; no rule verification is implied. |
| Commit | Recheck plan and external-state hashes, then atomically update the setup selection/approved state files | Record success only after all checks; interrupted operations reconcile from the journal and byte identities. |
| Rollback | Use receipt to reselect old binaries; restore pre-migration state only if necessary | Detect newer config/rule edits first; preserve them and refuse silent replacement. |

A successful binary upgrade does not change rule versions or prove current-source compliance. Saved ruleset bytes, `.archsift-library.json` entry IDs and deleted-entry tombstones, chain order/references, exception reasons, external config paths, and reports are preserved by default. Export/import is not an upgrade strategy. Deletion followed by same-name import intentionally creates a different identity.

Old versions remain until an explicit cleanup request selects precisely owned files. Removing an installation never automatically deletes user config, rules, chains or reports. Cross-schema rollback must restore the matching state snapshot together with binaries; selecting an older executable alone is insufficient after an incompatible migration.

## Planned regression and acceptance matrix

| Area | Required cases |
| --- | --- |
| No-SDK installation | Self-contained setup and payload on a controlled x64 host without SDK/runtime; no dotnet fallback or global tools. |
| Host invariants | Before/after PATH/environment/Profile equality; regression rejects persistent mutation APIs; isolated .NET children cannot register tools in User PATH. |
| Filesystem safety | Both-direction overlap, links, traversal ZIP, duplicate paths, corrupt manifest, partial extraction, disk-full and ownership-restricted cleanup. |
| Repeat/conflict behavior | Reinstall identical bytes, same-version different bytes, existing config, stale plan, concurrent setup and live UI. |
| UI lifecycle | Console and pipe/file output, startup failure, no token leak, Safe shutdown, Ctrl+C, orphan cleanup and no remaining listener. |
| State preservation | Ruleset hash/entryId equality, tombstones, existing and dangling chains, external config paths and unsaved-edit boundary. |
| Upgrade/rollback | Compatible upgrade, unsupported schema, failed migration, interruption in each phase, later user edits, retained old version and rollback without data loss. |
| IFX acceptance | Separate human-operated command blocks; read-only target, external artifacts, existing/offline context and no analysis/build during setup smoke. |

Implementation should add tests that exercise these failures and identities, rather than only asserting the happy-path file layout. Real IFX acceptance is an operator gate. Published 0.4.0 historical evidence remains frozen; a future release needs new exact-source/package evidence.

## Deliverables before implementation

Freeze setup/receipt/journal schemas and compatibility rules; choose UI/CLI argument syntax, network acquisition behavior and resource limits; define no-SDK synthetic fixtures and token handling; then implement the launch repair and setup packages in that order. The current documented design recommends a portable native installer. Replacing it with MSI or broad shell integration would require a separate scope decision.
