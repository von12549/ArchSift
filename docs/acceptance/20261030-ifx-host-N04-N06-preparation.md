# IFX 0.11.3-rc.1 N04/N05/N06 operator validation

Prepared 2026-10-30. Status: scripts and local simulations passed; new-machine acceptance pending. This is a separate validation bundle. It does not replace the frozen source overlay, delivery ZIP or approved packages.

## Scope and prerequisites

Use Windows x64 PowerShell 7.4+ with NoProfile and the already accepted .NET SDK 10.0.303 x64. Close Launcher/Workbench. Keep the reviewed fixture at `C:\IFX\negative-validation\fixture-5a66ac63faa34c33ad9cb7152a3c5725`, original five packages, preparation.json and copy-manifest.json intact. Keep `C:\IFX\delivery-verify-01`, `C:\IFX\IFX-New` and `C:\IFX\host-state-01` unchanged. The fixture's original host-state must remain empty. N01-N03 artifacts are not modified.

The suite verifies the original preparation/copy/delivery identities, all 979 original/copied source-package hashes and four accepted configuration/marker hashes. It holds the accepted revision's operation lease, creates a fresh `cases\N456-<guid>` directory and rechecks protected inputs and host hashes after each passing subcase. Only fresh case-local configuration, locks, logs and reconstructed caches are written.

## Five sequential subcases

| Subcase | Setup and operation | Expected outcome |
| --- | --- | --- |
| N04-local | Prepare a new case, change its local runtime path, invoke Prepare again | Nonzero exit; `Existing host configuration differs; preserve it for review.`; local-configuration failure receipt; edited file preserved |
| N04-nuget | Prepare another case, append a comment to its generated NuGet.Config, invoke Prepare again | Nonzero exit; `Existing generated NuGet configuration differs; preserve it.`; edited file preserved |
| N05-operation | Prepare a new case, exclusively hold its operation.lock, invoke Prepare, release the lock and invoke Prepare again | Actual sharing violation, no new run or changed revision while locked, successful recovery Prepare with reused configuration |
| N06-archive | Prepare a new case; copy five approved archives into a cache and extract the CLI payload; validate pristine files; mutate one byte in the cache CLI archive; invoke Doctor | Nonzero exit; `Restored package differs: ArchSift.Cli`; failedStage package-verification and productDispatchAttempted false |
| N06-web | Construct another pristine cache, mutate one byte in its Web DLL, invoke Doctor | Nonzero exit; `Cached CLI/Web payload differs.`; failedStage package-verification and productDispatchAttempted false |

N04/N05 execute no SDK operation. N06 issues only `--version`, `--list-runtimes` and `--info` against the installed dotnet host before its expected package-verification failure. It performs no NuGet restore, build, architecture test, UI launch or intended product dispatch. A pristine cache is checked by the unchanged hash-bound Assert-IfxHostPackages function, then the exact rejection is confirmed against mutated bytes before the real Doctor entry. If prerequisites fail, the wrong error/stage appears, or product dispatch is recorded, the suite stops and does not accept that subcase.

The user requested a batch handoff. All subcases execute sequentially; actual errors, closeouts, protected hashes and scope are checked before advancing. A failed subcase stops the batch, preserving evidence. This replaces the earlier manual handoff after each successful case for this batch only. Do not automatically retry or repair failures.

## Run all groups

Verify the ZIP against the separately supplied SHA-256, then extract into a new `C:\IFX\N04-N06-tools` directory. Do not overwrite an older bundle. Run from a PowerShell 7 x64 session:

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoLogo -NoProfile `
    -File 'C:\IFX\N04-N06-tools\Invoke-IfxRc113NegativeSuite.ps1' -Case All
$code = $LASTEXITCODE
Write-Host "N456 checker exit code: $code"
if ($code -ne 0) { throw 'N456 failed. Preserve evidence and stop.' }
```

Expected: five named `expected rejection: PASS` messages, then `N04/N05/N06 suite: PASS (5 subcases)`, checker exit 0 and `suite-result.json` status `expected-rejections-verified` with passedSubcases 5. Each subcase's checks must be true. Expected failing child operations normally exit 1; prerequisite/recovery Prepare operations exit 0. There are nine Prepare invocations and two rejection-only Doctor invocations, producing ten closeouts; the occupied-lock attempt creates no run/closeout. The batch reports dotnetMetadataExecuted true; restoreExecuted, productExecuted, architectureTestsExecuted, activationPerformed, gitCommitCreated and published remain false.

## Run one group

Use `-Case N04`, `-Case N05` or `-Case N06` on the same main script, or use the included `Test-IfxRc113N04.ps1`, `Test-IfxRc113N05.ps1`, `Test-IfxRc113N06.ps1` wrappers with `pwsh -NoProfile -File`. A single-group run uses a fresh suite and expects two, one or two passing subcases respectively. Do not run the wrappers again after a successful All run merely to repeat the same acceptance.

## Evidence and limitations

The final console line `N456 evidence:` identifies the whole suite directory to return. It includes suite-before.json, suite-result.json on success, per-case stdout/stderr, copied operation closeouts, checks/results, mutation/configuration or cache inventories, and an SHA-256/length inventory of all suite files. The inventory excludes itself. On failure, failure.json records the failing case and completed count; existing artifacts remain. Return the complete N456 directory and the printed checker exit code. Keep mutated files inside their case; do not use them as approved packages. No active configuration, original cache, accepted state or prior receipt is migrated or repaired.

N05 qualifies the IFX operation lock and its recovery, not ArchSift's internal workspace-session lock. N06 qualifies guards on a cache constructed from approved packages, not cache creation by a new real NuGet restore. Those boundaries remain explicit and must not be presented as broader product concurrency or cold network qualification. This handoff contains no architecture tests, activation/migration, commit or publication. Optional offline qualification remains skipped; local archive extraction here only creates isolated fault fixtures.

Local validation parsed all scripts and exercised five subcases through unchanged configuration/operation/integrity functions with real disposable file mutations and locks. Entry transport and three SDK metadata responses were mocked; a nonexecutable fake host ensured no real dotnet/product execution. Three additional checks rejected generic/empty error expectations and mutation outside a case. See validation-summary.json. These results do not constitute new-machine acceptance.

Bundle SHA-256: `d610146ca2f056ff76b8e95bbdfea600a1f6a3df148cf53bc7354e0c51b7c781`.
