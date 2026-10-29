# N02 missing CLI package acceptance

Recorded 2026-10-30 (Australia/Sydney). Status: **passed-new-machine-missing-cli-rejection**.

The operator supplied `N02-missing-cli-e166917685b4446f8502eb1baaf1f887` from the disposable fixture under `C:\IFX\negative-validation\fixture-5a66ac63faa34c33ad9cb7152a3c5725`. The copied entry used `Prepare`, candidate version `0.11.3-rc.1`, `C:\Program Files\dotnet\dotnet.exe`, the case-specific four-package source and empty case storage. The CLI archive alone was absent.

## Reviewed results

- Child exit code: 1. Result status: expected-rejection-verified.
- All 11 checks true, with result.json and checks.json agreeing.
- The read-only probe records System.IO.FileNotFoundException, HRESULT -2147024894 and the exact omitted CLI filename. Actual entry stderr contains the same missing-file message and full case-specific path, followed by child-exit/no-fallback failure. stdout.txt is empty.
- Seven inventory entries match exact SHA-256 and byte length. The manifest itself is separately hashed in the archive inventory. No failure.json or unexpected evidence files were supplied.
- The four supplied non-CLI package archives match package-subset.json and the hash-bound immutable delivery approval in identity, length and SHA-256. No CLI is present in the supplied subset.
- The accepted four configuration/marker hashes, original copy-manifest digest and host baseline agree with retained N01 before.json.
- The operator checker records both disposable storage roots empty, the subset unchanged, all original/copied source/package inputs unchanged and host equality. All dotnet, restore, product, architecture-test, activation, commit and publication flags are false.

Eight original evidence files and four package archives were copied byte-for-byte to `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-host-0.11.3-rc.1-integration-evidence\N02-acceptance-20261030\original-case`. The independent verification report is `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-host-0.11.3-rc.1-integration-evidence\N02-acceptance-20261030\verification.json`; its SHA-256 is `79047fb9b8ff81cf1b7cd077931eeebdc53fe25428d13d82b552fd5d4e7b4f28`. The shared originals, immutable delivery ZIP, package approvals and previous N01 artifacts were not modified.

## Evidence limits

The review directly verifies the supplied bytes and their consistency. It does not independently inspect the live new-machine C:\IFX tree; unchanged source/state/configuration checks there are operator-generated evidence. A separate console transcript of the checker process exit code was not supplied, so no independently observed checker exit 0 is claimed. The successful result, eleven checks and completed manifest establish this case's acceptance. No normal closeout.json is expected because package validation rejects before operation revision creation.

The localized wrapper portion of stderr has encoding corruption, while the exact missing-file message and path remain readable and match the recorded exception. Original stderr is preserved without correction. The no-execution scope is supported by reviewed Prepare control flow and receipts, not OS-level process tracing. The reviewer ran only read/hash/archive/document operations and did not rerun the IFX entry, SDK, restore or product.

## Remaining scope

N01 and N02 are closed only for their respective early validation failures. Next is N03: alter only a CLI package in a fresh disposable five-package copy and verify the hash-approval rejection with Prepare. N03 and the later generated-configuration, operation-occupancy and restored-payload integrity cases remain pending. No global negative-matrix, architecture compliance or public-feed-only clone acceptance is claimed. Optional offline qualification remains skipped. Activation/migration, commits and publication remain outside this work.
