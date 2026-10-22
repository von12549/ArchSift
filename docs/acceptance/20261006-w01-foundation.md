# ArchSift W01 foundation acceptance

Date: 2026-10-06 (Australia/Sydney)

Conclusion: W01 COMPLETE; W02 NOT STARTED. Initial complete offline restore, locked restore, compilation and foundation tests passed on Windows x64, SDK 10.0.303, net10.0, Debug. This is not six-rule or 0.1.0 product acceptance.

W00 local commit `5c378153abde38540dd81184706f92306091c53c` retains primary human author Xiaolong Feng and ends with `Co-Authored-By: Codex <noreply@openai.com>`. It was not pushed. W01 changes remain in the worktree; the user's instruction to commit and then start W01 applied to the W00 delivery at that time.

## Deliverables

Five product projects, three test projects and `ArchSift.slnx` exist. global.json, Directory.Build.props, Directory.Packages.props and NuGet.Config specify TFM, versions, warnings and sources; eight locks pin actual direct/transitive dependencies. Direct packages use exact ranges. Core/Contracts have no third-party packages; the independent ArchUnit layer references engine 0.13.4 and entry points compose services.

CLI supports help/version and returns 2 for unimplemented commands. The Web foundation supports only version, returning 2 otherwise without starting a server. Chinese stdout/stderr explicitly uses UTF-8. Shared product version and engine package/assembly identities are separate; no assembly architecture analysis ran.

The new development-check script uses child ArgumentList, isolated CLI home, paired global-tools opt-out, an offline local feed, isolated development package cache and environment/Profile hash comparisons; locked restore is default. Root .gitignore excludes own bin/obj and development artifacts; .gitattributes fixes text to LF so later checkouts do not change document bytes.

The [W01 dependency inventory](../dependencies/w01/package-inventory.json) records 19 actual package/version pairs. All lock contentHashes match new cache metadata; nuspecs and licenses for new test tools are archived. W00 candidate records remain historical; actual locks did not resolve System.Collections.Immutable. See [dependency notes](../dependencies/w01/README.md) for license sources and differences. No Guard implementation was copied/adapted; migration entries remain `migrationExecuted=false`.

## Final verification

Final run ID: `b5cc04a62542430da23d92c053397edc`.

| Check | Actual result |
| --- | --- |
| SDK resolution | global.json 10.0.100/latestFeature → 10.0.303 |
| Initial offline restore | Eight projects succeeded and generated locks, using only the explicit local cache feed |
| Locked restore | Both the second bootstrap restore and final restore after fixes succeeded |
| Debug build | Eight projects passed, 0 warnings / 0 errors |
| UnitTests | 2/2: shared product version, engine package/assembly identities |
| IntegrationTests | 5/5: CLI/Web version, CLI help, configuration errors for unimplemented commands and correct Web rejection without a server |
| ArchitectureTests | 2/2: all product reference directions; compiled Core/Contracts lack entry-point/Guard/ArchUnit dependencies |
| Host hashes | User/Machine environment, Process PATH and four Profiles matched after SDK/restore/build/test and at completion |

Archived: [checks](w01/checks.json), [host baseline](w01/host-baseline.json), [final comparison](w01/host-final.json), [raw build output](w01/build.stdout.log), [raw test output](w01/test.stdout.log), [results and raw TRX hashes](w01/test-summary.json). Original development TRX/logs remain locally under untracked `artifacts/development/<run-id>`; text archives use LF.

## Findings and fixes

The first sandboxed restore exited during SDK workload validation; environment hashes stayed equal. Normally approved execution could continue. The suggested workload update was not run. Invalid double hyphens in a NuGet XML comment were then fixed.

Tests after the first successful build exposed Chinese redirected-output encoding loss and confusion between NuGet and DLL versions. UTF-8 output was fixed. The local ArchUnitNET DLL reports AssemblyVersion 0.13.0.0 / InformationalVersion 1.0.0, so the adapter now generates package-version metadata from central configuration and separately retains the loaded assembly version. An initial informational-version-only fix was insufficient; its failure remains recorded. The final fix passed all 9/9 tests.

Development evidence for failed runs `abd0849b9aa54549ab61a0296fe7289c`, `35fac73db8de416faa10846fdb9cdcfe`, `a2921f5349aa4f03838e63db7db936fa` and `7abc54570cd540f78852dfb812838182` remains; every final host-state-equal was true. W00's manifest is its historical commit snapshot, not rewritten for W01 plan/README changes; new-stage delivery snapshots are separate.

## Acceptance boundary and next step

This verifies the development foundation and actual compiled dependency direction. No Guard/IFX integration, target analysis, six-rule execution, real positive/negative ArchUnitNET examples, reports or loopback UI ran. Unchanged host state does not imply all A09 boundaries, cancellation and failed-build fixtures passed.

NuGetAudit=false is offline configuration; no network vulnerability check ran. The new project was not published and no public license was chosen. Preserve all third-party licenses. W02 starts with rules/config/report/assembly-manifest contracts, JSON Schema draft-07 validation, six templates and CLI rules. Record rationale, version, license and lock changes before adding a schema dependency.
