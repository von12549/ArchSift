# W06 reports and CLI acceptance

Date: 2026-10-06 (Australia/Sydney).

Conclusion: W06 COMPLETE. The shared Core service combines project/assembly results into schema-validated JSON and escaped HTML. CLI analyze/verify/draft, argument overrides, effective configuration, exit codes, cancellation and partial results were implemented.

Final run `b68f322bcef54c9a828bbba34c443bb3`: SDK 10.0.303/net10.0/Debug; offline locked restore/build succeeded with zero warnings/errors.70 unit +17 integration +2 architecture =89/89 passed. Environment/Profile hashes remained unchanged at every checkpoint and completion.

New coverage included analyze without rules; default violation exit 0; zero-match exit 4/disabled not-applicable; stable CLI/service finding parity; unknown argument 2; missing-file execution error 3; HTML XSS escaping; independent current run IDs; precancelled 130; completed project results retained on Worker failure; and actual CLI JSON/HTML/draft with explicit overrides. An old entry test incorrectly treated verify as unimplemented and was changed to an actually unknown command. Version/help behavior remained.

Archived [checks](w06/checks.json), [host-final](w06/host-final.json), [build](w06/build.stdout.log) and [test](w06/test.stdout.log). Public fields/limitations are in [reports](../reports.md) and [CLI](../cli.md). No real IFX/Guard integration started. W07 would use the same Core service/serializer for loopback UI and current/historical-result separation.
