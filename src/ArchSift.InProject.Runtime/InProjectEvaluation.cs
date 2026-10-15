using System.Reflection;
using System.Text.Json;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record InProjectEvaluatedFile(string Path, string Sha256);
public sealed record InProjectEvaluatedReference(string Path, string CompilerPath,
    string Sha256, string CompilerSha256, string AssemblyIdentity, string SourceTarget);

public sealed record InProjectEvaluation(string ProjectPath, string TargetFramework, string Configuration,
    string TargetPath, InProjectEvaluatedReference[] References,
    InProjectEvaluatedFile[] CopyLocal, InProjectEvaluatedFile[] Compile,
    InProjectEvaluatedFile[] Analyzers, InProjectEvaluatedFile[] AdditionalFiles,
    InProjectEvaluatedFile[] EmbeddedResources, InProjectEvaluatedFile[] Imports,
    string RawSha256);

public static class InProjectEvaluations
{
    public static InProjectEvaluation Parse(string json, string projectPath, string tfm, string configuration)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var properties = root.GetProperty("Properties");
        if (properties.GetProperty("TargetFramework").GetString() != tfm ||
            properties.GetProperty("Configuration").GetString() != configuration)
            throw new ConfigurationException("MSBuild project context differs from selection.");
        var target = FullFileName(properties.GetProperty("TargetPath").GetString());
        var items = root.GetProperty("Items");
        var references = new List<InProjectEvaluatedReference>();
        foreach (var item in items.GetProperty("ReferencePath").EnumerateArray())
        {
            var path = Existing(item.GetProperty("FullPath").GetString());
            var alternative = item.TryGetProperty("ReferenceAssembly", out var referenceAssembly)
                ? referenceAssembly.GetString() : null;
            var compiler = string.IsNullOrWhiteSpace(alternative) ? path : Existing(alternative);
            references.Add(new(path, compiler, LocalPackage.HashFile(path), LocalPackage.HashFile(compiler),
                AssemblyName.GetAssemblyName(compiler).FullName!,
                item.TryGetProperty("ReferenceSourceTarget", out var source) ? source.GetString() ?? "" : ""));
        }
        var imports = (properties.TryGetProperty("MSBuildAllProjects", out var allProjects)
                ? (allProjects.GetString() ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [])
            .Where(File.Exists).Select(Existing).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new InProjectEvaluatedFile(path, LocalPackage.HashFile(path))).ToArray();
        return new(Path.GetFullPath(projectPath), tfm, configuration, target,
            references.ToArray(), ReadFiles(items, "ReferenceCopyLocalPaths"),
            ReadFiles(items, "Compile"), ReadFiles(items, "Analyzer"),
            ReadFiles(items, "AdditionalFiles"), ReadFiles(items, "EmbeddedResource"),
            imports, ContentHash.Text(json));
    }

    public static void VerifyStable(InProjectEvaluation before, InProjectEvaluation after)
    {
        if (before.ProjectPath != after.ProjectPath || before.TargetFramework != after.TargetFramework ||
            before.Configuration != after.Configuration || before.TargetPath != after.TargetPath ||
            !Same(before.References.Select(reference => reference.Path + "\0" + reference.CompilerPath + "\0" +
                    reference.Sha256 + "\0" + reference.CompilerSha256 + "\0" + reference.AssemblyIdentity),
                after.References.Select(reference => reference.Path + "\0" + reference.CompilerPath + "\0" +
                    reference.Sha256 + "\0" + reference.CompilerSha256 + "\0" + reference.AssemblyIdentity)) ||
            !SameFiles(before.CopyLocal, after.CopyLocal) || !SameFiles(before.Compile, after.Compile) ||
            !SameFiles(before.Analyzers, after.Analyzers) || !SameFiles(before.AdditionalFiles, after.AdditionalFiles) ||
            !SameFiles(before.EmbeddedResources, after.EmbeddedResources) || !SameFiles(before.Imports, after.Imports))
            throw new ConfigurationException("MSBuild evaluated inputs or references changed during compilation.");
    }

    private static InProjectEvaluatedFile[] ReadFiles(JsonElement items, string name)
    {
        if (!items.TryGetProperty(name, out var selected)) return [];
        return selected.EnumerateArray().Select(item =>
        {
            var path = Existing(item.GetProperty("FullPath").GetString());
            return new InProjectEvaluatedFile(path, LocalPackage.HashFile(path));
        }).OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string FullFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ConfigurationException("MSBuild evaluation did not yield an absolute path.");
        var full = Path.GetFullPath(path);
        PathSafety.EnsureNoLinks(full);
        return full;
    }

    private static string Existing(string? path)
    {
        var full = FullFileName(path);
        if (!File.Exists(full)) throw new ConfigurationException("Evaluated MSBuild input is missing: " + full);
        return full;
    }

    private static bool SameFiles(InProjectEvaluatedFile[] left, InProjectEvaluatedFile[] right) =>
        Same(left.Select(file => file.Path + "\0" + file.Sha256),
            right.Select(file => file.Path + "\0" + file.Sha256));
    private static bool Same(IEnumerable<string> left, IEnumerable<string> right) =>
        left.Order(StringComparer.Ordinal).SequenceEqual(right.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
