# ArchSift 0.11.2 SDK offline correction candidate

Unpublished candidate. Public publication remains unauthorized. Original 0.11.0 and 0.11.1 packages, source identities and acceptance receipts remain immutable.

SDK 10.0.303 parses DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE as a Boolean. The previous value `1` did not disable background workload advertising refresh. Product SDK child launchers, the project bootstrap and current build/verification scripts now use the literal `true`. SafeProcess reapplies that setting after caller overrides, together with its isolated CLI home and global-tools PATH protection. Test-gate and UI child launch paths also pin the setting. The parent and persisted host environment remain unchanged.

This version retains the 0.11.1 interrupted-marker recovery correction. It does not expand Setup's historical ZIP allowlist or authorize a public release, installer, IFX policy change or host repair. Hosted regressions and distinct package provenance must pass before candidate use. Exact spare Windows internal-workflow/network qualification and independent human UI acceptance remain required; Ubuntu offline remains deferred.
