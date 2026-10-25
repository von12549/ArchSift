# NR06 disconnected and human environments: proposed arrangement

Status: **AWAITING USER CONFIRMATION.** No VM, paid resource, account, credential, remote tunnel, private-source transfer, actual disconnection or user acceptance has been provisioned or executed. NR06.00-NR06.03 authority does not activate these later lanes. Publishing remains closed and the author machine does not run Guard/dotnet.

## Recommended reusable arrangement

Use a user-owned disposable Windows x64 VM on a separate approved virtualization host, with an interactive console independent of the guest network. An existing licensed Windows 11 or Windows Server 2025 image may be used after the user identifies it; this plan does not purchase a license or cloud resource. Proposed sizing is 4 vCPU, 8 GiB RAM and 80 GiB scratch disk, subject to fixture measurement. Require an existing browser and PowerShell >=7.4. Access the online phase by the user's normal private RDP/console. Strict disconnected interaction must use the hypervisor/KVM console: disconnecting the guest NIC also breaks ordinary RDP.

Create separate clean checkpoints/copies for connected, disconnected and rollback states. Record VM/image ID, console path, scope/cost owner, scratch/evidence roots and expected teardown. Do not install a self-hosted GitHub runner or alter the existing IFX runner. The user retains machine/console credentials; no credential is pasted into a command, repository or evidence artifact.

Provision exact SDK 10.0.303 plus compatible Core/ASP.NET 10.0.13 from verified official portable archives before test baselines. Extract into an owned guest tool directory and invoke explicit paths. Use existing PowerShell/browser prerequisites from the approved image; no MSI, setx, persisted PATH/environment, Profile edits or trust-policy relaxation. Existing compatible host prerequisites may be used only after identities are measured and frozen. All test SDK/home/cache settings stay in the reviewed child environment.

Use a public-safe synthetic net10.0 target with frozen required-case inventory (the baseline gate is 3 C# plus 1 JSON and one ruleset). No real IFX source, original machine state or private inventory is transferred. A private IFX lane would need separate target/privacy/transfer approval, exact policy/offline/runtime binding and private evidence handling. Generic acceptance cannot claim real IFX integration.

## Connected preparation and transfer

After separate execution authorization, download the exact five public signed archives, their actual cell-specific dependency closure and required framework/test/tool packages. Independently verify owner, signatures, nonsignature payload, all raw archive hashes and actual canonical NuGet content hashes. Build a minimal test feed and manifest instead of copying a global cache or using old unsigned packages. Complete the separate fresh connected complete-closure audit before presenting offline results as fully qualified.

Freeze SDK/runtime bytes, standard NuGet.Config/global.json/tool manifest/locks, fixtures, policy, trusted signature/certificate prerequisites and evidence-tool identity. Missing trust prerequisites must be resolved by selecting a qualified approved image before the isolation snapshot, not by disabling signature checks or accepting unknown roots. Retain metadata/trust evidence for offline verification. Transfer the feed/fixture via a verified virtual disk or controlled host-to-guest artifact copy; hash the complete transfer before extraction and again in the guest. No file share may provide hidden package/network fallback during the offline window.

## Actual network denial

For Windows, disconnect every guest application-network adapter at the hypervisor after the prepared checkpoint, retain loopback, remove/make inaccessible shared network folders and access only through the out-of-band VM console. Record adapter/hypervisor state, routes, controlled denial probes before and after the acceptance window and process/network telemetry during it. No process under test may attempt external traffic. No author-host firewall, machine policy, persisted environment or live source changes are allowed. If the console/isolation mechanism cannot be demonstrated reliably, mark strict offline INCONCLUSIVE and keep full NR06 open.

For Ubuntu machine-only acceptance, the proposed future lane is a task-owned container on a disposable hosted runner with an exact SDK/runtime image digest, --network=none, loopback retained, all needed prerequisites preloaded, read-only input mounts and fresh owned writable caches/state. Verify the isolation mechanism independently. Do not claim this Linux lane covers Windows project entry or hands-on Workbench behavior. It belongs to NR06.04 and is not dispatched by the current authorization.

Run local-source-only configuration acceptance separately from actual disconnected acceptance. In strict offline use allowNetwork=false, cleared inherited sources/fallback/audit sources, exact pins and fresh package/HTTP/plugin/tool resolver/CLI-home caches. Do not use --ignore-failed-sources, credential prompts or an unreviewed fallback. Offline functional audit is not-run-offline; only the separate connected audit can pass auditing. Collect private receipts locally during disconnection; reconnect or transfer verified evidence through the hypervisor only after the acceptance window and zero-owned-process closeout.

## Human sequence and required confirmation

Before provisioning/execution the user confirms: (1) the Windows VM/hypervisor/image and console arrangement, whether an existing machine or separately approved paid resource is used, and teardown owner; (2) public synthetic target or a separately authorized private IFX copy; (3) the proposed NIC-disconnected Windows and network-none Ubuntu isolation boundaries; (4) private evidence location and allowed transfer method. Confirmation of infrastructure does not prefill acceptance or authorize publishing.

When machine evidence is ready, provide one reviewed command block at a time for that exact remote VM and wait for the user's result. First identify machine/session, source/package hashes, exact target, state/cache and expected inventory. Distinguish .archsift/workspace.json, generated external startup configuration and daily inventory/test-input JSON by exact purpose/path. In connected mode the user checks Launcher and its owned Workbench, version/target/rules/results/core navigation and Safe shutdown; after a closed-listener receipt, independently checks Workbench. Repeat the agreed essential flows in the isolated checkpoint through the VM console. The user records every mandatory answer, defects, a fresh 1-5 usability rating and explicit acceptance; old 4/5 or CI screenshots are not answers.

Teardown stops only verified owned processes, preserves inputs/failed/rollback evidence, confirms host/PATH/Profile equality and exports hashed receipts. Revert/remove only the explicitly owned guest checkpoint/disk after evidence retention and the user's selected retention decision. Do not unlist packages, create tags/Releases, install a new installer, reactivate local Guard or touch live IFX as part of this arrangement.

See [authorized implementation scope](20261025-nr06-consumer-qualification.md). This document is an arrangement proposal and contains no runnable local SDK command.
