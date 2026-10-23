# Workbench next iteration: NuGet Web delivery and JSON inventory readability

Recorded: 2026-10-23, from the user's accepted IFX 0.10.0 human review. **NX01 and NX02 are recorded for the next iteration, not started.** No target version is assigned; preserve one unified product version when that version is chosen. This closeout records scope and acceptance criteria, not implementation authorization, a new release, or a rewrite of published 0.10.0 assets. [Machine-readable backlog](20261023-workbench-next-improvements.json).

## NX01 — Include Web in NuGet Workbench delivery

The user observed that a NuGet-based Workbench cannot start Web UI independently of a separately supplied Web installation. Read-only inspection confirms the released `ArchSift.Cli.0.10.0.nupkg` contains 40 entries and no Web host/static payload. [CLI startup](../../src/ArchSift.Cli/Program.cs) searches adjacent `web/ArchSift.Web.dll`, then a development checkout, and fails if neither exists. The accepted standalone Windows distribution already includes Web; the gap concerns the NuGet distribution.

Design a unified-version delivery that supplies and resolves the matching Web host, dependency/runtime descriptors and static assets. Bundling it in the tool package versus automatically resolving a companion package remains a design decision. The intended outcome is **no separate Workbench installation or source build**; the approved .NET SDK/runtime is still an explicit prerequisite. Preserve Core's independence from CLI/Web and consumer installation.

Acceptance requires a cold install/restore from an explicit approved feed, followed by working `ui` and Launcher with no separate Web installation/source checkout; matching versions/provenance/notices and package approval; complete offline behavior without hidden downloads or unrelated installed-Web fallback; clear missing/tampered/version-mismatch errors; unchanged CLI architecture use; loopback/token protection, Safe shutdown, upgrade/rollback and both entrance lifecycle checks. Do not modify persistent PATH/environment. Keep the accepted release/package closure immutable and qualify new payloads separately.

## NX02 — Collapse the Architecture tests JSON inventory

The user observed that all JSON rules are rendered as individual rows, making the 401-case inventory too long to read. [renderInventory](../../src/ArchSift.Web/wwwroot/test-workbench.js) uses flat ruleset cards and ordered case lists. This is the inventory surface, not the separate Results view's existing collapsible groups.

Use compact per-ruleset disclosure groups with visible name/path and case counts, initially collapsed; add expand/collapse-all controls and retain clear access to copy/edit actions. Keep keyboard/screen-reader behavior and both existing locales, sensible focus/disclosure state on refresh or locale changes, all original case IDs and source paths, and visible empty/disabled/error states. Keep the C# and responsibility/deferred summaries clear.

Collapse must affect presentation only: complete mode still runs all 493 cases = 92 C# + 401 JSON. Do not change selections, coverage, rule authority, report bytes, source-read-only boundaries or external copy/edit behavior. Validate readability with the real-size 401-case inventory, keyboard navigation, locale switching, expand/collapse controls, and unchanged execution/report contracts.

## Boundaries

Both suggestions are retained as enhancements to the accepted version. The user reported no defects and no untested items. Existing runtime isolation, policy/deferred scope, package approvals, original versions/configuration/evidence and immutable tags/assets remain protected. Implementation, version selection and a new qualification/release cycle belong to the next authorized work package.
