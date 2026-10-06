using System.Reflection;

namespace ArchSift.Contracts;

/// <summary>Shared product identity; rule and report contracts arrive in W02.</summary>
public static class ToolIdentity
{
    public const string Name = "archsift";

    public static string Version =>
        typeof(ToolIdentity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];
}
