using System.Text.Json;
using System.Xml.Linq;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Testing;

public sealed record ApprovedPackage(string Id, string Version, string ContentHash);
public sealed record PackageApproval(int SchemaVersion, string TestingVersion, string CliVersion, ApprovedPackage[] Packages);

/// <summary>Small explicit approval for the test-tool dependency closure, independent of machine cache paths.</summary>
public static class PackageToolApproval
{
    public static void Verify(string root, string projectRelative, string approvalRelative)
    {
        var project = PathSafety.Under(root, projectRelative);
        var approval = JsonSerializer.Deserialize<PackageApproval>(File.ReadAllBytes(PathSafety.Under(root, approvalRelative)), JsonContract.Options)
            ?? throw new InvalidDataException("Package tool approval is missing.");
        if (approval.SchemaVersion != 1 || approval.TestingVersion != ToolIdentity.Version ||
            approval.CliVersion != approval.TestingVersion || approval.Packages is not { Length: > 0 } ||
            approval.Packages.Any(item => string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Version) || string.IsNullOrWhiteSpace(item.ContentHash)) ||
            approval.Packages.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != approval.Packages.Length)
            throw new InvalidDataException("Unsupported package tool approval.");
        var documents = new List<XDocument> { XDocument.Load(project) };
        var central = PathSafety.Under(root, "Directory.Packages.props");
        if (File.Exists(central)) documents.Add(XDocument.Load(central));
        var references = documents.SelectMany(item => item.Descendants()).Where(item =>
            item.Name.LocalName is "PackageReference" or "PackageVersion" &&
            ((string?)item.Attribute("Include"))?.StartsWith("ArchSift.", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        if (references.Length == 0 || references.Any(item =>
            !string.Equals((string?)item.Attribute("Include"), "ArchSift.Testing", StringComparison.Ordinal) ||
            item.AncestorsAndSelf().Any(node => node.Attribute("Condition") is not null || node.Name.LocalName is "Choose" or "When" or "Otherwise" or "Target") ||
            item.Attribute("VersionOverride") is not null))
            throw new InvalidDataException("Unapproved test tool declaration.");
        var versions = references.Select(item => (string?)item.Attribute("Version") ?? item.Element("Version")?.Value)
            .Where(item => item is not null).ToArray();
        if (versions.Length != 1 || versions[0] != "[" + approval.TestingVersion + "]")
            throw new InvalidDataException("Testing package version is not exactly approved.");
        using var locked = JsonDocument.Parse(File.ReadAllBytes(PathSafety.Under(root,
            Path.GetRelativePath(root, Path.Combine(Path.GetDirectoryName(project)!, "packages.lock.json")))));
        var graph = locked.RootElement.GetProperty("dependencies").GetProperty("net10.0");
        var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Visit("ArchSift.Testing");
        if (!closure.SetEquals(approval.Packages.Select(item => item.Id)))
            throw new InvalidDataException("Tool dependency closure differs from approval.");
        using var tools = JsonDocument.Parse(File.ReadAllBytes(PathSafety.Under(root, ".config/dotnet-tools.json")));
        var toolEntries = tools.RootElement.GetProperty("tools").EnumerateObject().ToArray();
        var cliEntries = toolEntries.Where(item => item.Name.Equals("archsift.cli", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (tools.RootElement.GetProperty("version").GetInt32() != 1 || !tools.RootElement.GetProperty("isRoot").GetBoolean() ||
            cliEntries.Length != 1 || cliEntries[0].Name != "archsift.cli" ||
            cliEntries[0].Value.GetProperty("version").GetString() != approval.CliVersion ||
            cliEntries[0].Value.GetProperty("commands").GetArrayLength() != 1 ||
            cliEntries[0].Value.GetProperty("commands")[0].GetString() != "archsift" ||
            toolEntries.Where(item => item.Name != "archsift.cli").Any(item =>
                item.Name.StartsWith("ArchSift.", StringComparison.OrdinalIgnoreCase) ||
                item.Value.GetProperty("commands").EnumerateArray().Any(command =>
                    string.Equals(command.GetString(), "archsift", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("Unapproved local CLI tool manifest.");

        void Visit(string id)
        {
            if (!closure.Add(id)) return;
            var properties = graph.EnumerateObject().Where(item => item.Name.Equals(id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (properties.Length != 1) throw new InvalidDataException("Ambiguous or missing tool dependency.");
            var node = properties[0].Value;
            var approved = approval.Packages.SingleOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (approved is null || node.GetProperty("type").GetString() == "Project" ||
                node.GetProperty("resolved").GetString() != approved.Version ||
                node.GetProperty("contentHash").GetString() != approved.ContentHash)
                throw new InvalidDataException("Tool package identity differs from approval: " + id);
            if (node.TryGetProperty("dependencies", out var children))
                foreach (var child in children.EnumerateObject()) Visit(child.Name);
        }
    }
}
