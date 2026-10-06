namespace ArchSift.Core;

/// <summary>Engine identity for composition. Analysis operations are added in W05.</summary>
public interface IAssemblyAnalysisEngine
{
    string Name { get; }
    string Version { get; }
}
