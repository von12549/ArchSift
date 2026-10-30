# ArchSift 0.11.4 release notes

Status: unpublished candidate; final qualification pending.

- Add optional inclusive/exclusive stable SDK bounds to schema-2 test gates and an optional exclusive Workspace bound. Without a gate range, the approved baseline comparison remains exact.
- Preserve required-case baseline SDK as provenance while validating generic stable SDK metadata without a literal 10.0.303 requirement.
- Add configured, locked gate restore using an explicit source-clearing NuGet.Config. Keep offline restore as the default and record restore mode separately from the independent audit.
- Generate and automatically select Workspace daily-test inventory for both configured and offline preparation, including Launcher child Workbench. Startup does not execute architecture tests.
- Include the shared daily-test execution lease in session busy status and shutdown checks. Launcher and direct Workbench shutdown refuse an active daily gate until it completes or explicit cancellation finishes.
- Load hash-verified project rules through workbench.policyFile, reject conflicting direct lists, and include rule/policy/gate content in generated configuration identity.
- Resolve omitted state roots through per-user LocalApplicationData and keep Doctor observational. Update the packaged entry template to the same configured/exact policy and SDK bounds.

Old configuration contracts remain supported by the new version. New fields require the new package; 0.11.3 rejects them. Published 0.11.3 bytes, historical acceptance and existing installation state are not rewritten by this release.
