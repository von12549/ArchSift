using System.Diagnostics;
using ArchSift.Contracts;

namespace ArchSift.Core;

internal sealed class ChainProgressTracker
{
    private readonly object sync = new();
    private readonly Action<ChainProgress>? observer;
    private readonly ChainEntryProgress[] entries;
    private long revision;
    private string stage = "preparing";
    public string Id { get; }
    public string Started { get; } = DateTimeOffset.UtcNow.ToString("O");
    public Stopwatch Timer { get; } = Stopwatch.StartNew();
    public long Preparation { get; set; }
    public long Evidence { get; set; }
    public long Evaluation { get; set; }
    private long verificationTicks;
    private long writingTicks;

    public void Verify(Action action) => Measure(action, ref verificationTicks);
    public void Write(Action action) => Measure(action, ref writingTicks);
    private static void Measure(Action action, ref long ticks)
    {
        var started = Stopwatch.GetTimestamp();
        try { action(); }
        finally { Interlocked.Add(ref ticks, Stopwatch.GetTimestamp() - started); }
    }
    public ChainTimings Timings() => new(Preparation, Evidence, Evaluation,
        (long)(Interlocked.Read(ref verificationTicks) * 1000d / Stopwatch.Frequency),
        (long)(Interlocked.Read(ref writingTicks) * 1000d / Stopwatch.Frequency), Timer.ElapsedMilliseconds);

    public ChainProgressTracker(ChainDocument chain, Action<ChainProgress>? observer, string? runId)
    {
        Id = runId ?? Guid.NewGuid().ToString("N");
        if (!System.Text.RegularExpressions.Regex.IsMatch(Id, "^[a-f0-9]{32}$")) throw new ConfigurationException("Invalid chain run identity.");
        this.observer = observer;
        entries = chain.Entries.Select(e => new ChainEntryProgress(e.EntryId, "waiting", null, null, null, null, false, null)).ToArray();
        Publish();
    }
    public void Stage(string value) { lock (sync) { stage = value; Publish(); } }
    public void Entry(int index, string value, ChainChild? child = null)
    {
        lock (sync)
        {
            var previous = entries[index];
            entries[index] = previous with
            {
                State = value, Execution = child?.Execution, Compliance = child?.Compliance,
                StartedUtc = previous.StartedUtc ?? (value is "running" or "writing" ? DateTimeOffset.UtcNow.ToString("O") : null),
                FinishedUtc = child is null ? null : DateTimeOffset.UtcNow.ToString("O"),
                OutputAvailable = child?.ReportDirectory is not null, Error = child?.Diagnostic?.Message
            };
            Publish();
        }
    }
    private void Publish()
    {
        var snapshot = entries.ToArray();
        observer?.Invoke(new(Id, ++revision, stage, Timer.ElapsedMilliseconds, snapshot.Length,
            snapshot.Count(e => e.Execution is not null), snapshot.Count(e => e.Execution is "completed" or "partial"),
            snapshot.Count(e => e.State == "skipped"), snapshot.Count(e => e.State is "running" or "writing"), snapshot));
    }
}
