# N02: missing candidate CLI package rejection

Prepared 2026-10-30. Status: command ready, awaiting operator execution on the new machine. This is an external acceptance helper, not a replacement for the immutable delivery ZIP or package bytes.

Helper: `Test-IfxRc113MissingCli.ps1`  
SHA-256: `7a47fa8874a285957ff01e7cd7794e1eb6fbf8724cb92943445dd5e2b6091a70`

Use Windows x64 PowerShell 7.4 or later without Profiles. Close the accepted Launcher/Workbench before running. Copy the helper to `C:\IFX\Test-IfxRc113MissingCli.ps1`. The existing fixture must be `C:\IFX\negative-validation\fixture-5a66ac63faa34c33ad9cb7152a3c5725`, with its original preparation.json and copy-manifest.json. The original approved delivery remains in `C:\IFX\delivery-verify-01`; accepted source/state remain `C:\IFX\IFX-New` and `C:\IFX\host-state-01`.

```powershell
$ErrorActionPreference = 'Stop'
$script = 'C:\IFX\Test-IfxRc113MissingCli.ps1'
$expected = '7a47fa8874a285957ff01e7cd7794e1eb6fbf8724cb92943445dd5e2b6091a70'
if ((Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expected) {
    throw 'N02 helper SHA-256 mismatch; stop.'
}
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoLogo -NoProfile -File $script
$code = $LASTEXITCODE
Write-Host "N02 checker exit code: $code"
if ($code -ne 0) { throw 'N02 checker failed. Preserve evidence; do not reset or continue.' }
```

The helper checks 974 source and five package copies against the original delivery and four protected configuration/marker hashes. It creates `cases\N02-missing-cli-<guid>` inside the fixture, copies only the four non-CLI packages into `packages-without-cli`, creates empty case `host-state`, and invokes the copied `archsift.cmd -Action Prepare -Mode Launcher` with that subset and `C:\Program Files\dotnet\dotnet.exe`. It does not delete the CLI from any existing package directory. The base fixture host-state must remain empty.

Expected outcome: the child exits nonzero (normally 1), identifies the absent `ArchSift.Cli.0.11.3-rc.1.nupkg`, and stops before preparation/restore. The checker prints `N02 expected rejection: PASS`, exits 0, and records `status: expected-rejection-verified`. All 11 checks must be true. Missing-file messages printed during this negative case are expected; a final checker exception is not a pass.

The runner derives the exact localized missing-file message through the hash-bound read-only hash function, records `System.IO.FileNotFoundException` and the exact missing filename, then checks the real entry stderr. It normalizes only formatting for comparison. Unexpected culture/formatting or any unrelated failure stops for review without relaxing the criteria. Four subset package hashes, original/copied source/package hashes and accepted configuration hashes must remain unchanged; both disposable state directories remain empty, with no prepared-success output and host equality. Execution flags for dotnet, restore, product, architecture tests, activation, commit and publication remain false. Those flags rely on the reviewed Prepare control flow and evidence, not OS-level process tracing.

The case evidence contains before.json, package-subset.json, expected-error.json, stdout.txt, stderr.txt, checks.json, result.json and evidence-manifest.json. The last file inventories SHA-256 and length of the other files and excludes itself; failure.json is added on failure. No normal operation closeout.json is expected because rejection occurs before revision creation. Keep all outputs, including failures. Copy the complete `N02-missing-cli-<guid>` case directory to the shared evidence location for review before another negative gate.

Preparation validation: PowerShell syntax plus 14 matcher checks passed, including a file-I/O-only subprocess producing real missing-file stderr, wrapped/ANSI messages, wrong package/path, permission errors, unrelated hash/host errors, generic child failure and empty expectations. Validation result: `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-host-0.11.3-rc.1-integration-evidence\N02-harness-regression-4aed96ba6ec14619a9624fd824374af0\results.json`. This is not a new-machine acceptance pass and did not invoke the IFX entry, SDK or product.

The optional offline scheme remains skipped. No architecture test, configuration activation/migration, commit or publication is included.
