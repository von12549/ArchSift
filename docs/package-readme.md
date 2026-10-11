# ArchSift 0.6.0 package

Windows x64 self-contained CLI and loopback workbench. Declared project discovery requires no SDK or Guard installation and makes no persistent PATH/Profile changes. Isolated builds need supported target SDKs/targeting packs and default to offline restore. This package is a candidate until its exact bytes pass the release acceptance gates.

After extracting, run in PowerShell:

```powershell
./archsift.exe --version
./archsift.exe analyze --target D:/your-source --output D:/archsift-reports
./archsift.exe verify --config D:/your-config/archsift.json
./archsift.exe chain verify --config D:/your-config/archsift.json --chain D:/your-rules/chains/architecture.json
./archsift.exe changes --config D:/your-config/archsift.json
./archsift.exe ui --config D:/your-config/archsift.json
./archsift.exe launch --root D:/your-owned-installation
```

Changes requires local Git and compares HEAD with the final worktree by default; explicit --base/--head local commits must be supplied together. It does not fetch, checkout or write to the target. CLI UI forwards stdout/stderr and prints URL/PID/waiting state; a private control pipe ends Web on parent exit. Use Safe shutdown or Ctrl+C; closing the browser alone does not stop it. Never publish the token.

The 0.6.0 launcher presents profiles before loading target JSON and owns the workbench lifetime. New installs use config/default.json, with profiles.json and selection.json under config; Setup protects only its single registered configuration and library. Other JSON files remain external, even if placed in config. Existing config paths and prior version guides remain available. Current tool version and actual chain stages are visible; --max-concurrency 1..4 defaults to serial, shares evidence and keeps report order. No workflow dependency graph or conditional triggers are provided.

The separate self-contained Windows ArchSift.Setup.exe supports reviewed plan/apply installation, explicit adoption, compatible side-by-side upgrade, receipt rollback and journal recovery. It does not change PATH or install an SDK. --notices prints embedded runtime licenses. Unknown schema/migration rejects; ruleset entry IDs, tombstones and chain references are preserved. See docs/setup.md and docs/installation.md. Setup does not analyze/build targets or adopt policy.

JSON is executable policy. Eight templates in templates/rules include direct project/NuGet allowlists. Allowlists intersect and do not override deny rules. Incomplete declarations retain limitations. Legacy verify composes configured files; card/chain verification uses frozen saved library bytes, with independent chain children and a separate summary. Import/export retains exact saved JSON bytes, including BOM; unsaved drafts do not change runs. Missing chain entries produce diagnostics while later entries continue.

JSON reports are authoritative; HTML and SARIF are projections. Conclusive violations default to exit 0; configuration/execution/partial-inconclusive/cancellation use 2/3/4/130. A partial/compliant result is limited coverage, never full source or IFX certification. Rules, state and reports belong outside the target. English generated text is independent of UI locale; user policy text is preserved.

Guidance is in docs/cli.md, docs/changes.md, docs/reports.md, docs/rules.md and docs/build-inputs.md; schemas are in schemas. samples/ci contains opt-in reporting examples, without required checks.

LICENSE is ArchSift's MIT license. licenses retains complete third-party and .NET runtime notices. package-manifest.json records source commit, version, RID and file hashes. Its candidate status describes packaging time; final acceptance must bind the exact ZIP hash. Verify the corresponding SHA256SUMS.txt.

Source and evidence: [von12549/ArchSift](https://github.com/von12549/ArchSift). Linux CLI uses source and .NET 10; this Windows ZIP does not promise Linux Web UI or a Linux self-contained distribution. IFX C/D/E coverage and policy adoption remain outside this release.
