using System.Xml.Linq;
using System.Text.RegularExpressions;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record InProjectBuildContext(ProjectModel Project, string TargetFramework, string Configuration);

public sealed record InProjectBuildPlan(ProjectSnapshot Snapshot, InProjectBuildContext[] Contexts);

public static class InProjectBuildPlanner
{
    public static InProjectBuildPlan Create(InProjectLoadedConfiguration config, CancellationToken token = default)
    {
        var entry = Path.GetRelativePath(config.Root, config.SolutionPath).Replace('\\', '/');
        var snapshot = ProjectDiscovery.Discover(config.Root, entry, "net10.0", token);
        if (snapshot.Scope.IncludedProjects.Length == 0 || snapshot.Scope.UnresolvedReferences.Length > 0 ||
            snapshot.Scope.ExternalReferences.Length > 0)
            throw new ConfigurationException("Solution project scope or reference closure is incomplete.");
        if (snapshot.InputIdentity.Files.Any(file => file.Path.StartsWith(".archsift/", StringComparison.OrdinalIgnoreCase)))
            throw new ConfigurationException("Business project explicitly references an InProject tool file.");
        ValidateAncestorInputs(config.Root);
        ValidateBuildFiles(config.Root, snapshot.InputIdentity);
        ValidateSolutionConfiguration(config.SolutionPath, config.Root, config.Configuration,
            snapshot.Scope.IncludedProjects);
        var projects = snapshot.Projects.ToDictionary(project => project.Id, StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<ProjectModel>();
        void Visit(string id)
        {
            token.ThrowIfCancellationRequested();
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new ConfigurationException("Project reference cycle in required closure.");
            if (!projects.TryGetValue(id, out var project)) throw new ConfigurationException("Missing referenced project: " + id);
            // Conditional properties are reviewed separately and then checked against the
            // actual MSBuild evaluation and compiler invocation for each fresh snapshot.
            var unresolved = project.Limitations.Where(limit => limit !=
                "Build context contains unevaluated conditions/expressions.").ToArray();
            if (!project.Supported || !project.FrameworkComplete || !project.ReferencesComplete ||
                !project.PackagesComplete || unresolved.Length > 0 || project.Frameworks.Length == 0 ||
                project.Frameworks.Any(tfm => tfm is not ("net8.0" or "net9.0" or "net10.0")))
                throw new ConfigurationException("Required project has unsupported build semantics: " + id +
                    " " + string.Join("; ", project.Limitations));
            foreach (var reference in project.References)
            {
                if (reference.Status != "resolved" || reference.TargetId is null)
                    throw new ConfigurationException("Required project has an unresolved/external ProjectReference: " + id);
                Visit(reference.TargetId);
            }
            visiting.Remove(id);
            visited.Add(id);
            order.Add(project);
        }
        foreach (var member in snapshot.Scope.IncludedProjects) Visit(member);
        var explicitProjects = config.Contexts.Select(context => context.Project).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (explicitProjects.Except(visited, StringComparer.OrdinalIgnoreCase).Any())
            throw new ConfigurationException("Selected context is outside the required solution closure.");
        var contexts = new List<InProjectBuildContext>();
        foreach (var project in order)
        {
            var selected = config.Contexts.Where(context => context.Project.Equals(project.Id, StringComparison.OrdinalIgnoreCase))
                .Select(context => context.TargetFramework).ToArray();
            if (selected.Length == 0)
            {
                if (project.Frameworks.Length != 1)
                    throw new ConfigurationException("Multi-target project requires explicit context selection: " + project.Id);
                selected = [project.Frameworks[0]];
            }
            foreach (var tfm in selected)
            {
                if (!project.Frameworks.Contains(tfm, StringComparer.Ordinal))
                    throw new ConfigurationException("Selected TFM is not declared by project: " + project.Id);
                contexts.Add(new(project, tfm, config.Configuration));
            }
        }
        return new(snapshot, contexts.ToArray());
    }

    private static void ValidateAncestorInputs(string root)
    {
        for (var directory = Directory.GetParent(root); directory is not null; directory = directory.Parent)
        {
            foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets",
                         "Directory.Packages.props", "global.json", "NuGet.Config", "nuget.config" })
            {
                var path = Path.Combine(directory.FullName, name);
                if (File.Exists(path))
                    throw new ConfigurationException("Ancestor build input cannot be relocated into fresh snapshot: " + path);
            }
        }
    }

    private static void ValidateBuildFiles(string root, InputIdentity identity)
    {
        foreach (var file in identity.Files.Where(file => Path.GetExtension(file.Path) is ".csproj" or ".props" or ".targets"))
        {
            var path = Path.GetFullPath(file.Path.Replace('/', Path.DirectorySeparatorChar), root);
            var document = InputCapture.ReadXml(path);
            if (document.Descendants().Any(node => node.Name.LocalName is "UsingTask" or "Target" or "Import"))
                throw new ConfigurationException("Custom target/task/import needs reviewed input evidence: " + file.Path);
            if (document.Descendants().Any(node => node.Name.LocalName is "Analyzer" or "AdditionalFiles" or
                    "SourceGenerator" or "CompilerVisibleProperty"))
                throw new ConfigurationException("Explicit analyzer/generator input needs reviewed evidence: " + file.Path);
            if (document.Descendants().Any(node => node.Name.LocalName is "OutputPath" or "BaseOutputPath" or
                    "BaseIntermediateOutputPath" or "IntermediateOutputPath" or "MSBuildProjectExtensionsPath" or
                    "OutDir" or "ArtifactsPath" or "RestorePackagesPath" or "NuGetPackageRoot" or
                    "CompilerGeneratedFilesOutputPath"))
                throw new ConfigurationException("Custom build output path cannot be safely relocated: " + file.Path);
            InProjectBuildInputReview.Validate(document, file.Path);
        }
    }

    private static void ValidateSolutionConfiguration(string path, string root, string configuration,
        string[] includedProjects)
    {
        if (Path.GetExtension(path).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            var document = InputCapture.ReadXml(path);
            if (document.Descendants().Any(node => node.Name.LocalName is "Configuration" or "Platform" ||
                    node.Name.LocalName == "Project" && node.Attributes().Any(attribute => attribute.Name.LocalName != "Path")))
                throw new ConfigurationException("Custom solution configuration mapping needs reviewed evidence.");
            return;
        }
        var content = File.ReadAllText(path);
        var selection = configuration + "|Any CPU";
        if (!Regex.IsMatch(content, @"(?m)^\s*" + Regex.Escape(selection) + @"\s*=\s*" +
                Regex.Escape(selection) + @"\s*$", RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(2)))
            throw new ConfigurationException("Selected solution Any CPU configuration is absent.");
        var projectLines = Regex.Matches(content,
            "(?m)^Project\\(\"[^\"]+\"\\)\\s*=\\s*\"[^\"]+\",\\s*\"(?<path>[^\"]+\\.csproj)\",\\s*\"\\{(?<id>[0-9A-Fa-f-]{36})\\}\"",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        var mapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match project in projectLines)
        {
            var projectPath = Path.GetFullPath(project.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar),
                Path.GetDirectoryName(path)!);
            if (!PathSafety.IsUnder(projectPath, root))
                throw new ConfigurationException("Solution project escapes worktree.");
            mapped.Add(Path.GetRelativePath(root, projectPath).Replace('\\', '/'));
            var id = project.Groups["id"].Value;
            foreach (var suffix in new[] { "ActiveCfg", "Build.0" })
            {
                var prefix = "{" + id + "}." + selection + "." + suffix;
                var entries = content.Split('\n', StringSplitOptions.TrimEntries)
                    .Where(line => line.StartsWith(prefix + " =", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (entries.Length != 1 || !entries[0].EndsWith("= " + selection, StringComparison.OrdinalIgnoreCase))
                    throw new ConfigurationException("Solution project has an unsupported configuration mapping: " +
                        project.Groups["path"].Value);
            }
        }
        if (projectLines.Count == 0 || mapped.Count != projectLines.Count ||
            !mapped.SetEquals(includedProjects))
            throw new ConfigurationException("Solution project mapping differs from discovered required closure.");
    }
}
