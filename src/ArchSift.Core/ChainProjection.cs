using ArchSift.Contracts;

namespace ArchSift.Core;

public static class ChainProjection
{
    public static ChainAggregate Aggregate(ChainSummary summary)
    {
        var planned = summary.Snapshot.Entries.Select(e => e.EntryId).ToArray();
        if (planned.Distinct(StringComparer.Ordinal).Count() != planned.Length ||
            summary.Entries.Select(e => e.EntryId).Distinct(StringComparer.Ordinal).Count() != summary.Entries.Length ||
            summary.Entries.Any(e => !planned.Contains(e.EntryId, StringComparer.Ordinal)))
            throw new ConfigurationException("Chain aggregate has duplicate or unplanned entry identities.");
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
            { ["pass"] = 0, ["violation"] = 0, ["inconclusive"] = 0, ["error"] = 0, ["not-applicable"] = 0 };
        foreach (var entry in summary.Entries)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in entry.RuleResults)
            {
                if (!ids.Add(result.RuleId) || !counts.ContainsKey(result.Status))
                    throw new ConfigurationException("Chain aggregate has duplicate rules or unknown rule status.");
                counts[result.Status]++;
            }
        }
        bool Reported(ChainChild entry) => entry.Diagnostic is null &&
            (entry.ReportDirectory is not null || entry.RuleResults.Length > 0 || entry.Findings.Length > 0);
        int WithState(string state) => summary.Entries.Count(e => e.Execution == state);
        var bound = summary.Entries.Count(e => Reported(e) && e.Coverage.SourceBound);
        var completeBinding = planned.Length > 0 && summary.Entries.Length == planned.Length && bound == planned.Length;
        // An identical aggregate evidence identity proves these are counts of the same contexts.
        // Legacy/mixed evidence without this identity cannot supply a unique-context total.
        var contexts = completeBinding && summary.Entries.All(e => e.AssemblyContextCount is not null && e.EvidenceSha256 is not null) &&
            summary.Entries.Select(e => (e.EvidenceSha256, e.AssemblyContextCount)).Distinct().Count() == 1
            ? summary.Entries[0].AssemblyContextCount : null;
        return new(planned.Length, summary.Entries.Count(Reported),
            summary.Entries.Count(e => e.Execution is "completed" or "partial" or "failed" or "cancelled" or "skipped"),
            WithState("completed"), WithState("partial"), WithState("failed"), WithState("cancelled"), WithState("skipped"),
            planned.Length - summary.Entries.Length, summary.Entries.Count(e => e.Diagnostic is not null), counts.Values.Sum(),
            new(counts["pass"], counts["violation"], counts["inconclusive"], counts["error"], counts["not-applicable"]),
            summary.Entries.Sum(e => e.Findings.Length), summary.ProjectCount, completeBinding, bound, contexts,
            summary.Execution, summary.Compliance, summary.ExitCode);
    }

    public static string Text(ChainAggregate value) =>
        $"Rules {value.RecordedRules}: pass={value.StatusCounts.Pass}; violation={value.StatusCounts.Violation}; inconclusive={value.StatusCounts.Inconclusive}; error={value.StatusCounts.Error}; not-applicable={value.StatusCounts.NotApplicable}; findings={value.FindingCount}. " +
        $"Entries ended={value.EndedEntries}/{value.PlannedEntries}; reported={value.ReportedEntries}; completed={value.CompletedEntries}; partial={value.PartialEntries}; failed={value.FailedEntries}; cancelled={value.CancelledEntries}; skipped={value.SkippedEntries}; missing={value.MissingEntries}; diagnostics={value.DiagnosticCount}. " +
        $"Unique projects={value.ProjectCount}; contexts={value.AssemblyContextCount?.ToString() ?? "unknown"}; {value.Execution}/{value.Compliance}/exit {value.ExitCode}.";
}
