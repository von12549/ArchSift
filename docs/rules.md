# Rules, library and reading projections

JSON is the executable policy authority; Markdown is read-only documentation. Ruleset schemaVersion 1 has `id, version, description, rules, exceptions`. Legacy composed verification requires globally unique rule IDs across all files. Each independent chain entry has its own namespace; summary identity is `(entryId, ruleId)`.

Eight templates in `templates/rules`: project-reference, project-reference-allowlist, graph-integrity, target-framework, nuget-denylist, nuget-allowlist, type-dependency and naming. Review scope, parameters and reasons before using them. The tool does not treat existing dependencies or candidate drafts as approved policy.

Selectors specify `kind, match, value` and optional `allowEmpty`. Exact uses ordinal matching; package IDs are case-insensitive. Glob supports `*` and `?`; paths also support `**` and zero-directory `**/`. Path `*` cannot cross a slash. Regex, character classes, scripts and implicit matching are unsupported. Paths are root-relative slash paths, never absolute or traversal paths. Selectors are limited to 256 characters; matching is iterative.

Scope kind must match rule type; naming follows subjectKind; type dependencies use namespace/type selectors. A project identity is its relative csproj path; project naming uses the filename without extension. File naming checks relative source paths.

Project-reference allowlists check resolved direct references; NuGet allowlists check declared direct PackageReference IDs. Explicit empty allowed arrays deny every dependency of that kind. Multiple allowlists are independently evaluated, yielding an intersection; allow does not override deny. Exceptions target one rule ID and retain the original finding. Incomplete declarations cannot prove pass.

Unknown fields/types, missing parameters, invalid enums, whitespace-only reasons, duplicate JSON keys/IDs and duplicate allowlist values are rejected. Composed TFM/naming policies with no overlap are configuration errors, never last-definition-wins. Zero source matches are inconclusive unless explicitly allowed empty, when they are not-applicable. Overlapping exceptions use stable precedence: exact, more literal characters, fewer wildcards, selector value, exception ID.

## Saved library and chains

Import validates schema and single-file semantics before copying exact bytes into external `rulesDirectory`. Filename or active ruleset-ID conflicts reject without mutation. Export JSON downloads current saved bytes, including a retained UTF-8 BOM. Export Markdown remains a separate reading projection. Neither exports an unsaved draft.

The registry `.archsift-library.json` assigns stable lowercase GUID entry IDs. Saving an existing card retains its entry ID; deleting it leaves a tombstone. Reimporting the same name or policy ID creates a new entry and never reconnects a chain automatically. Reconnection is an explicit chain edit.

Versioned chain documents are stored under `rulesDirectory/chains`. They contain metadata and an ordered array of `{entryId}` references, without inline rules. New entries select existing saved library cards; duplicate references are rejected. Existing dangling references remain editable and verifiable. Each run freezes the latest saved bytes and records exact hashes; reordering changes order only.

Legacy external `config.rulesets` paths remain usable by CLI composition. The workbench shows them as external/read-only and requires explicit Import before selecting them in a chain.

## Validation

`rules validate --file` validates one file, including its local composition and exception references. Legacy multi-file verification validates cross-file exceptions and composition together. `rules render --file --output` creates Markdown with source hash, version, scope, parameters, severity and exception reasons.

Bundled schemas use JSON Schema draft-07. The offline built-in validator implements the fixed schemas' type/const/enum/required/properties/additionalProperties/items/minItems/minLength/minimum/pattern/allOf/oneOf/anyOf/local-$ref keywords. It does not fetch remote schemas or claim full draft-07 support. JSON has a 4 MiB limit and depth 64. Original bytes, including BOM, determine identity; semantic validation is separate.

`scripts/Verify-Contracts.ps1` runs controlled offline build/tests, independent PowerShell Test-Json and real CLI validate/render. Historical schema examples and acceptance records remain unchanged.
