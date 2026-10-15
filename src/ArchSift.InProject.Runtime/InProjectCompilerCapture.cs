using System.Text.RegularExpressions;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed record InProjectCompilerCapture(string CompilerPath, string CompilerSha256,
    string CommandSha256, InProjectEvaluatedFile[] Sources, InProjectEvaluatedFile[] References,
    InProjectEvaluatedFile[] Analyzers, InProjectEvaluatedFile[] AdditionalFiles,
    InProjectEvaluatedFile[] OtherInputs, InProjectEvaluatedFile[] ResponseFiles,
    string IntermediateOutputPath, string IntermediateOutputSha256)
{
    public string? GeneratedFilesOutputPath { get; init; }
    public InProjectEvaluatedFile[] GeneratedFiles { get; init; } = [];
}

public static class InProjectCompilerCaptures
{
    private static readonly HashSet<string> RazorBuildAnalyzers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.AspNetCore.Razor.Utilities.Shared.dll",
        "Microsoft.CodeAnalysis.Razor.Compiler.dll",
        "Microsoft.Extensions.ObjectPool.dll"
    };
    private static readonly Regex CompilerLine = new(@"(?<compiler>[A-Za-z]:\\.*?csc\.(?:exe|dll))\s+(?<args>/noconfig\s.*?)(?:\s+\(TaskId:\d+\))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));
    private static readonly Regex ArgumentTokens = new("(?:[^\\s\"]+|\"[^\"]*\")+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    public static InProjectCompilerCapture Parse(string diagnosticLog, string projectDirectory)
    {
        var commands = diagnosticLog.Split('\n').Select(line => CompilerLine.Match(line.Trim()))
            .Where(match => match.Success).ToArray();
        if (commands.Length != 1)
            throw new ConfigurationException("Exactly one actual C# compiler invocation is required for each build context.");
        var match = commands[0];
        var compiler = Existing(match.Groups["compiler"].Value);
        var arguments = Tokens(match.Groups["args"].Value);
        var responses = new List<InProjectEvaluatedFile>();
        arguments = Expand(arguments, projectDirectory, responses, new(StringComparer.OrdinalIgnoreCase), 0);
        var sources = new List<InProjectEvaluatedFile>();
        var references = new List<InProjectEvaluatedFile>();
        var analyzers = new List<InProjectEvaluatedFile>();
        var additional = new List<InProjectEvaluatedFile>();
        var other = new List<InProjectEvaluatedFile>();
        string? output = null;
        string? generatedDirectory = null;
        foreach (var argument in arguments)
        {
            if (argument.StartsWith("/out:", StringComparison.OrdinalIgnoreCase))
            {
                if (output is not null) throw new ConfigurationException("Duplicate compiler output flag.");
                output = Existing(Resolve(argument[5..], projectDirectory));
            }
            else if (argument.StartsWith("/reference:", StringComparison.OrdinalIgnoreCase) ||
                     argument.StartsWith("/r:", StringComparison.OrdinalIgnoreCase))
            {
                var value = argument[(argument.IndexOf(':') + 1)..];
                var alias = value.LastIndexOf('=');
                if (alias >= 0) value = value[(alias + 1)..];
                references.Add(Fact(value, projectDirectory));
            }
            else if (argument.StartsWith("/analyzer:", StringComparison.OrdinalIgnoreCase))
                analyzers.Add(Fact(argument[10..], projectDirectory));
            else if (argument.StartsWith("/additionalfile:", StringComparison.OrdinalIgnoreCase))
                additional.Add(Fact(argument[16..], projectDirectory));
            else if (argument.StartsWith("/generatedfilesout:", StringComparison.OrdinalIgnoreCase))
            {
                if (generatedDirectory is not null)
                    throw new ConfigurationException("Duplicate generated-files output flag.");
                generatedDirectory = Resolve(argument[19..], projectDirectory);
            }
            else if (argument.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !argument.StartsWith('/'))
                sources.Add(Fact(argument, projectDirectory));
            else
            {
                foreach (var prefix in new[] { "/analyzerconfig:", "/resource:", "/link:", "/keyfile:",
                             "/ruleset:", "/sourcelink:", "/win32res:", "/win32icon:", "/appconfig:" })
                {
                    if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    var value = argument[prefix.Length..];
                    if (prefix is "/resource:" or "/link:") value = value.Split(',')[0];
                    other.Add(Fact(value, projectDirectory));
                    break;
                }
            }
        }
        if (output is null || sources.Count == 0 || references.Count == 0)
            throw new ConfigurationException("Compiler output, sources or references were not observed.");
        return new(compiler, LocalPackage.HashFile(compiler), ContentHash.Text(match.Value),
            sources.ToArray(), references.ToArray(), analyzers.ToArray(), additional.ToArray(),
            other.ToArray(), responses.ToArray(), output, LocalPackage.HashFile(output))
        {
            GeneratedFilesOutputPath = generatedDirectory,
            GeneratedFiles = generatedDirectory is null ? [] : CaptureGenerated(generatedDirectory)
        };
    }

    public static void VerifyAgainstEvaluation(InProjectCompilerCapture compiler, InProjectEvaluation evaluation,
        string snapshotRoot, IReadOnlyDictionary<string, string> originalInputHashes,
        InProjectPackageReview? packageReview = null, string? packageCache = null)
    {
        var evaluatedAnalyzers = evaluation.Analyzers.Select(Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedAnalyzers = compiler.Analyzers.Where(file => evaluatedAnalyzers.Contains(Key(file))).ToArray();
        var buildAnalyzers = compiler.Analyzers.Where(file => !evaluatedAnalyzers.Contains(Key(file))).ToArray();
        if (!Same(compiler.References, evaluation.References.Select(reference =>
                new InProjectEvaluatedFile(reference.CompilerPath, reference.CompilerSha256))) ||
            !Same(selectedAnalyzers, evaluation.Analyzers) ||
            buildAnalyzers.Any(file => !ReviewedRazorBuildAnalyzer(compiler.CompilerPath, file.Path)) ||
            !Same(compiler.AdditionalFiles, evaluation.AdditionalFiles))
            throw new ConfigurationException("Actual compiler references/analyzers differ from MSBuild evaluation.");
        var seenSources = compiler.Sources.Select(source => source.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (evaluation.Compile.Any(source => !seenSources.Contains(source.Path)))
            throw new ConfigurationException("Evaluated compile item was not passed to csc.");
        foreach (var source in compiler.Sources)
        {
            if (!PathSafety.IsUnder(source.Path, snapshotRoot))
            {
                if (packageReview is not null && packageCache is not null &&
                    packageReview.AllowsPackageFile(packageCache, source.Path, source.Sha256)) continue;
                throw new ConfigurationException("Compiler source escapes the fresh snapshot.");
            }
            var relative = Path.GetRelativePath(snapshotRoot, source.Path).Replace('\\', '/');
            if (originalInputHashes.TryGetValue(relative, out var expected))
            {
                if (expected != source.Sha256) throw new ConfigurationException("Compiler source differs from captured worktree input.");
                continue;
            }
            var file = Path.GetFileName(source.Path);
            if (!(packageReview?.AllowsGeneratedSource(relative) == true ||
                relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase) &&
                (file.EndsWith(".AssemblyAttributes.cs", StringComparison.Ordinal) ||
                  file.EndsWith(".AssemblyInfo.cs", StringComparison.Ordinal) ||
                  file.EndsWith(".MvcApplicationPartsAssemblyInfo.cs", StringComparison.Ordinal) ||
                  file.EndsWith(".GlobalUsings.g.cs", StringComparison.Ordinal))))
                throw new ConfigurationException("Unreviewed generated compiler source: " + relative);
        }
        if (LocalPackage.HashFile(compiler.IntermediateOutputPath) != compiler.IntermediateOutputSha256)
            throw new ConfigurationException("Intermediate compiler output changed after capture.");
        var expectedGeneratedDirectory = Path.Combine(Path.GetDirectoryName(evaluation.ProjectPath)!, "obj",
            evaluation.Configuration, evaluation.TargetFramework, "archsift-generated");
        if (!expectedGeneratedDirectory.Equals(compiler.GeneratedFilesOutputPath, PathSafety.Comparison) ||
            !PathSafety.IsUnder(expectedGeneratedDirectory, snapshotRoot) ||
            !Same(compiler.GeneratedFiles, CaptureGenerated(expectedGeneratedDirectory)))
            throw new ConfigurationException("Generated compiler sources differ from the owned build output.");
    }

    private static string[] Expand(string[] tokens, string cwd, List<InProjectEvaluatedFile> files,
        HashSet<string> visiting, int depth)
    {
        if (depth > 4) throw new ConfigurationException("Nested compiler response file limit exceeded.");
        var result = new List<string>();
        foreach (var token in tokens)
        {
            if (!token.StartsWith('@')) { result.Add(token); continue; }
            var path = Existing(Resolve(token[1..], cwd));
            if (!visiting.Add(path)) throw new ConfigurationException("Compiler response file cycle.");
            files.Add(new(path, LocalPackage.HashFile(path)));
            result.AddRange(Expand(Tokens(File.ReadAllText(path)), Path.GetDirectoryName(path)!, files, visiting, depth + 1));
            visiting.Remove(path);
        }
        return result.ToArray();
    }

    private static string[] Tokens(string command) => ArgumentTokens.Matches(command)
        .Select(match => match.Value.Replace("\"", "", StringComparison.Ordinal)).ToArray();

    private static InProjectEvaluatedFile Fact(string value, string cwd)
    {
        var path = Existing(Resolve(value, cwd));
        return new(path, LocalPackage.HashFile(path));
    }

    private static string Resolve(string value, string cwd) => Path.GetFullPath(value, cwd);

    private static string Existing(string path)
    {
        PathSafety.EnsureNoLinks(path);
        if (!File.Exists(path)) throw new ConfigurationException("Actual compiler input/output is missing: " + path);
        return Path.GetFullPath(path);
    }

    private static bool Same(IEnumerable<InProjectEvaluatedFile> left, IEnumerable<InProjectEvaluatedFile> right) =>
        left.Select(file => file.Path + "\0" + file.Sha256).Order(StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(right.Select(file => file.Path + "\0" + file.Sha256).Order(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

    private static InProjectEvaluatedFile[] CaptureGenerated(string directory)
    {
        PathSafety.EnsureNoLinks(directory);
        if (!Directory.Exists(directory))
            throw new ConfigurationException("Compiler generated-file directory is missing.");
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase).Select(path =>
            {
                if (!PathSafety.IsUnder(path, directory) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    throw new ConfigurationException("Unexpected compiler-generated file: " + path);
                PathSafety.EnsureNoLinks(path);
                return new InProjectEvaluatedFile(path, LocalPackage.HashFile(path));
            }).ToArray();
    }

    private static string Key(InProjectEvaluatedFile file) => file.Path + "\0" + file.Sha256;

    public static bool ReviewedRazorBuildAnalyzer(string compilerPath, string analyzerPath)
    {
        var sdkRoot = Directory.GetParent(compilerPath)?.Parent?.Parent?.FullName;
        if (sdkRoot is null) return false;
        var folder = Path.Combine(sdkRoot, "Sdks", "Microsoft.NET.Sdk.Razor", "source-generators");
        return RazorBuildAnalyzers.Contains(Path.GetFileName(analyzerPath)) &&
            Path.GetDirectoryName(analyzerPath)?.Equals(folder, PathSafety.Comparison) == true;
    }
}
