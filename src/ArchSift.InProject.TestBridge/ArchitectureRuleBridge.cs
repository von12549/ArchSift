using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.InProject.TestBridge;

public static class ArchitectureRuleBridge
{
    public const string EnvelopeVariable = "ARCHSIFT_INPROJECT_TEST_ENVELOPE";

    public static IEnumerable<object[]> Cases()
    {
        var (envelope, _) = ReadEnvelope();
        if (envelope.ExpectedRuleIds.Length == 0 ||
            envelope.ExpectedRuleIds.Distinct(StringComparer.Ordinal).Count() != envelope.ExpectedRuleIds.Length)
            throw new InvalidDataException("Architecture test envelope has no unique rule cases.");
        var cases = envelope.ExpectedRuleIds.Order(StringComparer.Ordinal)
            .Select(rule => InProjectTestCaseIdentity.ForRule(rule)).ToArray();
        if (cases.Distinct(StringComparer.Ordinal).Count() != cases.Length)
            throw new InvalidDataException("Architecture test case identities collide.");
        return cases.Select(id => new object[] { id }).ToArray();
    }

    public static InProjectRuleObservation Execute(string caseId)
    {
        var (envelope, directory) = ReadEnvelope();
        var rules = envelope.ExpectedRuleIds.Where(rule =>
            InProjectTestCaseIdentity.ForRule(rule) == caseId).ToArray();
        if (rules.Length != 1)
            throw new InvalidDataException("Unexpected or ambiguous architecture test case.");
        var ruleKey = rules[0];
        var material = ChildFile(directory, envelope.ResultMaterialPath);
        var bytes = File.ReadAllBytes(material);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (sha != envelope.ResultMaterialSha256)
            throw new InvalidDataException("Worker result material changed before xUnit execution.");
        var observations = JsonSerializer.Deserialize<InProjectRuleObservation[]>(bytes, JsonContract.Options)
            ?? throw new InvalidDataException("Worker rule result material is empty.");
        var matches = observations.Where(item => item.RuleKey == ruleKey).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Worker rule result is missing or duplicated.");
        var receiptDirectory = ChildDirectory(directory, envelope.ReceiptDirectory);
        Directory.CreateDirectory(receiptDirectory);
        var id = "ArchSift.InProject.ArchitectureTests.ArchitectureRules.Evaluate(" + caseId + ")";
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ruleKey))).ToLowerInvariant() + ".json";
        var receipt = new InProjectTestReceipt(1, envelope.RunId, ruleKey, id, 1, 1,
            matches[0].Status, sha);
        using (var stream = new FileStream(Path.Combine(receiptDirectory, name), FileMode.CreateNew,
                   FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(stream, receipt, JsonContract.Options);
        return matches[0];
    }

    private static (InProjectTestEnvelope Envelope, string Directory) ReadEnvelope()
    {
        var path = Environment.GetEnvironmentVariable(EnvelopeVariable);
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException("Architecture test envelope is unavailable.");
        var full = Path.GetFullPath(path);
        var bytes = File.ReadAllBytes(full);
        if (bytes.Length > 4 * 1024 * 1024)
            throw new InvalidDataException("Architecture test envelope exceeds 4 MiB.");
        var envelope = JsonSerializer.Deserialize<InProjectTestEnvelope>(bytes, JsonContract.Options)
            ?? throw new InvalidDataException("Architecture test envelope is empty.");
        if (envelope.SchemaVersion != BridgeIdentity.ProtocolVersion ||
            envelope.RunId.Length == 0 || envelope.ExpectedRuleIds is null)
            throw new InvalidDataException("Unsupported architecture test envelope.");
        return (envelope, Path.GetDirectoryName(full)!);
    }

    private static string ChildFile(string directory, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name))
            throw new InvalidDataException("Architecture test material path escapes report directory.");
        var path = Path.Combine(directory, name);
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Architecture test material is missing or linked.");
        return path;
    }

    private static string ChildDirectory(string directory, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name))
            throw new InvalidDataException("Architecture test receipt path escapes report directory.");
        var path = Path.Combine(directory, name);
        if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Architecture test receipt directory is linked.");
        return path;
    }
}
