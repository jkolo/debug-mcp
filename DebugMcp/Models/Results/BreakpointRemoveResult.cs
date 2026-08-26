namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>breakpoint_remove</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record BreakpointRemoveResult(
    bool Success,
    string? Message = null,
    ToolError? Error = null);
