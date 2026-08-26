namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>debug_launch</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record DebugLaunchResult(
    bool Success,
    SessionSummary? Session = null,
    ToolError? Error = null);
