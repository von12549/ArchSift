# Portable NuGet Workbench

Version: 0.10.1 candidate. Exact stable qualification and final user acceptance are pending. Use the five matching product packages and the reviewed offline-feed manifest. The CLI tool contains complete Web under tools/net10.0/any/web; no separate Web installation or source checkout is required.

NuGet installation requires SDK 10.0.303. Web requires compatible Core and ASP.NET 10.0.13 shared runtimes; SDK identity alone does not establish the selected runtime patch. CLI-only analysis keeps its Core-only requirement. The separate Windows x64 ZIP/Setup includes its runtime. Target builds still need the target's approved SDKs, packs and explicit offline dependencies.

## Local tool and direct UI

Create a dedicated working directory outside target/source roots. Use an explicit NuGet.Config with only approved local sources, then a local tool manifest. During candidate testing use the exact candidate version; after stable acceptance use the matching stable version and its approvals.

~~~powershell
dotnet new tool-manifest
dotnet tool install ArchSift.Cli --version 0.10.1-rc.2 --configfile D:/approved/NuGet.Config --add-source D:/approved/offline-feed
dotnet tool restore --configfile D:/approved/NuGet.Config --add-source D:/approved/offline-feed
dotnet tool run archsift -- ui --config D:/configuration/archsift.json
~~~

An explicit tool-path installation is also supported with dotnet tool install --tool-path D:/tools/archsift-0101 and the same version/source arguments. Do not globally install or repair PATH. Validate package SHA-256, release manifest and approvals before consuming packages. Runtime inventory hashes detect corruption but are not an independent authenticity anchor.

## Portable Launcher

Prepare a valid existing configuration whose target, library, output and package/cache paths are canonical, disjoint from portable state and the tool/source payload. Its state directory's parent must already exist. Initialize explicitly:

~~~powershell
dotnet tool run archsift -- launch --root D:/workbench-state/project-a --config D:/configuration/archsift.json
dotnet tool run archsift -- launch --root D:/workbench-state/project-a
~~~

The root contains workbench-state.json schema 1, catalog and selection under config, and owned locks. The marker identifies portable state; it is separate from Setup install.json. Configuration/library files are external and receive no Setup backup protection. The UI shows portable mode and the running tool version. The invoking tool supplies the matching verified Web; no executable cache path is stored as ownership.

Unknown nonempty roots, conflicting markers, invalid/future schemas, unsafe paths and busy state are rejected or explained without replacement. A bounded interrupted bootstrap can resume only when its recorded config and partial bytes match exactly; unexpected files remain for review. To register another configuration, use the Launcher profile flow after explicit initialization. The optional final --no-browser argument exposes the loopback address for controlled headless operation.

Managed launch --root <managed-install-root> retains Setup selection, backups, leases and receipts. A mismatched Launcher/selected version requires the selected installation entry. It does not become portable automatically.

## JSON inventory and shutdown

Architecture tests shows one initially collapsed disclosure per JSON ruleset, with filename, canonical path and enabled-case count. Expand all / Collapse all affects JSON inventory only. Copy/edit remains available while closed. All original IDs remain available; folding never disables or skips cases. State is held in the current Web session, follows canonical config/rule paths and clears when changing configuration.

Use Tab and native Enter/Space behavior; visible focus and English/Chinese labels remain available. Save or export changes and finish/cancel jobs before Safe shutdown. Closing the browser does not stop the service. The owned child exits on parent loss. Do not persist or share session tokens.

## Upgrade and rollback

Keep the complete old nupkg/tool manifest/locks/approved feed and approvals, plus external configuration/library/catalog backups. Restore the old combination in a separate isolated tool directory; leave new portable metadata retained and unused by old code. The 0.10.0 NuGet CLI has no standalone Web; retain its accepted Windows installation as the old UI fallback.

Managed rollback separately requires a successful Setup receipt and unchanged post-state. Back up outer scripts, catalog/selection and unprotected external configurations independently. Rehearse old -> new -> old -> new on copied installations with clean shutdown and hash checks. Preserve evidence on drift. Release acceptance does not upgrade existing IFX entrances or adopt deferred policies.
