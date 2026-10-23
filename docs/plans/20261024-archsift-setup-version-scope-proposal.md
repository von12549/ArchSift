# Proposed Setup version-scope exception for the NuGet workspace change

Date: 2026-10-24. Status: **PROPOSAL ONLY / NOT APPLIED / HUMAN SCOPE DECISION REQUIRED**.

The authorized workspace plan keeps existing Setup compatibility unchanged and introduces a new NuGet-only product version. Actual full development regression found three existing managed-Launcher tests construct installation metadata using the currently compiled product identity. With 0.11.0 these tests are rejected by the unchanged explicit Setup compatibility guard. A numeric stable version already passes the existing metadata format; the remaining boundary is the known Workbench-version allowlist. The new package's portable/initialization path does not require a managed installation.

Automatic approval review rejected the earlier proposal to widen Setup's release-candidate version regex because it conflicted with the NuGet-only plan. That patch was not applied. Setup source/schema remain byte-identical to the baseline. No alternative mechanism has been used to apply that rejected patch.

The concrete proposed exception is to add only stable `0.11.0` to `PackageReader.IsWorkbenchVersion`, while retaining the existing schema-1 compatibility checks, package hash/source checks, old version behavior and rejection of unrelated future versions. No regex/schema change is proposed, no target/host policy changes are proposed, and no new Setup binary/ZIP publication is included.

Proposed source change, pending explicit user scope authorization:

```diff
-    public static bool IsWorkbenchVersion(string version) => version is "0.10.0" or "0.10.1" ||
+    public static bool IsWorkbenchVersion(string version) => version is "0.10.0" or "0.10.1" or "0.11.0" ||
```

Target: `src/ArchSift.Setup/PackageReader.cs`. Add focused tests for 0.11.0's explicit identity and unchanged rejection of unrelated versions; retain all existing assertions. Then rerun the complete product suite and independently verify CI. This changes the Setup compatibility allowlist and therefore requires an explicit scope decision before application, despite being one source line.

Alternative: retain the original Setup boundary, keep the new package NuGet-only, and leave the managed-Launcher regression/complete-CI integration unresolved until a separately reviewed test/distribution boundary is agreed. No failed case may be suppressed or reported as a pass. Continue independent NuGet/package/IFX preparation while the decision is pending.

Observed evidence: `D:/ArchSift-lab/runs/archsift-unified-init-20261024-01/full-development-02`, current package input `development-pack-05`, and successful actual HTTP v3/private-auth/source-mapping/cache/offline-negative consumption at `D:/ArchSift-lab/fixtures/workspace-network-development-02/verification.json`. These are development results, not final package or IFX adoption acceptance.
