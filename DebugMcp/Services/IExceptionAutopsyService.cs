using DebugMcp.Models.Inspection;

namespace DebugMcp.Services;

/// <summary>
/// Service for collecting bundled exception context when the debugger is paused at an exception.
/// </summary>
public interface IExceptionAutopsyService
{
    /// <summary>
    /// Collects full exception context: exception details, inner exception chain,
    /// stack frames with source locations, and local variables for top frames.
    /// </summary>
    /// <param name="maxFrames">Maximum stack frames to return (default: 10).</param>
    /// <param name="includeVariablesForFrames">Number of top frames to include local variables for (default: 1).</param>
    /// <param name="maxInnerExceptions">Maximum inner exception chain depth (default: 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bundled exception context.</returns>
    /// <exception cref="InvalidOperationException">Thrown when debugger is not paused at an exception.</exception>
    Task<ExceptionAutopsyResult> GetExceptionContextAsync(
        int maxFrames = 10,
        int includeVariablesForFrames = 1,
        int maxInnerExceptions = 5,
        CancellationToken cancellationToken = default);
}
