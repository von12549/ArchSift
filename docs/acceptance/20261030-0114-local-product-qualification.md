# ArchSift 0.11.4 local product candidate

Date: 2026-10-30 (Australia/Sydney). Status: **local product scope passed; unpublished development candidate**. The [machine record](20261030-0114-local-product-qualification.json) binds exact inputs, package hashes, test scope and the external evidence bundle. This does not establish clean-source CI, real IFX acceptance or publication readiness.

## Implemented behavior

The new CLI/TestGate accepts explicit stable SDK bounds for schema-2 daily gates while retaining exact baseline behavior when no range is supplied. Required-case SDK metadata is validated generically and remains provenance. Explicit selected dotnet paths are carried through restore/build/test children; reports identify the actual selected SDK, host path, restore policy and gate range.

Configured workspaces generate a daily-test configuration using an explicit source-clearing NuGet.Config and locked restore. The generated startup configuration selects that inventory automatically for direct Workbench and Launcher children. Loading inventory does not build or run the target. Independent dependency audit remains a separate result; `restoreMode` controls package sources while the existing `offline-architecture` audit setting disables in-gate audit.

Omitted storage roots resolve to per-user LocalApplicationData; Init/Start create the owned project state. Doctor does not initialize state or execute a dotnet probe child. Project policies are confined to the project root, contain hash-bound rules and cannot coexist with a direct rule list. Invalid policy shapes produce configuration errors. Source/policy/rule/gate/host identities control generated templates. Runtime/cache overlaps and reparse points are rejected. Configuration refresh has a bound recovery journal: an interrupted refresh resumes only for matching inputs and preserves history and conflicting edits.

The package-owned PowerShell entry now follows the same default storage, host/configured versus exact/offline policies and SDK ceiling. Init returns machine-readable JSON without an extra text line. UI guidance refers to declared package sources rather than incorrectly promising offline restore in configured mode.

## Actual validation

| Check | Result and scope |
| --- | --- |
| Source regression | Architecture 2/2, unit 157/157, integration 206/206, MTP 58/58; no skipped cases. The missing portable fixture was supplied through the standard tracked recipe |
| Final Workspace changes | All 50 Workspace cases passed after malformed-policy, runtime/cache and interrupted-refresh changes. Earlier unaffected suites are retained; the touched scope was rerun |
| Final package construction | Five matching 0.11.4 packages, Release build from explicit locked offline inputs, pinned internal dependency edges and version-specific embedded documentation; Web inventory verifies 69 content files |
| Actual daily SDK matrix | Exact final packages ran the owned synthetic gate on 10.0.300, 10.0.303 and 10.0.403. Each executed 2 typed C# plus 1 JSON case, with complete coverage, zero failures and zero skips |
| Exact compatibility and negatives | Exact 10.0.303 lane passed; a later SDK without range was blocked before restore. Missing explicit host and unknown fields returned the existing configuration-error code 5 without fallback |
| Default state and automatic inventory | Absent unique project state was created beneath the generic LocalApplicationData default; Doctor before Init left it absent. Generated project rules and daily inventory loaded without manual path entry |
| Packaged lifecycle | Four first/repeat direct/Launcher-child sessions passed readiness, automatic inventory, dirty-state shutdown rejection, safe stop, listener cleanup and lease release |
| Agent visual/hands-on | Codex used the Windows Chrome extension to open Launcher and child Workbench, inspect/expand the automatically selected inventory, execute the synthetic gate and observe allow/0 with 3 passed, then close safely. Direct Workbench on SDK 10.0.300 was also exercised. Final exact-package minimum-SDK startup/inventory/shutdown was observed after the last CLI changes |
| Evidence binding | The earlier rendered Web has all 69 content files byte-equal to the final package; its inventory JSON differs only in serialization order, with equivalent version/source/entries. Reused visual/run observations are identified separately from final exact-package CLI/bootstrap tests |
| Host state | Recorded installation/build/test/review boundaries retained environment/Profile equality. SDKs were verified archives extracted into owned external directories; parent/persistent environment was not repaired |

UI acceptance is explicitly Agent-delegated. `humanPerformed=false`; no independent-human score or separate-machine result is claimed. Screenshots were inspected through the browser tool; no exported PNG is represented as an evidence file.

The earlier failed compiler/fixture/harness/native-window-access attempts remain in their original runs. The harness corrections included using CLI error code 5 and avoiding the reserved PowerShell PROFILE variable. Successful matrix evidence was revalidated when reused during the scoped harness follow-up. The Windows reparse fixture uses a real junction without requiring symbolic-link privilege and also exposed a reserved-temporary directory guard that is now enforced.

## Frozen source and packages

The base commit is `944c3ff7697d5d2dc7cbd11beefbd0bd268b898f`; the actual source is uncommitted. All 910 build inputs were byte-inventoried and rechecked before creating the final source archive. Input-manifest SHA-256: `9483a8e787571566179355fb2de9c57f46b8f9cc9505282499bf58b3aa4583ad`. Source archive SHA-256: `fd709027afece9e6f60eaf0ce5336dd9f6ff5a530dfe075a94e6413bbaffa324`. These are development-source identities, not a new Git commit. This closeout and its status links are post-build documentation.

| Package | SHA-256 |
| --- | --- |
| ArchSift.Contracts.0.11.4.nupkg | `ad24ec356aebde2c6ebfef7e678841f94b025dfefec86a01080c6d3adf963a53` |
| ArchSift.Core.0.11.4.nupkg | `a8fa9e56a227d3e057b0e9a99a59a94c16920b60acf275138c5f8cbb5f80dc27` |
| ArchSift.ArchUnit.0.11.4.nupkg | `b02e965eed4dc1fad7fddf466b73138a48478ba81547152c983760d5b9adf338` |
| ArchSift.Testing.0.11.4.nupkg | `394637437bef319c329f98e7811d52460e207b671146b152982ecae9302af8be` |
| ArchSift.Cli.0.11.4.nupkg | `b4b467ab530632c9cf9b713d95aa6fa74ec4c2f2dcc7c7e30c9a1c9c287d0a87` |

Review assets are in `D:/ArchSift-lab/archives/0114-review-20261030-02`. The 44-member `local-product-evidence-final.zip` was reopened and checked for exact member count, safe unique paths, byte lengths and hashes. Bundle SHA-256: `284b555bc3a2521c745d542fca0cdec6a9d960c0ca16764aaecafaa4ad21e71a`. The source archive, package copies, patch and machine record are separate bound assets in that review directory.

## Next boundary

Continue with the isolated IFX new-version binding and its actual 493-case matrix, independent dependency audit and later clean-machine/migration scope in the [release plan](../plans/20261030-0114-daily-portability-release.md). Product synthetic coverage does not replace those gates. Clean committed-source/CI package qualification, the 0.11.4 publishing profile and final public identities also remain pending. No commit, push, publication or existing-installation activation occurred. The user requires another confirmation before publication.
