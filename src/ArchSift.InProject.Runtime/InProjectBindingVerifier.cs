using System.Reflection;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public static class InProjectBindingVerifier
{
    public static InProjectBindingVerdict Verify(InProjectLoadedConfiguration config, InProjectBuildPlan plan,
        InProjectSnapshot snapshot, InProjectDailyBinding binding,
        IReadOnlyList<(InProjectEvaluation Evaluation, InProjectCompilerCapture Compiler)> observed)
    {
        var limitations = new List<string>();
        var capturePath = Path.Combine(snapshot.ReportDirectory, "daily-binding.json");
        try
        {
            if (!File.Exists(capturePath) || File.ReadAllText(capturePath) !=
                JsonSerializer.Serialize(binding, JsonContract.Options) + "\n")
                throw new ConfigurationException("Persisted daily capture differs from the observed build record.");
            if (binding.SchemaVersion != 1 || binding.Product != "ArchSift.InProject" ||
                binding.RunId != snapshot.RunId || binding.WorktreeId != ContentHash.Text(config.Root) ||
                binding.Configuration != config.Configuration ||
                binding.Solution != Path.GetRelativePath(config.Root, config.SolutionPath).Replace('\\', '/') ||
                binding.InputSha256 != snapshot.InputIdentity.Sha256 ||
                binding.StartInputSha256 != snapshot.InputIdentity.Sha256 ||
                binding.EndInputSha256 != InputCapture.Capture(config.Root).Sha256 ||
                binding.RulesSha256 != config.RulesSha256 || binding.ToolSha256 != config.ToolSha256 ||
                binding.Restore.Status != "succeeded" ||
                !binding.Restore.Sources.SequenceEqual([config.LocalFeed], StringComparer.OrdinalIgnoreCase) ||
                binding.Contexts.Length != plan.Contexts.Length || observed.Count != plan.Contexts.Length)
                throw new ConfigurationException("Daily capture identity, inputs, restore or context count differ from the run.");
            InProjectSnapshots.VerifySnapshot(snapshot.SourceDirectory, snapshot.InputIdentity);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < plan.Contexts.Length; index++)
            {
                var expected = plan.Contexts[index];
                var captured = binding.Contexts[index];
                var (evaluation, compiler) = observed[index];
                var key = captured.Project + "\0" + captured.TargetFramework + "\0" + captured.Configuration;
                if (!keys.Add(key) || captured.Project != expected.Project.Id ||
                    captured.TargetFramework != expected.TargetFramework ||
                    captured.Configuration != expected.Configuration ||
                    captured.CompilerPath != compiler.CompilerPath ||
                    captured.CompilerCommandSha256 != compiler.CommandSha256 ||
                    captured.OutputPath != evaluation.TargetPath ||
                    captured.OutputSha256 != LocalPackage.HashFile(evaluation.TargetPath) ||
                    captured.GlobalPropertiesSha256 != InProjectDailyBuild.GlobalPropertiesSha256(expected,
                        compiler.GeneratedFilesOutputPath!))
                    throw new ConfigurationException("Daily capture context differs from actual build evidence.");
                if (captured.Materials.Length == 0 || captured.References.Length == 0)
                    throw new ConfigurationException("Daily capture omits compiler inputs or references.");
                foreach (var material in captured.Materials)
                {
                    PathSafety.EnsureNoLinks(material.Path);
                    if (!File.Exists(material.Path) || LocalPackage.HashFile(material.Path) != material.Sha256)
                        throw new ConfigurationException("Daily compiler/import material changed or is missing.");
                    if (material.Kind is "source" or "generated-source" &&
                        !PathSafety.IsUnder(material.Path, snapshot.SourceDirectory))
                        throw new ConfigurationException("Captured source escapes the fresh snapshot.");
                }
                if (compiler.GeneratedFiles.Any(file => !captured.Materials.Any(material =>
                        material.Kind == "generated-source" && material.Path == file.Path &&
                        material.Sha256 == file.Sha256)))
                    throw new ConfigurationException("Daily capture omits compiler-generated source material.");
                foreach (var reference in captured.References)
                {
                    PathSafety.EnsureNoLinks(reference.Path);
                    if (!File.Exists(reference.Path) || LocalPackage.HashFile(reference.Path) != reference.Sha256 ||
                        ManagedIdentity(reference.Path) != reference.AssemblyIdentity)
                        throw new ConfigurationException("Selected compiler/copy-local reference changed.");
                }
                var expectedCompilerRefs = evaluation.References.Select(reference => reference.CompilerPath + "\0" +
                    reference.CompilerSha256 + "\0" + reference.AssemblyIdentity).Order(StringComparer.Ordinal).ToArray();
                var capturedCompilerRefs = captured.References.Where(reference => reference.Role == "compile")
                    .Select(reference => reference.Path + "\0" + reference.Sha256 + "\0" + reference.AssemblyIdentity)
                    .Order(StringComparer.Ordinal).ToArray();
                if (!expectedCompilerRefs.SequenceEqual(capturedCompilerRefs, StringComparer.Ordinal))
                    throw new ConfigurationException("Daily capture differs from evaluated compiler references.");
                InProjectCompilerCaptures.VerifyAgainstEvaluation(compiler, evaluation, snapshot.SourceDirectory,
                    snapshot.InputIdentity.Files.ToDictionary(file => file.Path, file => file.Sha256,
                        StringComparer.OrdinalIgnoreCase), config.PackageReview,
                    Path.Combine(config.InstallationRoot, "state", "cache", "packages"));
                var closure = new List<string>();
                var framework = evaluation.References.Select(reference => reference.CompilerPath)
                    .Where(path => path.Replace('\\', '/').Contains("/packs/Microsoft.NETCore.App.Ref/", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                ProjectContextEvidence.ValidateClosure(
                    [evaluation.TargetPath, .. evaluation.References.Select(reference => reference.CompilerPath)],
                    closure, framework, evaluation.TargetPath);
                if (closure.Count != 0) throw new ConfigurationException("Dependency closure changed after capture.");
            }
            var assets = plan.Contexts.Select(context => Path.Combine(snapshot.SourceDirectory,
                    Path.GetDirectoryName(context.Project.Id.Replace('/', Path.DirectorySeparatorChar)) ?? "",
                    "obj", "project.assets.json"))
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)
                .Select(LocalPackage.HashFile);
            if (binding.Restore.AssetsSha256 != ContentHash.Text(string.Join("\n", assets)))
                throw new ConfigurationException("Restored assets differ from captured selection.");
            var current = InProjectConfiguration.Load(config.Root);
            if (current.ProjectSha256 != config.ProjectSha256 || current.RulesSha256 != config.RulesSha256 ||
                current.ToolSha256 != config.ToolSha256)
                throw new ConfigurationException("Current configuration, rules or tool differs from build start.");
        }
        catch (Exception error) when (error is ConfigurationException or IOException or BadImageFormatException or
            InvalidDataException or KeyNotFoundException or InvalidOperationException)
        {
            limitations.Add(error.Message);
        }
        var captureSha = File.Exists(capturePath) ? LocalPackage.HashFile(capturePath) : "";
        return new(1, "ArchSift.InProject", snapshot.RunId, captureSha, "1", limitations.Count == 0,
            limitations.ToArray());
    }

    private static string ManagedIdentity(string path)
    {
        try { return AssemblyName.GetAssemblyName(path).FullName!; }
        catch (BadImageFormatException) { return "native/" + Path.GetFileName(path); }
    }
}
