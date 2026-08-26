namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>debug_step</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record DebugStepResult(
    bool Success,
    string? StepMode = null,
    SessionStateInfo? Session = null,
    ToolError? Error = null);
