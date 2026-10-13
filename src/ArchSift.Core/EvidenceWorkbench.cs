using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class EvidenceWorkbench
{
    public static EvidenceStoragePlan Plan(RunConfiguration config, EvidenceStorageRequest request, string[] hostProtectedRoots)
    {
        void Segment(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, @"^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$", RegexOptions.CultureInvariant) || value.EndsWith('.') ||
                Regex.IsMatch(value, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw new ConfigurationException("Evidence identity must be a safe, nonreserved path segment.");
        }
        Segment(request.ProjectId); Segment(request.RunId);
        if (string.IsNullOrWhiteSpace(request.EvidenceRoot) && string.IsNullOrWhiteSpace(request.DataRoot))
            throw new ConfigurationException("Select an explicit data root or custom evidence root.");
        if (request.ProtectedRoots is null || request.DataRoot is not null && !Path.IsPathFullyQualified(request.DataRoot) ||
            request.EvidenceRoot is not null && !Path.IsPathFullyQualified(request.EvidenceRoot))
            throw new ConfigurationException("Evidence roots must be explicitly resolved absolute paths.");
        var root = Path.GetFullPath(request.EvidenceRoot ?? Path.Combine(request.DataRoot!, "evidence"));
        PathSafety.EnsureDisjoint(config.Target.Root, root);
        var protectedRoots = hostProtectedRoots.Concat(request.ProtectedRoots).Concat(new[] { config.Output.Directory })
            .Concat(config.RulesDirectory is null ? [] : new[] { config.RulesDirectory })
            .Concat(request.DataRoot is null ? [] : new[] { "versions", "config", "rules", "reports", "downloads", "operations" }.Select(p => Path.Combine(request.DataRoot, p)));
        foreach (var path in protectedRoots) PathSafety.EnsureDisjoint(path, root);
        var run = PathSafety.Under(root, request.ProjectId + "/" + request.RunId);
        if (Directory.Exists(run) || File.Exists(run)) throw new ConfigurationException("Evidence run already exists; create a new run identity.");
        return new(root, request.ProjectId, request.RunId, run, Path.Combine(run, "assembly-manifest-v3.json"));
    }

    public static EvidenceDetails Details(RunConfiguration config)
    {
        var path = config.Build.AssemblyManifest ?? throw new ConfigurationException("Select an existing assembly manifest to inspect.");
        PathSafety.EnsureDisjoint(config.Target.Root, path); PathSafety.EnsureNoLinks(path);
        if (new FileInfo(path).Length > ProjectContextEvidence.MaximumJsonBytes) throw new ConfigurationException("Manifest exceeds 16 MiB.");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > ProjectContextEvidence.MaximumJsonBytes) throw new ConfigurationException("Manifest exceeds 16 MiB.");
        using var document = JsonDocument.Parse(bytes);
        var version = document.RootElement.GetProperty("schemaVersion").GetInt32();
        if (version is not (1 or 2 or 3)) throw new ConfigurationException("Unsupported manifest version.");
        SchemaValidation.Validate(document.RootElement, version == 3 ? "assembly-manifest-v3" : version == 2 ? "assembly-manifest-v2" : "assembly-manifest");
        var manifest = document.RootElement.Deserialize<AssemblyManifest>(JsonContract.Options)!;
        ExternalBuildReview? review = null; EvidenceCapture? capture = null;
        if (manifest.ReviewedBuild is { } link)
        {
            PathSafety.EnsureDisjoint(config.Target.Root, link.CapturePath); PathSafety.EnsureDisjoint(config.Target.Root, link.ReviewPath);
            capture = ExternalBuildEvidence.Read<EvidenceCapture>(link.CapturePath);
            review = ExternalBuildEvidence.Read<ExternalBuildReview>(link.ReviewPath);
        }
        return new(Path.GetFullPath(path), ContentHash.Bytes(bytes), version, "not-revalidated", manifest.Sdk, manifest.TargetFramework,
            manifest.Configuration, manifest.Generation, manifest.InputIdentity.Sha256, manifest.BuildInputIdentity.Sha256,
            manifest.Assemblies.Where(a => a.SourceProject != "@dependency").ToArray(), manifest.ProjectContexts ?? [],
            manifest.ReviewedBuild?.CapturePath, manifest.ReviewedBuild?.ReviewPath, review?.Reviewer, review?.Approved,
            review?.Unknowns ?? [], (review?.Materials ?? []).GroupBy(m => m.Role).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal), capture?.ExternalInputs.Length ?? 0);
    }

    public static EvidenceValidationResult Validate(RunConfiguration config, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (config.Build.AssemblyManifest is null) throw new ConfigurationException("Independent validation requires an existing manifest.");
        var started = DateTimeOffset.UtcNow.ToString("O"); var timer = Stopwatch.StartNew();
        var snapshot = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework, token);
        var inputs = AssemblyArtifacts.Existing(config, snapshot, token);
        try
        {
            InputCapture.VerifyUnchanged(config.Target.Root, snapshot.InputIdentity, token);
            foreach (var proof in inputs.EvidenceHashes)
                if (!File.Exists(proof.Key) || AssemblyArtifacts.FileHash(proof.Key, token) != proof.Value) throw new SourceChangedException();
            foreach (var file in inputs.Sha256)
                if (!File.Exists(file.Key) || AssemblyArtifacts.FileHash(file.Key, token) != file.Value) throw new SourceChangedException();
        }
        catch (SourceChangedException error)
        { inputs = inputs with { SourceBound = false, CoverageComplete = false, Limitations = [.. inputs.Limitations, error.Message] }; }
        token.ThrowIfCancellationRequested(); timer.Stop();
        var owners = !inputs.SourceBound ? 0 : inputs.Contexts.Length > 0 ? inputs.Contexts.Select(c => c.SourceProject).Distinct(StringComparer.Ordinal).Count() : snapshot.Projects.Length;
        var identity = inputs.EvidenceHashes.Count == 0 ? null : ContentHash.Text(string.Join("\n", inputs.EvidenceHashes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "\0" + p.Value)));
        return new(inputs.SourceBound ? "validated" : "stale", started, timer.ElapsedMilliseconds, inputs.SourceBound, owners,
            inputs.Contexts.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count(), inputs.CoverageComplete, inputs.EvidenceOrigin, identity, inputs.Limitations);
    }
}
