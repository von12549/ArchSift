using System.Diagnostics;

namespace ArchSift.Core;

public sealed record EvidenceHashStatistics(long FileReads, long BytesRead, long HashMilliseconds);
public sealed class EvidenceHashTelemetry : IDisposable
{
    private static readonly AsyncLocal<EvidenceHashTelemetry?> current = new();
    private readonly EvidenceHashTelemetry? previous;
    private long files, bytes, ticks;
    public EvidenceHashTelemetry() { previous = current.Value; current.Value = this; }
    public EvidenceHashStatistics Statistics => new(files, bytes, (long)(ticks * 1000d / Stopwatch.Frequency));
    internal static void Record(long length, long elapsedTicks)
    {
        if (current.Value is not { } scope) return;
        Interlocked.Increment(ref scope.files); Interlocked.Add(ref scope.bytes, length); Interlocked.Add(ref scope.ticks, elapsedTicks);
    }
    public void Dispose() => current.Value = previous;
}
