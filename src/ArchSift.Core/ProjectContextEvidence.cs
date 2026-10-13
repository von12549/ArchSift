using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

/// <summary>Verifies reviewed per-project compiler resolutions without executing any target code.</summary>
public static class ProjectContextEvidence
{
    public const int MaximumJsonBytes = 16 * 1024 * 1024;
    public static void Seal(RunConfiguration config, EvidenceCapture capture, ExternalBuildReview review,
        string capturePath, string reviewPath, string output, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var source = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework, token);
        var rejected = ExternalBuildEvidence.ValidateReview(config, source, capture, review, capturePath, reviewPath, new(StringComparer.Ordinal), token);
        if (rejected.Count > 0) throw new ConfigurationException(string.Join("\n", rejected));
        var directory = Path.GetDirectoryName(output)!;
        var manifest = new AssemblyManifest
        {
            SchemaVersion = 3, InputIdentity = capture.InputIdentity, BuildInputIdentity = capture.BuildInputIdentity,
            Sdk = review.Sdk, TargetFramework = review.TargetFramework, Configuration = review.Configuration, Generation = "external",
            ReviewedBuild = new(capturePath, AssemblyArtifacts.FileHash(capturePath, token), reviewPath, AssemblyArtifacts.FileHash(reviewPath, token)),
            ProjectContexts = review.ProjectContexts,
            Assemblies = review.Assemblies.Select(a => { token.ThrowIfCancellationRequested(); return new AssemblyEntry(AssemblyArtifacts.Inspect(a.Path).Name, a.SourceProject,
                Path.GetRelativePath(directory, a.Path).Replace('\\', '/'), AssemblyArtifacts.FileHash(a.Path, token)); }).ToArray()
        };
        var temporary = output + ".check-" + Guid.NewGuid().ToString("N");
        try
        {
            ExternalBuildEvidence.WriteNew(temporary, manifest);
            var snapshot = ProjectDiscovery.Discover(config.Target.Root, config.Target.Entry, config.Build.TargetFramework, token);
            var result = AssemblyArtifacts.Existing(config with { Build = config.Build with { AssemblyManifest = temporary } }, snapshot, token);
            if (!result.SourceBound) throw new ConfigurationException(string.Join("\n", result.Limitations));
            token.ThrowIfCancellationRequested();
            File.Move(temporary, output, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static AssemblyInputs Existing(RunConfiguration config, ProjectSnapshot snapshot, JsonElement document, CancellationToken token = default)
    {
        SchemaValidation.Validate(document, "assembly-manifest-v3");
        var manifest = document.Deserialize<AssemblyManifest>(JsonContract.Options)!;
        var path = Path.GetFullPath(config.Build.AssemblyManifest!);
        PathSafety.EnsureDisjoint(config.Target.Root, path);
        var directory = Path.GetDirectoryName(path)!;
        var link = manifest.ReviewedBuild!;
        var capturePath = Path.GetFullPath(link.CapturePath); var reviewPath = Path.GetFullPath(link.ReviewPath);
        PathSafety.EnsureDisjoint(config.Target.Root, capturePath); PathSafety.EnsureDisjoint(config.Target.Root, reviewPath);
        var capture = ExternalBuildEvidence.Read<EvidenceCapture>(capturePath);
        var review = ExternalBuildEvidence.Read<ExternalBuildReview>(reviewPath);
        var limits = new List<string>(); var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        void Fail(string reason) => limits.Add("Project context evidence: " + reason);
        if (review.SchemaVersion != 2 || review.BuildSnapshotRoot is null || review.ProjectContexts is null) Fail("Partitioned review v2 is required.");
        if (AssemblyArtifacts.FileHash(capturePath, token) != link.CaptureSha256 || AssemblyArtifacts.FileHash(reviewPath, token) != link.ReviewSha256) Fail("Capture/review bytes differ from seal.");
        if (limits.Count == 0) limits.AddRange(ExternalBuildEvidence.ValidateReview(config, snapshot, capture, review, capturePath, reviewPath, hashes, token));
        if (manifest.Sdk != review.Sdk || manifest.TargetFramework != review.TargetFramework || manifest.Configuration != review.Configuration ||
            JsonSerializer.Serialize(manifest.InputIdentity, JsonContract.Options) != JsonSerializer.Serialize(capture.InputIdentity, JsonContract.Options) ||
            JsonSerializer.Serialize(manifest.BuildInputIdentity, JsonContract.Options) != JsonSerializer.Serialize(capture.BuildInputIdentity, JsonContract.Options) ||
            JsonSerializer.Serialize(manifest.ProjectContexts, JsonContract.Options) != JsonSerializer.Serialize(review.ProjectContexts, JsonContract.Options)) Fail("Sealed context/source facts differ from review.");
        var primary = new Dictionary<string, string>(StringComparer.Ordinal);
        var primaryHashes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var fileHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var contexts = new List<AssemblyAnalysisContext>();
        foreach (var entry in manifest.Assemblies)
        {
            token.ThrowIfCancellationRequested();
            var file = PathSafety.Under(directory, entry.Path);
            if (!File.Exists(file)) { Fail("Primary assembly is missing: " + entry.SourceProject); continue; }
            var facts = AssemblyArtifacts.Inspect(file);
            var project = review.Projects.SingleOrDefault(p => p.SourceProject == entry.SourceProject);
            if (entry.SourceProject == "@dependency" || project is null || project.AssemblyName != facts.Name || entry.AssemblyName != facts.Name ||
                facts.Framework != ".NETCoreApp,Version=v" + manifest.TargetFramework[3..] || facts.Configuration != manifest.Configuration ||
                !review.Assemblies.Any(a => a.SourceProject == entry.SourceProject && SamePath(a.Path, file))) Fail("Primary project/PE mapping differs: " + entry.SourceProject);
            var hash = AssemblyArtifacts.FileHash(file, token);
            if (hash != entry.Sha256 || !primary.TryAdd(entry.SourceProject, file)) Fail("Primary hash/ownership is invalid: " + entry.SourceProject);
            if (!primaryHashes.TryGetValue(facts.Name, out var variants)) primaryHashes[facts.Name] = variants = new(StringComparer.Ordinal);
            variants.Add(hash);
            fileHashes[file] = hash;
        }
        if (primary.Count == 0 || !snapshot.Projects.Select(p => p.Id).Order(StringComparer.Ordinal).SequenceEqual(primary.Keys.Order(StringComparer.Ordinal)) ||
            snapshot.Projects.Any(p => !p.Supported)) Fail("Every discovered project requires one supported primary assembly.");
        if (limits.Count == 0)
        {
            var buildRoot = Path.GetFullPath(review.BuildSnapshotRoot!); PathSafety.EnsureDisjoint(config.Target.Root, buildRoot);
            var builtSource = InputCapture.Capture(buildRoot, token);
            if (builtSource.Sha256 != capture.InputIdentity.Sha256) Fail("Build snapshot differs from captured current source.");
            foreach (var file in builtSource.Files) hashes[PathSafety.Under(buildRoot, file.Path)] = file.Sha256;
            var owned = new HashSet<string>(StringComparer.Ordinal); var ids = new HashSet<string>(StringComparer.Ordinal);
            var identities = new Dictionary<string, AssemblyName>(StringComparer.Ordinal);
            var external = capture.ExternalInputs.ToDictionary(f => Path.GetFullPath(f.Path), f => f.Sha256, PathComparer);
            var generated = review.Materials.Where(m => m.Role == "generated-input").ToDictionary(m => Path.GetFullPath(m.Path), m => m.Sha256, PathComparer);
            var snapshotPrimaryHashes = primaryHashes.Values.SelectMany(s => s).ToHashSet(StringComparer.Ordinal);
            foreach (var context in review.ProjectContexts!)
            {
                token.ThrowIfCancellationRequested();
                if (!ids.Add(context.Id) || !owned.Add(context.SourceProject) || !primary.TryGetValue(context.SourceProject, out var root) ||
                    !SamePath(root, context.AssemblyPath) || context.ResolutionPolicy != "dotnet-reference-path") { Fail("Duplicate/invalid context ownership: " + context.Id); continue; }
                var previousLimitCount = limits.Count;
                try { ValidateEvaluation(context, review, buildRoot, capture.PropertyNames, Fail); }
                catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
                { Fail("Malformed project evaluation material: " + context.Id); }
                if (limits.Count != previousLimitCount) continue;
                var runtime = (context.RuntimePaths ?? []).Select(Path.GetFullPath).ToArray();
                var platformCandidates = new HashSet<string>(PathComparer);
                if (context.RuntimePaths is not null)
                {
                    using var evaluation = JsonDocument.Parse(File.ReadAllBytes(context.EvaluationPath));
                    foreach (var item in evaluation.RootElement.GetProperty("Items").GetProperty("ReferenceCopyLocalPaths").EnumerateArray())
                        if (item.TryGetProperty("RuntimeIdentifier", out var rid) && !string.IsNullOrWhiteSpace(rid.GetString()))
                            platformCandidates.Add(Path.GetFullPath(item.GetProperty("FullPath").GetString()!));
                }
                var chosen = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var file in context.ReferencePaths.Select(Path.GetFullPath))
                {
                    var name = AssemblyName.GetAssemblyName(file).Name!;
                    if (!chosen.TryAdd(name, file)) Fail("Duplicate compiler selection within " + context.Id + ": " + name);
                }
                var runtimeNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var file in runtime)
                {
                    PathSafety.EnsureDisjoint(config.Target.Root, file); PathSafety.EnsureNoLinks(file);
                    var hash = AssemblyArtifacts.FileHash(file, token); hashes[file] = hash;
                    if (!(external.TryGetValue(file, out var before) && before == hash) &&
                        !(generated.TryGetValue(file, out var generatedHash) && generatedHash == hash) && !snapshotPrimaryHashes.Contains(hash))
                        Fail("Copy-local artifact was not captured/generated by this build: " + file);
                    AssemblyName identity;
                    try { identity = AssemblyName.GetAssemblyName(file); }
                    catch (BadImageFormatException) { continue; } // Native/data assets remain hash-bound; native execution is outside type-analysis scope.
                    if (!string.IsNullOrEmpty(identity.CultureName) && identity.Name!.EndsWith(".resources", StringComparison.Ordinal)) continue;
                    if (platformCandidates.Contains(file)) continue; // Portable compilation context uses the unqualified asset; platform variants remain hash-bound, not guessed.
                    if (!runtimeNames.Add(identity.Name!)) Fail("Duplicate copy-local selection within " + context.Id + ": " + identity.Name);
                    if (chosen.TryGetValue(identity.Name!, out var compiler))
                    {
                        if (!CanResolve(AssemblyName.GetAssemblyName(compiler), identity)) Fail("Copy-local identity is incompatible with compiler selection: " + context.Id);
                        // SDK reference contracts remain selected contracts, never replaced by an arbitrary implementation.
                        if (!IsSdkReference(compiler, review.SdkToolPath, review.TargetFramework)) chosen[identity.Name!] = file;
                    }
                    else chosen[identity.Name!] = file;
                }
                var paths = new[] { root }.Concat(chosen.Values).ToArray();
                var selected = new Dictionary<string, AssemblyName>(StringComparer.Ordinal);
                foreach (var file in paths)
                {
                    PathSafety.EnsureDisjoint(config.Target.Root, file); PathSafety.EnsureNoLinks(file);
                    if (!identities.TryGetValue(file, out var identity)) identities[file] = identity = AssemblyName.GetAssemblyName(file);
                    if (!selected.TryAdd(identity.Name!, identity)) Fail("Conflicting/duplicate selection within " + context.Id + ": " + identity.Name);
                    if (!fileHashes.TryGetValue(file, out var hash)) fileHashes[file] = hash = AssemblyArtifacts.FileHash(file, token);
                    if (primaryHashes.TryGetValue(identity.Name!, out var current))
                    { if (!current.Contains(hash)) Fail("Old/wrong first-party dependency in " + context.Id + ": " + identity.Name); }
                    else if (!external.TryGetValue(file, out var before) || before != hash) Fail("Reference not captured before build: " + file);
                }
                var frameworks = context.ReferencePaths.Where(p => IsSdkReference(p, review.SdkToolPath, review.TargetFramework)).ToArray();
                var closure = new List<string>(); ValidateClosure(paths, closure, frameworks, root); limits.AddRange(closure.Select(s => "Project context " + context.Id + ": " + s));
                contexts.Add(new(context.Id, context.SourceProject, root, paths) { FrameworkPaths = frameworks });
            }
            if (!primary.Keys.Order(StringComparer.Ordinal).SequenceEqual(owned.Order(StringComparer.Ordinal))) Fail("Context owner coverage is incomplete.");
        }
        hashes[path] = AssemblyArtifacts.FileHash(path, token); hashes[capturePath] = AssemblyArtifacts.FileHash(capturePath, token); hashes[reviewPath] = AssemblyArtifacts.FileHash(reviewPath, token);
        var bound = limits.Count == 0;
        if (manifest.Configuration == "Release") limits.Add("Release optimization may remove type dependencies.");
        return new(fileHashes.Keys.ToArray(), fileHashes, bound, bound && limits.Count == 0, limits.Distinct().ToArray(), manifest.Sdk, manifest.TargetFramework, manifest.Configuration)
        { Contexts = contexts.ToArray(), EvidenceOrigin = "external-reviewed", EvidenceHashes = hashes };
    }

    private static void ValidateEvaluation(ReviewedAssemblyContext context, ExternalBuildReview review, string buildRoot,
        string[] propertyNames, Action<string> fail)
    {
        if (!review.Materials.Any(m => m.Role == "evaluation-trace" && SamePath(m.Path, context.EvaluationPath))) { fail("Missing actual evaluation material: " + context.Id); return; }
        PathSafety.EnsureNoLinks(context.EvaluationPath);
        if (new FileInfo(context.EvaluationPath).Length > MaximumJsonBytes) { fail("Evaluation material is too large."); return; }
        using var trace = JsonDocument.Parse(File.ReadAllBytes(context.EvaluationPath));
        var props = trace.RootElement.GetProperty("Properties");
        var project = review.Projects.Single(p => p.SourceProject == context.SourceProject);
        if (!SamePath(props.GetProperty("MSBuildProjectFullPath").GetString()!, PathSafety.Under(buildRoot, context.SourceProject)) ||
            !SamePath(props.GetProperty("TargetPath").GetString()!, context.AssemblyPath) || props.GetProperty("AssemblyName").GetString() != project.AssemblyName ||
            props.GetProperty("TargetFramework").GetString() != review.TargetFramework || props.GetProperty("Configuration").GetString() != review.Configuration ||
            propertyNames.Any(p => props.GetProperty(p).GetString() != project.EvaluatedProperties[p])) fail("Evaluated project/context properties differ: " + context.Id);
        var references = trace.RootElement.GetProperty("Items").GetProperty("ReferencePath").EnumerateArray()
            .Select(item => Path.GetFullPath(item.GetProperty("FullPath").GetString()!)).ToArray();
        var declared = context.ReferencePaths.Select(Path.GetFullPath).ToArray();
        if (references.Length != references.Distinct(PathComparer).Count() || declared.Length != declared.Distinct(PathComparer).Count() ||
            !references.Order(PathComparer).SequenceEqual(declared.Order(PathComparer), PathComparer)) fail("Selected references differ from actual MSBuild trace: " + context.Id);
        if (context.RuntimePaths is { } runtime)
        {
            var local = trace.RootElement.GetProperty("Items").GetProperty("ReferenceCopyLocalPaths").EnumerateArray().Select(i => Path.GetFullPath(i.GetProperty("FullPath").GetString()!)).ToArray();
            var paths = runtime.Select(Path.GetFullPath).ToArray();
            if (local.Length != local.Distinct(PathComparer).Count() || paths.Length != paths.Distinct(PathComparer).Count() ||
                !local.Order(PathComparer).SequenceEqual(paths.Order(PathComparer), PathComparer)) fail("Runtime references differ from actual copy-local trace: " + context.Id);
        }
    }

    /// <summary>Checks .NET compatible resolution against this explicit selection, never a directory scan/highest-version guess.</summary>
    public static void ValidateClosure(string[] paths, List<string> limits, string[]? frameworkPaths = null, string? primaryPath = null)
    {
        var selected = new Dictionary<string, AssemblyName>(StringComparer.Ordinal);
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var actual = AssemblyName.GetAssemblyName(path);
            if (!selected.TryAdd(actual.Name!, actual)) limits.Add("Duplicate selected assembly: " + actual.Name);
            byName.TryAdd(actual.Name!, path);
        }
        var pending = new Queue<string>(primaryPath is null ? paths : [primaryPath]);
        var visited = new HashSet<string>(PathComparer);
        while (pending.TryDequeue(out var path))
        {
            if (!visited.Add(path)) continue;
            // A selected SDK reference contract is a leaf. Optional forwarded APIs are not runtime dependencies of the analyzed program.
            if ((frameworkPaths ?? []).Any(p => SamePath(p, path))) continue;
            using var stream = File.OpenRead(path); using var pe = new PEReader(stream); var reader = pe.GetMetadataReader();
            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                var requested = new AssemblyName { Name = reader.GetString(reference.Name), Version = reference.Version, CultureName = reader.GetString(reference.Culture) };
                if ((reference.Flags & AssemblyFlags.PublicKey) != 0) requested.SetPublicKey(reader.GetBlobBytes(reference.PublicKeyOrToken));
                else requested.SetPublicKeyToken(reader.GetBlobBytes(reference.PublicKeyOrToken));
                if (!selected.TryGetValue(requested.Name!, out var actual) || !CanResolve(requested, actual))
                    limits.Add("Missing/incompatible resolved dependency of " + Path.GetFileName(path) + ": " + requested.FullName);
                else pending.Enqueue(byName[requested.Name!]);
            }
        }
    }
    public static bool CanResolve(AssemblyName requested, AssemblyName selected) => requested.Name == selected.Name &&
        (requested.CultureName ?? "") == (selected.CultureName ?? "") &&
        ((requested.GetPublicKeyToken() ?? []).Length == 0 || (requested.GetPublicKeyToken() ?? []).SequenceEqual(selected.GetPublicKeyToken() ?? [])) &&
        selected.Version is not null && requested.Version is not null && selected.Version >= requested.Version;
    private static bool IsSdkReference(string path, string sdkTool, string tfm)
    {
        var dotnet = Directory.GetParent(Path.GetDirectoryName(sdkTool)!)!.Parent!.FullName;
        var packs = Path.Combine(dotnet, "packs");
        if (!PathSafety.IsUnder(path, packs)) return false;
        var relative = Path.GetRelativePath(packs, path).Replace('\\', '/').Split('/');
        if (relative.Length != 5 || relative[0] is not ("Microsoft.NETCore.App.Ref" or "Microsoft.AspNetCore.App.Ref") || relative[2] != "ref" || relative[3] != tfm) return false;
        using var stream = File.OpenRead(path); using var pe = new PEReader(stream); var reader = pe.GetMetadataReader();
        if (reader.TypeDefinitions.Count == 1 && reader.MethodDefinitions.Count == 0 && reader.ExportedTypes.Count > 0) return true;
        foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
            {
                var declaring = reader.GetTypeDefinition(reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType());
                if (reader.GetString(declaring.Namespace) == "System.Runtime.CompilerServices" && reader.GetString(declaring.Name) == "ReferenceAssemblyAttribute") return true;
                continue;
            }
            if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
            var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (member.Parent.Kind != HandleKind.TypeReference) continue;
            var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (reader.GetString(type.Namespace) == "System.Runtime.CompilerServices" && reader.GetString(type.Name) == "ReferenceAssemblyAttribute") return true;
        }
        return false;
    }
    private static bool SamePath(string a, string b) => Path.GetFullPath(a).Equals(Path.GetFullPath(b), PathSafety.Comparison);
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
