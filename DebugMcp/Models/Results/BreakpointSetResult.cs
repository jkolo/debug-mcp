using DebugMcp.Models.Breakpoints;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>breakpoint_set</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record BreakpointSetResult(
    bool Success,
    BreakpointInfo? Breakpoint = null,
    bool? Duplicate = null,
    ToolError? Error = null);

/// <summary>
/// Wire shape for a breakpoint object as returned by <c>breakpoint_set</c> and
/// <c>breakpoint_enable</c> (identical shape in both tools' pre-US3 <c>SerializeBreakpoint</c> helper).
/// </summary>
public sealed record BreakpointInfo(
    string Id,
    BreakpointLocation Location,
    string State,
    bool Enabled,
    bool Verified,
    int HitCount,
    string? Condition = null,
    string? Message = null);
