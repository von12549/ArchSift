# IFX 0.11.3-rc.1 new-machine archive verification

Recorded 2026-10-30 (Australia/Sydney). Status: **scoped original machine evidence verified and archived**. This closes the outstanding original-receipt archival task for the accepted local/new-machine Launcher workflow. No product or tests were rerun.

The received ZIP `ifx-rc113-evidence-4c15a74b85574b0b846ce9de2998bdf7.zip` has SHA-256 `355743ef79f597147a905dfbe84da2d118a7a8dd78948965b268bd5a22e1c1fc`. Its external checksum, all 40 unique safe entries, 38 archive-manifest rows, 39 SHA256SUMS entries and 35 copied original-file rows match. The received standalone collection report agrees with the archived report. An unchanged archive copy and verification.json are retained under `C:\Users\von12\.codex\visualizations\2026\10\27\01a17b40-c2aa-76b3-9ad8-af61e115451f\ifx-host-0.11.3-rc.1-integration-evidence\new-machine-archive-verification-20261030`.

## Verified original records

- Six outer closeouts: five successful operations (Prepare, Restore, Init and two Start operations) and the original failed Restore at host-prerequisites. All retain hostEqual=true and activationPerformed=false.
- Eight successful tool/project restore stage receipts, three initialization records and two Launcher product closeouts with exitCode=0.
- SDK 10.0.303 and Core/ASP.NET runtimes 10.0.11, using C:\Program Files\dotnet\dotnet.exe. All three initializations have dailyTestConfig=null.
- Input identity `628150e41bb4825f774e72a7f98338b964ca664d27c4ca9bc7ed2976ebf5f8d2`, target C:\IFX\IFX-New and per-user state key `3d40eb9d9fa865c1` agree throughout. Startup configuration is under that user's projects\ifx-new\config\workbench.json.
- Original before.json and reuse-checks.json: all 11 checks true; pre-repeat local/startup configuration hashes independently match archived configuration bytes.
- The 54-file source application receipt and operation records match the approved source manifest and baseline. Five package hash observations match approved package hashes; 32 backup/atomic-backup observations match the 16 modified baseline file identities. Actual package/backup bytes remain on the new machine.

## Evidence boundary and next scope

The original collection report remains `archived-with-gaps`. Its sole gap is the collector's fixed reminder that historical Verify console output and human UI confirmation cannot be reconstructed. Previous explicit operator confirmations cover those observations; the archive additionally contains the guarded source application record. Do not rewrite the report or fabricate a historical transcript. No independent agent UI inspection or remote filesystem observation is claimed. Verification checks transferred bytes and their consistency; hashes are not trusted signatures.

Original-receipt archival and selected runtime metadata are now complete for this scoped handoff. Standalone Doctor, direct Workbench, the full real failure matrix, RC offline qualification, migration/rollback/activation and future published-package public-feed-only clone acceptance remain pending. The earlier SDK failure/recovery covers one prerequisite case, not that full matrix. Architecture tests, active configuration switching, commits and publication remain excluded.

Current package/source behavior and the delivered candidate kit remain unchanged. This addendum supersedes earlier statements that the new-machine receipt archive or exact runtime patches were unavailable. The earlier report-only closure and original collection ZIP are preserved as historical evidence.
