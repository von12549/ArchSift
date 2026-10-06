using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.ArchUnit;

public sealed class AssemblyWorkerClient(string cliAssembly)
{
    public async Task<AssemblyEvaluation> EvaluateAsync(AssemblyInputs inputs, RuleBundle bundle, string outputRoot, CancellationToken token)
    {
        PathSafety.EnsureNoLinks(outputRoot);
        var home = Path.Combine(outputRoot, "workers", Guid.NewGuid().ToString("N"), "cli-home");
        var request = JsonSerializer.Serialize(new WorkerRequest(inputs, bundle.Documents.Select(d => d.Ruleset).ToArray()), JsonContract.Options);
        if (request.Length > 16 * 1024 * 1024) throw new ConfigurationException("Worker request exceeds 16 MiB.");
        var self = Environment.ProcessPath;
        var useSelf = self is not null && !Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
            System.Reflection.Assembly.GetEntryAssembly()?.Location == cliAssembly;
        var process = await SafeProcess.RunAsync(useSelf ? self! : SafeProcess.Dotnet, Path.GetDirectoryName(cliAssembly)!,
            useSelf ? ["__worker"] : [cliAssembly, "__worker"], home, request, token);
        if (process.ExitCode != 0) throw new IOException("Assembly worker failed: " + process.Error);
        try { return JsonSerializer.Deserialize<AssemblyEvaluation>(process.Output, JsonContract.Options)!; }
        catch (JsonException e) { throw new IOException("Assembly worker output is invalid.", e); }
    }
}
