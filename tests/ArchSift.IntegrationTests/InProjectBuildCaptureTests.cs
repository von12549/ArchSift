using ArchSift.Contracts;
using ArchSift.Core;
using ArchSift.InProject.Runtime;

namespace ArchSift.IntegrationTests;

public sealed class InProjectBuildCaptureTests
{
    [Fact]
    public void CompilerCapture_ExpandsResponseFileAndRejectsItsLoss()
    {
        // The shipped InProject compiler capture parses Windows x64 MSBuild commands.
        if (!OperatingSystem.IsWindows()) return;
        var lab = Environment.GetEnvironmentVariable("ARCHSIFT_TEST_LAB_ROOT") ??
            Path.Combine(Path.GetTempPath(), "ArchSift-InProject-fixtures");
        var root = Path.Combine(lab, "ip02-response-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var compiler = Path.Combine(root, "csc.exe");
        var reference = Path.Combine(root, "Reference.dll");
        var source = Path.Combine(root, "Input.cs");
        var output = Path.Combine(root, "Output.dll");
        var response = Path.Combine(root, "inputs.rsp");
        var generated = Path.Combine(root, "generated");
        var generatedSource = Path.Combine(generated, "Generator", "Emitted.cs");
        File.WriteAllText(compiler, "synthetic compiler identity");
        File.WriteAllText(reference, "synthetic selected reference");
        File.WriteAllText(source, "internal class Input { }");
        File.WriteAllText(output, "synthetic compiler output");
        Directory.CreateDirectory(Path.GetDirectoryName(generatedSource)!);
        File.WriteAllText(generatedSource, "internal class Emitted { }");
        File.WriteAllText(response,
            "/out:Output.dll /reference:Reference.dll /generatedfilesout:generated Input.cs");
        var log = compiler + " /noconfig @inputs.rsp (TaskId:7)";

        var captured = InProjectCompilerCaptures.Parse(log, root);
        Assert.Equal(LocalPackage.HashFile(response), Assert.Single(captured.ResponseFiles).Sha256);
        Assert.Equal(LocalPackage.HashFile(source), Assert.Single(captured.Sources).Sha256);
        Assert.Equal(LocalPackage.HashFile(reference), Assert.Single(captured.References).Sha256);
        Assert.Equal(LocalPackage.HashFile(output), captured.IntermediateOutputSha256);
        Assert.Equal(generated, captured.GeneratedFilesOutputPath);
        Assert.Equal(LocalPackage.HashFile(generatedSource), Assert.Single(captured.GeneratedFiles).Sha256);

        File.Delete(generatedSource);
        Directory.Delete(Path.GetDirectoryName(generatedSource)!);
        Directory.Delete(generated);
        Assert.Throws<ConfigurationException>(() => InProjectCompilerCaptures.Parse(log, root));

        File.Delete(response);
        Assert.Throws<ConfigurationException>(() => InProjectCompilerCaptures.Parse(log, root));
    }

    [Fact]
    public void BindingVerifier_RejectsCaptureChangedAfterProducerWrite()
    {
        var lab = Environment.GetEnvironmentVariable("ARCHSIFT_TEST_LAB_ROOT") ??
            Path.Combine(Path.GetTempPath(), "ArchSift-InProject-fixtures");
        var report = Path.Combine(lab, "ip02-tamper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(report);
        File.WriteAllText(Path.Combine(report, "daily-binding.json"), "{}\n");
        var hash = new string('a', 64);
        var binding = new InProjectDailyBinding(1, "ArchSift.InProject", "run", "worktree",
            "Product.slnx", "Debug", hash, hash, hash, "10.0.303",
            new("succeeded", ["local"], hash), [], hash, hash);
        var snapshot = new InProjectSnapshot("run", report, report, report, new InputIdentity(hash, []));

        var verdict = InProjectBindingVerifier.Verify(null!, null!, snapshot, binding, []);

        Assert.False(verdict.SourceBound);
        Assert.Contains(verdict.Limitations, limitation => limitation.Contains("Persisted daily capture", StringComparison.Ordinal));
    }
}
