# CLI and configuration

```text
archsift analyze --config config.json
archsift verify --config config.json
archsift chain verify --config config.json --chain chain.json --target-kind fixture
archsift changes --config config.json
archsift changes --config config.json --base HEAD~1 --head HEAD
archsift ui --config config.json
archsift rules validate --file architecture.json
archsift rules render --file architecture.json --output architecture.md
archsift rules draft --config config.json --output D:/archsift-output/drafts
```

`analyze` discovers declared projects without policy (`compliance=null`). Legacy `verify` composes all configured ruleset paths into one report, retaining global rule-ID uniqueness and cross-file checks. `chain verify` runs each referenced saved library ruleset independently in order and writes child reports plus a separate summary. It requires a managed library in `rulesDirectory`, or defaults to `<output.directory>/rules`. `--target-kind` is `real` by default or `fixture`; it labels context and does not change analysis semantics. A child partial/error/missing-ruleset result does not stop later entries. Invalid global input, input drift or cancellation has separate stopping behavior.

Ordinary analyze/verify/changes/draft options `--target --entry --rules --output --tfm --configuration` override configuration. Repeated `--rules` replaces the configured path collection; other flags cannot repeat. Chain verification accepts only its documented config/chain/target-kind flags. Root, ruleset, output, manifest, cache/feed and rules-directory paths resolve relative to the config file. `target.entry` is relative to target root; ordinary CLI `--entry` resolves from invocation cwd and must remain under that root.

Effective ordinary-run context is printed to stderr. stdout is authoritative JSON for analysis/comparison/chain commands and confirmation/path text for rule-management commands. No command fetches remote Git refs or imports external rules automatically.

Each run uses a unique external output directory. Normal files are `report.json/html/sarif`; comparisons use `comparison.json/html/sarif`. Chain output contains `snapshot.json`, one entry directory per saved reference, independent normal reports or typed diagnostics, and `chain-summary.json/html/sarif`. Card and chain verification always produce all three formats. Existing `output.formats` controls legacy runs.

Output and target must be disjoint and must not cross links/reparse points. Markdown rendering creates a new file and cannot overwrite JSON. Existing mode does not restore/build automatically. Isolated builds are explicit, default Debug/offline, and need supported target prerequisites; see [build inputs](build-inputs.md). They are not a full sandbox.

The workbench uses saved cards for Verify/Verify Chain. Import selects a local JSON file; Export JSON preserves saved bytes including UTF-8 BOM. External configured paths remain read-only in the UI until explicitly imported. Unsaved editor changes never affect a run.

| Exit | Meaning |
| --- | --- |
| 0 | Complete and conclusive; violations may exist |
| 2 | Configuration rejection; global chain input rejected before children |
| 3 | Global execution/report failure; ordinary build/Worker error |
| 4 | Partial/inconclusive result; continued chain child errors are recorded here |
| 130 | Cancelled, with current audit evidence retained |

Analysis CLI Ctrl+C cancels the current run/process tree. UI job cancellation is separate from server shutdown; cancelled reports remain downloadable and labelled. Web binds loopback with origin/token protection. Closing the page does not stop the service. 0.5.0 concurrently forwards Web stdout/stderr, bounds startup to 30 seconds, requests graceful child shutdown on Ctrl+C, waits up to 10 seconds before owned-tree termination, and ends Web when its parent pipe closes. Package/terminal acceptance is tracked in the 0.5.0 plan. The older [0.4.0 issue and temporary Web entry](installation-0.4.md#known-040-cli-ui-launch-issue) remain version-specific. [Setup commands](setup.md) use a separate executable.
