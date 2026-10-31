# ArchSift 0.11.4 NuGet publication

All five original 0.11.4 packages are public on NuGet.org. [Transaction 41172412457](https://github.com/von12549/ArchSift/actions/runs/41172412457) completed successfully after explicit user publication confirmation, activation PR 50, all 13 PR checks and actual-main CI, a fresh bound official audit and human `nuget-release` environment approval.

Original package source is `b7dd87f7f8d073a3fde46bd7068c8911b832972d`, tree `d6a861dded687955df61645d6fe50bf17d3713f9`. Publishing workflow commit is `a818f941ce2d4ee089b5a7955f0ccbd3e16f7440`. Canonical candidate artifact 13028797577 and every frozen original submission remain unchanged. Later CI builds do not replace these submissions. [publication.json](publication.json) records exact original/served hashes, request/artifact identities, approval and closeout evidence.

| Package | Public version |
|---|---|
| ArchSift.Contracts | [0.11.4](https://www.nuget.org/packages/ArchSift.Contracts/0.11.4) |
| ArchSift.Core | [0.11.4](https://www.nuget.org/packages/ArchSift.Core/0.11.4) |
| ArchSift.ArchUnit | [0.11.4](https://www.nuget.org/packages/ArchSift.ArchUnit/0.11.4) |
| ArchSift.Testing | [0.11.4](https://www.nuget.org/packages/ArchSift.Testing/0.11.4) |
| ArchSift.Cli | [0.11.4](https://www.nuget.org/packages/ArchSift.Cli/0.11.4) |

Sequential submission and the independent hosted public-verification job succeeded. Hosted `dotnet nuget verify --all` checked repository signatures against current official certificates. Separate anonymous downloads independently verified owner `XiaolongFeng`, every original nonsignature payload entry and the repository-signature entry. NuGet adds a signature, so original submission hashes and served archive hashes are recorded separately. No local dotnet operation was used for publication verification.

The integrated original-byte preview and fresh official audit completed before credential acquisition in this same transaction. Independent preapproval downloads verified both artifact ZIPs and all originals before the human gate. The audit covered 12 projects and 36 package identities, with mode `all`, level `moderate`, warnings as errors and fresh official index/base/update data. The prior standalone original-byte preview 41169711834 remains an immutable separate preparation record. The publishing-helper version allowlist was corrected and checked against the active workflow before submission; all 301 release safety tests, 51 workflow mutations, two HTTP mutations and two version-boundary mutations passed.

`ARCHSIFT_NUGET_PUBLISH_0114` was deleted after transaction completion and its original absent state was independently rechecked. Local environment/Profile hashes matched the original baseline. No author-host Guard/dotnet operation, persisted environment repair, tag/GitHub Release, installer or IFX main/live change occurred.

## Accepted scope and next IFX work

The exact original packages passed native packaged consumers on SDK 10.0.303, 10.0.300 and 10.0.403; isolated IFX passed all 493 daily cases on each SDK in order, the formal 493-case lane, an independent 105-project/CLI/Web audit and four actual Agent root-entry UI modes. See the unchanged [qualification](qualification.json) and its six source/package-bound evidence lanes.

The physical secondary-machine build-08 evidence remains immutable and is retained through explicit exhaustive runtime equivalence plus fresh canonical Agent tests. No fresh physical-machine execution of these canonical raw archives is claimed. The passing canonical root UI used fresh explicit owned storage; the shared-default-cache NU1403 failure remains preserved. See [retained coverage](evidence/secondary-machine-equivalence-review.json) and [actual UI evidence](evidence/agent-ui-review.json).

IFX's final public signed-package binding, public-only clone validation, stale private-cache isolation and separate merge/live cutover remain pending. Historical migration/rollback, actual disconnected acceptance and untested future eligible SDKs are outside this publication. Frozen embedded README/release-note bytes retain their candidate terminology; this publication record establishes their final status without replacing package bytes.
