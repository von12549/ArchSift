# W04 project-rule acceptance

Date: 2026-10-06 (Australia/Sydney).

Conclusion: W04 COMPLETE. Implemented project references, two graph-integrity checks, literal TFMs, direct NuGet-ID denylists, project/source-path naming and unaccepted rule suggestions. Assembly/type naming and type-dependency remained for W05; declaration graphs were not substituted for them.

Per-rule results distinguish pass, violation, inconclusive and not-applicable. Zero source/scope matches default to inconclusive; explicit allowEmpty gives not-applicable. Disabled rules do not run. Known violations remain and take violation precedence while preserving partial-coverage limitations. Exceptions reference stable IDs/reasons without deleting the original finding/severity; effective violation counts exclude explicitly exempt items. Finding IDs combine rule ID, subject and dependency evidence, with stable ordering.

Rule suggestions separately output observedFacts, candidateRules and unresolvedDecisions. Every candidate is disabled and suggests review only of internal-reference resolution/supported frameworks. Existing dependencies do not automatically become allowed policy.

Final run `76d50c072ddb469c8ca60018d6d910c1`: SDK 10.0.303/net10.0/Debug; offline locked restore/build succeeded with zero warnings/errors.65 unit +12 integration +2 architecture =79/79 passed. Host environment/Profile hashes matched at every checkpoint and completion.

New actual-declaration fixtures cover each project rule's clean/violation/missing/unsupported paths; NuGet casing and ID-only behavior; retained exception evidence; allowEmpty/disabled; missing solution-membership prerequisite; external/uncovered states; stable finding IDs; source-path naming and unaccepted candidate state.

Archived [checks](w04/checks.json), [host-final](w04/host-final.json), [build](w04/build.stdout.log) and [test](w04/test.stdout.log). No additional third-party dependencies or Guard-source migration. W05 would implement assembly inputs, Workers, real ArchUnitNET checks, isolated/offline builds and trust-negative cases.
