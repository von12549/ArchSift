# ArchSift 0.11.4

ArchSift is a .NET architecture analyzer with a CLI, a local Web Workbench and typed architecture tests. This 0.11.4 candidate adds bounded daily SDK selection, automatic external per-user workspace storage and hash-verified project-rule policies.

The CLI includes matching Web, workspace schemas, project-entry templates and the TestGate implementation. Use matching ArchSift.Contracts, Core, ArchUnit, Testing and Cli packages. Daily SDK compatibility is configured by the consuming project; IFX selects stable SDKs from 10.0.300 inclusive to 10.1.0 exclusive and still requires compatible .NET 10 Core/ASP.NET frameworks.

A configured workspace can generate its daily-test inventory automatically from an explicit NuGet.Config and locked dependencies. Loading that inventory does not run tests. Actual configured restore and architecture execution require an explicit gate operation; dependency audit is a separate result. Offline/exact approval remains available for reproducible qualification.

When state.root is omitted, the generic workspace uses per-user LocalApplicationData/ArchSift/state. Init/Start create the owned directories. Doctor inspects without initializing state or probing a dotnet child. Project workbench.policyFile selects hash-bound project rules; personal drafts remain external. Generated state and downloaded packages belong outside the source checkout.

This is a candidate package document. Final supported environments and acceptance are bound to the exact package bytes in the release evidence. Publication and IFX adoption must not be inferred from a source build.
