# Optional CI reporting

`archsift-report.yml` is only a workflow_dispatch example. Copy it into the consumer's `.github/workflows` yourself and supply a reviewed release version, ZIP SHA-256 and configuration path. ArchSift does not automatically change consumer workflows, required checks, branch policies or code-scanning permissions.

Configuration should explicitly include `output.formats: ["json", "html", "sarif"]`; the example overrides the source root/output with checkout/runner temp paths. Declaration-only project analysis does not need an installed SDK. Isolated builds require separately configured target SDKs/packs, an offline feed or explicit network sources; do not enable them by default.

Violations default to exit 0 and do not block. Invalid arguments, execution errors, inconclusive results and cancellation retain 2/3/4/130 respectively. The example does not hide tool failures with `continue-on-error`; it always uploads reports and exit-code.txt, and its check is not required. SARIF is an artifact by default. Upload to GitHub code scanning requires the consumer's separate opt-in and authorization and is not enabled here.

The product's own CI explicitly restores over the network only on an ephemeral GitHub runner, without changing the product's offline default. Actions are pinned to reviewed official v4 commit SHAs. CI completion must be established from actual run evidence; a workflow file's existence does not prove cross-platform success.
