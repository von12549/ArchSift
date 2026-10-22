# W11 allow policies, composition and compatibility decision

Date: 2026-10-07 (Australia/Sydney)

DECIDED / IMPLEMENTED / ACCEPTED. Created before schema/runtime changes; implementation/acceptance later completed, [record](../acceptance/20261007-w11-allowlists-ui.md). Preserve design-time scope/order/constraints, not retrospective decisions.

## Goals and boundaries

W11 adds two closed policies:

- project-reference-allowlist: each resolved direct reference of selected sources must match at least one allowedTargets selector.
- nuget-allowlist: every directly declared PackageReference in scope must be in allowedPackageIds.

Existing project-reference/nuget-denylist retain prohibitions. No inferred transitives/version-range interpretation/online vulnerabilities/automatically generated-enabled policy/unbound-source compliance.

## Contract choices

Keep schemaVersion 1. Add optional union branches without changing old requirements/defaults/meaning, no old JSON migration. Six templates become eight; old names/content remain readable.

project-reference-allowlist parameters:

```json
{
  "source": { "kind": "project", "match": "glob", "value": "src/**/*.csproj" },
  "allowedTargets": [
    { "kind": "project", "match": "glob", "value": "src/Contracts/**/*.csproj" }
  ]
}
```

allowedTargets is selector union within a rule. Explicit empty means no resolved direct refs allowed. Both source/scope must match; zero sources follows allowEmpty inconclusive/not-applicable.

nuget-allowlist parameters:

```json
{
  "allowedPackageIds": ["Microsoft.Extensions.Logging.Abstractions"]
}
```

IDs use OrdinalIgnoreCase; reject case-variant duplicates, normalize finding targets lowercase. Explicit empty forbids direct packages. Central versions supplement facts, not allowedness; incomplete declarations cannot pass.

## Composition

Every enabled rule independently evaluates; all applicable constraints hold together. File/load/severity/UI order gives no precedence or last-wins.

| Combination | Fixed outcome |
| --- | --- |
| One allow, dependency in set | pass unless coverage limits make inconclusive |
| One allow, outside set | violation finding |
| Multiple applicable allows | Independent, effective intersection; any rejection violates |
| Empty intersection | Valid/deterministic prohibition, not config error |
| Broad allow/narrow deny | Deny remains, no allow override |
| Both reject same dependency | Keep both rule-ID findings |
| Allow accepts/deny rejects | Deny retained, overall noncompliant |
| Deny exempt/allow rejects | Exempt deny only, allow still noncompliant |
| Allow exempt/no deny | Retain exempt finding ID/reason, rule may pass |
| Different severity | Finding severity only, no precedence/compliance change |
| Duplicate ruleID across files | Existing configuration error |
| Existing incompatible TFM/naming | Existing configuration error |
| Allow/deny overlap | Valid independent rules/exceptions yield deterministic result |

Sort results/findings by stable ID. Ruleset identity retains each file byte hash; provenance may include order but ruleResults/findings/compliance/SARIF sets cannot change.

## Incomplete inputs and zero matches

- Judge project allow only for resolved direct edges with target projectID.
- ReferencesComplete=false/unresolved/external/unsupported yields limitation, never pass solely because known edges allowed.
- Only declared direct packages; PackagesComplete=false/uncertain declarations limit coverage, unknown never allowed silently.
- Existing in-scope projects with no dependencies can pass if declarations complete; distinct from zero source/scope.
- Q11 zero source/scope defaults inconclusive; explicit selector allowEmpty=true gives not-applicable.

## Unique exception attribution

Exceptions act only on their ruleId. Multiple matches no longer choose file-first; choose first stable ordering:

1. exact before glob;
2. More nonwildcard literal characters first;
3. Then fewer wildcards;
4. Then selector value Ordinal;
5. Then exception ID Ordinal.

Exception/file permutations cannot change selected ID/reason. No-overlap behavior unchanged; overlap compliance unchanged, attribution changes from unspecified input order to stable selection. Record in W11/release notes, never claim old byte stability.

## Reports and UI

JSON authority. HTML/SARIF retain ruleID/severity/source-target/exception ID-reason/limitations, no special type mislabeling allow as deny. English meanings of localized Chinese UI labels:

| Machine field/type | Localized label meaning in English |
| --- | --- |
| project-reference-allowlist | Project-reference allowlist |
| nuget-allowlist | Direct NuGet package allowlist |
| partial | Partially completed |
| inconclusive | Cannot determine |
| not-applicable | Not applicable |
| noncompliant | Current rules violated |

Continuously show product/actual root/entry/rules/output/build/TFM/config. Run confirmation cannot rely on mutable forms. Large graphs default summary, filter/collapse details without hiding actual counts/limitations/status.

## Compatibility and sequence

1. Commit decision/mark W11 IN PROGRESS.
2. Add schema union/two templates; byte/semantic-regress six old templates.
3. Implement allows/stable exceptions; never short-circuit deny.
4. Add permutation/intersection/allow-deny/exception/zero/incomplete fixtures, all-format/CLI-UI parity.
5. Then context/large-result/terminology/lifecycle UX, unchanged Core semantics.
6. After automation, reviewed human command blocks on read-only real IFX before W11 completion.

## Minimum test matrix

| Group | Required coverage |
| --- | --- |
| Legacy | Six templates/rules/results unchanged, unknown fields/types rejected |
| Project allow | exact/glob/in-out/empty/zero source/no refs/incomplete-unresolved |
| NuGet allow | case/in-out/empty/central/incomplete/direct only |
| Composition | intersection/broad allow-narrow deny/double reject/severity |
| Exceptions | separate allow-deny/cross-rule isolation/overlap permutations |
| Multiple files | rule-file permutations same semantics/duplicate ID-dangling exception errors |
| Projections | Markdown/JSON/HTML/SARIF/CLI/UI types/attribution agree |
| UX | Real-fixture identity/105 projects/filter-collapse/desktop-narrow/normal-cancel-error-duplicate lifecycle |

At decision time no schema/product/assets changed, no Guard/IFX run, no W11 acceptance claim.
