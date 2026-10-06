using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class ProjectDiscovery
{
    public static ProjectSnapshot Discover(string root, string? entry = null, string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        root = Path.GetFullPath(root);
        var before = InputCapture.Capture(root, cancellationToken);
        var projectPaths = before.Files.Where(f => f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path).Order(StringComparer.Ordinal).ToArray();
        if (projectPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != projectPaths.Length)
            throw new ConfigurationException("Case-colliding project identities.");
        var canonical = projectPaths.ToDictionary(p => p, p => p,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var projects = projectPaths.Select(p => ReadProject(root, p, canonical)).ToArray();
        var unsupported = new List<string>(before.Files.Where(f => f.Path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
            f.Path.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)).Select(f => f.Path + ": unsupported project language"));
        unsupported.AddRange(projects.SelectMany(p => p.Limitations.Select(l => p.Id + ": " + l)));
        string[] included;
        if (entry is null) included = projectPaths;
        else
        {
            var path = PathSafety.Under(root, entry);
            if (!File.Exists(path)) throw new ConfigurationException("Entry does not exist: " + entry);
            if (!before.Files.Any(f => Path.GetFullPath(f.Path, root).Equals(path, PathSafety.Comparison)))
                throw new ConfigurationException("Entry is excluded from the supported scan scope.");
            var suffix = Path.GetExtension(path).ToLowerInvariant();
            if (suffix == ".csproj") included = [Path.GetRelativePath(root, path).Replace('\\', '/')];
            else if (suffix is ".sln" or ".slnx") included = ReadSolution(root, path, unsupported);
            else throw new ConfigurationException("Entry must be .sln, .slnx or .csproj.");
        }
        var external = projects.SelectMany(p => p.References.Where(r => r.Status == "external").Select(r => p.Id + " -> " + r.Include)).ToList();
        var unresolved = projects.SelectMany(p => p.References.Where(r => r.Status == "missing").Select(r => p.Id + " -> " + r.Include)).ToList();
        var internalMembers = new List<string>();
        foreach (var member in included)
        {
            var full = Path.GetFullPath(member, root);
            if (!PathSafety.IsUnder(full, root)) { external.Add("entry -> " + member); continue; }
            PathSafety.EnsureNoLinks(full);
            if (!canonical.TryGetValue(member, out var id)) { unresolved.Add("entry -> " + member); continue; }
            internalMembers.Add(id);
        }
        var members = internalMembers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var unlisted = entry is not null && !Path.GetExtension(entry).Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            ? projectPaths.Except(members, StringComparer.Ordinal).ToArray() : [];
        var candidates = projects.SelectMany(p => p.Frameworks).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (targetFramework is null && projects.Any(p => p.Frameworks.Length > 1))
            throw new ConfigurationException("Multi-TFM input requires an explicit targetFramework. Candidates: " + string.Join(", ", candidates));
        if (targetFramework is not null && targetFramework is not ("net8.0" or "net9.0" or "net10.0"))
            throw new ConfigurationException("Unsupported selected targetFramework: " + targetFramework);
        InputCapture.VerifyUnchanged(root, before, cancellationToken);
        return new(projects, new(members, unlisted, external.Order(StringComparer.Ordinal).ToArray(),
            unresolved.Order(StringComparer.Ordinal).ToArray(), unsupported.Distinct().Order(StringComparer.Ordinal).ToArray()),
            before, before.Files.Where(f => f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).Select(f => f.Path).ToArray(),
            entry, targetFramework);
    }

    private static string[] ReadSolution(string root, string path, List<string> unsupported)
    {
        var paths = new List<string>();
        try
        {
            if (Path.GetExtension(path).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                var xml = InputCapture.ReadXml(path);
                if (xml.Root?.Name.LocalName != "Solution") throw new XmlException("Expected Solution element.");
                foreach (var project in xml.Descendants().Where(n => n.Name.LocalName == "Project"))
                {
                    var value = (string?)project.Attribute("Path");
                    if (value is null) unsupported.Add("solution Project has no Path");
                    else paths.Add(value);
                }
            }
            else
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (!line.TrimStart().StartsWith("Project(", StringComparison.Ordinal)) continue;
                    var match = Regex.Match(line, "^Project\\(.*?\\)\\s*=\\s*\"[^\"]*\",\\s*\"([^\"]*)\"",
                        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                    if (!match.Success) { unsupported.Add("unparsed solution project line"); continue; }
                    paths.Add(match.Groups[1].Value);
                }
            }
        }
        catch (XmlException e) { unsupported.Add("solution XML: " + e.Message); }
        foreach (var unsupportedPath in paths.Where(p => Path.GetExtension(p).Equals(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                     Path.GetExtension(p).Equals(".vbproj", StringComparison.OrdinalIgnoreCase)))
            unsupported.Add("solution unsupported project: " + unsupportedPath);
        return paths.Where(p => Path.GetExtension(p).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetRelativePath(root, Path.GetFullPath(p.Replace('\\', Path.DirectorySeparatorChar), Path.GetDirectoryName(path)!)).Replace('\\', '/'))
            .Order(StringComparer.Ordinal).ToArray();
    }

    private static ProjectModel ReadProject(string root, string id, IReadOnlyDictionary<string, string> canonical)
    {
        var path = Path.GetFullPath(id, root);
        var name = Path.GetFileNameWithoutExtension(path);
        var limits = new List<string>();
        XDocument xml;
        try { xml = InputCapture.ReadXml(path); }
        catch (XmlException error) { return new(id, name, name, false, [], null, false, [], false, [], false, ["Invalid project XML: " + error.Message]); }
        var sdkStyle = xml.Root?.Name.LocalName == "Project" && ((string?)xml.Root.Attribute("Sdk") is { Length: > 0 } ||
            xml.Root.Elements().Any(e => e.Name.LocalName == "Sdk"));
        if (!sdkStyle) limits.Add("Only SDK-style C# is supported.");
        var imports = xml.Descendants().Any(n => n.Name.LocalName == "Import");
        if (imports) limits.Add("Explicit Import is not evaluated.");
        var framework = Framework(root, path, xml, limits);
        var assemblyNodes = xml.Descendants().Where(n => n.Name.LocalName == "AssemblyName").ToArray();
        var assemblyName = assemblyNodes.Length == 1 && Literal(assemblyNodes[0]) ? assemblyNodes[0].Value.Trim() : name;
        if (assemblyNodes.Length > 0 && (assemblyNodes.Length != 1 || !Literal(assemblyNodes[0])))
            limits.Add("AssemblyName cannot be resolved as an unconditional literal.");
        foreach (var item in xml.Descendants().Where(n => n.Name.LocalName is
                     "Compile" or "Content" or "None" or "EmbeddedResource" or "AdditionalFiles" or "Analyzer"))
        {
            var include = (string?)item.Attribute("Include");
            if (include is null) continue;
            if (!Unconditional(item) || !LiteralText(include) || include.Contains(';'))
            { limits.Add("Explicit build input contains a condition/expression/list: " + include); continue; }
            var inputPath = Path.GetFullPath(include.Replace('\\', Path.DirectorySeparatorChar), Path.GetDirectoryName(path)!);
            if (!PathSafety.IsUnder(inputPath, root)) limits.Add("Explicit build input is outside TargetRoot: " + include);
            else if (!include.Contains('*') && !include.Contains('?') && !File.Exists(inputPath))
                limits.Add("Explicit build input is missing: " + include);
        }
        var refs = new List<ProjectReferenceFact>();
        var packages = new List<PackageReferenceFact>();
        var referencesComplete = sdkStyle && !imports;
        var packagesComplete = sdkStyle && !imports;
        foreach (var node in xml.Descendants().Where(n => n.Name.LocalName == "ProjectReference"))
        {
            var include = (string?)node.Attribute("Include");
            if (include is null || !Unconditional(node) || !LiteralText(include) || include.IndexOfAny(['*', '?', ';']) >= 0)
            {
                refs.Add(new(include ?? "(Update/unknown)", null, "unsupported"));
                referencesComplete = false;
                limits.Add("Conditional/expression/non-literal ProjectReference.");
                continue;
            }
            var full = Path.GetFullPath(include.Replace('\\', Path.DirectorySeparatorChar), Path.GetDirectoryName(path)!);
            if (!PathSafety.IsUnder(full, root)) { refs.Add(new(include, null, "external")); continue; }
            PathSafety.EnsureNoLinks(full);
            var target = Path.GetRelativePath(root, full).Replace('\\', '/');
            refs.Add(canonical.TryGetValue(target, out var canonicalId) ? new(include, canonicalId, "resolved") : new(include, target, "missing"));
        }
        foreach (var node in xml.Descendants().Where(n => n.Name.LocalName == "PackageReference"))
        {
            var package = (string?)node.Attribute("Include");
            if (package is null || !Unconditional(node) || !LiteralText(package))
            {
                packages.Add(new(package ?? "(Update/unknown)", null, "unsupported")); packagesComplete = false;
                limits.Add("Conditional/expression/non-literal PackageReference."); continue;
            }
            var version = (string?)node.Attribute("Version") ?? node.Elements().FirstOrDefault(n => n.Name.LocalName == "Version")?.Value;
            if (version is null) version = CentralPackageVersion(root, path, package, limits);
            if (version is not null && !LiteralText(version)) { limits.Add("Package version expression is unresolved: " + package); version = null; }
            packages.Add(new(package, version, "declared"));
        }
        foreach (var configPath in Ancestors(root, path).SelectMany(d => new[] { Path.Combine(d, "Directory.Build.props"), Path.Combine(d, "Directory.Build.targets") }).Where(File.Exists))
        {
            XDocument settings;
            try { settings = InputCapture.ReadXml(configPath); } catch (XmlException) { limits.Add("Invalid ancestor build XML."); referencesComplete = packagesComplete = false; continue; }
            if (settings.Descendants().Any(n => n.Name.LocalName is "ProjectReference" or "PackageReference" or "Import"))
            { limits.Add("Ancestor build items/imports are not evaluated."); referencesComplete = packagesComplete = false; }
        }
        return new(id, name, assemblyName, sdkStyle, framework.Values, framework.Source, framework.Complete && sdkStyle && !imports,
            refs.ToArray(), referencesComplete, packages.ToArray(), packagesComplete, limits.Distinct().ToArray());
    }

    private static (string[] Values, string? Source, bool Complete) Framework(string root, string path, XDocument project, List<string> limits)
    {
        var nodes = project.Descendants().Where(n => n.Name.LocalName is "TargetFramework" or "TargetFrameworks").ToArray();
        var source = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (nodes.Length == 0)
        {
            foreach (var directory in Ancestors(root, path))
            {
                var props = Path.Combine(directory, "Directory.Build.props");
                if (!File.Exists(props)) continue;
                source = Path.GetRelativePath(root, props).Replace('\\', '/');
                try
                {
                    var settings = InputCapture.ReadXml(props);
                    if (settings.Descendants().Any(n => n.Name.LocalName == "Import"))
                    { limits.Add("Framework props import unsupported."); return ([], source, false); }
                    nodes = settings.Descendants().Where(n => n.Name.LocalName is "TargetFramework" or "TargetFrameworks").ToArray();
                }
                catch (XmlException) { limits.Add("Invalid framework props XML."); return ([], source, false); }
                break;
            }
        }
        if (nodes.Length != 1 || !Literal(nodes[0]))
        { limits.Add("Missing, conditional or ambiguous TFM declaration."); return ([], source, false); }
        var values = nodes[0].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 0) { limits.Add("Empty TFM declaration."); return ([], source, false); }
        if (values.Any(v => v is not ("net8.0" or "net9.0" or "net10.0"))) limits.Add("TFM outside net8.0/net9.0/net10.0 support.");
        return (values, source, true);
    }

    private static string? CentralPackageVersion(string root, string path, string id, List<string> limits)
    {
        foreach (var directory in Ancestors(root, path))
        {
            var props = Path.Combine(directory, "Directory.Packages.props");
            if (!File.Exists(props)) continue;
            try
            {
                var xml = InputCapture.ReadXml(props);
                if (xml.Descendants().Any(n => n.Name.LocalName == "Import"))
                { limits.Add("Central package imports unsupported."); return null; }
                var entries = xml.Descendants().Where(n => n.Name.LocalName == "PackageVersion" &&
                    string.Equals((string?)n.Attribute("Include"), id, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (entries.Length != 1 || !Unconditional(entries[0])) { limits.Add("Central package version missing/conditional/ambiguous: " + id); return null; }
                var version = (string?)entries[0].Attribute("Version") ?? entries[0].Elements().FirstOrDefault(n => n.Name.LocalName == "Version")?.Value;
                return version is not null && LiteralText(version) ? version : null;
            }
            catch (XmlException) { limits.Add("Central package XML is invalid."); return null; }
        }
        return null;
    }

    private static IEnumerable<string> Ancestors(string root, string project)
    {
        var directory = Path.GetDirectoryName(project);
        while (directory is not null && PathSafety.IsUnder(directory, root))
        {
            yield return directory;
            if (directory.Equals(root, PathSafety.Comparison)) yield break;
            directory = Path.GetDirectoryName(directory);
        }
    }
    private static bool Literal(XElement node) => Unconditional(node) && LiteralText(node.Value);
    private static bool LiteralText(string value) => !string.IsNullOrWhiteSpace(value) && !value.Contains("$(", StringComparison.Ordinal) &&
        !value.Contains("@(", StringComparison.Ordinal) && !value.Contains("%(", StringComparison.Ordinal);
    private static bool Unconditional(XElement node) => node.AncestorsAndSelf().All(n => n.Attribute("Condition") is null);
}
