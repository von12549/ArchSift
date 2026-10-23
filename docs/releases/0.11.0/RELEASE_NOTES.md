# ArchSift 0.11.0 candidate: project workspace initialization

Status: implementation candidate, not publicly published or independently accepted. The current candidate version is 0.11.0; existing 0.10.1 artifacts and acceptance remain immutable.

The NuGet CLI adds workspace init, doctor and start commands. A project adopts one `.archsift/` entry/configuration/README, with optional ignored host settings. Standard root tool manifests, NuGet configuration, SDK constraints and existing policy/test files keep their original locations. The package carries the matching Web payload, entry template and workspace schemas.

Configured NuGet sources and standard caches support network/private/offline use. SDK discovery reads the host without persistent environment changes. Generic use requires a stable SDK >=10.0.0, project global.json compatibility and compatible .NET 10 Core/ASP.NET frameworks; actual minimum-version qualification remains a release gate. IFX keeps its independent exact host/offline/policy approval.

Initialization creates owned external state, defaulting to C:/ArchSift-lab with user/project partitioning. Unknown state, conflicting root bindings, unapproved payloads and unsafe overrides fail without repair. Target builds and application execution are not startup side effects. Editable state/history remain separate from NuGet caches.

This candidate's new distribution scope is the five matching NuGet packages with embedded Web. Existing 0.10.1 Windows ZIP/Setup distribution and migration compatibility are unchanged; no new Setup capability or public registry availability is implied. Build candidate packages with scripts/Build-WorkspacePackages.ps1 from clean committed source and an explicit approved host/offline build feed. Public delivery follows qualification and independent human acceptance.
