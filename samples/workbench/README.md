# Generic Workbench integration templates

Current-source update: 0.10.1 is a candidate under qualification; final human acceptance and publication are pending. See [portable Workbench](../../docs/portable-workbench.md) for NuGet UI, portable Launcher and rollback. References to published 0.10.0 below retain their historical context.
These files are reviewable templates for a new consumer, not an approved production policy or preaccepted baseline. They accompany [project integration](../../docs/project-integration.md). Replace every example path, version-dependent input and zero hash, then validate the resulting project/configuration. Keep an existing consumer's original tests and tools intact.

- `config.example.json`: ordinary analysis and Web configuration; no automatic restore/build.
- `Architecture.Tests.csproj.template`: exact package and MTP runner declarations for a standalone test project. Adapt central package management explicitly if used.
- `dotnet-tools.example.json`: content to merge into `.config/dotnet-tools.json` without dropping other tools.
- `NuGet.Config.template`: one explicit offline source; replace its example path.
- `test-gate.template.json`: external portable schemaVersion 2 configuration; provide reviewed policy/required-case manifests and hashes.

The current templates use 0.10.1-rc.1 consistently. Stable qualification must update every product reference and approval identity together. These templates do not establish package availability or acceptance. See [release status](../../docs/releases/0.10.1/release-status.json).

For an independent small adapter example, see [portable verification](../portable-verification/README.md). Production JSON rules need the consumer's actual reviewed assembly closure, not that synthetic assembly.
