# W05 assembly and isolated-build acceptance

Date: 2026-10-06 (Australia/Sydney).

Conclusion: W05 COMPLETE. Integrated real ArchUnitNET type dependencies and type/assembly naming, separate-process Workers, assembly manifests, existing/isolated inputs, offline/explicit-source restore, independent package caches and input/hash/context validation.

Final run `529a15adc1c646cab192010b2379a7a8`: SDK 10.0.303/net10.0/Debug; eight-project offline locked restore/build passed with zero warnings/errors.65 unit +16 integration +2 architecture =83/83 passed. Repeated real synthetic fixtures also compiled through standard SDK children. User/Machine environment, Process PATH and Profile hashes matched throughout and at completion.

Actual positive/negative cases compiled external snapshots from current synthetic source and used ArchUnitNET's Cecil file-loading API to check namespace/type dependencies, including generic-field targets. Workers never CLR-load target assemblies or call business methods. A target module initializer did not write its trap marker. Original source gained no bin/obj and its hash was unchanged. Compiled type/assembly naming positive/negative results also passed.

Manifests verify current source identity, snapshot-build input identity, project/assembly mappings, SDK/TFM/Configuration, actual PE assembly identity/context and SHA-256. Workers recheck bytes, version/token closure and missing dependencies, then recheck bytes on completion. Unbound DLLs retain assemblies-only evidence but rules are inconclusive. Timestamp freshness never substitutes for provenance binding. Every Worker is new, without an old-result cache. Local NuGet caches may be reused; existing artifacts require full hash/identity verification.

Negative cases covered wrong hashes, stale manifests after source changes, missing assembly closure, zero-match selectors, TFM/configuration mismatch, real project compilation errors, unsafe custom output paths and cancellation. Initially unstructured wrong-hash handling and an error fixture outside project compile scope were fixed, with failed runs retained. Only integration-test Core/ArchUnit project references were added. After lock refresh, Newtonsoft.Json13.0.3 no longer appeared in the actual union; no new third-party runtime package was introduced.

Standard-SDK isolated mode copies a conservative in-root input snapshot and writes an isolation sentinel into external output. Build identity records snapshot, SDK/TFM/configuration and final analysis assembly. It is not a complete MSBuild file-access trace. Full evaluation remained deferred. Custom SDKs, tasks/imports, output paths, conditions/expressions and package build tasks/analyzers/generators were outside first-stage safely copied isolated scope, accurately rejected or unbound to current source. No automatic network or original-checkout edits. Razor/JSON/lock inputs enter the source snapshot. These boundaries do not mean ordinary SDK type analysis is unsupported: existing mode can inspect supplied DLLs with explicit binding strength.

Archived [checks](w05/checks.json), [host-final](w05/host-final.json), [build](w05/build.stdout.log) and [test](w05/test.stdout.log). The [official ArchUnitNET file-loading API](https://github.com/TNG/ArchUnitNET/blob/1ab5943d761d48f86b42f45ef047130dc9aff1c6/ArchUnitNET/Loader/ArchLoader.cs) and cache-disable option were API references; neither its source nor Guard implementation was copied.

W06 would combine project/assembly per-rule states, JSON/HTML reports, CLI analyze/verify/draft, exit codes and cancelled/partial reports. No real IFX run had occurred.
