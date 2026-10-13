using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.IntegrationTests;

public sealed class EvidenceJobTests
{
    [Fact]
    public async Task RealOfflineBuildProducesUnapprovedPersistentMaterialsWithoutRunningTarget()
    {
        if (!OperatingSystem.IsWindows()) return; // New external build UI is Windows-only; ordinary CLI tests remain cross-platform.
        var root = Path.Combine(Path.GetTempPath(), "archsift-evidence-job-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source"); Directory.CreateDirectory(Path.Combine(source, "Lib")); Directory.CreateDirectory(Path.Combine(source, "App"));
        var flag = Path.Combine(root, "target-must-not-execute.txt");
        File.WriteAllText(Path.Combine(source, "Lib", "Lib.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ArchSiftPreviewProbe>known</ArchSiftPreviewProbe><Description>$(ArchSiftPreviewProbe)</Description></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(source, "Lib", "Types.cs"), "namespace Lib; public class Value {} public static class Marker { [System.Runtime.CompilerServices.ModuleInitializer] public static void Start() => System.IO.File.WriteAllText(@\"" + flag + "\", \"BAD\"); }");
        File.WriteAllText(Path.Combine(source, "App", "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"../Lib/Lib.csproj\" /></ItemGroup></Project>");
        File.WriteAllText(Path.Combine(source, "App", "Types.cs"), "namespace App; public class Value { public Lib.Value Item = new(); }");
        try
        {
            var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = source }, Build = new() { TargetFramework = "net10.0", Configuration = "Debug" }, Output = new() { Directory = Path.Combine(root, "reports") } };
            var request = new EvidenceJobRequest(new(Path.Combine(root, "ArchSift"), null, "fixture", Guid.NewGuid().ToString("N"), []),
                SafeProcess.Dotnet, Environment.GetEnvironmentVariable("ARCHSIFT_TEST_LOCAL_FEED") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
                null, [], [], true, "synthetic fixture test", "Reviewed two plain SDK projects with no custom tasks or generators.");
            var service = new EvidenceJobService([]); var preview = service.Preview(config, request);
            Assert.Equal(2, preview.ProjectCount); Assert.False(Directory.Exists(preview.Storage.RunDirectory));
            Assert.Equal("Lib/Lib.csproj", preview.Projects[0]);
            var initial = InputCapture.Capture(source); var updates = new List<EvidenceJobView>();
            var record = await service.BuildAsync(config, request, preview.PreviewSha256, updates.Add);
            Assert.Equal(preview.Commands.Length, record.Executions.Length);
            for (var index = 0; index < preview.Commands.Length; index++)
            {
                Assert.Equal(preview.Commands[index].Stage, record.Executions[index].Stage switch
                {
                    "inspecting-sdk" => "sdk", "restoring" => "restore", "building" => "build", "evaluating" => "evaluate", var stage => stage
                });
                Assert.Equal(preview.Commands[index].Arguments, record.Executions[index].Arguments);
            }
            Assert.Contains(preview.Commands, command => command.Stage == "evaluate" && command.Arguments.Any(argument => argument.Contains("ArchSiftPreviewProbe", StringComparison.Ordinal)));
            Assert.True(record.Stage == "review-required", JsonSerializer.Serialize(record, JsonContract.Options));
            Assert.Equal(2, record.CompletedProjects); Assert.Empty(record.Unknowns);
            Assert.Equal(2, record.EvaluatedProjects);
            Assert.True(File.Exists(Path.Combine(record.Preview.Storage.RunDirectory, "job.json")));
            Assert.False(File.Exists(preview.Storage.ManifestPath));
            Assert.Equal(initial.Sha256, InputCapture.Capture(source).Sha256); Assert.False(File.Exists(flag));
            Assert.All(record.Executions, execution => { Assert.Equal(0, execution.ExitCode); Assert.True(execution.HostStateEqual); Assert.True(execution.OwnedTreeStopped); Assert.NotNull(execution.ProcessId); });
            Assert.Contains(updates, update => update.Stage == "building" && update.Executions.Any(e => e.ExitCode is null && e.ProcessId is not null));
            using var template = JsonDocument.Parse(File.ReadAllBytes(record.ReviewTemplatePath!));
            Assert.False(template.RootElement.GetProperty("approved").GetBoolean()); Assert.False(template.RootElement.GetProperty("inputsComplete").GetBoolean());
            Assert.Equal(2, template.RootElement.GetProperty("projectContexts").GetArrayLength());
            using var capture = JsonDocument.Parse(File.ReadAllBytes(record.CapturePath!)); SchemaValidation.Validate(capture.RootElement, "evidence-capture");
            SchemaValidation.Validate(template.RootElement, "external-build-review-v2");
            await Assert.ThrowsAsync<ConfigurationException>(() => service.BuildAsync(config, request, preview.PreviewSha256));
            var scope = EvidenceReviewService.Scope(record.Preview.Storage.RunDirectory, source);
            var approval = new EvidenceReviewRequest(scope.JobDirectory, scope.ScopeSha256, "synthetic fixture reviewer", "delegated-agent", "owned fixture test harness",
                "Reviewed actual SDK build exits, compiler-input audit, snapshot identities and per-project resolutions.",
                ["SDK-generated assembly/TFM inputs are read by actual Csc commands; restore assets precede build."],
                "Plain reviewed SDK fixture; no custom tasks/generators beyond captured SDK logic.", ["Portable synthetic type evidence; no application/runtime certification."], true);
            Assert.Throws<ConfigurationException>(() => EvidenceReviewService.Approve(approval with { Reason = "" }, source));
            var approved = EvidenceReviewService.Approve(approval, source);
            Assert.True(approved.Stage == "validated", JsonSerializer.Serialize(approved, JsonContract.Options));
            Assert.True(approved.Validation!.SourceBound); Assert.Equal(2, approved.Validation.ContextCount);
            Assert.False(File.Exists(flag));
            Assert.Throws<ConfigurationException>(() => EvidenceReviewService.Approve(approval, source));
            var exported = EvidenceReviewService.ExportConfiguration(scope.JobDirectory, source);
            Assert.True(File.Exists(exported.ConfigurationCopyPath));
            var bound = ConfigLoader.Load(exported.ConfigurationCopyPath!); Assert.Equal(approved.ManifestPath, bound.Build.AssemblyManifest);
            var recovered = EvidenceLifecycle.Recover(scope.JobDirectory, source, false); Assert.False(recovered.AutomaticRestart); Assert.False(recovered.CurrentSourceVerified);
            Assert.True(EvidenceLifecycle.Revalidate(scope.JobDirectory, source).SourceBound);
            Directory.CreateDirectory(config.Output.Directory);
            var archive = EvidenceLifecycle.PlanArchive(new(scope.JobDirectory, Path.Combine(root, "archive"), []), config, [], false);
            var copied = EvidenceLifecycle.Archive(archive, config, [], false); Assert.True(copied.OriginalRetained); Assert.False(copied.DirectlyPortable);
            Assert.True(File.Exists(approved.ManifestPath)); Assert.True(File.Exists(copied.ReceiptPath));
            var activeArchive = EvidenceLifecycle.PlanArchive(new(scope.JobDirectory, Path.Combine(root, "active-archive"), []), config, [], true);
            Assert.NotEmpty(activeArchive.Blockers);
            Assert.Throws<ConfigurationException>(() => EvidenceLifecycle.Archive(activeArchive, config, [], true));
            var rawTrace = Path.Combine(scope.JobDirectory, "unexpected.binlog");
            File.WriteAllBytes(rawTrace, [0, 1, 2, 3]);
            var rawBlocked = EvidenceLifecycle.PlanCleanup(new(scope.JobDirectory, copied.ArchiveDirectory, []), config, [], false);
            Assert.Contains(rawBlocked.Blockers, reason => reason.Contains("Raw binary build traces", StringComparison.Ordinal));
            File.Delete(rawTrace);
            var unrelated = Path.Combine(scope.JobDirectory, "user-note.txt"); File.WriteAllText(unrelated, "retain me");
            File.WriteAllText(Path.Combine(config.Output.Directory, "reference.json"), JsonSerializer.Serialize(new { assemblyManifest = approved.ManifestPath }));
            var referenced = EvidenceLifecycle.PlanCleanup(new(scope.JobDirectory, copied.ArchiveDirectory, [config.Output.Directory]), config, [], false);
            Assert.NotEmpty(referenced.References); Assert.NotEmpty(referenced.Blockers);
            Assert.Throws<ConfigurationException>(() => EvidenceLifecycle.CleanupArchived(referenced, config, [], false));
            Assert.True(File.Exists(approved.ManifestPath));
            File.Delete(Path.Combine(config.Output.Directory, "reference.json"));
            var extraReferences = Path.Combine(root, "extra-references"); Directory.CreateDirectory(extraReferences);
            var cleanPreview = EvidenceLifecycle.PlanCleanup(new(scope.JobDirectory, copied.ArchiveDirectory, [extraReferences]), config, [], false);
            Assert.Empty(cleanPreview.Blockers); Assert.NotEmpty(cleanPreview.SelectedOwnedFiles);
            Assert.Contains("user-note.txt", cleanPreview.RetainedUnknownFiles);
            var lateReference = Path.Combine(extraReferences, "late.json");
            File.WriteAllText(lateReference, JsonSerializer.Serialize(new { manifest = approved.ManifestPath }));
            Assert.Throws<ConfigurationException>(() => EvidenceLifecycle.CleanupArchived(cleanPreview, config, [], false));
            Assert.True(File.Exists(approved.ManifestPath));
            File.Delete(lateReference);
            var receipt = EvidenceLifecycle.CleanupArchived(cleanPreview, config, [], false);
            Assert.True(File.Exists(receipt)); Assert.False(File.Exists(approved.ManifestPath));
            Assert.True(Directory.Exists(scope.JobDirectory));
            Assert.Equal("retain me", File.ReadAllText(unrelated));
            Assert.All(cleanPreview.RetainedUnknownFiles, relative => Assert.True(File.Exists(PathSafety.Under(scope.JobDirectory, relative))));
        }
        finally { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OwnedGroupStopsDetachedChildAfterParentExitAndKeepsCancellationOutput()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "archsift-owned-evidence-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        try
        {
            var script = "$child=Start-Process -FilePath '" + shell.Replace("'", "''", StringComparison.Ordinal) + "' -ArgumentList @('-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 30') -WindowStyle Hidden -PassThru; Write-Output $child.Id";
            var result = await SafeProcess.RunAsync(shell, root, ["-NoProfile", "-NonInteractive", "-Command", script], Path.Combine(root, "home"), null, CancellationToken.None, ownTree: true);
            Assert.True(result.OwnedTreeStopped); Assert.Equal(0, result.ExitCode);
            var childId = int.Parse(result.Output.Trim(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Throws<ArgumentException>(() => System.Diagnostics.Process.GetProcessById(childId));
            using var cancel = new CancellationTokenSource(); ProcessResult? partial = null;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SafeProcess.RunAsync(shell, root,
                ["-NoProfile", "-NonInteractive", "-Command", "Write-Output 'Started'; Start-Sleep -Seconds 30"], Path.Combine(root, "cancel-home"), null, cancel.Token,
                ownTree: true, processStarted: _ => cancel.CancelAfter(700), failureOutput: value => partial = value));
            Assert.NotNull(partial); Assert.True(partial.OwnedTreeStopped); Assert.Contains("Started", partial.Output);
        }
        finally { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ChangedPreviewCannotStartOrCreateAnEvidenceRun()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "archsift-stale-preview-" + Guid.NewGuid().ToString("N")); var source = Path.Combine(root, "source"); Directory.CreateDirectory(source);
        try
        {
            var project = Path.Combine(source, "A.csproj"); File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var config = new RunConfiguration { SchemaVersion = 1, Target = new() { Root = source }, Build = new() { TargetFramework = "net10.0" }, Output = new() { Directory = Path.Combine(root, "reports") } };
            var request = new EvidenceJobRequest(new(Path.Combine(root, "ArchSift"), null, "fixture", "run", []), SafeProcess.Dotnet,
                Environment.GetEnvironmentVariable("ARCHSIFT_TEST_LOCAL_FEED") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"), null, [], [], true, "fixture", "Reviewed fixture");
            var service = new EvidenceJobService([]); var preview = service.Preview(config, request); File.AppendAllText(project, "<!--changed-->");
            await Assert.ThrowsAsync<ConfigurationException>(() => service.BuildAsync(config, request, preview.PreviewSha256));
            Assert.False(Directory.Exists(preview.Storage.RunDirectory));
        }
        finally { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); Directory.Delete(root, true); }
    }

    [Fact]
    public void LogRedactionDoesNotPersistSensitiveAssignments()
    {
        var root = Path.Combine(Path.GetTempPath(), "archsift-redaction-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "log.txt"); EvidenceArtifactStore.Log(file,
                "MY_API_SECRET = secret-value\nPATH = raw-path\nCompiler command is reviewable\nmsbuild -p:Password=hidden-value\n");
            var text = File.ReadAllText(file); Assert.DoesNotContain("secret-value", text); Assert.DoesNotContain("raw-path", text);
            Assert.DoesNotContain("hidden-value", text); Assert.Contains("Compiler command", text);
        }
        finally { Assert.True(PathSafety.IsUnder(root, Path.GetTempPath())); Directory.Delete(root, true); }
    }
}
