# ArchSift 0.11.2

This release preparation retains the 0.11.1 validated interrupted-marker recovery correction and fixes product-owned SDK child settings to use the effective literal `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true`. Startup, test-gate and UI child processes keep settings isolated from the parent and persisted host environment.

The preceding unpublished 0.11.2 candidate completed the explicitly accepted Windows scope: package consumers, connected MTP, vulnerability audits, cold/locked disconnected consumers, rejection and recovery cases, Launcher and independent Workbench analysis/report viewing, human interaction and calibrated network-event review. Full disconnected MTP is not verified or claimed. Ubuntu disconnected acceptance is deferred. The final submission package identities and any required supplemental acceptance are recorded separately; previous candidate answers do not automatically accept rebuilt packages.

Distribution consists of four net10.0 libraries and the ArchSift.Cli local tool with its matching framework-dependent Web payload and project template. There is no new installer or live IFX adoption. Public availability must be checked against the eventual publication record; preparation is not publication.

Publication completed for the exact frozen originals; see [public package identities and verification](PUBLICATION.md) and `publication.json`. The earlier candidate and preparation statements retain their historical scope.
