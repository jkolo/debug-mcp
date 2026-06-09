using DebugMcp.Models.Batch;

namespace DebugMcp.Services.Batch;

public interface IBatchRunner
{
    /// <summary>
    /// Runs the batch from the agent's perspective (awaitable).
    /// Returns when all experiments trigger, timeout expires, process exits, or cancellation is requested.
    /// </summary>
    Task<BatchResult> RunAsync(BatchRequest request, CancellationToken cancellationToken = default);

    /// <summary>True if a batch is currently running.</summary>
    bool IsRunning { get; }
}
