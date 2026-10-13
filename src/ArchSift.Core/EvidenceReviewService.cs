using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class EvidenceReviewService
{
    public static EvidenceMaterialPage Materials(string jobDirectory, string currentTarget, int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ConfigurationException("Material offset/limit is out of bounds.");
        var job = Load(jobDirectory, currentTarget); var review = ExternalBuildEvidence.Read<ExternalBuildReview>(job.ReviewTemplatePath!);
        return new(review.Materials.Skip(offset).Take(limit).ToArray(), offset, limit, review.Materials.Length);
    }
    public static EvidenceMaterialPreview Material(string jobDirectory, string currentTarget, string sha256)
    {
        var job = Load(jobDirectory, currentTarget); var review = ExternalBuildEvidence.Read<ExternalBuildReview>(job.ReviewTemplatePath!);
        var material = review.Materials.FirstOrDefault(m => m.Sha256 == sha256) ?? throw new ConfigurationException("Material is not part of this run.");
        PathSafety.EnsureNoLinks(material.Path);
        if (!PathSafety.IsUnder(material.Path, jobDirectory) || AssemblyArtifacts.FileHash(material.Path) != material.Sha256) throw new ConfigurationException("Material changed or escapes its owned run.");
        if (Path.GetExtension(material.Path).ToLowerInvariant() is not (".json" or ".log" or ".txt" or ".md" or ".cs" or ".xml" or ".props" or ".targets"))
            return new(material.Path, material.Role, material.Sha256, false, null, "Binary material is local review only; no Web export or text-decoding claim.");
        using var stream = File.OpenRead(material.Path); var bytes = new byte[(int)Math.Min(stream.Length, 256 * 1024)]; var count = stream.Read(bytes, 0, bytes.Length);
        return new(material.Path, material.Role, material.Sha256, stream.Length > bytes.Length, System.Text.Encoding.UTF8.GetString(bytes, 0, count), null);
    }
    public static EvidenceReviewScope Scope(string jobDirectory, string currentTarget, CancellationToken token = default)
    {
        var job = Load(jobDirectory, currentTarget);
        if (job.Stage != "review-required" || job.Error is not null || job.Unknowns.Length != 0 || job.FinishedUtc is null ||
            job.Executions.Any(e => e.ExitCode != 0 || !e.OwnedTreeStopped || !e.HostStateEqual) || job.CompletedProjects != job.Preview.ProjectCount || job.EvaluatedProjects != job.Preview.ProjectCount)
            throw new ConfigurationException("Only completed, unchanged build/evaluation materials can enter review.");
        var capturePath = job.CapturePath ?? throw new ConfigurationException("Capture material is missing.");
        var templatePath = job.ReviewTemplatePath ?? throw new ConfigurationException("Review template is missing.");
        var capture = ExternalBuildEvidence.Read<EvidenceCapture>(capturePath); var review = ExternalBuildEvidence.Read<ExternalBuildReview>(templatePath);
        var facts = new List<string> { AssemblyArtifacts.FileHash(capturePath), AssemblyArtifacts.FileHash(templatePath), job.Preview.PreviewSha256 };
        InputCapture.VerifyUnchanged(currentTarget, capture.InputIdentity, token);
        if (InputCapture.Capture(review.BuildSnapshotRoot!, token).Sha256 != capture.InputIdentity.Sha256) throw new SourceChangedException();
        foreach (var file in capture.ExternalInputs)
            if (!File.Exists(file.Path) || AssemblyArtifacts.FileHash(file.Path, token) != file.Sha256) throw new ConfigurationException("Review input changed/missing: " + file.Path);
        foreach (var material in review.Materials.OrderBy(m => m.Path, StringComparer.Ordinal))
        {
            PathSafety.EnsureNoLinks(material.Path);
            if (!File.Exists(material.Path) || new FileInfo(material.Path).Length != material.Length || AssemblyArtifacts.FileHash(material.Path, token) != material.Sha256)
                throw new ConfigurationException("Review material changed/missing: " + material.Path);
            facts.Add(material.Path + "\0" + material.Sha256);
        }
        return new(Path.GetFullPath(jobDirectory), job.Stage, ContentHash.Text(string.Join("\n", facts)), facts[0], facts[1],
            job.Preview.ProjectCount, review.Materials.Length, review.Materials.GroupBy(m => m.Role).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            review.Unknowns, ["Inspect actual build commands/exits and source/snapshot causal relationship.", "Review actual compiler inputs and generated input readers.",
                "Review SDK/package/import/task risks and uncaptured external behavior.", "State limits; approval does not certify runtime, deployment or architecture compliance."]);
    }

    public static EvidenceReviewResult Approve(EvidenceReviewRequest request, string currentTarget, CancellationToken token = default, Action<EvidenceReviewResult>? progress = null)
    {
        token.ThrowIfCancellationRequested();
        if (!request.Approve || string.IsNullOrWhiteSpace(request.Reviewer) || request.ReviewerKind is not ("human" or "delegated-agent") ||
            string.IsNullOrWhiteSpace(request.AuthorizationReference) || string.IsNullOrWhiteSpace(request.Reason) ||
            string.IsNullOrWhiteSpace(request.ImportTaskAssessment) || request.GeneratedInputRelationships is not { Length: > 0 } || request.Boundaries is not { Length: > 0 } ||
            request.GeneratedInputRelationships.Any(string.IsNullOrWhiteSpace) || request.Boundaries.Any(string.IsNullOrWhiteSpace))
            throw new ConfigurationException("Actual reviewer authority, causal reasons, generated relationships, import/task assessment and boundaries are required.");
        var scope = Scope(request.JobDirectory, currentTarget, token);
        if (scope.ScopeSha256 != request.ReviewScopeSha256) throw new ConfigurationException("Review scope changed; inspect actual current materials again.");
        var job = Load(request.JobDirectory, currentTarget); var root = job.Preview.Storage.RunDirectory;
        var host = EvidenceHostState.Capture(); EvidenceArtifactStore.WriteNew(Path.Combine(root, "review-host-baseline-" + Guid.NewGuid().ToString("N") + ".json"), host);
        var reviewPath = Path.Combine(root, "review-v2.json"); var manifestPath = job.Preview.Storage.ManifestPath;
        EvidenceValidationResult? validated = null;
        void Stage(string stage)
        {
            job = job with { Stage = stage }; EvidenceArtifactStore.SaveJob(job);
            progress?.Invoke(new(stage, root, File.Exists(reviewPath) ? reviewPath : null, File.Exists(manifestPath) ? manifestPath : null,
                null, null, request.Boundaries, job.Error));
        }
        try
        {
            token.ThrowIfCancellationRequested();
            var template = ExternalBuildEvidence.Read<ExternalBuildReview>(job.ReviewTemplatePath!);
            // Machine input gaps cannot be removed by approval. The remaining template unknown is this mandatory causal review.
            if (!template.Unknowns.SequenceEqual(new[] { "Actual material review and build causality approval are required." }, StringComparer.Ordinal))
                throw new ConfigurationException("Unresolved machine inputs require recapture/build; approval cannot erase unknowns.");
            var notes = Path.Combine(root, "review-approved-notes.json");
            EvidenceArtifactStore.WriteNew(notes, new { kind = "actual-build-review-reasons", scopeSha256 = scope.ScopeSha256, request.Reviewer,
                request.ReviewerKind, request.AuthorizationReference, request.Reason, request.GeneratedInputRelationships, request.ImportTaskAssessment, request.Boundaries,
                actualExecutionCount = job.Executions.Length, declaredLimits = "External-reviewed portable type evidence; no runtime/deployment/full architecture certification." });
            var review = template with { Reviewer = request.Reviewer, Approved = true, InputsComplete = true, Unknowns = [],
                Materials = [.. template.Materials.Where(m => m.Role != "review-notes"), EvidenceArtifactStore.Material(notes, "review-notes")] };
            EvidenceArtifactStore.WriteNew(reviewPath, review, true); Stage("review-approved");
            host.Verify(); token.ThrowIfCancellationRequested(); Stage("sealing");
            ExternalBuildEvidence.Seal(job.Configuration, job.CapturePath!, reviewPath, manifestPath, token);
            host.Verify(); token.ThrowIfCancellationRequested(); Stage("validating");
            var config = job.Configuration with { Build = job.Configuration.Build with { Mode = "existing", AssemblyManifest = manifestPath, AssemblyPaths = [] } };
            validated = EvidenceWorkbench.Validate(config, token);
            EvidenceArtifactStore.WriteNew(Path.Combine(root, "independent-validation.json"), validated);
            if (!validated.SourceBound) throw new ConfigurationException("Independent validation rejected the sealed evidence.");
            host.Verify(); Stage("validated");
            return new("validated", root, reviewPath, manifestPath, validated, null, request.Boundaries, null);
        }
        catch (OperationCanceledException) { Stage("cancelled"); return new("cancelled", root, File.Exists(reviewPath) ? reviewPath : null, File.Exists(manifestPath) ? manifestPath : null, null, null, request.Boundaries, "Review/seal/validation was cancelled; material is preserved."); }
        catch (Exception error)
        {
            job = job with { Error = error.Message }; Stage(error is HostDriftException ? "host-drift" : "failed");
            return new(job.Stage, root, File.Exists(reviewPath) ? reviewPath : null, File.Exists(manifestPath) ? manifestPath : null, null, null, request.Boundaries, error.Message);
        }
        finally
        {
            var final = EvidenceHostState.Capture(); EvidenceArtifactStore.WriteNew(Path.Combine(root, "review-host-final-" + Guid.NewGuid().ToString("N") + ".json"), new { equal = final.Identity == host.Identity, hashes = final });
            if (final.Identity != host.Identity) { job = job with { Stage = "host-drift", Error = "Host drift; stop and preserve evidence." }; EvidenceArtifactStore.SaveJob(job); throw new HostDriftException(); }
        }
    }

    public static EvidenceReviewResult ExportConfiguration(string jobDirectory, string currentTarget)
    {
        var job = Load(jobDirectory, currentTarget);
        if (job.Stage != "validated") throw new ConfigurationException("Only independently validated evidence can be exported into a configuration copy.");
        var root = job.Preview.Storage.RunDirectory; var manifest = job.Preview.Storage.ManifestPath;
        var config = job.Configuration with { Build = job.Configuration.Build with { Mode = "existing", AssemblyManifest = manifest, AssemblyPaths = [] } };
        var validation = EvidenceWorkbench.Validate(config);
        if (!validation.SourceBound) throw new ConfigurationException("Evidence became stale before configuration export.");
        var destination = Path.Combine(root, "rules-copy");
        var mapping = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (config.RulesDirectory is { } library && Directory.Exists(library))
        {
            foreach (var source in EvidenceJobService.Files([library]).Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
            {
                var target = PathSafety.Under(destination, Path.GetRelativePath(library, source)); CopyNew(source, target); mapping[source] = target;
            }
        }
        foreach (var ruleset in config.Rulesets)
            if (!mapping.ContainsKey(ruleset))
            {
                var target = Path.Combine(destination, ContentHash.Text(ruleset)[..16] + "-" + Path.GetFileName(ruleset)); CopyNew(ruleset, target); mapping[ruleset] = target;
            }
        config = config with { RulesDirectory = destination, Rulesets = config.Rulesets.Select(p => mapping[p]).ToArray(), Output = config.Output with { Directory = Path.Combine(root, "reports") } };
        var path = Path.Combine(root, "config-bound.json"); EvidenceArtifactStore.WriteNew(path, config);
        EvidenceArtifactStore.WriteNew(Path.Combine(root, "configuration-copy-index.json"), new { kind = "configuration-and-library-copy", path, manifestPath = manifest, copiedRules = mapping,
            originalConfigurationModified = false, originalPolicyAdopted = false });
        return new("configured", root, Path.Combine(root, "review-v2.json"), manifest, validation, path, ["Open this saved configuration copy in a new UI session; the current session/library remains unchanged."], null);
    }
    private static void CopyNew(string source, string target)
    {
        PathSafety.EnsureNoLinks(source); PathSafety.EnsureNoLinks(target);
        var hash = AssemblyArtifacts.FileHash(source); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target, false);
        if (AssemblyArtifacts.FileHash(target) != hash || AssemblyArtifacts.FileHash(source) != hash) throw new SourceChangedException();
    }
    public static EvidenceJobRecord Load(string jobDirectory, string currentTarget)
    {
        jobDirectory = Path.GetFullPath(jobDirectory); PathSafety.EnsureDisjoint(currentTarget, jobDirectory); PathSafety.EnsureNoLinks(jobDirectory);
        var path = Path.Combine(jobDirectory, "job.json");
        if (new FileInfo(path).Length > ProjectContextEvidence.MaximumJsonBytes) throw new ConfigurationException("Evidence job record exceeds 16 MiB.");
        var bytes = File.ReadAllBytes(path); using var doc = JsonDocument.Parse(bytes); SchemaValidation.ValidateDocument(doc.RootElement, JsonSerializer.SerializeToElement(new { type = "object" }));
        var job = doc.RootElement.Deserialize<EvidenceJobRecord>(JsonContract.Options) ?? throw new ConfigurationException("Evidence job record is empty.");
        if (job.SchemaVersion != 1 || job.Kind != "evidence-job" || !string.Equals(job.Configuration.Target.Root, currentTarget, PathSafety.Comparison) ||
            !string.Equals(Path.GetFullPath(job.Preview.Storage.RunDirectory), jobDirectory, PathSafety.Comparison)) throw new ConfigurationException("Evidence job/project identity differs from the current target.");
        foreach (var artifact in new[] { job.CapturePath, job.ReviewTemplatePath, job.Preview.Storage.ManifestPath }.Where(p => p is not null))
            if (!PathSafety.IsUnder(artifact!, jobDirectory)) throw new ConfigurationException("Evidence artifact escapes its owned run.");
        using var owner = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(jobDirectory, "owner.json")));
        if (owner.RootElement.GetProperty("runId").GetString() != job.Id || !string.Equals(owner.RootElement.GetProperty("targetRoot").GetString(), currentTarget, PathSafety.Comparison))
            throw new ConfigurationException("Evidence run ownership record differs.");
        return job;
    }
}
