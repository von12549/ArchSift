using System.Text;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

public sealed class InProjectBuildProcess(InProjectSnapshot snapshot, InProjectHostState baseline,
    string packageCache, CancellationToken cancellation)
{
    private readonly string cliHome = Path.Combine(snapshot.RunDirectory, "cli-home");
    private int step;

    public async Task<string> DotnetAsync(string name, string workingDirectory, string[] arguments)
        => (await DotnetResultAsync(name, workingDirectory, arguments)).Output;

    public async Task<ProcessResult> DotnetResultAsync(string name, string workingDirectory,
        string[] arguments, IReadOnlyDictionary<string, string>? childEnvironment = null,
        bool allowNonzero = false)
    {
        cancellation.ThrowIfCancellationRequested();
        var prefix = (++step).ToString("D3") + "-" + name;
        ProcessResult? failure = null;
        ProcessResult result;
        try
        {
            var environment = new Dictionary<string, string> { ["NUGET_PACKAGES"] = packageCache };
            if (childEnvironment is not null)
                foreach (var item in childEnvironment) environment[item.Key] = item.Value;
            result = await SafeProcess.RunAsync(SafeProcess.Dotnet, workingDirectory, arguments,
                cliHome, null, cancellation, environment,
                timeoutLimit: TimeSpan.FromMinutes(5), ownTree: true,
                failureOutput: captured => failure = captured);
        }
        catch
        {
            if (failure is not null) Save(prefix, failure);
            if (InProjectHostIdentity.Capture().Sha256 != baseline.Sha256)
                throw new InvalidOperationException("Host environment/Profile drift detected; build evidence preserved.");
            throw;
        }
        var actual = failure ?? result;
        Save(prefix, actual);
        if (InProjectHostIdentity.Capture().Sha256 != baseline.Sha256)
            throw new InvalidOperationException("Host environment/Profile drift detected; build evidence preserved.");
        if (actual.Output.Contains("[output truncated]", StringComparison.Ordinal) ||
            actual.Error.Contains("[output truncated]", StringComparison.Ordinal))
            throw new ConfigurationException("Build diagnostic stream was truncated; compiler inputs cannot be verified.");
        if (actual.ExitCode != 0 && !allowNonzero)
            throw new IOException(name + " exited " + actual.ExitCode + "; inspect preserved run logs.");
        return actual;
    }

    private void Save(string prefix, ProcessResult result)
    {
        File.WriteAllText(Path.Combine(snapshot.ReportDirectory, prefix + ".stdout.log"), result.Output, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(snapshot.ReportDirectory, prefix + ".stderr.log"), result.Error, new UTF8Encoding(false));
    }
}
