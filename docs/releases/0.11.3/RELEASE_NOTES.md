# ArchSift 0.11.3 release preparation

Status: qualification preparation. Build, regression and publication outcomes are recorded separately; this document does not assert public availability or final acceptance.

This version retains the 0.11.2 fixes and adds the IFX installed-host/configured-source workspace repair validated in the isolated 0.11.3-rc.1 candidate. After the consumer restores exact approved packages, IFX initialization can use the selected installed .NET host and explicit NuGet configuration. It retains package/version, policy/case, host and CLI/Web payload integrity checks. Normal workspace initialization returns no daily architecture-test configuration and does not run architecture tests.

Distribution remains four net10.0 libraries (Contracts, Core, ArchUnit and Testing) and the framework-dependent CLI tool with matching Web payload. No runtime downloader, machine environment repair or new installer is added. The architecture gate keeps its separate approved offline-source policy; online host startup does not expand that policy.

The repair has been reconciled with upstream main `68f225172ebd2fafd056587015ec37573652b6b3`, including the hosted publishing helper's version selection and the immutable 0.11.2 publication records. Local source version is 0.11.3. Earlier RC validation remains evidence for those RC inputs, not acceptance of unbuilt final packages. No 0.11.2 repeat waiver is inherited.

See [preparation plan](../../plans/20261030-0113-release-ifx-integration.md). The release driver and qualification checks now support 0.11.3, with scoped synthetic validation recorded separately. The new publishing profile remains closed and has no final identities; the proposed workflow is outside active workflow discovery. The active publishing workflow still selects 0.11.2. Pipeline support does not establish final package qualification or authorize publication.
