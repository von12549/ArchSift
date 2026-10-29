# ArchSift 0.11.3 local qualification and closed pipeline preparation

Recorded 2026-10-30. Status: **local build and scoped synthetic regression passed; publishing pipeline prepared with authority closed**. This is not final release qualification. The JSON companion is authoritative.

## Completed

- SDK 10.0.303, net10.0, Release: nine SDK/locked offline restore/build/Web publish/package stages passed. Build reported zero warnings and errors. Five 0.11.3 packages were produced in a fresh external directory.
- Independent archive inspection verified exact internal dependencies, nuspec identities, README/release notes/licenses, framework-dependent configuration, project template and the full 69-file Web inventory. A ZIP and hash manifest preserve all 884 build-source inputs.
- 93 distinct scoped product synthetic cases passed: 31 IFX policy, 37 workspace, 24 child-environment/portable-workbench/entrypoint and one administrator link-fixture case. No full solution test suite, actual IFX application or architecture tests ran. These tests use the newly built Release assemblies, not an installed-package consumer lifecycle.
- Release driver, model, artifact route and hosted helpers recognize 0.11.3 while rejecting an unbound draft before reference/network reads. Final source must be committed; new final-package/machine/human evidence is required. A 0.11.2 waiver cannot qualify 0.11.3. All 226 final synthetic release tests passed, plus 51 workflow-structure mutation rejections, two child HTTP-boundary mutations and two helper syntax checks.
- The proposed 0.11.3 workflow is outside `.github/workflows`. The active publishing workflow is unchanged. Both new profile capabilities remain false, hashes/artifact bindings are null, and no final trusted reference or qualification file was fabricated. No credentials were acquired or remote publishing policy modified.

## Evidence and retained failures

The user-supplied NuGet policy image shows an active GitHubActions policy for owner XiaolongFeng, package pattern ArchSift.*, repository von12549/ArchSift (owner ID 2306937, repository ID 1407069697), workflow publish-nuget.yml and environment nuget-release. These values match the publishing profile. Image bytes and SHA-256 are retained; the image is not an independent authenticated live policy readback or publication transaction approval.

The first sandbox build stopped at SDK workload verification before successful restore. The same offline build passed outside the sandbox in a new directory. Its exact infrastructure root cause was not traced; no workload update or environment repair ran. A non-administrator link test failed creating its synthetic fixture; the user confirmed Windows UAC and the isolated administrator retry passed. An initial release simulation failed because an old raw-digest fixture omitted version fields; the fixture was completed and the production guard retained. All original failure logs remain evidence.

Normal, unsandboxed build and administrator process baselines are separate. Persistent User/Machine environment and Profile hashes agree, each session's final hashes agree, and the post-pipeline normal baseline agrees. No persisted environment, Profile or registry repair was performed.

## Limits and next gate

These packages were built from uncommitted source based on `68f225172ebd2fafd056587015ec37573652b6b3`. Their identity is the complete archived source input set plus package hashes; the base commit does not identify the complete source. They remain **development-unqualified**, despite having a stable 0.11.3 version. Release-pipeline/documentation edits made after packaging are recorded separately; product runtime/template inputs remain unchanged. A later clean source/CI build requires new package identities and qualification.

Next prepare a reviewed source-freeze change and obtain the separate commit/remote-CI scope, then build from that committed identity, qualify its exact bytes and finish final reference/artifact/audit bindings. Final installed-package lifecycle and human review have not been performed for these bytes; prior RC results and old waivers are not relabeled. Actual publication and human environment approval, final IFX public binding/clone acceptance and existing-machine activation remain subsequent gates.

No active IFX configuration switch, commit, PR, publication, real IFX integration or architecture test occurred. The accepted RC candidate and current 0.11.0 IFX checkout were not modified. Optional offline user delivery remains skipped.
