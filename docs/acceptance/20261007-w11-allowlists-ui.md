# W11 allow policies and large-result UI completion

Date: 2026-10-07 (Australia/Sydney). COMPLETE / PASS.

Over published/verified 0.2.0, W11 delivers project/package allowlists, deterministic allow/deny/exception composition, large-result hierarchy/auditable UI lifecycle. W11 closes without rewriting 0.2.0 assets or claiming new commit/push/release/next-package authorization.

Design authority: [W11 decision](../decisions/20261007-w11-policy-composition.md). JSON remains rule/report authority; Markdown/HTML/SARIF/UI projections.

## Scope

- schemaVersion 1 additive union adds project-reference-allowlist/nuget-allowlist; six old rules/templates readable, eight JSON/Markdown templates.
- Project allowlist checks resolved direct references for selected sources. NuGet checks declared direct PackageReference with OrdinalIgnoreCase IDs. Empty allowed sets valid closed policy.
- Independent applicable rules; multiple allows intersect, allow never overrides deny. Unique overlapping-exception attribution orders exact/literal length/wildcard count/selector/exception ID.
- Incomplete declarations/references remain coverage limitations, unknown never silently allowed. Zero matches remain inconclusive or explicit allowEmpty not-applicable.
- All projections retain IDs/severity/source-target/exception ID-reason/limitations. HTML/UI show exception attribution.
- Each job freezes/display target identity (real/fixture), root/entry/rules/output/build/TFM/config. Distinct run conclusion/rule conclusion/violation evidence/coverage boundary plus project-reference inventory. Filter/collapse large details while retaining limitation count/partial-inconclusive effects.
- Launcher distinguishes session/final JSON, keeps parent PowerShell attached through service exit and provides authenticated loopback/shutdown/PID-port evidence.

## Automated gates

Final evidence D:\ArchSift-lab\runs\development-53b4733726dc4624a6a46b063d9f4997:

| Item | Result |
| --- | --- |
| SDK/config | .NET10.0.303/Debug |
| Restore/build | Explicit offline, 0 warnings/errors |
| Unit |97/97|
| Integration |31/31|
| Architecture |2/2|
| Total |130/130, 0 failed/other|
| Templates | Eight independent validate/render, 16 checks |
| Host | User/Machine environment/Process PATH/four Profiles equal each step/final |

Coverage: old contracts; exact/glob/empty/zero/incomplete allowlists; case/central versions; allow-deny intersection/double rejection/severity; cross-rule exception isolation; overlapping-exception/file/rule permutations; all projections. No old-rules migration. Overlap compliance unchanged, but attribution changes from unspecified input order to frozen selection, explicitly recorded compatibility change.

Desktop/480px evidence D:\ArchSift-lab\runs\w11-ui-hierarchy-visual-check. desktop.png SHA-256 `dce77610deb1a6e06d1476933ce15636751c64fb4ff1e4b33de7f30416bcfa9f`, narrow.png `80f2906f07ee398e6fe4fa0d01766e0726a2bcac9622029eafbcb060facf98e8`. Narrow scrollWidth==clientWidth==465, no overflow. Synthetic, not real IFX human review.

## Real IFX CLI

[Field task](w11-ifx-allowlist-task.md). Accepted D:\ArchSift-lab\runs\w11-ifx-allowlists-3ba4055f16ab446ba47652515c47d8d3\operator-result.json:

- D:\IFX-10-Root\IFX-New/IFX.sln, HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`, existing/net10.0/Debug; before-after HEAD/status equal.
- exit 4/partial/noncompliant, 105 projects/0 assemblies/0 executionerrors.
- w11-ifx-project-allowlist finds IFX.Application.Primitives.csproj -> IFX.Domain.Primitives.csproj.
- w11-ifx-nuget-allowlist finds fluentvalidation.dependencyinjectionextensions direct package.
- Fixed JSON/HTML/SARIF; saved JSON equals stdout, both rules in projections. No IFX build/restore/application.

## Real IFX UI and independent operator

First human review correctly found 105 limitations still expanded despite collapsed projects, with inadequate distinctions between inventory/results/findings/coverage. Retain NEEDS FIX. Four semantic sections/separate project inventory then implemented, retaining totals/effects/on-demand details.

Accepted D:\ArchSift-lab\evidence\w11-ifx-ui-a0d97ac98b5541748d877e41698adbe0\final-result.json, pass:

- Operator confirmed context/real-target label/105 projects/0 assemblies/partial-noncompliant-exit 4.
- Two rules/findings, limitation counts, 105 default-collapsed limits, project collapse/filter and three downloads confirmed.
- Narrow screen readable/no horizontal scrolling; inventory/results/status/severity visually distinct.
- Independent operator L1/confidence 4/5, translated observation: much clearer page, different information in separate areas with different colors.
- CLI/UI core parity; SARIF validates official OASIS schema SHA-256 `ad6db49878699b091f3eeb765b6e29e92a34bad4da88664d000c923b549c3a25`.
- Safe shutdown, recorded PID/port absent, launcher exited; product/target/host equal.

Retain failures/misjudgments: unobserved session, cross-shell PID query falsely absent, self-matching finalizer. Final audit found abandoned PID 34900 listening 4435; confirmed exact session/path/name/command then terminated only that PID, port 0. Lifecycle defect/cleanup history does not rewrite final UI semantics.

## Final safety audit

D:\ArchSift-lab\evidence\w11-final-safety-audit-1c8087f725b645678ee8776c27e8b963\audit-result.json, pass:

-20 automated gates/130 tests/eight template validate-render/CLI-UI/report hashes/SARIF parity/official schema passed.
- Product HEAD `6330951f833537c13bd6f04c293cc4dfb951e58b`; W11 dirty state unchanged since human session, git diff --check passed.
- IFX HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`, clean; no build/restore/application.
- User/Machine environment/Process PATH/four Profiles individually equal.
- Zero known UI identities/ports/candidate Web/launchers/SDK containers; Docker query successful.
-24 key materials in critical-evidence-manifest.json SHA-256 `d44d7fbc0a3c147056fb510a79bfb71e1053f86d89c982e8663b6ba0c81db77c`. Failed audits/human sessions/raw reports retained; no broad cleanup.

## Boundaries

- IFX declarations/existing, 105 limitations/0 assemblies/partial accepted conservative outcome, not full source/assembly compliance.
- NuGet allowlist only direct IDs, no transitive/version/vulnerability checks; project allowlist only resolved direct refs.
- No fail-on/required checks/remote governance/automatic approved policy or Guard-IFX/persisted-host modifications.
- Source/docs uncommitted/unpushed/unpublished at original record time; 0.2.0 assets unchanged.

## Later publication

User separately authorized commit/push/release after completion. Source `d1b1d7c325f02c1f1b81abda376e9463de71ea78` published [v0.3.0](https://github.com/von12549/ArchSift/releases/tag/v0.3.0). Windows/Ubuntu 130/130 each, ZIP SHA-256 `0202e9734ec990474c415d4152274db93337a768e51837ef968fc4b539e44c79`; downloaded package smoke passed. [Publication](../releases/0.3.0-publication.md). Subsequent status supplement, not rewritten acceptance timing.

## Conclusion

Design/implementation/regressions/real IFX CLI/independent UI/final zero-residue audit satisfy W11 COMPLETE/PASS. Later authority used for 0.3.0, no new work package authorized/started.
