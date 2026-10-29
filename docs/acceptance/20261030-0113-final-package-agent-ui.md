# Final 0.11.3 package consumer and delegated Agent UI acceptance

The [JSON record](20261030-0113-final-package-agent-ui.json) is authoritative. The user explicitly delegated the UI review to Codex on 2026-10-30. Independent human UI acceptance was skipped; no human usability score or human execution is claimed.

The tested source is `af25bed0da8b3cedf5706a404dd3f33a8f7b45df`, tree `8d862be384a63806ec76c23c5c4ee604f94321e9`. Main candidate run `40922246528` / artifact `12918368591` is bound by archive SHA-256 `a9a153660aef63569c57e68af39376ee060fd5a2c577e96b25cc9321e850dc32`. Five exact unsigned original package archives were independently checked before use. They were not rebuilt for UI acceptance.

| Executed scope | Result |
| --- | --- |
| Fresh packaged synthetic consumer, normal tool restore, locked gate | PASS; four cases, no failures/skips, no product project references |
| Installed-host/configured-source CLI workspace Init | PASS; 0.11.3, external state, daily test configuration absent |
| Doctor | PASS; ready, initialized, not busy |
| Launcher and selected child Workbench, first and repeat | PASS; correct version, target and configuration paths; safe closure |
| Direct Workbench, first and repeat | PASS; same paths; safe closure |
| Product closeouts | Four exits 0 with host equality |
| Reuse | Five initialization receipts reuse state/startup paths; three configuration file hashes unchanged |
| Host and processes | Normal/execution baselines equal; all 13 recorded process IDs exited |
| Release guard simulations | 242 passed, including 16 added delegation acceptance/rejection cases |

The observed host was Windows x64, SDK 10.0.303, Core/ASP.NET 10.0.11, with Release/net10.0 synthetic fixtures. Host mode intentionally uses installed eligible runtime patches. The test adapter's older exact-runtime metadata is not an assertion that runtime 10.0.13 ran.

Agent UI coverage includes version/path rendering, the external configuration context, the read-only ruleset view, an empty history without stale success, selected-profile navigation, safe shutdown, and repeated startup. Screenshots and accessibility snapshots are retained. Long paths wrap; the narrow browser viewport uses horizontal section navigation. Full editing, chain execution, exports, all locales, accessibility and subjective human usability were not covered. No UI architecture run was triggered.

## Retained probe and contract limitation

`WorkspaceInit-01` failed before product dispatch because the generic packaged `.archsift/start.ps1` still requires exact/offline IFX preparation. Its exit 1 and host-equal receipt are retained. The IFX host integration contract uses its own repository entry to restore/verify exact packages and then dispatch CLI workspace directly. `WorkspaceInit-02` and subsequent lifecycle checks exercised that supported contract. No product guard was relaxed or template silently patched. Generic-template IFX host/configured support is not accepted by this result.

The first feed-preparation attempt incorrectly compared raw archive SHA-512 with NuGet's declared lock-content hash. Those identities were checked separately in the corrected preparation, followed by actual fresh locked restore. Both preparation histories remain retained; this was a harness comparison error, not a product restore success or package-tampering finding.

## Evidence and remaining work

The external evidence root is recorded in JSON. `acceptance-evidence.zip` contains 106 reviewed files, per-file hashes, screenshots, product receipts, host equality and test output. Archive SHA-256 is `cc2d3b53b4e120afee3f0f3023f2249b48b759c941c08613122396d1fc5602a1`. Archived expired session fragments are redacted; the manifest retains both original and archived hashes. Original evidence remains unchanged outside the archive.

This is scoped machine/Agent acceptance, not publication approval or complete release readiness. The release guard change is an uncommitted follow-up; its source identity differs from the frozen product identity. Review/CI, final reference/qualification binding, fresh dependency audit and credential-free preview remain next. Publication switches and the active workflow are unchanged. Human release-environment approval remains mandatory.

Real IFX architecture execution, final public-only cold clone/restore, repository-signed served packages, migration/activation, full disconnected execution and Ubuntu acceptance are not claimed. Optional offline user delivery remains skipped. Active IFX stays at 0.11.0; accepted RC evidence and original package bytes remain immutable.
