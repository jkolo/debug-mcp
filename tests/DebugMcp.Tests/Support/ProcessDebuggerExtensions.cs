using DebugMcp.Models;
using DebugMcp.Services;

namespace DebugMcp.Tests.Support;

/// <summary>
/// Extension methods for test synchronization with IProcessDebugger.
/// Replaces the removed WaitForBreakpointAsync polling mechanism.
/// </summary>
public static class ProcessDebuggerExtensions
{
    /// <summary>
    /// Waits for the debugger to reach a Paused state (breakpoint, step, or exception hit).
    /// Returns the active thread ID when paused (or null if unknown).
    /// Use this in integration tests instead of the removed WaitForBreakpointAsync.
    /// </summary>
    public static async Task<int?> WaitForPauseAsync(this IProcessDebugger debugger, TimeSpan timeout)
    {
        if (debugger.CurrentState == SessionState.Paused)
            return debugger.ActiveThreadId;

        var tcs = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<SessionStateChangedEventArgs>? handler = null;

        handler = (_, e) =>
        {
            if (e.NewState == SessionState.Paused)
            {
                debugger.StateChanged -= handler;
                tcs.TrySetResult(e.ThreadId);
            }
        };

        debugger.StateChanged += handler;

        // Re-check after subscribing to avoid race
        if (debugger.CurrentState == SessionState.Paused)
        {
            debugger.StateChanged -= handler;
            return debugger.ActiveThreadId;
        }

        return await tcs.Task.WaitAsync(timeout);
    }
}
