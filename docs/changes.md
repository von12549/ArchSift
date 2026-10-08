# Changes comparison

`archsift changes --config config.json` compares local HEAD with the final on-disk worktree, including staged, unstaged and selected untracked inputs. It performs full analysis of both sides.

Explicit comparison: `archsift changes --config config.json --base HEAD~1 --head HEAD`. Both refs must resolve to local commits. No fetch, checkout, upstream guessing, target/index changes or hooks are performed. TargetRoot may be a repository subdirectory. UI and CLI use the same Core service.

Git is required for changes; a missing Git executable returns execution error 3. The Windows self-contained package does not install Git or repair PATH.

Snapshots and reports use unique external run directories. Historical bytes come directly from Git blobs without export-ignore/export-subst/checkout-filter transformations. Links, submodules, platform collisions and out-of-root paths are rejected conservatively. Limits: 100000 files, 128 MiB per file and 512 MiB per tree. Worktree source and default HEAD are rechecked for drift.

Root-internal policies come from their respective revisions; external policies are frozen once for both sides and their originals checked afterwards. Editing/deleting/disabling rules or changing exceptions is a policy change, never automatically a code fix. Excluded tracked/untracked files are listed; qualifying ignored inputs still follow InputCapture. Explicit resources survive normal generated-directory exclusions.

Stable finding IDs are compared only when rule/exception fingerprints, build/engine context and conclusive rule statuses match. Results are added, existing, resolved or unclassified. Rename display requires unique equal content hashes; no source mapping is fabricated. A missing or failed baseline preserves target evidence without claiming new findings.

comparison.json is authoritative; HTML escapes user text and includes both full reports. Status is completed, inconclusive or cancelled. Conclusive violations still exit 0; configuration errors exit 2, execution/baseline errors 3, partial/inconclusive comparison 4, cancellation 130. Completed comparable subsets can be retained within an incomplete overall run.

Cancellation may generate clearly labelled current JSON/HTML/SARIF audit downloads, with no complete comparison conclusion. Those files are valid evidence, not stale success. An explicit two-commit cancellation is a separate regression scenario from default HEAD/worktree cancellation.

Existing unbound DLLs do not prove either revision's source state. Isolated builds remain explicit and offline by default. Comparison schemaVersion 1 stays separate from analysis and chain-summary schemas.
