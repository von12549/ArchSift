using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArchSift.Contracts;

namespace ArchSift.Core;

/// <summary>Hashes and verifies externally reviewed facts; never invokes the target build or application.</summary>
public static class ExternalBuildEvidence
{
    public static EvidenceCapture Capture(RunConfiguration config, string[] externalInputs, string[]? environmentInputs = null, CancellationToken token = default)
    {
        if (externalInputs.Any(string.IsNullOrWhiteSpace) || (environmentInputs ?? []).Any(p => string.IsNullOrWhiteSpace(p) || p.Contains('=') || p.Contains('\0')))
            throw new ConfigurationException("Evidence input paths/environment names must be nonblank and valid.");
        var snapshot = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework, token);
        var tfm = config.Build.TargetFramework ?? throw new ConfigurationException("Evidence capture requires an explicit TFM.");
        var properties = snapshot.InputIdentity.Files.Where(f => Path.GetExtension(f.Path) is ".csproj" or ".props" or ".targets")
            .SelectMany(f => Regex.Matches(File.ReadAllText(PathSafety.Under(config.Target.Root, f.Path)), @"\$\(([A-Za-z_][A-Za-z0-9_]*)(?=[).])",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
        var files = externalInputs.Select(Path.GetFullPath).Distinct(PathComparer).Order(StringComparer.Ordinal).Select(p =>
        {
            if (PathSafety.IsUnder(p, config.Target.Root)) throw new ConfigurationException("External inputs must be outside TargetRoot.");
            return FileFact(p, "build", token);
        }).ToArray();
        var buildFiles = snapshot.InputIdentity.Files.Select(f => f with { Kind = "build" }).ToArray();
        InputCapture.VerifyUnchanged(config.Target.Root, snapshot.InputIdentity, token);
        return new(1, config.Target.Root, config.Target.Entry, tfm, config.Build.Configuration, snapshot.InputIdentity,
            new(AssemblyArtifacts.FileIdentity(buildFiles), buildFiles), files,
            properties.Concat(environmentInputs ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)
                .ToDictionary(p => p, EnvironmentHash, StringComparer.Ordinal), properties);
    }

    public static ExternalBuildReview Template(EvidenceCapture capture, string captureHash) => new()
    {
        CaptureSha256 = captureHash, Reviewer = "", Sdk = "", SdkToolPath = "",
        TargetFramework = capture.TargetFramework, Configuration = capture.Configuration,
        Projects = capture.InputIdentity.Files.Where(f => f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(f => new ReviewedProject(f.Path, "", capture.TargetFramework,
                capture.PropertyNames.ToDictionary(p => p, _ => "REVIEW ACTUAL EVALUATED VALUE", StringComparer.Ordinal))).ToArray()
    };

    public static ExternalBuildReview ProjectTemplate(EvidenceCapture capture, string captureHash) => Template(capture, captureHash) with
    {
        SchemaVersion = 2, BuildSnapshotRoot = "REVIEW ACTUAL BUILD SNAPSHOT ROOT",
        ProjectContexts = capture.InputIdentity.Files.Where(f => f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(f => new ReviewedAssemblyContext("project-" + ContentHash.Text(f.Path)[..16], f.Path,
                "REVIEW ACTUAL ASSEMBLY PATH", "REVIEW ACTUAL EVALUATION PATH", [])).ToArray()
    };

    public static void Seal(RunConfiguration config, string capturePath, string reviewPath, string output, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        capturePath = Path.GetFullPath(capturePath); reviewPath = Path.GetFullPath(reviewPath); output = Path.GetFullPath(output);
        PathSafety.EnsureDisjoint(config.Target.Root, output);
        var capture = Read<EvidenceCapture>(capturePath);
        var review = Read<ExternalBuildReview>(reviewPath);
        if (review.SchemaVersion == 2) { ProjectContextEvidence.Seal(config, capture, review, capturePath, reviewPath, output, token); return; }
        var snapshot = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework, token);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var limits = ValidateReview(config, snapshot, capture, review, capturePath, reviewPath, hashes, token);
        if (limits.Count > 0) throw new ConfigurationException(string.Join("\n", limits));
        var directory = Path.GetDirectoryName(output)!;
        var entries = review.Assemblies.Select(a =>
        {
            token.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(a.Path);
            var relative = Path.GetRelativePath(directory, path);
            PathSafety.Under(directory, relative);
            return new AssemblyEntry(AssemblyArtifacts.Inspect(path).Name, a.SourceProject, relative.Replace('\\', '/'), AssemblyArtifacts.FileHash(path, token));
        }).ToArray();
        var manifest = new AssemblyManifest
        {
            SchemaVersion = 2, InputIdentity = capture.InputIdentity, BuildInputIdentity = capture.BuildInputIdentity,
            Sdk = review.Sdk, TargetFramework = review.TargetFramework, Configuration = review.Configuration,
            Generation = "external", Assemblies = entries,
            ReviewedBuild = new(capturePath, AssemblyArtifacts.FileHash(capturePath, token), reviewPath, AssemblyArtifacts.FileHash(reviewPath, token))
        };
        // Validate all mapping, PE metadata and closure before publishing any successful seal.
        var temporary = output + ".check-" + Guid.NewGuid().ToString("N");
        try
        {
            WriteNew(temporary, manifest);
            var verified = AssemblyArtifacts.Existing(config with { Build = config.Build with { AssemblyManifest = temporary } }, snapshot, token);
            if (!verified.SourceBound) throw new ConfigurationException(string.Join("\n", verified.Limitations));
            token.ThrowIfCancellationRequested();
            File.Move(temporary, output, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static List<string> ValidateReview(RunConfiguration config, ProjectSnapshot snapshot, EvidenceCapture capture,
        ExternalBuildReview review, string capturePath, string reviewPath, Dictionary<string, string> hashes, CancellationToken token = default)
    {
        var limits = new List<string>();
        void Fail(string message) => limits.Add("External evidence: " + message);
        if (capture.SchemaVersion != 1 || review.SchemaVersion is not (1 or 2) || review.Origin != "external-reviewed") Fail("Unsupported capture/review identity.");
        if (!review.Approved || !review.InputsComplete || review.Unknowns.Length != 0 || string.IsNullOrWhiteSpace(review.Reviewer)) Fail("Review is incomplete or has unresolved inputs.");
        if (review.ExitCode != 0 || review.Command.Length == 0 || review.Command.Any(string.IsNullOrWhiteSpace)) Fail("Successful build command/log proof is missing.");
        if (AssemblyArtifacts.FileHash(capturePath, token) != review.CaptureSha256) Fail("Review does not bind capture bytes.");
        if (!Path.GetFullPath(capture.TargetRoot).Equals(Path.GetFullPath(config.Target.Root), PathSafety.Comparison) ||
            capture.Entry != config.Target.Entry || capture.InputIdentity.Sha256 != snapshot.InputIdentity.Sha256 ||
            !SameFiles(capture.InputIdentity.Files, snapshot.InputIdentity.Files)) Fail("Capture does not bind current source/entry.");
        if (capture.BuildInputIdentity.Sha256 != AssemblyArtifacts.FileIdentity(capture.BuildInputIdentity.Files) ||
            !SameFiles(capture.BuildInputIdentity.Files, snapshot.InputIdentity.Files)) Fail("Build input capture is incomplete.");
        if (capture.TargetFramework != config.Build.TargetFramework || capture.Configuration != config.Build.Configuration ||
            review.TargetFramework != capture.TargetFramework || review.Configuration != capture.Configuration ||
            string.IsNullOrWhiteSpace(review.Sdk)) Fail("SDK/TFM/Configuration proof is inconsistent.");
        var currentProperties = Capture(config, [], token: token).PropertyNames;
        if (!currentProperties.SequenceEqual(capture.PropertyNames, StringComparer.Ordinal) ||
            currentProperties.Any(p => !capture.EnvironmentHashes.ContainsKey(p))) Fail("Conditional environment/property capture is incomplete.");
        foreach (var property in currentProperties)
            if (!capture.EnvironmentHashes.TryGetValue(property, out var expected) || expected != EnvironmentHash(property)) Fail("Condition input changed: " + property);
        foreach (var environment in capture.EnvironmentHashes)
            if (environment.Value != EnvironmentHash(environment.Key)) Fail("Reviewed environment input changed: " + environment.Key);
        foreach (var role in new[] { "build-log", "evaluation-trace", "review-notes", "generated-input" })
            if (!review.Materials.Any(m => m.Role == role && m.Length > 0)) Fail("Missing review material: " + role);
        if (!snapshot.Projects.Select(p => p.Id).Order(StringComparer.Ordinal).SequenceEqual(review.Projects.Select(p => p.SourceProject).Order(StringComparer.Ordinal)))
            Fail("Evaluated project coverage is incomplete/duplicate.");
        if (Path.IsPathFullyQualified(review.SdkToolPath))
        {
            var versionFile = Path.Combine(Path.GetDirectoryName(review.SdkToolPath)!, ".version");
            if (!File.Exists(versionFile) || File.ReadAllLines(versionFile).ElementAtOrDefault(1) != review.Sdk)
                Fail("SDK version differs from the captured SDK installation.");
        }
        // An already rejected review cannot bind. Avoid repeatedly hashing an entire SDK for such requests.
        if (limits.Count > 0) return limits;
        foreach (var file in capture.ExternalInputs)
        {
            token.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(file.Path) || PathSafety.IsUnder(file.Path, config.Target.Root)) { Fail("Invalid external input path."); continue; }
            if (!File.Exists(file.Path) || FileFact(file.Path, "build", token) is var actual && (actual.Sha256 != file.Sha256 || actual.Length != file.Length))
                Fail("External build input changed/missing: " + file.Path);
            else hashes[file.Path] = file.Sha256;
        }
        if (capture.ExternalInputs.Select(f => f.Path).Distinct(PathComparer).Count() != capture.ExternalInputs.Length) Fail("Duplicate external input.");
        if (!capture.ExternalInputs.Any(f => f.Path.Equals(review.SdkToolPath, PathSafety.Comparison))) Fail("Actual SDK tool was not captured before the build.");
        if (!Path.IsPathFullyQualified(review.SdkToolPath)) Fail("SDK tool must be an absolute captured path.");
        else
        {
            var sdkVersionPath = Path.Combine(Path.GetDirectoryName(review.SdkToolPath)!, ".version");
            if (!capture.ExternalInputs.Any(f => f.Path.Equals(sdkVersionPath, PathSafety.Comparison)) || !File.Exists(sdkVersionPath) ||
                File.ReadAllLines(sdkVersionPath).ElementAtOrDefault(1) != review.Sdk)
                Fail("SDK version differs from the captured SDK installation.");
        }
        foreach (var role in new[] { "build-log", "evaluation-trace", "review-notes", "generated-input" })
            if (!review.Materials.Any(m => m.Role == role && m.Length > 0)) Fail("Missing review material: " + role);
        foreach (var material in review.Materials)
        {
            token.ThrowIfCancellationRequested();
            PathSafety.EnsureDisjoint(config.Target.Root, material.Path);
            if (!File.Exists(material.Path) || FileFact(material.Path, "evidence", token) is var actual &&
                (actual.Sha256 != material.Sha256 || actual.Length != material.Length)) Fail("Review material changed/missing: " + material.Role);
            else hashes[Path.GetFullPath(material.Path)] = material.Sha256;
        }
        foreach (var tracked in review.Imports.Concat(review.GeneratorsAndTasks))
            if (!capture.ExternalInputs.Any(f => f.Path.Equals(tracked, PathSafety.Comparison)) &&
                !snapshot.InputIdentity.Files.Any(f => PathSafety.Under(config.Target.Root, f.Path).Equals(tracked, PathSafety.Comparison))) Fail("Untracked import/generator/task: " + tracked);
        if (review.Projects.Select(p => p.SourceProject).Distinct(StringComparer.Ordinal).Count() != review.Projects.Length ||
            !snapshot.Projects.Select(p => p.Id).Order(StringComparer.Ordinal).SequenceEqual(review.Projects.Select(p => p.SourceProject).Order(StringComparer.Ordinal))) Fail("Evaluated project coverage is incomplete/duplicate.");
        foreach (var project in review.Projects)
            if (project.TargetFramework != review.TargetFramework || string.IsNullOrWhiteSpace(project.AssemblyName) ||
                currentProperties.Any(p => !project.EvaluatedProperties.TryGetValue(p, out var value) || value.Contains("REVIEW ACTUAL", StringComparison.Ordinal) ||
                    value.Contains("$(", StringComparison.Ordinal))) Fail("Evaluated facts missing for " + project.SourceProject);
        foreach (var path in review.FrameworkPaths)
            if (!capture.ExternalInputs.Any(f => f.Path.Equals(path, PathSafety.Comparison))) Fail("Framework input not captured: " + path);
        foreach (var assembly in review.Assemblies) PathSafety.EnsureDisjoint(config.Target.Root, assembly.Path);
        hashes[capturePath] = AssemblyArtifacts.FileHash(capturePath, token); hashes[reviewPath] = AssemblyArtifacts.FileHash(reviewPath, token);
        return limits;
    }

    internal static void ValidateClosure(string[] paths, string[] frameworkPaths, List<string> limits)
    {
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            PathSafety.EnsureNoLinks(path);
            var identity = AssemblyName.GetAssemblyName(path);
            if (!identities.TryAdd(identity.Name!, identity.FullName)) limits.Add("External evidence: Duplicate closure identity: " + identity.Name);
        }
        // SDK reference surfaces can legitimately contain several framework versions (netstandard/net8/net10).
        // Match exact PE identities; never substitute a newer same-name framework or relax non-framework conflicts.
        var frameworks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in frameworkPaths)
        {
            PathSafety.EnsureNoLinks(path);
            if (!frameworks.Add(AssemblyName.GetAssemblyName(path).FullName)) limits.Add("External evidence: Duplicate framework identity.");
        }
        foreach (var path in paths)
        {
            using var stream = File.OpenRead(path); using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
            var reader = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                var name = reader.GetString(reference.Name);
                var expected = new AssemblyName { Name = name, Version = reference.Version, CultureName = reader.GetString(reference.Culture) };
                if ((reference.Flags & AssemblyFlags.PublicKey) != 0) expected.SetPublicKey(reader.GetBlobBytes(reference.PublicKeyOrToken));
                else expected.SetPublicKeyToken(reader.GetBlobBytes(reference.PublicKeyOrToken));
                if (identities.TryGetValue(name, out var actual) ? actual != expected.FullName : !frameworks.Contains(expected.FullName))
                    limits.Add("External evidence: Missing/mismatched dependency closure: " + name);
            }
        }
    }

    public static T Read<T>(string path)
    {
        PathSafety.EnsureNoLinks(path);
        var maximum = typeof(T) == typeof(EvidenceCapture) ? RuleLoader.MaximumJsonBytes : ProjectContextEvidence.MaximumJsonBytes;
        if (new FileInfo(path).Length > maximum) throw new ConfigurationException("Evidence JSON exceeds its bounded size limit.");
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes);
        var partitioned = typeof(T) == typeof(ExternalBuildReview) && document.RootElement.GetProperty("schemaVersion").GetInt32() == 2;
        if (!partitioned && bytes.Length > RuleLoader.MaximumJsonBytes) throw new ConfigurationException("Evidence JSON exceeds 4 MiB.");
        SchemaValidation.Validate(document.RootElement, typeof(T) == typeof(EvidenceCapture) ? "evidence-capture" : partitioned ? "external-build-review-v2" : "external-build-review");
        return document.RootElement.Deserialize<T>(JsonContract.Options)!;
    }
    public static void WriteNew<T>(string path, T value)
    {
        PathSafety.EnsureNoLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, value, JsonContract.Options);
    }
    public static InputFile FileFact(string path, string kind, CancellationToken token = default)
    {
        path = Path.GetFullPath(path); PathSafety.EnsureNoLinks(path);
        return new(path, AssemblyArtifacts.FileHash(path, token), new FileInfo(path).Length, kind);
    }
    private static string EnvironmentHash(string name) => ContentHash.Text(JsonSerializer.Serialize(Environment.GetEnvironmentVariable(name)));
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static bool SameFiles(InputFile[] a, InputFile[] b) => a.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => (f.Path, f.Sha256, f.Length))
        .SequenceEqual(b.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => (f.Path, f.Sha256, f.Length)));
}
