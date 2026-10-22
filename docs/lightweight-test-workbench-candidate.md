# Lightweight architecture-test workbench: historical local-candidate use and distribution boundaries

2026-10-16 **accepted LT06 local UI revision candidate**: UID lookup, language, result placement and global-navigation issues from the first human review were corrected. The new payload was `D:/ArchSift-lab/packages/lt06-ui-revision-20261016-a`, manifest SHA-256 `34147699cb67d5f24d68e48de93152208ca75519fefbea38df66dc2376443c41`; see [LT06 UI revision](acceptance/20261016-lightweight-lt06-ui-revision.md). At packaging it used `e0b3bddabefd4561ca9ddfc13ed68e7d91a4f5b0` plus the manifest-listed uncommitted source files. The user subsequently completed [independent human review](acceptance/20261016-lightweight-lt06-human-ui-review.md) of the same payload, passing all five items. Manifest `sourceState` is the packaging-time identity, not current Git-stage status. The initial fixed-candidate provenance below remains historical.

To reopen that historical revised candidate, run the command below under `pwsh -NoProfile` and open only its printed loopback URL locally; do not publish its token. If an agent has already launched the candidate and opened the browser beside Codex, continue that session rather than launching the same port twice. Its runs, rule copies and history use the independent `lt06-ui-revision-20261016-a` run root without changing the first candidate or original IFX checkout.

```powershell
& 'D:/ArchSift-lab/packages/lt06-ui-revision-20261016-a/web/ArchSift.Web.exe' --config 'D:/ArchSift-lab/runs/lt06-ui-revision-20261016-a/web-config.json'
```

In Architecture tests, select `D:/ArchSift-lab/runs/lt06-ui-revision-20261016-a/test-gate-config.json`. At this historical stage, the independent run root held a complete 80+1 pass report under global Results -> Architecture test results or History -> Architecture test history. Switch English/Chinese to inspect localization. In Subset mode, search by case name and select a result to fill its discovery UID. Read-only review does not require repeating the real gate.

The **initial local-review candidate** fixed on 2026-10-16 used product commit `e0b3bddabefd4561ca9ddfc13ed68e7d91a4f5b0` and development version `0.9.0-dev`. Its payload was `D:/ArchSift-lab/packages/lt06-candidate-e0b3bdd`, with per-file `manifest.json` SHA-256 `4a19cfd8ae57464d4b6a010dec9a2b923ab1e5b5aa5bf287b3b308fc2d768212`. At that initial stage it was for independent local review, before [human UI acceptance](acceptance/20261016-lightweight-lt06-human-ui-review.md) or authorization for public publication, installation, IFX policy adoption or PR/merge. The [machine-candidate record](acceptance/20261016-lightweight-lt06-candidate.md) lists actual gates and limitations.

## Use in the authorized external IFX worktree

The local demonstration target was fixed at `D:/ArchSift-lab/fixtures/lt05-shared-cc06528`, IFX source `cc06528ab17052baafa496df2a89f6cde9207384`, rule SHA-256 `abd6a7d94e3655b4255b2a280e8fa58c6c50e64ce91dce6848121d24d20a92aa`. This was an independent worktree with LT02A integration files; original `D:/IFX-10-Root/IFX-New` checkout/installation remained read-only. The execution-ID baseline came from **that fixed path** at `D:/ArchSift-lab/runs/lt05-metrics-20261015/worktree-original-case-ids.json`, SHA-256 `74a1d63dabbb0be14e139134661fed6bf0af77fe3ba00f13fc69959de66ca72b`. Moving the worktree requires another native 80-case review and explicit baseline; that ID file cannot simply be reused.

The environment was Windows x64, .NET 10 SDK (locally verified 10.0.303), net10.0/Release and IFX xUnit v3/Microsoft.Testing.Platform. Candidate Web/CLI publish outputs depended on the installed .NET 10 runtime. The test project continued to use this repository's `ArchSift.Testing` through its reviewed project reference, original locked IFX dependencies, explicit offline NuGet sources and external package cache. The payload did not install IFX's test project, original tests, NuGet packages or adapter source into other targets.

The following fixed-input UI invocation runs under `pwsh -NoProfile`. It prints a one-time loopback session URL; open it locally only and do not expose the session token. Before closing the terminal, press Ctrl+C and wait for controlled tasks to exit.

```powershell
& 'D:/ArchSift-lab/packages/lt06-candidate-e0b3bdd/web/ArchSift.Web.exe' --config 'D:/ArchSift-lab/runs/lt06-candidate-e0b3bdd/web-config.json'
```

Select external configuration `D:/ArchSift-lab/runs/lt06-candidate-e0b3bdd/test-gate-config.json` in Architecture tests. Reading it only displays baseline/JSON/not-included lists. Only after starting Full mode can the current authoritative report establish its result. UI rule copies go to `D:/ArchSift-lab/runs/lt06-candidate-e0b3bdd/web-rules`; runs/history use `web-output/test-runs` under the same external run root, without editing original IFX rules. For CLI-only review, create a new unused external run directory distinct from the saved `cli-gate`, point a configuration copy's `runDirectory` there, then run:

```powershell
& 'D:/ArchSift-lab/packages/lt06-candidate-e0b3bdd/cli/archsift.exe' test-gate --config '<absolute-external-config-copy>'
```

The gate forces `IFX_UPDATE_SNAPSHOTS=0` in test children. Exit 0 is complete pass, 2 actual violation, 3 execution/report error, 4 inconclusive/subset coverage, 5 invalid configuration and 130 cancellation. Full evidence continues through the original ArchSift evidence entry; daily allow does not prove source binding. `architecture-test-run.json` is authoritative, with HTML/UI projections. At this historical stage, 182 unmigrated Class A and 406 disabled Class B candidates remained not included and did not count as passed. Unknown history does not prove current-source compliance. Only basic independent run directories and read-only history existed, without automatic rotation, archive recovery or run resumption.

## Historical test-dependency and UI distribution plan

The first version delivered `ArchSift.Testing`/`ArchSift.TestGate` source and locks in the ArchSift repository. The target test project explicitly referenced the former; CLI/Web publish outputs contained coordination assemblies and used the same TestGate service. Each controlled target needed independent review of new references, adapters, rules, offline sources and original-case baseline. The LT02A example was not formal IFX policy. That first version published no separate NuGet package, copied no InProject runtime and changed no old Setup/installation selection. Cross-machine delivery still needed portable dependency paths, target authorization, locked sources and case-ID baselines. A local candidate manifest was not a general installer.

The UI reused Web loopback sessions, safety tokens and rule editor. The candidate included only published CLI/Web directories and a SHA manifest, without a new installer, background service or public site. Any later authorized release would build from frozen source and confirmed dependency layout, with reviewed packages/release gates. LT05 comparable cold runs were approximately 118 seconds, dominated by target rebuild, with no proven substantial speedup. That result did not justify adding a materials-lifecycle system.
