namespace ArchSift.Core;

public static class RuleTemplates
{
    public static string[] Names { get; } = ["project-reference", "project-reference-allowlist", "graph-integrity", "target-framework", "nuget-denylist", "nuget-allowlist", "type-dependency", "naming"];
    public static string Json(string name)
    {
        if (!Names.Contains(name, StringComparer.Ordinal)) throw new ConfigurationException("Unknown rule template.");
        var assembly = typeof(RuleTemplates).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Templates." + name + ".json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
