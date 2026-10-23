# Getting started with ArchSift Workbench

Current-source update: 0.10.1 is a candidate under qualification; final human acceptance and publication are pending. See [portable Workbench](portable-workbench.md) for NuGet UI, portable Launcher and rollback. References to published 0.10.0 below retain their historical context.
Release status: **0.10.0 published and download-verified**. See the [publication record](releases/0.10.0-publication.md) for exact assets, final acceptance and successful public-download usage. The following instructions retain their packaging-stage context; immutable bundled copies are qualified by the matching final acceptance record.

These instructions cover the 0.10.0 layout and its numbered release candidates. Use the full version in the artifact manifest. Source [release status](releases/0.10.0/release-status.json) records packaging-stage progress; the separate matching release acceptance record establishes final acceptance/publication for exact bytes. Older hashes do not verify a new version.

## Choose the entry point

| Task | Entry point | Prerequisites |
| --- | --- | --- |
| Explore a project, edit JSON rule copies, view reports | Windows Workbench | Complete verified Windows x64 ZIP, local browser and writable external state |
| Run an approved full architecture-test gate | CLI or Architecture tests tab | Approved test project, SDK 10.0.303, offline dependencies, rules and case baseline |
| Verify current-source assembly evidence | Build evidence workflow | Explicit capture/build/review/seal inputs; see the evidence guide |
| Install or change selected binary version | Separate Setup | Verified matching package, reviewed plan and inactive owned processes |

## Verify and unpack

Download the Windows ZIP, separate Setup, `SHA256SUMS.txt`, `artifact-manifest.json` and `acceptance.json` from the same release. Check the version, source identity, supported environment, limitations and acceptance state. Compare each downloaded file's SHA-256 and length with the release record before running it. Retain `LICENSE`, third-party notices and the complete payload. Hash equality proves byte agreement; it is not a publisher signature.

Extract into a new empty local directory such as `D:/Tools/ArchSift/versions/0.10.0`. Keep configuration, rules, reports and evidence outside analyzed source roots. A package extraction alone does not enroll files for Setup protection. Use the [Setup guide](setup.md) for managed installation, upgrade and rollback.

## Create an ordinary analysis/Web configuration

Copy [the example](../samples/workbench/config.example.json) to an external configuration directory, then replace every example path and the solution/project entry. `target.entry` is relative to `target.root`; the other paths resolve relative to the configuration file. Output and target roots must be disjoint. This configuration starts in `existing` mode and performs no automatic build or restore.

```powershell
& 'D:/Tools/ArchSift/versions/0.10.0/archsift.exe' --version
& 'D:/Tools/ArchSift/versions/0.10.0/archsift.exe' analyze --config 'D:/ArchSift-state/MyProject/web.json'
& 'D:/Tools/ArchSift/versions/0.10.0/archsift.exe' ui --config 'D:/ArchSift-state/MyProject/web.json'
```

Run PowerShell with `pwsh -NoProfile`. The terminal prints a private loopback session URL. Open it locally and keep its token private. Confirm target, entry, framework, configuration, output and library before starting an action. Rulesets imports/editing create external saved copies; unsaved changes do not become execution inputs. Discovery is not verification and a zero-match rule is inconclusive by default.

## Run architecture tests

Ordinary analysis/Web JSON is different from a test-gate configuration. Follow [project integration](project-integration.md) to prepare the test project, exact packages, enabled JSON type-dependency cases and reviewed case identities. In Architecture tests, select that project's external gate configuration. Its target root must match the Web session.

Use Full for acceptance. Subset needs an actually discovered UID and is diagnostic only; it cannot allow the complete gate. List/configuration views and a previous green report are not current execution results. Read the new run's authoritative `architecture-test-run.json`, including coverage, actual case outcomes, exclusions, audit status and `runGate`. Preserve failed and cancelled runs.

## Finish safely

Finish or cancel current jobs, save/export wanted edits and select Safe shutdown. Wait for the terminal to return. Closing a browser tab alone leaves the service running. Do not upgrade an active installation or kill processes using an old PID. See [installation](installation.md) for ownership/lifecycle behavior and [reports](reports.md) for result semantics.

The self-contained package supplies the runtime needed to launch Workbench. Building an analyzed project or running its tests still requires its approved SDKs and dependencies. Linux CLI regression does not qualify Linux Web or an ARM64 binary. No Guard installation, persistent PATH repair, profile modification or background service is required.

For framework-dependent library/tool/test use, provision a supported patched .NET 10 runtime (at least 10.0.13 at this release) separately from the pinned 10.0.303 SDK. The SDK version does not prove the selected shared-runtime patch. The Windows ZIP includes its reviewed 10.0.13 runtime; it does not install or repair system tools.
