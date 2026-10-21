using System.Text.Json;

namespace ArchSift.TestGate;

public sealed record AuditCompletion(int? ProjectCount, int? AuditedCount, int? SkippedCount)
{
    public string? Enabled { get; init; }
    public string? Mode { get; init; }
    public string? Level { get; init; }
}
public sealed record GateAudit(string Mode, string Status, AuditCompletion Completion, string[] RestoreArguments);

/// <summary>NuGet completion information is evidence of coverage, never inferred from exit code zero.</summary>
public static class NuGetAudit
{
    public static string[] RestoreArguments(TestGateConfig config, string project, string nugetConfig) =>
        new[] { "restore", project, "--configfile", nugetConfig, "--source", Path.GetFullPath(config.LocalFeed),
            "--packages", Path.GetFullPath(config.PackageCache), "--disable-parallel", "--disable-build-servers",
            "-m:1", "-v:minimal" }
        .Concat(config.AuditMode == "offline-architecture" ? ["-p:NuGetAudit=false"] : Array.Empty<string>())
        .Concat(config.ToolRoot is null ? [] : new[] { "-p:ArchSiftToolRoot=" + Path.GetFullPath(config.ToolRoot) })
        .Concat(config.SchemaVersion == 2 ? new[] { "--locked-mode" } : [])
        .Concat(["-getProperty:RestoreProjectCount,RestoreProjectsAuditedCount,RestoreSkippedCount,NuGetAudit,NuGetAuditMode,NuGetAuditLevel"])
        .ToArray();

    public static AuditCompletion ReadCompletion(string output)
    {
        // MSBuild may print ordinary restore lines before its structured property object.
        for (var index = output.IndexOf('{'); index >= 0; index = output.IndexOf('{', index + 1))
        {
            try
            {
                using var document = JsonDocument.Parse(output[index..]);
                if (!document.RootElement.TryGetProperty("Properties", out var properties)) continue;
                return new(Count("RestoreProjectCount"), Count("RestoreProjectsAuditedCount"), Count("RestoreSkippedCount"))
                { Enabled = Value("NuGetAudit"), Mode = Value("NuGetAuditMode"), Level = Value("NuGetAuditLevel") };
                int? Count(string name) => properties.TryGetProperty(name, out var value) &&
                    int.TryParse(value.ToString(), out var count) && count >= 0 ? count : null;
                string? Value(string name) => properties.TryGetProperty(name, out var value) &&
                    value.ToString() is { Length: > 0 } text ? text : null;
            }
            catch (JsonException) { }
        }
        return new(null, null, null);
    }

    public static bool CoverageComplete(AuditCompletion completion, int expectedProjects) => expectedProjects > 0 &&
        completion.ProjectCount == expectedProjects && completion.AuditedCount == expectedProjects && completion.SkippedCount == 0;
}
