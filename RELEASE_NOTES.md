# ArchSift Workbench 0.10.0

Published and download-verified on 2026-10-23. The [publication record](docs/releases/0.10.0-publication.md) binds the release to source `f082894`, all 11 attachments, tagged source archives and successful public-download usage. [Final acceptance](docs/releases/0.10.0/acceptance.json) records the user's 4/5 review, no defects/untested items and both verified Safe shutdowns. The original bundled notes retain their preparation timing; accepted assets remain unchanged.

This release brings the existing packaged architecture-test workflow and local Workbench under one version. The display name is ArchSift Workbench; package IDs and the `archsift` command remain unchanged. CLI, Web, Setup and the four public libraries use the same release version. Release candidates include their complete suffix and are built separately from stable artifacts.

The Architecture tests workspace runs dedicated C# architecture tests and enabled JSON type-dependency cases through the shared gate. Portable consumers use reviewed logical case identities, pinned tool/package inputs, explicit offline sources and external reports. Full, subset, cancelled, incomplete and failed outcomes remain distinct. The separate evidence workflow handles reviewed source-bound assemblies; a daily test pass does not replace it.

The delivery includes a self-contained Windows x64 CLI/Workbench ZIP, separate Setup, four library packages, one CLI tool package, a minimal generic offline feed, English configuration/use guides, templates, schemas, acceptance metadata and checksums. It excludes the private IFX package feed and machine-specific IFX configuration. Exact assets and hashes are recorded in the publication record.

The product targets .NET 10. Analysis supports SDK-style C# net8/net9/net10; target builds require the target's approved SDKs, packs and dependencies. The portable daily gate currently requires SDK 10.0.303, net10.0 and xUnit v3 4.0.1/Microsoft.Testing.Platform. Launching the self-contained Windows payload does not require an installed .NET runtime; the exact assets passed independent Windows guest qualification.

Formal documentation is English. The existing English/Chinese UI switch remains available. No new report-schema version is introduced solely for the product version change.

The live IFX baseline remains 0.10.0-cl03.1: 493 cases (92 C# + 401 JSON), 404 responsibilities and 89 production assemblies, with A179 and five B07 items deferred and three A responsibilities retained in C#. The separate final stable external-snapshot qualification passed the same 493 cases and a 105-project official audit; live IFX remains unchanged. Publication does not upgrade IFX or adopt new policies. ArchSift.InProject remains retired.

Final-source CI, package closure, vulnerability/license review, external IFX regression, Web/CLI parity, lifecycle, independent Windows and user acceptance passed for the exact stable bytes. Binaries are not Authenticode signed. Unsupported platforms and measured limitations remain explicit in final acceptance; no minimum-Windows certification is claimed.

Start with [getting started](docs/getting-started.md), [project integration](docs/project-integration.md), [installation](docs/installation.md) and [Setup](docs/setup.md). Preserve older releases and their evidence; do not rename a candidate into a stable package.
