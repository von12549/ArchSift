# NR06 SDK child background-network correction

The existing user authorization covers product repair, regression, normal PR/CI/merge, unpublished candidate preparation and human-operated spare verification. Public publication, publishing switches or credentials, tags/Releases, new installers, author-host Guard/dotnet and persistent host changes remain prohibited.

## Findings and retained evidence

P2F-CANDIDATE-COLD-V1 stopped on an omitted compiler image. Central review also found external DNS query intent during the bare SDK build; functional success did not establish strict offline success. P2F-CANDIDATE-COLD-V2, run p01-20261027T115638Z-9fe52bd4, seal 2b6ea603a7e9964327189e5272d288da11bdcfd506f04db080eb859dedb5a70c, passed five cold cells, four locked checks, four APIs, exact CLI/version/108-file payload, eighteen outer operations and six traces covering 56 complete process lifetimes. All loss counters and product DNS events were zero after the harness supplied `true`.

P2F-NEGATIVE-V1, run p01-20261027T130518Z-136f1e4d, seal c439ae71a95dd6af53ffab90f2475221dc8d72f6d47b8f438161dc26f66f3515, passed four expected rejections and five traces covering 30 process lifetimes. Missing Contracts failed with NU1101; the deliberately invalid Core archive failed ZIP parsing; a remote offline source was rejected before restore/state initialization; a one-byte Web HTML change was rejected before Web startup. Product DNS and loss counters were zero. These receipts qualify only their observed 0.11.1 consumer/rejection scope, not internal builds or independent UI.

The [SDK 10.0.303 workload updater](https://github.com/dotnet/sdk/blob/v10.0.303/src/Cli/dotnet/Commands/Workload/Install/WorkloadManifestUpdater.cs) uses bool.TryParse for the disabling variable. The string `1` is ineffective. Product-owned SafeProcess and workspace probes still supplied that value, while test-gate and UI child launch paths did not explicitly pin the setting. A qualified outer harness cannot establish that internally launched SDK processes retain the required value.

## Narrow repair and regression

Use literal `true` in current SDK child-launch settings across product, project bootstrap and build/verification scripts. Pin it after SafeProcess caller overrides, preserving child-only DOTNET_CLI_HOME plus DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0 and all host-drift checks. Do not modify persisted settings, package sources, trust, audit policy, target code execution policy or historical evidence. A source guard checks recognized assignment syntaxes and all five product launch paths. Four hosted integration cases inspect the actual SafeProcess child environment with absent, `1`, `false` and `true` caller values, require effective Boolean true and preserved home/tools/PATH and parent settings. Existing Windows/Linux regression remains mandatory.

Use distinct development/candidate version 0.11.2; do not rebuild or replace accepted 0.11.1 package identities. Retain the recovery fix and historical Setup fixture allowlist. The existing read-only candidate workflow builds five NuGet archives and embedded Web with exact source/hash provenance and no publishing capability. Hosted tests are not native offline evidence.

## Remaining acceptance

Require normal PR checks, review, merge and actual-main checks, independently inspected new candidate bytes, fresh dependency/embedded-Web audit and focused spare verification of product-internal SDK execution under the calibrated network observer. Requalify changed payload identities; do not reuse 0.11.1 acceptance as proof for new bytes. Then complete the independent human visual/usability review. Ubuntu remains DEFERRED_BY_USER; full NR06 remains incomplete until the required scope and explicit deferrals are accurately closed out.
