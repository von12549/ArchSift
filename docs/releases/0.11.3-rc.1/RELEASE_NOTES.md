# ArchSift 0.11.3-rc.1 local development candidate

This unpublished, unqualified candidate adds IFX workspace support for an installed host with explicitly configured NuGet sources after the consumer restores its pinned dependencies. IFX gate identity, manifest/version pins, policy/case hashes, approved CLI archive hash, restored payload inventory/bytes and Web integrity checks remain mandatory. Exact-runtime/offline behavior remains supported.

Configured-source IFX initialization returns a null daily-test configuration and explains that the existing architecture gate requires separate approved offline preparation. It does not run restore, build or architecture tests. This candidate does not add online architecture-gate restore.

The repaired source based on published 0.11.2 compiled and obtained passing evidence for 93 selected synthetic cases across the original run and one authorized elevated retry. Those results predate this candidate identity and do not qualify these package bytes. Candidate verification is recorded separately. The source has uncommitted changes; its base commit alone does not identify the complete candidate source. Retain the accompanying full source-input manifest and package hashes.

Distribution is four net10.0 libraries and the matching framework-dependent CLI/Web tool package. No runtime installation or offline runtime bundle is included. No public publication, signing, real IFX integration, architecture gate, UI acceptance, migration or active IFX configuration switch is claimed. Do not use this candidate to replace published 0.11.2 bytes or activate IFX 0.11.0 state.
