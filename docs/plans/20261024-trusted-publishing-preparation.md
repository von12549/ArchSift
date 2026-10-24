# Trusted Publishing integration preparation

Authorized 2026-10-24: update plans, add preparation workflow/scripts/tests, normal PR/CI/merge. GitHub environment and NuGet policy are user-operated. Credential acquisition, package publication, tags/releases and new installers are not authorized. Trusted Publishing replaces scoped manual API keys as the preferred future route; API keys remain an explicitly reviewed fallback only.

TP00: preserve NR04 accepted package/preview identities and host/live-target boundaries. NR04 is complete: Launcher/owned Workbench and independent Workbench passed, correct version/target, 92 C# / 13 rulesets / 401 JSON, safe shutdown, usability 4/5 and no reported defects. The result applies to original final-build-02 submissions, not rebuilt workflow-source packages.

TP01: add manual-only publish-nuget.yml as a read-only preflight, a sanitized fixed release reference, narrow workflow privilege checks and functional positive/negative hash/reference tests. No environment-bound job is included, preventing implicit environment creation. No id-token permission or NuGet login/push path exists. Existing CI and IFX security rules remain unchanged.

TP02: verify scripts/workflows locally, actual accepted bytes and original source CI; obtain normal PR and actual-main Windows/Ubuntu CI, then execute the main-only read-only dispatch. Preserve failed attempts and passing evidence separately. Do not rerun copied IFX integration for changes that cannot change accepted product bytes.

TP03: hand the user the environment instructions first, wait for readback, then the NuGet policy instructions. Match nuget-release/main/manual reviewer/no bypass and XiaolongFeng/von12549/ArchSift/publish-nuget.yml/ArchSift.*. Configuration remains pending until actual operator feedback. OIDC authentication remains untested; neither a configured policy nor an empty public index proves publication rights.

TP04 (future, not authorized): reviewed activation PR with environment-gated credential/publish steps, immutable artifact transfer/hash binding, new source CI and explicit NR05–NR08 authority. The merged preparation workflow is intentionally unable to publish even if dispatched or the user later saves a trust policy. Preserve original package-source commit separately from activation-workflow commit. Public serving/signature/download/release verification stays future work.

See the [operator and rollback guide](../guides/trusted-publishing.md) and [frozen submission reference](../releases/0.11.0/trusted-publishing-reference.json). Execution evidence, account readback and exact final delivery status are maintained separately; this scoped source document does not claim pending configuration or OIDC testing has passed.

Hosted preflight correction: the first manual run on Linux resolved gh to both /usr/bin/gh and /bin/gh; the executable invocation failed before any credential or publication operation. Select the first application explicitly and exercise the actual read-only source/CI lookup in preparation checks on both platforms. Preserve the failed run as evidence. The earlier local Windows check did not cover duplicate Linux command resolution.
