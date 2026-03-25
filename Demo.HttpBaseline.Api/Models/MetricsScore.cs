namespace Demo.HttpBaseline.Api.Models;

public sealed class MetricsStore
{
    private long _totalRequests;
    private long _completedRequests;
    private long _failedRequests;
    private long _currentlyRunning;
    private long _totalElapsedMs;

    public void RequestStarted()
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _currentlyRunning);
    }

    public void RequestCompleted(long elapsedMs)
    {
        Interlocked.Increment(ref _completedRequests);
        Interlocked.Add(ref _totalElapsedMs, elapsedMs);
        Interlocked.Decrement(ref _currentlyRunning);
    }

    public void RequestFailed()
    {
        Interlocked.Increment(ref _failedRequests);
        Interlocked.Decrement(ref _currentlyRunning);
    }

    public MetricsSnapshot GetSnapshot()
    {
        var totalRequests = Interlocked.Read(ref _totalRequests);
        var completedRequests = Interlocked.Read(ref _completedRequests);
        var failedRequests = Interlocked.Read(ref _failedRequests);
        var currentlyRunning = Interlocked.Read(ref _currentlyRunning);
        var totalElapsedMs = Interlocked.Read(ref _totalElapsedMs);

        double averageElapsedMs = completedRequests == 0
            ? 0
            : (double)totalElapsedMs / completedRequests;

        return new MetricsSnapshot(
            TotalRequests: totalRequests,
            CompletedRequests: completedRequests,
            FailedRequests: failedRequests,
            CurrentlyRunning: currentlyRunning,
            TotalElapsedMs: totalElapsedMs,
            AverageElapsedMs: averageElapsedMs
        );
    }
}

public sealed record MetricsSnapshot(
    long TotalRequests,
    long CompletedRequests,
    long FailedRequests,
    long CurrentlyRunning,
    long TotalElapsedMs,
    double AverageElapsedMs
);