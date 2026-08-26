namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>debug_continue</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record DebugContinueResult(
    bool Success,
    SessionStateInfo? Session = null,
    ToolError? Error = null);

/// <summary>
/// The compact "session" envelope shared by <c>debug_continue</c>, <c>debug_step</c>, and
/// <c>debug_pause</c> (distinct from <see cref="SessionSummary"/>, which is the richer envelope
/// used by <c>debug_attach</c>/<c>debug_launch</c>). <see cref="PauseReason"/>,
/// <see cref="Location"/>, and <see cref="ActiveThreadId"/> are only ever set when the underlying
/// session is Paused, matching the legacy conditional dictionary keys exactly.
/// </summary>
public sealed record SessionStateInfo(
    int ProcessId,
    string ProcessName,
    string State,
    string LaunchMode,
    string? PauseReason = null,
    LocationInfo? Location = null,
    int? ActiveThreadId = null);

/// <summary>
/// The "location" object nested under a paused <see cref="SessionStateInfo"/>. The legacy code
/// built this as one fixed anonymous object whenever <c>CurrentLocation</c> was non-null, with
/// all four sub-fields unconditionally present — including a literal <c>column: null</c> when
/// <see cref="Column"/> was unset. With <see cref="Column"/> left null here, the SDK's
/// null-omission means that one sub-field is now absent rather than present-with-null when no
/// column is known, matching the same literal-null-vs-omitted gap flagged for
/// <see cref="SessionSummary"/> and <see cref="DebugDisconnectResult"/>.
/// </summary>
public sealed record LocationInfo(string File, int Line, int? Column = null, string? FunctionName = null);
