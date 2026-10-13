using ArchSift.Contracts;

namespace ArchSift.Core;

public static class WorkbenchRequests
{
    public static HistoryQuery History(HistoryQuery request)
    {
        if (request.Offset < 0 || request.Limit is < 1 or > 100)
            throw new ConfigurationException("History offset must be nonnegative and limit must be from 1 to 100.");
        if (request.Selection?.Length > 256) throw new ConfigurationException("History selection exceeds 256 characters.");
        if (request.Operation is not (null or "analyze" or "verify" or "chain" or "changes" or "evidence-validate" or "evidence-build" or "evidence-review"))
            throw new ConfigurationException("Unknown history operation.");
        if (request.State is not (null or "running" or "completed" or "partial" or "failed" or "cancelled" or "unavailable"))
            throw new ConfigurationException("Unknown history state.");
        return request;
    }
}
