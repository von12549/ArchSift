using System.Text.Json;
using ArchSift.InProject.Runtime;

if (args.Length != 1) throw new ArgumentException("Expected one synthetic checkout root.");
var root = Path.GetFullPath(args[0]);
var buildRoot = Path.Combine(root, ".archsift", "state", "build");
var reportRoot = Path.Combine(root, ".archsift", "reports");
var prior = Directory.EnumerateDirectories(buildRoot).Select(Path.GetFileName)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
using var cancellation = new CancellationTokenSource();
var run = InProjectDailyBuild.RunAsync(root, cancellation.Token);
string? runId = null;
var deadline = DateTime.UtcNow.AddMinutes(2);
while (DateTime.UtcNow < deadline && !run.IsCompleted)
{
    runId = Directory.EnumerateDirectories(buildRoot).Select(Path.GetFileName)
        .FirstOrDefault(name => !prior.Contains(name!) &&
            File.Exists(Path.Combine(buildRoot, name!, "owner.json")));
    if (runId is not null) break;
    await Task.Delay(25);
}
if (runId is null) throw new InvalidOperationException("A cancellable owned run did not begin.");
cancellation.Cancel();
try
{
    await run;
    throw new InvalidOperationException("Cancelled run unexpectedly completed.");
}
catch (OperationCanceledException) { }
var statePath = Path.Combine(reportRoot, runId, "build-state.json");
using var state = JsonDocument.Parse(File.ReadAllBytes(statePath));
if (state.RootElement.GetProperty("state").GetString() != "cancelled" ||
    state.RootElement.GetProperty("sourceBound").GetBoolean())
    throw new InvalidOperationException("Cancelled run has no false terminal state.");
Console.WriteLine(JsonSerializer.Serialize(new { runId, state = "cancelled", sourceBound = false }));
