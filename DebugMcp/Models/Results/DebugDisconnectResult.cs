namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>debug_disconnect</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
/// <remarks>
/// The legacy "no active session" branch always emitted <c>previousSession: null</c> (a literal
/// null, not an absent key) because it built the response as one fixed anonymous-object shape.
/// With <see cref="PreviousSession"/> left null, the SDK's null-omission means the key is now
/// absent rather than present-with-null in that one case — flagged per the pattern doc rather
/// than treated as an exact reproduction.
/// </remarks>
public sealed record DebugDisconnectResult(
    bool Success,
    string? State = null,
    string? Message = null,
    bool? WasTerminated = null,
    bool? TimedOut = null,
    PreviousSessionInfo? PreviousSession = null,
    ToolError? Error = null);

public sealed record PreviousSessionInfo(int ProcessId, string ProcessName, string LaunchMode);
