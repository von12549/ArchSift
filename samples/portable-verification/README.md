# Independent portable verification example

`PortableTests.cs` is an ArchSift-owned synthetic example with two original Theory rows and one actual JSON type-dependency case. It uses its own `Synthetic.AllowedSource` assembly, retains JSON case/evaluation output and calls `ArchitectureTestAdapter.AssertPassed`. It contains no IFX source or policy and does not prove any real consumer's architecture compliance.

The source-tree helper `scripts/Prepare-PortableVerificationFixture.ps1` creates a fresh external project, pinned SDK/runner inputs, rules, policy and reviewed synthetic logical identities, then restores and runs the full gate. It requires an already built current-source CLI, SDK 10.0.303 and a complete explicit offline feed. Its generated project uses a development ProjectReference to ArchSift.Testing; this is **source verification**, not packaged-consumer or empty-cache package qualification.

```powershell
pwsh -NoProfile -File scripts/Prepare-PortableVerificationFixture.ps1 -FixtureRoot 'D:/ArchSift-lab/fixtures/new-portable-example' -LocalFeed 'D:/approved-feed' -PackageCache 'D:/ArchSift-lab/packages/new-example-cache'
```

Use a fresh unused external fixture root. Inspect retained logs and `fixture.json`; expected synthetic coverage is three actual passes, zero failures/skips and audit `not-run`. A failure is retained and must not be relabeled as a pass. The helper runs dedicated tests, not a target application entry point, and compares host hashes. It performs no formal policy adoption.

For a packaged consumer, use the exact package/tool declarations in the [generic templates](../workbench/README.md), adapt the test adapter to the actual reviewed production assemblies, preserve original tests and follow [project integration](../../docs/project-integration.md). A release must separately qualify those packages with cold caches and moved paths.
