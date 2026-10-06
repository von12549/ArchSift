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
            var loader = new ArchLoader().WithoutArchitectureCache().WithoutRuleEvaluationCache();
            foreach (var path in request.Inputs.Paths)
                loader.LoadFilteredDirectory(Path.GetDirectoryName(path)!, Path.GetFileName(path));
            var architecture = loader.Build();
            var types = architecture.Types.Where(t => !t.IsStub && !t.IsGenericParameter && inspected.ContainsKey(t.Assembly.Name)).ToArray();
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
                    foreach (var dependencyTarget in new[] { dependency.Target }.Concat(dependency.TargetGenericArguments.SelectMany(GenericTypes)))
                        if (Match(target, dependencyTarget))
                            Add(rule.Scope.Kind, rule.Scope.Kind == "namespace" ? type.Namespace.FullName : type.FullName,
                                "ArchUnitNET 检测到禁止的类型依赖。", type.FullName, dependencyTarget.FullName, type.Assembly.Name + ":" + type.FullName);
                }
                else
                {
                    var required = RuleSemantics.NameSelector(rule);
                    if (required.Kind == "assembly")
                    {
                        var names = inspected.Keys.Where(n => SelectorMatcher.Matches(rule.Scope, n)).ToArray(); matched = names.Length;
                        foreach (var name in names)
                            if (!SelectorMatcher.Matches(required, name)) Add("assembly", name, "程序集命名不符合规则。");
                    }
                    else
                    {
                        var scoped = types.Where(t => Match(rule.Scope, t)).ToArray(); matched = scoped.Length;
                        foreach (var type in scoped)
                            if (!SelectorMatcher.Matches(required, type.FullName)) Add("type", type.FullName, "类型命名不符合规则。", location: type.Assembly.Name + ":" + type.FullName);
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
                    current.Add(new(ContentHash.Text(string.Join("\0", rule.Id, kind, subject, source ?? "", target ?? "")),
                        rule.Id, kind, subject, message, rule.Severity, source, target, location));
                }
            }
            foreach (var path in request.Inputs.Paths)
                if (AssemblyArtifacts.FileHash(path) != request.Inputs.Sha256[path]) throw new InvalidDataException("Assembly changed during analysis.");
            return new(results.OrderBy(r => r.RuleId, StringComparer.Ordinal).ToArray(),
                findings.DistinctBy(f => f.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToArray(), inspected.Count, [], versions);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            return new(rules.Select(r => new RuleResult(r.Id, "inconclusive", 0, [], [error.Message])).ToArray(), [], 0, [error.Message], versions);
        }
    }

    private static bool Match(Selector selector, IType type) => SelectorMatcher.Matches(selector,
        selector.Kind == "namespace" ? type.Namespace.FullName : type.FullName);

    private static IEnumerable<IType> GenericTypes(GenericArgument argument)
    {
        var seen = new HashSet<GenericArgument>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<GenericArgument>(); pending.Push(argument);
        while (pending.TryPop(out var item))
        {
            if (!seen.Add(item)) continue;
            yield return item.Type;
            foreach (var child in item.GenericArguments) pending.Push(child);
        }
    }
}
