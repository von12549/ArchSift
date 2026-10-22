# W09 change-comparison acceptance

Date: 2026-10-07 (Australia/Sydney). Status: COMPLETE / PASS.

- SDK 10.0.303 / Windows x64 / net10.0 / Debug; locked offline restore, compilation with zero warnings/errors and 106/106 regression tests passed (75 unit, 29 integration, 2 architecture). Final run `e03f75bcbbd14b9b82b8d0a8d52d0da3`. PowerShell Test-Json and actual CLI validate/render separately verified the six templates.
- Passed default HEAD/final-worktree state, simultaneous staged+unstaged changes, untracked source, cross-project impact, explicit endpoints, subdirectory root, export-ignore not changing blob identity, deletion/unique-content rename, rule deletion/scope/exception changes, baseline failure, context mismatch, insufficient coverage, cancellation, HTML escaping, CLI/Core parity and actual authenticated HTTP comparison.
- Synthetic browser run `D:\ArchSift-lab\runs\w09-ui-bd04886f37704ab092e7b92c9d0aadfd` detected a known added reference with exit 0. After an invalid local ref, current results/downloads cleared and old success remained only in history. See [comparison](w09/comparison.png) and [failure](w09/failed-current-comparison.png) screenshots. No new human usability score was supplied.
- User/Machine environment, Process PATH and all four Profile hashes matched at every .NET check and completion. The exact preview service stopped; port 9814 had no listener. Comparison changed neither source nor index. No real Guard/IFX integration ran.

Two initial failed regressions remain: `6d5a698d14d74627a9df16fde5a3cd13` (old help assertion, Windows fixture Git-object read-only cleanup); `1cbefdae489e487ab20bf3347a023dda` (fixture finding hash, empty ruleset/schema and assertion calibration). Failures were not rewritten as passes; host hashes always matched. No broad cleanup of failed synthetic-fixture remnants occurred. Original logs/TRX remained in development artifacts.

Boundaries: Git links/submodules/collisions are conservatively rejected and snapshots have file/byte limits. No incremental optimization, per-commit validation, automatic fetch, automatic exemption or fail-on. Existing build/DLL binding limits remain; see [change comparison](../changes.md). W10 was not yet complete.
