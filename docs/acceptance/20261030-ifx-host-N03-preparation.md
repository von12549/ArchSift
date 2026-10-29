# N03: candidate CLI hash mismatch rejection

Prepared 2026-10-30. Status: command ready, awaiting new-machine execution. N01/N02 remain closed. This helper is external to the immutable delivery kit.

Helper: `Test-IfxRc113TamperedCli.ps1`  
SHA-256: `1ea96c4b2e1d1803868f5d184d10d956d0230e95b9350eb4af18a955dad11998`

Use Windows x64 PowerShell 7.4+ without Profiles. Close the accepted Launcher/Workbench first. Copy the helper to `C:\IFX\Test-IfxRc113TamperedCli.ps1`. Keep the existing `C:\IFX\negative-validation\fixture-5a66ac63faa34c33ad9cb7152a3c5725` intact, including preparation.json, copy-manifest.json, its 974 source/five package copies and empty base host-state. Preserve the approved delivery in `C:\IFX\delivery-verify-01`, accepted source/state and previous case evidence.

```powershell
$ErrorActionPreference = 'Stop'
$script = 'C:\IFX\Test-IfxRc113TamperedCli.ps1'
$expected = '1ea96c4b2e1d1803868f5d184d10d956d0230e95b9350eb4af18a955dad11998'
if ((Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expected) {
    throw 'N03 helper SHA-256 mismatch; stop.'
}
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoLogo -NoProfile -File $script
$code = $LASTEXITCODE
Write-Host "N03 checker exit code: $code"
if ($code -ne 0) { throw 'N03 checker failed. Preserve evidence and stop.' }
```

The helper revalidates the preparation and copy-manifest digests, all 979 original/copied inputs and four accepted configuration/marker hashes. It holds the accepted revision's operation lease. It creates `cases\N03-tampered-cli-<guid>`, copies all five approved packages into `packages-tampered-cli`, and modifies only that CLI copy. The mutation requires the exact approved CLI hash and a case-specific target path; a repeated mutation or another case/root is rejected. The final byte at offset 2906315 changes from 0 to 1, with 2906316 bytes retained. Original SHA-256 is `60d4eaac1129de7ccddcd5fa4b9bf5124f758605c799d695cc797221134cd051`; mutated SHA-256 is `9932142c2a5283100c1eef9938f69fc4a8bfe6461fdaf382eb7323a3ece2c528`. mutation.json records and verifies the one-byte difference.

Only the copied entry's `Prepare` action is invoked, using `C:\Program Files\dotnet\dotnet.exe`, the new five-file source and empty case host-state. The hash-bound entry validates the package hashes before resolving the host or creating an operation revision. Expected child exit is nonzero, normally 1, with `Candidate source package differs from approval.` No ordinary closeout.json is expected. Error formatting may wrap the literal; generic child failures, missing files/hosts, directory or permission errors alone are not accepted.

Expected checker result: `N03 expected rejection: PASS`, checker exit 0, result status `expected-rejection-verified`, and all 13 checks true. Checks cover nonzero child exit, exact approval rejection, CLI existence/unchanged length, mismatch from approval, preservation of the recorded mutation, unchanged four other packages, exactly five package files, both storage roots empty, no prepared-success output, unchanged original/copied inputs, unchanged accepted configuration and host equality. Execution and activation flags remain false based on reviewed Prepare control flow and evidence, not OS process tracing.

Evidence: before.json (including helper SHA-256), package-copies.json (all five approved pre-mutation hashes), mutation.json, stdout.txt, stderr.txt, checks.json, result.json and evidence-manifest.json. The manifest hashes and sizes the other evidence files, excluding itself. failure.json is preserved on error. Keep the mutated package as evidence; do not restore, run, promote or replace an approved package with it. Copy the complete `N03-tampered-cli-<guid>` case directory to the shared location and report the printed checker exit code. A final checker exception is a failure to review, not an expected pass. Do not rerun automatically.

Helper validation: syntax plus 16 synthetic checks passed, covering exact byte mutation, independent byte comparison, rejection of repeated mutation/original root/other case, original preservation, real hash-assertion stderr, literal/wrapped/ANSI formatting and unrelated errors. Result: `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-host-0.11.3-rc.1-integration-evidence\N03-harness-regression-74117f55c9f54ab1a146aff05ed57da1\results.json`. These checks used only a disposable local package copy, hash/byte operations and an assertion-only PowerShell subprocess; no IFX entry, SDK or product ran. New-machine N03 acceptance is not yet claimed.

Optional offline qualification remains skipped. No restore, architecture test, active configuration switching, migration, commit or publication is included.
