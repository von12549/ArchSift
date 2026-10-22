# IFX field analysis and independent-operator tasks

Status: `COMPLETE / PASS`. Real IFX, constructed fixture, independent scoring and final safety/cleanup completed; see [W08 completion](20261007-w08-completion.md).

Prerequisite: freshly extract W08 ZIP and verify hash/manifest. TargetRoot is original IFX checkout, read-only; reports under D:\ArchSift-lab. Reference HEAD `64ef2674c57e6cf9031481d6d4410cca55b0dad5`; re-record actual HEAD/ordinary changes, preserving user changes.

## Block 1 (discovery only, no build/restore)

Run this block in normal PowerShell and return operator-result.json; do not run later blocks automatically:

```powershell
pwsh -NoProfile -File D:\ArchSift\scripts\Invoke-IfxManualStep.ps1 -PackageRoot D:\ArchSift-lab\packages\smoke-8492698999f94ea9a535a42263fc417f\unpacked
```

Script verifies candidate files, records actual Git identity/status and safe environment/Profile hashes, invokes native analyze with explicit net10.0/Debug and default existing-artifact mode without required rules. No IFX restore/build, Guard execution, source/CI/persisted-environment changes. Exit 0 means declarations discovered; 4 means accurately reported coverage limits, not success. Preserve evidence and stop for other nonzero exits or drift.

After block 1 results, review block 2 rule/UI commands and execute individually. Unsupported IFX conditions/expressions/generators must be reported inconclusive with reasons; old Guard 8/8 or handwritten manifests cannot prove new capability.

## Independent operator A10

At least one nonimplementer independently chooses supported target/entry/external output, discovers without rules, creates a project-reference/NuGet rule from template with reason, runs/identifies violations, creates/inspects missing-input inconclusive, saves rules/exports reports/Markdown, and understands different result meanings. Also inspect real IFX report and constructed violation.

Record actual start/end, documentation, help L0–L4, confidence 1–5, each result/failure evidence. L0 independent; L1 general prompt; L2 document location/one error explanation; L3 field-by-field guidance; L4 proxy operation. A10 requires confidence>=4/5 and maximum L2. Implementer cannot fill answers; browser automation is not independent usability.

After field work, recheck User/Machine environment, Process PATH, four Profiles and original checkout. Clean only precisely recorded outputs; retain reports/operator evidence. No recursive source/broad-lab deletion.

## Completion results

Real IFX existing/net10.0/Debug discovered 105/105 projects. 105 declaration limitations accurately produced partial, zero execution errors. Exact project-reference rule found IFX.Application.Primitives -> IFX.Domain.Primitives, partial/noncompliant. No build/restore or target/product/persisted-environment changes.

Independent operator completed fixture/real IFX UI, confidence 4/5, conservatively maximum L2, A10 PASS. Final cleanup terminated precisely identified orphan candidate UI without deleting evidence. Final audit: zero candidate processes/listeners on three registered ports; host/IFX/product equal, failure=null/pass=true.
