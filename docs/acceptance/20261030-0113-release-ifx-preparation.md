# ArchSift 0.11.3 release and IFX integration preparation

Recorded 2026-10-30. Status: **source synchronized; isolated CI metadata correction prepared; static and simulated checks passed**. Formal release/adoption is still pending. The JSON companion is authoritative.

The product worktree is now based on upstream main `68f225172ebd2fafd056587015ec37573652b6b3`. All 35 pre-existing local files were backed up, 34 preserved byte-for-byte at synchronization, and the implementation plan was combined with upstream additions. The upstream publishing-helper corrections and immutable 0.11.2 publication records were retained. No new commit was created.

The source version is 0.11.3. Thirty-four project-reference version edges in ten lock files were updated statically; third-party dependency identities were preserved. The existing repair implementation and regression source are unchanged. No new package bytes or final source commit identity are claimed. The 0.11.3 publishing profile is a closed, unbound draft; the active release workflow and driver were not switched or enabled for this version.

A new isolated IFX preparation directory preserves the original RC executable identities while correcting its CI source metadata to match them. A shared pre-execution guard checks versions, provenance, archive hashes, library approvals/locks and the central pin. The workflow now validates metadata and mutation fixtures first; architecture, audit and final acceptance retain their checks. Runner paths are explicit variables rather than historical developer paths. Missing resources fail closed. The 251-package inventory is metadata only; no complete feed, runtime provisioning or actual CI pass is claimed.

Validation passed: **31 static checks**, **19 metadata simulation cases**, and PowerShell syntax parsing for **6 changed/new scripts**. Negative fixtures reject stale/mixed versions, mismatched hashes, duplicate/extra/unsafe entries, loose tool selection, provenance drift, library-lock drift and partial stable-version transitions. CRLF/LF provenance normalization passed. These tests execute only the metadata validator, not product/architecture entry points.

The accepted RC directory and frozen delivery remain unchanged. The real IFX checkout remains at `3e330fd9d651b2b43fe4d4e2f27099a141705892` with CLI 0.11.0; its pre-existing untracked documents were preserved. Old RC evidence remains valid for its original inputs and is not a final-byte 0.11.3 acceptance.

Next: separately authorize final-source compilation/packaging and scoped synthetic product regression, then complete final-identity pipeline qualification and public package binding. Actual CI, public-source-only clone acceptance, publication and existing-machine activation remain separate. No real restore/build, SDK/product execution, integration/architecture test, active configuration switch, commit, PR or publication occurred in this preparation.
