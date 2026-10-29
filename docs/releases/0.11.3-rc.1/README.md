# ArchSift 0.11.3-rc.1 local development candidate

This package is for isolated validation of the IFX host/configured workspace repair. It is not a published or qualified release. The matching CLI package contains the framework-dependent Web payload and project entry template; libraries alone do not provide the Launcher.

The consumer must pin `archsift.cli` to `0.11.3-rc.1` in its root local-tool manifest, retain `isRoot: true` and `rollForward: false`, and use matching package/lock/gate identities. Before product startup, explicitly restore dependencies from reviewed sources and verify the candidate receipt and archive hash. Do not replace the current IFX profile or weaken its package approval to load this build.

An eligible installed Windows x64 .NET 10 SDK/Core/ASP.NET environment and PowerShell 7.4+ are prerequisites for the project-entry/UI workflow. No runtime installation, global tool, PATH change, automatic fallback or environment repair is performed. Host/configured IFX uses an explicit existing NuGet.Config and external cache, null exact-runtime/source approvals, and its original IFX gate identity.

Ordinary workspace startup does not restore/build the target or run architecture tests. In configured mode `dailyTestConfig` is null; the existing daily architecture gate still requires separately approved offline preparation. Packaged UI and real IFX acceptance remain pending.

Read the accompanying candidate source/package identity receipt and local validation report. The original 0.11.2 base commit does not describe the uncommitted repair by itself. Published versions and prior IFX state remain unchanged.
