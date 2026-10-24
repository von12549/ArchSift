# Trusted Publishing for ArchSift 0.11.0: publisher remains closed

The user authorized TA00–TA03 activation-code preparation, tests, PR, CI and merge. No real release asset upload, publishing credential, NuGet push, tag, Release or installer is authorized. Local Guard/dotnet remains stopped and host persistent environment/profiles are unchanged. [Implementation plan](../plans/20261025-trusted-publishing-activation.md).

## Current configuration and evidence

NuGet account: **XiaolongFeng**. The operator confirmed email verification and showed policy **ArchSift** Active: GitHub owner/repository **von12549/ArchSift**, workflow **publish-nuget.yml**, environment **nuget-release**, glob **ArchSift.***, Push new packages and package versions, no Unlist/relist. This is operator evidence, not successful token exchange or proof of package-ID rights. Recheck private policy/account status before any later production exchange; never share passwords, MFA codes, tokens or API keys.

The GitHub environment is read back separately: reviewer **von12549**, administrator bypass **disabled**, exactly **Branch/main**, no tag rule. Prevent self-review remains off for the sole maintainer; dispatch and approval still require separate decisions. Do not broaden rules, add credentials/secrets or alter existing IFX protections. The product repository currently has no main branch protection; “Protected branches only” would not implement this main-only restriction.

The [original preparation reference](../releases/0.11.0/trusted-publishing-reference.json) remains immutable historical evidence. Its user-operated-pending status describes the earlier capture, not current configuration. Accepted packages remain tied to source `8b721f44e90a41285fa74a6f72576c7f6996ef7d` and tree `2c18d18145beb52a917a3884ddd7f442ec486967`, five original hashes and NR04 human acceptance (4/5). The activation workflow commit is a separate identity. Nothing in activation preparation rebuilds or substitutes those packages.

## Safe default operation

The manual-only [publish-nuget.yml](../../.github/workflows/publish-nuget.yml) defaults to **preflight** on main. It checks original package source/CI, current main workflow CI and actual environment rules, then reports a closed preflight. It does not read/download/upload real submission archives, obtain OIDC/NuGet credentials, run dotnet or publish. Its artifact uploader is skipped. The publisher and independent public-verification jobs are skipped.

Two committed Boolean locks in [publishing-activation.json](../releases/0.11.0/publishing-activation.json) remain **false**: `realAssetTransferEnabled` and `publicationEnabled`. Repository variable **ARCHSIFT_NUGET_PUBLISH_0110** remains absent or false. A switch/input alone cannot enable preview/publishing. Future enabling needs a separately reviewed code change and fresh bound workflow/request identities; do not edit these files directly on main as an operational shortcut.

After merged-main CI succeeds, run only Actions → **ArchSift NuGet release (closed preparation)** → Run workflow → branch **main**, mode **preflight**, all three identity fields empty. A pass proves read-only prerequisites at that time; it does not prove real package bytes, live NuGet authentication, package-name rights, publication or new consumer acceptance. Wrong repo/ref, rerun attempt, moved main, failed/missing source/main CI or weakened environment rules fail closed.

## Future preview and actual publication

TA04 requires its own authorization to transfer exact accepted archives. Existing regression artifacts do not contain those originals. The proposed data-only public Git branch exposes package contents even before NuGet publication; branch deletion is not guaranteed erasure. Do not upload under TA00–TA03, invent a local Actions upload API, silently rebuild, or change transport without review. Use only a separately approved immutable data commit and exact request/payload hashes.

After the transfer locks are separately enabled, **preview** can validate the exact five packages and request, create an immutable same-run artifact and display run/artifact ID and raw ZIP digest. Its job has no OIDC permission. A future **publish** dispatch repeats preparation; before approving that run, compare its concrete artifact/request identities, package hashes/order, original source, reviewed current workflow commit and CI with the approved preview. Review does not mean merely matching a display name.

Actual NR05 requires separate explicit user authorization for credential acquisition and all five pushes. Recheck NuGet policy, account/ownership and exact-version server state, committed locks, repository switch and environment protection. The user dispatches from main then selects **Review deployments → Approve and deploy** for that exact run. No administrator bypass. Reject if anything differs. Policy glob ArchSift.* does not widen the workflow's five exact approved IDs.

Only the approved publish job has id-token-write. It redownloads and rechecks the exact artifact/main/rules before the pinned official NuGet/login action. The temporary key goes through a masked same-job output to a push child environment, never a durable config/secret, CLI argument, artifact or cross-job output. No manual-key fallback or automatic refresh. Each isolated .NET child has CLI home plus ADD_GLOBAL_TOOLS_TO_PATH=0 and host-state checks; no local SDK/Guard resumption or IFX runner use.

Publish order is Contracts → Core → ArchUnit → Testing → Cli. Each accepted push is followed by signed served-byte/payload/owner proof before the next push. Reruns, duplicate-skipping as proof and replacing a public version are forbidden. A separate job without OIDC/key repeats public signature/owner/payload checks. It is only part of NR06: fresh public consumer, offline closure, audit, human-operated lifecycle/UI and final acceptance remain required and must not be inferred from model tests or old screenshots.

## Stop, rollback and later closeout

Close the version switch and reject/cancel pending jobs if prerequisites change. Switch closure is not guaranteed to revoke an already running job or issued one-hour credential. A user may separately disable/delete the exact trust policy to prevent future exchange; this does not undo existing pushes or guarantee revocation of an issued key. Keep safe nonsecret receipts, never environment/debug dumps.

On a timeout or partial publication, stop later packages and withhold announcements/tag/Release. Reconcile each exact server version, signature, owner and payload. Only a new approved transaction can resume missing identical submissions; the initial activation does not implement automatic resume. Defects require separate user-authorized unlisting/deprecation and a newly qualified correction version. Exact-version restores can continue after unlisting.

NR07 tag/Release remains separately authorized, targets the original package source, is package-only and make_latest=false. No new installer, standalone Web ZIP or live IFX adoption. Preserve previous versions/configuration, original unsigned preview and new signed-public evidence. Additional Guard integration remains human-operated, one reviewed command at a time on an approved external host; local Guard/dotnet stays stopped unless the user separately changes that boundary.

Official references: [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), [deployment review](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/review-deployments), [NuGet signing](https://learn.microsoft.com/en-us/nuget/api/repository-signatures-resource), [unlisting](https://learn.microsoft.com/en-us/nuget/nuget-org/policies/deleting-packages).
