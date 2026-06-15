namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Ensures the pinned ReSharper command-line engine is installed in the local cache,
/// acquiring it lazily on first use. Concurrency-safe and idempotent.
/// </summary>
public interface IReSharperEngineProvider
{
    /// <summary>
    /// Returns the path to a ready <c>jb</c> shim, installing the engine on first use.
    /// </summary>
    /// <exception cref="ReSharperPrerequisiteException">dotnet CLI unavailable.</exception>
    /// <exception cref="ReSharperAcquisitionException">install/download failed or cache unwritable.</exception>
    /// <exception cref="OperationCanceledException">acquisition timeout/cancellation.</exception>
    Task<EngineInstallState> EnsureEngineAsync(CancellationToken cancellationToken);
}
