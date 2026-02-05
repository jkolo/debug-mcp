namespace DebugMcp.Models.Breakpoints;

/// <summary>
/// Represents a debugging pause point or tracepoint in source code.
/// </summary>
/// <param name="Id">Unique identifier (UUID format).</param>
/// <param name="Location">Source location (file, line, optional column).</param>
/// <param name="State">Current lifecycle state (pending/bound/disabled).</param>
/// <param name="Enabled">User-controlled enable flag.</param>
/// <param name="Verified">True if bound to executable code.</param>
/// <param name="HitCount">Number of times breakpoint has been hit.</param>
/// <param name="Type">Whether this is a blocking breakpoint or tracepoint.</param>
/// <param name="Condition">Optional condition expression (C# syntax).</param>
/// <param name="Message">Status message (e.g., why unverified).</param>
/// <param name="LogMessage">Template with {expression} placeholders for tracepoints.</param>
/// <param name="HitCountMultiple">Notify only every Nth hit (0 = every hit).</param>
/// <param name="MaxNotifications">Auto-disable after N notifications (0 = unlimited).</param>
/// <param name="NotificationsSent">Count of notifications sent (for max limit).</param>
public record Breakpoint(
    string Id,
    BreakpointLocation Location,
    BreakpointState State,
    bool Enabled,
    bool Verified,
    int HitCount,
    BreakpointType Type = BreakpointType.Blocking,
    string? Condition = null,
    string? Message = null,
    string? LogMessage = null,
    int HitCountMultiple = 0,
    int MaxNotifications = 0,
    int NotificationsSent = 0);
