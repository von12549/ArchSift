using System.Reflection;
using ArchSift.Core;
using ArchUnitNET.Loader;

namespace ArchSift.ArchUnit;

/// <summary>Registers the pinned engine identity; no conformance checks run in W01.</summary>
public sealed class ArchUnitEngine : IAssemblyAnalysisEngine
{
    public string Name => "archunitnet";
    public string Version => typeof(ArchUnitEngine).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "ArchUnitNetPackageVersion").Value!;

    // The upstream DLL assembly version is independent of its NuGet package version.
    public string LoadedAssemblyVersion => typeof(ArchLoader).Assembly.GetName().Version!.ToString();
}
