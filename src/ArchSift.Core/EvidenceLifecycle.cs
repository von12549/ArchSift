using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class EvidenceLifecycle
{
    public static EvidenceCleanupPlan PlanCleanup(EvidenceArchiveRequest request, RunConfiguration current, string[] protectedRoots, bool activeInSession)
    {
        var archive = PlanArchive(request, current, protectedRoots, activeInSession, true);
        var job = EvidenceReviewService.Load(request.JobDirectory, current.Target.Root); var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Own(string? path) { if (path is not null && PathSafety.IsUnder(path, archive.JobDirectory)) owned.Add(Path.GetRelativePath(archive.JobDirectory, path).Replace('\\', '/')); }
        foreach (var name in new[] { "owner.json", "job.json", "source-files.json", "host-baseline.json", "host-final.json", "NuGet.Config", "compiler-input-audit.json", "review-required.md", "review-approved-notes.json", "independent-validation.json", "config-bound.json", "configuration-copy-index.json" }) Own(Path.Combine(archive.JobDirectory, name));
        Own(job.CapturePath); Own(job.ReviewTemplatePath); Own(job.Preview.Storage.ManifestPath); Own(Path.Combine(archive.JobDirectory, "review-v2.json"));
        if (job.ReviewTemplatePath is { } template)
        {
            var review = ExternalBuildEvidence.Read<ExternalBuildReview>(template); foreach (var material in review.Materials) Own(material.Path);
            var capture = ExternalBuildEvidence.Read<EvidenceCapture>(job.CapturePath!); foreach (var external in capture.ExternalInputs) Own(external.Path);
            foreach (var source in capture.InputIdentity.Files) Own(PathSafety.Under(review.BuildSnapshotRoot!, source.Path));
        }
        foreach (var execution in job.Executions) { Own(execution.StdoutPath); Own(execution.StderrPath); }
        var selected = archive.Files.Where(f => owned.Contains(f.RelativePath)).ToArray();
        var unknown = archive.Files.Where(f => !owned.Contains(f.RelativePath)).Select(f => f.RelativePath).ToArray();
        var blockers = archive.Blockers.Concat(archive.References.Select(r => "Still referenced: " + r)).ToArray();
        var plan = new EvidenceCleanupPlan(archive.JobDirectory, archive.ArchiveDirectory, "", selected, unknown, archive.ReferenceRoots, archive.References, blockers);
        return plan with { PlanSha256 = ContentHash.Text(JsonSerializer.Serialize(plan, JsonContract.Options)) };
    }

    public static string CleanupArchived(EvidenceCleanupPlan plan, RunConfiguration current, string[] protectedRoots, bool activeInSession)
    {
        if (activeInSession || plan.Blockers.Length != 0 || plan.References.Length != 0 || plan.PlanSha256 != ContentHash.Text(JsonSerializer.Serialize(plan with { PlanSha256 = "" }, JsonContract.Options)))
            throw new ConfigurationException("Cleanup preview is blocked, active or changed.");
        var actual = PlanCleanup(new(plan.JobDirectory, plan.ArchiveDirectory, plan.ReferenceRoots), current, protectedRoots, false);
        if (actual.Blockers.Length != 0 || actual.References.Length != 0 ||
            !actual.SelectedOwnedFiles.SequenceEqual(plan.SelectedOwnedFiles) ||
            !actual.RetainedUnknownFiles.SequenceEqual(plan.RetainedUnknownFiles) ||
            !actual.ReferenceRoots.SequenceEqual(plan.ReferenceRoots))
            throw new ConfigurationException("Cleanup references, ownership or bytes changed.");
        foreach (var file in plan.SelectedOwnedFiles)
        {
            var archived = PathSafety.Under(plan.ArchiveDirectory, file.RelativePath); var original = PathSafety.Under(plan.JobDirectory, file.RelativePath);
            if (!File.Exists(archived) || AssemblyArtifacts.FileHash(archived) != file.Sha256 || AssemblyArtifacts.FileHash(original) != file.Sha256)
                throw new ConfigurationException("Recoverable archive or original bytes differ; cleanup refused.");
        }
        var intent = Path.Combine(plan.ArchiveDirectory, "cleanup-intent-" + Guid.NewGuid().ToString("N") + ".json");
        EvidenceArtifactStore.WriteNew(intent, new { kind = "owned-evidence-files-cleanup-intent", plan.PlanSha256,
            files = plan.SelectedOwnedFiles, retainedUnknownFiles = plan.RetainedUnknownFiles,
            note = "Archive bytes were verified before deleting exact listed originals. If no completion receipt follows, inspect both locations; deletion may be partial." });
        // Exactly listed files only. Unknown files and all directories remain; no recursive root delete.
        foreach (var file in plan.SelectedOwnedFiles) File.Delete(PathSafety.Under(plan.JobDirectory, file.RelativePath));
        var receipt = Path.Combine(plan.ArchiveDirectory, "cleanup-receipt-" + Guid.NewGuid().ToString("N") + ".json");
        EvidenceArtifactStore.WriteNew(receipt, new { kind = "owned-evidence-files-cleanup", plan.PlanSha256, selectedFileCount = plan.SelectedOwnedFiles.Length,
            retainedUnknownFiles = plan.RetainedUnknownFiles, recursiveRootDeletion = false, recoverableArchive = true }); return receipt;
    }
    public static EvidenceRecovery Recover(string jobDirectory, string currentTarget, bool activeInThisSession)
    {
        var job = EvidenceReviewService.Load(jobDirectory, currentTarget); var differences = new List<string>();
        var inputs = InputCapture.Capture(currentTarget);
        if (inputs.Sha256 != job.Preview.SourceIdentity) differences.Add("Current source identity differs from the recorded execution preview.");
        if (job.CapturePath is { } path)
        {
            var capture = ExternalBuildEvidence.Read<EvidenceCapture>(path);
            foreach (var file in capture.ExternalInputs)
                if (!File.Exists(file.Path) || AssemblyArtifacts.FileHash(file.Path) != file.Sha256) differences.Add("External input changed/missing: " + file.Path);
        }
        var active = job.Stage is "planned" or "snapshotting" or "snapshot-ready" or "inspecting-sdk" or "restoring" or "capturing" or "captured" or "building" or "evaluating" or "sealing" or "validating";
        return new(jobDirectory, job.Stage, active ? activeInThisSession ? "active-session" : "terminal-state-unknown" : "recorded-terminal",
            differences.Count > 0 ? "stale" : "not-revalidated", differences.ToArray(), false, false, EvidenceJobService.View(job));
    }

    public static EvidenceValidationResult Revalidate(string jobDirectory, string currentTarget, CancellationToken token = default)
    {
        var job = EvidenceReviewService.Load(jobDirectory, currentTarget);
        if (job.Stage is not ("validated" or "configured")) throw new ConfigurationException("Only sealed, independently validated runs can be reused.");
        var config = job.Configuration with { Build = job.Configuration.Build with { Mode = "existing", AssemblyManifest = job.Preview.Storage.ManifestPath, AssemblyPaths = [] } };
        var result = EvidenceWorkbench.Validate(config, token);
        EvidenceArtifactStore.WriteNew(Path.Combine(jobDirectory, "revalidation-" + Guid.NewGuid().ToString("N") + ".json"), result);
        return result;
    }

    public static EvidenceArchivePlan PlanArchive(EvidenceArchiveRequest request, RunConfiguration current, string[] protectedRoots, bool activeInSession, bool existingArchive = false)
    {
        var job = EvidenceReviewService.Load(request.JobDirectory, current.Target.Root);
        var root = Path.GetFullPath(request.JobDirectory); var archive = Path.GetFullPath(request.ArchiveDirectory);
        PathSafety.EnsureDisjoint(root, archive); PathSafety.EnsureDisjoint(current.Target.Root, archive);
        foreach (var protectedRoot in protectedRoots) PathSafety.EnsureDisjoint(protectedRoot, archive);
        if (!existingArchive && (Directory.Exists(archive) || File.Exists(archive))) throw new ConfigurationException("Archive destination must be create-new.");
        if (request.ReferenceRoots is null) throw new ConfigurationException("Reference scan roots are required.");
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var referenceRoots = (existingArchive ? request.ReferenceRoots.Append(current.Output.Directory) : request.ReferenceRoots.AsEnumerable())
            .Select(Path.GetFullPath).Distinct(pathComparer).OrderBy(p => p, pathComparer).ToArray();
        var blockers = new List<string>(); var references = new List<string>();
        if (existingArchive && !Directory.Exists(archive)) blockers.Add("The recoverable archive is unavailable.");
        if (activeInSession || job.Stage is "building" or "restoring" or "capturing" or "evaluating" or "sealing" or "validating") blockers.Add("Active or unknown terminal state; no cleanup is permitted.");
        if (current.Build.AssemblyManifest is { } manifest && PathSafety.IsUnder(manifest, root)) references.Add("Current configuration references this evidence run.");
        foreach (var referenceRoot in referenceRoots)
        {
            if (!Directory.Exists(referenceRoot)) { blockers.Add("Reference scan root is unavailable."); continue; }
            PathSafety.EnsureNoLinks(referenceRoot);
            foreach (var file in EvidenceJobService.Files([referenceRoot]).Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                if (PathSafety.IsUnder(file, root)) continue;
                if (new FileInfo(file).Length > ProjectContextEvidence.MaximumJsonBytes) { blockers.Add("Reference file exceeds bounded scan scope: " + file); continue; }
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllBytes(file));
                    if (ContainsReference(document.RootElement, root)) references.Add(file);
                }
                catch (JsonException) { blockers.Add("Reference file cannot be interpreted: " + file); }
            }
        }
        var files = EvidenceJobService.Files([root]).Select(file => new EvidenceArchiveFile(Path.GetRelativePath(root, file).Replace('\\', '/'), AssemblyArtifacts.FileHash(file), new FileInfo(file).Length)).OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToArray();
        if (files.Any(f => f.RelativePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase)))
            blockers.Add("Raw binary build traces remain local confidential evidence; generic archive/export is refused until a separate reviewed private-access procedure is supplied.");
        // Archive preserves all bytes and the source, including unknown added files. It never claims ownership to delete them.
        var plan = new EvidenceArchivePlan(root, archive, "", files, referenceRoots, references.Distinct().ToArray(), blockers.Distinct().ToArray(), true);
        return plan with { PlanSha256 = ContentHash.Text(JsonSerializer.Serialize(plan, JsonContract.Options)) };
    }
    public static EvidenceArchiveResult Archive(EvidenceArchivePlan expected, RunConfiguration current, string[] protectedRoots, bool activeInSession)
    {
        var actual = PlanArchive(new(expected.JobDirectory, expected.ArchiveDirectory, expected.ReferenceRoots), current, protectedRoots, activeInSession);
        if (!actual.Files.SequenceEqual(expected.Files) || !actual.ReferenceRoots.SequenceEqual(expected.ReferenceRoots) ||
            !actual.References.SequenceEqual(expected.References) || actual.JobDirectory != expected.JobDirectory ||
            actual.ArchiveDirectory != expected.ArchiveDirectory || expected.Blockers.Length > 0 || actual.Blockers.Length > 0 || !expected.SourceRetained)
            throw new ConfigurationException("Archive plan changed or has unresolved blockers.");
        if (expected.PlanSha256 != ContentHash.Text(JsonSerializer.Serialize(expected with { PlanSha256 = "" }, JsonContract.Options))) throw new ConfigurationException("Archive preview identity is invalid.");
        Directory.CreateDirectory(expected.ArchiveDirectory);
        foreach (var file in expected.Files)
        {
            var from = PathSafety.Under(expected.JobDirectory, file.RelativePath); var to = PathSafety.Under(expected.ArchiveDirectory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(from, to, false);
            if (AssemblyArtifacts.FileHash(from) != file.Sha256 || AssemblyArtifacts.FileHash(to) != file.Sha256) throw new SourceChangedException();
        }
        var receipt = Path.Combine(expected.ArchiveDirectory, "archive-receipt-" + Guid.NewGuid().ToString("N") + ".json");
        EvidenceArtifactStore.WriteNew(receipt, new { kind = "evidence-archive-receipt", expected.PlanSha256, expected.JobDirectory, expected.ArchiveDirectory,
            originalRetained = true, directlyPortable = false, fileCount = expected.Files.Length,
            limits = "Sealed absolute references retain their original paths; this is a recoverable byte archive, not a rebound manifest. No deletion performed." });
        return new("archived-copy", expected.ArchiveDirectory, expected.Files.Length, true, false, receipt);
    }
    private static bool ContainsReference(JsonElement value, string root)
    {
        if (value.ValueKind == JsonValueKind.String && value.GetString() is { } text && Path.IsPathFullyQualified(text))
        { try { return PathSafety.IsUnder(text, root); } catch (ArgumentException) { return false; } }
        if (value.ValueKind == JsonValueKind.Object) return value.EnumerateObject().Any(p => ContainsReference(p.Value, root));
        if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().Any(v => ContainsReference(v, root));
        return false;
    }
}
