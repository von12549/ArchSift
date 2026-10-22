# Generic Workbench integration templates

These files are reviewable templates for a new consumer, not an approved production policy or preaccepted baseline. They accompany [project integration](../../docs/project-integration.md). Replace every example path, version-dependent input and zero hash, then validate the resulting project/configuration. Keep an existing consumer's original tests and tools intact.

- `config.example.json`: ordinary analysis and Web configuration; no automatic restore/build.
- `Architecture.Tests.csproj.template`: exact package and MTP runner declarations for a standalone test project. Adapt central package management explicitly if used.
- `dotnet-tools.example.json`: content to merge into `.config/dotnet-tools.json` without dropping other tools.
- `NuGet.Config.template`: one explicit offline source; replace its example path.
- `test-gate.template.json`: external portable schemaVersion 2 configuration; provide reviewed policy/required-case manifests and hashes.

The sample uses stable 0.10.0 as the intended version. For RC qualification, change every product reference and approval identity together to the actual candidate version. No 0.10.0 package is declared published by these templates. See [release status](../../docs/releases/0.10.0/release-status.json).

For an independent small adapter example, see [portable verification](../portable-verification/README.md). Production JSON rules need the consumer's actual reviewed assembly closure, not that synthetic assembly.
