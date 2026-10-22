# ArchSift Workbench unified release plan

Date: 2026-10-22 (Australia/Sydney)
Status: EXECUTION AUTHORIZED / WB01–WB02 COMPLETE / WB03 IN PROGRESS / RELEASE NOT ACCEPTED OR PUBLISHED
Product: ArchSift
Workbench name: ArchSift Workbench
Proposed stable release: 0.10.0
Local candidate sequence: 0.10.0-rc.1, then additional numbered candidates if required

Execution update, 2026-10-23: the user authorized this complete plan, including issue fixes, commits/pushes, PRs, CI, merges and publication. The [frozen execution contract](../releases/0.10.0/release-contract.json) and [current ledger](../releases/0.10.0/release-status.json) supersede the planning-only restrictions below. Final independent human UI review remains user-owned; live IFX adoption and registry publishing remain excluded. No duplicate general publication permission is required. Historical planning observations and original authorization boundaries below retain their 2026-10-22 timing.

The machine-readable planning authority is [the release assessment](../decisions/20261022-workbench-release-assessment.json). This document projects its observations, gap register and work-package dependencies. It records a proposed release route, not product acceptance or authorization to execute that route.

## 1. Original planning authorization and scope

The current request authorizes this local English plan, read-only version/readiness checks, an implementation-plan status entry and persistence of the documentation-language policy in AGENTS.md. It does not authorize product changes, version stamping, translation batches, builds, installations, real IFX runs, CI dispatch, staging, commits, pushes, PRs, merges, tags, package uploads or releases. Earlier permissions for completed work packages do not authorize this release.

Future execution must name the authorized work packages and operations. A package's completion does not start its successor automatically. Prepare reviewable outputs before requesting any final publication approval. The final independent human visual and hands-on review belongs to the user.

The release covers the existing packaged architecture-test workflow, loopback Web workbench, CLI and shared libraries. Preserve the frozen Q01-Q14 decisions, W00-W10 dependency boundaries, six rule types, real ArchUnitNET dependency checks and shared Core/Contracts behavior. Keep daily test gates and full evidence workflows distinct. The retired ArchSift.InProject route stays retired. A179, five B07 items, the three C# responsibilities and retained materials keep their current decisions; release preparation does not adopt additional IFX policies.

## 2. Read-only baseline and corrected historical interpretation

| Object | Observation on 2026-10-22 | Meaning for release planning |
| --- | --- | --- |
| ArchSift local HEAD and remote main | 7964e425aa611b1d32a9a16bfa05e94d93bd538a; remote tree f4c2686d2ef9b536a06d7f72ac8250445a6c21f8 | A planning reference, not a frozen release commit. Remote main is currently unprotected; no protection changes were made. |
| Existing working materials | Three modified tracked paths and 1,439 untracked paths before this plan; staged diff empty | Do not include the whole worktree in a release or require deleting unrelated materials. Select exact reviewed inputs later. |
| Product source delivery | PR #9 delivered 43 source/validation files to main 3f61be0; PR #10 delivered the two documentation updates to 5e6c4cd | Source delivery is already complete. The remaining task is final release-commit/build provenance, not repeating the closed source-archive package. |
| Current documentation delivery | Remote main 7964e4 follows PR #14, which synchronized the 493-case integration description | Current tracked documentation is present remotely; today's language policy still requires a migration. |
| IFX integration | Recorded main b591b9f2ffca5d0bbbfb1f9352a56870fadf927c; 0.10.0-cl03.1; 251-package approved source | Existing accepted integration, not automatic adoption of a new version. This planning round did not rerun IFX or independently revalidate its live checkout. |
| Latest IFX evidence | 493/493 = 92 C# + 401 JSON; 404 responsibilities; zero skipped; independent official audit 105/105; 89 production assemblies | Reuse as a comparison baseline. These numbers must not become results for unbuilt 0.10.0 artifacts. |
| CL03 packages and Web | Four libraries, one CLI package, matching framework-dependent Web ZIP; 250 frozen source inputs | Existing immutable candidate artifacts. Their original source-byte provenance remains valid; no claim of an exact compiler-input closure or byte-reproducible build is added. |
| Historical human/independent-machine evidence | FA05 human review accepted its 97-case candidate; SP04 human feedback accepted its rule-list repair; SP03 independently checked its older portable candidate on Windows | Preserve completed evidence. None is a complete human/lifecycle acceptance of the final Workbench 0.10.0 bytes. |
| CL03 Web evidence | Eleven production-renderer checks and wide/narrow screenshot review; no real HTTP session or final independent human review for that exact ZIP | The workbench has useful tested behavior, with final-package gaps remaining. |
| Default development version | Directory.Build.props still declares 0.9.0-dev; CL03 used explicit packaging overrides | Introduce one reviewed release-version input later; do not relabel existing artifacts. |

Primary local evidence:

- [Source archive closeout](../acceptance/20261021-source-archive-final-completion.md).
- [Documentation delivery closeout](../acceptance/20261021-source-archive-doc-final-completion.md).
- [CL03 packaging and synthetic checks](../acceptance/20261021-cl03-candidate-packaging.md).
- [CL05 closeout](../acceptance/20261021-cl05-final-completion.md), interpreted together with its later source-delivery successors.
- [Latest 493-case IFX acceptance](../acceptance/20261022-b03rest-portable-activation.md).
- [FA05 human acceptance](../acceptance/20261017-fa05-human-ui-completion.md).
- [SP03 independent Windows verification](../acceptance/20261020-sp03-completion.md).
- [SP04 human repair confirmation](../acceptance/20261020-sp04-ui-human-confirmation.md).
- [InProject retirement](../acceptance/20261022-inproject-retirement.md).

The assessment records the SHA-256 of these evidence documents as read. They remain historical records; this plan does not rewrite their outcomes.

## 3. Version availability and unified identity

Read-only GitHub queries returned eight tags and eight releases, ending with the source-only v0.9.0-dev prerelease. The latest stable release in that inventory is v0.8.0. The releases second page was empty. Neither the tag collection nor the matching-ref queries contained a 0.10.0/v0.10.0 prefix.

NuGet.org flat-container indexes for ArchSift.Contracts, ArchSift.Core, ArchSift.ArchUnit, ArchSift.Testing and ArchSift.Cli each returned HTTP 404 in the successful network-enabled retry. This is no observed public package-index collision; it does not reserve package IDs, establish ownership or guarantee publication rights. The three reviewed local candidate/current/previous feeds contain only cl03.1/sp03.1 prerelease packages, with no exact stable 0.10.0 package in those directories.

GitHub Packages enumeration returned HTTP 403 because the current credential lacks read:packages. Its version inventory is inconclusive. Do not change credentials or permissions automatically. GitHub Releases with downloadable artifacts and a reviewed local/offline package source is the proposed distribution channel. NuGet.org/GitHub Packages registry publication is outside this plan's default publication scope. If registry publication is added later, verify ID ownership, all versions and write rights first.

Conclusion: **0.10.0 is suitable as a provisional GitHub release target, with no observed GitHub collision. It is not reserved and is not declared globally available in every registry.** Recheck immediately before both candidate version allocation and final publication. If stable 0.10.0 has since been used, stop and choose a reviewed new version; never replace existing bytes.

| Surface | Unified identity rule |
| --- | --- |
| User-facing application | ArchSift Workbench 0.10.0; candidate UI includes the full rc suffix |
| Release/tag | ArchSift Workbench 0.10.0 / v0.10.0; any public RC requires separate authorization and a matching rc tag |
| Library/tool package IDs | Retain ArchSift.Contracts, ArchSift.Core, ArchSift.ArchUnit, ArchSift.Testing and ArchSift.Cli |
| CLI | Retain command archsift; CLI, Web, Setup and product libraries consume the same release-version input |
| Package and informational versions | Full release identity, e.g. 0.10.0-rc.1 or 0.10.0; source SHA may be recorded as informational build metadata |
| Assembly/file versions | One centrally defined numeric 0.10.0.0 policy, with full prerelease identity separately visible; inspect actual generated attributes |
| Product-internal package dependencies | Exact same full version, not a floating or cross-candidate dependency |
| Reports/contracts | Keep their own existing schema versions. A product-version change alone must not bump or reinterpret a report schema. |
| IFX consumption | Continue 0.10.0-cl03.1 until a separately authorized, validated adoption; never alias it to stable 0.10.0 |

RC and stable artifacts are different artifacts even if their only intended change is version metadata. Build stable artifacts afresh from the approved source and recorded stable properties, verify them, and obtain human acceptance of those final bytes. Publication then copies the exact accepted stable artifacts; it does not rename RC files into stable files or rebuild after acceptance.

## 4. Proposed distribution contract

The authoritative release home is the existing ArchSift GitHub repository. Branding changes display names and documentation; it does not rename assemblies, package IDs, commands or configuration contracts without a demonstrated need.

| Proposed required delivery | Contents and release gate |
| --- | --- |
| archsift-workbench-0.10.0-win-x64.zip | Self-contained Windows x64 Workbench/CLI payload, existing required resources and third-party notices; no installed SDK/runtime required merely to launch. Analyzed-project builds still need their declared SDKs. |
| ArchSift.Setup.exe | Matching-version separate Setup using the reviewed existing install/lifecycle model; manifest identifies 0.10.0 even if the filename stays unchanged. Verify the CLI's installed Web discovery and shutdown path. |
| Five product nupkg assets | Four libraries plus ArchSift.Cli, exact version 0.10.0; inspect nuspec, tool metadata, dependencies, license files and actual DLL identities. |
| Offline consumption bundle and manifest | Minimal approved generic consumer closure, explicit sources and dependency hashes; test with empty caches and moved paths. The private IFX 251-package source is retained privately and is not uploaded wholesale. |
| English release documentation | Getting started, installation/runtime requirements, package consumption, upgrade/rollback, limitations, troubleshooting, change notes and publication record. |
| acceptance.json, artifact manifest and SHA256SUMS.txt | Source commit, SDK/TFM/configuration/RID, build properties, actual coverage, audit scope, human evidence identity, all asset hashes and known limitations. |
| Tagged source ZIP/tar | GitHub-generated source archives from the approved commit; inspect their included documentation, build inputs, licenses and links as release artifacts too. |

The matching framework-dependent Web ZIP may be an additional developer asset only after its separate runtime-prerequisite and cold-launch checks. It cannot substitute for the required self-contained Windows delivery. No general installer, signed-binary, no-SDK-host, minimum-Windows-version or Linux binary claim is inherited from an older release.

Product remains net10.0; the existing SDK-style C# net8/net9/net10 analysis scope stays unchanged. Record the actual approved build SDK; historical evidence used 10.0.303, while global.json currently permits feature-band roll-forward. Pin or explicitly record resolved release tools and restore inputs instead of treating the global.json floor as proof of the actual SDK. Windows x64 is the primary distribution. Linux CLI regression remains a separate claim; ARM64, Linux Web binary distribution and new installer formats stay deferred.

## 5. English documentation policy and migration

Effective immediately, all newly created or revised formal repository and distribution documentation uses English: README files, guides, plans, decisions, acceptance narratives, packaged help/notices authored by ArchSift, release notes, PR descriptions and future commit subjects/bodies. Machine fields remain stable English. Chinese is for internal conversation and quick reading only. New Chinese internal summaries must stay outside committed/distributed formal documentation.

This is a documentation policy. It does not remove the existing Chinese UI localization, translate source identifiers/user data or alter program behavior. Preserve third-party legal text, upstream quotations and attribution verbatim; English explanation may surround those exact materials. Existing commits, tags, signed artifacts and raw machine/human evidence remain immutable.

The immutable HEAD inventory contains 187 tracked Markdown/HTML files, of which 149 contain Han characters. This is a screening result, not 149 proven translation defects: quoted data, licenses and UI examples need classification. The complete path queue is in the assessment. Eight visible release descriptions were also screened; v0.2.0, v0.3.0, v0.7.0 and v0.9.0-dev contain Han characters.

Migration is planned, not performed in this round:

1. Establish an exact public-document manifest from the intended release commit, plus text inside packages, generated HTML/help, release descriptions and GitHub source archives. Screen other prose-bearing formats, including descriptive JSON/YAML fields; Markdown/HTML counts alone are not a complete audit.
2. Translate the active root README, Workbench/installation/report guides, portable-consumption sample README, current package-source/retention documentation and ArchSift-authored third-party explanations. Keep IDs, commands, numerical outcomes, paths and scope distinctions accurate.
3. Review every other tracked formal document included in the new source archive, including historical acceptance narratives and plans. Preserve original bytes in an independently checked historical archive/old commit, translate prose with original SHA/provenance mapping, and retain original pass/fail/pending outcomes and chronology. Translation is not a new acceptance. Do not silently remove history or substitute an English wrapper for untranslated formal content.
4. Preserve authoritative raw JSON/test logs and human messages. Where raw evidence contains Chinese, classify it explicitly as evidence/data, retain it byte-for-byte, and provide an English formal narrative. Never translate machine identifiers or mutate a hash-bound evidence object to satisfy a prose scan.
5. Review current remotely hosted documentation and unversioned user entry points across ArchSift and the related IFX integration docs. Any IFX repository edits require a separately scoped documentation delivery; this plan makes none.
6. Address the four observed historical Chinese release descriptions as a separate reviewed metadata-only batch if later authorized. Keep all old asset bytes, checksums, tags, dates and outcomes; retain original bodies in evidence. Immutable old source archives retain their historical contents.
7. Gate the new release on human-reviewed English prose and a complete language-scan disposition list. Permit only identified legal/evidence/data/UI-example exceptions, not untranslated formal guides. Check links, code blocks and numerical consistency after translation.

The current Chinese implementation-plan history is retained in this planning round. Its new status entry is English; its publication-facing translation belongs to the migration package. No bulk translation or historical remote metadata edit is implied by saving this plan.

## 6. Acceptance gap register

All OPEN entries below concern the final proposed release. Existing accepted work remains accepted for its recorded object. A gate can close only with evidence for its actual source and artifact identity.

| ID | Current state and gap | Required closure |
| --- | --- | --- |
| G01 | OPEN: provisional version and channel | Recheck tags/releases/local feeds; define exact assets. Registry inventory/ownership is mandatory only if a registry is added as a channel. |
| G02 | OPEN: default 0.9.0-dev and candidate-specific version overrides | Central version input; inspect all product DLLs, CLI/Web/Setup displays, package metadata, dependencies and report tool identity. |
| G03 | OPEN: final release provenance | Select a reviewed committed source/tree, locks, build tools/properties and source manifest; map package inputs without claiming unsupported byte reproducibility. Prior source delivery is complete. |
| G04 | OPEN: final Windows ZIP/Setup and package closure | Produce new immutable RC/stable outputs; verify generic cold consumption, moved paths, empty caches, both hashes and all notices. |
| G05 | OPEN: final-source regression | Actual Windows/Ubuntu checks and required unit/integration/native MTP/architecture/templates/schema/Node checks; compare case identities and explain deltas. Prior 57 native cases and 2/157/95/3 development groups are historical references, not a prescribed substitute for current discovery. |
| G06 | OPEN: final Web HTTP behavior and CLI/Web parity | Launch the packaged loopback server; run/list/filter rules, full/subset/cancel/error/history/download flows; compare authoritative JSON and CLI results on identical inputs. |
| G07 | OPEN: latest IFX against new bytes | Reviewed external snapshot; current 493-case selection, 404 responsibilities, 89 assembly identities and zero skipped/unexpected cases; preserve policies, snapshots, assertions and timing thresholds. Explain any baseline change before proceeding. |
| G08 | OPEN: failure/status isolation | Missing/unknown/duplicate/skip/zero-match/tamper/wrong-version/unbound/missing-source and interrupted/cancelled runs fail correctly; never show a prior success as current. Pass, violation, inconclusive, not-applicable and error remain distinct. |
| G09 | OPEN: final-closure vulnerability and license review | Actual product/tool/runtime and consumer closures; separately authorized explicit audit sources, timestamp and severity handling. Offline Audit=not-run is not a security pass. Preserve all legal text. |
| G10 | OPEN: install/upgrade/rollback/uninstall | New Windows delivery on fresh and existing state, spaces/non-ASCII paths, wrong/missing runtime for optional FDD, occupied port, safe shutdown and process/listener closure; recoverable version removal preserves reports/config/history/evidence. |
| G11 | OPEN: independent machine/support claims | New payload on an independent supported Windows x64 environment, including self-contained launch without SDK/runtime; record exact OS/tools and actual target-build prerequisites. Unsupported platforms/signing stay explicit limitations unless separately qualified. |
| G12 | OPEN: final human UI and usability | User reviews exact final stable payload after machine gates; English guidance/version identity, complete/subset/cancel/failure/history, sorting/wrapping, existing locale switch, narrow window/keyboard, safe close. Keep answers blank until supplied by the user. |
| G13 | OPEN: English formal documentation | Complete migration manifest and reviewed exceptions, correct instructions/links, English release bodies and packaged/source-archive prose for the new release. Historical remote-body conversion is separately tracked. |
| G14 | OPEN: artifact/acceptance integrity | acceptance.json plus final manifest/sums bind exact source, properties, assets, machine and human results; independently re-read archives; disclose signatures/support limits. |
| G15 | NOT STARTED: publication and post-download validation | After explicit publication authorization, create the exact tag/release and upload accepted bytes, then download and compare every asset and source archive; test documented installation/consumption and record final closure. |

GitHub Packages' 403 is an INCONCLUSIVE metadata check, not a product failure. Historical Chinese content is a migration workload, not a reason to invalidate earlier engineering acceptance. The 195 retained materials and retired-route history are not cleanup blockers for this release.

## 7. Ordered work packages and dependencies

Every package WB01-WB10 is NOT STARTED and NOT AUTHORIZED by this planning request.

| Package | Dependency | Deliverable and exit condition | Responsibility / authorization boundary |
| --- | --- | --- | --- |
| WB00 - Plan and assessment | Read existing plans/current acceptance | This English plan, read-only version check, explicit gap register, language policy and local validation | Agent; authorized now, complete when this documentation is checked |
| WB01 - Freeze release contract | WB00 | Approve 0.10.0 identity, channel/assets, support matrix, source selection and evidence/backup scope; recheck collisions | Maintainer decides; read-only/freeze work only when requested |
| WB02 - Branding and English migration | WB01 | Reviewed naming/version surfaces and exact documentation migration list; English prose and preserved originals | Agent implements a bounded batch after authorization; no silent remote edits |
| WB03 - Source and release tooling delivery | WB02 | Central version/build/manifest inputs, exact selected changes, appropriate local regression; separately authorized PR and actual-main CI bind a final source commit | Commit/push/PR/merge need explicit authorization; human primary author and Codex trailers/signature required |
| WB04 - RC build and package inspection | WB03 | New external RC directory/cache, locked explicit-source build, required assets and provenance; no old candidate overwritten | Machine execution authorization and approved external paths required |
| WB05 - Synthetic, regression and closure gates | WB04 | G04/G05/G08 and candidate G09 evidence; negative/compatibility tests and actual dependency/license audit | Run sequential where stateful; inspect each outcome; any code fix returns to WB03 and creates a new RC |
| WB06 - Real IFX snapshot/parity | WB05 | G06/G07 evidence with current policy, CLI/Web parity and baseline protection | Specific delegated real-run authorization; reviewed offline commands; original target remains read-only |
| WB07 - Lifecycle and independent Windows | WB06 | G10/G11 results, explicit supported-environment claims and recoverable rollback evidence | Separate test installations/snapshots; no replacement of the live IFX consumer |
| WB08 - Stable candidate and human acceptance | WB07 | Build new stable-version assets from approved source/properties; repeat identity, cold-use, parity, IFX/lifecycle/closure checks affected by new bytes; user accepts exact stable payload | All mandatory final-artifact machine gates must be evidenced before the human session. No mechanical renaming of RC files. |
| WB09 - Final release review | WB08 | English notes, resolved mandatory gaps, evidence/asset/source archive manifests, complete language dispositions and concrete publication preview; recheck version availability | Maintainer approves the concrete publication. Hold without tagging/uploading until authorized. |
| WB10 - Publish and verify distribution | WB09 plus explicit publication authorization | Exact tag/commit, accepted assets, English release body, public download hash/installation checks and final publication record | Publishing, optional registry use and any IFX live adoption have separate explicit scopes |

Within WB08, determine the affected revalidation scope from actual RC-to-stable input/byte differences and document it before execution. Final stable artifacts must have their own cold launch, version/closure, CLI/Web/IFX and lifecycle evidence; older equivalent semantic tests may be referenced only with explicit identity/change analysis. Any later payload or behavior change invalidates the relevant machine and human acceptance and requires new artifacts.

A publicly uploaded RC is optional and is a publication operation in its own right. Local numbered candidates do not require a GitHub prerelease. A new source commit, CI dispatch or IFX live update is never implicit in an earlier package's success.

## 8. Execution and evidence rules for later work

- Use pwsh -NoProfile and child-only settings; isolated DOTNET_CLI_HOME always pairs with DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0. Never edit persisted environment, registry environment, Profiles or startup files.
- Capture safe host/environment/Profile hashes before build/install/real integration; compare at relevant checkpoints and completion, print only hashes/equality, stop and preserve evidence on persistent drift. Do not automatically adopt a changed host baseline.
- Place actual runs, real snapshots, package outputs and raw evidence under fresh precisely named D:\ArchSift-lab directories, with normal path approvals. This round creates no external run/artifact directory.
- Restore offline from explicit approved sources by default. Any network acquisition/audit must be separately explicit; do not ignore restore failures or silently use warm caches. Record resolved SDK/TFM/configuration/RID and exact inputs for every result.
- Do not execute target application entry points. Review snapshot MSBuild inputs because copying a target does not sandbox its custom targets. Real gates run sequentially and each result is inspected before the next.
- Preserve current/prior 251-package feeds, CL03 artifacts, live IFX inputs, policies, locks, reports, backups, failed/cancelled attempts and unrelated local materials. New outputs never reuse a recorded artifact/version directory.
- Use JSON as executable rule/report and release-status authority. Markdown/HTML are projections. Record not-run/inconclusive/error separately and never infer success from existing caches, old CI or screenshots.
- Reference complete SHA values, original CI event/attempt and actual checkout identity. Product Windows/Ubuntu CI and IFX's three required checks prove different scopes; neither replaces the other.
- Keep publication source selection explicit: no blanket git add, raw private IFX package source, unknown local scripts or whole-worktree archives as release inputs. Preserve third-party rights and selected Guard source-inventory obligations.
- If rollback is needed, recover the recorded complete prior combination while retaining parallel adopted policies and checks. A release publication does not perform IFX rollback/adoption or material cleanup.

## 9. Release go/no-go and completion

Before publication, G01-G14 must be closed for their declared scope, with any genuinely non-applicable optional surface explicitly justified. There must be no unresolved mandatory error, incomplete coverage, drift, asset/source mismatch or missing human acceptance. Known platform/signature limitations must be stated in English and consistent with the release's actual promises. Registry queries may remain out of scope only while registry publication is excluded.

The final review packet must show the product name/version, source SHA/tree, exact asset list/hashes, dependency/audit scope, acceptance matrix, independent human result, English-document manifest, installation/rollback instructions and a publication preview. Then obtain explicit authorization for tagging/upload/publication. No vague future approval replaces a reviewable packet.

After authorized publication, G15 closes only when live release/tag metadata, anonymous downloads where supported, all asset hashes, source-archive contents and documented cold usage are verified. If an upload or verification fails, preserve failure evidence, report the actual partial state and avoid replacing same-version accepted bytes. Do not report completion until distribution verification passes.

IFX adoption of 0.10.0 remains a separate later package with its own authorization and acceptance. The current 0.10.0-cl03.1 installation remains the accepted live baseline until then.

## 10. Historical status at the end of the original planning round

WB00 planning and read-only assessment: complete. WB01-WB10: not started. Product release acceptance: not granted. No package/version/product code, existing acceptance outcome, IFX checkout, CI workflow or remote object was changed. No .NET build/test/install, real integration, UI session, commit, push, PR, merge, tag or publication was executed.

The next actionable scope, if requested, is WB01: freeze the release contract and exact migration/build input list. This plan itself does not start WB01.
