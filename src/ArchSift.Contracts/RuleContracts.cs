using System.Text.Json;

namespace ArchSift.Contracts;

public sealed record Selector
{
    public required string Kind { get; init; }
    public required string Match { get; init; }
    public required string Value { get; init; }
    public bool AllowEmpty { get; init; }
}

public sealed record Rule
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required bool Enabled { get; init; }
    public required Selector Scope { get; init; }
    public required JsonElement Parameters { get; init; }
    public required string Severity { get; init; }
    public required string Reason { get; init; }
}

public sealed record RuleException
{
    public required string Id { get; init; }
    public required string RuleId { get; init; }
    public required Selector Scope { get; init; }
    public required string Reason { get; init; }
}

public sealed record Ruleset
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required Rule[] Rules { get; init; }
    public required RuleException[] Exceptions { get; init; }
}

public sealed record RulesetIdentity(string Id, string Version, string Sha256, string Path);
