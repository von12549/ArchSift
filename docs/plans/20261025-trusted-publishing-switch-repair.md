# Trusted Publishing RELEASE_SWITCH wiring repair

Authorized on 2026-10-25: repair the missing step environment binding, add publish-mode regressions, complete normal PR/CI/merge, and refresh the credential-free preview and final confirmation card. Keep the operational switch absent/false. No credential acquisition, publish-mode hosted dispatch, NuGet push, tags/Release, new installer, local Guard/dotnet, persistent host mutation or existing gate changes. A new real publication attempt requires separate explicit authority.

## Preserved failure and minimal change

The single authorized run 40154094089 at main 3fccc260f8bd3408cb48a72391c8231f4a3f918b failed before audit/environment approval/login/push. Its prepare step received RELEASE_SWITCH=true; its verify-preview step did not receive that variable. The unchanged publish context guard rejected the missing value. The operational switch was closed and read back absent; no retry occurred. The independently downloaded input ZIP retained all seven original files. Original logs, ZIP, closure receipt and stopped evidence remain immutable in D:/ArchSift-lab/runs/archsift-ta06-publish-20261025-01.

Add only the existing repository variable expression to the preflight input-verification step and its strict workflow schema. Do not hardcode true, weaken checkContext, grant additional permissions or alter package bytes/source acceptance. Retain all original regression coverage.

## Regression and hosted verification

Execute the real production verify-preview CLI with the actual YAML step environment, synthetic package/reference bytes and a test-only preload that replaces only those synthetic inputs and all transports. Unknown network operations, credential endpoints, writes and native child execution fail. No fixture switch is added to production code. Prove publish mode with the correctly resolved true switch reaches complete artifact provenance/digest/request/package validation; absent/empty/false/nonexact switch fails before any artifact read. Preview remains credential-free with a closed switch. Corrupted ZIP, cross-run metadata, request mismatch and injected credentials still fail. Structural mutations reject missing, literal or wrong-variable wiring.

Run local Node and PowerShell metadata/synthetic checks only. Normal exact-head and actual-main Windows/Ubuntu CI must pass, with independent official artifact/test/host-state readback. Rehearse complete inverse-patch applicability without applying it.

## Refreshed preview, identities and rollback

After merge/main CI, create a unique independent data branch by verifying/reusing the six prior public blobs and generating only the request bound to the new workflow SHA. Preserve both earlier data branches and all accepted package assets. Run closed default preflight and a new preview only; independently verify input/audit ZIPs, original bytes, same-run bindings and fresh official source audit. Publisher/public-verification remain skipped. Record a new external final confirmation card including the failed-run reference and new workflow/data/request/run/artifact identities. Historical cards stay unchanged and are superseded only for the current next-attempt identity. Human policy readback remains historical configuration evidence, never a new publication approval.

On repair/preview failure, preserve all evidence and stop for diagnosis; never enable the operational switch or substitute a real publish run as a test. A reviewed inverse PR can revert this binding/schema/test change without changing original packages, historical records, host environment or safety rules. Credentials, any new publish attempt and actual environment approval await the human's separate authority.
