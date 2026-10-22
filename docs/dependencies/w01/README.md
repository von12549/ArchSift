# W01 dependency and provenance records

Date: 2026-10-06 (Australia/Sydney)

Eight project packages.lock.json files govern actual W01 dependencies. The [machine inventory](package-inventory.json) merges 19 distinct package/version pairs. Core/Contracts have no third-party packages; the independent ArchUnit layer pins TngTech.ArchUnitNET. Current version 0.1.0-dev, tool TFM net10.0.

## Direct dependency choices

| Dependency | Pinned version | Purpose | License |
| --- | --- | --- | --- |
| TngTech.ArchUnitNET | 0.13.4 | Frozen assembly-analysis engine; W01 composes identity only, actual checks in W05 | Apache-2.0; [W00 LICENSE/NOTICE](../../migration/third-party-notices.md) |
| Microsoft.NET.Test.Sdk | 17.14.1 | SDK testhost/dotnet test, tests only | MIT; [fixed-source LICENSE](licenses/vstest-17.14.1-LICENSE.txt) |
| xunit | 2.9.3 | Unit/integration/dependency-direction tests, locally cached/offline reproducible | Apache-2.0; [fixed v2 license/MIT exceptions](licenses/xunit-2.9.3-LICENSE.txt), [full Apache-2.0](../../migration/licenses/ArchUnitNET-0.13.4-LICENSE.txt) |
| xunit.runner.visualstudio | 3.1.4 | VSTest runner supporting .NET 8+/xUnit v2, PrivateAssets=all | Apache-2.0; nuspec/copyright retained in evidence/inventory |

No Guard shell loading/installation entry or extra Roslyn/schema/coverage-collector product dependency. Microsoft.CodeCoverage is Test SDK transitive tooling; no coverage was collected.

Four centrally managed direct packages use exact [version] ranges; locks pin transitive versions/contentHash. Test runners use PrivateAssets=all, test projects are not packaged and product projects never reference tests.

## Actual resolution and identity distinctions

ArchUnit runtime closure has six packages: TngTech.ArchUnitNET, CycleDetection, JetBrains.Annotations, Mono.Cecil, Newtonsoft.Json 13.0.4, System.ValueTuple. W00 System.Collections.Immutable was a candidate, absent from actual locks.

IntegrationTests does not reference ArchUnit DLL; its TestHost separately resolves Newtonsoft.Json 13.0.3, while two other test projects use 13.0.4. Lock/archive both versions separately. Independent test-process differences do not require one version across the solution.

NuGet lock contentHash corresponds to restored .nupkg.metadata contentHash; rawNupkgSha512 hashes full ZIP bytes, corresponding to W00 full-package SHA-512. Record definitions separately. All 19 locks/metadata match, without publisher-signature or vulnerability-audit claims.

Local ArchUnitNET DLL AssemblyVersion `0.13.0.0`, InformationalVersion `1.0.0`. Adapter generates package-version metadata centrally and explicitly returns `0.13.4`, retaining LoadedAssemblyVersion. Never infer NuGet identity from DLL informational version.

## Licensing and provenance

All resolved nuspecs (LF-normalized) are in evidence; inventory records original cache nuspec hashes, authors, copyright, repository URL/commit. Runtime dependencies retain W00 full licenses/NOTICE. New test-tool licenses concern development; W08 must inspect actual publish output for distribution.

xUnit metadata declares Apache-2.0; fixed v2 source license retains MIT exceptions for imported code. xunit.abstractions 2.0.3 only has legacy licenseUrl, no expression/source commit; retain that limitation, not exact-source-binding claims. Full MIT text from Microsoft test packages source `490850ae3fdc1b470e3804ceab4f6a41cf89ae51` is archived.

References: [official restore config/source options](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-restore), [NuGet central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management), [xUnit v2 VSTest guide](https://xunit.net/docs/getting-started/v2/getting-started).

## Offline operation and later work

Root NuGet.Config clears inherited sources. Development checks explicitly restore from local feed into isolated repository cache. No network/implicit fallback/vulnerability feed. NuGetAudit=false means offline, not audit passed. Network restoration/audits require explicit recorded operations.

W02 records schema-validator rationale/license/locks; W05 real ArchUnitNET type-dependency positive/negatives; W08 actual distributed components and self-contained .NET runtime notices.
