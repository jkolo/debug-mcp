namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>process_write_input</c>. Field names preserved from the pre-US3
/// hand-rolled JSON (FR-021).</summary>
public sealed record ProcessWriteInputResult(
    bool Success,
    int? BytesWritten = null,
    bool? StdinClosed = null,
    ToolError? Error = null);
