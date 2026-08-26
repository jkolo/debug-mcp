namespace DebugMcp.Services.Progress;

/// <summary>
/// Wraps an opaque long-running call (an external process, an ICorDebug-bound wait) with a
/// periodic re-emission of its current stage, satisfying SC-001's 60-second silence ceiling for
/// operations with no natural sub-progress to report — the ReSharper engine acquisition, the
/// <c>jb inspectcode</c> run, and the <c>debug_launch</c> wait (see data-model.md §4).
/// </summary>
public static class HeartbeatProgress
{
    /// <summary>Default heartbeat interval — comfortably under SC-001's 60s ceiling.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Reports <paramref name="stage"/> once, then re-reports it every <paramref name="interval"/>
    /// until <paramref name="work"/> completes. A null <paramref name="progress"/> is always safe
    /// (FR-005) — <paramref name="work"/> still runs, nothing is reported.
    /// </summary>
    public static async Task<T> RunAsync<T>(
        Func<Task<T>> work,
        IProgressReporter? progress,
        string stage,
        TimeSpan? interval = null,
        CancellationToken cancellationToken = default)
    {
        progress?.ReportStage(stage);

        if (progress is null)
        {
            return await work().ConfigureAwait(false);
        }

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatTask = HeartbeatLoopAsync(progress, stage, interval ?? DefaultInterval, heartbeatCts.Token);

        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            heartbeatCts.Cancel();
            await heartbeatTask.ConfigureAwait(false);
        }
    }

    private static async Task HeartbeatLoopAsync(
        IProgressReporter progress, string stage, TimeSpan interval, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                progress.ReportStage(stage);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected: RunAsync cancels this loop once `work` completes.
        }
    }
}
