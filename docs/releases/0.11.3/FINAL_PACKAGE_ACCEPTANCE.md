# 0.11.3 final package acceptance checkpoint

The main-built candidate from `af25bed0da8b3cedf5706a404dd3f33a8f7b45df` passed the scoped package-consumer and Agent UI lifecycle checks. See the [acceptance record](../../acceptance/20261030-0113-final-package-agent-ui.md) and [release plan](../../plans/20261030-0113-release-ifx-integration.md). This document supplements the frozen embedded package documents; it does not replace package bytes.

Independent human UI review was explicitly skipped and delegated to Codex. The release contract records that distinction with `humanAcceptance.status = skipped-explicit-user-delegation`, `performed = false`, and separate source/package-bound Agent evidence. It does not waive normal CI, package integrity, fresh audit, release-environment approval or explicit publication authorization.

IFX installed-host/configured-source consumers must restore and verify the pinned package through the IFX repository entry, then dispatch CLI workspace. The generic package startup template still enforces IFX exact/offline preparation; that template is not qualified for the host/configured IFX route. Ordinary startup does not run architecture tests.

The candidate remains unpublished. Final public source/hash bindings, served signatures, a fresh public-only clone and IFX activation remain pending. End-user offline delivery is optional; the local qualification feed does not change that boundary.
