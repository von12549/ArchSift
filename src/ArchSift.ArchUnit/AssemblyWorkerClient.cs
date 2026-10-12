using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.ArchUnit;

public sealed class AssemblyWorkerClient(string cliAssembly)
{
    public async Task<AssemblyEvaluation> EvaluateAsync(AssemblyInputs inputs, RuleBundle bundle, string outputRoot, CancellationToken token)
    {
        PathSafety.EnsureNoLinks(outputRoot);
        var workspace = PathSafety.Under(outputRoot, "worker-contexts-" + Guid.NewGuid().ToString("N"));
        var home = Path.Combine(workspace, "cli-home");
        var request = JsonSerializer.Serialize(new WorkerRequest(inputs, bundle.Documents.Select(d => d.Ruleset).ToArray())
            { ContextWorkspace = workspace, ParentOwnsContextCleanup = true }, JsonContract.Options);
        if (request.Length > 16 * 1024 * 1024) throw new ConfigurationException("Worker request exceeds 16 MiB.");
        var self = Environment.ProcessPath;
        var useSelf = self is not null && !Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
            System.Reflection.Assembly.GetEntryAssembly()?.Location == cliAssembly;
        if (Directory.Exists(workspace) || File.Exists(workspace)) throw new IOException("Worker lease already exists.");
        Directory.CreateDirectory(workspace);
        try
        {
            var process = await SafeProcess.RunAsync(useSelf ? self! : SafeProcess.Dotnet, Path.GetDirectoryName(cliAssembly)!,
                useSelf ? ["__worker"] : [cliAssembly, "__worker"], home, request, token);
            if (process.ExitCode != 0) throw new IOException("Assembly worker failed: " + process.Error);
            try { return JsonSerializer.Deserialize<AssemblyEvaluation>(process.Output, JsonContract.Options)!; }
            catch (JsonException e) { throw new IOException("Assembly worker output is invalid.", e); }
        }
        finally
        {
            // SafeProcess waits for normal exit and kills/waits on failure or cancellation before returning.
            PathSafety.EnsureNoLinks(workspace);
            if (!PathSafety.IsUnder(workspace, outputRoot)) throw new IOException("Unsafe worker lease cleanup.");
            if (Directory.Exists(workspace)) Directory.Delete(workspace, true);
        }
    }
}
