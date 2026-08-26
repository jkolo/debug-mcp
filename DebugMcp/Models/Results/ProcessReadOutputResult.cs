namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>process_read_output</c>. Field names preserved from the pre-US3
/// hand-rolled JSON (FR-021). The legacy tool built three distinct anonymous-type shapes
/// depending on the requested <c>stream</c> ("stdout" | "stderr" | "both"), each omitting the
/// keys for the stream(s) not requested; here that becomes leaving the corresponding properties
/// null, which the SDK omits from the wire output the same way.</summary>
public sealed record ProcessReadOutputResult(
    bool Success,
    string? Stdout = null,
    string? Stderr = null,
    int? StdoutBytes = null,
    int? StderrBytes = null,
    ToolError? Error = null);
