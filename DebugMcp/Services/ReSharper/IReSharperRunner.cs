namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Runs <c>jb inspectcode</c> for a request and returns the raw inspection report (XML) text.
/// This is the seam faked in service unit tests (no real process spawned).
/// </summary>
public interface IReSharperRunner
{
    /// <summary>
    /// Executes the engine and returns the report document text.
    /// </summary>
    /// <exception cref="ReSharperBuildFailedException">pre-analysis build failed.</exception>
    /// <exception cref="ReSharperRunFailedException">engine crashed or exited non-zero.</exception>
    /// <exception cref="OperationCanceledException">inspection timeout/cancellation.</exception>
    Task<string> RunInspectCodeAsync(InspectionRunRequest request, string jbPath, CancellationToken cancellationToken);
}
