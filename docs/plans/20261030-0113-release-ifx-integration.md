# ArchSift 0.11.3 and IFX integration preparation

## Actual-main package and delegated Agent UI checkpoint, 2026-10-30

This checkpoint supersedes the pending CI/package/UI statements below without changing their historical results. PR 43 merged at `af25bed0da8b3cedf5706a404dd3f33a8f7b45df` (tree `8d862be384a63806ec76c23c5c4ee604f94321e9`). All 13 jobs in its five main workflows passed. Candidate run `40922246528`, artifact `12918368591`, produced the five reviewed 0.11.3 packages. The prior wrong-README artifact remains rejected.

The user explicitly skipped independent human UI acceptance and delegated this final-candidate UI scope to Codex. Record human acceptance as `skipped-explicit-user-delegation`, performed false; record actual Agent coverage separately. No human score or prior-release waiver is borrowed. See [the machine-authoritative record](../acceptance/20261030-0113-final-package-agent-ui.json) and [coverage and limitations](../acceptance/20261030-0113-final-package-agent-ui.md).

Exact main packages passed a fresh isolated synthetic packaged consumer (four cases), tool restore, installed-host/configured-source workspace Init/Doctor, Launcher and child Workbench first/repeat, and direct Workbench first/repeat. All four product lifecycle closeouts exited 0 with host equality. The same target/state/startup paths were reused, three configuration hashes remained equal, and all recorded processes exited. No real IFX target or architecture gate ran. SDK 10.0.303 and installed Core/ASP.NET 10.0.11 were observed.

The generic package-owned startup template still requires exact/offline preparation for IFX metadata. Its rejected probe is preserved. The accepted IFX integration contract restores/verifies the pinned package and dispatches `CLI workspace` directly; this qualification follows that contract. Do not advertise generic-template IFX host/configured support.

R13-03 final-source package/integrity and the scoped consumer/UI lane have passing evidence. R13-04 remains incomplete: the follow-up release guard models explicit Agent delegation independently of human acceptance, with source/tree/package-set, authorization and evidence hashes. All 242 release-safety simulations passed. These tooling/documentation edits are uncommitted and do not alter the tested product packages. Normal review/CI, final reference/qualification binding, a fresh dependency audit and credential-free preview remain next. Publication capabilities remain false and final qualification/reference files remain absent. R13-05/R13-06, public-only cold clone, signed served-byte verification and IFX activation remain pending. Active IFX 0.11.0 and the accepted RC delivery remain unchanged.

Optional offline delivery remains skipped. The local feed used to test unpublished packages is qualification infrastructure, not a required end-user delivery layout. Human nuget-release environment approval and separate publication authorization are unchanged by UI delegation.

## Source commit and CI correction, 2026-10-30

The authorized continuation committed the first freeze as `73e95f429035b13eb6d047bfea1a9c7369a82271` and opened [draft PR 43](https://github.com/von12549/ArchSift/pull/43). Its clean PR package build passed, but independent ZIP inspection found the shared 0.11.2 README in all five 0.11.3 packages. That artifact is rejected and retained. R13-03 therefore remains incomplete despite successful compilation.

The package builder now requires and passes both version-specific documents and verifies their embedded hashes before recording success. Four simulations using the actual guard pass; syntax passes. The scoped correction is captured in replacement product freeze `0113-source-review-20261030-02`; the original freeze and isolated IFX source remain unchanged. See [the correction record](../acceptance/20261030-0113-source-ci-documentation-correction.json). Corrected PR CI and artifact review come next, followed by a distinct merge/main-build decision. The PR preview artifact is not an eligible actual-main publication artifact. Publication, final IFX binding, real IFX/architecture gates and activation remain pending.

## Source freeze and review, 2026-10-30

Freeze `0113-source-review-20261030-01` captures the reviewed uncommitted product source and the separate IFX preparation. No new blocking code defect was identified; a stale pipeline-support statement in the current release notes was corrected before capture. Sixteen static consistency/preservation checks pass. The external freeze receipt binds complete source ZIPs, per-file SHA-256 inventories and the proposed commit path scope. See [the authoritative review](../acceptance/20261030-0113-source-freeze-review.json).

R13-01/R13-02 and the implemented parts of R13-03/R13-04 are frozen for commit review, with their previously recorded limitations. This does not mark final-source CI, release qualification, publication, IFX stable binding, fresh-clone acceptance or activation complete. R13-05/R13-06 remain pending. Active IFX stays at 0.11.0; executable isolated IFX pins remain RC. No commit, tag, push, real test or publication occurred in this phase. The next scoped decision is committing the reviewed product change set and running the required source CI; any source change first invalidates and replaces the affected freeze inventory.

## Authorized local qualification and pipeline preparation, 2026-10-30

The user authorized the next planned package and supplied a NuGet Trusted Publishing policy screenshot. Its owner/repository IDs, workflow, environment and package scope match the local publishing profile; this is retained user-supplied evidence, not a fresh authenticated service readback.

The 0.11.3 qualification build completed nine SDK/locked-offline-restore/Release-build/Web-publish/package stages, with zero compiler warnings/errors and five independently reviewed packages. All 93 scoped product synthetic regressions passed after an administrator-only link-fixture retry. Package review verified 69 Web payload files and archived 884 source inputs. The initial sandbox workload-check failure and the unprivileged link-fixture failure remain preserved. Normal, unsandboxed build and administrator sessions retain separate process baselines with equal persistent environment/Profile hashes; no environment repair occurred.

The packages remain development-unqualified because their source is uncommitted. Their exact full source snapshot and archive hashes identify these local bytes; the base commit alone does not. A subsequent clean committed/CI build needs its own identities and qualification. No real IFX integration, architecture test, installed-package lifecycle or final-byte human acceptance is claimed for this local package set.

Release driver/model/artifact/helper support for 0.11.3 is now implemented behind closed, unbound profile checks. A proposed workflow is stored under `docs/releases/0.11.3`, outside active workflow discovery. The active publish workflow remains unchanged. 226 release-safety simulations pass, including 0.11.3 dirty-source, evidence, waiver and version-mixing rejections. Existing workflow checks pass 51 structural mutations and two child HTTP-boundary mutations. Final reference/qualification files are deliberately absent; no historical waiver or fake package identity was supplied.

Current phase status: R13-03 local build/regression completed, but final clean-source packages remain pending; R13-04 code/proposed-workflow preparation completed, but final qualification, source CI, artifact binding, fresh audit, active workflow selection and publication remain pending. R13-05/R13-06 and existing-machine activation remain pending. See `docs/acceptance/20261030-0113-local-qualification.json`. Earlier preparation statements below are retained as dated history.

Date: 2026-10-30. Authority: the user authorized product/main reconciliation, a 0.11.3 version proposal, IFX CI conflict correction, static checks and simulations. No real restore/build/integration/architecture run, active IFX switch, commit, PR or publication is included in the execution recorded here.

## Version and source decision

Use `0.11.3` for the next stable source preparation. All five deliverable packages use that version and retain exact internal package dependencies. Do not replace 0.11.2 or RC package bytes. Final package identities remain unknown until a separately authorized build and qualification.

The existing repair checkout was fast-forwarded from `91cd73adc85a4da8f79fffa04ad834c870d78d07` to current main `68f225172ebd2fafd056587015ec37573652b6b3`. Seventeen upstream paths were incorporated. All 35 locally changed/untracked files were backed up first; 34 were retained byte-for-byte at synchronization, and the implementation plan's local checkpoint prefix was combined with the upstream body. No merge commit was created. An empty hook directory was used for this local fast-forward to avoid executing unreviewed hook actions. The upstream hosted-NuGet fixes and published 0.11.2 records are preserved.

`Directory.Build.props` now selects 0.11.3. Ten source/test lock files contain 34 updated project-reference dependency version edges. This is a static lock edit, not a restore result. Third-party versions/hashes and the product repair/test implementation were preserved. Source commit and source tree for the final package build are pending; current HEAD alone does not identify these uncommitted inputs.

## IFX isolation and CI resolution

The accepted `ifx-host-0.11.3-rc.1-candidate`, frozen delivery ZIP and real IFX checkout remain unchanged. A separate `ifx-0.11.3-integration-preparation` directory carries the changes. Its executable host provenance still selects RC, while `architecture/archsift-release-target.json` records 0.11.3 as a non-executable future target. No fake final hashes, signature result or publication flag is introduced.

The preparation directory's architecture source metadata now lists the same five RC packages as its gate, tool, host contract, central Testing pin, library locks and approvals. The 246 third-party source entries are unchanged. `toolVersion` and a normalized provenance digest are coherent. This resolves the static mixed-version conflict; no complete offline feed has been assembled or qualified.

`Test-ArchSiftPackageInputs.ps1` validates those bindings before any architecture runtime resolution or operation. The guard is called from the CI dispatcher, architecture entry, dependency audit entry and source-config generator. It reads metadata only. The existing source validator still checks exact package files and rejects extra files; runtime hash checks, architecture execution, dependency audit and evidence aggregation remain mandatory when those jobs are later authorized and run.

The CI workflow retains its jobs, triggers and success requirements, but takes source/runtime paths from `IFX_ARCHITECTURE_PACKAGE_SOURCE` and `IFX_ARCHITECTURE_DOTNET_ROOT` repository/runner configuration instead of a developer's historical directory. Missing paths stop the job explicitly. Setting a path cannot bypass package or runtime approvals. No GitHub variable, runner or branch protection was changed. A configured compatible runner/feed and actual CI results remain pending.

This preserves the existing architecture CI policy. It does not require an offline delivery kit for ordinary IFX users. Default host-mode cloning and startup still use installed prerequisites and exact online acquisition; architecture execution is a distinct, separately approved operation.

## Phase status and required evidence

| Phase | Current state | Completion condition |
| --- | --- | --- |
| R13-01 source synchronization and version | Prepared locally | Static preservation and project-lock checks pass; final build remains pending |
| R13-02 IFX CI input consistency | Implemented in isolated preparation directory | Static/mutation checks pass; actual runner/source qualification remains pending |
| R13-03 final product packages | Pending | Authorized build of reviewed source, package inventories and required synthetic regressions; no borrowed RC pass for new bytes |
| R13-04 publication pipeline and qualification | Closed draft only | Review 0.11.3 support in driver/model/qualification/workflow/helper together; bind final source, CI, payloads, human review and fresh audit evidence; preserve fail-closed locks |
| R13-05 IFX final public bindings | Pending final served identities | Atomically update executable pins, locks, approval/provenance/host contract/gate/source metadata and their digests; verify signatures/owner/served bytes |
| R13-06 final clone/CI acceptance | Pending and not authorized here | Fresh clone, empty cache/state, no overlay/CandidateSource; independently authorized repository CI and lifecycle verification |
| Existing-machine activation | Separate future decision | Explicit target revision/version, fresh isolated state, operator-controlled rollback |

The new 0.11.3 publishing profile has both capabilities false, null hashes and no reference/artifact. It is deliberately not enabled in the current driver or active workflow. Do not claim preflight/preview/publish readiness from that file. No remote operational switch was opened. Never transfer 0.11.2 publication authority or human-repeat waivers to 0.11.3.

Final public binding must use actual repository-signed served package identities. Preserve the original RC evidence and original submission hashes separately. Do not rename RC packages or globally replace version strings while retaining RC archive hashes. At final binding, update the architecture source metadata in the same reviewed change, so the consistency guard rejects partial transitions.

## Validation boundary and next action

Validation for this package consists of file/hash checks, PowerShell parsing and metadata-only mutation fixtures. No SDK, restored tool, product, real CI job or architecture test is executed. See `docs/acceptance/20261030-0113-release-ifx-preparation.json` for actual outcomes and retained logs.

The next recommended package is reviewed final-source compilation/packaging and synthetic product regression, followed by final-identity release-pipeline preparation. It requires its own explicit execution scope and host-baseline capture. Publication, real IFX/architecture validation and activation remain separate. Optional offline user delivery remains skipped. Fresh-clone usability does not depend on migrating old state.
