using DebugMcp.Models.Batch;
using DebugMcp.Services.Progress;

namespace DebugMcp.Services.Batch;

public interface IBatchRunner
{
    /// <summary>
    /// Runs the batch from the agent's perspective (awaitable).
    /// Returns when all experiments trigger, timeout expires, process exits, or cancellation is requested.
    /// </summary>
    /// <param name="progress">Optional stage reporter (feature 069, US1): "experiment triggered n of m". Null is always safe.</param>
    Task<BatchResult> RunAsync(BatchRequest request, CancellationToken cancellationToken = default, IProgressReporter? progress = null);

    /// <summary>True if a batch is currently running.</summary>
    bool IsRunning { get; }
}
