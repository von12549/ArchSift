# LT06 independent human visual and hands-on checklist

**Initial candidate needs revision; revised independent UI passed; local LT06 accepted, not released.** The first table records explicit user results/questions; the final table records five later confirmations. Initial source `e0b3bddabefd4561ca9ddfc13ed68e7d91a4f5b0`, manifest SHA-256 `4a19cfd8ae57464d4b6a010dec9a2b923ab1e5b5aa5bf287b3b308fc2d768212`; revised manifest `34147699cb67d5f24d68e48de93152208ca75519fefbea38df66dc2376443c41`. See [machine record](20261016-lightweight-lt06-ui-revision.md) and [guide](../lightweight-test-workbench-candidate.md). Review uses only authorized external D:/ArchSift-lab/fixtures/lt05-shared-cc06528 and external rules/reports; original checkout/install remain untouched.

Reviewer: user via chat; formal confirmation pending. Date/zone: 2026-10-16, Australia/Sydney. Browser: Chrome 155.0.8059.40. Machine manifest hashes match; reviewer confirmation pending.

| Item | Human task/criterion | Result | Notes/evidence |
| --- | --- | --- | --- |
| Startup/session | Open local candidate URL; readable layout without obstruction; keep token private | User pass | Observations as expected. |
| Three sources/scope | Distinguish original C#, JSON and excluded policies; 80 loaded IDs are not execution | User pass | Observations as expected. |
| Rule editing | Copy externally, read/validate/save; original C# read-only, unsaved drafts excluded | User pass | Smooth and clear, explicit read-only/draft prompts; full run uses saved bytes. Copy: D:/ArchSift-lab/runs/lt06-candidate-e0b3bdd/web-rules/test-feabc9a9.json. |
| Full run | Observe progress/current 80+1, no skips/missing cases, allow/0; 588 excluded never passed | User/machine pass | Actual draft-boundary run agrees. |
| Subset/cancel | Actual discovery UID, diagnostic-only subset, cancellation never shows old success | Function expected; usability needs revision | block/4 and cancelled/block/130 correct; UID available only in discovery.log. |
| Failure | Distinguish JSON findings/native assertions; block/2 retained after restoration | User/machine pass | 80 originals passed, one JSON failure/finding; restored allow/0, old failure separate. |
| History/downloads | Distinguish success/failure/cancel/freshness; current downloads, missing materials unavailable | User pass | New success/old failure separate; zero-test block/3 is error. Missing report gives reason and no view/download. Exact old freshness wording unspecified. |
| Full evidence | Separate workflow; daily allow is not full evidence proof | User pass | Entry/scope as expected. |
| Usability | Text/tables/keyboard/wait/error/narrow layout | Keyboard/narrow expected; experience needs revision | Missing UID/English; results below history; separate from global Results/History. |

## Received human feedback (2026-10-16 Australia/Sydney)

User confirmed navigation tasks 1/2/3: expand 588 exclusions and download formats; given actual UID yields nonrelease subset; restart/cancel without stale success. These confirmations do not pass unspecified items.

UID issue: result table shows CTRF execution ID while input needs MTP discovery UID; they are not interchangeable. Only discovery.log exposes the needed UID. Reproduce by completing a run, viewing results, switching to subset and looking for a usable UID. Missing UI access prevents human usability pass.

User confirmed smooth copy/read/validate/save and clear text. Read-only external-copy SHA-256 `6d6bbf68d2bbbd80579167df911a647c91421b591a1d3edf8dd06ec6ab5233e8`; generated gate config points to it.

User confirmed original C# read-only labeling and “Unsaved changes; Verify uses saved bytes.” Full gate with an unsaved negative draft behaved as expected. User explicitly passed startup and source/scope tasks. Report 20261015T144526626-cdf6d9e9 is full/allow/0, complete, 80+1 executed/passed, zero skips/missing, 588 excluded. Saved SHA-256 remains `6d6bbf68d2bbbd80579167df911a647c91421b591a1d3edf8dd06ec6ab5233e8`, consistent with draft exclusion.

User's negative run: 80 C# passes, one JSON failure, explicit IFX.Platform.Context.Contracts.EndpointScopeMetadata → IFX.Platform.Context.Contracts.ExecutionScope. Report 20261015T145020201-5f829ca2 is full/complete/block/2, zero skips/missing, 588 excluded; JSON violation, matched=1, one actual finding. This ArchSift/ArchUnitNET result is distinct from native assertions. Negative-rule SHA-256 `8cf3119c6561ec2ae4e8593e65eca9779bfb60fa8df78784b8e3c5a2f39ddfde`.

User confirmed restored run finished/allow/exit 0. Rule SHA-256 restored to `6d6bbf68d2bbbd80579167df911a647c91421b591a1d3edf8dd06ec6ab5233e8`; report 20261015T145402028-e312cbe2 is full/complete/allow/0, 80+1 executed/passed, zero skips/missing, 588 excluded. Old block/2 directory remains.

User confirmed new success and old failure remain separate, view/freshness/prior zero-test block/3 error are as expected, and full-evidence workflow is separate from daily allow/0. Exact old-failure freshness wording was not supplied.

An explicitly synthetic temporary history record lt06-ui-missing-artifact-probe-20261016 under external web-output/test-runs claimed finished/allow/0 without JSON/HTML. User observed “Authoritative JSON or HTML report is missing.”, unavailable state, no view/download and expected narrow layout. After inspection it was precisely removed, probeRemoved=True. This was not a real gate run.

User confirmed keyboard operation and Chrome 155.0.8059.40. Overall feedback, translated: generally OK, but small issues need revision; no final pass. Other findings: Chinese-only architecture page, results below history requiring long scrolling, and separation from global Results/History. User requested redesign, fixed new identity and affected machine/independent rechecks.

Initial overall human conclusion: NEEDS REVISION, based on user feedback; new candidate review pending then.

Reproduction: subset needs discovery UID while table shows execution ID; English switch leaves architecture page Chinese; history viewing appends results below list; global Results/History omit architecture tests.

Reviewer signature:________

## Revised candidate independent review (user confirmed)

Revised review: D:/ArchSift-lab/packages/lt06-ui-revision-20261016-a; config/reports D:/ArchSift-lab/runs/lt06-ui-revision-20261016-a. Machine Web gate 80+1 allow/0. User confirmed each of five items below, without automatic carryover. Revised browser/version/pixel width were not supplied; initial Chrome identity is not reused.

| Review point | Task/observation | Human result | Notes |
| --- | --- | --- | --- |
| UID | In subset mode, search case and select Use this UID; distinguish discovery/execution; no IFX rerun needed | User pass | Visible and automatically filled. |
| Languages | Switch English/Chinese in tests, Results and History | User pass | Correct and readable. |
| Result location | Open report in Results without scrolling past history | User pass | Results and History separate. |
| Global integration | Switch analysis/test views, open history report, find existing analysis workflow | User pass | Page relationships clear. |
| Narrow/keyboard | Search, view switches and history without obstruction/lost focus | User pass | Narrow layout and keyboard normal. |

Revised overall human PASS: all five explicitly confirmed. Reviewer/date: user via chat, 2026-10-16 (Australia/Sydney). Revised browser/version/window pixels not provided.

Local LT06 machine and independent UI gates are complete for this fixed manifest. Product changes or repackaging require new identity and affected rechecks. PR, merge, publication and IFX policy adoption remain separate decisions.
