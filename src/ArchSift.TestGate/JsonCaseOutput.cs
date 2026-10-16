using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.TestGate;

public sealed record JsonCaseOutput(string? CaseId, string? Sha256, RuleEvaluation? Evaluation)
{
    public static JsonCaseOutput Parse(string output)
    {
        const string casePrefix = "ARCHSIFT_JSON_CASE:";
        const string evaluationPrefix = "ARCHSIFT_JSON_EVALUATION:";
        string? caseId = null;
        string? sha = null;
        RuleEvaluation? evaluation = null;
        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(casePrefix, StringComparison.Ordinal))
            {
                var body = line[casePrefix.Length..];
                var split = body.LastIndexOf(':');
                if (split <= 0 || split == body.Length - 1)
                    throw new InvalidDataException("Malformed JSON test marker.");
                var nextCaseId = body[..split];
                var nextSha = body[(split + 1)..];
                if (caseId is not null && (caseId != nextCaseId || sha != nextSha))
                    throw new InvalidDataException("Conflicting JSON test markers.");
                caseId = nextCaseId;
                sha = nextSha;
            }
            else if (line.StartsWith(evaluationPrefix, StringComparison.Ordinal))
            {
                if (evaluation is not null) throw new InvalidDataException("Duplicate JSON test evaluation.");
                evaluation = JsonSerializer.Deserialize<RuleEvaluation>(line[evaluationPrefix.Length..], JsonContract.Options)
                    ?? throw new InvalidDataException("Empty JSON test evaluation.");
            }
        }
        return new(caseId, sha, evaluation);
    }
}
