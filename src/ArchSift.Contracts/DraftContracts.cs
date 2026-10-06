namespace ArchSift.Contracts;

public sealed record RuleDraft(ProjectSnapshot ObservedFacts, Ruleset CandidateRules, string[] UnresolvedDecisions);
