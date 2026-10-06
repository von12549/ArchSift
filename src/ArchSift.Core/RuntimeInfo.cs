using ArchSift.Contracts;

namespace ArchSift.Core;

public static class RuntimeInfo
{
    public static string ProductVersion => ToolIdentity.Version;
}
