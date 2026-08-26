using DebugMcp.Models.Breakpoints;

namespace DebugMcp.Models.Results;

/// <summary>
/// Wire shape for <c>breakpoint_set_exception</c>. Field names preserved from the pre-US3
/// hand-rolled JSON (FR-021). <see cref="Breakpoint"/> reuses the domain
/// <see cref="ExceptionBreakpoint"/> record directly — its fields already match the wire
/// shape 1:1 under camelCase (pre-US3 <c>SerializeExceptionBreakpoint</c> helper).
/// </summary>
public sealed record BreakpointSetExceptionResult(
    bool Success,
    ExceptionBreakpoint? Breakpoint = null,
    string? Note = null,
    ToolError? Error = null);
