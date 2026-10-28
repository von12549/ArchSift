# ArchSift 0.11.2 NuGet publication

The five frozen original submissions are publicly available on nuget.org. [Publication run](https://github.com/von12549/ArchSift/actions/runs/40669066494) completed successfully after explicit user authorization, normal activation PR/main CI, fresh bound audit and human nuget-release environment approval.

Package source: `91cd73adc85a4da8f79fffa04ad834c870d78d07`. Publishing workflow commit: `ea6ef0d3ab9b3de9aff2c7de3385b71dc60532a0`. These are deliberately distinct; later evidence/documentation builds are not replacements for the published submissions.

All five public packages passed the hosted repository-signature and owner checks. Independent anonymous downloads match every nonsignature payload entry in the frozen manifest. NuGet adds the repository signature, so served archive SHA-256 values differ from original submission values. `publication.json` is the machine-readable identity record.

| Package | Public version |
| --- | --- |
| ArchSift.Contracts | [0.11.2](https://www.nuget.org/packages/ArchSift.Contracts/0.11.2) |
| ArchSift.Core | [0.11.2](https://www.nuget.org/packages/ArchSift.Core/0.11.2) |
| ArchSift.ArchUnit | [0.11.2](https://www.nuget.org/packages/ArchSift.ArchUnit/0.11.2) |
| ArchSift.Testing | [0.11.2](https://www.nuget.org/packages/ArchSift.Testing/0.11.2) |
| ArchSift.Cli | [0.11.2](https://www.nuget.org/packages/ArchSift.Cli/0.11.2) |

The fresh official audit covered 12 projects and 36 package identities with all/moderate warnings-as-errors. The version-specific operational switch was closed after the successful transaction, restoring its original absent state. The 0.11.0 switch remains closed.

Scoped Windows qualification and the explicit final Workbench/human repeat waivers remain as recorded in `qualification.json`; historical answers are not relabeled as final-byte reruns. Full disconnected MTP is not verified or claimed, and Ubuntu disconnected qualification remains deferred. No tag/GitHub Release, installer or live IFX adoption was performed. No Guard or dotnet command was executed on the author host.
