namespace ArchSift.Contracts;

public sealed record InProjectRuleCoverage(string RuleKey, string Classification, string[] TestIds,
    int Discovered, int Started, int Finished, string Status, int Matched,
    string[] FindingIds, string[] Limitations);

public sealed record InProjectGateCoverage(bool Complete, string[] RequiredRules, string[] ExecutedRules);
public sealed record InProjectGateDecision(string Decision, string Reason, int ExitCode);
public sealed record InProjectGateMaterials(string State, string IndexPath);

public sealed record InProjectGateResult(int SchemaVersion, string Product, string RunId, string WorktreeId,
    string Execution, InProjectBindingVerdict Binding, string EvidenceGrade,
    InProjectGateCoverage Coverage, InProjectRuleCoverage[] Rules, Finding[] Findings,
    string[] Limitations, InProjectGateDecision Gate, InProjectGateMaterials Materials, string Freshness);

public sealed record InProjectTestEnvelope(int SchemaVersion, string RunId, string ProductVersion,
    string RulesetSha256, string InputIdentitySha256, string ContextIdentitySha256,
    string[] ExpectedRuleIds, string ResultMaterialPath, string ResultMaterialSha256,
    string WorkerIdentitySha256, string ReceiptDirectory);

public sealed record InProjectTestReceipt(int SchemaVersion, string RunId, string RuleKey,
    string TestId, int Started, int Finished, string Status, string ResultMaterialSha256);

public sealed record InProjectRuleObservation(string RuleKey, string Status, int Matched,
    string[] FindingIds, string[] Limitations);
