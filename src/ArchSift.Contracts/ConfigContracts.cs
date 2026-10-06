using System.Text.Json.Serialization;

namespace ArchSift.Contracts;

public sealed record TargetSettings
{
    public required string Root { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Entry { get; init; }
}

public sealed record BuildSettings
{
    public string Mode { get; init; } = "existing";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetFramework { get; init; }
    public string Configuration { get; init; } = "Debug";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssemblyManifest { get; init; }
    public string[] AssemblyPaths { get; init; } = [];
    public bool AllowNetwork { get; init; }
    public string[] Sources { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalFeed { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CacheDirectory { get; init; }
}

public sealed record OutputSettings
{
    public required string Directory { get; init; }
    public string[] Formats { get; init; } = ["json", "html"];
}

public sealed record RunConfiguration
{
    public required int SchemaVersion { get; init; }
    public required TargetSettings Target { get; init; }
    public string[] Rulesets { get; init; } = [];
    public BuildSettings Build { get; init; } = new();
    public required OutputSettings Output { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RulesDirectory { get; init; }
}
