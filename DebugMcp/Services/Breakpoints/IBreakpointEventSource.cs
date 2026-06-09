namespace DebugMcp.Services.Breakpoints;

/// <summary>
/// Exposes the BreakpointResolved event for batch dispatch without coupling to IBreakpointManager operations.
/// </summary>
public interface IBreakpointEventSource
{
    /// <summary>
    /// Raised synchronously on the ICorDebug callback thread after a breakpoint hit is fully resolved
    /// (location resolved, condition evaluated, hit count incremented).
    /// Handlers run while the process is stopped — synchronous evaluation is safe.
    /// </summary>
    event EventHandler<ResolvedBreakpointHitEventArgs>? BreakpointResolved;
}
