using DebugMcp.Models.Breakpoints;

namespace DebugMcp.Services.Breakpoints;

/// <summary>
/// Service for sending MCP notifications when breakpoints are hit.
/// </summary>
public interface IBreakpointNotifier
{
    /// <summary>
    /// Sends an MCP notification for a breakpoint hit event.
    /// Fire-and-forget pattern - failures are logged but don't affect debuggee.
    /// </summary>
    /// <param name="notification">The notification payload to send.</param>
    Task SendBreakpointHitAsync(BreakpointNotification notification);
}
