namespace DebugMcp.Models.Breakpoints;

/// <summary>
/// Payload sent via MCP notification when a breakpoint or tracepoint fires.
/// </summary>
/// <param name="BreakpointId">ID of the triggered breakpoint/tracepoint.</param>
/// <param name="Type">Whether blocking or tracepoint.</param>
/// <param name="Location">File, line, function info.</param>
/// <param name="ThreadId">Thread that hit the breakpoint.</param>
/// <param name="Timestamp">When the hit occurred.</param>
/// <param name="HitCount">Total times this breakpoint has been hit.</param>
/// <param name="LogMessage">Evaluated log message (tracepoints only).</param>
/// <param name="ExceptionInfo">For exception breakpoints.</param>
public record BreakpointNotification(
    string BreakpointId,
    BreakpointType Type,
    NotificationLocation Location,
    int ThreadId,
    DateTimeOffset Timestamp,
    int HitCount,
    string? LogMessage = null,
    ExceptionInfo? ExceptionInfo = null);

/// <summary>
/// Location information included in breakpoint notifications.
/// </summary>
/// <param name="File">Absolute path to source file.</param>
/// <param name="Line">1-based line number.</param>
/// <param name="Column">1-based column number (if available).</param>
/// <param name="FunctionName">Name of the function containing the breakpoint.</param>
/// <param name="ModuleName">Name of the module/assembly.</param>
public record NotificationLocation(
    string File,
    int Line,
    int? Column = null,
    string? FunctionName = null,
    string? ModuleName = null);
