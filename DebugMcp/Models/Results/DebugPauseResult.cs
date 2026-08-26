namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>debug_pause</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record DebugPauseResult(
    bool Success,
    SessionStateInfo? Session = null,
    IReadOnlyList<PauseThreadInfo>? Threads = null,
    ToolError? Error = null);

/// <summary>One entry of the "threads" array in a <c>debug_pause</c> response.</summary>
public sealed record PauseThreadInfo(int Id, PauseThreadLocation? Location = null);

/// <summary>
/// The "location" object nested under a <see cref="PauseThreadInfo"/>. Note this shape differs
/// from <see cref="LocationInfo"/> (session-level location): the legacy code fell back to
/// "Unknown" for a missing function name instead of omitting the field, and omitted
/// <see cref="File"/>/<see cref="Line"/> individually based on truthiness (non-empty file,
/// line &gt; 0) rather than nullness — both reproduced here via the tool's construction logic.
/// </summary>
public sealed record PauseThreadLocation(string Function, string? File = null, int? Line = null);
