namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>tracepoint_set</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record TracepointSetResult(
    bool Success,
    TracepointInfo? Tracepoint = null,
    ToolError? Error = null);

/// <summary>Wire shape for the nested tracepoint object (pre-US3 <c>SerializeTracepoint</c> helper).</summary>
public sealed record TracepointInfo(
    string Id,
    string Type,
    TracepointLocation Location,
    string State,
    bool Enabled,
    string? LogMessage = null,
    int? HitCountMultiple = null,
    int? MaxNotifications = null);

/// <summary>
/// Wire shape for a tracepoint's location — 5 fields only (no <c>endLine</c>/<c>endColumn</c>),
/// distinct from <see cref="DebugMcp.Models.Breakpoints.BreakpointLocation"/> which
/// breakpoint_set/breakpoint_enable use (pre-US3 <c>SerializeTracepoint</c> never emitted those
/// two fields, unlike <c>SerializeBreakpoint</c>).
/// </summary>
public sealed record TracepointLocation(
    string File,
    int Line,
    int? Column = null,
    string? FunctionName = null,
    string? ModuleName = null);
