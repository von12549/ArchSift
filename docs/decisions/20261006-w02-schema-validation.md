# W02 schema-validation choice

Date: 2026-10-06 (Australia/Sydney).

Use built-in .NET System.Text.Json to read contracts, embed four fixed draft-07 schemas and implement only the keywords those schemas use. No general external schema package is added. Core/Contracts keep no external runtime dependency and offline locks stay unchanged.

Rule loading performs schema validation before separate scope/parameter/duplicate-ID/exception/policy-conflict semantic validation. Duplicate JSON keys reject. Limits are 4 MiB/depth 64. $ref permits only internal document pointers, with no remote-schema execution surface. Unknown object fields are configuration-error. Parsed integers may use mathematically integral notation; int DTOs separately enforce range. Hash original input bytes, not reserialized values, for provenance.

This validates fixed configuration contracts, not arbitrary user schemas or full draft-07. PowerShell Test-Json independently checks six templates; .NET tests cover contract positives/negatives and JSON round-trips. Future fixed-schema keyword needs require implementation plus independent positive/negative tests, or a recorded decision to adopt a complete validator.

No new NuGet package/third-party source was added. Existing dependency locks/license records remain valid.
