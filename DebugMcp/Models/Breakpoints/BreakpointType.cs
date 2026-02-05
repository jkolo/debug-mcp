namespace DebugMcp.Models.Breakpoints;

/// <summary>
/// Distinguishes blocking breakpoints from non-blocking tracepoints.
/// </summary>
public enum BreakpointType
{
    /// <summary>Traditional breakpoint - pauses execution, waitable via breakpoint_wait.</summary>
    Blocking = 0,

    /// <summary>Observation point - sends notification, continues execution immediately.</summary>
    Tracepoint = 1
}
