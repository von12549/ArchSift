# Lightweight architecture-test Workbench LT01 synthetic acceptance

Date: 2026-10-15 (Australia/Sydney). **LT01 SYNTHETIC COMPLETE / LT02 NOT STARTED / PRODUCT NOT ACCEPTED / NOT RELEASED**. Code commit `082d0ec0e92ea805c1630ee29631c0ee0ba06603`, branch `codex/archsift-lightweight-workbench`, continues from LT00 `da1bcff326e895baa4c80879b4cb0a1608b0ad32`. This report/plan status receive a separate LT01 record commit. User authorized later development/acceptance/stage commits/pushes and specifically LT02A/LT05 offline operations on independent IFX copies under D:/ArchSift-lab. Original IFX checkout/install remain read-only; final independent human UI stays user-owned.

## Scope and inputs

- SDK `10.0.303`, runtime `.NET 10.0.11`, net10.0, Release, Windows x64; xUnit v3 `4.0.1`/Microsoft.Testing.Platform native in-process runner. Explicit local offline source C:/Users/von12/.nuget/packages restores into ignored development cache. Serial solution --locked-mode restore passed without network sources.
- Synthetic passing rules SHA-256 `b9669d62d9195f9322a1fda34b46003472564070c8ec492d71234900cc88b341`; violating rules `f2a9e089715735f07b1ef55f8e4462ef9e4ee6b80dfdbb75780e1683e3470274`. Mechanism tests only, not approved IFX B daily policy. Original IFX baseline remains `cc06528ab17052baafa496df2a89f6cde9207384`; that checkout was neither run nor modified.
- New ArchSift.Testing adapter reuses Core schema/semantic validation, rule identity, selectors and exceptions; cases use `archsift-json:<rule-id>`. TypeDependencyTestEngine reads actual assemblies in-process and traverses type/generic dependencies with ArchUnitNET, without UI/CLI/Worker precomputed results or fabricated SourceBound. Existing AssemblyWorker and new entry share dependency-target traversal; old reports/schemas unchanged.

## Actual checks

| Check | Result |
| --- | --- |
| dotnet restore ArchSift.slnx --locked-mode, explicit local offline source, -m: 1 | 11 projects restored; initial default parallel invocation exited 1 without diagnostics, serial retry passed |
| dotnet build ArchSift.slnx --no-restore -c Release -m: 1 | 11 projects passed, 0 warnings/errors |
| New xUnit v3/MTP passing synthetic entry | 7/7, 0 skipped/not-run; actual field/method-signature/generic violations yield stable findings, exceptions retain ID/reason, zero matches inconclusive; all-disabled, duplicate IDs and unsupported rules reject |
| New negative MTP child, child-only ARCHSIFT_LT01_NEGATIVE=1 | One actual failure of seven, exit 1; ForbiddenSource -> ForbiddenTarget message proves real parameterized-test failure |
| Existing UnitTests/IntegrationTests/ArchitectureTests | 151/151, 95/95, 2/2, zero skips; new dependency direction registered/reverified |
| git diff --cached --check | Passed; code commit includes required Codex Co-Authored-By trailer |

Initial restricted-sandbox VSTest testhost could not connect within 90 seconds and aborted unit tests. Approved host execution using identical --no-build --no-restore arguments passed all three old projects; the abort is not a pass. Every .NET before/after environment/Profile hash was `364ec6a885b3387d0b93f1bc14be64d1d6c85d0a3bcd331b0240503e7666efdb`, without persistent drift. New tests delete exact temporary JSON in finally.

## Coverage boundary and next stage

LA02–LA04 synthetic mechanisms passed. LA05 actual assembly scope and LA07 IFX runner compatibility await LT02A. No formal B policy enters daily gates. Original IFX architecture tests did not run; actual discovery/execution of the derived 80 cases, whole-project coverage, snapshot switching and real source/build causality remain unproved. No UI, coordination, history, installation or full evidence work. LT02 implements headless coordination/coverage blocking; authorized external IFX LT02A must pass before LT03.
