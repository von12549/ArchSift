# ArchSift 0.11.3 NuGet publication

All five original 0.11.3 packages are public on NuGet.org. [Transaction 40980841068](https://github.com/von12549/ArchSift/actions/runs/40980841068) completed successfully after explicit user authorization, activation PR 45, passing PR/main CI, a fresh bound official audit and human `nuget-release` environment approval.

Original package source is `af25bed0da8b3cedf5706a404dd3f33a8f7b45df`, tree `8d862be384a63806ec76c23c5c4ee604f94321e9`. Publishing workflow commit is `cde8fbaf4048d7ec6f9807ac0a339315a5acfc65`. Candidate artifact `12918368591` and the frozen submission hashes remain unchanged. Later CI builds do not replace these submissions. [publication.json](publication.json) is the machine-readable final identity record.

| Package | Public version |
| --- | --- |
| ArchSift.Contracts | [0.11.3](https://www.nuget.org/packages/ArchSift.Contracts/0.11.3) |
| ArchSift.Core | [0.11.3](https://www.nuget.org/packages/ArchSift.Core/0.11.3) |
| ArchSift.ArchUnit | [0.11.3](https://www.nuget.org/packages/ArchSift.ArchUnit/0.11.3) |
| ArchSift.Testing | [0.11.3](https://www.nuget.org/packages/ArchSift.Testing/0.11.3) |
| ArchSift.Cli | [0.11.3](https://www.nuget.org/packages/ArchSift.Cli/0.11.3) |

Sequential submission and the independent hosted public-verification job succeeded. Hosted verification checked repository signatures against current official repository certificates, gallery owner `XiaolongFeng` and every original nonsignature payload entry. Separate anonymous downloads independently matched every payload entry and owner; their archive hashes and signature-entry hashes are recorded. NuGet adds a repository signature, so served archive hashes differ from original submission hashes. No local dotnet operation was used for publication verification.

The fresh official source audit covered 12 projects and 36 package identities with Audit mode `all`, level `moderate`, warnings as errors, and official index/base/update data. PR 45 and its actual-main CI each passed 13/13 checks. The original-package preview run `40962973691` remains a separate successful preparation record. Local environment/Profile hashes matched the original baseline at completion.

`ARCHSIFT_NUGET_PUBLISH_0113` was deleted after transaction completion and its original absent state was verified by readback. The committed publication capability remains available only behind the closed operational switch, protected approval and exact fresh transaction/audit checks. No tag/GitHub Release or installer was created.

The user explicitly delegated UI acceptance to Codex and skipped independent human UI review; no human UI pass or score is claimed. Scoped actual Agent coverage and exclusions remain in [qualification.json](qualification.json). The generic package startup template still requires IFX exact/offline preparation; the supported IFX installed-host/configured-source integration restores and verifies exact packages, then dispatches CLI workspace directly. Ordinary workspace startup does not run architecture tests.

Active IFX remains at 0.11.0. Final public IFX pinning, fresh-clone acceptance, real IFX architecture execution, migration and activation remain separate. Optional offline user delivery remains skipped; full disconnected and Ubuntu qualification are not claimed.
