namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>breakpoint_enable</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record BreakpointEnableResult(
    bool Success,
    BreakpointInfo? Breakpoint = null,
    ToolError? Error = null);
