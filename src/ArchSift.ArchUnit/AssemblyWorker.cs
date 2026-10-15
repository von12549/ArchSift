using System.Text.Json;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using ArchSift.Contracts;
using ArchSift.Core;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace ArchSift.ArchUnit;

public static class AssemblyWorker
{
    public static AssemblyEvaluation Evaluate(WorkerRequest request)
        => request.Inputs.Contexts.Length > 0 ? EvaluateContexts(request) : EvaluateSingle(request);

    private static AssemblyEvaluation EvaluateSingle(WorkerRequest request, string? contextId = null, string? primary = null, string[]? frameworks = null,
        Dictionary<string, string>? owners = null)
    {
        var engine = new ArchUnitEngine();
        var versions = new Dictionary<string, string> { ["archunitnet"] = engine.Version, ["archunitnetAssembly"] = engine.LoadedAssemblyVersion };
        var rules = request.Rulesets.SelectMany(s => s.Rules).Where(r => r.Enabled &&
            (r.Type == "type-dependency" || r.Type == "naming" && r.Parameters.GetProperty("subjectKind").GetString() is "type" or "assembly")).ToArray();
        var exceptions = request.Rulesets.SelectMany(s => s.Exceptions).ToArray();
        var limits = request.Inputs.Limitations.ToList();
        var findings = new List<Finding>(); var results = new List<RuleResult>();
        var complete = request.Inputs.CoverageComplete;
        var inspected = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var path in request.Inputs.Paths)
            {
                PathSafety.EnsureNoLinks(path);
                if (!request.Inputs.Sha256.TryGetValue(path, out var expected) || AssemblyArtifacts.FileHash(path) != expected)
                    throw new InvalidDataException("Assembly bytes changed or were not bound.");
                var metadata = AssemblyArtifacts.Inspect(path);
                if (!inspected.TryAdd(metadata.Name, path)) throw new InvalidDataException("Duplicate assembly identity.");
            }
            if (primary is not null)
            {
                if (owners is null)
                {
                    var closure = new List<string>(); ProjectContextEvidence.ValidateClosure(request.Inputs.Paths, closure, frameworks, primary);
                    limits.AddRange(closure); complete &= closure.Count == 0;
                }
            }
            else
            {
            var runtimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            var frameworkNames = Directory.GetFiles(runtimeDirectory, "*.dll").Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
            var ownDeps = Path.ChangeExtension(System.Reflection.Assembly.GetEntryAssembly()!.Location, ".deps.json");
            if (File.Exists(ownDeps))
            {
                using var deps = JsonDocument.Parse(File.ReadAllBytes(ownDeps));
                var target = deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
                var packs = deps.RootElement.GetProperty("targets").GetProperty(target).EnumerateObject()
                    .Where(p => p.Name.StartsWith("runtimepack.Microsoft.NETCore.App.", StringComparison.Ordinal) ||
                        p.Name.StartsWith("runtimepack.Microsoft.AspNetCore.App.", StringComparison.Ordinal)).ToArray();
                if (packs.Length > 0)
                {
                    frameworkNames.Clear();
                    foreach (var pack in packs)
                        if (pack.Value.TryGetProperty("runtime", out var runtime))
                            frameworkNames.UnionWith(runtime.EnumerateObject().Select(p => Path.GetFileNameWithoutExtension(p.Name)));
                }
            }
            var shared = Path.GetDirectoryName(Path.GetDirectoryName(runtimeDirectory.TrimEnd(Path.DirectorySeparatorChar)))!;
            var aspnet = Path.Combine(shared, "Microsoft.AspNetCore.App");
            if (Directory.Exists(aspnet))
                foreach (var version in Directory.GetDirectories(aspnet).Where(d => Path.GetFileName(d).StartsWith(
                             (request.Inputs.TargetFramework ?? "net10.0")[3..] + ".", StringComparison.Ordinal)))
                    frameworkNames.UnionWith(Directory.GetFiles(version, "*.dll").Select(Path.GetFileNameWithoutExtension));
            foreach (var path in request.Inputs.Paths)
            {
                using var stream = File.OpenRead(path); using var pe = new PEReader(stream); var reader = pe.GetMetadataReader();
                foreach (var handle in reader.AssemblyReferences)
                {
                    var reference = reader.GetAssemblyReference(handle);
                    var name = reader.GetString(reference.Name);
                    if (frameworkNames.Contains(name)) continue;
                    if (!inspected.TryGetValue(name, out var dependencyPath))
                    { complete = false; limits.Add("Assembly closure is missing: " + name); continue; }
                    var expected = new AssemblyName { Name = name, Version = reference.Version, CultureName = reader.GetString(reference.Culture) };
                    if ((reference.Flags & AssemblyFlags.PublicKey) != 0) expected.SetPublicKey(reader.GetBlobBytes(reference.PublicKeyOrToken));
                    else expected.SetPublicKeyToken(reader.GetBlobBytes(reference.PublicKeyOrToken));
                    if (AssemblyName.GetAssemblyName(dependencyPath).FullName != expected.FullName)
                    { complete = false; limits.Add("Assembly closure version/token mismatch: " + name); }
                }
            }
            }
            var loader = new ArchLoader().WithoutArchitectureCache().WithoutRuleEvaluationCache();
            foreach (var path in owners is not null ? request.Inputs.Paths.Where(p => owners.ContainsKey(AssemblyArtifacts.Inspect(p).Name)) : primary is null ? request.Inputs.Paths : new[] { primary })
                loader.LoadFilteredDirectory(Path.GetDirectoryName(path)!, Path.GetFileName(path));
            var architecture = loader.Build();
            var primaryName = primary is null ? null : AssemblyArtifacts.Inspect(primary).Name;
            var types = architecture.Types.Where(t => !t.IsStub && !t.IsGenericParameter &&
                (owners is not null ? owners.ContainsKey(t.Assembly.Name) : primaryName is null ? inspected.ContainsKey(t.Assembly.Name) : t.Assembly.Name == primaryName)).ToArray();
            foreach (var rule in rules)
            {
                var current = new List<Finding>(); var matched = 0; var allowEmpty = rule.Scope.AllowEmpty;
                if (rule.Type == "type-dependency")
                {
                    var source = rule.Parameters.GetProperty("source").Deserialize<Selector>(JsonContract.Options)!;
                    var target = rule.Parameters.GetProperty("forbiddenTarget").Deserialize<Selector>(JsonContract.Options)!;
                    var sources = types.Where(t => Match(rule.Scope, t) && Match(source, t)).ToArray();
                    matched = sources.Length; allowEmpty |= source.AllowEmpty;
                    foreach (var type in sources)
                    foreach (var dependency in type.Dependencies)
                    foreach (var dependencyTarget in TypeDependencyTraversal.Targets(dependency.Target, dependency.TargetGenericArguments))
                        if (Match(target, dependencyTarget))
                            Add(rule.Scope.Kind, rule.Scope.Kind == "namespace" ? type.Namespace.FullName : type.FullName,
                                "ArchUnitNET detected a forbidden type dependency.", type.FullName, dependencyTarget.FullName, type.Assembly.Name + ":" + type.FullName);
                }
                else
                {
                    var required = RuleSemantics.NameSelector(rule);
                    if (required.Kind == "assembly")
                    {
                        var names = inspected.Keys.Where(n => (owners is not null ? owners.ContainsKey(n) : primaryName is null || n == primaryName) && SelectorMatcher.Matches(rule.Scope, n)).ToArray(); matched = names.Length;
                        foreach (var name in names)
                            if (!SelectorMatcher.Matches(required, name)) Add("assembly", name, "Assembly name does not match the naming rule.");
                    }
                    else
                    {
                        var scoped = types.Where(t => Match(rule.Scope, t)).ToArray(); matched = scoped.Length;
                        foreach (var type in scoped)
                            if (!SelectorMatcher.Matches(required, type.FullName)) Add("type", type.FullName, "Type name does not match the naming rule.", location: type.Assembly.Name + ":" + type.FullName);
                    }
                }
                current = ProjectRuleEvaluator.ApplyExceptions(current, exceptions);
                findings.AddRange(current);
                var ruleLimits = limits.Distinct().ToList();
                if (!request.Inputs.SourceBound) ruleLimits.Add("assemblies-only: cannot establish current-source conformance.");
                if (matched == 0 && !allowEmpty) ruleLimits.Add("Source selector matched zero types/assemblies.");
                var status = !request.Inputs.SourceBound ? "inconclusive"
                    : current.Any(f => f.ExceptionId is null) ? "violation"
                    : !complete ? "inconclusive"
                    : matched == 0 ? (allowEmpty ? "not-applicable" : "inconclusive") : "pass";
                results.Add(new(rule.Id, status, matched, current.Select(f => f.Id).Distinct().Order(StringComparer.Ordinal).ToArray(),
                    ruleLimits.Distinct().Order(StringComparer.Ordinal).ToArray()));
                void Add(string kind, string subject, string message, string? source = null, string? target = null, string? location = null)
                {
                    var identity = string.Join("\0", rule.Id, kind, subject, source ?? "", target ?? "");
                    var owner = owners is not null ? owners[(location?.Split(':')[0]) ?? subject] : contextId;
                    if (owner is not null) identity += "\0" + owner;
                    current.Add(new(ContentHash.Text(identity), rule.Id, kind, subject, message, rule.Severity, source, target,
                        owner is null ? location : "@context/" + owner + "/" + (location ?? subject)));
                }
            }
            foreach (var path in request.Inputs.Paths)
                if (AssemblyArtifacts.FileHash(path) != request.Inputs.Sha256[path]) throw new InvalidDataException("Assembly changed during analysis.");
            return new(results.OrderBy(r => r.RuleId, StringComparer.Ordinal).ToArray(),
                findings.DistinctBy(f => f.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToArray(), owners?.Count ?? (primary is null ? inspected.Count : 1), [], versions);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            return new(rules.Select(r => new RuleResult(r.Id, "inconclusive", 0, [], [error.Message])).ToArray(), [], 0, [error.Message], versions);
        }
    }

    private static AssemblyEvaluation EvaluateContexts(WorkerRequest request)
    {
        var parent = Path.GetFullPath(request.ContextWorkspace ?? Path.GetTempPath()); PathSafety.EnsureNoLinks(parent);
        var root = PathSafety.Under(parent, "context-worker-" + Guid.NewGuid().ToString("N"));
        var evaluations = new List<AssemblyEvaluation>();
        try
        {
            if (Directory.Exists(root)) throw new IOException("Context workspace already exists.");
            Directory.CreateDirectory(root);
            var batches = new List<(Dictionary<string, string> Files, Dictionary<string, string> Hashes, Dictionary<string, string> Owners)>();
            foreach (var context in request.Inputs.Contexts)
            {
                var closure = new List<string>(); ProjectContextEvidence.ValidateClosure(context.Paths, closure, context.FrameworkPaths, context.PrimaryPath);
                if (closure.Count > 0) throw new InvalidDataException(string.Join("; ", closure));
                var selections = context.Paths.ToDictionary(p => AssemblyArtifacts.Inspect(p).Name, p => p, StringComparer.Ordinal);
                var ownerName = AssemblyArtifacts.Inspect(context.PrimaryPath).Name;
                var batchIndex = batches.FindIndex(b => !b.Owners.ContainsKey(ownerName) && selections.All(s => !b.Hashes.TryGetValue(s.Key, out var previous) || previous == request.Inputs.Sha256[s.Value]));
                if (batchIndex < 0) { batchIndex = batches.Count; batches.Add((new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal))); }
                var batch = batches[batchIndex];
                foreach (var selection in selections) { batch.Files.TryAdd(selection.Key, selection.Value); batch.Hashes.TryAdd(selection.Key, request.Inputs.Sha256[selection.Value]); }
                batch.Owners.Add(ownerName, context.Id);
            }
            var index = 0;
            foreach (var batch in batches)
            {
                var directory = PathSafety.Under(root, (index++).ToString("D4")); Directory.CreateDirectory(directory);
                var paths = new List<string>(); var hashes = new Dictionary<string, string>(StringComparer.Ordinal); string? primary = null;
                foreach (var original in batch.Files.Values)
                {
                    PathSafety.EnsureNoLinks(original);
                    if (!request.Inputs.Sha256.TryGetValue(original, out var expected) || AssemblyArtifacts.FileHash(original) != expected)
                        throw new InvalidDataException("Context assembly changed or is unbound.");
                    var destination = PathSafety.Under(directory, AssemblyArtifacts.Inspect(original).Name + ".dll");
                    File.Copy(original, destination, false);
                    if (AssemblyArtifacts.FileHash(destination) != expected) throw new InvalidDataException("Context copy changed.");
                    paths.Add(destination); hashes[destination] = expected;
                    if (batch.Owners.ContainsKey(AssemblyArtifacts.Inspect(original).Name)) primary ??= destination;
                }
                if (primary is null) throw new InvalidDataException("Context primary assembly is missing.");
                var inputs = request.Inputs with { Paths = paths.ToArray(), Sha256 = hashes, Contexts = [] };
                evaluations.Add(EvaluateSingle(new(inputs, request.Rulesets), primary: primary, owners: batch.Owners));
            }
            foreach (var file in request.Inputs.Paths)
                if (AssemblyArtifacts.FileHash(file) != request.Inputs.Sha256[file]) throw new InvalidDataException("Context input changed during analysis.");
            var results = evaluations.SelectMany(e => e.Results).GroupBy(r => r.RuleId, StringComparer.Ordinal).Select(group =>
            {
                var matched = group.Sum(r => r.Matched);
                var relevant = group.Where(r => r.Matched > 0 || r.Limitations.Any(l => l != "Source selector matched zero types/assemblies.")).ToArray();
                var status = !request.Inputs.SourceBound ? "inconclusive" : group.Any(r => r.Status == "violation") ? "violation"
                    : relevant.Any(r => r.Status is "inconclusive" or "error") ? "inconclusive" : matched > 0 ? "pass"
                    : group.All(r => r.Status == "not-applicable") ? "not-applicable" : "inconclusive";
                var limitations = relevant.SelectMany(r => r.Limitations).Distinct().Order(StringComparer.Ordinal).ToArray();
                if (matched == 0 && status == "inconclusive") limitations = [.. limitations, "Source selector matched zero types/assemblies."];
                return new RuleResult(group.Key, status, matched, group.SelectMany(r => r.FindingIds).Distinct().Order(StringComparer.Ordinal).ToArray(), limitations);
            }).OrderBy(r => r.RuleId, StringComparer.Ordinal).ToArray();
            return new(results, evaluations.SelectMany(e => e.Findings).DistinctBy(f => f.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToArray(),
                evaluations.Sum(e => e.AssemblyCount), evaluations.SelectMany(e => e.Errors).Distinct().ToArray(), evaluations.First().EngineVersions);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or BadImageFormatException or ArgumentException or InvalidOperationException)
        {
            return new(request.Rulesets.SelectMany(s => s.Rules).Where(r => r.Enabled &&
                (r.Type == "type-dependency" || r.Type == "naming" && r.Parameters.GetProperty("subjectKind").GetString() is "type" or "assembly"))
                .Select(r => new RuleResult(r.Id, "inconclusive", 0, [], [error.Message])).ToArray(),
                [], 0, [error.Message], new(StringComparer.Ordinal));
        }
        finally
        {
            // The process client reclaims its dedicated lease after exit, when metadata readers can no longer hold files open.
            if (!request.ParentOwnsContextCleanup && Directory.Exists(root)) { PathSafety.EnsureNoLinks(root); if (!PathSafety.IsUnder(root, parent)) throw new IOException("Unsafe context cleanup."); Directory.Delete(root, true); }
        }
    }

    private static bool Match(Selector selector, IType type) => SelectorMatcher.Matches(selector,
        selector.Kind == "namespace" ? type.Namespace.FullName : type.FullName);

}
