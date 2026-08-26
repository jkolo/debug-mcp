namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>debug_attach</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record DebugAttachResult(
    bool Success,
    SessionSummary? Session = null,
    ToolError? Error = null);

/// <summary>
/// The "session" envelope shared by <c>debug_attach</c> and <c>debug_launch</c>. Launch-only
/// fields (<see cref="PauseReason"/>, <see cref="CommandLineArgs"/>, <see cref="WorkingDirectory"/>)
/// stay null (and therefore omitted on the wire) for <c>debug_attach</c>, which never set them in
/// the legacy anonymous-object response either.
/// </summary>
public sealed record SessionSummary(
    int ProcessId,
    string ProcessName,
    string ExecutablePath,
    string RuntimeVersion,
    string State,
    string LaunchMode,
    string AttachedAt,
    string? PauseReason = null,
    string[]? CommandLineArgs = null,
    string? WorkingDirectory = null);
